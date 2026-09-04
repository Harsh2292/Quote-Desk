using System.ClientModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using QuoteDesk.Agents.Checkpointing;
using QuoteDesk.Agents.Llm;
using QuoteDesk.Agents.Prompts;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// The façade task 07 calls — everything above the HTTP layer. A fresh <see cref="Workflow"/> (fresh
/// executor instances, fresh <see cref="TokenUsageTracker"/>) is built for every call to
/// <see cref="StartAsync"/>, <see cref="ResumeAsync"/>, or <see cref="ProcessAsync"/>, including a
/// resume after a real restart — nothing about a suspended run depends on any object still living in
/// this process's memory. <see cref="ProcessAsync"/> is what <c>POST /api/enquiries/{id}/process</c>
/// actually calls — it transparently resumes a failed run past Resolve when possible, rather than
/// always restarting via <see cref="StartAsync"/> directly.
/// </summary>
public sealed partial class EnquiryPipeline(
    IEnquiryRepository enquiries,
    IAgentRunRepository agentRuns,
    ReadToolRegistry readTools,
    PricingTools pricingTools,
    QuoteWriteTools writeTools,
    IQuoteRepository quotes,
    ICatalogRepository catalog,
    ICustomerRepository customers,
    ChatClientRegistry chatClients,
    PromptLibrary prompts,
    LlmOptions options,
    SqlCheckpointStore checkpointStore,
    TimeProvider timeProvider,
    ILogger<EnquiryPipeline> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<AgentEvent> StartAsync(int enquiryId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var enquiry = await enquiries.GetByIdAsync(enquiryId, cancellationToken);
        if (enquiry is null)
        {
            yield return new ErrorEvent { Code = "internal", Message = $"Enquiry {enquiryId} does not exist." };
            yield break;
        }

        var sessionId = $"enquiry-{enquiryId}-{Guid.NewGuid():N}";
        var run = await agentRuns.CreateAsync(
            new NewAgentRun(enquiryId, sessionId, AgentRunStatuses.Running, timeProvider.GetUtcNow(), enquiry.OwnerUserId),
            cancellationToken);

        var tokens = new TokenUsageTracker(options.TokenBudget);
        var workflow = BuildWorkflow(tokens);
        var checkpointManager = CheckpointManager.CreateJson(checkpointStore, JsonOptions);
        var enquiryInput = new EnquiryInput(enquiry.Id, enquiry.SenderId, enquiry.RawBody, enquiry.ReceivedAt);

        StreamingRun? streamingRun = null;
        ErrorEvent? startError = null;
        try
        {
            streamingRun = await InProcessExecution.RunStreamingAsync(workflow, enquiryInput, checkpointManager, sessionId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            startError = ToErrorEvent(ex);
        }

        if (startError is not null)
        {
            await agentRuns.UpdateStatusAsync(run.Id, AgentRunStatuses.Failed, null, timeProvider.GetUtcNow(), tokens.PromptTokens, tokens.CompletionTokens, cancellationToken);
            yield return startError;
            yield break;
        }

        await using (streamingRun)
        {
            await foreach (var evt in RunAndTranslateAsync(streamingRun!, run.Id, tokens, pendingAnswer: null, cancellationToken))
            {
                yield return evt;
            }
        }
    }

    public async IAsyncEnumerable<AgentEvent> ResumeAsync(
        int enquiryId, ApprovalDecision decision, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var run = await agentRuns.GetLatestByEnquiryIdAsync(enquiryId, cancellationToken);
        if (run is null || run.Status != AgentRunStatuses.PendingApproval)
        {
            yield return new ErrorEvent { Code = "internal", Message = $"Enquiry {enquiryId} has no pending approval to resume." };
            yield break;
        }

        // Seeded from what StartAsync's own tracker already recorded before this run suspended — its
        // instance is long gone (a completed HTTP request's local state), so the only way this leg's
        // DoneEvent can ever report the true cumulative usage is by picking the total back up from
        // where AgentRuns.UpdateStatusAsync last persisted it.
        var tokens = new TokenUsageTracker(options.TokenBudget, run.PromptTokens ?? 0, run.CompletionTokens ?? 0);
        var workflow = BuildWorkflow(tokens);
        var checkpointManager = CheckpointManager.CreateJson(checkpointStore, JsonOptions);

        var checkpoint = await checkpointManager.GetLatestCheckpointAsync(run.SessionId, cancellationToken);
        if (checkpoint is null)
        {
            yield return new ErrorEvent { Code = "internal", Message = $"No checkpoint found for session '{run.SessionId}'." };
            yield break;
        }

        StreamingRun? streamingRun = null;
        ErrorEvent? resumeError = null;
        try
        {
            streamingRun = await InProcessExecution.ResumeStreamingAsync(workflow, checkpoint, checkpointManager, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            resumeError = ToErrorEvent(ex);
        }

        if (resumeError is not null)
        {
            yield return resumeError;
            yield break;
        }

        await using (streamingRun)
        {
            await foreach (var evt in RunAndTranslateAsync(streamingRun!, run.Id, tokens, decision, cancellationToken))
            {
                yield return evt;
            }
        }
    }

    /// <summary>
    /// What <c>POST /api/enquiries/{id}/process</c> actually calls. Transparently resumes a failed
    /// run from its last good checkpoint when Resolve already succeeded — the expensive,
    /// quota-scarce step — instead of always restarting from Extract the way a bare
    /// <see cref="StartAsync"/> call does. Falls through to a normal fresh <see cref="StartAsync"/>
    /// for every other case: no prior run, a prior run that isn't Failed, or one that failed before
    /// Resolve finished (nothing worth resuming past there — Extract is cheap and Resolve has to run
    /// either way, so "resuming" would save almost nothing). The trace panel shows this honestly: a
    /// resumed run's events pick up directly at "price", visibly skipping fresh extract/resolve
    /// stages — no separate UI affordance exists, or is needed, to say which happened.
    /// </summary>
    public async IAsyncEnumerable<AgentEvent> ProcessAsync(int enquiryId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var resumable = await FindResumableFailedRunAsync(enquiryId, cancellationToken);
        if (resumable is not null)
        {
            await foreach (var evt in ResumeFailedAsync(resumable, cancellationToken))
            {
                yield return evt;
            }

            yield break;
        }

        await foreach (var evt in StartAsync(enquiryId, cancellationToken))
        {
            yield return evt;
        }
    }

    /// <summary>Eligible only when the latest run for this enquiry failed <i>after</i> Resolve
    /// completed — found from the persisted trace's last <see cref="StageEvent"/>, the same
    /// deserialize-and-inspect pattern <c>EnquiryEndpoints.GetByIdAsync</c> already uses — and a
    /// checkpoint genuinely exists for it. Returns null (meaning "start fresh") for everything else,
    /// including a missing or corrupt trace: this is a convenience shortcut, never the only way to
    /// make progress.
    ///
    /// Checks for last stage <c>"price"</c>, not <c>"resolve"</c> — <see cref="ResolveExecutor"/>
    /// emits its own <see cref="StageEvent"/> the moment Resolve <i>starts</i>, not once it
    /// completes, so "resolve" is also the last stage seen when Resolve itself is what failed. A
    /// "price" stage event, by contrast, can only exist if Resolve already finished and control
    /// passed to Price — which is exactly the case worth resuming past.</summary>
    private async Task<AgentRunRecord?> FindResumableFailedRunAsync(int enquiryId, CancellationToken cancellationToken)
    {
        var run = await agentRuns.GetLatestByEnquiryIdAsync(enquiryId, cancellationToken);
        if (run is null || run.Status != AgentRunStatuses.Failed || run.TraceJson is null)
        {
            return null;
        }

        var trace = JsonSerializer.Deserialize<List<AgentEvent>>(run.TraceJson, JsonOptions);
        var lastStage = trace?.OfType<StageEvent>().LastOrDefault();
        if (lastStage?.Stage != "price")
        {
            return null;
        }

        var checkpointManager = CheckpointManager.CreateJson(checkpointStore, JsonOptions);
        var checkpoint = await checkpointManager.GetLatestCheckpointAsync(run.SessionId, cancellationToken);
        return checkpoint is not null ? run : null;
    }

    /// <summary>Resumes <paramref name="run"/> — already confirmed Failed, past Resolve, with a real
    /// checkpoint — from that checkpoint. Reuses the same <c>AgentRun</c> row rather than creating a
    /// new one: resuming a checkpoint necessarily keeps its original SessionId (there is no framework
    /// parameter to graft it onto a new one), and <c>AgentRuns.SessionId</c> has a unique index, so a
    /// second row could never carry it anyway. <c>pendingAnswer: null</c> into
    /// <see cref="RunAndTranslateAsync"/> is correct here the same way it is for a genuine first run:
    /// Price hasn't reached the approval port yet, so there is no decision to inject — if it reaches
    /// that port again, it suspends fresh, exactly like a normal run would.</summary>
    private async IAsyncEnumerable<AgentEvent> ResumeFailedAsync(AgentRunRecord run, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tokens = new TokenUsageTracker(options.TokenBudget, run.PromptTokens ?? 0, run.CompletionTokens ?? 0);
        var workflow = BuildWorkflow(tokens);
        var checkpointManager = CheckpointManager.CreateJson(checkpointStore, JsonOptions);
        var checkpoint = await checkpointManager.GetLatestCheckpointAsync(run.SessionId, cancellationToken);

        if (checkpoint is null)
        {
            // FindResumableFailedRunAsync just confirmed one exists — this would mean it vanished in
            // the narrow window between that check and this one. Report it plainly rather than
            // guessing at a recovery; the next "Retry" click re-runs the same eligibility check and
            // correctly starts fresh once it sees no checkpoint at all.
            yield return new ErrorEvent { Code = "internal", Message = $"No checkpoint found for session '{run.SessionId}'." };
            yield break;
        }

        await agentRuns.UpdateStatusAsync(run.Id, AgentRunStatuses.Running, null, timeProvider.GetUtcNow(), tokens.PromptTokens, tokens.CompletionTokens, cancellationToken);

        StreamingRun? streamingRun = null;
        ErrorEvent? resumeError = null;
        try
        {
            streamingRun = await InProcessExecution.ResumeStreamingAsync(workflow, checkpoint, checkpointManager, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            resumeError = ToErrorEvent(ex);
        }

        if (resumeError is not null)
        {
            await agentRuns.UpdateStatusAsync(run.Id, AgentRunStatuses.Failed, null, timeProvider.GetUtcNow(), tokens.PromptTokens, tokens.CompletionTokens, cancellationToken);
            yield return resumeError;
            yield break;
        }

        await using (streamingRun)
        {
            await foreach (var evt in RunAndTranslateAsync(streamingRun!, run.Id, tokens, pendingAnswer: null, cancellationToken))
            {
                yield return evt;
            }
        }
    }

    private WorkflowNodes BuildNodes(TokenUsageTracker tokens)
    {
        // Each stage is wrapped in its own BudgetedChatClient over that stage's own model client
        // (ChatClientRegistry: Extract/Narrate on the cheap high-quota model, Resolve on the capable
        // one — docs/SPEC.md §4), all three sharing this run's one TokenUsageTracker so the budget is
        // still enforced per round-trip across every model in play, not per model.
        var extractModel = options.ExtractModel ?? options.Model;
        var resolveModel = options.ResolveModel ?? options.Model;
        var narrateModel = options.NarrateModel ?? options.Model;

        var extractClient = new BudgetedChatClient(chatClients.Extract, tokens);
        var resolveClient = new BudgetedChatClient(chatClients.Resolve, tokens);
        var narrateClient = new BudgetedChatClient(chatClients.Narrate, tokens);

        var extractAgent = extractClient.AsAIAgent(instructions: prompts.Extract, name: "Extract", description: null, tools: null);
        var narrateAgent = narrateClient.AsAIAgent(instructions: prompts.Narrate, name: "Narrate", description: null, tools: null);

        // price_quote is deliberately excluded: Resolve gets only the four lookup tools, so pricing
        // is never something the model can call — it is the Price node's job, in plain code.
        var lookupTools = readTools.Tools.Where(t => t.Name != "price_quote").ToList();

        return new WorkflowNodes(
            new ExtractExecutor("Extract", extractAgent, extractModel, options.UseStructuredOutput, logger),
            new ResolveExecutor("Resolve", resolveClient, resolveModel, lookupTools, prompts.Resolve, options.MaxToolCalls, catalog, customers, logger),
            new PriceExecutor("Price", pricingTools, narrateAgent, narrateModel),
            new ApproveExecutor("Approve", writeTools, quotes, timeProvider));
    }

    private Workflow BuildWorkflow(TokenUsageTracker tokens) => QuoteDeskWorkflow.Build(BuildNodes(tokens));

    /// <summary>
    /// Drives one <see cref="StreamingRun"/>'s event stream to completion or suspension, translating
    /// framework events into <see cref="AgentEvent"/>s. Shared by <see cref="StartAsync"/> (no
    /// decision yet — stops and persists at the first request) and <see cref="ResumeAsync"/> (a
    /// decision in hand — answers the request the moment it is republished, then keeps going).
    /// </summary>
    private async IAsyncEnumerable<AgentEvent> RunAndTranslateAsync(
        StreamingRun run,
        int agentRunId,
        TokenUsageTracker tokens,
        ApprovalDecision? pendingAnswer,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var answered = false;
        var enumerator = run.WatchStreamAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                WorkflowEvent? current = null;
                ErrorEvent? loopError = null;
                var hasMore = false;

                try
                {
                    hasMore = await enumerator.MoveNextAsync();
                    if (hasMore)
                    {
                        current = enumerator.Current;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    loopError = ToErrorEvent(ex);
                }

                if (loopError is not null)
                {
                    await agentRuns.UpdateStatusAsync(agentRunId, AgentRunStatuses.Failed, null, timeProvider.GetUtcNow(), tokens.PromptTokens, tokens.CompletionTokens, cancellationToken);
                    yield return loopError;
                    yield break;
                }

                if (!hasMore)
                {
                    yield break;
                }

                switch (current)
                {
                    case AgentTraceEvent trace:
                        yield return trace.Event;
                        break;

                    case RequestInfoEvent info when pendingAnswer is not null && !answered:
                        answered = true;
                        await run.SendResponseAsync(info.Request.CreateResponse(pendingAnswer));
                        break;

                    case RequestInfoEvent info when pendingAnswer is null:
                        if (info.Request.TryGetDataAs<ApprovalRequest>(out var approvalRequest))
                        {
                            var stored = JsonSerializer.Serialize(new StoredApproval(info.Request.RequestId, approvalRequest), JsonOptions);
                            await agentRuns.UpdateStatusAsync(agentRunId, AgentRunStatuses.PendingApproval, stored, timeProvider.GetUtcNow(), tokens.PromptTokens, tokens.CompletionTokens, cancellationToken);
                            yield return new ApprovalRequiredEvent
                            {
                                ApprovalId = agentRunId.ToString(CultureInfo.InvariantCulture),
                                Action = "approve_quote",
                                Payload = approvalRequest,
                            };
                        }

                        yield break;

                    case ExecutorFailedEvent { Data: { } executorException }:
                        await agentRuns.UpdateStatusAsync(agentRunId, AgentRunStatuses.Failed, null, timeProvider.GetUtcNow(), tokens.PromptTokens, tokens.CompletionTokens, cancellationToken);
                        yield return ToErrorEvent(executorException);
                        yield break;

                    case WorkflowErrorEvent { Exception: { } workflowException }:
                        await agentRuns.UpdateStatusAsync(agentRunId, AgentRunStatuses.Failed, null, timeProvider.GetUtcNow(), tokens.PromptTokens, tokens.CompletionTokens, cancellationToken);
                        yield return ToErrorEvent(workflowException);
                        yield break;

                    case WorkflowOutputEvent output:
                        var result = output.As<PipelineResult>();
                        var status = result?.Success == true ? AgentRunStatuses.Completed : AgentRunStatuses.Rejected;
                        await agentRuns.UpdateStatusAsync(agentRunId, status, null, timeProvider.GetUtcNow(), tokens.PromptTokens, tokens.CompletionTokens, cancellationToken);
                        yield return new DoneEvent
                        {
                            At = timeProvider.GetUtcNow(),
                            Usage = new UsageInfo
                            {
                                PromptTokens = (int)Math.Min(tokens.PromptTokens, int.MaxValue),
                                CompletionTokens = (int)Math.Min(tokens.CompletionTokens, int.MaxValue),
                            },
                        };
                        yield break;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    private ErrorEvent ToErrorEvent(Exception ex)
    {
        // Log the real exception — type, message, stack — every time, so the next failure is
        // diagnosable from the server log rather than by reading AgentRuns.TraceJson by hand. The
        // client only ever sees the shaped ErrorEvent below, never this.
        LogPipelineRunFailed(logger, ex, ex.GetType().FullName);

        return ex switch
        {
            // Two exception types map to the same provider_rate_limited code because the two
            // providers this pipeline can be built against (docs/SPEC.md §4) throw different ones for
            // the exact same condition: ClientResultException from the OpenAI-compatible client
            // ("github" profile, and "gemini" before Google.GenAI was adopted), Google.GenAI.ClientError
            // from Google's native SDK ("gemini" profile now).
            ClientResultException { Status: 429 } or Google.GenAI.ClientError { StatusCode: 429 } =>
                new ErrorEvent { Code = "provider_rate_limited", Message = "The model provider is rate-limiting requests right now." },

            BudgetExceededException budgetEx =>
                new ErrorEvent { Code = "budget_exceeded", Message = budgetEx.Message },

            // A provider 400/413 whose message mentions tokens or context length is the enquiry
            // overwhelming the model's input window — the failure mode that took down task 08's first
            // live run. Report it as budget_exceeded (same "too big to process" family), not a bare
            // internal error.
            ClientResultException { Status: 400 or 413 } cre when MentionsContextLimit(cre.Message) =>
                new ErrorEvent { Code = "budget_exceeded", Message = "The enquiry produced too much context for the model to process." },
            Google.GenAI.ClientError { StatusCode: 400 } ge when MentionsContextLimit(ge.Message) =>
                new ErrorEvent { Code = "budget_exceeded", Message = "The enquiry produced too much context for the model to process." },

            // Found live: Google.GenAI.ServerError is a *distinct* type from ClientError — the 5xx
            // family ("This model is currently experiencing high demand...") rather than the 429
            // family — and fell through to the generic "internal" bucket before this case existed,
            // which is actively misleading: it implies a bug in this app rather than the real,
            // transient, provider-side condition it is. Reported through the same
            // provider_rate_limited code (and so the same replay-picker UX) as an actual rate limit.
            //
            // Deliberately narrowed to StatusCode: 503, not the bare type — ServerError is documented
            // (Google.GenAI's own XML docs) as a blanket wrapper for the whole 5xx range, and the only
            // condition actually observed live is 503 "high demand". A genuine 500 could just as
            // easily mean this app sent a malformed request (a real bug worth surfacing as `internal`,
            // exception-logged, and investigated) as a transient provider fault.
            Google.GenAI.ServerError { StatusCode: 503 } =>
                new ErrorEvent { Code = "provider_rate_limited", Message = "The model provider is temporarily unavailable." },

            OperationCanceledException =>
                new ErrorEvent { Code = "internal", Message = "The run was cancelled or timed out." },

            _ => new ErrorEvent { Code = "internal", Message = "The run failed unexpectedly." },
        };
    }

    // Source-generated (CA1848: LoggerMessage delegates instead of the LoggerExtensions convenience
    // methods) — see StructuredModelCall.cs's remark on why dotnet build (Debug) never caught this.
    [LoggerMessage(Level = LogLevel.Error, Message = "Enquiry pipeline run failed ({ExceptionType})")]
    private static partial void LogPipelineRunFailed(ILogger logger, Exception exception, string? exceptionType);

    private static bool MentionsContextLimit(string? message) =>
        message is not null
        && (message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || message.Contains("context length", StringComparison.OrdinalIgnoreCase)
            || message.Contains("context window", StringComparison.OrdinalIgnoreCase)
            || message.Contains("too large", StringComparison.OrdinalIgnoreCase));

    /// <summary>What is persisted to <c>AgentRuns.ApprovalRequestJson</c> while a run is suspended —
    /// the <see cref="ApprovalRequest"/> the approval card shows, plus the port's own
    /// <see cref="Microsoft.Agents.AI.Workflows.ExternalRequest.RequestId"/>, needed to correlate the
    /// response the framework republishes on resume. Public (not private, despite being written only
    /// from inside this class) because task 07's approval endpoints are the reader of this column and
    /// need the exact wire shape to deserialize it — better that than reimplementing this record, or
    /// parsing the JSON by hand, on the other side of the project boundary.</summary>
    public sealed record StoredApproval(string RequestId, ApprovalRequest Request);
}

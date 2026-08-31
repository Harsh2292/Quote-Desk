using System.ClientModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
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
/// <see cref="StartAsync"/> or <see cref="ResumeAsync"/>, including a resume after a real restart —
/// nothing about a suspended run depends on any object still living in this process's memory.
/// </summary>
public sealed class EnquiryPipeline(
    IEnquiryRepository enquiries,
    IAgentRunRepository agentRuns,
    ReadToolRegistry readTools,
    PricingTools pricingTools,
    QuoteWriteTools writeTools,
    IQuoteRepository quotes,
    ICatalogRepository catalog,
    ICustomerRepository customers,
    IChatClient chatClient,
    PromptLibrary prompts,
    LlmOptions options,
    SqlCheckpointStore checkpointStore,
    TimeProvider timeProvider)
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
            new NewAgentRun(enquiryId, sessionId, AgentRunStatuses.Running, timeProvider.GetUtcNow()), cancellationToken);

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
            await agentRuns.UpdateStatusAsync(run.Id, AgentRunStatuses.Failed, null, timeProvider.GetUtcNow(), cancellationToken);
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

        var tokens = new TokenUsageTracker(options.TokenBudget);
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

    private WorkflowNodes BuildNodes(TokenUsageTracker tokens)
    {
        var extractAgent = chatClient.AsAIAgent(instructions: prompts.Extract, name: "Extract", description: null, tools: null);
        var narrateAgent = chatClient.AsAIAgent(instructions: prompts.Narrate, name: "Narrate", description: null, tools: null);

        // price_quote is deliberately excluded: Resolve gets only the four lookup tools, so pricing
        // is never something the model can call — it is the Price node's job, in plain code.
        var lookupTools = readTools.Tools.Where(t => t.Name != "price_quote").ToList();

        return new WorkflowNodes(
            new ExtractExecutor("Extract", extractAgent, tokens),
            new ResolveExecutor("Resolve", chatClient, lookupTools, prompts.Resolve, options.MaxToolCalls, catalog, customers, tokens),
            new PriceExecutor("Price", pricingTools, narrateAgent, tokens),
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
                    await agentRuns.UpdateStatusAsync(agentRunId, AgentRunStatuses.Failed, null, timeProvider.GetUtcNow(), cancellationToken);
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
                            await agentRuns.UpdateStatusAsync(agentRunId, AgentRunStatuses.PendingApproval, stored, timeProvider.GetUtcNow(), cancellationToken);
                            yield return new ApprovalRequiredEvent
                            {
                                ApprovalId = agentRunId.ToString(CultureInfo.InvariantCulture),
                                Action = "approve_quote",
                                Payload = approvalRequest,
                            };
                        }

                        yield break;

                    case ExecutorFailedEvent { Data: { } executorException }:
                        await agentRuns.UpdateStatusAsync(agentRunId, AgentRunStatuses.Failed, null, timeProvider.GetUtcNow(), cancellationToken);
                        yield return ToErrorEvent(executorException);
                        yield break;

                    case WorkflowErrorEvent { Exception: { } workflowException }:
                        await agentRuns.UpdateStatusAsync(agentRunId, AgentRunStatuses.Failed, null, timeProvider.GetUtcNow(), cancellationToken);
                        yield return ToErrorEvent(workflowException);
                        yield break;

                    case WorkflowOutputEvent output:
                        var result = output.As<PipelineResult>();
                        var status = result?.Success == true ? AgentRunStatuses.Completed : AgentRunStatuses.Rejected;
                        await agentRuns.UpdateStatusAsync(agentRunId, status, null, timeProvider.GetUtcNow(), cancellationToken);
                        yield return new DoneEvent
                        {
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

    private static ErrorEvent ToErrorEvent(Exception ex) => ex switch
    {
        // Two exception types map to the same provider_rate_limited code because the two providers
        // this pipeline can be built against (docs/SPEC.md §4) throw different ones for the exact same
        // condition: ClientResultException from the OpenAI-compatible client ("github" profile, and
        // "gemini" before Google.GenAI was adopted), Google.GenAI.ClientError from Google's native SDK
        // ("gemini" profile now). Found live: a real Gemini free-tier daily quota (20 requests/day for
        // gemini-3.6-flash) threw ClientError, not ClientResultException, and fell through to the
        // generic "internal" branch below until this was added.
        ClientResultException { Status: 429 } =>
            new ErrorEvent { Code = "provider_rate_limited", Message = "The model provider is rate-limiting requests right now." },
        Google.GenAI.ClientError { StatusCode: 429 } =>
            new ErrorEvent { Code = "provider_rate_limited", Message = "The model provider is rate-limiting requests right now." },
        BudgetExceededException budgetEx =>
            new ErrorEvent { Code = "budget_exceeded", Message = budgetEx.Message },
        _ => new ErrorEvent { Code = "internal", Message = "The run failed unexpectedly." },
    };

    /// <summary>What is persisted to <c>AgentRuns.ApprovalRequestJson</c> while a run is suspended —
    /// the <see cref="ApprovalRequest"/> the approval card shows, plus the port's own
    /// <see cref="Microsoft.Agents.AI.Workflows.ExternalRequest.RequestId"/>, needed to correlate the
    /// response the framework republishes on resume. Public (not private, despite being written only
    /// from inside this class) because task 07's approval endpoints are the reader of this column and
    /// need the exact wire shape to deserialize it — better that than reimplementing this record, or
    /// parsing the JSON by hand, on the other side of the project boundary.</summary>
    public sealed record StoredApproval(string RequestId, ApprovalRequest Request);
}

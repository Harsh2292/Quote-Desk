using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// The Approve stage — the only node that reaches <see cref="WriteToolRegistry"/>'s tools
/// (CLAUDE.md rule 3: "nothing leaves without a human"). Runs only once an
/// <see cref="ApprovalDecision"/> has arrived from outside the workflow; makes no model call at all —
/// there is nothing left to interpret once a human has decided.
/// </summary>
public sealed class ApproveExecutor(string id, QuoteWriteTools writeTools, IQuoteRepository quotes, TimeProvider timeProvider)
    : Executor<ApprovalDecision, PipelineResult>(id, options: null, declareCrossRunShareable: false)
{
    public override async ValueTask<PipelineResult> HandleAsync(
        ApprovalDecision message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        if (!message.Approved || message.Quote is not { } quote)
        {
            return new PipelineResult { Success = false, Reason = message.RejectionReason ?? "Rejected by approver." };
        }

        var draft = await TracedCallAsync(
            context, "create_quote_draft", new { message.EnquiryId },
            () => writeTools.CreateQuoteDraftAsync(message.EnquiryId, quote, cancellationToken),
            r => r.Created,
            cancellationToken);

        if (!draft.Created || draft.QuoteId is not int quoteId)
        {
            return new PipelineResult { Success = false, Reason = draft.Reason };
        }

        await quotes.MarkApprovedAsync(quoteId, message.ApprovedByUserId, QuoteStatus.Approved, timeProvider.GetUtcNow(), cancellationToken);

        var sendResult = await TracedCallAsync(
            context, "send_quote", new { QuoteId = quoteId },
            () => writeTools.SendQuoteAsync(quoteId, cancellationToken),
            r => r.Sent,
            cancellationToken);

        return new PipelineResult
        {
            Success = sendResult.Sent,
            QuoteId = quoteId,
            QuoteNumber = draft.Number,
            Reason = sendResult.Reason,
        };
    }

    /// <summary>Traces this write call the same way <see cref="TracedAIFunction"/> traces a model-driven
    /// tool call (CLAUDE.md rule 4: "every stage and tool call is traced") — even though nothing here
    /// is model-invoked, the trace panel should show these two calls exactly like any other.</summary>
    private static async Task<TResult> TracedCallAsync<TResult>(
        IWorkflowContext context,
        string name,
        object args,
        Func<Task<TResult>> call,
        Func<TResult, bool> isOk,
        CancellationToken cancellationToken)
    {
        await context.AddEventAsync(new AgentTraceEvent(new ToolStartEvent { Name = name, Args = args, At = DateTimeOffset.UtcNow }), cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var result = await call();
        stopwatch.Stop();

        await context.AddEventAsync(
            new AgentTraceEvent(new ToolEndEvent { Name = name, Ms = stopwatch.ElapsedMilliseconds, Ok = isOk(result), Result = result }),
            cancellationToken);

        return result;
    }
}

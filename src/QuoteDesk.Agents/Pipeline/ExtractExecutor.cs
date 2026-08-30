using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// The Extract stage — one model call, no tools (tasks/task-06-agents-workflow.md: "Extract agent
/// node reads the enquiry text → line items, ship-to, required-by, commercial asks"). The enquiry
/// text is wrapped in <see cref="UntrustedContent"/> before it ever reaches the model.
/// </summary>
public sealed class ExtractExecutor(string id, AIAgent agent, TokenUsageTracker tokens)
    : Executor<EnquiryInput, ExtractionResult>(id, options: null, declareCrossRunShareable: false)
{
    public override async ValueTask<ExtractionResult> HandleAsync(
        EnquiryInput message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        await context.AddEventAsync(
            new AgentTraceEvent(new StageEvent { Stage = "extract", At = DateTimeOffset.UtcNow }), cancellationToken);

        var prompt = UntrustedContent.Wrap(message.RawBody);
        var response = await agent.RunAsync(prompt, session: null, options: null, cancellationToken);
        tokens.Add(response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount);

        var extracted = ModelJson.Parse<ExtractedEnquiry>(response.Text);
        return new ExtractionResult { Enquiry = message, Extracted = extracted };
    }
}

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// The Extract stage — one model call, no tools. It reads the enquiry text and returns structure:
/// line items, ship-to, required-by, commercial asks. The enquiry text is wrapped in
/// <see cref="UntrustedContent"/> before it ever reaches the model.
///
/// This is the stage schema-enforced output helps most: no tools to complicate the response format,
/// and a shape that either parses or takes the whole run down with it.
/// </summary>
public sealed class ExtractExecutor(
    string id,
    AIAgent agent,
    string model,
    bool useStructuredOutput,
    ILogger logger)
    : Executor<EnquiryInput, ExtractionResult>(id, options: null, declareCrossRunShareable: false)
{
    public override async ValueTask<ExtractionResult> HandleAsync(
        EnquiryInput message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        await context.AddEventAsync(
            new AgentTraceEvent(new StageEvent { Stage = "extract", At = DateTimeOffset.UtcNow, Model = model }), cancellationToken);

        var prompt = UntrustedContent.Wrap(message.RawBody);
        var extracted = await StructuredModelCall.RunAsync<ExtractedEnquiry>(
            agent, prompt, useStructuredOutput, logger, cancellationToken);

        return new ExtractionResult { Enquiry = message, Extracted = extracted };
    }
}

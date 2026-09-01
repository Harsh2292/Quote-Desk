using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Agents.Tools.Results;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// The Price stage — pure code (CLAUDE.md rule 1: "the model never decides money"). Every number in
/// the resulting <see cref="ApprovalRequest"/> comes straight from <see cref="PricingTools.PriceQuoteAsync"/>;
/// the one model call here only writes the narration sentence, and cannot change a number because it
/// never sees them as anything but already-computed text to summarize.
/// </summary>
public sealed class PriceExecutor(string id, PricingTools pricingTools, AIAgent narrateAgent, string narrateModel)
    : Executor<ResolutionResult, ApprovalRequest>(id, options: null, declareCrossRunShareable: false)
{
    public override async ValueTask<ApprovalRequest> HandleAsync(
        ResolutionResult message, IWorkflowContext context, CancellationToken cancellationToken)
    {
        // The pricing itself has no model in the loop at all (CLAUDE.md rule 1) — the only model
        // call this stage makes is Narrate's closing sentence, so that is the model this event names.
        await context.AddEventAsync(
            new AgentTraceEvent(new StageEvent { Stage = "price", At = DateTimeOffset.UtcNow, Model = narrateModel }), cancellationToken);

        var lineRequests = message.Resolved
            .Select(r => new QuoteLineRequest { Sku = r.Sku, Quantity = r.Quantity })
            .ToArray();

        var priced = await pricingTools.PriceQuoteAsync(message.CustomerId, lineRequests, cancellationToken);

        var narration = await NarrateAsync(message, priced, cancellationToken);

        return new ApprovalRequest
        {
            EnquiryId = message.Enquiry.EnquiryId,
            CustomerId = message.CustomerId,
            CustomerName = message.CustomerName,
            PricedQuote = priced,
            Unresolved = message.Unresolved,
            Narration = narration,
            ShipTo = message.Extracted.ShipTo,
            RequiredBy = message.Extracted.RequiredBy,
        };
    }

    private async Task<string> NarrateAsync(ResolutionResult resolution, PricedQuote priced, CancellationToken cancellationToken)
    {
        var summary = JsonSerializer.Serialize(new
        {
            priced.CustomerId,
            resolution.CustomerName,
            priced.Lines,
            priced.Subtotal,
            priced.Freight,
            priced.Tax,
            priced.GrandTotal,
            priced.ValidUntil,
            priced.Warnings,
            Unresolved = resolution.Unresolved,
        });

        // Token usage is counted by BudgetedChatClient, which every stage's agent is built on — not
        // here, so there is exactly one place the budget is enforced.
        var response = await narrateAgent.RunAsync(
            $"Priced quote and resolution details (JSON):\n{summary}", session: null, options: null, cancellationToken);

        return response.Text;
    }
}

using System.ComponentModel;
using Microsoft.Extensions.AI;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Agents.Tools;

/// <summary>
/// <c>create_quote_draft</c> and <c>send_quote</c> — gated, per docs/SPEC.md §7. Both live only in
/// <see cref="WriteToolRegistry"/>; the Resolve agent is constructed with
/// <see cref="ReadToolRegistry"/> only, so neither tool is ever reachable from the model — that
/// separation, not a runtime check inside these methods, is what enforces CLAUDE.md rule 3
/// ("nothing leaves without a human"). Task 06's workflow calls these directly, after a human has
/// approved (or edited) the <see cref="PricedQuote"/> that <c>price_quote</c> produced.
/// </summary>
public sealed class QuoteWriteTools(IQuoteRepository quotes, IEnquiryRepository enquiries, TimeProvider timeProvider)
{
    [Description("Persists an approved priced quote as a draft against its enquiry. Only ever called after a human has approved, edited or overridden the PricedQuote from price_quote.")]
    public async Task<QuoteDraftResult> CreateQuoteDraftAsync(
        [Description("The enquiry this quote answers.")]
        int enquiryId,
        [Description("The approved PricedQuote, exactly as produced by price_quote (after any human edits).")]
        PricedQuote quote,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);

        var enquiry = await enquiries.GetByIdAsync(enquiryId, cancellationToken);
        if (enquiry is null)
        {
            return new QuoteDraftResult { Created = false, Reason = $"Enquiry {enquiryId} does not exist." };
        }

        if (quote.Lines.Count == 0)
        {
            return new QuoteDraftResult { Created = false, Reason = "A quote must have at least one line." };
        }

        var now = timeProvider.GetUtcNow();
        var newQuote = new NewQuote(
            enquiryId,
            QuoteStatus.Draft,
            quote.Subtotal,
            quote.Freight,
            quote.Tax,
            quote.GrandTotal,
            now,
            quote.ValidUntil,
            ShipTo: null, // populated once the Extract stage (task 06) supplies a customer-stated ship-to
            RequiredBy: null, // populated once the Extract stage (task 06) supplies a requested date
            [.. quote.Lines.Select(l => new NewQuoteLine(
                l.Sku, l.Quantity, l.NetUnitPrice, l.DiscountPct, l.LineTotal, l.RequiresOverride, l.DispatchDate, l.DeliveryDate, Note: null))]);

        var record = await quotes.CreateDraftAsync(newQuote, cancellationToken);

        return new QuoteDraftResult { Created = true, QuoteId = record.Id, Number = record.Number, Reason = "Draft created." };
    }

    [Description("Marks an approved quote as sent. QuoteDesk never actually emails or messages a customer (docs/SPEC.md §9 non-goal) — this stamps SentAt and logs it, matching how a real send would be recorded once that channel is wired up.")]
    public async Task<SendResult> SendQuoteAsync(
        [Description("The Id returned by create_quote_draft.")]
        int quoteId,
        CancellationToken cancellationToken)
    {
        var quote = await quotes.GetByIdAsync(quoteId, cancellationToken);
        if (quote is null)
        {
            return new SendResult { Sent = false, Reason = $"Quote {quoteId} does not exist." };
        }

        if (quote.Status is not (QuoteStatus.Draft or QuoteStatus.Approved))
        {
            return new SendResult { Sent = false, Reason = $"Quote {quoteId} is '{quote.Status}' and cannot be sent." };
        }

        var now = timeProvider.GetUtcNow();
        await quotes.MarkSentAsync(quoteId, QuoteStatus.Sent, now, cancellationToken);

        return new SendResult { Sent = true, SentAt = now, Reason = "Quote marked sent." };
    }
}

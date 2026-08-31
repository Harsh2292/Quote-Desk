namespace QuoteDesk.Data.Repositories;

public interface IQuoteRepository
{
    Task<QuoteRecord?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Newest first — what <c>GET /api/quotes</c> (task 07) lists. Summary shape, not full
    /// line detail; joins the customer name in from the quote's enquiry.</summary>
    Task<IReadOnlyList<QuoteSummaryRecord>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Persists a new quote and its lines, then assigns <c>Number</c> from the row's own
    /// generated Id (e.g. "QTN-2026-0004") — a plain default per docs/DOMAIN.md's numbering
    /// convention, not a locked business rule.</summary>
    Task<QuoteRecord> CreateDraftAsync(NewQuote quote, CancellationToken cancellationToken);

    /// <summary>Stamps a quote sent. The caller decides <paramref name="status"/> and whether the
    /// current status permits this — this method performs the write unconditionally.</summary>
    Task<QuoteRecord> MarkSentAsync(int quoteId, string status, DateTimeOffset sentAt, CancellationToken cancellationToken);

    /// <summary>Records which signed-in salesperson approved a quote, and when, and stamps
    /// <paramref name="status"/> (the caller owns the status vocabulary, same as
    /// <see cref="MarkSentAsync"/>). Performs the write unconditionally.</summary>
    Task<QuoteRecord> MarkApprovedAsync(int quoteId, int approvedByUserId, string status, DateTimeOffset approvedAt, CancellationToken cancellationToken);
}

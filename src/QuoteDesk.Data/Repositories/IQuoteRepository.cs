namespace QuoteDesk.Data.Repositories;

public interface IQuoteRepository
{
    Task<QuoteRecord?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Persists a new quote and its lines, then assigns <c>Number</c> from the row's own
    /// generated Id (e.g. "QTN-2026-0004") — a plain default per docs/DOMAIN.md's numbering
    /// convention, not a locked business rule.</summary>
    Task<QuoteRecord> CreateDraftAsync(NewQuote quote, CancellationToken cancellationToken);

    /// <summary>Stamps a quote sent. The caller decides <paramref name="status"/> and whether the
    /// current status permits this — this method performs the write unconditionally.</summary>
    Task<QuoteRecord> MarkSentAsync(int quoteId, string status, DateTimeOffset sentAt, CancellationToken cancellationToken);
}

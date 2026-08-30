using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Repositories;

public sealed class QuoteRepository(QuoteDeskDbContext db) : IQuoteRepository
{
    public async Task<QuoteRecord?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var quote = await db.Quotes.AsNoTracking()
            .Include(q => q.Lines)
            .SingleOrDefaultAsync(q => q.Id == id, cancellationToken);

        return quote is null ? null : ToRecord(quote);
    }

    public async Task<QuoteRecord> CreateDraftAsync(NewQuote quote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);

        var entity = new Quote
        {
            EnquiryId = quote.EnquiryId,
            // Placeholder — Number depends on the Id EF assigns below, so it is set after the first
            // save. Unique per call (not just empty) so two drafts created at the same instant can
            // never collide on the Numbers unique index before either gets its real value.
            Number = $"PENDING-{Guid.NewGuid():N}",
            Status = quote.Status,
            Subtotal = quote.Subtotal,
            Freight = quote.Freight,
            Tax = quote.Tax,
            Total = quote.Total,
            CreatedAt = quote.CreatedAt,
            ValidUntil = quote.ValidUntil,
            ShipTo = quote.ShipTo,
            RequiredBy = quote.RequiredBy,
            Lines = [.. quote.Lines.Select(l => new QuoteLine
            {
                QuoteId = 0, // set by EF once Quote.Id is assigned
                Sku = l.Sku,
                Qty = l.Qty,
                UnitPrice = l.UnitPrice,
                DiscountPct = l.DiscountPct,
                LineTotal = l.LineTotal,
                RequiresOverride = l.RequiresOverride,
                DispatchDate = l.DispatchDate,
                DeliveryDate = l.DeliveryDate,
                Note = l.Note,
            })],
        };

        db.Quotes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        entity.Number = $"QTN-{quote.CreatedAt.Year.ToString(CultureInfo.InvariantCulture)}-{entity.Id:D4}";
        await db.SaveChangesAsync(cancellationToken);

        return ToRecord(entity);
    }

    public async Task<QuoteRecord> MarkSentAsync(int quoteId, string status, DateTimeOffset sentAt, CancellationToken cancellationToken)
    {
        var entity = await db.Quotes.Include(q => q.Lines)
            .SingleAsync(q => q.Id == quoteId, cancellationToken);

        entity.Status = status;
        entity.SentAt = sentAt;
        await db.SaveChangesAsync(cancellationToken);

        return ToRecord(entity);
    }

    public async Task<QuoteRecord> MarkApprovedAsync(int quoteId, int approvedByUserId, string status, DateTimeOffset approvedAt, CancellationToken cancellationToken)
    {
        var entity = await db.Quotes.Include(q => q.Lines)
            .SingleAsync(q => q.Id == quoteId, cancellationToken);

        entity.ApprovedByUserId = approvedByUserId;
        entity.ApprovedAt = approvedAt;
        entity.Status = status;
        await db.SaveChangesAsync(cancellationToken);

        return ToRecord(entity);
    }

    private static QuoteRecord ToRecord(Quote q) => new(
        q.Id,
        q.EnquiryId,
        q.Number,
        q.Status,
        q.Subtotal,
        q.Freight,
        q.Tax,
        q.Total,
        q.CreatedAt,
        q.ValidUntil,
        q.ShipTo,
        q.RequiredBy,
        q.ApprovedByUserId,
        q.ApprovedAt,
        q.SentAt,
        [.. q.Lines.Select(l => new QuoteLineRecord(l.Id, l.Sku, l.Qty, l.UnitPrice, l.DiscountPct, l.LineTotal, l.RequiresOverride, l.DispatchDate, l.DeliveryDate, l.Note))]);
}

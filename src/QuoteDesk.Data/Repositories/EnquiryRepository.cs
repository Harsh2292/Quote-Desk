using Microsoft.EntityFrameworkCore;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Repositories;

public sealed class EnquiryRepository(QuoteDeskDbContext db) : IEnquiryRepository
{
    public async Task<EnquiryRecord?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var enquiry = await db.Enquiries.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

        return enquiry is null
            ? null
            : new EnquiryRecord(enquiry.Id, enquiry.Channel, enquiry.SenderId, enquiry.RawBody, enquiry.ReceivedAt, enquiry.CustomerId, enquiry.Status, enquiry.OwnerUserId);
    }

    public async Task<int> CreateAsync(NewEnquiry enquiry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(enquiry);

        var entity = new Enquiry
        {
            Channel = enquiry.Channel,
            SenderId = enquiry.SenderId,
            RawBody = enquiry.RawBody,
            ReceivedAt = enquiry.ReceivedAt,
            CustomerId = enquiry.CustomerId,
            Status = enquiry.Status,
            OwnerUserId = enquiry.OwnerUserId,
        };

        db.Enquiries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task UpdateCustomerAsync(int id, int customerId, CancellationToken cancellationToken)
    {
        var entity = await db.Enquiries.SingleAsync(e => e.Id == id, cancellationToken);
        entity.CustomerId = customerId;
        await db.SaveChangesAsync(cancellationToken);
    }
}

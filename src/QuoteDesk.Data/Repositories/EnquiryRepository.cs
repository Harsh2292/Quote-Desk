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
            : new EnquiryRecord(enquiry.Id, enquiry.Channel, enquiry.SenderId, enquiry.RawBody, enquiry.ReceivedAt, enquiry.CustomerId, enquiry.Status);
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
        };

        db.Enquiries.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}

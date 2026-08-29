using Microsoft.EntityFrameworkCore;

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
}

namespace QuoteDesk.Data.Repositories;

/// <summary>Reads exist so the seed's deliberate cases (including the unknown-sender enquiry) are
/// individually queryable. <see cref="CreateAsync"/> is the write path every intake adapter uses.</summary>
public interface IEnquiryRepository
{
    Task<EnquiryRecord?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<int> CreateAsync(NewEnquiry enquiry, CancellationToken cancellationToken);
}

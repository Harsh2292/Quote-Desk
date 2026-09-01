namespace QuoteDesk.Data.Repositories;

/// <summary>Reads exist so the seed's deliberate cases (including the unknown-sender enquiry) are
/// individually queryable. <see cref="CreateAsync"/> is the write path every intake adapter uses.</summary>
public interface IEnquiryRepository
{
    Task<EnquiryRecord?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<int> CreateAsync(NewEnquiry enquiry, CancellationToken cancellationToken);

    /// <summary>Writes back the customer Resolve identified, once a human has approved the quote
    /// built against it. An enquiry is created with <c>CustomerId: null</c> (task 04's <c>PasteAdapter</c>
    /// never knows the customer at intake time) and nothing wrote it afterward until task 09's live
    /// run found every screen reading <c>Enquiries.CustomerId</c> — the Quotes list, <c>GET
    /// /api/enquiries/{id}</c> — showing a resolved customer as unmatched.</summary>
    Task UpdateCustomerAsync(int id, int customerId, CancellationToken cancellationToken);
}

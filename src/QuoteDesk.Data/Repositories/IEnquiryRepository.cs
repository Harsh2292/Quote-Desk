namespace QuoteDesk.Data.Repositories;

/// <summary>Read-only for now. Task 04's <c>PasteAdapter</c> owns writing new enquiries; this exists
/// so the seed's deliberate cases (including the unknown-sender enquiry) are individually queryable.</summary>
public interface IEnquiryRepository
{
    Task<EnquiryRecord?> GetByIdAsync(int id, CancellationToken cancellationToken);
}

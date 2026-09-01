using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.UnitTests.Intake;

/// <summary>An in-memory <see cref="IEnquiryRepository"/> so <see cref="PasteAdapterTests"/> needs
/// no database — the write path is the only thing under test.</summary>
internal sealed class FakeEnquiryRepository : IEnquiryRepository
{
    private readonly List<EnquiryRecord> _stored = [];

    public IReadOnlyList<EnquiryRecord> Stored => _stored;

    public Task<EnquiryRecord?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(_stored.SingleOrDefault(e => e.Id == id));

    public Task<int> CreateAsync(NewEnquiry enquiry, CancellationToken cancellationToken)
    {
        var id = _stored.Count + 1;
        _stored.Add(new EnquiryRecord(id, enquiry.Channel, enquiry.SenderId, enquiry.RawBody, enquiry.ReceivedAt, enquiry.CustomerId, enquiry.Status));
        return Task.FromResult(id);
    }

    public Task UpdateCustomerAsync(int id, int customerId, CancellationToken cancellationToken)
    {
        var index = _stored.FindIndex(e => e.Id == id);
        if (index >= 0)
        {
            _stored[index] = _stored[index] with { CustomerId = customerId };
        }

        return Task.CompletedTask;
    }
}

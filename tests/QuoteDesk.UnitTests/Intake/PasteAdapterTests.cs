using FluentAssertions;
using QuoteDesk.Intake;

namespace QuoteDesk.UnitTests.Intake;

public class PasteAdapterTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));

    // The worked example from docs/DOMAIN.md — the primary eval case. If a change breaks this, the
    // change is wrong.
    private const string WorkedExampleBody = """
        Hi Mehul bhai,
        Need urgent quote —
        250 nos of the 6203 bearings (same as last time)
        40 mtr of the 25mm PU timing belt
        12 pcs ring frame spindle tape, the thicker one

        Delivery at our Sachin unit, need by 5th. Last time you gave 8% on bearings, please keep same.

        Kiran — Shreeji Textiles
        """;

    [Fact]
    public void FromPastedText_WorkedExampleBody_PreservesTextVerbatim()
    {
        var enquiry = PasteAdapter.FromPastedText("kiran@shreejitextiles.com", WorkedExampleBody, ReceivedAt);

        enquiry.Body.Should().Contain("250 nos of the 6203 bearings (same as last time)");
        enquiry.Body.Should().Contain("Kiran — Shreeji Textiles");
        enquiry.Channel.Should().Be(EnquiryChannel.Paste);
        enquiry.SenderId.Should().Be("kiran@shreejitextiles.com");
        enquiry.ReceivedAt.Should().Be(ReceivedAt);
    }

    [Fact]
    public void FromPastedText_BodyWithLeadingAndTrailingWhitespace_Trims()
    {
        var enquiry = PasteAdapter.FromPastedText("sender@example.com", "  50 pcs bearing 6203  \r\n", ReceivedAt);

        enquiry.Body.Should().Be("50 pcs bearing 6203");
    }

    [Fact]
    public async Task IngestAsync_EmptyBody_StoresAsNeedsManualEntry()
    {
        var repository = new FakeEnquiryRepository();
        var adapter = new PasteAdapter(repository);
        var enquiry = PasteAdapter.FromPastedText("sender@example.com", string.Empty, ReceivedAt);

        var result = await adapter.IngestAsync(enquiry, CancellationToken.None);

        result.Status.Should().Be(EnquiryStatus.NeedsManualEntry);
        repository.Stored.Single().Status.Should().Be(EnquiryStatus.NeedsManualEntry);
    }

    [Fact]
    public async Task IngestAsync_WhitespaceOnlyBody_StoresAsNeedsManualEntry()
    {
        var repository = new FakeEnquiryRepository();
        var adapter = new PasteAdapter(repository);
        var enquiry = PasteAdapter.FromPastedText("sender@example.com", "   \n\t  ", ReceivedAt);

        var result = await adapter.IngestAsync(enquiry, CancellationToken.None);

        result.Status.Should().Be(EnquiryStatus.NeedsManualEntry);
    }

    [Fact]
    public async Task IngestAsync_FiftyKilobyteBody_StoresSuccessfully()
    {
        var repository = new FakeEnquiryRepository();
        var adapter = new PasteAdapter(repository);
        var largeBody = string.Concat(Enumerable.Repeat("100 pcs bearing 6203, please quote.\n", 1400)); // ~51KB
        var enquiry = PasteAdapter.FromPastedText("sender@example.com", largeBody, ReceivedAt);

        var result = await adapter.IngestAsync(enquiry, CancellationToken.None);

        result.Status.Should().Be(EnquiryStatus.Pending);
        repository.Stored.Single().RawBody.Length.Should().BeGreaterThan(50_000);
    }

    [Fact]
    public async Task IngestAsync_AttachmentOnlyEnquiry_StoresAsNeedsManualEntryRatherThanThrowing()
    {
        var repository = new FakeEnquiryRepository();
        var adapter = new PasteAdapter(repository);
        var enquiry = new IncomingEnquiry
        {
            Channel = EnquiryChannel.Paste,
            SenderId = "sender@example.com",
            Body = string.Empty,
            ReceivedAt = ReceivedAt,
            Attachments = [new EnquiryAttachment { FileName = "list.jpg", ContentType = "image/jpeg", SizeBytes = 204_800 }],
        };

        var act = async () => await adapter.IngestAsync(enquiry, CancellationToken.None);

        var result = await act.Should().NotThrowAsync();
        result.Subject.Status.Should().Be(EnquiryStatus.NeedsManualEntry);
    }

    [Fact]
    public void Channel_Always_IsPaste()
    {
        var adapter = new PasteAdapter(new FakeEnquiryRepository());

        adapter.Channel.Should().Be(EnquiryChannel.Paste);
    }
}

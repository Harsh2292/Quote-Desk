namespace QuoteDesk.Intake;

/// <summary>The outcome of ingesting one <see cref="IncomingEnquiry"/> — the id it was stored
/// under and the status it was stored with (see <see cref="EnquiryStatusRule"/>).</summary>
public sealed record EnquiryIntakeResult(int EnquiryId, string Status);

/// <summary>
/// One adapter per channel, converging on the same shape. <see cref="PasteAdapter"/> is the only
/// implementation today; task 10 adds an email (IMAP) and a WhatsApp (Twilio) adapter behind this
/// same interface, with no change required outside QuoteDesk.Intake.
/// </summary>
public interface IEnquiryIntakeAdapter
{
    EnquiryChannel Channel { get; }

    Task<EnquiryIntakeResult> IngestAsync(IncomingEnquiry enquiry, CancellationToken cancellationToken);
}

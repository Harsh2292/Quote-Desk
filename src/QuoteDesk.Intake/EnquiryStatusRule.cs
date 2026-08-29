namespace QuoteDesk.Intake;

/// <summary>
/// Decides the initial status for a freshly ingested enquiry. Pure and shared by every adapter, so
/// the "attachment-only enquiries need a human" rule is written once rather than reimplemented per
/// channel when task 10 adds email and WhatsApp.
/// </summary>
public static class EnquiryStatusRule
{
    public static string Resolve(IncomingEnquiry enquiry)
    {
        ArgumentNullException.ThrowIfNull(enquiry);

        return string.IsNullOrWhiteSpace(enquiry.Body)
            ? EnquiryStatus.NeedsManualEntry
            : EnquiryStatus.Pending;
    }
}

namespace QuoteDesk.Agents.Tools;

/// <summary>
/// The <c>Quotes.Status</c> values the write tools produce and transition between.
/// QuoteDesk.Data never hardcodes these — <c>QuoteRepository</c> just persists whatever string it
/// is given, the same pattern QuoteDesk.Intake uses for <c>EnquiryStatus</c>.
/// </summary>
public static class QuoteStatus
{
    public const string Draft = "draft";
    public const string Approved = "approved";
    public const string Sent = "sent";
    public const string Rejected = "rejected";
}

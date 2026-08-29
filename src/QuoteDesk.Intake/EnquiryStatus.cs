namespace QuoteDesk.Intake;

/// <summary>
/// The <c>Enquiries.Status</c> values any adapter can write. Kept as plain strings rather than an
/// enum because the column already holds values written by <c>DeterministicSeeder</c>
/// ("quoted", "new_customer") that no adapter produces — an enum would either have to grow those
/// too or fail to model rows adapters never create.
/// </summary>
public static class EnquiryStatus
{
    public const string Pending = "pending";

    /// <summary>No usable text and only attachments (or nothing at all) — stored rather than
    /// failing, and never sent to the model (docs/SPEC.md §5, tasks/task-04-intake.md).</summary>
    public const string NeedsManualEntry = "needs_manual_entry";
}

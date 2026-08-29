namespace QuoteDesk.Intake;

/// <summary>
/// Where an enquiry arrived from. Deliberately confined to this project — nothing downstream of
/// intake (QuoteDesk.Agents, QuoteDesk.Api) is allowed to know or care which channel produced an
/// <see cref="IncomingEnquiry"/> (docs/SPEC.md §5). Adapters convert to and from the plain
/// <c>Channel</c> string column on the <c>Enquiries</c> table.
/// </summary>
public enum EnquiryChannel
{
    Paste,
    Email,
    WhatsApp,
}

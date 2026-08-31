using Microsoft.Extensions.AI;

namespace QuoteDesk.IntegrationTests.Agents;

/// <summary>
/// The scripted <see cref="ChatResponse" /> turns for docs/DOMAIN.md's Shreeji Textiles worked
/// example — the project's primary eval case, reproduced against the real seeded database via
/// <see cref="StubChatClient" />/<see cref="QuoteDesk.IntegrationTests.Api.ScriptableChatClient" />.
/// Shared, rather than duplicated per test class, by <c>EnquiryPipelineTests</c> (task 06, calling the
/// pipeline directly) and <c>AgentStreamEndpointTests</c> (task 07, calling it over HTTP).
/// </summary>
public static class WorkedExampleScript
{
    /// <summary>The worked example's raw text, exactly as docs/DOMAIN.md quotes it — what a test
    /// posts to <c>POST /api/enquiries</c> before processing it.</summary>
    public const string Body = """
        Hi Mehul bhai,
        Need urgent quote —
        250 nos of the 6203 bearings (same as last time)
        40 mtr of the 25mm PU timing belt
        12 pcs ring frame spindle tape, the thicker one

        Delivery at our Sachin unit, need by 5th. Last time you gave 8% on bearings, please keep same.

        Kiran — Shreeji Textiles
        """;

    /// <summary>The sender id the worked example's enquiry must carry for <c>resolve_customer</c> to
    /// match the seeded Shreeji Textiles row by email domain.</summary>
    public const string SenderId = "kiran@shreejitextiles.com";

    /// <summary>
    /// One scripted turn per model round-trip for the whole worked example: Extract (1 turn),
    /// Resolve's tool-calling loop (4 turns — resolve_customer, one batched search_catalog call
    /// resolving all three lines at once, one get_customer_history call for the ambiguous spindle
    /// tape, then a final resolution turn), and Price's narration (1 turn). Every tool call in between
    /// is executed for real. search_catalog is batched (not one call per line) since task 07's live
    /// verification found the per-line version cost one real Gemini call per line for no benefit — see
    /// docs/SESSION-LOG.md and CatalogTools.SearchCatalogAsync's own remarks.
    /// </summary>
    public static List<ChatResponse> BuildWorkedExampleTurns(int shreejiCustomerId)
    {
        List<ChatResponse> turns =
        [
            Text("""
                {"lines":[
                    {"description":"250 nos of the 6203 bearings (same as last time)","quantity":250,"uom":"nos"},
                    {"description":"40 mtr of the 25mm PU timing belt","quantity":40,"uom":"mtr"},
                    {"description":"12 pcs ring frame spindle tape, the thicker one","quantity":12,"uom":"pcs"}
                ],
                "companyName":"Shreeji Textiles","shipTo":"Sachin","requiredBy":"2026-03-05",
                "commercialAsk":"last time you gave 8% on bearings, please keep same"}
                """),
            Call("resolve_customer", new Dictionary<string, object?> { ["companyName"] = "Shreeji Textiles", ["senderId"] = SenderId }),
            Call("search_catalog", new Dictionary<string, object?>
            {
                ["queries"] = new object[]
                {
                    new { query = "6203 bearing", hints = new[] { "same as last time" } },
                    new { query = "25mm PU timing belt", hints = Array.Empty<string>() },
                    new { query = "ring frame spindle tape", hints = new[] { "thicker" } },
                },
            }),
            Call("get_customer_history", new Dictionary<string, object?> { ["customerId"] = shreejiCustomerId, ["sku"] = null }),
            Text($$"""
                {"customerId":{{shreejiCustomerId}},"lines":[
                    {"originalDescription":"250 nos of the 6203 bearings (same as last time)","quantity":250,"sku":"BRG-6203-2RS","reason":"Exact SKU match, confirmed by three prior purchases at the same rate."},
                    {"originalDescription":"40 mtr of the 25mm PU timing belt","quantity":40,"sku":"BELT-PU-25MM","reason":"Clean catalogue match."},
                    {"originalDescription":"12 pcs ring frame spindle tape, the thicker one","quantity":12,"sku":null,"reason":"Search returned several thickness variants too close to tell apart, and order history has no prior spindle tape purchase to break the tie — needs a human to pick."}
                ]}
                """),
            Text("Bearings and belt priced within policy at 8%; the spindle tape thickness is unresolved and needs your input; the belt's delivery misses the requested date."),
        ];

        return turns;
    }

    public static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text))
    {
        Usage = new UsageDetails { InputTokenCount = 50, OutputTokenCount = 50 },
    };

    public static ChatResponse Call(string name, Dictionary<string, object?> arguments) => new(new ChatMessage(
        ChatRole.Assistant,
        [new FunctionCallContent(callId: Guid.NewGuid().ToString(), name: name, arguments: arguments)]))
    {
        Usage = new UsageDetails { InputTokenCount = 50, OutputTokenCount = 20 },
    };
}

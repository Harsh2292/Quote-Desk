using FluentAssertions;
using Microsoft.Extensions.AI;
using QuoteDesk.Agents.Checkpointing;
using QuoteDesk.Agents.Llm;
using QuoteDesk.Agents.Pipeline;
using QuoteDesk.Agents.Prompts;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data;
using QuoteDesk.IntegrationTests.Data;

namespace QuoteDesk.IntegrationTests.Agents;

/// <summary>
/// Drives the full pipeline — Extract → Resolve → Price → suspend → Approve — through a stubbed
/// <see cref="IChatClient"/> against the real, deterministically seeded database. CLAUDE.md:
/// "Integration tests use a stubbed IChatClient. CI must pass with no network and no API key."
/// docs/DOMAIN.md's worked example is the primary eval case reproduced here.
/// </summary>
[Collection("Repository")]
public class EnquiryPipelineTests(RepositoryFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));
    private const int ShreejiEnquiryId = 1;

    [Fact]
    public async Task StartAsync_ShreejiWorkedExample_SuspendsAtApprovalWithSpindleTapeUnresolved()
    {
        var shreeji = await fixture.Customers.FindByEmailDomainAsync("shreejitextiles.com", CancellationToken.None);
        var stub = new StubChatClient(BuildWorkedExampleTurns(shreeji!.Id));
        var pipeline = BuildPipeline(stub);

        var events = await CollectAsync(pipeline.StartAsync(ShreejiEnquiryId, CancellationToken.None));

        events.OfType<StageEvent>().Select(e => e.Stage).Should().ContainInOrder("extract", "resolve", "price");

        var toolNames = events.OfType<ToolStartEvent>().Select(e => e.Name).ToList();
        toolNames.Should().Contain(["resolve_customer", "search_catalog", "get_customer_history"]);
        toolNames.Should().NotContain(["price_quote", "create_quote_draft", "send_quote"],
            "price_quote is Price's job in code, and the write tools must never be reachable before a human approves");

        var approval = events.Should().ContainSingle(e => e is ApprovalRequiredEvent)
            .Which.Should().BeOfType<ApprovalRequiredEvent>().Subject;
        var request = approval.Payload.Should().BeOfType<ApprovalRequest>().Subject;

        request.Unresolved.Should().ContainSingle(l => l.OriginalDescription.Contains("spindle tape", StringComparison.OrdinalIgnoreCase));
        request.PricedQuote.Lines.Should().HaveCount(2);
        request.PricedQuote.Lines.Single(l => l.Sku == "BRG-6203-2RS").DiscountPct.Should().Be(0.08m);

        var beltLine = request.PricedQuote.Lines.Single(l => l.Sku == "BELT-PU-25MM");
        beltLine.DeliveryDate.Should().NotBe(new DateOnly(2026, 3, 5), "stock is short, so delivery misses the customer's requested 5th");

        events.Should().NotContain(e => e is DoneEvent, "the run must stop at approval, not run to completion, until a decision arrives");

        var run = await fixture.AgentRuns.GetLatestByEnquiryIdAsync(ShreejiEnquiryId, CancellationToken.None);
        run!.Status.Should().Be("pending_approval");
    }

    [Fact]
    public async Task ResumeAsync_Approved_CreatesAndSendsTheQuote()
    {
        var shreeji = await fixture.Customers.FindByEmailDomainAsync("shreejitextiles.com", CancellationToken.None);
        var stub = new StubChatClient(BuildWorkedExampleTurns(shreeji!.Id));
        var pipeline = BuildPipeline(stub);

        var startEvents = await CollectAsync(pipeline.StartAsync(ShreejiEnquiryId, CancellationToken.None));
        var pricedQuote = startEvents.OfType<ApprovalRequiredEvent>().Single().Payload.Should().BeOfType<ApprovalRequest>().Subject.PricedQuote;

        var decision = new ApprovalDecision
        {
            EnquiryId = ShreejiEnquiryId,
            Approved = true,
            ApprovedByUserId = await GetOrCreateApproverUserIdAsync(),
            Quote = pricedQuote,
        };

        var resumeEvents = await CollectAsync(pipeline.ResumeAsync(ShreejiEnquiryId, decision, CancellationToken.None));

        var toolNames = resumeEvents.OfType<ToolStartEvent>().Select(e => e.Name).ToList();
        toolNames.Should().ContainInOrder("create_quote_draft", "send_quote");

        var done = resumeEvents.Should().ContainSingle(e => e is DoneEvent).Which;
        done.Should().BeOfType<DoneEvent>();

        var run = await fixture.AgentRuns.GetLatestByEnquiryIdAsync(ShreejiEnquiryId, CancellationToken.None);
        run!.Status.Should().Be("completed");

        var quoteNumber = resumeEvents.OfType<ToolEndEvent>().Single(e => e.Name == "create_quote_draft")
            .Result.Should().BeOfType<QuoteDraftResult>().Subject.Number;
        quoteNumber.Should().StartWith("QTN-2026-");
    }

    [Fact]
    public async Task ResumeAsync_AfterASimulatedProcessRestart_StillCreatesTheQuote()
    {
        // "A suspended approval survives an application restart" — proven here by never reusing a
        // single EnquiryPipeline, Workflow, or CheckpointManager instance across suspend and resume:
        // each phase below is built completely from scratch, sharing nothing but the same underlying
        // SQL rows a real restart would also leave behind.
        var shreeji = await fixture.Customers.FindByEmailDomainAsync("shreejitextiles.com", CancellationToken.None);
        var turns = BuildWorkedExampleTurns(shreeji!.Id);

        var startPipeline = BuildPipeline(new StubChatClient(turns));
        var startEvents = await CollectAsync(startPipeline.StartAsync(ShreejiEnquiryId, CancellationToken.None));
        var pricedQuote = startEvents.OfType<ApprovalRequiredEvent>().Single().Payload.Should().BeOfType<ApprovalRequest>().Subject.PricedQuote;

        // A brand-new pipeline, built from nothing but the fixture's repositories — standing in for a
        // freshly started process that shares no in-memory state with the one that suspended.
        var resumePipeline = BuildPipeline(new StubChatClient([]));
        var decision = new ApprovalDecision
        {
            EnquiryId = ShreejiEnquiryId,
            Approved = true,
            ApprovedByUserId = await GetOrCreateApproverUserIdAsync(),
            Quote = pricedQuote,
        };

        var resumeEvents = await CollectAsync(resumePipeline.ResumeAsync(ShreejiEnquiryId, decision, CancellationToken.None));

        resumeEvents.Should().ContainSingle(e => e is DoneEvent);
        var draft = resumeEvents.OfType<ToolEndEvent>().Single(e => e.Name == "create_quote_draft")
            .Result.Should().BeOfType<QuoteDraftResult>().Subject;
        draft.Created.Should().BeTrue();

        var stored = await fixture.Quotes.GetByIdAsync(draft.QuoteId!.Value, CancellationToken.None);
        stored!.Status.Should().Be("sent");
    }

    [Fact]
    public async Task StartAsync_TokenBudgetExceededOnTheFirstCall_ReturnsBudgetExceededAndMarksTheRunFailed()
    {
        var extractOnly = new ChatResponse(new ChatMessage(ChatRole.Assistant, "not reached"))
        {
            Usage = new UsageDetails { InputTokenCount = 1000, OutputTokenCount = 1000 },
        };
        var stub = new StubChatClient([extractOnly]);
        var pipeline = BuildPipeline(stub, tokenBudget: 10);

        var events = await CollectAsync(pipeline.StartAsync(ShreejiEnquiryId, CancellationToken.None));

        var error = events.Should().ContainSingle(e => e is ErrorEvent).Which.Should().BeOfType<ErrorEvent>().Subject;
        error.Code.Should().Be("budget_exceeded");
        events.Last().Should().BeSameAs(error, "the run must stop the moment the budget is exceeded, emitting nothing after the error");

        var run = await fixture.AgentRuns.GetLatestByEnquiryIdAsync(ShreejiEnquiryId, CancellationToken.None);
        run!.Status.Should().Be("failed");
    }

    /// <summary>Quotes.ApprovedByUserId is a real foreign key to Users, which the deterministic seed
    /// deliberately leaves empty (docs/SPEC.md §6: a row is only ever auto-provisioned by a Google
    /// sign-in). Upserting is idempotent on GoogleSubject, so every test in this class shares one row.</summary>
    private async Task<int> GetOrCreateApproverUserIdAsync()
    {
        var user = await fixture.Users.UpsertFromGoogleAsync(
            new GoogleUserUpsert("test-approver-sub", "approver@quotedesk.test", "Test Approver", null, "admin", Now),
            CancellationToken.None);

        return user.Id;
    }

    private EnquiryPipeline BuildPipeline(IChatClient chatClient, int tokenBudget = 20_000)
    {
        var timeProvider = new FixedTimeProvider(Now);
        var customerTools = new CustomerTools(fixture.Customers, fixture.OrderHistory);
        var catalogTools = new CatalogTools(fixture.Catalog);
        var stockTools = new StockTools(fixture.Stock, timeProvider);
        var pricingTools = new PricingTools(fixture.Customers, fixture.Catalog, fixture.Stock, fixture.PriceRules, timeProvider);
        var readTools = new ReadToolRegistry(customerTools, catalogTools, stockTools, pricingTools);
        var writeTools = new QuoteWriteTools(fixture.Quotes, fixture.Enquiries, timeProvider);
        var options = new LlmOptions { Endpoint = "https://example.test/", ApiKey = "unused", Model = "stub", MaxToolCalls = 8, TokenBudget = tokenBudget };
        var checkpointStore = new SqlCheckpointStore(fixture.Checkpoints, timeProvider);

        return new EnquiryPipeline(
            fixture.Enquiries,
            fixture.AgentRuns,
            readTools,
            pricingTools,
            writeTools,
            fixture.Quotes,
            fixture.Catalog,
            fixture.Customers,
            chatClient,
            new PromptLibrary(),
            options,
            checkpointStore,
            timeProvider);
    }

    private static async Task<List<AgentEvent>> CollectAsync(IAsyncEnumerable<AgentEvent> events)
    {
        var list = new List<AgentEvent>();
        await foreach (var evt in events)
        {
            list.Add(evt);
        }

        return list;
    }

    /// <summary>
    /// One scripted turn per model round-trip for the whole Shreeji Textiles worked example
    /// (docs/DOMAIN.md): Extract (1 turn), Resolve's tool-calling loop (5 turns — resolve_customer,
    /// three search_catalog calls, one get_customer_history call for the ambiguous spindle tape —
    /// then a final resolution turn), and Price's narration (1 turn). Every tool call in between is
    /// executed for real against <see cref="RepositoryFixture"/>'s seeded database.
    /// </summary>
    private static List<ChatResponse> BuildWorkedExampleTurns(int shreejiCustomerId)
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
            Call("resolve_customer", new Dictionary<string, object?> { ["companyName"] = "Shreeji Textiles", ["senderId"] = "kiran@shreejitextiles.com" }),
            Call("search_catalog", new Dictionary<string, object?> { ["query"] = "6203 bearing", ["hints"] = new[] { "same as last time" } }),
            Call("search_catalog", new Dictionary<string, object?> { ["query"] = "25mm PU timing belt", ["hints"] = Array.Empty<string>() }),
            Call("search_catalog", new Dictionary<string, object?> { ["query"] = "ring frame spindle tape", ["hints"] = new[] { "thicker" } }),
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

    private static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text))
    {
        Usage = new UsageDetails { InputTokenCount = 50, OutputTokenCount = 50 },
    };

    private static ChatResponse Call(string name, Dictionary<string, object?> arguments) => new(new ChatMessage(
        ChatRole.Assistant,
        [new FunctionCallContent(callId: Guid.NewGuid().ToString(), name: name, arguments: arguments)]))
    {
        Usage = new UsageDetails { InputTokenCount = 50, OutputTokenCount = 20 },
    };
}

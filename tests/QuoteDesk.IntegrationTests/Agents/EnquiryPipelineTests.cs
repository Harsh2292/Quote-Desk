using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
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

    /// <summary>A model does not follow a formatting instruction with 100% reliability. Before the
    /// retry layer existed, one reply of prose instead of JSON killed the entire run — the failure had
    /// no recovery path at all. Now the parse error is handed back and the run continues.</summary>
    [Fact]
    public async Task StartAsync_WhenExtractRepliesWithProseInsteadOfJson_RetriesWithTheErrorAndStillReachesApproval()
    {
        var shreeji = await fixture.Customers.FindByEmailDomainAsync("shreejitextiles.com", CancellationToken.None);
        var turns = WorkedExampleScript.BuildWorkedExampleTurns(shreeji!.Id);
        turns.Insert(0, WorkedExampleScript.Text("Sure! I'd be happy to help you with that enquiry."));

        var stub = new StubChatClient(turns);
        var pipeline = BuildPipeline(stub);

        var events = await CollectAsync(pipeline.StartAsync(ShreejiEnquiryId, CancellationToken.None));

        events.Should().NotContain(e => e is ErrorEvent, "an unparseable first reply must be retried, not fatal");
        events.Should().ContainSingle(e => e is ApprovalRequiredEvent);

        // The retry is only useful if it tells the model what was actually wrong.
        stub.ReceivedMessages[1].Last().Text.Should().Contain("could not be used");
    }

    [Fact]
    public async Task StartAsync_ShreejiWorkedExample_SuspendsAtApprovalWithSpindleTapeUnresolved()
    {
        var shreeji = await fixture.Customers.FindByEmailDomainAsync("shreejitextiles.com", CancellationToken.None);
        var stub = new StubChatClient(WorkedExampleScript.BuildWorkedExampleTurns(shreeji!.Id));
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
        var stub = new StubChatClient(WorkedExampleScript.BuildWorkedExampleTurns(shreeji!.Id));
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
        var turns = WorkedExampleScript.BuildWorkedExampleTurns(shreeji!.Id);

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
            timeProvider,
            NullLogger<EnquiryPipeline>.Instance);
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
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QuoteDesk.Agents.Checkpointing;
using QuoteDesk.Agents.Llm;
using QuoteDesk.Agents.Pipeline;
using QuoteDesk.Agents.Prompts;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Evals;

/// <summary>
/// Phase 0 of the quota-vs-quality investigation (docs/SESSION-LOG.md): `gemini-3.6-flash` hit a real
/// 20-requests/day free-tier wall. Google's quota is per-model, per-project, so `gemini-3.1-flash-lite`
/// has its own untouched bucket — this either reveals real quota headroom, or its own real limit,
/// either way more trustworthy than the unverified "1,500/day" blog claims already proven unreliable
/// once this session (the "streaming only" claim about thought_signature was also wrong).
///
/// Deliberately not just a ping: the actual risk of a smaller model isn't "does it answer" but "does
/// it still get the genuinely ambiguous judgement calls right" — the spindle tape correctly staying
/// unresolved rather than guessed, the bearing correctly resolving via order history rather than
/// picked arbitrarily. Holds Flash-Lite to the exact same bar <see cref="GeminiWorkedExampleEval"/>
/// already holds `gemini-3.6-flash` to, so the comparison is fair. Same no-op-without-a-key contract.
/// </summary>
public class GeminiFlashLiteWorkedExampleEval
{
    private const string DevConnectionString =
        "Server=localhost,1433;Database=QuoteDesk;User Id=sa;Password=QuoteDesk!Local1;TrustServerCertificate=True";

    [Fact]
    public async Task StartAsync_WorkedExampleAgainstFlashLite_ResolvesBearingCorrectlyAndLeavesSpindleTapeUnresolved()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<GeminiFlashLiteWorkedExampleEval>()
            .Build();
        var apiKey = configuration["Llm:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return; // No key supplied — this eval is a deliberate no-op outside a manual run.
        }

        var options = new DbContextOptionsBuilder<QuoteDeskDbContext>().UseSqlServer(DevConnectionString).Options;
        await using var db = new QuoteDeskDbContext(options);

        var enquiries = new EnquiryRepository(db);
        var agentRuns = new AgentRunRepository(db);
        var customers = new CustomerRepository(db);
        var catalog = new CatalogRepository(db);
        var stock = new StockRepository(db);
        var orderHistory = new OrderHistoryRepository(db);
        var priceRules = new PriceRuleRepository(db);
        var quotes = new QuoteRepository(db);
        var timeProvider = TimeProvider.System;

        var enquiryId = await enquiries.CreateAsync(
            new NewEnquiry(
                "Paste",
                "kiran@shreejitextiles.com",
                """
                Hi Mehul bhai,
                Need urgent quote —
                250 nos of the 6203 bearings (same as last time)
                40 mtr of the 25mm PU timing belt
                12 pcs ring frame spindle tape, the thicker one

                Delivery at our Sachin unit, need by 5th. Last time you gave 8% on bearings, please keep same.

                Kiran — Shreeji Textiles
                """,
                timeProvider.GetUtcNow(),
                CustomerId: null,
                "pending"),
            CancellationToken.None);

        var customerTools = new CustomerTools(customers, orderHistory);
        var catalogTools = new CatalogTools(catalog);
        var stockTools = new StockTools(stock, timeProvider);
        var pricingTools = new PricingTools(customers, catalog, stock, priceRules, timeProvider);
        var readTools = new ReadToolRegistry(customerTools, catalogTools, stockTools, pricingTools);
        var writeTools = new QuoteWriteTools(quotes, enquiries, timeProvider);

        var llmOptions = new LlmOptions
        {
            Provider = "gemini",
            Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/",
            ApiKey = apiKey,
            Model = "gemini-3.1-flash-lite",
            MaxToolCalls = 8,
            TokenBudget = 20_000,
        };
        var chatClient = ChatClientFactory.Create(llmOptions);
        var checkpointStore = new SqlCheckpointStore(new WorkflowCheckpointRepository(new DevDbContextFactory()), timeProvider);

        var pipeline = new EnquiryPipeline(
            enquiries, agentRuns, readTools, pricingTools, writeTools, quotes, catalog, customers,
            chatClient, new PromptLibrary(), llmOptions, checkpointStore, timeProvider,
            NullLogger<EnquiryPipeline>.Instance);

        var events = new List<AgentEvent>();
        await foreach (var evt in pipeline.StartAsync(enquiryId, CancellationToken.None))
        {
            events.Add(evt);
        }

        events.Should().NotContain(e => e is ErrorEvent, "gemini-3.1-flash-lite should complete cleanly the same way gemini-3.6-flash does");

        var approval = events.Should().ContainSingle(e => e is ApprovalRequiredEvent)
            .Which.Should().BeOfType<ApprovalRequiredEvent>().Subject;
        var request = approval.Payload.Should().BeOfType<ApprovalRequest>().Subject;

        // The two judgement calls that actually matter — a weaker model's real failure mode is
        // guessing here instead of routing to a human, which these two assertions catch directly.
        request.Unresolved.Should().ContainSingle(
            l => l.OriginalDescription.Contains("spindle tape", StringComparison.OrdinalIgnoreCase),
            "the two spindle tape thickness variants are genuinely ambiguous and must not be guessed, regardless of model");
        request.PricedQuote.Lines.Should().ContainSingle(
            l => l.Sku == "BRG-6203-2RS",
            "the bearing should resolve via the customer's order history, not be picked arbitrarily or left unresolved");
    }

    private sealed class DevDbContextFactory : IDbContextFactory<QuoteDeskDbContext>
    {
        public QuoteDeskDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<QuoteDeskDbContext>().UseSqlServer(DevConnectionString).Options);

        public Task<QuoteDeskDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}

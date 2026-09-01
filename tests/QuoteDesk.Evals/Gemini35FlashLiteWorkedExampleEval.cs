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
/// Second round of the quota-vs-quality investigation (docs/SESSION-LOG.md): `gemini-3.1-flash-lite`
/// passed on quota (its own separate bucket, no daily-limit error) but failed on quality — it searched
/// too broadly (112 weak candidates for one bearing lookup) and then brute-forced disambiguation one
/// SKU at a time, burning 153,724 tokens against a 20,000 budget before the safety cap stopped it.
/// Google AI Studio's own rate-limit dashboard (checked directly by Harsh, not a blog) shows
/// `gemini-3.5-flash-lite` at 500 requests/day — 25x `gemini-3.6-flash`'s real 20/day — so it's worth
/// the same rigorous check: a different, newer "Lite" model isn't guaranteed to repeat the same
/// failure.
///
/// Deliberately not just a ping: the actual risk of a smaller model isn't "does it answer" but "does
/// it still get the genuinely ambiguous judgement calls right" — the spindle tape correctly staying
/// unresolved rather than guessed, the bearing correctly resolving via order history rather than
/// picked arbitrarily. Holds this model to the exact same bar <see cref="GeminiWorkedExampleEval"/>
/// already holds `gemini-3.6-flash` to, so the comparison is fair. Same no-op-without-a-key contract.
/// </summary>
public class Gemini35FlashLiteWorkedExampleEval
{
    private const string DevConnectionString =
        "Server=localhost,1433;Database=QuoteDesk;User Id=sa;Password=QuoteDesk!Local1;TrustServerCertificate=True";

    [Fact]
    public async Task StartAsync_WorkedExampleAgainst35FlashLite_ResolvesBearingCorrectlyAndLeavesSpindleTapeUnresolved()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Gemini35FlashLiteWorkedExampleEval>()
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
            Model = "gemini-3.5-flash-lite",
            MaxToolCalls = 8,
            TokenBudget = 20_000,
        };
        // No ExtractModel/ResolveModel/NarrateModel set above, so every stage falls back to Model —
        // this eval is a single-model comparison (this Lite model, end to end), not production's
        // per-stage routing.
        var chatClients = new ChatClientRegistry(llmOptions, model => ChatClientFactory.Create(llmOptions, model), loggerFactory: null);
        var checkpointStore = new SqlCheckpointStore(new WorkflowCheckpointRepository(new DevDbContextFactory()), timeProvider);

        var pipeline = new EnquiryPipeline(
            enquiries, agentRuns, readTools, pricingTools, writeTools, quotes, catalog, customers,
            chatClients, new PromptLibrary(), llmOptions, checkpointStore, timeProvider,
            NullLogger<EnquiryPipeline>.Instance);

        var events = new List<AgentEvent>();
        await foreach (var evt in pipeline.StartAsync(enquiryId, CancellationToken.None))
        {
            events.Add(evt);
        }

        events.Should().NotContain(e => e is ErrorEvent, "gemini-3.5-flash-lite should complete cleanly the same way gemini-3.6-flash does");

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

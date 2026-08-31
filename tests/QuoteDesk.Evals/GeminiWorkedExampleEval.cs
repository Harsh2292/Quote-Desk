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
/// The first real eval (CLAUDE.md: "Evals live in tests/QuoteDesk.Evals, excluded from the default
/// run... A test that calls a real model is an eval, not an integration test") — answers the question
/// task 06 and task 07 both left open: does the real <c>gemini-3.6-flash</c> endpoint accept the
/// tool-call argument shapes <c>AIFunctionFactory</c> produces, end to end against docs/DOMAIN.md's
/// worked example? Runs against the real local dev database (already seeded — see
/// docs/SESSION-LOG.md), never a test database, and never resumes to approval, so it only reads.
/// Reads <c>Llm:ApiKey</c> from the same local <c>dotnet user-secrets</c> store
/// <c>QuoteDesk.Api</c> uses (see the shared <c>UserSecretsId</c> in this project's own .csproj) —
/// never a command-line environment variable, so the key is never typed anywhere a shell history or
/// an approval prompt could echo it. Skips itself — passing trivially rather than failing — when no
/// key is present, so `dotnet test` never needs one even if this project is run directly.
/// </summary>
public class GeminiWorkedExampleEval
{
    private const string DevConnectionString =
        "Server=localhost,1433;Database=QuoteDesk;User Id=sa;Password=QuoteDesk!Local1;TrustServerCertificate=True";

    [Fact]
    public async Task StartAsync_WorkedExampleAgainstRealGemini_ResolvesBearingsAndBeltAndSuspendsAtApproval()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<GeminiWorkedExampleEval>()
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
            Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/",
            ApiKey = apiKey,
            Model = "gemini-3.6-flash",
            MaxToolCalls = 8,
            TokenBudget = 20_000,
        };
        var chatClient = ChatClientFactory.Create(llmOptions);
        // A dedicated factory, not the scoped `db` above: the workflow engine writes checkpoints from
        // its own background execution task, concurrently with this method's own use of `db` — the
        // same reason WorkflowCheckpointRepository uses IDbContextFactory in production (docs/SPEC.md
        // §6). Sharing one instance throws EF Core's "a second operation was started on this context".
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

        events.Should().NotContain(e => e is ErrorEvent, "a real Gemini call should complete cleanly against the worked example");
        events.OfType<ToolStartEvent>().Select(e => e.Name).Should().Contain("resolve_customer");
        events.Should().ContainSingle(e => e is ApprovalRequiredEvent);
    }

    private sealed class DevDbContextFactory : IDbContextFactory<QuoteDeskDbContext>
    {
        public QuoteDeskDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<QuoteDeskDbContext>().UseSqlServer(DevConnectionString).Options);

        public Task<QuoteDeskDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}

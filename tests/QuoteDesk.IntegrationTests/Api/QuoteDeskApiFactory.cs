using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuoteDesk.Api.Auth;
using QuoteDesk.Data;
using QuoteDesk.Data.Seed;
using QuoteDesk.IntegrationTests.Data;

namespace QuoteDesk.IntegrationTests.Api;

/// <summary>
/// This repo's first <see cref="WebApplicationFactory{TEntryPoint}"/> — <c>Program.cs</c>'s trailing
/// <c>public partial class Program;</c> exists for exactly this. Supplies test config so no developer
/// secret is needed to run these tests, and swaps in <see cref="StubGoogleIdTokenValidator"/> so CI
/// needs no network and no real Google credentials.
/// </summary>
/// <remarks>
/// Configuration is set via <b>environment variables in the constructor</b>, not
/// <c>ConfigureWebHost().ConfigureAppConfiguration()</c>. Program.cs reads the connection string and
/// <c>Auth:*</c> settings synchronously, before <c>builder.Build()</c> — but a test's
/// <c>ConfigureAppConfiguration</c> callback only runs as part of that same <c>Build()</c> call, i.e.
/// after Program.cs has already read the values. An in-memory config override there is silently
/// ignored, and — since <c>ConnectionStrings:QuoteDesk</c> is set locally via <c>dotnet user-secrets</c>
/// for the real dev database — the test would otherwise migrate and wipe it. Environment variables set
/// before the host is created have no such ordering problem: <c>WebApplicationBuilder.CreateBuilder</c>
/// reads them synchronously, before any of Program.cs's own code runs.
/// </remarks>
public sealed class QuoteDeskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DatabaseName = "QuoteDeskTests_Api";

    public const string ClientId = "test-client-id";
    public const string AdminEmail = "admin@quotedesk.test";
    public const string SigningKey = "integration-test-signing-key-at-least-32-bytes!!";

    public QuoteDeskApiFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__QuoteDesk", TestConnection.For(DatabaseName));
        Environment.SetEnvironmentVariable("Auth__Google__ClientId", ClientId);
        Environment.SetEnvironmentVariable("Auth__Jwt__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Auth__Jwt__Issuer", "quotedesk-tests");
        Environment.SetEnvironmentVariable("Auth__Jwt__Audience", "quotedesk-tests");
        Environment.SetEnvironmentVariable("Auth__Jwt__LifetimeHours", "8");
        Environment.SetEnvironmentVariable("Auth__AdminEmails__0", AdminEmail);
        // Program.cs now fails fast on an empty Llm:ApiKey (task 07) — a placeholder is enough since
        // IChatClient itself is swapped for ScriptableChatClient below and never reaches a real provider.
        Environment.SetEnvironmentVariable("Llm__ApiKey", "test-key");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGoogleIdTokenValidator>();
            services.AddSingleton<IGoogleIdTokenValidator, StubGoogleIdTokenValidator>();

            // CLAUDE.md: "Integration tests use a stubbed IChatClient. CI must pass with no network
            // and no API key." ScriptableChatClient wraps a per-test-scriptable StubChatClient.
            services.RemoveAll<IChatClient>();
            services.AddSingleton<ScriptableChatClient>();
            services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<ScriptableChatClient>());
        });
    }

    public async Task InitializeAsync()
    {
        // Forces host creation now rather than on the first HTTP request, so a fresh, migrated,
        // seeded database is guaranteed before any test's first call.
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteDeskDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DeterministicSeeder.SeedAsync(db, CancellationToken.None);
    }

    public new async Task DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuoteDeskDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}

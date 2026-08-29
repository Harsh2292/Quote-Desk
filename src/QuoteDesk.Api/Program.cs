using System.Globalization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using QuoteDesk.Data;
using QuoteDesk.Data.Seed;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

var connectionString = builder.Configuration.GetConnectionString("QuoteDesk")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:QuoteDesk. Set it with dotnet user-secrets.");

builder.Services.AddQuoteDeskData(connectionString);

builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database");

var app = builder.Build();

// `dotnet run -- --seed` fills an empty database with deterministic demo data, then exits — it
// never starts Kestrel, so it is safe to run against a fresh container before the app comes up.
if (args.Contains("--seed", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<QuoteDeskDbContext>();
    await DeterministicSeeder.SeedAsync(db, CancellationToken.None);
    Log.Information("Seed complete.");
    return;
}

app.UseSerilogRequestLogging();

// Liveness never depends on anything downstream — it answers even when the database is unreachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});

// Readiness runs every registered check, including the database check added above.
app.MapHealthChecks("/health/ready");

app.Run();

// Exposed for WebApplicationFactory in QuoteDesk.IntegrationTests.
public partial class Program;

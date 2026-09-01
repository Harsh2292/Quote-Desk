using Microsoft.EntityFrameworkCore;
using QuoteDesk.Data;

namespace QuoteDesk.IntegrationTests.Data;

/// <summary>
/// Points at the same local SQL Server container docker-compose.yml starts, but a dedicated
/// per-purpose database — never the "QuoteDesk" database the dev seed and Api use, so running the
/// tests can never disturb it.
/// </summary>
internal static class TestConnection
{
    // Falls back to docker-compose.yml's own default so local runs need no extra setup; CI (task 09)
    // sets MSSQL_SA_PASSWORD for its SQL Server service container and passes the same value through
    // this env var, so the two never have to be kept in sync by hand.
    private static readonly string Password =
        Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD") ?? "QuoteDesk!Local1";

    private static string Base => $"Server=localhost,1433;User Id=sa;Password={Password};TrustServerCertificate=True";

    public static string For(string databaseName) => $"{Base};Database={databaseName}";

    public static QuoteDeskDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<QuoteDeskDbContext>()
            .UseSqlServer(For(databaseName))
            .Options;

        return new QuoteDeskDbContext(options);
    }
}

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
    private const string Base = "Server=localhost,1433;User Id=sa;Password=QuoteDesk!Local1;TrustServerCertificate=True";

    public static string For(string databaseName) => $"{Base};Database={databaseName}";

    public static QuoteDeskDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<QuoteDeskDbContext>()
            .UseSqlServer(For(databaseName))
            .Options;

        return new QuoteDeskDbContext(options);
    }
}

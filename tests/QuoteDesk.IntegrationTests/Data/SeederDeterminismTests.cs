using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuoteDesk.Data.Seed;

namespace QuoteDesk.IntegrationTests.Data;

/// <summary>
/// "Running the seed twice produces byte-identical data" (tasks/task-02-data-efcore.md) — proven
/// here by seeding two independent, freshly-migrated databases from the same fixed random seed and
/// comparing a full projection of every table, rather than trusting the fixed seed by inspection.
/// </summary>
public class SeederDeterminismTests
{
    [Fact]
    public async Task SeedAsync_TwoFreshDatabases_ProduceIdenticalData()
    {
        var first = await SeedFreshDatabaseAsync("QuoteDeskTests_Determinism_A");
        var second = await SeedFreshDatabaseAsync("QuoteDeskTests_Determinism_B");

        second.Should().BeEquivalentTo(first, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task SeedAsync_CalledTwiceAgainstTheSameDatabase_IsANoOp()
    {
        await using var db = TestConnection.CreateContext("QuoteDeskTests_Determinism_Idempotent");
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        await DeterministicSeeder.SeedAsync(db, CancellationToken.None);
        var countAfterFirstSeed = await db.Customers.CountAsync();

        await DeterministicSeeder.SeedAsync(db, CancellationToken.None);
        var countAfterSecondSeed = await db.Customers.CountAsync();

        countAfterSecondSeed.Should().Be(countAfterFirstSeed);
    }

    private static async Task<SeedSnapshot> SeedFreshDatabaseAsync(string databaseName)
    {
        await using var db = TestConnection.CreateContext(databaseName);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DeterministicSeeder.SeedAsync(db, CancellationToken.None);

        return new SeedSnapshot(
            [.. db.Customers.OrderBy(c => c.Id).Select(c => $"{c.Name}|{c.Tier}|{c.EmailDomain}|{c.WhatsAppNumber}|{c.CreditDays}|{c.DefaultShipTo}|{c.GstIn}")],
            [.. db.CatalogItems.OrderBy(c => c.Sku).Select(c => $"{c.Sku}|{c.Name}|{c.Category}|{c.ListPrice}|{c.CostPrice}|{c.Attributes}")],
            [.. db.StockLevels.OrderBy(s => s.Sku).Select(s => $"{s.Sku}|{s.OnHand}|{s.LeadTimeDays}|{s.ReorderLevel}")],
            [.. db.PriceRules.OrderBy(p => p.Scope).ThenBy(p => p.Target).ThenBy(p => p.MinQty).Select(p => $"{p.Scope}|{p.Target}|{p.MinQty}|{p.DiscountPct}")],
            [.. db.OrderHistory.OrderBy(o => o.CustomerId).ThenBy(o => o.Sku).ThenBy(o => o.OrderedAt).Select(o => $"{o.CustomerId}|{o.Sku}|{o.Qty}|{o.UnitPrice}|{o.OrderedAt}")],
            [.. db.Enquiries.OrderBy(e => e.Id).Select(e => $"{e.Channel}|{e.SenderId}|{e.RawBody}|{e.ReceivedAt}|{e.CustomerId}|{e.Status}")]);
    }

    private sealed record SeedSnapshot(
        List<string> Customers,
        List<string> CatalogItems,
        List<string> StockLevels,
        List<string> PriceRules,
        List<string> OrderHistory,
        List<string> Enquiries);
}

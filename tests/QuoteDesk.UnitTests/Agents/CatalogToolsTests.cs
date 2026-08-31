using FluentAssertions;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data;
using QuoteDesk.UnitTests.Agents.Fakes;

namespace QuoteDesk.UnitTests.Agents;

public class CatalogToolsTests
{
    [Fact]
    public async Task SearchCatalogAsync_NullQueries_ThrowsRatherThanNullReferenceException()
    {
        var tools = new CatalogTools(new FakeCatalogRepository());

        var act = async () => await tools.SearchCatalogAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SearchCatalogAsync_NullHintsOnOneQuery_ThrowsRatherThanNullReferenceException()
    {
        var tools = new CatalogTools(new FakeCatalogRepository());

        var act = async () => await tools.SearchCatalogAsync(
            [new CatalogSearchQuery { Query = "bearing", Hints = null! }], CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SearchCatalogAsync_ExactSku_ReturnsResolved()
    {
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(new CatalogItemRecord(1, "BELT-PU-25MM", "25mm PU Timing Belt", "Belts", "Mtr", 30.00m, 22.50m, null));
        var tools = new CatalogTools(catalog);

        var results = await tools.SearchCatalogAsync([Query("25mm PU timing belt")], CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Query.Should().Be("25mm PU timing belt");
        result.Outcome.Should().Be("resolved");
        result.ResolvedSku.Should().Be("BELT-PU-25MM");
    }

    [Fact]
    public async Task SearchCatalogAsync_ThickerOneWithNoDistinguishingHint_ReturnsAmbiguousListingBothVariants()
    {
        var catalog = new FakeCatalogRepository();
        catalog.Items.AddRange(
        [
            new CatalogItemRecord(1, "SPT-RF-6MM", "Ring Frame Spindle Tape", "SpindleTapes", "Mtr", 32.00m, 22.40m, "6mm"),
            new CatalogItemRecord(2, "SPT-RF-8MM", "Ring Frame Spindle Tape", "SpindleTapes", "Mtr", 38.00m, 26.60m, "8mm"),
        ]);
        var tools = new CatalogTools(catalog);

        var results = await tools.SearchCatalogAsync([Query("ring frame spindle tape", "thicker")], CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Outcome.Should().Be("ambiguous");
        result.ResolvedSku.Should().BeNull();
        result.Candidates.Select(c => c.Sku).Should().Contain(["SPT-RF-6MM", "SPT-RF-8MM"]);
    }

    [Fact]
    public async Task SearchCatalogAsync_NoMatch_ReturnsNotFound()
    {
        var tools = new CatalogTools(new FakeCatalogRepository());

        var results = await tools.SearchCatalogAsync([Query("hydraulic widget")], CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.Outcome.Should().Be("not_found");
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchCatalogAsync_CandidatesAlwaysCarryConfidenceAndReason()
    {
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(new CatalogItemRecord(1, "GEAR-M2-40T", "Module 2 Spur Gear (40T)", "Gears", "Nos", 100.00m, 90.00m, null));
        var tools = new CatalogTools(catalog);

        var results = await tools.SearchCatalogAsync([Query("module 2 spur gear")], CancellationToken.None);

        results.Should().ContainSingle().Which.Candidates.Should().OnlyContain(c => c.Reason != null && c.Confidence >= 0 && c.Confidence <= 1);
    }

    /// <summary>The whole point of batching: one call resolves every line, results in the same order
    /// as the queries they answer — found live that a per-line call cost one real Gemini call per line
    /// item for no benefit (docs/SESSION-LOG.md).</summary>
    [Fact]
    public async Task SearchCatalogAsync_MultipleQueriesInOneCall_ReturnsOneResultPerQueryInOrder()
    {
        var catalog = new FakeCatalogRepository();
        catalog.Items.AddRange(
        [
            new CatalogItemRecord(1, "BRG-6203-2RS", "6203 Series Ball Bearing (2RS)", "Bearings", "Nos", 120.00m, 90.00m, null),
            new CatalogItemRecord(2, "BELT-PU-25MM", "25mm PU Timing Belt", "Belts", "Mtr", 30.00m, 22.50m, null),
        ]);
        var tools = new CatalogTools(catalog);

        var results = await tools.SearchCatalogAsync(
            [Query("6203 bearing"), Query("hydraulic widget"), Query("25mm PU timing belt")],
            CancellationToken.None);

        results.Should().HaveCount(3);
        results[0].Query.Should().Be("6203 bearing");
        results[0].Outcome.Should().Be("resolved");
        results[0].ResolvedSku.Should().Be("BRG-6203-2RS");
        results[1].Query.Should().Be("hydraulic widget");
        results[1].Outcome.Should().Be("not_found");
        results[2].Query.Should().Be("25mm PU timing belt");
        results[2].Outcome.Should().Be("resolved");
        results[2].ResolvedSku.Should().Be("BELT-PU-25MM");
    }

    private static CatalogSearchQuery Query(string query, params string[] hints) =>
        new() { Query = query, Hints = hints };
}

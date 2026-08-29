using FluentAssertions;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Data;
using QuoteDesk.UnitTests.Agents.Fakes;

namespace QuoteDesk.UnitTests.Agents;

public class CatalogToolsTests
{
    [Fact]
    public async Task SearchCatalogAsync_NullQuery_ThrowsRatherThanNullReferenceException()
    {
        var tools = new CatalogTools(new FakeCatalogRepository());

        var act = async () => await tools.SearchCatalogAsync(null!, [], CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SearchCatalogAsync_NullHints_ThrowsRatherThanNullReferenceException()
    {
        var tools = new CatalogTools(new FakeCatalogRepository());

        var act = async () => await tools.SearchCatalogAsync("bearing", null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SearchCatalogAsync_ExactSku_ReturnsResolved()
    {
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(new CatalogItemRecord(1, "BELT-PU-25MM", "25mm PU Timing Belt", "Belts", "Mtr", 30.00m, 22.50m, null));
        var tools = new CatalogTools(catalog);

        var result = await tools.SearchCatalogAsync("25mm PU timing belt", [], CancellationToken.None);

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

        var result = await tools.SearchCatalogAsync("ring frame spindle tape", ["thicker"], CancellationToken.None);

        result.Outcome.Should().Be("ambiguous");
        result.ResolvedSku.Should().BeNull();
        result.Candidates.Select(c => c.Sku).Should().Contain(["SPT-RF-6MM", "SPT-RF-8MM"]);
    }

    [Fact]
    public async Task SearchCatalogAsync_NoMatch_ReturnsNotFound()
    {
        var tools = new CatalogTools(new FakeCatalogRepository());

        var result = await tools.SearchCatalogAsync("hydraulic widget", [], CancellationToken.None);

        result.Outcome.Should().Be("not_found");
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchCatalogAsync_CandidatesAlwaysCarryConfidenceAndReason()
    {
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(new CatalogItemRecord(1, "GEAR-M2-40T", "Module 2 Spur Gear (40T)", "Gears", "Nos", 100.00m, 90.00m, null));
        var tools = new CatalogTools(catalog);

        var result = await tools.SearchCatalogAsync("module 2 spur gear", [], CancellationToken.None);

        result.Candidates.Should().OnlyContain(c => c.Reason != null && c.Confidence >= 0 && c.Confidence <= 1);
    }
}

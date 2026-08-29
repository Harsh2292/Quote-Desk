using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;

namespace QuoteDesk.UnitTests.Domain;

/// <summary>
/// Enforces the hard constraints from tasks/task-03-pricing-domain.md by reading the actual source
/// files, rather than trusting that nobody adds a clock read later.
/// </summary>
public class DomainPurityTests
{
    [Fact]
    public void DomainProject_NeverReadsTheRealClock()
    {
        var offenders = FindOffendingFiles(
            "DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now", "DateTimeOffset.UtcNow");

        offenders.Should().BeEmpty("QuoteDesk.Domain must never read the clock — time is always a parameter");
    }

    [Fact]
    public void DomainProject_HasZeroPackageAndProjectReferences()
    {
        var csprojPath = Path.Combine(GetDomainSourceDirectory(), "QuoteDesk.Domain.csproj");
        var content = File.ReadAllText(csprojPath);

        content.Should().NotContain("<PackageReference");
        content.Should().NotContain("<ProjectReference");
    }

    private static List<string> FindOffendingFiles(params string[] forbiddenPhrases)
    {
        var domainDir = GetDomainSourceDirectory();
        var files = Directory.GetFiles(domainDir, "*.cs", SearchOption.AllDirectories);

        return [.. files.Where(file =>
        {
            var text = File.ReadAllText(file);
            return forbiddenPhrases.Any(phrase => text.Contains(phrase, StringComparison.Ordinal));
        })];
    }

    private static string GetDomainSourceDirectory([CallerFilePath] string thisFilePath = "")
    {
        var testsDomainDir = Path.GetDirectoryName(thisFilePath)!;
        var repoRoot = Path.GetFullPath(Path.Combine(testsDomainDir, "..", "..", ".."));
        return Path.Combine(repoRoot, "src", "QuoteDesk.Domain");
    }
}

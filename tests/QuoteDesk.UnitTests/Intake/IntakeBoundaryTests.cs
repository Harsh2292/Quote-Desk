using System.Runtime.CompilerServices;
using FluentAssertions;

namespace QuoteDesk.UnitTests.Intake;

/// <summary>
/// "EnquiryChannel never appears outside QuoteDesk.Intake" (tasks/task-04-intake.md) — checked by
/// scanning the actual source files, in the style of QuoteDesk.UnitTests.Domain.DomainPurityTests,
/// rather than trusting that nobody adds a leaky reference later.
/// </summary>
public class IntakeBoundaryTests
{
    [Theory]
    [InlineData("QuoteDesk.Agents")]
    [InlineData("QuoteDesk.Api")]
    public void EnquiryChannel_NeverAppearsOutsideIntake(string projectName)
    {
        var repoRoot = GetRepoRoot();
        var projectDir = Path.Combine(repoRoot, "src", projectName);

        var offenders = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("EnquiryChannel", StringComparison.Ordinal))
            .ToList();

        offenders.Should().BeEmpty($"{projectName} must never reference the EnquiryChannel enum owned by QuoteDesk.Intake");
    }

    private static string GetRepoRoot([CallerFilePath] string thisFilePath = "")
    {
        var testsIntakeDir = Path.GetDirectoryName(thisFilePath)!;
        return Path.GetFullPath(Path.Combine(testsIntakeDir, "..", "..", ".."));
    }
}

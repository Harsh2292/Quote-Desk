using FluentAssertions;
using QuoteDesk.Agents.Pipeline;
using QuoteDesk.Agents.Prompts;

namespace QuoteDesk.UnitTests.Agents;

/// <summary>
/// "Prompts live as .md files in Agents/Prompts/, loaded at startup" (tasks/task-06-agents-workflow.md)
/// — checked by actually constructing <see cref="PromptLibrary"/>, and that the untrusted-content
/// delimiter every enquiry-bearing prompt promises to honor is really in the text a human can read.
/// </summary>
public class PromptLibraryTests
{
    [Fact]
    public void Constructor_LoadsAllThreePrompts_NonEmpty()
    {
        var library = new PromptLibrary();

        library.Extract.Should().NotBeNullOrWhiteSpace();
        library.Resolve.Should().NotBeNullOrWhiteSpace();
        library.Narrate.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(nameof(PromptLibrary.Extract))]
    [InlineData(nameof(PromptLibrary.Resolve))]
    public void EnquiryBearingPrompts_DescribeTheUntrustedContentDelimiter(string promptName)
    {
        var library = new PromptLibrary();
        var prompt = promptName switch
        {
            nameof(PromptLibrary.Extract) => library.Extract,
            nameof(PromptLibrary.Resolve) => library.Resolve,
            _ => throw new ArgumentOutOfRangeException(nameof(promptName)),
        };

        prompt.Should().Contain(UntrustedContent.Start);
        prompt.Should().Contain(UntrustedContent.End);
        prompt.Should().Contain("never instructions");
    }

    [Fact]
    public void NarratePrompt_NeverMentionsComputingOrAdjustingNumbers()
    {
        // The narration step must never imply it can compute a price — pure rendering only.
        var library = new PromptLibrary();

        library.Narrate.Should().Contain("never calculate or adjust");
    }
}

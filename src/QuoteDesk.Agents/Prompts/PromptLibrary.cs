using System.Reflection;

namespace QuoteDesk.Agents.Prompts;

/// <summary>
/// The three system prompts, embedded into the assembly (per CLAUDE.md: they must survive the task 09
/// Docker image, so they are compiled in rather than copied as loose content files) and loaded once
/// here, not inline strings and not read from disk on every request. Reading fails fast in the
/// constructor — a missing prompt is a startup error, not a first-request surprise.
/// </summary>
public sealed class PromptLibrary
{
    public string Extract { get; }
    public string Resolve { get; }
    public string Narrate { get; }

    public PromptLibrary()
    {
        Extract = Load("extract.md");
        Resolve = Load("resolve.md");
        Narrate = Load("narrate.md");
    }

    private static string Load(string fileName)
    {
        var assembly = typeof(PromptLibrary).Assembly;
        var resourceName = $"{assembly.GetName().Name}.Prompts.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded prompt '{resourceName}' not found. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

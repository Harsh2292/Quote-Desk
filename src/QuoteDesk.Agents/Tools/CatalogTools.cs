using System.ComponentModel;
using Microsoft.Extensions.AI;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Agents.Tools;

/// <summary><c>search_catalog</c> — read-only, per docs/SPEC.md §7.</summary>
public sealed class CatalogTools(ICatalogRepository catalog)
{
    /// <summary>A candidate at or above this confidence, with no other candidate within
    /// <see cref="AmbiguityMargin"/> of it, counts as resolved.</summary>
    private const double ResolvedThreshold = 0.8;

    private const double AmbiguityMargin = 0.2;

    /// <summary>Generic words that would otherwise dilute every candidate's score without telling
    /// candidates apart — filtered out of the token list used for scoring and search.</summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "THE", "A", "AN", "OF", "FOR", "NOS", "PCS", "PC", "MTR", "MTRS", "ONE", "SAME", "LAST", "TIME",
    };

    [Description(
        "Searches the catalogue for items matching a customer's description. Pass the core item words as " +
        "query and any extra qualifying words (nicknames, sizes, 'the thicker one') as hints — every word " +
        "helps scoring. Outcome is 'resolved' when one candidate is clearly the best match, 'ambiguous' " +
        "when several are too close to tell apart (never guess between them — ask the customer, or check " +
        "get_customer_history for a prior purchase that breaks the tie), or 'not_found' when nothing matches.")]
    public async Task<CatalogSearchResult> SearchCatalogAsync(
        [Description("The core search phrase, e.g. '6203 bearing' or 'ring frame spindle tape'.")]
        string query,
        [Description("Extra qualifying words from the enquiry, e.g. ['thicker'] or ['25mm']. Pass an empty array if there are none.")]
        string[] hints,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(hints);

        var tokens = BuildTokens(query, hints);
        var searchTerms = tokens.Append(query.Trim()).Where(t => t.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase);

        var found = new Dictionary<string, CatalogItemRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in searchTerms)
        {
            foreach (var item in await catalog.SearchAsync(term, cancellationToken))
            {
                found.TryAdd(item.Sku, item);
            }
        }

        if (found.Count == 0)
        {
            return new CatalogSearchResult { Outcome = "not_found", Candidates = [], Reason = $"No catalogue item matches '{query}'." };
        }

        var scored = found.Values.Select(item => Score(item, tokens)).OrderByDescending(c => c.Confidence).ToList();
        var best = scored[0];
        var runnerUp = scored.Count > 1 ? scored[1] : null;

        var isResolved = best.Confidence >= ResolvedThreshold
            && (runnerUp is null || best.Confidence - runnerUp.Confidence >= AmbiguityMargin);

        return isResolved
            ? new CatalogSearchResult { Outcome = "resolved", ResolvedSku = best.Sku, Candidates = scored, Reason = best.Reason }
            : new CatalogSearchResult
            {
                Outcome = "ambiguous",
                Candidates = scored,
                Reason = $"{scored.Count} candidates are too close to choose between automatically — ask which one, or check the customer's order history.",
            };
    }

    private static CatalogCandidate Score(CatalogItemRecord item, IReadOnlyList<string> tokens)
    {
        if (tokens.Any(t => string.Equals(item.Sku, t, StringComparison.OrdinalIgnoreCase)))
        {
            return ToCandidate(item, 1.0, "Exact SKU match.");
        }

        var haystack = $"{item.Sku} {item.Name} {item.Attributes}".ToUpperInvariant();
        var matched = tokens.Where(t => haystack.Contains(t, StringComparison.Ordinal)).ToList();
        var confidence = tokens.Count == 0 ? 0.5 : Math.Round((double)matched.Count / tokens.Count, 2);

        var reason = matched.Count == 0
            ? "Returned by the catalogue search but none of the search terms matched its name or attributes."
            : $"Matched on: {string.Join(", ", matched)}.";

        return ToCandidate(item, confidence, reason);
    }

    private static CatalogCandidate ToCandidate(CatalogItemRecord item, double confidence, string reason) => new()
    {
        Sku = item.Sku,
        Name = item.Name,
        Category = item.Category,
        Uom = item.Uom,
        ListPrice = item.ListPrice,
        Attributes = item.Attributes,
        Confidence = confidence,
        Reason = reason,
    };

    private static IReadOnlyList<string> BuildTokens(string query, string[] hints) =>
        [.. Tokenize(query).Concat(hints.SelectMany(Tokenize))
            .Where(t => t.Length >= 2 && !StopWords.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static IEnumerable<string> Tokenize(string text) =>
        text.Split([' ', ',', '(', ')', '-', '/'], StringSplitOptions.RemoveEmptyEntries).Select(NormalizeToken);

    /// <summary>Simple pluralization handling ("BEARINGS" -> "BEARING") for alphabetic tokens only,
    /// so numeric codes like "6203" are never truncated.</summary>
    private static string NormalizeToken(string token)
    {
        var upper = token.ToUpperInvariant();
        return upper.Length > 3 && upper[^1] == 'S' && upper.All(char.IsLetter) ? upper[..^1] : upper;
    }
}

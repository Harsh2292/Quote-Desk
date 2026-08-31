using System.ComponentModel;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Agents.Tools;

/// <summary>
/// <c>search_catalog</c> — read-only, per docs/SPEC.md §7.
///
/// Two-stage retrieval. Stage one is recall: a cheap substring lookup per search word, unioned.
/// Stage two is precision: re-rank that shortlist with <b>whole-word</b> matching (so "ring" no
/// longer matches "bea<i>ring</i>") and <b>inverse-document-frequency weighting</b> (so a rare,
/// distinguishing word like "PU" or "6203" counts far more than a common family word like "belt").
/// Scores are an additive sum of matched-word weights, so an extra hint can only help a candidate,
/// never dilute it. Then a confidence floor and a hard cap: at most five candidates ever leave this
/// tool, whatever the outcome.
/// </summary>
public sealed class CatalogTools(ICatalogRepository catalog)
{
    /// <summary>A candidate at or above this normalised score, clear of the runner-up by
    /// <see cref="AmbiguityMargin"/>, counts as resolved.</summary>
    private const double ResolvedThreshold = 0.55;

    /// <summary>Two candidates closer than this are "too close to call" — the tool refuses to pick.</summary>
    private const double AmbiguityMargin = 0.15;

    /// <summary>An absolute score below this is a weak partial match — noise, not a candidate.</summary>
    private const double ConfidenceFloor = 0.2;

    /// <summary>A candidate scoring less than this fraction of the best is dropped even if it clears
    /// the absolute floor — it means the top result matched a rare distinguishing word this one only
    /// matched the family word of.</summary>
    private const double RelativeFloor = 0.5;

    /// <summary>Never return more than this many candidates. A tool that cannot answer in five rows
    /// should say <c>ambiguous</c> or <c>not_found</c>.</summary>
    private const int MaxCandidates = 5;

    /// <summary>Generic words that carry no signal about which item is meant — dropped from both the
    /// search terms and the score.</summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "THE", "A", "AN", "OF", "FOR", "AND", "OR", "TO", "AS", "WITH", "PLEASE", "NEED", "REQUIRE",
        "REQUIRED", "QUOTE", "RATE", "PRICE", "URGENT", "ASAP", "NOS", "PCS", "PC", "MTR", "MTRS",
        "MTS", "ONE", "SAME", "LAST", "TIME", "OUR", "USUAL", "TERMS", "SEND", "SHARE", "CHAHIYE",
        "BHEJO", "KA", "KAL", "TAK", "MIL", "JAYEGA",
    };

    [Description(
        "Searches the machinery-spares catalogue. Pass one entry per line item in a single call, " +
        "not one call per line. For each entry put the item's own words in query (part number, " +
        "product type, size) and any qualifiers — nicknames, 'the thicker one', 'same as last time' " +
        "— in hints. Naming the product family (Bearings, Belts, Gears, SpindleTapes) and the " +
        "distinguishing spec (a size like 25mm, a series like 6203, a type like PU) makes the match " +
        "sharper. Returns one result per query, in order. Outcome is 'resolved' when one candidate " +
        "is clearly best, 'ambiguous' when several are too close to tell apart (do not guess — check " +
        "get_customer_history for a prior purchase, or narrow the query), or 'not_found'.")]
    public async Task<IReadOnlyList<CatalogSearchResult>> SearchCatalogAsync(
        [Description("One entry per line item to resolve — a 3-line enquiry passes 3 entries here in one call.")]
        CatalogSearchQuery[] queries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queries);

        var results = new List<CatalogSearchResult>(queries.Length);
        foreach (var query in queries)
        {
            results.Add(await SearchOneAsync(query, cancellationToken));
        }

        return results;
    }

    private async Task<CatalogSearchResult> SearchOneAsync(CatalogSearchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Query);
        ArgumentNullException.ThrowIfNull(query.Hints);

        var terms = BuildTerms(query.Query, query.Hints);

        // ── stage 1: recall — cheap substring lookup per term, unioned ────────────────────────────
        var shortlist = new Dictionary<string, CatalogItemRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in terms.Append(query.Query.Trim()).Where(t => t.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var item in await catalog.SearchAsync(term, cancellationToken))
            {
                shortlist.TryAdd(item.Sku, item);
            }
        }

        if (shortlist.Count == 0)
        {
            return new CatalogSearchResult
            {
                Query = query.Query,
                Outcome = "not_found",
                Candidates = [],
                Reason = $"Nothing in the catalogue matches '{query.Query}'. If it was a nickname, try the product type and a size.",
            };
        }

        // ── stage 2: precision — whole-word match, weighted by how rare each word is ──────────────
        var items = shortlist.Values.ToList();
        var wordSets = items.ToDictionary(i => i.Sku, i => WordsOf($"{i.Sku} {i.Name} {i.Category} {i.Attributes}"));

        // A term's weight is its inverse document frequency across the shortlist: a term matching few
        // rows is highly distinguishing; one matching most rows barely narrows anything. Terms that
        // match nothing (e.g. "thicker") drop out entirely and never penalise a score.
        var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in terms)
        {
            var df = items.Count(i => wordSets[i.Sku].Contains(term));
            if (df > 0)
            {
                weights[term] = Math.Log((items.Count + 1.0) / (df + 1.0)) + 1.0;
            }
        }

        var totalWeight = weights.Values.Sum();
        var ranked = items
            .Select(item =>
            {
                var exact = terms.Any(t => string.Equals(item.Sku, t, StringComparison.OrdinalIgnoreCase));
                var matchedWeight = weights.Where(w => wordSets[item.Sku].Contains(w.Key)).Sum(w => w.Value);
                var confidence = exact ? 1.0
                    : totalWeight <= 0 ? 0.0
                    : Math.Round(matchedWeight / totalWeight, 3);
                return new CatalogCandidate
                {
                    Sku = item.Sku,
                    Name = item.Name,
                    Category = item.Category,
                    Attributes = item.Attributes,
                    Confidence = confidence,
                };
            })
            .Where(c => c.Confidence >= ConfidenceFloor)
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.Sku, StringComparer.Ordinal)
            .ToList();

        var topScore = ranked.Count > 0 ? ranked[0].Confidence : 0.0;
        var scored = ranked
            .Where(c => c.Confidence >= topScore * RelativeFloor)
            .Take(MaxCandidates)
            .ToList();

        if (scored.Count == 0)
        {
            return new CatalogSearchResult
            {
                Query = query.Query,
                Outcome = "not_found",
                Candidates = [],
                Reason = $"Items were found by keyword but none is a strong enough match for '{query.Query}' — narrow it with a size, series or type.",
            };
        }

        var best = scored[0];
        var runnerUp = scored.Count > 1 ? scored[1] : null;
        var resolved = best.Confidence >= ResolvedThreshold
            && (runnerUp is null || best.Confidence - runnerUp.Confidence >= AmbiguityMargin);

        if (resolved)
        {
            return new CatalogSearchResult
            {
                Query = query.Query,
                Outcome = "resolved",
                ResolvedSku = best.Sku,
                Candidates = scored,
                Reason = $"'{best.Name}' is the clear match.",
            };
        }

        var families = scored.Select(c => c.Category).Distinct().ToList();
        var hint = families.Count > 1
            ? $"Candidates span {string.Join(" and ", families)} — say which family."
            : "Candidates differ only by a spec (size, series, type or thickness) — supply it, or check the customer's order history.";

        return new CatalogSearchResult
        {
            Query = query.Query,
            Outcome = "ambiguous",
            Candidates = scored,
            Reason = $"{scored.Count} candidates are too close to choose between. {hint}",
        };
    }

    /// <summary>Query + hint words, normalised, with stop-words removed. Single-character tokens are
    /// dropped unless they are digits — a bare "3" in "module 3 spur gear" is the distinguishing
    /// spec (Extract has already separated the quantity, so a lone digit here is not a count).</summary>
    private static IReadOnlyList<string> BuildTerms(string query, string[] hints) =>
        [.. Tokenize(query).Concat(hints.SelectMany(Tokenize))
            .Where(t => (t.Length >= 2 || (t.Length == 1 && char.IsDigit(t[0]))) && !StopWords.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The distinct normalised words of a catalogue string, for whole-word matching.</summary>
    private static HashSet<string> WordsOf(string text) =>
        new(Tokenize(text), StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Tokenize(string text) =>
        text.Split(
                // Deliberately not splitting on '.', so a decimal spec like "1.5" (Module 1.5 gears)
                // stays one token and "2.5" cannot match a query for "2".
                [' ', ',', '(', ')', '-', '/', ':', ';', '\n', '\r', '\t', '"', '\''],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeToken);

    /// <summary>Uppercase, and strip a trailing plural "s" from alphabetic words only, so "bearings"
    /// and "bearing" match but numeric codes like "6203" are untouched.</summary>
    private static string NormalizeToken(string token)
    {
        var upper = token.ToUpperInvariant();
        return upper.Length > 3 && upper[^1] == 'S' && upper.All(char.IsLetter) ? upper[..^1] : upper;
    }
}

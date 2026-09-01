namespace QuoteDesk.Api.RateLimiting;

/// <summary>
/// Bound from the "RateLimiting" config section in <c>Program.cs</c> — the same pattern
/// <see cref="QuoteDesk.Agents.Llm.LlmOptions"/> and <c>AuthOptions</c> use, so every numeric limit
/// lives in appsettings.json rather than as a literal buried in the middleware setup.
///
/// Three limiters, each protecting something different (task 09):
///
/// <list type="bullet">
/// <item><see cref="GlobalPermitPerMinute"/> is a <c>GlobalLimiter</c> — it applies to <b>every</b>
/// request automatically, the same "protected by default" shape the fallback authorization policy
/// already uses, so a route added later needs no rate-limiting code of its own to get a baseline.</item>
/// <item><see cref="AuthPermitPerMinute"/> is an additional, stricter limiter stacked on top of the
/// global one, applied only to <c>POST /api/auth/google</c> — the one anonymous route, and the one
/// that costs a real Google token verification per call.</item>
/// <item><see cref="PipelinePermitPerDay"/> is the "hard daily cap on the public demo" CLAUDE.md's
/// Security section calls for, applied only to <c>POST /api/enquiries/{id}/process</c> — the one
/// route that spends the shared Gemini key. It is a single shared bucket, not per-user: the key's
/// quota is shared by every visitor, so the cap has to be too. <c>POST /api/approvals/{id}</c>
/// deliberately does not carry this policy — <see cref="QuoteDesk.Agents.Pipeline.ApproveExecutor"/>
/// makes no model call at all, so it only needs the global baseline.</item>
/// </list>
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int GlobalPermitPerMinute { get; init; } = 60;

    public int AuthPermitPerMinute { get; init; } = 10;

    public int PipelinePermitPerDay { get; init; } = 15;
}

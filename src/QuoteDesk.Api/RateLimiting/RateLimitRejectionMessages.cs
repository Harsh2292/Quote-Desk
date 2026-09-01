using Microsoft.AspNetCore.Http;

namespace QuoteDesk.Api.RateLimiting;

/// <summary>
/// The human-readable <c>ProblemDetails.Detail</c> written when the rate limiter (<c>Program.cs</c>)
/// rejects a request. Pulled out of the inline <c>OnRejected</c> callback into its own pure function
/// so the "which route gets which message" mapping is unit-testable without a running host — a
/// second full <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> in
/// the same test process was tried and dropped: it raced the existing shared fixture over process
/// environment variables (both set <c>RateLimiting__*</c> to configure their own limits, and
/// environment variables are process-global, not per-host), producing exactly the kind of
/// ordering-dependent flakiness CLAUDE.md's Tests section rules out.
/// </summary>
public static class RateLimitRejectionMessages
{
    public const string Generic = "Too many requests. Slow down and try again in a moment.";

    public const string PipelineDailyCap =
        "This demo shares one daily allowance for running the agent pipeline, and it has been reached. Try again tomorrow.";

    /// <summary>Distinguishes the one route that spends the shared Gemini key
    /// (<c>POST /api/enquiries/{id}/process</c>, carrying the "pipeline" policy) from everything
    /// else, so its rejection explains the daily cap rather than a generic rate-limit message.</summary>
    public static string For(PathString path) =>
        path.Value?.EndsWith("/process", StringComparison.Ordinal) == true ? PipelineDailyCap : Generic;
}

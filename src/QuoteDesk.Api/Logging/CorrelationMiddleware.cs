using Serilog.Context;

namespace QuoteDesk.Api.Logging;

/// <summary>
/// Pushes a correlation id onto Serilog's <see cref="LogContext"/> for the lifetime of one request,
/// so every log line the request produces — including <c>UseSerilogRequestLogging</c>'s own "Request
/// finished" summary — carries it (CLAUDE.md: "every log line carries the correlation id"). Reuses an
/// incoming <c>X-Correlation-Id</c> header when the caller supplies one (useful for a client that
/// wants to tie its own logs to ours), otherwise mints a fresh one; echoes it back on the response
/// either way so a caller that did not send one can still find its own request in the logs.
/// </summary>
public sealed class CorrelationMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("n");

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

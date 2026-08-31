using System.Text.Json;
using Microsoft.AspNetCore.Http;
using QuoteDesk.Agents.Pipeline;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Api.Streaming;

/// <summary>
/// The one place SSE framing exists — both <c>POST /api/enquiries/{id}/process</c> and
/// <c>POST /api/approvals/{id}</c> (a resume is a stream too, since the Approve stage's own tool
/// calls are traced the same way) call this instead of writing frames themselves.
/// </summary>
public static class AgentEventStreamWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Streams <paramref name="events" /> to the client as SSE frames, then persists the full trace —
    /// this stream's events appended onto whatever was already stored for the run — in a
    /// <c>finally</c>, so a dropped connection still leaves whatever ran on the record.
    /// <paramref name="resolveAgentRunId"/> is called only once streaming ends, because
    /// <c>StartAsync</c>'s run is not known to the caller until the pipeline creates it — see the
    /// endpoint call sites for how each resolves it.
    /// </summary>
    public static async Task WriteAsync(
        HttpContext context,
        IAsyncEnumerable<AgentEvent> events,
        Func<CancellationToken, Task<int?>> resolveAgentRunId,
        IAgentRunRepository agentRuns,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        var buffered = new List<AgentEvent>();
        try
        {
            await foreach (var evt in events.WithCancellation(cancellationToken))
            {
                buffered.Add(evt);
                var json = JsonSerializer.Serialize(evt, JsonOptions);
                await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            if (buffered.Count > 0)
            {
                // Deliberately CancellationToken.None from here: the client's own connection may
                // already be gone — that is exactly the case this exists to handle — and a trace
                // write cut short by the same token that just fired would defeat the point.
                var agentRunId = await resolveAgentRunId(CancellationToken.None);
                if (agentRunId is { } id)
                {
                    await PersistTraceAsync(id, buffered, agentRuns, timeProvider);
                }
            }
        }
    }

    private static async Task PersistTraceAsync(
        int agentRunId, IReadOnlyList<AgentEvent> newEvents, IAgentRunRepository agentRuns, TimeProvider timeProvider)
    {
        var run = await agentRuns.GetByIdAsync(agentRunId, CancellationToken.None);
        var trace = run?.TraceJson is { } existingJson
            ? JsonSerializer.Deserialize<List<AgentEvent>>(existingJson, JsonOptions) ?? []
            : [];

        trace.AddRange(newEvents);

        var merged = JsonSerializer.Serialize(trace, JsonOptions);
        await agentRuns.AppendTraceAsync(agentRunId, merged, timeProvider.GetUtcNow(), CancellationToken.None);
    }
}

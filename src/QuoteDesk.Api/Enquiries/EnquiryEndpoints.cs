using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using QuoteDesk.Agents.Pipeline;
using QuoteDesk.Api.Auth;
using QuoteDesk.Api.Streaming;
using QuoteDesk.Data.Repositories;
using QuoteDesk.Intake;

namespace QuoteDesk.Api.Enquiries;

public sealed record PasteEnquiryRequest(string Body, string? SenderId);

public sealed record EnquiryCreatedResponse(int EnquiryId, string Status);

/// <summary>What <c>GET /api/enquiries/{id}</c> returns — the enquiry plus its latest pipeline run,
/// if one exists. <see cref="Trace"/> is the persisted transcript (docs/SPEC.md §8's
/// <c>AgentEvent</c> stream), replayed after the live SSE stream that produced it has closed.</summary>
public sealed record EnquiryDetailResponse(
    int Id,
    string Channel,
    string SenderId,
    string RawBody,
    DateTimeOffset ReceivedAt,
    int? CustomerId,
    string Status,
    string? RunStatus,
    ApprovalRequest? PendingApproval,
    IReadOnlyList<AgentEvent>? Trace);

public static class EnquiryEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapEnquiryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/enquiries");

        group.MapPost("/", CreateFromPasteAsync);
        // "pipeline" is a hard, demo-wide daily cap stacked on top of the app-wide GlobalLimiter
        // (Program.cs) — this is the one route that spends the shared Gemini key.
        group.MapPost("/{id:int}/process", ProcessAsync).RequireRateLimiting("pipeline");
        group.MapGet("/{id:int}", GetByIdAsync);

        return app;
    }

    private static async Task<Results<Created<EnquiryCreatedResponse>, ProblemHttpResult>> CreateFromPasteAsync(
        PasteEnquiryRequest request,
        ClaimsPrincipal principal,
        PasteAdapter adapter,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // A blank body with nothing attached is a client bug (an empty textarea submit), not a
        // business case — reject it outright. A blank body that *does* carry attachments is the
        // real needs_manual_entry case, handled once channels that can attach files exist (task 10).
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return TypedResults.Problem("Body must not be empty.", statusCode: StatusCodes.Status400BadRequest);
        }

        // MapInboundClaims is disabled in Program.cs, so "email" comes through exactly as JwtIssuer
        // wrote it, not remapped to the legacy XML claim type.
        var senderId = request.SenderId ?? principal.FindFirst("email")?.Value;
        if (string.IsNullOrWhiteSpace(senderId))
        {
            return TypedResults.Problem("SenderId was not supplied and the token carries no email claim.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Ownership: stamps which signed-in salesperson created this enquiry. A missing or
        // unparseable "sub" here would mean the fallback authorization policy already let an invalid
        // token through, which should not happen — but if it did, falling back to an unowned enquiry
        // (visible to nobody, per Entities.Enquiry.OwnerUserId's remarks) is the safe failure mode,
        // not a 401 on the one route that should be maximally easy to use in a demo.
        int? ownerUserId = principal.TryGetUserId(out var parsedOwnerId) ? parsedOwnerId : null;

        var enquiry = PasteAdapter.FromPastedText(senderId, request.Body, timeProvider.GetUtcNow());
        var result = await adapter.IngestAsync(enquiry, ownerUserId, cancellationToken);

        return TypedResults.Created(
            $"/api/enquiries/{result.EnquiryId}",
            new EnquiryCreatedResponse(result.EnquiryId, result.Status));
    }

    /// <summary>Streams the pipeline over SSE. Returns <c>Task</c>, not an <c>IResult</c> — see
    /// <c>ApprovalEndpoints.DecideAsync</c>'s remarks for why a streaming endpoint takes this shape.
    /// A missing enquiry is not checked by <see cref="EnquiryPipeline.ProcessAsync"/> itself — it
    /// reports that as an <c>ErrorEvent</c> on the stream, the one error channel a client reading SSE
    /// is already watching — but ownership has to be checked <i>before</i> the stream opens, the same
    /// reason <c>ApprovalEndpoints.DecideAsync</c> validates up front: once SSE framing has started,
    /// nothing can write a plain 404 over it. Without this, the URL's own <c>{id}</c> would let any
    /// signed-in stranger drive another user's enquiry through the whole pipeline — spending their
    /// quota and reading their trace, exactly the leak <c>GetByIdAsync</c> below closes for the
    /// non-streaming read. Calls <c>ProcessAsync</c>, not <c>StartAsync</c> directly —
    /// <c>ProcessAsync</c> transparently resumes a failed run past Resolve when Resolve already
    /// succeeded, rather than always restarting from Extract; the existing "Retry" button on the Desk
    /// needed no change to gain this.</summary>
    private static async Task ProcessAsync(
        int id,
        HttpContext context,
        ClaimsPrincipal principal,
        IEnquiryRepository enquiries,
        EnquiryPipeline pipeline,
        IAgentRunRepository agentRuns,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var callerId))
        {
            await TypedResults.Problem("Token does not carry a valid subject.", statusCode: StatusCodes.Status401Unauthorized)
                .ExecuteAsync(context);
            return;
        }

        var owned = await enquiries.GetByIdAsync(id, cancellationToken);
        if (owned is null || owned.OwnerUserId != callerId)
        {
            // 404, not 403 — mirrors GetByIdAsync and ApprovalEndpoints.DecideAsync: a signed-in
            // stranger must not learn that enquiry {id} exists and belongs to someone else.
            await TypedResults.Problem($"Enquiry {id} does not exist.", statusCode: StatusCodes.Status404NotFound)
                .ExecuteAsync(context);
            return;
        }

        await AgentEventStreamWriter.WriteAsync(
            context,
            pipeline.ProcessAsync(id, cancellationToken),
            async ct =>
            {
                // Whether this call started fresh or resumed in place, the run it acted on — if any —
                // is the latest for this enquiry by the time the stream has ended. Null when the
                // enquiry did not exist and no run was ever created.
                var run = await agentRuns.GetLatestByEnquiryIdAsync(id, ct);
                return run?.Id;
            },
            agentRuns,
            timeProvider,
            cancellationToken);
    }

    private static async Task<Results<Ok<EnquiryDetailResponse>, ProblemHttpResult>> GetByIdAsync(
        int id,
        ClaimsPrincipal principal,
        IEnquiryRepository enquiries,
        IAgentRunRepository agentRuns,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserId(out var callerId))
        {
            return TypedResults.Problem("Token does not carry a valid subject.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var enquiry = await enquiries.GetByIdAsync(id, cancellationToken);
        // 404, not 403: a trivially enumerable int id must not confirm to a stranger that an enquiry
        // exists and belongs to someone else — same message either way.
        if (enquiry is null || enquiry.OwnerUserId != callerId)
        {
            return TypedResults.Problem($"Enquiry {id} does not exist.", statusCode: StatusCodes.Status404NotFound);
        }

        var run = await agentRuns.GetLatestByEnquiryIdAsync(id, cancellationToken);
        var pendingApproval = run?.ApprovalRequestJson is { } approvalJson
            ? JsonSerializer.Deserialize<EnquiryPipeline.StoredApproval>(approvalJson, JsonOptions)?.Request
            : null;
        var trace = run?.TraceJson is { } traceJson
            ? JsonSerializer.Deserialize<List<AgentEvent>>(traceJson, JsonOptions)
            : null;

        return TypedResults.Ok(new EnquiryDetailResponse(
            enquiry.Id,
            enquiry.Channel,
            enquiry.SenderId,
            enquiry.RawBody,
            enquiry.ReceivedAt,
            enquiry.CustomerId,
            enquiry.Status,
            run?.Status,
            pendingApproval,
            trace));
    }
}

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using QuoteDesk.Agents.Pipeline;
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
        group.MapPost("/{id:int}/process", ProcessAsync);
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

        var enquiry = PasteAdapter.FromPastedText(senderId, request.Body, timeProvider.GetUtcNow());
        var result = await adapter.IngestAsync(enquiry, cancellationToken);

        return TypedResults.Created(
            $"/api/enquiries/{result.EnquiryId}",
            new EnquiryCreatedResponse(result.EnquiryId, result.Status));
    }

    /// <summary>Streams the pipeline over SSE. Returns <c>Task</c>, not an <c>IResult</c> — see
    /// <c>ApprovalEndpoints.DecideAsync</c>'s remarks for why a streaming endpoint takes this shape.
    /// A missing enquiry is not checked here: <see cref="EnquiryPipeline.StartAsync"/> already reports
    /// it as an <c>ErrorEvent</c> on the stream itself, which is the one error channel a client
    /// reading SSE is already watching.</summary>
    private static async Task ProcessAsync(
        int id,
        HttpContext context,
        EnquiryPipeline pipeline,
        IAgentRunRepository agentRuns,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await AgentEventStreamWriter.WriteAsync(
            context,
            pipeline.StartAsync(id, cancellationToken),
            async ct =>
            {
                // StartAsync creates its AgentRun row before anything else happens, so by the time
                // the stream has ended, the run this call created — if any — is the latest for this
                // enquiry. Null when the enquiry did not exist and no run was ever created.
                var run = await agentRuns.GetLatestByEnquiryIdAsync(id, ct);
                return run?.Id;
            },
            agentRuns,
            timeProvider,
            cancellationToken);
    }

    private static async Task<Results<Ok<EnquiryDetailResponse>, ProblemHttpResult>> GetByIdAsync(
        int id,
        IEnquiryRepository enquiries,
        IAgentRunRepository agentRuns,
        CancellationToken cancellationToken)
    {
        var enquiry = await enquiries.GetByIdAsync(id, cancellationToken);
        if (enquiry is null)
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

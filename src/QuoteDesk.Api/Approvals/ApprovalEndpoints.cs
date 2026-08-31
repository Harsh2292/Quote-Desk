using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using QuoteDesk.Agents.Pipeline;
using QuoteDesk.Api.Streaming;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Api.Approvals;

public sealed record ApprovalDecisionRequest(string Decision, string? RejectionReason);

/// <summary>What <c>GET /api/approvals</c> lists — one card's worth. <see cref="ApprovalId"/> is the
/// <c>AgentRun.Id</c> that <c>POST /api/approvals/{id}</c> takes, the same id
/// <see cref="EnquiryPipeline"/> already put in <c>ApprovalRequiredEvent.ApprovalId</c>.</summary>
public sealed record PendingApprovalSummary(int ApprovalId, int EnquiryId, DateTimeOffset CreatedAt, ApprovalRequest Request);

public static class ApprovalEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/approvals");

        group.MapGet("/", ListPendingAsync);
        group.MapPost("/{id:int}", DecideAsync);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<PendingApprovalSummary>>> ListPendingAsync(
        IAgentRunRepository agentRuns, CancellationToken cancellationToken)
    {
        var runs = await agentRuns.GetPendingApprovalsAsync(cancellationToken);

        var summaries = new List<PendingApprovalSummary>();
        foreach (var run in runs)
        {
            if (run.ApprovalRequestJson is not { } json)
            {
                continue;
            }

            var stored = JsonSerializer.Deserialize<EnquiryPipeline.StoredApproval>(json, JsonOptions);
            if (stored is not null)
            {
                summaries.Add(new PendingApprovalSummary(run.Id, run.EnquiryId, run.CreatedAt, stored.Request));
            }
        }

        return TypedResults.Ok<IReadOnlyList<PendingApprovalSummary>>(summaries);
    }

    /// <summary>
    /// Resumes a suspended run with a human decision — streamed over SSE, the same as
    /// <c>/process</c>, so the Approve stage's own <c>create_quote_draft</c>/<c>send_quote</c> tool
    /// calls reach the trace panel live. Returns <c>Task</c>, not an <c>IResult</c>: once SSE framing
    /// has started, nothing can write a status line over it, so the validation failures below write a
    /// <see cref="ProblemHttpResult"/> directly via <c>ExecuteAsync</c> and return before that point.
    /// </summary>
    private static async Task DecideAsync(
        int id,
        ApprovalDecisionRequest request,
        HttpContext context,
        ClaimsPrincipal principal,
        IAgentRunRepository agentRuns,
        EnquiryPipeline pipeline,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (request.Decision == "edit")
        {
            await TypedResults.Problem(
                "Editing a priced quote before approval is not yet supported — task 08 defines the edit payload once the approval card exists.",
                statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(context);
            return;
        }

        if (request.Decision is not ("approve" or "reject"))
        {
            await TypedResults.Problem(
                $"Unknown decision '{request.Decision}'. Expected 'approve' or 'reject'.",
                statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(context);
            return;
        }

        var run = await agentRuns.GetByIdAsync(id, cancellationToken);
        if (run is null || run.Status != AgentRunStatuses.PendingApproval || run.ApprovalRequestJson is not { } approvalJson)
        {
            await TypedResults.Problem(
                $"Approval {id} is not awaiting a decision.",
                statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(context);
            return;
        }

        // MapInboundClaims is disabled in Program.cs, so "sub" comes through exactly as JwtIssuer
        // wrote it — our own user id, not Google's subject.
        var subject = principal.FindFirst("sub")?.Value;
        if (subject is null || !int.TryParse(subject, out var approvedByUserId))
        {
            await TypedResults.Problem(
                "Token does not carry a valid subject.",
                statusCode: StatusCodes.Status401Unauthorized).ExecuteAsync(context);
            return;
        }

        var stored = JsonSerializer.Deserialize<EnquiryPipeline.StoredApproval>(approvalJson, JsonOptions)!;

        var decision = request.Decision == "approve"
            ? new ApprovalDecision
            {
                EnquiryId = run.EnquiryId,
                Approved = true,
                ApprovedByUserId = approvedByUserId,
                Quote = stored.Request.PricedQuote,
            }
            : new ApprovalDecision
            {
                EnquiryId = run.EnquiryId,
                Approved = false,
                ApprovedByUserId = approvedByUserId,
                RejectionReason = request.RejectionReason ?? "Rejected by approver.",
            };

        // The run id is already known — no lookup needed after the stream ends, unlike /process.
        await AgentEventStreamWriter.WriteAsync(
            context,
            pipeline.ResumeAsync(run.EnquiryId, decision, cancellationToken),
            _ => Task.FromResult<int?>(id),
            agentRuns,
            timeProvider,
            cancellationToken);
    }
}

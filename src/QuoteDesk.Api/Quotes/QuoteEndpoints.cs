using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using QuoteDesk.Agents.Pipeline;
using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Api.Quotes;

/// <summary>One row of <c>GET /api/quotes</c> — mirrors <see cref="QuoteSummaryRecord"/> exactly; kept
/// as a distinct Api-layer type only so no <c>QuoteDesk.Data</c> record ever appears in a response
/// shape directly, the same discipline every other endpoint in this project follows.</summary>
public sealed record QuoteSummaryResponse(
    int Id,
    int EnquiryId,
    string Number,
    string Status,
    int? CustomerId,
    string? CustomerName,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset ValidUntil,
    IReadOnlyList<string> ItemNames)
{
    public static QuoteSummaryResponse From(QuoteSummaryRecord r) =>
        new(r.Id, r.EnquiryId, r.Number, r.Status, r.CustomerId, r.CustomerName, r.Total, r.CreatedAt, r.ValidUntil, r.ItemNames);
}

public sealed record QuoteLineResponse(
    int Id,
    string Sku,
    string ItemName,
    int Qty,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal LineTotal,
    bool RequiresOverride,
    DateOnly? DispatchDate,
    DateOnly? DeliveryDate,
    string? Note);

/// <summary>The detail <c>GET /api/quotes/{id}</c> returns — full lines, plus the trace of the run
/// that produced this quote (docs/SPEC.md §8: "detail, with the trace that produced it").</summary>
public sealed record QuoteDetailResponse(
    int Id,
    int EnquiryId,
    string Number,
    string Status,
    decimal Subtotal,
    decimal Freight,
    decimal Tax,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset ValidUntil,
    string? ShipTo,
    DateOnly? RequiredBy,
    int? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? SentAt,
    IReadOnlyList<QuoteLineResponse> Lines,
    IReadOnlyList<AgentEvent>? Trace);

public static class QuoteEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quotes");

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:int}", GetByIdAsync);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<QuoteSummaryResponse>>> ListAsync(
        IQuoteRepository quotes, CancellationToken cancellationToken)
    {
        var records = await quotes.ListAsync(cancellationToken);

        return TypedResults.Ok<IReadOnlyList<QuoteSummaryResponse>>([.. records.Select(QuoteSummaryResponse.From)]);
    }

    private static async Task<Results<Ok<QuoteDetailResponse>, ProblemHttpResult>> GetByIdAsync(
        int id,
        IQuoteRepository quotes,
        IAgentRunRepository agentRuns,
        CancellationToken cancellationToken)
    {
        var quote = await quotes.GetByIdAsync(id, cancellationToken);
        if (quote is null)
        {
            return TypedResults.Problem($"Quote {id} does not exist.", statusCode: StatusCodes.Status404NotFound);
        }

        var run = await agentRuns.GetLatestByEnquiryIdAsync(quote.EnquiryId, cancellationToken);
        var trace = run?.TraceJson is { } traceJson
            ? JsonSerializer.Deserialize<List<AgentEvent>>(traceJson, JsonOptions)
            : null;

        return TypedResults.Ok(new QuoteDetailResponse(
            quote.Id,
            quote.EnquiryId,
            quote.Number,
            quote.Status,
            quote.Subtotal,
            quote.Freight,
            quote.Tax,
            quote.Total,
            quote.CreatedAt,
            quote.ValidUntil,
            quote.ShipTo,
            quote.RequiredBy,
            quote.ApprovedByUserId,
            quote.ApprovedAt,
            quote.SentAt,
            [.. quote.Lines.Select(l => new QuoteLineResponse(
                l.Id, l.Sku, l.ItemName, l.Qty, l.UnitPrice, l.DiscountPct, l.LineTotal, l.RequiresOverride, l.DispatchDate, l.DeliveryDate, l.Note))],
            trace));
    }
}

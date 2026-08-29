using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using QuoteDesk.Intake;

namespace QuoteDesk.Api.Enquiries;

public sealed record PasteEnquiryRequest(string Body, string? SenderId);

public sealed record EnquiryCreatedResponse(int EnquiryId, string Status);

public static class EnquiryEndpoints
{
    public static IEndpointRouteBuilder MapEnquiryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/enquiries");

        group.MapPost("/", CreateFromPasteAsync);

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
}

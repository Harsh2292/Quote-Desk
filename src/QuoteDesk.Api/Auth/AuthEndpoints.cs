using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Api.Auth;

public sealed record GoogleSignInRequest(string IdToken);

public sealed record AuthResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);

/// <summary>
/// The shape a client ever sees. Deliberately excludes <see cref="UserRecord.GoogleSubject"/> —
/// nothing outside QuoteDesk.Data needs Google's own identifier for this user.
/// </summary>
public sealed record UserDto(int Id, string Email, string Name, string? PictureUrl, string Role)
{
    public static UserDto From(UserRecord user) => new(user.Id, user.Email, user.Name, user.PictureUrl, user.Role);
}

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // The only anonymous route below the fallback "require authenticated user" policy — signing
        // in is how you get the token the policy demands everywhere else. "auth" is a stricter limit
        // stacked on top of the app-wide GlobalLimiter (Program.cs): each call costs a real Google
        // token verification, and this is the entire surface an unauthenticated caller can reach.
        group.MapPost("/google", SignInWithGoogleAsync).AllowAnonymous().RequireRateLimiting("auth");

        group.MapGet("/me", GetCurrentUserAsync);

        return app;
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> SignInWithGoogleAsync(
        GoogleSignInRequest request,
        IGoogleIdTokenValidator validator,
        IUserRepository users,
        IOptions<AuthOptions> options,
        IJwtIssuer jwtIssuer,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        GoogleIdentity identity;
        try
        {
            identity = await validator.ValidateAsync(request.IdToken, cancellationToken);
        }
        catch (InvalidGoogleTokenException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status401Unauthorized);
        }

        var now = timeProvider.GetUtcNow();
        var role = RoleResolver.Resolve(identity.Email, options.Value.AdminEmails);

        var user = await users.UpsertFromGoogleAsync(
            new GoogleUserUpsert(identity.Subject, identity.Email, identity.Name, identity.PictureUrl, role, now),
            cancellationToken);

        var issued = jwtIssuer.Issue(user, now);

        return TypedResults.Ok(new AuthResponse(issued.Token, issued.ExpiresAt, UserDto.From(user)));
    }

    private static async Task<Results<Ok<UserDto>, ProblemHttpResult>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        // MapInboundClaims is disabled in Program.cs, so the "sub" claim is not remapped to the
        // legacy XML claim type — it comes through exactly as JwtIssuer wrote it.
        var subject = principal.FindFirst("sub")?.Value;
        if (subject is null || !int.TryParse(subject, out var userId))
        {
            return TypedResults.Problem("Token does not carry a valid subject.", statusCode: StatusCodes.Status401Unauthorized);
        }

        // Reads the database rather than trusting the token's own claims, so a role change in
        // Auth:AdminEmails takes effect on the next request instead of waiting for the token to expire.
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return TypedResults.Problem("This account no longer exists.", statusCode: StatusCodes.Status401Unauthorized);
        }

        return TypedResults.Ok(UserDto.From(user));
    }
}

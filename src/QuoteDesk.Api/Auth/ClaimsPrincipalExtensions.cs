using System.Security.Claims;

namespace QuoteDesk.Api.Auth;

/// <summary>
/// One place to read our own user id back out of the bearer token. Every endpoint that scopes a
/// resource to its owner needs this same two lines — <c>MapInboundClaims</c> is disabled in
/// <c>Program.cs</c>, so the <c>sub</c> claim is not remapped to the legacy XML claim type and comes
/// through exactly as <see cref="JwtIssuer"/> wrote it, our own <c>AppUser.Id</c>, not Google's
/// subject.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal principal, out int userId)
    {
        var subject = principal.FindFirst("sub")?.Value;
        return int.TryParse(subject, out userId);
    }
}

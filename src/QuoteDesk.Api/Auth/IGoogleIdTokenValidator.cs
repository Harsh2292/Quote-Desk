namespace QuoteDesk.Api.Auth;

/// <summary>The verified identity extracted from a Google ID token — never the raw claim set.</summary>
public sealed record GoogleIdentity(string Subject, string Email, string Name, string? PictureUrl);

/// <summary>The presented token failed Google's own verification, or its email is unverified.</summary>
public sealed class InvalidGoogleTokenException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// An interface purely so integration tests can stub Google out — the same reason
/// <c>IChatClient</c> is stubbed rather than called for real in CI (CLAUDE.md).
/// </summary>
public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken);
}

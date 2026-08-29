using System.Text.Json;
using QuoteDesk.Api.Auth;

namespace QuoteDesk.IntegrationTests.Api;

/// <summary>
/// Replaces <see cref="GoogleIdTokenValidator"/> in <see cref="QuoteDeskApiFactory"/> so CI never
/// calls Google — the same principle CLAUDE.md states for stubbing <c>IChatClient</c>. A test builds
/// its "token" with <see cref="TokenFor"/>, which is really just the identity serialized as JSON;
/// the stub deserializes it back rather than verifying a real signature.
/// </summary>
public sealed class StubGoogleIdTokenValidator : IGoogleIdTokenValidator
{
    public const string InvalidToken = "not-a-real-token";

    public static string TokenFor(GoogleIdentity identity) => JsonSerializer.Serialize(identity);

    public Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        if (idToken == InvalidToken)
        {
            throw new InvalidGoogleTokenException("Stub rejected the token — it is not a serialized GoogleIdentity.");
        }

        GoogleIdentity? identity;
        try
        {
            identity = JsonSerializer.Deserialize<GoogleIdentity>(idToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidGoogleTokenException("Stub could not parse the token.", ex);
        }

        return Task.FromResult(identity ?? throw new InvalidGoogleTokenException("Stub received a null identity."));
    }
}

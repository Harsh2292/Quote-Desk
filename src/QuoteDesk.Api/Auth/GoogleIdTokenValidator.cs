using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace QuoteDesk.Api.Auth;

public sealed class GoogleIdTokenValidator(IOptions<AuthOptions> options) : IGoogleIdTokenValidator
{
    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idToken);
        cancellationToken.ThrowIfCancellationRequested();

        GoogleJsonWebSignature.Payload payload;
        try
        {
            // No CancellationToken overload exists on this call — confirmed against the installed
            // 1.76.0 package's XML docs before writing this.
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [options.Value.Google.ClientId],
            });
        }
        catch (InvalidJwtException ex)
        {
            throw new InvalidGoogleTokenException("Google rejected the sign-in token.", ex);
        }

        // An unverified email would let anyone claim an address that happens to be in Auth:AdminEmails.
        if (!payload.EmailVerified)
        {
            throw new InvalidGoogleTokenException("Google has not verified this account's email address.");
        }

        return new GoogleIdentity(payload.Subject, payload.Email, payload.Name, payload.Picture);
    }
}

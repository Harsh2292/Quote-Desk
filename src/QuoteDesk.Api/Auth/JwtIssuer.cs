using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using QuoteDesk.Data;

namespace QuoteDesk.Api.Auth;

public sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt);

public interface IJwtIssuer
{
    /// <summary>
    /// Mints a bearer token for <paramref name="user"/>. <paramref name="now"/> is a parameter,
    /// never read from the clock, so the expiry is deterministic under test — the same discipline
    /// QuoteDesk.Domain applies to every date calculation.
    /// </summary>
    IssuedToken Issue(UserRecord user, DateTimeOffset now);
}

public sealed class JwtIssuer(IOptions<AuthOptions> options) : IJwtIssuer
{
    private static readonly JsonWebTokenHandler Handler = new();

    public IssuedToken Issue(UserRecord user, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwt = options.Value.Jwt;
        var expiresAt = now.AddHours(jwt.LifetimeHours);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                // Our own user id, not Google's sub — the sub claim never leaves QuoteDesk.Data.
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(CultureInfo.InvariantCulture),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.Name] = user.Name,
                ["role"] = user.Role,
            },
        };

        var token = Handler.CreateToken(descriptor);
        return new IssuedToken(token, expiresAt);
    }
}

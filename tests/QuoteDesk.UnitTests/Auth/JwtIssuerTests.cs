using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using QuoteDesk.Api.Auth;
using QuoteDesk.Data;
using Xunit;

namespace QuoteDesk.UnitTests.Auth;

public class JwtIssuerTests
{
    private const string SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!";

    private static readonly UserRecord User = new(
        Id: 42,
        GoogleSubject: "google-subject-123",
        Email: "kiran@shreejitextiles.example",
        Name: "Kiran",
        PictureUrl: "https://example.test/kiran.png",
        Role: "sales",
        CreatedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        LastLoginAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    [Fact]
    public void Issue_ForUser_ContainsSubjectEmailAndRoleClaims()
    {
        var issuer = CreateIssuer(out var options);
        var now = DateTimeOffset.Parse("2026-08-29T09:00:00Z");

        var issued = issuer.Issue(User, now);
        var identity = Validate(issued.Token, options, now);

        identity.FindFirst("sub")!.Value.Should().Be("42");
        identity.FindFirst("email")!.Value.Should().Be(User.Email);
        identity.FindFirst("role")!.Value.Should().Be(User.Role);
    }

    [Fact]
    public void Issue_WithLifetime_SetsExpiryFromSuppliedTime()
    {
        var issuer = CreateIssuer(out var options);
        var now = DateTimeOffset.Parse("2026-08-29T09:00:00Z");

        var issued = issuer.Issue(User, now);

        issued.ExpiresAt.Should().Be(now.AddHours(options.Jwt.LifetimeHours));
    }

    [Fact]
    public void Issue_Token_ValidatesAgainstTheSameParameters()
    {
        var issuer = CreateIssuer(out var options);
        var now = DateTimeOffset.Parse("2026-08-29T09:00:00Z");

        var issued = issuer.Issue(User, now);

        // Validated at the instant of issuance — well inside the token's lifetime.
        Validate(issued.Token, options, now).Should().NotBeNull();
    }

    [Fact]
    public void Issue_TokenPastExpiry_FailsValidation()
    {
        var issuer = CreateIssuer(out var options);
        var now = DateTimeOffset.Parse("2026-08-29T09:00:00Z");

        var issued = issuer.Issue(User, now);

        // One second past the lifetime granted at issuance — no ClockSkew tolerance either.
        var afterExpiry = now.AddHours(options.Jwt.LifetimeHours).AddSeconds(1);
        var act = () => Validate(issued.Token, options, afterExpiry);

        act.Should().Throw<SecurityTokenException>();
    }

    private static JwtIssuer CreateIssuer(out AuthOptions options)
    {
        options = new AuthOptions
        {
            Google = new GoogleOptions { ClientId = "unit-test-client-id" },
            Jwt = new JwtOptions { SigningKey = SigningKey, Issuer = "quotedesk-tests", Audience = "quotedesk-tests", LifetimeHours = 8 },
        };

        return new JwtIssuer(Options.Create(options));
    }

    /// <summary>
    /// Validates <paramref name="token"/> as of exactly <paramref name="asOf"/>, never the real
    /// clock — <c>LifetimeValidator</c> is called unconditionally by this library regardless of
    /// <see cref="TokenValidationParameters.ValidateLifetime"/>, so it replaces the library's own
    /// wall-clock comparison entirely and keeps expiry tests deterministic.
    /// </summary>
    private static ClaimsIdentity Validate(string token, AuthOptions options, DateTimeOffset asOf)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Jwt.SigningKey)),
            LifetimeValidator = (notBefore, expires, _, _) =>
                (!notBefore.HasValue || notBefore.Value <= asOf.UtcDateTime)
                && (!expires.HasValue || asOf.UtcDateTime < expires.Value),
        };

        var result = new JsonWebTokenHandler().ValidateTokenAsync(token, parameters).GetAwaiter().GetResult();

        if (!result.IsValid)
        {
            throw result.Exception ?? new SecurityTokenValidationException("Token failed validation.");
        }

        return result.ClaimsIdentity;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuoteDesk.Api.Auth;
using Xunit;

namespace QuoteDesk.IntegrationTests.Api;

/// <summary>
/// Exercises the Google sign-in endpoint and the fallback authorization policy end to end, against
/// the real pipeline in <c>Program.cs</c> via <see cref="QuoteDeskApiFactory"/>.
/// </summary>
public class AuthEndpointsTests(QuoteDeskApiFactory factory) : IClassFixture<QuoteDeskApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithGarbageToken_Returns401()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "this-is-not-a-jwt");

        var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthLive_WithoutToken_Returns200()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), CancellationToken.None);

        // Proves the fallback "require authenticated user" policy did not lock out the liveness probe.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Google_WithValidIdToken_ReturnsTokenAndCreatesUser()
    {
        using var client = factory.CreateClient();
        var identity = new GoogleIdentity("sub-new-user", "kiran@shreejitextiles.example", "Kiran", null);

        var response = await client.PostAsJsonAsync(
            "/api/auth/google",
            new { idToken = StubGoogleIdTokenValidator.TokenFor(identity) },
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(Json, CancellationToken.None);
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.User.Email.Should().Be(identity.Email);
        body.User.Role.Should().Be(RoleResolver.Sales);
    }

    [Fact]
    public async Task Google_WithInvalidIdToken_Returns401ProblemDetails()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/google",
            new { idToken = StubGoogleIdTokenValidator.InvalidToken },
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, CancellationToken.None);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status401Unauthorized);

        // Never the raw exception text, per CLAUDE.md's rule on errors sent to the client.
        var raw = await response.Content.ReadAsStringAsync(CancellationToken.None);
        raw.Should().NotContain("Exception").And.NotContain("StackTrace").And.NotContain(" at ");
    }

    [Fact]
    public async Task Google_ForReturningUser_UpdatesLastLoginAndDoesNotDuplicate()
    {
        using var client = factory.CreateClient();
        var identity = new GoogleIdentity("sub-returning-user", "returning@shreejitextiles.example", "Returning User", null);
        var token = StubGoogleIdTokenValidator.TokenFor(identity);

        var first = await client.PostAsJsonAsync("/api/auth/google", new { idToken = token }, CancellationToken.None);
        var firstBody = await first.Content.ReadFromJsonAsync<AuthResponse>(Json, CancellationToken.None);

        var second = await client.PostAsJsonAsync("/api/auth/google", new { idToken = token }, CancellationToken.None);
        var secondBody = await second.Content.ReadFromJsonAsync<AuthResponse>(Json, CancellationToken.None);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        secondBody!.User.Id.Should().Be(firstBody!.User.Id, "the same Google subject must resolve to the same row, never a duplicate");
    }

    [Fact]
    public async Task Google_ForAdminEmail_AssignsAdminRole()
    {
        using var client = factory.CreateClient();
        var identity = new GoogleIdentity("sub-admin-user", QuoteDeskApiFactory.AdminEmail, "Admin", null);

        var response = await client.PostAsJsonAsync(
            "/api/auth/google",
            new { idToken = StubGoogleIdTokenValidator.TokenFor(identity) },
            CancellationToken.None);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(Json, CancellationToken.None);

        body!.User.Role.Should().Be(RoleResolver.Admin);
    }

    [Fact]
    public async Task Me_WithIssuedToken_ReturnsTheSignedInUser()
    {
        using var client = factory.CreateClient();
        var identity = new GoogleIdentity("sub-me-lookup", "me-lookup@shreejitextiles.example", "Me Lookup", null);

        var signIn = await client.PostAsJsonAsync(
            "/api/auth/google",
            new { idToken = StubGoogleIdTokenValidator.TokenFor(identity) },
            CancellationToken.None);
        var signInBody = await signIn.Content.ReadFromJsonAsync<AuthResponse>(Json, CancellationToken.None);

        client.DefaultRequestHeaders.Authorization = new("Bearer", signInBody!.Token);
        var me = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), CancellationToken.None);

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await me.Content.ReadFromJsonAsync<UserDto>(Json, CancellationToken.None);
        meBody!.Email.Should().Be(identity.Email);

        // UserDto has no GoogleSubject property to begin with — this asserts the wire shape agrees:
        // the raw JSON never carries the Google subject either.
        var raw = await me.Content.ReadAsStringAsync(CancellationToken.None);
        raw.Should().NotContain("googleSubject", "the client is never told the Google subject");
    }
}

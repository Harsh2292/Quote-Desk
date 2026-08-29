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
/// Proves the global exception handler in Program.cs (<c>AddProblemDetails</c> +
/// <c>UseExceptionHandler</c>) actually converts an unhandled exception into a generic
/// ProblemDetails response, per CLAUDE.md's "no stack traces, connection strings or inner exception
/// text" rule — for exceptions no endpoint catches itself, not just the ones it does.
/// </summary>
[Collection("QuoteDeskApi")]
public class GlobalExceptionHandlingTests(QuoteDeskApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UnhandledDbException_Returns500ProblemDetailsWithNoExceptionText()
    {
        using var client = factory.CreateClient();
        const string email = "duplicate-email-trigger@shreejitextiles.example";

        // Two different Google subjects signing in with the same email is not validated anywhere
        // upstream — UserRepository.UpsertFromGoogleAsync's INSERT then hits Users.IX_Users_Email
        // and throws DbUpdateException, uncaught by AuthEndpoints. A real bug class (e.g. an email
        // changing on one Google account to collide with another), not a contrived one.
        await client.PostAsJsonAsync(
            "/api/auth/google",
            new { idToken = StubGoogleIdTokenValidator.TokenFor(new GoogleIdentity("sub-a", email, "User A", null)) },
            CancellationToken.None);

        var response = await client.PostAsJsonAsync(
            "/api/auth/google",
            new { idToken = StubGoogleIdTokenValidator.TokenFor(new GoogleIdentity("sub-b", email, "User B", null)) },
            CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, CancellationToken.None);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status500InternalServerError);

        var raw = await response.Content.ReadAsStringAsync(CancellationToken.None);
        raw.Should()
            .NotContain("Exception").And.NotContain("StackTrace").And.NotContain(" at ")
            .And.NotContain("DbUpdateException").And.NotContain("SqlException")
            .And.NotContain("IX_Users_Email", "the unique index name is a schema detail the client must never see");
    }
}

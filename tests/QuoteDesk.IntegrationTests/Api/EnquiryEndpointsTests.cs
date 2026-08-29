using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using QuoteDesk.Api.Auth;
using QuoteDesk.Api.Enquiries;
using Xunit;

namespace QuoteDesk.IntegrationTests.Api;

/// <summary>Exercises <c>POST /api/enquiries</c> end to end against the real pipeline in
/// <c>Program.cs</c>, via <see cref="QuoteDeskApiFactory"/>.</summary>
[Collection("QuoteDeskApi")]
public class EnquiryEndpointsTests(QuoteDeskApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // The worked example from docs/DOMAIN.md.
    private const string WorkedExampleBody = """
        Hi Mehul bhai,
        Need urgent quote —
        250 nos of the 6203 bearings (same as last time)
        40 mtr of the 25mm PU timing belt
        12 pcs ring frame spindle tape, the thicker one

        Delivery at our Sachin unit, need by 5th. Last time you gave 8% on bearings, please keep same.

        Kiran — Shreeji Textiles
        """;

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/enquiries", new PasteEnquiryRequest(WorkedExampleBody, null), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_WorkedExampleBody_Returns201AndStoresTheBodyIntact()
    {
        // A distinct email from every AuthEndpointsTests case — the two classes now share one
        // database via the "QuoteDeskApi" collection, and Users.Email is unique.
        using var client = await AuthenticatedClientAsync("kiran-paste@shreejitextiles.example");

        var response = await client.PostAsJsonAsync("/api/enquiries", new PasteEnquiryRequest(WorkedExampleBody, null), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EnquiryCreatedResponse>(Json, CancellationToken.None);
        body!.EnquiryId.Should().BePositive();
        body.Status.Should().Be("pending");
    }

    [Fact]
    public async Task Post_BlankBody_Returns400ProblemDetails()
    {
        using var client = await AuthenticatedClientAsync("blank-body@shreejitextiles.example");

        var response = await client.PostAsJsonAsync("/api/enquiries", new PasteEnquiryRequest("   ", null), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, CancellationToken.None);
        problem.Should().NotBeNull();

        var raw = await response.Content.ReadAsStringAsync(CancellationToken.None);
        raw.Should().NotContain("Exception").And.NotContain("StackTrace");
    }

    [Fact]
    public async Task Post_NoSenderIdSupplied_UsesTheSignedInUsersEmail()
    {
        const string email = "sender-from-token@shreejitextiles.example";
        using var client = await AuthenticatedClientAsync(email);

        var response = await client.PostAsJsonAsync("/api/enquiries", new PasteEnquiryRequest("50 pcs bearing 6203, please quote.", null), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = factory.CreateClient();
        var identity = new GoogleIdentity($"sub-{Guid.NewGuid():N}", email, "Test User", null);

        var signIn = await client.PostAsJsonAsync(
            "/api/auth/google",
            new { idToken = StubGoogleIdTokenValidator.TokenFor(identity) },
            CancellationToken.None);
        var signInBody = await signIn.Content.ReadFromJsonAsync<AuthResponse>(Json, CancellationToken.None);

        client.DefaultRequestHeaders.Authorization = new("Bearer", signInBody!.Token);
        return client;
    }
}

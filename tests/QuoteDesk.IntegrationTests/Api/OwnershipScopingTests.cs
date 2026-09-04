using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using QuoteDesk.Agents.Pipeline;
using QuoteDesk.Api.Approvals;
using QuoteDesk.Api.Auth;
using QuoteDesk.Api.Enquiries;
using QuoteDesk.Api.Quotes;
using QuoteDesk.Data.Repositories;
using QuoteDesk.IntegrationTests.Agents;
using Xunit;

namespace QuoteDesk.IntegrationTests.Api;

/// <summary>
/// Proves the per-user ownership scoping actually holds. Before this, <c>GET /api/approvals</c>
/// listed every pending run globally and <c>POST /api/approvals/{id}</c> validated only that a run was
/// pending — on a public demo where any Google account can sign in, a stranger could read another
/// visitor's enquiries and order history, and could approve or reject someone else's quote (the write
/// path — <c>ApproveExecutor</c> holds <c>create_quote_draft</c>/<c>send_quote</c>). Every case below
/// exercises the real HTTP host, the real seeded database and a scripted model — no code here trusts
/// the fix by inspection, each one drives the actual endpoint two different signed-in users hit.
/// </summary>
[Collection("QuoteDeskApi")]
public class OwnershipScopingTests(QuoteDeskApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Enquiry_CreatedByOneUser_Returns404ForAnotherSignedInUser()
    {
        using var owner = await AuthenticatedClientAsync("owner-enquiry@shreejitextiles.example");
        using var stranger = await AuthenticatedClientAsync("stranger-enquiry@shreejitextiles.example");

        var response = await owner.PostAsJsonAsync(
            "/api/enquiries", new PasteEnquiryRequest("50 pcs bearing 6203, please quote.", "kiran@shreejitextiles.com"), CancellationToken.None);
        var created = await response.Content.ReadFromJsonAsync<EnquiryCreatedResponse>(Json, CancellationToken.None);

        var ownerRead = await owner.GetAsync($"/api/enquiries/{created!.EnquiryId}", CancellationToken.None);
        ownerRead.StatusCode.Should().Be(HttpStatusCode.OK, "the creator can always read their own enquiry");

        var strangerRead = await stranger.GetAsync($"/api/enquiries/{created.EnquiryId}", CancellationToken.None);
        strangerRead.StatusCode.Should().Be(HttpStatusCode.NotFound, "a stranger must not even learn the enquiry exists");
    }

    [Fact]
    public async Task Process_OnAnotherUsersEnquiry_Returns404AndNeverOpensTheStream()
    {
        using var owner = await AuthenticatedClientAsync("owner-process@shreejitextiles.example");
        using var stranger = await AuthenticatedClientAsync("stranger-process@shreejitextiles.example");

        var response = await owner.PostAsJsonAsync(
            "/api/enquiries", new PasteEnquiryRequest("50 pcs bearing 6203, please quote.", "kiran@shreejitextiles.com"), CancellationToken.None);
        var created = await response.Content.ReadFromJsonAsync<EnquiryCreatedResponse>(Json, CancellationToken.None);

        var processResponse = await stranger.PostAsync($"/api/enquiries/{created!.EnquiryId}/process", content: null, CancellationToken.None);

        // A plain 404 ProblemDetails, not an opened SSE stream — the check runs before
        // AgentEventStreamWriter.WriteAsync, so a stranger never gets a live trace or spends the
        // owner's Gemini quota on their own enquiry.
        processResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        processResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Approvals_PendingList_NeverShowsAnotherUsersRun()
    {
        using var owner = await AuthenticatedClientAsync("owner-list@shreejitextiles.example");
        using var stranger = await AuthenticatedClientAsync("stranger-list@shreejitextiles.example");
        await ScriptWorkedExampleAsync();

        var enquiryId = await CreateWorkedExampleEnquiryAsync(owner);
        var events = await ProcessAndReadEventsAsync(owner, enquiryId);
        var approvalId = events.OfType<ApprovalRequiredEvent>().Single().ApprovalId;

        var ownerPending = await owner.GetFromJsonAsync<List<PendingApprovalSummary>>("/api/approvals", Json, CancellationToken.None);
        ownerPending.Should().ContainSingle(a => a.ApprovalId.ToString() == approvalId);

        var strangerPending = await stranger.GetFromJsonAsync<List<PendingApprovalSummary>>("/api/approvals", Json, CancellationToken.None);
        strangerPending.Should().NotContain(a => a.ApprovalId.ToString() == approvalId, "a stranger must not see another user's pending approval");
    }

    [Fact]
    public async Task Approve_OnAnotherUsersRun_Returns404AndCreatesNoQuote()
    {
        using var owner = await AuthenticatedClientAsync("owner-approve@shreejitextiles.example");
        using var stranger = await AuthenticatedClientAsync("stranger-approve@shreejitextiles.example");
        await ScriptWorkedExampleAsync();

        var enquiryId = await CreateWorkedExampleEnquiryAsync(owner);
        var events = await ProcessAndReadEventsAsync(owner, enquiryId);
        var approvalId = events.OfType<ApprovalRequiredEvent>().Single().ApprovalId;

        // The write path: this is the one endpoint that reaches create_quote_draft/send_quote. If the
        // ownership check here has a gap, a stranger can approve or reject someone else's quote.
        var strangerDecision = await stranger.PostAsJsonAsync(
            $"/api/approvals/{approvalId}", new ApprovalDecisionRequest("approve", null), Json, CancellationToken.None);
        strangerDecision.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var strangerQuotes = await stranger.GetFromJsonAsync<List<QuoteSummaryResponse>>("/api/quotes", Json, CancellationToken.None);
        strangerQuotes.Should().NotContain(q => q.EnquiryId == enquiryId, "the rejected decision must not have created anything");

        // The owner can still approve for real — proves the ownership check does not also block the
        // legitimate caller.
        var ownerDecision = await owner.PostAsJsonAsync(
            $"/api/approvals/{approvalId}", new ApprovalDecisionRequest("approve", null), Json, CancellationToken.None);
        ownerDecision.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Quotes_ListAndDetail_ScopedToTheApprovingUser()
    {
        using var owner = await AuthenticatedClientAsync("owner-quotes@shreejitextiles.example");
        using var stranger = await AuthenticatedClientAsync("stranger-quotes@shreejitextiles.example");
        await ScriptWorkedExampleAsync();

        var enquiryId = await CreateWorkedExampleEnquiryAsync(owner);
        var events = await ProcessAndReadEventsAsync(owner, enquiryId);
        var approvalId = events.OfType<ApprovalRequiredEvent>().Single().ApprovalId;

        await owner.PostAsJsonAsync($"/api/approvals/{approvalId}", new ApprovalDecisionRequest("approve", null), Json, CancellationToken.None);

        var ownerQuotes = await owner.GetFromJsonAsync<List<QuoteSummaryResponse>>("/api/quotes", Json, CancellationToken.None);
        var quote = ownerQuotes.Should().ContainSingle(q => q.EnquiryId == enquiryId).Which;

        var strangerQuotes = await stranger.GetFromJsonAsync<List<QuoteSummaryResponse>>("/api/quotes", Json, CancellationToken.None);
        strangerQuotes.Should().NotContain(q => q.Id == quote.Id);

        var strangerDetail = await stranger.GetAsync($"/api/quotes/{quote.Id}", CancellationToken.None);
        strangerDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownerDetail = await owner.GetAsync($"/api/quotes/{quote.Id}", CancellationToken.None);
        ownerDetail.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task ScriptWorkedExampleAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var shreeji = await customers.FindByEmailDomainAsync("shreejitextiles.com", CancellationToken.None);

        factory.Services.GetRequiredService<ScriptableChatClient>()
            .Script(WorkedExampleScript.BuildWorkedExampleTurns(shreeji!.Id));
    }

    private static async Task<int> CreateWorkedExampleEnquiryAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/enquiries", new PasteEnquiryRequest(WorkedExampleScript.Body, WorkedExampleScript.SenderId), CancellationToken.None);
        var created = await response.Content.ReadFromJsonAsync<EnquiryCreatedResponse>(Json, CancellationToken.None);
        return created!.EnquiryId;
    }

    private static async Task<List<AgentEvent>> ProcessAndReadEventsAsync(HttpClient client, int enquiryId)
    {
        var response = await client.PostAsync($"/api/enquiries/{enquiryId}/process", content: null, CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync(CancellationToken.None);
        return ParseSseFrames(raw);
    }

    private static List<AgentEvent> ParseSseFrames(string raw)
    {
        var events = new List<AgentEvent>();
        foreach (var frame in raw.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = frame.Trim();
            if (!trimmed.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var evt = JsonSerializer.Deserialize<AgentEvent>(trimmed["data: ".Length..], Json);
            if (evt is not null)
            {
                events.Add(evt);
            }
        }

        return events;
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = factory.CreateClient();
        var identity = new GoogleIdentity($"sub-{Guid.NewGuid():N}", email, "Test User", null);

        var signIn = await client.PostAsJsonAsync(
            "/api/auth/google", new { idToken = StubGoogleIdTokenValidator.TokenFor(identity) }, CancellationToken.None);
        var signInBody = await signIn.Content.ReadFromJsonAsync<AuthResponse>(Json, CancellationToken.None);

        client.DefaultRequestHeaders.Authorization = new("Bearer", signInBody!.Token);
        return client;
    }
}

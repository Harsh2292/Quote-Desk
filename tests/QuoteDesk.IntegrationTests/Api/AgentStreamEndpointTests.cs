using System.ClientModel;
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

namespace QuoteDesk.IntegrationTests.Api;

/// <summary>
/// Exercises the pipeline reachable over HTTP for the first time (task 07) — <c>/process</c> and
/// <c>/approvals/{id}</c> streamed as SSE, against the real seeded database and a scripted
/// <see cref="ScriptableChatClient"/>. docs/DOMAIN.md's worked example is the primary eval case,
/// reused here via <see cref="WorkedExampleScript"/> rather than re-authored per test class.
/// </summary>
[Collection("QuoteDeskApi")]
public class AgentStreamEndpointTests(QuoteDeskApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Process_WithWorkedExample_EmitsEveryEventVariant()
    {
        using var client = await AuthenticatedClientAsync("stream-full-example@shreejitextiles.example");
        await ScriptWorkedExampleAsync();

        var enquiryId = await CreateWorkedExampleEnquiryAsync(client);
        var events = await ProcessAndReadEventsAsync(client, enquiryId);

        // TokenEvent is not asserted here: no stage in today's pipeline actually emits one — Price's
        // narration calls narrateAgent.RunAsync non-streaming (docs/SPEC.md §4 describes streaming
        // the closing narration, but task 06 never implemented that half). The SSE transport itself
        // is variant-agnostic (AgentEventStreamWriter serializes whatever AgentEvent arrives), so a
        // token event will flow the moment some stage actually produces one — see docs/SPEC.md §4 and
        // tasks/task-07-api.md's Notes on completion for the gap this records.
        events.Should().Contain(e => e is StageEvent);
        events.Should().Contain(e => e is ToolStartEvent);
        events.Should().Contain(e => e is ToolEndEvent);
        events.Should().ContainSingle(e => e is ApprovalRequiredEvent, "the spindle tape line is genuinely ambiguous and must stop for a human");

        events.OfType<ToolStartEvent>().Select(e => e.Name).Should()
            .NotContain(["price_quote", "create_quote_draft", "send_quote"], "money and writes never happen before approval");
    }

    [Fact]
    public async Task Process_WhenProviderReturns429_EmitsProviderRateLimited()
    {
        using var client = await AuthenticatedClientAsync("stream-429@shreejitextiles.example");

        var response = await client.PostAsJsonAsync(
            "/api/enquiries", new PasteEnquiryRequest("50 pcs bearing 6203, please quote.", "kiran@shreejitextiles.com"), CancellationToken.None);
        var created = await response.Content.ReadFromJsonAsync<EnquiryCreatedResponse>(Json, CancellationToken.None);

        var chatClient = factory.Services.GetRequiredService<ScriptableChatClient>();
        chatClient.ScriptThrow(new RateLimitedException());

        var events = await ProcessAndReadEventsAsync(client, created!.EnquiryId);

        var error = events.Should().ContainSingle(e => e is ErrorEvent).Which.Should().BeOfType<ErrorEvent>().Subject;
        error.Code.Should().Be("provider_rate_limited");
        error.Message.Should().NotContain("Exception").And.NotContain("StackTrace");
    }

    /// <summary>Found live (docs/SPEC.md §4): switching the "gemini" profile to Google's native SDK
    /// means a real rate limit throws <see cref="Google.GenAI.ClientError"/>, not
    /// <see cref="System.ClientModel.ClientResultException"/> — a genuine free-tier daily quota
    /// (20 requests/day for gemini-3.6-flash) hit this exact path during manual verification and, before
    /// <c>EnquiryPipeline.ToErrorEvent</c> was extended, fell through to a generic "internal" error
    /// instead of <c>provider_rate_limited</c>.</summary>
    [Fact]
    public async Task Process_WhenGoogleGenAiThrowsClientError429_EmitsProviderRateLimited()
    {
        using var client = await AuthenticatedClientAsync("stream-quota@shreejitextiles.example");

        var response = await client.PostAsJsonAsync(
            "/api/enquiries", new PasteEnquiryRequest("50 pcs bearing 6203, please quote.", "kiran@shreejitextiles.com"), CancellationToken.None);
        var created = await response.Content.ReadFromJsonAsync<EnquiryCreatedResponse>(Json, CancellationToken.None);

        var chatClient = factory.Services.GetRequiredService<ScriptableChatClient>();
        chatClient.ScriptThrow(new Google.GenAI.ClientError("Quota exceeded.", 429, "RESOURCE_EXHAUSTED"));

        var events = await ProcessAndReadEventsAsync(client, created!.EnquiryId);

        var error = events.Should().ContainSingle(e => e is ErrorEvent).Which.Should().BeOfType<ErrorEvent>().Subject;
        error.Code.Should().Be("provider_rate_limited");
        error.Message.Should().NotContain("Exception").And.NotContain("StackTrace");
    }

    [Fact]
    public async Task Process_ThenApprove_CompletesAndListsTheQuote()
    {
        using var client = await AuthenticatedClientAsync("stream-approve@shreejitextiles.example");
        await ScriptWorkedExampleAsync();

        var enquiryId = await CreateWorkedExampleEnquiryAsync(client);
        var processEvents = await ProcessAndReadEventsAsync(client, enquiryId);
        var approvalId = processEvents.OfType<ApprovalRequiredEvent>().Single().ApprovalId;

        var pending = await client.GetFromJsonAsync<List<PendingApprovalSummary>>("/api/approvals", Json, CancellationToken.None);
        pending.Should().ContainSingle(a => a.ApprovalId.ToString() == approvalId && a.EnquiryId == enquiryId);

        var decideResponse = await client.PostAsJsonAsync(
            $"/api/approvals/{approvalId}", new ApprovalDecisionRequest("approve", null), Json, CancellationToken.None);
        decideResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var decideRaw = await decideResponse.Content.ReadAsStringAsync(CancellationToken.None);
        var decideEvents = ParseSseFrames(decideRaw);

        decideEvents.Should().ContainSingle(e => e is DoneEvent);
        decideEvents.OfType<ToolStartEvent>().Select(e => e.Name).Should().ContainInOrder("create_quote_draft", "send_quote");

        var quotes = await client.GetFromJsonAsync<List<QuoteSummaryResponse>>("/api/quotes", Json, CancellationToken.None);
        quotes.Should().ContainSingle(q => q.EnquiryId == enquiryId && q.Number.StartsWith("QTN-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Process_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/enquiries/999999/process", content: null, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Enquiry_AfterProcessing_ReturnsThePersistedTrace()
    {
        using var client = await AuthenticatedClientAsync("stream-trace@shreejitextiles.example");
        await ScriptWorkedExampleAsync();

        var enquiryId = await CreateWorkedExampleEnquiryAsync(client);
        await ProcessAndReadEventsAsync(client, enquiryId);

        var detail = await client.GetFromJsonAsync<EnquiryDetailResponse>($"/api/enquiries/{enquiryId}", Json, CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.RunStatus.Should().Be("pending_approval");
        detail.PendingApproval.Should().NotBeNull();
        detail.Trace.Should().NotBeNullOrEmpty();
        detail.Trace!.Should().Contain(e => e is ApprovalRequiredEvent, "the live stream's own events must survive after the connection has closed");
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

    /// <summary>Splits an SSE body into its <c>data: {...}</c> frames and deserializes each back into
    /// an <see cref="AgentEvent"/> — the polymorphic <c>type</c> discriminator does the rest, the same
    /// options the server serialized with.</summary>
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

    /// <summary>A <see cref="ClientResultException"/> with <see cref="ClientResultException.Status"/>
    /// forced to 429 — <see cref="StubChatClient"/> only ever returns scripted responses, never
    /// throws, so a real provider rate-limit has to be simulated this way instead. <c>Status</c>'s
    /// setter is <c>protected</c>, so only a derived type can set it without a real
    /// <c>PipelineResponse</c> in hand.</summary>
    private sealed class RateLimitedException : ClientResultException
    {
        public RateLimitedException() : base("Simulated 429 for a test.", response: null)
        {
            Status = 429;
        }
    }
}

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using QuoteDesk.Api.RateLimiting;

namespace QuoteDesk.UnitTests.RateLimiting;

public class RateLimitRejectionMessagesTests
{
    [Theory]
    [InlineData("/api/enquiries/42/process", RateLimitRejectionMessages.PipelineDailyCap)]
    [InlineData("/api/enquiries/1/process", RateLimitRejectionMessages.PipelineDailyCap)]
    public void For_ThePipelineRoute_ReturnsTheDailyCapMessage(string path, string expected)
    {
        RateLimitRejectionMessages.For(new PathString(path)).Should().Be(expected);
    }

    [Theory]
    [InlineData("/api/auth/google")]
    [InlineData("/api/approvals/7")]
    [InlineData("/api/quotes")]
    [InlineData("/api/enquiries/42/process/")] // trailing slash — not an exact "/process" suffix
    public void For_EveryOtherRoute_ReturnsTheGenericMessage(string path)
    {
        RateLimitRejectionMessages.For(new PathString(path)).Should().Be(RateLimitRejectionMessages.Generic);
    }
}

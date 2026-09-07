using System.Net;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.ExternalProviders.ApiFootball;

public class ApiFootballAuthHandlerTests
{
    [Fact]
    public async Task SendAsync_WithApiKey_AddsAuthHeader()
    {
        var options = new TestOptionsMonitor<ApiFootballOptions>(new ApiFootballOptions { BaseUrl = "https://v3.football.api-sports.io", ApiKey = "test-key" });
        var inner = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new ApiFootballAuthHandler(options) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://v3.football.api-sports.io") };

        await client.GetAsync("countries");

        var sentRequest = Assert.Single(inner.Requests);
        Assert.True(sentRequest.Headers.TryGetValues("x-apisports-key", out var values));
        Assert.Equal("test-key", values!.Single());
    }

    [Fact]
    public async Task SendAsync_WithoutApiKey_ThrowsNotConfigured()
    {
        var options = new TestOptionsMonitor<ApiFootballOptions>(new ApiFootballOptions { BaseUrl = "https://v3.football.api-sports.io", ApiKey = null });
        var inner = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new ApiFootballAuthHandler(options) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://v3.football.api-sports.io") };

        await Assert.ThrowsAsync<ApiFootballNotConfiguredException>(() => client.GetAsync("countries"));
        Assert.Empty(inner.Requests); // must fail fast, before any network call
    }
}

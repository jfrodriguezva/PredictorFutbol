using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;
using SportsPredictor.Infrastructure.Persistence;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.ExternalProviders.ApiFootball;

public class ApiFootballRateLimitHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    public ApiFootballRateLimitHandlerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<SportsPredictorDbContext>(options => options.UseSqlite(_connection));
        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<SportsPredictorDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    private ApiFootballRateLimitHandler CreateHandler(int maxRetries = 3) =>
        new(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new TestOptionsMonitor<ApiFootballOptions>(new ApiFootballOptions { BaseUrl = "https://v3.football.api-sports.io", MaxRetries = maxRetries }),
            NullLogger<ApiFootballRateLimitHandler>.Instance);

    [Fact]
    public async Task SendAsync_SuccessfulResponse_PersistsQuotaSnapshot()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("x-ratelimit-requests-limit", "75000");
        response.Headers.Add("x-ratelimit-requests-remaining", "74999");
        response.Headers.Add("X-RateLimit-Limit", "450");
        response.Headers.Add("X-RateLimit-Remaining", "449");
        var inner = FakeHttpMessageHandler.Sequence(response);
        var handler = CreateHandler();
        handler.InnerHandler = inner;
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://v3.football.api-sports.io") };

        await client.GetAsync("countries");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SportsPredictorDbContext>();
        var snapshot = Assert.Single(dbContext.ApiQuotaSnapshots);
        Assert.Equal(75000, snapshot.DailyLimit);
        Assert.Equal(74999, snapshot.DailyRemaining);
        Assert.Equal(450, snapshot.MinuteLimit);
        Assert.Equal(449, snapshot.MinuteRemaining);
    }

    [Fact]
    public async Task SendAsync_TooManyRequestsThenSuccess_RetriesAndSucceeds()
    {
        var tooManyRequests = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        tooManyRequests.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
        var success = new HttpResponseMessage(HttpStatusCode.OK);
        var inner = FakeHttpMessageHandler.Sequence(tooManyRequests, success);
        var handler = CreateHandler(maxRetries: 3);
        handler.InnerHandler = inner;
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://v3.football.api-sports.io") };

        var response = await client.GetAsync("countries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
    }

    [Fact]
    public async Task SendAsync_TransientServerError_RetriesUpToMaxThenReturnsFailure()
    {
        var failing = Enumerable.Range(0, 10)
            .Select(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero) },
            })
            .ToArray();
        var inner = FakeHttpMessageHandler.Sequence(failing);
        var handler = CreateHandler(maxRetries: 2);
        handler.InnerHandler = inner;
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://v3.football.api-sports.io") };

        var response = await client.GetAsync("countries");

        // Never retries indefinitely: exactly maxRetries + 1 attempts, then gives up.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, inner.Requests.Count);
    }

    [Fact]
    public async Task SendAsync_NonRetryableError_DoesNotRetry()
    {
        var badRequest = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var inner = FakeHttpMessageHandler.Sequence(badRequest);
        var handler = CreateHandler(maxRetries: 3);
        handler.InnerHandler = inner;
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://v3.football.api-sports.io") };

        var response = await client.GetAsync("countries");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(inner.Requests);
    }
}

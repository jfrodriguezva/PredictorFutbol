using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

/// <summary>
/// Reads API-Football's quota headers off every response, persists a snapshot, and
/// retries HTTP 429 / transient 5xx responses with a bounded exponential backoff.
/// Never retries indefinitely.
/// </summary>
public sealed class ApiFootballRateLimitHandler : DelegatingHandler
{
    private const string DailyLimitHeader = "x-ratelimit-requests-limit";
    private const string DailyRemainingHeader = "x-ratelimit-requests-remaining";
    private const string MinuteLimitHeader = "X-RateLimit-Limit";
    private const string MinuteRemainingHeader = "X-RateLimit-Remaining";
    private const string Provider = "API-Football";

    private static readonly HttpStatusCode[] TransientServerErrors =
    [
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ApiFootballOptions> _options;
    private readonly ILogger<ApiFootballRateLimitHandler> _logger;

    public ApiFootballRateLimitHandler(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ApiFootballOptions> options,
        ILogger<ApiFootballRateLimitHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var maxRetries = Math.Max(0, _options.CurrentValue.MaxRetries);
        var attempt = 0;

        while (true)
        {
            var requestToSend = attempt == 0 ? request : await CloneRequestAsync(request, cancellationToken);
            var response = await base.SendAsync(requestToSend, cancellationToken);

            var canRetry = attempt < maxRetries
                && (response.StatusCode == HttpStatusCode.TooManyRequests || TransientServerErrors.Contains(response.StatusCode));

            if (!canRetry)
            {
                await RecordQuotaSnapshotAsync(response, cancellationToken);
                return response;
            }

            var delay = ComputeRetryDelay(response, attempt);
            _logger.LogWarning(
                "API-Football request to {Uri} returned {StatusCode}. Retrying in {DelaySeconds}s (attempt {Attempt}/{MaxRetries}).",
                request.RequestUri, (int)response.StatusCode, delay.TotalSeconds, attempt + 1, maxRetries);

            response.Dispose();
            await Task.Delay(delay, cancellationToken);
            attempt++;
        }
    }

    private static TimeSpan ComputeRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var untilDate = date - DateTimeOffset.UtcNow;
            if (untilDate > TimeSpan.Zero)
            {
                return untilDate;
            }
        }

        // Bounded exponential backoff: 1s, 2s, 4s, ... capped at 30s.
        var seconds = Math.Min(30, Math.Pow(2, attempt));
        return TimeSpan.FromSeconds(seconds);
    }

    private async Task RecordQuotaSnapshotAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var dailyLimit = ReadIntHeader(response, DailyLimitHeader);
        var dailyRemaining = ReadIntHeader(response, DailyRemainingHeader);
        var minuteLimit = ReadIntHeader(response, MinuteLimitHeader);
        var minuteRemaining = ReadIntHeader(response, MinuteRemainingHeader);

        if (dailyLimit is null && dailyRemaining is null && minuteLimit is null && minuteRemaining is null)
        {
            // Provider did not return any quota headers for this response; nothing to record.
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SportsPredictorDbContext>();

            dbContext.ApiQuotaSnapshots.Add(new ApiQuotaSnapshot
            {
                Provider = Provider,
                CapturedAt = DateTime.UtcNow,
                DailyLimit = dailyLimit,
                DailyRemaining = dailyRemaining,
                MinuteLimit = minuteLimit,
                MinuteRemaining = minuteRemaining,
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Recording quota telemetry must never fail the actual API call.
            _logger.LogError(ex, "Failed to persist API-Football quota snapshot.");
        }
    }

    private static int? ReadIntHeader(HttpResponseMessage response, string headerName)
    {
        if (response.Headers.TryGetValues(headerName, out var values)
            && int.TryParse(values.FirstOrDefault(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
        };

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (original.Content is not null)
        {
            var buffer = await original.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(buffer);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}

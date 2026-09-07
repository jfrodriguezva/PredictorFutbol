using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Common.Interfaces;
using SportsPredictor.Application.ExternalData;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Mappings;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

/// <summary>
/// Application-facing entry point for API-Football reference data: adds caching and
/// DTO mapping on top of the raw <see cref="ApiFootballClient"/>, and reports
/// connection/quota status. Scope limited to /countries and /leagues (Phase 3).
/// </summary>
public sealed class ApiFootballSyncService : IApiFootballClient
{
    private const string CountriesCacheKey = "api-football:countries";
    private const string LeaguesCacheKey = "api-football:leagues";
    private static readonly TimeSpan ReferenceDataTtl = TimeSpan.FromHours(24);

    private readonly ApiFootballClient _client;
    private readonly ICacheService _cache;
    private readonly SportsPredictorDbContext _dbContext;
    private readonly IOptionsMonitor<ApiFootballOptions> _options;

    public ApiFootballSyncService(
        ApiFootballClient client,
        ICacheService cache,
        SportsPredictorDbContext dbContext,
        IOptionsMonitor<ApiFootballOptions> options)
    {
        _client = client;
        _cache = cache;
        _dbContext = dbContext;
        _options = options;
    }

    public Task<IReadOnlyList<CountryDto>> GetCountriesAsync(CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync(
            CountriesCacheKey,
            async ct =>
            {
                var envelope = await _client.GetCountriesAsync(ct);
                return (IReadOnlyList<CountryDto>)envelope.Response.Select(c => c.ToDto()).ToList();
            },
            ReferenceDataTtl,
            cancellationToken);

    public Task<IReadOnlyList<LeagueDto>> GetLeaguesAsync(CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync(
            LeaguesCacheKey,
            async ct =>
            {
                var envelope = await _client.GetLeaguesAsync(ct);
                return (IReadOnlyList<LeagueDto>)envelope.Response.Select(l => l.ToDto()).ToList();
            },
            ReferenceDataTtl,
            cancellationToken);

    public async Task<ApiFootballStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var connected = !string.IsNullOrWhiteSpace(_options.CurrentValue.ApiKey);

        var latest = await _dbContext.ApiQuotaSnapshots
            .Where(s => s.Provider == "API-Football")
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new ApiFootballStatusDto(
            Connected: connected,
            DailyLimit: latest?.DailyLimit,
            DailyRemaining: latest?.DailyRemaining,
            MinuteLimit: latest?.MinuteLimit,
            MinuteRemaining: latest?.MinuteRemaining,
            LastCapturedAtUtc: latest?.CapturedAt);
    }

    public async Task<ApiFootballConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Bypasses the cache: a connection test must hit the network to be meaningful.
            var envelope = await _client.GetCountriesAsync(cancellationToken);
            return new ApiFootballConnectionTestResult(Success: true, ErrorMessage: null, CountriesReturned: envelope.Response.Count);
        }
        catch (ApiFootballNotConfiguredException)
        {
            throw;
        }
        catch (ApiFootballException ex)
        {
            return new ApiFootballConnectionTestResult(Success: false, ErrorMessage: ex.Message, CountriesReturned: null);
        }
    }
}

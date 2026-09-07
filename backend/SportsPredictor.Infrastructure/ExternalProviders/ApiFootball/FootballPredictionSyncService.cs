using System.Globalization;
using System.Text.Json;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ReferenceData;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

/// <summary>
/// Captures API-Football's own prediction for a match (CLAUDE.md section 23) as a
/// snapshot, purely as an external benchmark to compare our model against later —
/// never treated as ground truth. Operates per-match; every call appends a new
/// snapshot rather than overwriting the previous one.
/// </summary>
public sealed class FootballPredictionSyncService : IFootballPredictionSyncService
{
    private readonly ApiFootballClient _client;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballPredictionSyncService(ApiFootballClient client, SportsPredictorDbContext dbContext)
    {
        _client = client;
        _dbContext = dbContext;
    }

    public async Task<PredictionSyncResultDto> SyncPredictionAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await _dbContext.Matches.FindAsync([matchId], cancellationToken)
            ?? throw new NotFoundException(nameof(Match), matchId);

        var capturedAt = DateTime.UtcNow;

        if (match.ExternalApiFootballId is not int externalFixtureId)
        {
            return new PredictionSyncResultDto(false, null, null, null, capturedAt);
        }

        var envelope = await _client.GetPredictionsAsync(externalFixtureId, cancellationToken);
        var response = envelope.Response.FirstOrDefault();
        if (response is null)
        {
            return new PredictionSyncResultDto(false, null, null, null, capturedAt);
        }

        var home = ParsePercent(response.Predictions.Percent.Home);
        var draw = ParsePercent(response.Predictions.Percent.Draw);
        var away = ParsePercent(response.Predictions.Percent.Away);

        if (home is null || draw is null || away is null)
        {
            // The three required probability fields (CLAUDE.md's entity spec) must all
            // be present and parseable; otherwise there is nothing reliable to save.
            return new PredictionSyncResultDto(false, home, draw, away, capturedAt);
        }

        _dbContext.ApiFootballPredictionSnapshots.Add(new ApiFootballPredictionSnapshot
        {
            MatchId = match.Id,
            CapturedAt = capturedAt,
            HomeProbability = home.Value,
            DrawProbability = draw.Value,
            AwayProbability = away.Value,
            PredictedWinner = response.Predictions.Winner?.Name,
            PredictedScore = response.Predictions.Advice,
            RawJson = JsonSerializer.Serialize(response),
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PredictionSyncResultDto(true, home, draw, away, capturedAt);
    }

    /// <summary>API-Football reports percentages as strings like "45%"; parses to 0.45.</summary>
    private static double? ParsePercent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim().TrimEnd('%');
        return double.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value / 100.0
            : null;
    }
}

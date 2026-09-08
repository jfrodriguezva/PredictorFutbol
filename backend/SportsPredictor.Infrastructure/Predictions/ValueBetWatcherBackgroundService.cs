using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Predictions;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.Predictions;

/// <summary>
/// A first, deliberately minimal scheduler (CLAUDE.md section 14 left the full
/// orchestration — "MegaSyncJob" — out of scope; this is not that). Every tick, for
/// every enabled TrackedCompetition, generates a prediction for any upcoming
/// (Scheduled, within 7 days) match that doesn't have one yet, and turns any
/// resulting Value/StrongValue selection into a ValueBetNotification row.
///
/// Does NOT sync fixtures/odds from API-FOOTBALL itself — it only reacts to matches
/// already present in SQLite. It also only reacts to predictions it generates itself:
/// a match a human already generated a prediction for via the UI is skipped here (the
/// "does this match have a prediction yet" check doesn't distinguish who created it),
/// so it won't retroactively surface a notification for that one.
/// </summary>
public sealed class ValueBetWatcherBackgroundService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LookaheadWindow = TimeSpan.FromDays(7);
    private static readonly string[] NotifiableValueCategories = ["Value", "StrongValue"];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ValueBetWatcherBackgroundService> _logger;

    public ValueBetWatcherBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ValueBetWatcherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "ValueBetWatcherBackgroundService tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>The actual detection pass, exposed publicly (rather than kept private)
    /// so it can be exercised directly in tests without waiting on the tick timer.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SportsPredictorDbContext>();
        var predictionService = scope.ServiceProvider.GetRequiredService<IPredictionGenerationService>();

        var trackedCompetitions = await dbContext.TrackedCompetitions
            .Where(t => t.Enabled)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var horizon = now.Add(LookaheadWindow);
        var notificationsAdded = 0;

        foreach (var tracked in trackedCompetitions)
        {
            var season = await dbContext.Seasons.FirstOrDefaultAsync(
                s => s.CompetitionId == tracked.CompetitionId && s.StartDate.Year == tracked.Season, cancellationToken);
            if (season is null)
            {
                continue;
            }

            var upcomingMatches = await dbContext.Matches
                .Where(m => m.CompetitionId == tracked.CompetitionId && m.SeasonId == season.Id
                    && m.Status == MatchStatus.Scheduled && m.MatchDate >= now && m.MatchDate <= horizon)
                .ToListAsync(cancellationToken);

            foreach (var match in upcomingMatches)
            {
                var alreadyHasPrediction = await dbContext.Predictions.AnyAsync(p => p.MatchId == match.Id, cancellationToken);
                if (alreadyHasPrediction)
                {
                    continue;
                }

                GeneratePredictionResultDto result;
                try
                {
                    result = await predictionService.GeneratePrediction1X2Async(match.Id, modelVersionId: null, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // No trained model exists yet for this model name — nothing to do
                    // until one is trained; skip this competition, not just this match.
                    break;
                }
                catch (MlServiceException ex)
                {
                    _logger.LogWarning(ex, "ValueBetWatcherBackgroundService: prediction generation failed for match {MatchId}", match.Id);
                    continue;
                }

                foreach (var selection in result.Selections.Where(s => s.ValueCategory is not null && NotifiableValueCategories.Contains(s.ValueCategory)))
                {
                    dbContext.ValueBetNotifications.Add(new ValueBetNotification
                    {
                        MatchId = match.Id,
                        PredictionId = selection.Id,
                        Selection = selection.Selection,
                        ExpectedValue = selection.ExpectedValue!.Value,
                        DetectedAt = now,
                    });
                    notificationsAdded++;
                }
            }
        }

        if (notificationsAdded > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("ValueBetWatcherBackgroundService: added {Count} new value-bet notifications", notificationsAdded);
        }
    }
}

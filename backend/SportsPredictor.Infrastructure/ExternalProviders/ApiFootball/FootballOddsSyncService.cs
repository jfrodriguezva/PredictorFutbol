using System.Globalization;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ReferenceData;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

/// <summary>
/// Ingests bookmaker odds from API-Football (CLAUDE.md section 24). Operates
/// per-match (the provider's /odds is per-fixture). Every call appends fresh
/// OddsSnapshot rows — never overwrites, so opening/intermediate/closing lines can be
/// reconstructed later from the history of CapturedAt values.
/// </summary>
public sealed class FootballOddsSyncService : IFootballOddsSyncService
{
    private readonly ApiFootballClient _client;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballOddsSyncService(ApiFootballClient client, SportsPredictorDbContext dbContext)
    {
        _client = client;
        _dbContext = dbContext;
    }

    public async Task<OddsSyncResultDto> SyncOddsAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await _dbContext.Matches.FindAsync([matchId], cancellationToken)
            ?? throw new NotFoundException(nameof(Match), matchId);

        if (match.ExternalApiFootballId is not int externalFixtureId)
        {
            return new OddsSyncResultDto(0, DateTime.UtcNow);
        }

        var envelope = await _client.GetOddsAsync(externalFixtureId, cancellationToken);

        // For a match still in the future, "now" genuinely is the pre-kickoff capture
        // time. For a match already played, API-FOOTBALL's /odds only ever returns the
        // frozen pre-match line (never live/in-play odds), but stamping it with "now"
        // would date it AFTER kickoff — DatasetBuilderService's anti-leakage filter
        // (CapturedAt <= match.MatchDate) would then silently discard it forever, no
        // matter how real the data is. Backdating to the match's own kickoff time is
        // the latest timestamp that's still honestly "no later than kickoff".
        var capturedAt = DateTime.UtcNow > match.MatchDate ? match.MatchDate : DateTime.UtcNow;
        var snapshotsCreated = 0;

        foreach (var response in envelope.Response)
        {
            foreach (var bookmaker in response.Bookmakers)
            {
                foreach (var bet in bookmaker.Bets)
                {
                    foreach (var value in bet.Values)
                    {
                        if (!decimal.TryParse(value.Odd, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalOdds)
                            || decimalOdds <= 0)
                        {
                            // Can't do anything meaningful with an odd we can't parse
                            // or that isn't a valid multiplier; skip rather than guess.
                            continue;
                        }

                        _dbContext.OddsSnapshots.Add(new OddsSnapshot
                        {
                            MatchId = match.Id,
                            Sportsbook = bookmaker.Name,
                            Market = bet.Name,
                            Selection = value.Value,
                            // API-Football only reports decimal odds; AmericanOdds
                            // stays null here (converting formats is Phase 12's job).
                            DecimalOdds = decimalOdds,
                            // Raw implied probability (1/odds) — removing the vig
                            // across a mutually-exclusive market is Phase 12's job too.
                            ImpliedProbability = (double)(1m / decimalOdds),
                            CapturedAt = capturedAt,
                        });
                        snapshotsCreated++;
                    }
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OddsSyncResultDto(snapshotsCreated, capturedAt);
    }
}

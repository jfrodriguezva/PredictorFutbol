using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ReferenceData;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

/// <summary>
/// Corresponds to CLAUDE.md's "FootballLineupsSyncJob". Operates on a single Match
/// (API-Football's /fixtures/lineups is per-fixture, not per-league). Every call
/// appends fresh LineupSnapshot rows — never updates previous ones (CLAUDE.md
/// section 11 — lineups are most useful checked close to kickoff, and history matters).
/// </summary>
public sealed class FootballLineupSyncService : IFootballLineupSyncService
{
    private readonly ApiFootballClient _client;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballLineupSyncService(ApiFootballClient client, SportsPredictorDbContext dbContext)
    {
        _client = client;
        _dbContext = dbContext;
    }

    public async Task<LineupSyncResultDto> SyncLineupsAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await _dbContext.Matches.FindAsync([matchId], cancellationToken)
            ?? throw new NotFoundException(nameof(Match), matchId);

        if (match.ExternalApiFootballId is not int externalFixtureId)
        {
            // A locally-created match with no provider id (shouldn't normally happen
            // for anything synced via sync-fixtures) has nothing to look up.
            return new LineupSyncResultDto(0, DateTime.UtcNow);
        }

        var envelope = await _client.GetLineupsAsync(externalFixtureId, cancellationToken);
        var capturedAt = DateTime.UtcNow;
        var snapshotsCreated = 0;

        foreach (var entry in envelope.Response)
        {
            if (string.IsNullOrWhiteSpace(entry.Formation))
            {
                // Formation is the one field this entity requires; skip rather than invent it.
                continue;
            }

            var team = _dbContext.Teams.Local.FirstOrDefault(t => t.ExternalApiFootballId == entry.Team.Id)
                ?? await _dbContext.Teams.FirstOrDefaultAsync(t => t.ExternalApiFootballId == entry.Team.Id, cancellationToken);
            if (team is null)
            {
                // Both teams in a match we already synced should exist; if not, skip
                // this side rather than guess which team it was.
                continue;
            }

            _dbContext.LineupSnapshots.Add(new LineupSnapshot
            {
                MatchId = match.Id,
                TeamId = team.Id,
                Formation = entry.Formation,
                ExternalCoachId = entry.Coach?.Id,
                CoachName = entry.Coach?.Name,
                CapturedAt = capturedAt,
            });
            snapshotsCreated++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LineupSyncResultDto(snapshotsCreated, capturedAt);
    }
}

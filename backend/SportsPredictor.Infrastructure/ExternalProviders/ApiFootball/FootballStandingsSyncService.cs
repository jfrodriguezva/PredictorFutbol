using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ReferenceData;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

/// <summary>
/// Corresponds to CLAUDE.md's "FootballStandingsSyncJob". Runs on-demand for a single
/// tracked competition. Every call appends fresh StandingSnapshot rows — it never
/// updates or deletes previous ones, by design (CLAUDE.md section 10).
/// </summary>
public sealed class FootballStandingsSyncService : IFootballStandingsSyncService
{
    private readonly ApiFootballClient _client;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballStandingsSyncService(ApiFootballClient client, SportsPredictorDbContext dbContext)
    {
        _client = client;
        _dbContext = dbContext;
    }

    public async Task<StandingsSyncResultDto> SyncStandingsAsync(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        var tracked = await _dbContext.TrackedCompetitions.FindAsync([trackedCompetitionId], cancellationToken)
            ?? throw new NotFoundException(nameof(TrackedCompetition), trackedCompetitionId);

        var season = await _dbContext.Seasons.FirstOrDefaultAsync(
            s => s.CompetitionId == tracked.CompetitionId && s.StartDate.Year == tracked.Season,
            cancellationToken);

        var envelope = await _client.GetStandingsAsync(tracked.ExternalLeagueId, tracked.Season, cancellationToken);
        var capturedAt = DateTime.UtcNow;

        var snapshotsCreated = 0;
        var skippedUnknownTeam = 0;

        // "standings" is an array of groups (usually one, for a plain league table);
        // flatten them all into the same snapshot batch.
        var rows = envelope.Response.SelectMany(r => r.League.Standings).SelectMany(group => group);

        foreach (var row in rows)
        {
            if (season is null)
            {
                skippedUnknownTeam++; // no local season to attach this snapshot to either
                continue;
            }

            var team = _dbContext.Teams.Local.FirstOrDefault(t => t.ExternalApiFootballId == row.Team.Id)
                ?? await _dbContext.Teams.FirstOrDefaultAsync(t => t.ExternalApiFootballId == row.Team.Id, cancellationToken);

            if (team is null)
            {
                // A standings row for a team we don't know yet (sync-teams was never
                // run for it) — skip rather than create a bare stub for a table row.
                skippedUnknownTeam++;
                continue;
            }

            _dbContext.StandingSnapshots.Add(new StandingSnapshot
            {
                CompetitionId = tracked.CompetitionId,
                SeasonId = season.Id,
                TeamId = team.Id,
                CapturedAt = capturedAt,
                Rank = row.Rank,
                Points = row.Points,
                Played = row.All.Played,
                Wins = row.All.Win,
                Draws = row.All.Draw,
                Losses = row.All.Lose,
                GoalsFor = row.All.Goals.For,
                GoalsAgainst = row.All.Goals.Against,
                GoalDifference = row.GoalsDiff,
                Form = row.Form,
            });
            snapshotsCreated++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new StandingsSyncResultDto(snapshotsCreated, skippedUnknownTeam, capturedAt);
    }
}

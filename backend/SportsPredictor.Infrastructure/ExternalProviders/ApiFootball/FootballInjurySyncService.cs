using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ReferenceData;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;
using SportsPredictor.Infrastructure.Persistence;
using SportsPredictor.Infrastructure.Persistence.Configurations;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

/// <summary>
/// Corresponds to CLAUDE.md's "FootballInjuriesSyncJob". Every call appends fresh
/// InjurySnapshot rows — never updates or deletes previous ones (CLAUDE.md section 11).
/// </summary>
public sealed class FootballInjurySyncService : IFootballInjurySyncService
{
    private const string Source = "API-Football";

    private readonly ApiFootballClient _client;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballInjurySyncService(ApiFootballClient client, SportsPredictorDbContext dbContext)
    {
        _client = client;
        _dbContext = dbContext;
    }

    public async Task<InjurySyncResultDto> SyncInjuriesAsync(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        var tracked = await _dbContext.TrackedCompetitions.FindAsync([trackedCompetitionId], cancellationToken)
            ?? throw new NotFoundException(nameof(TrackedCompetition), trackedCompetitionId);

        var envelope = await _client.GetInjuriesAsync(tracked.ExternalLeagueId, tracked.Season, cancellationToken);
        var capturedAt = DateTime.UtcNow;

        var snapshotsCreated = 0;
        var teamsCreated = 0;
        var playersCreated = 0;

        foreach (var entry in envelope.Response)
        {
            var (team, teamWasCreated) = await GetOrCreateMinimalTeamAsync(entry.Team, cancellationToken);
            if (teamWasCreated)
            {
                teamsCreated++;
            }

            var (player, playerWasCreated) = await GetOrCreateMinimalPlayerAsync(entry.Player, team.Id, cancellationToken);
            if (playerWasCreated)
            {
                playersCreated++;
            }

            Guid? matchId = null;
            if (entry.Fixture?.Id is int externalFixtureId)
            {
                var match = _dbContext.Matches.Local.FirstOrDefault(m => m.ExternalApiFootballId == externalFixtureId)
                    ?? await _dbContext.Matches.FirstOrDefaultAsync(m => m.ExternalApiFootballId == externalFixtureId, cancellationToken);
                matchId = match?.Id;
            }

            _dbContext.InjurySnapshots.Add(new InjurySnapshot
            {
                PlayerId = player.Id,
                TeamId = team.Id,
                MatchId = matchId,
                Type = entry.Player.Type,
                Reason = entry.Player.Reason,
                CapturedAt = capturedAt,
                Source = Source,
            });
            snapshotsCreated++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InjurySyncResultDto(snapshotsCreated, teamsCreated, playersCreated, capturedAt);
    }

    private async Task<(Team team, bool wasCreated)> GetOrCreateMinimalTeamAsync(ApiFootballFixtureTeamModel model, CancellationToken cancellationToken)
    {
        var team = _dbContext.Teams.Local.FirstOrDefault(t => t.ExternalApiFootballId == model.Id)
            ?? await _dbContext.Teams.FirstOrDefaultAsync(t => t.ExternalApiFootballId == model.Id, cancellationToken);
        if (team is not null)
        {
            return (team, false);
        }

        team = new Team
        {
            SportId = SportConfiguration.FootballId,
            ExternalApiFootballId = model.Id,
            Name = model.Name,
        };
        _dbContext.Teams.Add(team);
        return (team, true);
    }

    private async Task<(Player player, bool wasCreated)> GetOrCreateMinimalPlayerAsync(ApiFootballInjuryPlayerModel model, Guid teamId, CancellationToken cancellationToken)
    {
        var player = _dbContext.Players.Local.FirstOrDefault(p => p.ExternalApiFootballId == model.Id)
            ?? await _dbContext.Players.FirstOrDefaultAsync(p => p.ExternalApiFootballId == model.Id, cancellationToken);
        if (player is not null)
        {
            return (player, false);
        }

        // /players (Priority 3) is not implemented yet — this is a minimal stand-in
        // populated from whatever the /injuries entry itself provides (id + name).
        player = new Player
        {
            TeamId = teamId,
            ExternalApiFootballId = model.Id,
            Name = model.Name,
        };
        _dbContext.Players.Add(player);
        return (player, true);
    }
}

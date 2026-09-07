using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ReferenceData;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;
using SportsPredictor.Infrastructure.Persistence;
using SportsPredictor.Infrastructure.Persistence.Configurations;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

/// <summary>
/// Corresponds to CLAUDE.md's "FootballReferenceSyncJob": manages TrackedCompetition
/// rows and syncs their reference data (teams, venues) from API-Football. Runs
/// on-demand (HTTP-triggered) for now; full job start/stop/resume/progress tracking
/// is deferred to the phase that introduces long-running historical imports.
/// </summary>
public sealed class FootballReferenceSyncService : IFootballReferenceService
{
    private readonly ApiFootballClient _client;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballReferenceSyncService(ApiFootballClient client, SportsPredictorDbContext dbContext)
    {
        _client = client;
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TrackedCompetitionDto>> GetTrackedCompetitionsAsync(CancellationToken cancellationToken)
    {
        var items = await _dbContext.TrackedCompetitions
            .Include(t => t.Competition)
            .OrderByDescending(t => t.Priority)
            .ToListAsync(cancellationToken);

        return items.Select(t => ToDto(t)).ToList();
    }

    public async Task<TrackedCompetitionDto> CreateTrackedCompetitionAsync(CreateTrackedCompetitionRequest request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.TrackedCompetitions
            .Include(t => t.Competition)
            .FirstOrDefaultAsync(t => t.ExternalLeagueId == request.ExternalLeagueId && t.Season == request.Season, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var envelope = await _client.GetLeagueByIdAsync(request.ExternalLeagueId, cancellationToken);
        var leagueEntry = envelope.Response.FirstOrDefault()
            ?? throw new ApiFootballException($"API-Football has no league with id {request.ExternalLeagueId}.");

        var competition = await GetOrCreateCompetitionAsync(leagueEntry, cancellationToken);
        UpsertSeasons(competition, leagueEntry.Seasons);

        var trackedCompetition = new TrackedCompetition
        {
            CompetitionId = competition.Id,
            ExternalLeagueId = request.ExternalLeagueId,
            Season = request.Season,
            Priority = request.Priority,
            HistoricalSeasonsToImport = request.HistoricalSeasonsToImport,
            SyncFixtures = request.SyncFixtures,
            SyncStandings = request.SyncStandings,
            SyncPlayers = request.SyncPlayers,
            SyncStatistics = request.SyncStatistics,
            SyncInjuries = request.SyncInjuries,
            SyncOdds = request.SyncOdds,
            SyncPredictions = request.SyncPredictions,
        };
        _dbContext.TrackedCompetitions.Add(trackedCompetition);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(trackedCompetition, competition);
    }

    public async Task<TrackedCompetitionDto> SetEnabledAsync(Guid trackedCompetitionId, bool enabled, CancellationToken cancellationToken)
    {
        var tracked = await _dbContext.TrackedCompetitions
            .Include(t => t.Competition)
            .FirstOrDefaultAsync(t => t.Id == trackedCompetitionId, cancellationToken)
            ?? throw new NotFoundException(nameof(TrackedCompetition), trackedCompetitionId);

        tracked.Enabled = enabled;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(tracked);
    }

    public async Task<TeamSyncResultDto> SyncTeamsAsync(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        var tracked = await _dbContext.TrackedCompetitions.FindAsync([trackedCompetitionId], cancellationToken)
            ?? throw new NotFoundException(nameof(TrackedCompetition), trackedCompetitionId);

        var envelope = await _client.GetTeamsAsync(tracked.ExternalLeagueId, tracked.Season, cancellationToken);

        var teamsUpserted = 0;
        var venuesUpserted = 0;

        foreach (var entry in envelope.Response)
        {
            await UpsertTeamAsync(entry.Team, cancellationToken);
            teamsUpserted++;

            if (await UpsertVenueAsync(entry.Venue, cancellationToken))
            {
                venuesUpserted++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TeamSyncResultDto(teamsUpserted, venuesUpserted);
    }

    private async Task<Competition> GetOrCreateCompetitionAsync(ApiFootballLeagueEntryModel entry, CancellationToken cancellationToken)
    {
        var competition = await _dbContext.Competitions
            .FirstOrDefaultAsync(c => c.ExternalApiFootballId == entry.League.Id, cancellationToken);

        if (competition is null)
        {
            competition = new Competition
            {
                SportId = SportConfiguration.FootballId,
                ExternalApiFootballId = entry.League.Id,
                Name = entry.League.Name,
                Country = entry.Country?.Name,
                CompetitionType = MapCompetitionType(entry.League.Type),
            };
            _dbContext.Competitions.Add(competition);
        }
        else
        {
            competition.Name = entry.League.Name;
            competition.Country = entry.Country?.Name;
            competition.CompetitionType = MapCompetitionType(entry.League.Type);
        }

        return competition;
    }

    private void UpsertSeasons(Competition competition, List<ApiFootballSeasonModel> seasons)
    {
        foreach (var seasonModel in seasons)
        {
            if (seasonModel.Start is null || seasonModel.End is null)
            {
                // Season entity requires both dates; skip rather than invent them.
                continue;
            }

            var seasonName = seasonModel.Start.Value.Year == seasonModel.End.Value.Year
                ? seasonModel.Start.Value.Year.ToString()
                : $"{seasonModel.Start.Value.Year}/{seasonModel.End.Value.Year}";

            var existing = _dbContext.Seasons.Local
                .FirstOrDefault(s => s.CompetitionId == competition.Id && s.Name == seasonName)
                ?? _dbContext.Seasons.FirstOrDefault(s => s.CompetitionId == competition.Id && s.Name == seasonName);

            if (existing is null)
            {
                _dbContext.Seasons.Add(new Season
                {
                    CompetitionId = competition.Id,
                    Name = seasonName,
                    StartDate = seasonModel.Start.Value,
                    EndDate = seasonModel.End.Value,
                });
            }
            else
            {
                existing.StartDate = seasonModel.Start.Value;
                existing.EndDate = seasonModel.End.Value;
            }
        }
    }

    private async Task UpsertTeamAsync(ApiFootballTeamInfoModel model, CancellationToken cancellationToken)
    {
        // Check tracked-but-not-yet-saved entities first: two entries in the same
        // /teams response batch can reference the same team before SaveChanges runs
        // (mirrors the bug found and fixed in FootballFixtureSyncService, Phase 5).
        var team = _dbContext.Teams.Local.FirstOrDefault(t => t.ExternalApiFootballId == model.Id)
            ?? await _dbContext.Teams.FirstOrDefaultAsync(t => t.ExternalApiFootballId == model.Id, cancellationToken);
        if (team is null)
        {
            _dbContext.Teams.Add(new Team
            {
                SportId = SportConfiguration.FootballId,
                ExternalApiFootballId = model.Id,
                Name = model.Name,
                Country = model.Country,
            });
        }
        else
        {
            team.Name = model.Name;
            team.Country = model.Country;
        }
    }

    private async Task<bool> UpsertVenueAsync(ApiFootballVenueModel? model, CancellationToken cancellationToken)
    {
        if (model?.Id is not int externalVenueId || string.IsNullOrWhiteSpace(model.Name))
        {
            // Without an id or a name there is nothing reliable to persist — skip rather than invent placeholder data.
            return false;
        }

        var venue = _dbContext.Venues.Local.FirstOrDefault(v => v.ExternalApiFootballId == externalVenueId)
            ?? await _dbContext.Venues.FirstOrDefaultAsync(v => v.ExternalApiFootballId == externalVenueId, cancellationToken);
        if (venue is null)
        {
            _dbContext.Venues.Add(new Venue
            {
                ExternalApiFootballId = externalVenueId,
                Name = model.Name,
                City = model.City,
            });
        }
        else
        {
            venue.Name = model.Name;
            venue.City = model.City;
        }

        return true;
    }

    private static CompetitionType MapCompetitionType(string? apiFootballType) => apiFootballType?.Trim().ToLowerInvariant() switch
    {
        "cup" => CompetitionType.Cup,
        "league" => CompetitionType.League,
        _ => CompetitionType.League,
    };

    private static TrackedCompetitionDto ToDto(TrackedCompetition tracked, Competition? competition = null) => new(
        tracked.Id,
        tracked.CompetitionId,
        (competition ?? tracked.Competition)?.Name ?? string.Empty,
        tracked.ExternalLeagueId,
        tracked.Season,
        tracked.Enabled,
        tracked.Priority,
        tracked.HistoricalSeasonsToImport,
        tracked.SyncFixtures,
        tracked.SyncStandings,
        tracked.SyncPlayers,
        tracked.SyncStatistics,
        tracked.SyncInjuries,
        tracked.SyncOdds,
        tracked.SyncPredictions);
}

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
/// Corresponds to CLAUDE.md's "FootballFixturesSyncJob" (and, for the narrowed Phase 5
/// scope, doubles as the results sync since a finished fixture's score comes back in
/// the same /fixtures response). <see cref="SyncFixturesAsync"/> is the full-season
/// historical import (Phase 5); <see cref="RefreshRecentAndUpcomingFixturesAsync"/> is
/// the incremental refresh (Phase 6, CLAUDE.md section 17) that only touches fixtures
/// near "now" instead of re-fetching the whole season every time.
/// </summary>
public sealed class FootballFixtureSyncService : IFootballFixtureSyncService
{
    private readonly ApiFootballClient _client;
    private readonly SportsPredictorDbContext _dbContext;

    public FootballFixtureSyncService(ApiFootballClient client, SportsPredictorDbContext dbContext)
    {
        _client = client;
        _dbContext = dbContext;
    }

    public async Task<FixtureSyncResultDto> SyncFixturesAsync(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        var tracked = await GetTrackedCompetitionAsync(trackedCompetitionId, cancellationToken);
        var envelope = await _client.GetFixturesAsync(tracked.ExternalLeagueId, tracked.Season, cancellationToken);
        return await ProcessAndSaveAsync(tracked, envelope.Response, cancellationToken);
    }

    public async Task<FixtureSyncResultDto> RefreshRecentAndUpcomingFixturesAsync(
        Guid trackedCompetitionId,
        int upcomingCount,
        int recentCount,
        CancellationToken cancellationToken)
    {
        var tracked = await GetTrackedCompetitionAsync(trackedCompetitionId, cancellationToken);

        var upcoming = upcomingCount > 0
            ? await _client.GetUpcomingFixturesAsync(tracked.ExternalLeagueId, upcomingCount, cancellationToken)
            : null;
        var recent = recentCount > 0
            ? await _client.GetRecentFixturesAsync(tracked.ExternalLeagueId, recentCount, cancellationToken)
            : null;

        // "next"/"last" fixtures for the same league almost always fall in the tracked
        // season; a fixture right at a season boundary that doesn't match is skipped
        // by ProcessAndSaveAsync rather than guessed at (counted in SkippedNoSeasonMatch).
        var entries = (upcoming?.Response ?? []).Concat(recent?.Response ?? []);

        return await ProcessAndSaveAsync(tracked, entries, cancellationToken);
    }

    private async Task<TrackedCompetition> GetTrackedCompetitionAsync(Guid trackedCompetitionId, CancellationToken cancellationToken) =>
        await _dbContext.TrackedCompetitions.FindAsync([trackedCompetitionId], cancellationToken)
            ?? throw new NotFoundException(nameof(TrackedCompetition), trackedCompetitionId);

    private async Task<FixtureSyncResultDto> ProcessAndSaveAsync(
        TrackedCompetition tracked,
        IEnumerable<ApiFootballFixtureEntryModel> entries,
        CancellationToken cancellationToken)
    {
        // The season row for this tracked competition was upserted when it was created
        // (Phase 4), keyed by the same calendar start year API-Football reports.
        var season = await _dbContext.Seasons.FirstOrDefaultAsync(
            s => s.CompetitionId == tracked.CompetitionId && s.StartDate.Year == tracked.Season,
            cancellationToken);

        var fixturesUpserted = 0;
        var teamsCreated = 0;
        var venuesCreated = 0;
        var skippedNoSeasonMatch = 0;

        foreach (var entry in entries)
        {
            if (season is null)
            {
                // Without a matching local Season we cannot satisfy Match.SeasonId
                // (required); skip rather than guess or leave it empty.
                skippedNoSeasonMatch++;
                continue;
            }

            var homeTeam = await GetOrCreateMinimalTeamAsync(entry.Teams.Home, cancellationToken);
            if (homeTeam.wasCreated)
            {
                teamsCreated++;
            }

            var awayTeam = await GetOrCreateMinimalTeamAsync(entry.Teams.Away, cancellationToken);
            if (awayTeam.wasCreated)
            {
                teamsCreated++;
            }

            Guid? venueId = null;
            if (entry.Fixture.Venue?.Id is int externalVenueId && !string.IsNullOrWhiteSpace(entry.Fixture.Venue.Name))
            {
                var (venue, wasCreated) = await GetOrCreateVenueAsync(externalVenueId, entry.Fixture.Venue.Name!, entry.Fixture.Venue.City, cancellationToken);
                venueId = venue.Id;
                if (wasCreated)
                {
                    venuesCreated++;
                }
            }

            await UpsertMatchAsync(entry, tracked.CompetitionId, season.Id, homeTeam.team.Id, awayTeam.team.Id, venueId, cancellationToken);
            fixturesUpserted++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new FixtureSyncResultDto(fixturesUpserted, teamsCreated, venuesCreated, skippedNoSeasonMatch);
    }

    private async Task UpsertMatchAsync(
        ApiFootballFixtureEntryModel entry,
        Guid competitionId,
        Guid seasonId,
        Guid homeTeamId,
        Guid awayTeamId,
        Guid? venueId,
        CancellationToken cancellationToken)
    {
        var match = _dbContext.Matches.Local.FirstOrDefault(m => m.ExternalApiFootballId == entry.Fixture.Id)
            ?? await _dbContext.Matches.FirstOrDefaultAsync(m => m.ExternalApiFootballId == entry.Fixture.Id, cancellationToken);

        if (match is null)
        {
            match = new Match
            {
                ExternalApiFootballId = entry.Fixture.Id,
                CompetitionId = competitionId,
                SeasonId = seasonId,
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                MatchDate = entry.Fixture.Date.UtcDateTime,
                Status = MapStatus(entry.Fixture.Status.Short),
                HomeScore = entry.Goals?.Home,
                AwayScore = entry.Goals?.Away,
                VenueId = venueId,
            };
            _dbContext.Matches.Add(match);
        }
        else
        {
            match.MatchDate = entry.Fixture.Date.UtcDateTime;
            match.Status = MapStatus(entry.Fixture.Status.Short);
            match.HomeScore = entry.Goals?.Home;
            match.AwayScore = entry.Goals?.Away;
            match.VenueId = venueId ?? match.VenueId;
        }
    }

    private async Task<(Team team, bool wasCreated)> GetOrCreateMinimalTeamAsync(ApiFootballFixtureTeamModel model, CancellationToken cancellationToken)
    {
        // Check tracked-but-not-yet-saved entities first: two fixtures in the same
        // batch can reference the same new team before SaveChanges ever runs.
        var team = _dbContext.Teams.Local.FirstOrDefault(t => t.ExternalApiFootballId == model.Id)
            ?? await _dbContext.Teams.FirstOrDefaultAsync(t => t.ExternalApiFootballId == model.Id, cancellationToken);
        if (team is not null)
        {
            return (team, false);
        }

        // Fixtures reference teams by id+name only (no country); this is a minimal
        // stand-in until a proper /teams sync (Phase 4) fills in the rest.
        team = new Team
        {
            SportId = SportConfiguration.FootballId,
            ExternalApiFootballId = model.Id,
            Name = model.Name,
        };
        _dbContext.Teams.Add(team);
        return (team, true);
    }

    private async Task<(Venue venue, bool wasCreated)> GetOrCreateVenueAsync(int externalVenueId, string name, string? city, CancellationToken cancellationToken)
    {
        var venue = _dbContext.Venues.Local.FirstOrDefault(v => v.ExternalApiFootballId == externalVenueId)
            ?? await _dbContext.Venues.FirstOrDefaultAsync(v => v.ExternalApiFootballId == externalVenueId, cancellationToken);
        if (venue is not null)
        {
            return (venue, false);
        }

        venue = new Venue
        {
            ExternalApiFootballId = externalVenueId,
            Name = name,
            City = city,
        };
        _dbContext.Venues.Add(venue);
        return (venue, true);
    }

    private static MatchStatus MapStatus(string shortStatus) => shortStatus switch
    {
        "TBD" or "NS" => MatchStatus.Scheduled,
        "1H" or "HT" or "2H" or "ET" or "BT" or "P" or "SUSP" or "INT" or "LIVE" => MatchStatus.InProgress,
        "FT" or "AET" or "PEN" => MatchStatus.Finished,
        "PST" => MatchStatus.Postponed,
        "CANC" or "ABD" or "AWD" or "WO" => MatchStatus.Cancelled,
        _ => MatchStatus.Scheduled,
    };
}

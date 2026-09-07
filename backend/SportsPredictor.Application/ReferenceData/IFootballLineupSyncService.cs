namespace SportsPredictor.Application.ReferenceData;

/// <summary>
/// Corresponds to CLAUDE.md's "FootballLineupsSyncJob". Operates on a single match
/// (not a whole tracked competition) since API-Football's lineups endpoint is
/// per-fixture.
/// </summary>
public interface IFootballLineupSyncService
{
    Task<LineupSyncResultDto> SyncLineupsAsync(Guid matchId, CancellationToken cancellationToken);
}

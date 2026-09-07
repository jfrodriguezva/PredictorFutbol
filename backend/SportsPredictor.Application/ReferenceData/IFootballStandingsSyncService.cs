namespace SportsPredictor.Application.ReferenceData;

/// <summary>
/// Corresponds to CLAUDE.md's "FootballStandingsSyncJob": captures a point-in-time
/// snapshot of a competition's table. Never overwrites — every call appends new rows,
/// so the table as of any past date can be reconstructed (CLAUDE.md section 10).
/// </summary>
public interface IFootballStandingsSyncService
{
    Task<StandingsSyncResultDto> SyncStandingsAsync(Guid trackedCompetitionId, CancellationToken cancellationToken);
}

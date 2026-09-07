namespace SportsPredictor.Application.ReferenceData;

/// <summary>
/// Manages which competitions the app tracks, and syncs their reference data
/// (teams, venues) from API-Football. Implemented by SportsPredictor.Infrastructure.
/// </summary>
public interface IFootballReferenceService
{
    Task<IReadOnlyList<TrackedCompetitionDto>> GetTrackedCompetitionsAsync(CancellationToken cancellationToken);

    /// <summary>Looks up the league (and its seasons) on API-Football, upserts Competition/Season locally, then creates the tracking row.</summary>
    Task<TrackedCompetitionDto> CreateTrackedCompetitionAsync(CreateTrackedCompetitionRequest request, CancellationToken cancellationToken);

    Task<TrackedCompetitionDto> SetEnabledAsync(Guid trackedCompetitionId, bool enabled, CancellationToken cancellationToken);

    /// <summary>Fetches GET /teams for the tracked competition's league+season and upserts Team/Venue by ExternalApiFootballId.</summary>
    Task<TeamSyncResultDto> SyncTeamsAsync(Guid trackedCompetitionId, CancellationToken cancellationToken);
}

namespace SportsPredictor.Application.ReferenceData;

/// <summary>Corresponds to CLAUDE.md's "FootballInjuriesSyncJob".</summary>
public interface IFootballInjurySyncService
{
    Task<InjurySyncResultDto> SyncInjuriesAsync(Guid trackedCompetitionId, CancellationToken cancellationToken);
}

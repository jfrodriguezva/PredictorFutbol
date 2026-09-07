namespace SportsPredictor.Application.Matches;

/// <summary>Read-only match listings for the dashboard — no sync/ingestion logic here.</summary>
public interface IMatchQueryService
{
    Task<IReadOnlyList<MatchDto>> GetMatchesForTrackedCompetitionAsync(Guid trackedCompetitionId, CancellationToken cancellationToken);

    Task<MatchDto> GetMatchAsync(Guid matchId, CancellationToken cancellationToken);
}

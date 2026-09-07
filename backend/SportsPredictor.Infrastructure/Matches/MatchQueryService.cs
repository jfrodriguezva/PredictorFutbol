using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Matches;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.Matches;

public sealed class MatchQueryService : IMatchQueryService
{
    private readonly SportsPredictorDbContext _dbContext;

    public MatchQueryService(SportsPredictorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MatchDto>> GetMatchesForTrackedCompetitionAsync(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        var tracked = await _dbContext.TrackedCompetitions.FindAsync([trackedCompetitionId], cancellationToken)
            ?? throw new NotFoundException(nameof(TrackedCompetition), trackedCompetitionId);

        var matches = await _dbContext.Matches
            .Where(m => m.CompetitionId == tracked.CompetitionId)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderByDescending(m => m.MatchDate)
            .Take(200)
            .ToListAsync(cancellationToken);

        return matches.Select(ToDto).ToList();
    }

    public async Task<MatchDto> GetMatchAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await _dbContext.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken)
            ?? throw new NotFoundException(nameof(Match), matchId);

        return ToDto(match);
    }

    private static MatchDto ToDto(Match match) => new(
        match.Id,
        match.HomeTeam?.Name ?? string.Empty,
        match.HomeTeamId,
        match.AwayTeam?.Name ?? string.Empty,
        match.AwayTeamId,
        match.MatchDate,
        match.Status.ToString(),
        match.HomeScore,
        match.AwayScore);
}

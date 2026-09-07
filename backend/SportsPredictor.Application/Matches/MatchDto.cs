namespace SportsPredictor.Application.Matches;

public sealed record MatchDto(
    Guid Id,
    string HomeTeamName,
    Guid HomeTeamId,
    string AwayTeamName,
    Guid AwayTeamId,
    DateTime MatchDateUtc,
    string Status,
    int? HomeScore,
    int? AwayScore);

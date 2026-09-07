namespace SportsPredictor.Application.ExternalData;

public sealed record LeagueDto(
    int ExternalLeagueId,
    string Name,
    string? Type,
    string? CountryName,
    int? CurrentSeasonYear);

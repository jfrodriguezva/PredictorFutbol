namespace SportsPredictor.Application.ExternalData;

public sealed record ApiFootballConnectionTestResult(bool Success, string? ErrorMessage, int? CountriesReturned);

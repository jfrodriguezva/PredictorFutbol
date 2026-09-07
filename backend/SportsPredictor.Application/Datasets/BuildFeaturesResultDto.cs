namespace SportsPredictor.Application.Datasets;

public sealed record BuildFeaturesResultDto(int MatchesProcessed, int FeatureValuesCreated, DateTime CapturedAtUtc);

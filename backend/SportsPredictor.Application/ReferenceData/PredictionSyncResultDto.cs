namespace SportsPredictor.Application.ReferenceData;

public sealed record PredictionSyncResultDto(bool Created, double? HomeProbability, double? DrawProbability, double? AwayProbability, DateTime CapturedAtUtc);

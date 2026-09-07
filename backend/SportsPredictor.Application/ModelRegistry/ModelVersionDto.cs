namespace SportsPredictor.Application.ModelRegistry;

public sealed record ModelVersionDto(
    Guid Id,
    string ModelName,
    string Sport,
    string Version,
    string Algorithm,
    DateTime TrainedAt,
    DateTime TrainingStartDate,
    DateTime TrainingEndDate,
    double? BrierScore,
    double? LogLoss,
    double? Accuracy,
    string ArtifactPath,
    bool Active);

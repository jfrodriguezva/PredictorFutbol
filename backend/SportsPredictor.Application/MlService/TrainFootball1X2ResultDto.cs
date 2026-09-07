namespace SportsPredictor.Application.MlService;

public sealed record TrainFootball1X2ResultDto(
    IReadOnlyList<TrainingAlgorithmResultDto> AlgorithmResults,
    string SelectedAlgorithm,
    string Version,
    string ArtifactPath,
    int DatasetSize,
    DateTime TrainingStartDate,
    DateTime TrainingEndDate,
    DateTime TrainedAtUtc);

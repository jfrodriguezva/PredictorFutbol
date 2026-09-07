namespace SportsPredictor.Application.MlService;

public sealed record TrainingAlgorithmResultDto(string Algorithm, double LogLoss, double BrierScore, double Accuracy);

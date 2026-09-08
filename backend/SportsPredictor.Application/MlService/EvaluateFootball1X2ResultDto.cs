namespace SportsPredictor.Application.MlService;

public sealed record CalibrationBinDto(double BinLower, double BinUpper, double PredictedMean, double ActualFrequency, int Count);

public sealed record CalibrationReportDto(double ExpectedCalibrationError, IReadOnlyList<CalibrationBinDto> Bins);

public sealed record BenchmarkResultDto(string Name, double LogLoss, double BrierScore, double Accuracy);

public sealed record BacktestWindowDto(
    int WindowIndex, int TrainSize, int TestSize, double Accuracy, double PrecisionMacro, double RecallMacro,
    double F1Macro, double LogLoss, double BrierScore, double CalibrationError, int TotalBets, double WinRate,
    double Roi, double AverageOdds);

public sealed record BacktestSummaryDto(
    IReadOnlyList<BacktestWindowDto> Windows, int TotalBets, double OverallRoi, double OverallWinRate,
    double MaxDrawdown, double AverageLogLoss, double AverageBrierScore, double AverageCalibrationError);

/// <summary>
/// Diagnostic only — never persisted. Lets a caller judge a model/benchmarks before
/// deciding whether to register/activate anything (CLAUDE.md sections 26-27).
/// </summary>
public sealed record EvaluateFootball1X2ResultDto(
    CalibrationReportDto Calibration,
    /// <summary>Null when the selected algorithm is "Ensemble" — not calibratable (see ml/evaluation/calibration.py).</summary>
    CalibrationReportDto? CalibrationAfterIsotonic,
    BacktestSummaryDto Backtest,
    IReadOnlyList<BenchmarkResultDto> Benchmarks,
    int DatasetSize);

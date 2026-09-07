using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.MlService.Models;

/// <summary>Mirrors ml/app/schemas.py's EvaluateFootball1X2Response.</summary>
public sealed class EvaluateFootball1X2ResponseModel
{
    [JsonPropertyName("calibration")]
    public CalibrationReportModel Calibration { get; set; } = new();

    [JsonPropertyName("backtest")]
    public BacktestSummaryModel Backtest { get; set; } = new();

    [JsonPropertyName("benchmarks")]
    public List<BenchmarkResultModel> Benchmarks { get; set; } = new();

    [JsonPropertyName("dataset_size")]
    public int DatasetSize { get; set; }
}

public sealed class CalibrationReportModel
{
    [JsonPropertyName("expected_calibration_error")]
    public double ExpectedCalibrationError { get; set; }

    [JsonPropertyName("bins")]
    public List<CalibrationBinModel> Bins { get; set; } = new();
}

public sealed class CalibrationBinModel
{
    [JsonPropertyName("bin_lower")]
    public double BinLower { get; set; }

    [JsonPropertyName("bin_upper")]
    public double BinUpper { get; set; }

    [JsonPropertyName("predicted_mean")]
    public double PredictedMean { get; set; }

    [JsonPropertyName("actual_frequency")]
    public double ActualFrequency { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public sealed class BenchmarkResultModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("log_loss")]
    public double LogLoss { get; set; }

    [JsonPropertyName("brier_score")]
    public double BrierScore { get; set; }

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }
}

public sealed class BacktestWindowModel
{
    [JsonPropertyName("window_index")]
    public int WindowIndex { get; set; }

    [JsonPropertyName("train_size")]
    public int TrainSize { get; set; }

    [JsonPropertyName("test_size")]
    public int TestSize { get; set; }

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    [JsonPropertyName("precision_macro")]
    public double PrecisionMacro { get; set; }

    [JsonPropertyName("recall_macro")]
    public double RecallMacro { get; set; }

    [JsonPropertyName("f1_macro")]
    public double F1Macro { get; set; }

    [JsonPropertyName("log_loss")]
    public double LogLoss { get; set; }

    [JsonPropertyName("brier_score")]
    public double BrierScore { get; set; }

    [JsonPropertyName("calibration_error")]
    public double CalibrationError { get; set; }

    [JsonPropertyName("total_bets")]
    public int TotalBets { get; set; }

    [JsonPropertyName("win_rate")]
    public double WinRate { get; set; }

    [JsonPropertyName("roi")]
    public double Roi { get; set; }

    [JsonPropertyName("average_odds")]
    public double AverageOdds { get; set; }
}

public sealed class BacktestSummaryModel
{
    [JsonPropertyName("windows")]
    public List<BacktestWindowModel> Windows { get; set; } = new();

    [JsonPropertyName("total_bets")]
    public int TotalBets { get; set; }

    [JsonPropertyName("overall_roi")]
    public double OverallRoi { get; set; }

    [JsonPropertyName("overall_win_rate")]
    public double OverallWinRate { get; set; }

    [JsonPropertyName("max_drawdown")]
    public double MaxDrawdown { get; set; }

    [JsonPropertyName("average_log_loss")]
    public double AverageLogLoss { get; set; }

    [JsonPropertyName("average_brier_score")]
    public double AverageBrierScore { get; set; }

    [JsonPropertyName("average_calibration_error")]
    public double AverageCalibrationError { get; set; }
}

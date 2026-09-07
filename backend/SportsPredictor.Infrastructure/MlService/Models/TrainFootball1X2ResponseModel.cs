using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.MlService.Models;

/// <summary>Mirrors ml/app/schemas.py's TrainFootball1X2Response (snake_case field names from FastAPI/Pydantic).</summary>
public sealed class TrainFootball1X2ResponseModel
{
    [JsonPropertyName("algorithm_results")]
    public List<AlgorithmResultModel> AlgorithmResults { get; set; } = new();

    [JsonPropertyName("selected_algorithm")]
    public string SelectedAlgorithm { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("artifact_path")]
    public string ArtifactPath { get; set; } = string.Empty;

    [JsonPropertyName("dataset_size")]
    public int DatasetSize { get; set; }

    [JsonPropertyName("training_start_date")]
    public DateTime TrainingStartDate { get; set; }

    [JsonPropertyName("training_end_date")]
    public DateTime TrainingEndDate { get; set; }

    [JsonPropertyName("trained_at_utc")]
    public DateTime TrainedAtUtc { get; set; }
}

public sealed class AlgorithmResultModel
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = string.Empty;

    [JsonPropertyName("log_loss")]
    public double LogLoss { get; set; }

    [JsonPropertyName("brier_score")]
    public double BrierScore { get; set; }

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }
}

using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// One trained model artifact. A new training run always creates a new version;
/// existing versions are never overwritten.
/// </summary>
public class ModelVersion : Entity
{
    public required string ModelName { get; set; }

    public required string Sport { get; set; }

    public required string Version { get; set; }

    public required string Algorithm { get; set; }

    public required DateTime TrainedAt { get; set; }

    public required DateTime TrainingStartDate { get; set; }

    public required DateTime TrainingEndDate { get; set; }

    public double? BrierScore { get; set; }

    public double? LogLoss { get; set; }

    public double? Accuracy { get; set; }

    public double? Roi { get; set; }

    public required string ArtifactPath { get; set; }

    public bool Active { get; set; }
}

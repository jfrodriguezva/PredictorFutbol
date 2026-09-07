using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

public class TrainingRun : Entity
{
    public required Guid ModelVersionId { get; set; }

    public required DateTime StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public required int DatasetSize { get; set; }

    public required string TrainingParametersJson { get; set; }

    public string? MetricsJson { get; set; }

    public ModelVersion? ModelVersion { get; set; }
}

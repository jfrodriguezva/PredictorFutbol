using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// A prediction snapshot, always saved before the match is played. Never modified
/// retroactively after the outcome is known — a re-run creates a new snapshot instead.
/// </summary>
public class Prediction : Entity
{
    public required Guid MatchId { get; set; }

    public required Guid ModelVersionId { get; set; }

    public required DateTime PredictionDate { get; set; }

    public required string Market { get; set; }

    public required string Selection { get; set; }

    public required double Probability { get; set; }

    public double? ExpectedValue { get; set; }

    public bool Recommended { get; set; }

    /// <summary>Populated only once the match result is known, via a separate evaluation write path.</summary>
    public string? ActualOutcome { get; set; }

    public bool? IsCorrect { get; set; }

    public Match? Match { get; set; }
    public ModelVersion? ModelVersion { get; set; }
}

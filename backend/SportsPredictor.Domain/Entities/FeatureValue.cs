using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// A single named feature computed for a match. <see cref="AvailableAt"/> is the
/// anti-data-leakage guard: a feature must never be used to predict a match if
/// AvailableAt is after that match's kickoff.
/// </summary>
public class FeatureValue : Entity
{
    public required Guid MatchId { get; set; }

    public required string FeatureName { get; set; }

    public required double NumericValue { get; set; }

    /// <summary>The earliest UTC instant at which this value could legitimately have been known.</summary>
    public required DateTime AvailableAt { get; set; }

    public required DateTime CapturedAt { get; set; }

    public Match? Match { get; set; }
}

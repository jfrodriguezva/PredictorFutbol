using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// A snapshot of API-Football's own prediction for a match (their /predictions
/// endpoint), kept as an external benchmark — never treated as ground truth, and
/// never overwritten (CLAUDE.md section 23).
/// </summary>
public class ApiFootballPredictionSnapshot : Entity
{
    public required Guid MatchId { get; set; }

    public required DateTime CapturedAt { get; set; }

    public required double HomeProbability { get; set; }

    public required double DrawProbability { get; set; }

    public required double AwayProbability { get; set; }

    public string? PredictedWinner { get; set; }

    /// <summary>
    /// API-Football does not return a single clean "predicted score" field — this
    /// holds whatever textual advice/goals guidance the endpoint provides, as-is.
    /// </summary>
    public string? PredictedScore { get; set; }

    public required string RawJson { get; set; }

    public Match? Match { get; set; }
}

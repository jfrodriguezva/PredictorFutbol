using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// The "expert analyst" explanation layered on top of a plain 1X2 prediction: SHAP
/// feature attribution, Kelly-Criterion stake sizing (when odds were available), and a
/// narrative (Claude-generated, or template-based without ANTHROPIC_API_KEY) — see
/// ml/app/main.py's POST /analyze/football-1x2. Keyed by MatchId + ModelVersionId
/// rather than a specific Prediction row, so it can be generated independently of
/// whether /api/predictions/generate has already run for this match. Like Prediction,
/// treated as an immutable snapshot — a re-analysis creates a new row.
/// </summary>
public class PredictionExplanation : Entity
{
    public required Guid MatchId { get; set; }

    public required Guid ModelVersionId { get; set; }

    public required DateTime GeneratedAt { get; set; }

    public required double ProbabilityHome { get; set; }
    public required double ProbabilityDraw { get; set; }
    public required double ProbabilityAway { get; set; }

    /// <summary>JSON array of {"feature": string, "impact": number}, most impactful first.</summary>
    public required string ShapTopFeaturesJson { get; set; }

    /// <summary>JSON object keyed "home"/"draw"/"away" with odds/edge/suggested-stake per
    /// outcome (see ml/decision/staking.py's recommend_stakes shape). Null when no market
    /// odds were available at analysis time.</summary>
    public string? StakesJson { get; set; }

    public required string NarrativeText { get; set; }

    public Match? Match { get; set; }
    public ModelVersion? ModelVersion { get; set; }
}

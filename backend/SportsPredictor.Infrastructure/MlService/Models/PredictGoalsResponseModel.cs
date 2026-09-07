using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.MlService.Models;

/// <summary>Mirrors ml/app/schemas.py's PredictGoalsResponse.</summary>
public sealed class PredictGoalsResponseModel
{
    [JsonPropertyName("home_expected_goals")]
    public double HomeExpectedGoals { get; set; }

    [JsonPropertyName("away_expected_goals")]
    public double AwayExpectedGoals { get; set; }

    [JsonPropertyName("over_2_5")]
    public double Over25 { get; set; }

    [JsonPropertyName("under_2_5")]
    public double Under25 { get; set; }

    [JsonPropertyName("btts_yes")]
    public double BttsYes { get; set; }

    [JsonPropertyName("btts_no")]
    public double BttsNo { get; set; }

    [JsonPropertyName("most_likely_home_goals")]
    public int MostLikelyHomeGoals { get; set; }

    [JsonPropertyName("most_likely_away_goals")]
    public int MostLikelyAwayGoals { get; set; }

    [JsonPropertyName("most_likely_score_probability")]
    public double MostLikelyScoreProbability { get; set; }
}

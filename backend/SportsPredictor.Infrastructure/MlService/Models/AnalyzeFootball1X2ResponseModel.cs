using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.MlService.Models;

public sealed class ShapFeatureImpactModel
{
    [JsonPropertyName("feature")]
    public required string Feature { get; set; }

    [JsonPropertyName("impact")]
    public double Impact { get; set; }
}

public sealed class StakeRecommendationModel
{
    [JsonPropertyName("label")]
    public required string Label { get; set; }

    [JsonPropertyName("decimal_odds")]
    public double DecimalOdds { get; set; }

    [JsonPropertyName("implied_probability")]
    public double ImpliedProbability { get; set; }

    [JsonPropertyName("model_probability")]
    public double ModelProbability { get; set; }

    [JsonPropertyName("edge")]
    public double Edge { get; set; }

    [JsonPropertyName("is_value_bet")]
    public bool IsValueBet { get; set; }

    [JsonPropertyName("kelly_fraction_full")]
    public double KellyFractionFull { get; set; }

    [JsonPropertyName("suggested_stake_pct_bankroll")]
    public double SuggestedStakePctBankroll { get; set; }
}

public sealed class AnalyzeFootball1X2ResponseModel
{
    [JsonPropertyName("home")]
    public double Home { get; set; }

    [JsonPropertyName("draw")]
    public double Draw { get; set; }

    [JsonPropertyName("away")]
    public double Away { get; set; }

    [JsonPropertyName("shap_top_features")]
    public required List<ShapFeatureImpactModel> ShapTopFeatures { get; set; }

    [JsonPropertyName("stakes")]
    public Dictionary<string, StakeRecommendationModel>? Stakes { get; set; }

    [JsonPropertyName("narrative")]
    public required string Narrative { get; set; }
}

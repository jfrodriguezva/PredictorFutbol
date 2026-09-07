using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

/// <summary>
/// GET /predictions?fixture={id} response shape, per the public API-Football v3
/// documentation. NOT verified against a live response — same caveat as the other
/// models. Treated purely as an external benchmark, never as ground truth
/// (CLAUDE.md section 23).
/// </summary>
public sealed class ApiFootballPredictionResponseModel
{
    [JsonPropertyName("predictions")]
    public ApiFootballPredictionDetailModel Predictions { get; set; } = new();
}

public sealed class ApiFootballPredictionDetailModel
{
    [JsonPropertyName("winner")]
    public ApiFootballPredictionWinnerModel? Winner { get; set; }

    [JsonPropertyName("advice")]
    public string? Advice { get; set; }

    /// <summary>Home/draw/away win percentages as strings, e.g. "45%".</summary>
    [JsonPropertyName("percent")]
    public ApiFootballPredictionPercentModel Percent { get; set; } = new();
}

public sealed class ApiFootballPredictionWinnerModel
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class ApiFootballPredictionPercentModel
{
    [JsonPropertyName("home")]
    public string? Home { get; set; }

    [JsonPropertyName("draw")]
    public string? Draw { get; set; }

    [JsonPropertyName("away")]
    public string? Away { get; set; }
}

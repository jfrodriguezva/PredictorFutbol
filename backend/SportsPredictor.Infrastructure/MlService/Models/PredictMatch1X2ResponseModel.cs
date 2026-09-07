using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.MlService.Models;

public sealed class PredictMatch1X2ResponseModel
{
    [JsonPropertyName("home")]
    public double Home { get; set; }

    [JsonPropertyName("draw")]
    public double Draw { get; set; }

    [JsonPropertyName("away")]
    public double Away { get; set; }
}

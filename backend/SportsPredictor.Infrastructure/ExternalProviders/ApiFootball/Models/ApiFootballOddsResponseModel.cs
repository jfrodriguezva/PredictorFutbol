using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

/// <summary>
/// GET /odds?fixture={id} response shape, per the public API-Football v3
/// documentation. NOT verified against a live response — same caveat as the other
/// models.
/// </summary>
public sealed class ApiFootballOddsResponseModel
{
    [JsonPropertyName("bookmakers")]
    public List<ApiFootballBookmakerModel> Bookmakers { get; set; } = new();
}

public sealed class ApiFootballBookmakerModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("bets")]
    public List<ApiFootballBetModel> Bets { get; set; } = new();
}

public sealed class ApiFootballBetModel
{
    /// <summary>E.g. "Match Winner", "Goals Over/Under".</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("values")]
    public List<ApiFootballOddValueModel> Values { get; set; } = new();
}

public sealed class ApiFootballOddValueModel
{
    /// <summary>
    /// E.g. "Home", "Draw", "Away", "Over 2.5" — but for handicap/total-line bet
    /// types API-Football sends this as a bare JSON number instead of a string
    /// (observed live against real Liga MX odds), so it needs a lenient converter
    /// rather than a plain string property.
    /// </summary>
    [JsonPropertyName("value")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string Value { get; set; } = string.Empty;

    /// <summary>Decimal odd as a string, e.g. "1.85".</summary>
    [JsonPropertyName("odd")]
    public string Odd { get; set; } = string.Empty;
}

/// <summary>Reads a JSON string or number into a string — API-Football is inconsistent about which it sends.</summary>
public sealed class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => string.Empty,
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

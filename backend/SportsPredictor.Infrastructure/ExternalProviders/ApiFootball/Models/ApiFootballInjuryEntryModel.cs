using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

/// <summary>
/// GET /injuries?league={id}&season={year} response shape, per the public API-Football
/// v3 documentation. NOT verified against a live response — same caveat as the other
/// models. Note API-Football does not report injury start/expected-return dates here.
/// </summary>
public sealed class ApiFootballInjuryEntryModel
{
    [JsonPropertyName("player")]
    public ApiFootballInjuryPlayerModel Player { get; set; } = new();

    [JsonPropertyName("team")]
    public ApiFootballFixtureTeamModel Team { get; set; } = new();

    [JsonPropertyName("fixture")]
    public ApiFootballInjuryFixtureRefModel? Fixture { get; set; }
}

public sealed class ApiFootballInjuryPlayerModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public sealed class ApiFootballInjuryFixtureRefModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

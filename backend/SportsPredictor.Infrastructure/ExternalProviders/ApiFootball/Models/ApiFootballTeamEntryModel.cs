using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

/// <summary>
/// GET /teams?league={id}&season={year} response shape, per the public API-Football v3
/// documentation. NOT verified against a live response (no API_FOOTBALL_KEY available
/// while implementing this phase) — same caveat as the countries/leagues models.
/// </summary>
public sealed class ApiFootballTeamEntryModel
{
    [JsonPropertyName("team")]
    public ApiFootballTeamInfoModel Team { get; set; } = new();

    [JsonPropertyName("venue")]
    public ApiFootballVenueModel? Venue { get; set; }
}

public sealed class ApiFootballTeamInfoModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string? Country { get; set; }
}

public sealed class ApiFootballVenueModel
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

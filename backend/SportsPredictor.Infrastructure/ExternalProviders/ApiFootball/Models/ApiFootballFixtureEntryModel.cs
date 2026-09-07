using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

/// <summary>
/// GET /fixtures?league={id}&season={year} response shape, per the public API-Football
/// v3 documentation. NOT verified against a live response (no API_FOOTBALL_KEY
/// available while implementing this phase) — same caveat as the other models.
/// </summary>
public sealed class ApiFootballFixtureEntryModel
{
    [JsonPropertyName("fixture")]
    public ApiFootballFixtureInfoModel Fixture { get; set; } = new();

    [JsonPropertyName("teams")]
    public ApiFootballFixtureTeamsModel Teams { get; set; } = new();

    [JsonPropertyName("goals")]
    public ApiFootballFixtureGoalsModel? Goals { get; set; }
}

public sealed class ApiFootballFixtureInfoModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; set; }

    [JsonPropertyName("status")]
    public ApiFootballFixtureStatusModel Status { get; set; } = new();

    [JsonPropertyName("venue")]
    public ApiFootballVenueModel? Venue { get; set; }
}

public sealed class ApiFootballFixtureStatusModel
{
    /// <summary>Short status code, e.g. "NS", "1H", "FT", "PST", "CANC".</summary>
    [JsonPropertyName("short")]
    public string Short { get; set; } = string.Empty;
}

public sealed class ApiFootballFixtureTeamsModel
{
    [JsonPropertyName("home")]
    public ApiFootballFixtureTeamModel Home { get; set; } = new();

    [JsonPropertyName("away")]
    public ApiFootballFixtureTeamModel Away { get; set; } = new();
}

public sealed class ApiFootballFixtureTeamModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class ApiFootballFixtureGoalsModel
{
    [JsonPropertyName("home")]
    public int? Home { get; set; }

    [JsonPropertyName("away")]
    public int? Away { get; set; }
}

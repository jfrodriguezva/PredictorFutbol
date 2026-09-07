using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

/// <summary>
/// GET /fixtures/lineups?fixture={id} response shape (one entry per team in the
/// fixture), per the public API-Football v3 documentation. NOT verified against a
/// live response — same caveat as the other models.
/// </summary>
public sealed class ApiFootballLineupEntryModel
{
    [JsonPropertyName("team")]
    public ApiFootballFixtureTeamModel Team { get; set; } = new();

    [JsonPropertyName("formation")]
    public string? Formation { get; set; }

    [JsonPropertyName("coach")]
    public ApiFootballCoachModel? Coach { get; set; }
}

public sealed class ApiFootballCoachModel
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

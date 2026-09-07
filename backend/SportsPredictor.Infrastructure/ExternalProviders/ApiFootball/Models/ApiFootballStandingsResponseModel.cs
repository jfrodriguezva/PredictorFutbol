using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

/// <summary>
/// GET /standings?league={id}&season={year} response shape, per the public API-Football
/// v3 documentation. NOT verified against a live response (no API_FOOTBALL_KEY
/// available while implementing this phase) — same caveat as the other models.
/// </summary>
public sealed class ApiFootballStandingsResponseModel
{
    [JsonPropertyName("league")]
    public ApiFootballStandingsLeagueModel League { get; set; } = new();
}

public sealed class ApiFootballStandingsLeagueModel
{
    /// <summary>
    /// Array of groups (usually a single group for a normal league table; multiple
    /// for competitions with group stages). Flattened by the sync service.
    /// </summary>
    [JsonPropertyName("standings")]
    public List<List<ApiFootballStandingRowModel>> Standings { get; set; } = new();
}

public sealed class ApiFootballStandingRowModel
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("team")]
    public ApiFootballFixtureTeamModel Team { get; set; } = new();

    [JsonPropertyName("points")]
    public int Points { get; set; }

    [JsonPropertyName("goalsDiff")]
    public int GoalsDiff { get; set; }

    [JsonPropertyName("form")]
    public string? Form { get; set; }

    [JsonPropertyName("all")]
    public ApiFootballStandingStatsModel All { get; set; } = new();
}

public sealed class ApiFootballStandingStatsModel
{
    [JsonPropertyName("played")]
    public int Played { get; set; }

    [JsonPropertyName("win")]
    public int Win { get; set; }

    [JsonPropertyName("draw")]
    public int Draw { get; set; }

    [JsonPropertyName("lose")]
    public int Lose { get; set; }

    [JsonPropertyName("goals")]
    public ApiFootballStandingGoalsModel Goals { get; set; } = new();
}

public sealed class ApiFootballStandingGoalsModel
{
    [JsonPropertyName("for")]
    public int For { get; set; }

    [JsonPropertyName("against")]
    public int Against { get; set; }
}

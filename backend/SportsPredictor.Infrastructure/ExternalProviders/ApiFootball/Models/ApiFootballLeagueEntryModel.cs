using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

public sealed class ApiFootballLeagueEntryModel
{
    [JsonPropertyName("league")]
    public ApiFootballLeagueInfoModel League { get; set; } = new();

    [JsonPropertyName("country")]
    public ApiFootballCountryModel? Country { get; set; }

    [JsonPropertyName("seasons")]
    public List<ApiFootballSeasonModel> Seasons { get; set; } = new();
}

public sealed class ApiFootballLeagueInfoModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public sealed class ApiFootballSeasonModel
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("start")]
    public DateOnly? Start { get; set; }

    [JsonPropertyName("end")]
    public DateOnly? End { get; set; }

    [JsonPropertyName("current")]
    public bool Current { get; set; }
}

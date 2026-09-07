using System.Text.Json.Serialization;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball.Models;

/// <summary>
/// The standard API-Football v3 response envelope, per the public documentation.
/// NOT verified against a live response (no API_FOOTBALL_KEY was available while
/// implementing this phase) — re-check against a real payload once a key is set,
/// per CLAUDE.md's "inspect real responses before finalizing DTOs" rule.
/// </summary>
public sealed class ApiFootballEnvelope<T>
{
    [JsonPropertyName("results")]
    public int Results { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonPropertyName("response")]
    public List<T> Response { get; set; } = new();
}

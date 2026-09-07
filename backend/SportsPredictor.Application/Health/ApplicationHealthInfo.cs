namespace SportsPredictor.Application.Health;

/// <summary>
/// Minimal application-layer health payload exposed by the API's /health endpoint.
/// </summary>
public sealed record ApplicationHealthInfo(string Status, DateTimeOffset CheckedAtUtc, string Version);

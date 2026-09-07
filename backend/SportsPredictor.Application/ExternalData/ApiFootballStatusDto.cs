namespace SportsPredictor.Application.ExternalData;

/// <summary>Snapshot for the /settings/data-sources page. Never includes the API key.</summary>
public sealed record ApiFootballStatusDto(
    bool Connected,
    int? DailyLimit,
    int? DailyRemaining,
    int? MinuteLimit,
    int? MinuteRemaining,
    DateTime? LastCapturedAtUtc);

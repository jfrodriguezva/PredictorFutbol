using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// A point-in-time read of an external provider's rate-limit headers.
/// Never hardcode plan limits — always read them from the provider's response.
/// </summary>
public class ApiQuotaSnapshot : Entity
{
    public required string Provider { get; set; }

    public required DateTime CapturedAt { get; set; }

    public int? DailyLimit { get; set; }

    public int? DailyRemaining { get; set; }

    public int? MinuteLimit { get; set; }

    public int? MinuteRemaining { get; set; }
}

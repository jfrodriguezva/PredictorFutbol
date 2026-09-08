using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// An in-app notification surfaced by <c>ValueBetWatcherBackgroundService</c> when a
/// generated Prediction turns out to be a Value/StrongValue selection. No email/push —
/// this is the whole delivery mechanism for now (there is no user/auth system yet).
/// </summary>
public class ValueBetNotification : Entity
{
    public required Guid MatchId { get; set; }

    public required Guid PredictionId { get; set; }

    /// <summary>"Home" / "Draw" / "Away" — same convention as Prediction.Selection.</summary>
    public required string Selection { get; set; }

    public required double ExpectedValue { get; set; }

    public required DateTime DetectedAt { get; set; }

    public bool Read { get; set; }

    public Match? Match { get; set; }
    public Prediction? Prediction { get; set; }
}

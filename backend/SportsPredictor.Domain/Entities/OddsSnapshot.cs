using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// A point-in-time bookmaker quote. Never overwritten; every capture creates a new row
/// so opening/intermediate/closing lines can be reconstructed.
/// </summary>
public class OddsSnapshot : Entity
{
    public required Guid MatchId { get; set; }

    public required string Sportsbook { get; set; }

    /// <summary>E.g. "1X2", "Over/Under 2.5", "BTTS".</summary>
    public required string Market { get; set; }

    /// <summary>E.g. "Home", "Draw", "Away", "Over", "Under", "Yes", "No".</summary>
    public required string Selection { get; set; }

    public int? AmericanOdds { get; set; }

    public required decimal DecimalOdds { get; set; }

    public required double ImpliedProbability { get; set; }

    public required DateTime CapturedAt { get; set; }

    public Match? Match { get; set; }
}

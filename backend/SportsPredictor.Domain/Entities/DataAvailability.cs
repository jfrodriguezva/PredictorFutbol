using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// Records what data API-Football coverage actually provides for a given match.
/// Missing coverage is not an error — it just means that data was never available.
/// </summary>
public class DataAvailability : Entity
{
    public required Guid MatchId { get; set; }

    public bool HasEvents { get; set; }

    public bool HasLineups { get; set; }

    public bool HasStatistics { get; set; }

    public bool HasPlayerStatistics { get; set; }

    public bool HasInjuries { get; set; }

    public bool HasOdds { get; set; }

    public bool HasPredictions { get; set; }

    public required DateTime CapturedAt { get; set; }

    public Match? Match { get; set; }
}

using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// A point-in-time starting lineup/formation for one team in one match. Never
/// overwritten; a re-check near kickoff creates a new snapshot.
/// </summary>
public class LineupSnapshot : Entity
{
    public required Guid MatchId { get; set; }

    public required Guid TeamId { get; set; }

    public required string Formation { get; set; }

    /// <summary>
    /// No local Coach entity exists yet (API-Football's /coachs is Priority 3,
    /// not implemented) — the raw provider id/name are kept here as a placeholder
    /// until a proper Coach entity is introduced.
    /// </summary>
    public int? ExternalCoachId { get; set; }

    public string? CoachName { get; set; }

    public required DateTime CapturedAt { get; set; }

    public Match? Match { get; set; }
    public Team? Team { get; set; }
}

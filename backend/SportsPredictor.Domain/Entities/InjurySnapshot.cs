using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// A point-in-time injury report. Never overwritten; a new report creates a new
/// snapshot (CLAUDE.md section 11 — "no sobrescribir historia; crear snapshots").
/// </summary>
public class InjurySnapshot : Entity
{
    public required Guid PlayerId { get; set; }

    public required Guid TeamId { get; set; }

    public Guid? MatchId { get; set; }

    public required string Type { get; set; }

    public string? Reason { get; set; }

    /// <summary>
    /// Not provided by API-Football's /injuries endpoint as of this integration —
    /// stays null until a source that reports it is wired up.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Same caveat as <see cref="StartDate"/>.</summary>
    public DateOnly? ExpectedReturn { get; set; }

    public required DateTime CapturedAt { get; set; }

    public required string Source { get; set; }

    public Player? Player { get; set; }
    public Team? Team { get; set; }
    public Match? Match { get; set; }
}

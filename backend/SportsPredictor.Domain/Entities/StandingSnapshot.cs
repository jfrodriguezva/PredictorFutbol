using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// A point-in-time snapshot of a team's league position. Never overwritten,
/// so the standings before any given match can be reconstructed.
/// </summary>
public class StandingSnapshot : Entity
{
    public required Guid CompetitionId { get; set; }

    public required Guid SeasonId { get; set; }

    public required Guid TeamId { get; set; }

    public required DateTime CapturedAt { get; set; }

    public required int Rank { get; set; }

    public required int Points { get; set; }

    public required int Played { get; set; }

    public required int Wins { get; set; }

    public required int Draws { get; set; }

    public required int Losses { get; set; }

    public required int GoalsFor { get; set; }

    public required int GoalsAgainst { get; set; }

    public required int GoalDifference { get; set; }

    /// <summary>Recent form string, e.g. "WWDLW".</summary>
    public string? Form { get; set; }

    public Competition? Competition { get; set; }
    public Season? Season { get; set; }
    public Team? Team { get; set; }
}

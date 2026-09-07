using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// Decides which competitions are worth spending API-Football quota on, and which
/// sync jobs should run for each.
/// </summary>
public class TrackedCompetition : Entity
{
    public required Guid CompetitionId { get; set; }

    public required int ExternalLeagueId { get; set; }

    public required int Season { get; set; }

    public bool Enabled { get; set; } = true;

    public int Priority { get; set; }

    public int HistoricalSeasonsToImport { get; set; }

    public bool SyncFixtures { get; set; } = true;

    public bool SyncStandings { get; set; } = true;

    public bool SyncPlayers { get; set; }

    public bool SyncStatistics { get; set; }

    public bool SyncInjuries { get; set; }

    public bool SyncOdds { get; set; }

    public bool SyncPredictions { get; set; }

    public Competition? Competition { get; set; }
}

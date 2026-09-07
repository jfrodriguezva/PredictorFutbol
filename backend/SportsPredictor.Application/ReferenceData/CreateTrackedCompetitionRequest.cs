namespace SportsPredictor.Application.ReferenceData;

/// <summary>
/// Starts tracking a competition. The competition (and its seasons) are looked up
/// from API-Football by <paramref name="ExternalLeagueId"/> and upserted locally
/// if not already known — the caller only needs the API-Football league id.
/// </summary>
public sealed record CreateTrackedCompetitionRequest(
    int ExternalLeagueId,
    int Season,
    int Priority = 0,
    int HistoricalSeasonsToImport = 0,
    bool SyncFixtures = true,
    bool SyncStandings = true,
    bool SyncPlayers = false,
    bool SyncStatistics = false,
    bool SyncInjuries = false,
    bool SyncOdds = false,
    bool SyncPredictions = false);

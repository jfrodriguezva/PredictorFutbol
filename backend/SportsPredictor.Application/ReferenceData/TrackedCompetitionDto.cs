namespace SportsPredictor.Application.ReferenceData;

public sealed record TrackedCompetitionDto(
    Guid Id,
    Guid CompetitionId,
    string CompetitionName,
    int ExternalLeagueId,
    int Season,
    bool Enabled,
    int Priority,
    int HistoricalSeasonsToImport,
    bool SyncFixtures,
    bool SyncStandings,
    bool SyncPlayers,
    bool SyncStatistics,
    bool SyncInjuries,
    bool SyncOdds,
    bool SyncPredictions);

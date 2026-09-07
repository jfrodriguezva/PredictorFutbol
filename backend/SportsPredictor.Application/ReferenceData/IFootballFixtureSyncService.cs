namespace SportsPredictor.Application.ReferenceData;

/// <summary>
/// Historical fixtures + results ingestion for a tracked competition (Phase 5, scope
/// narrowed to fixtures/results only — events/statistics/players/lineups are separate,
/// later jobs per CLAUDE.md section 14's "no MegaSyncJob" rule).
/// </summary>
public interface IFootballFixtureSyncService
{
    /// <summary>Fetches GET /fixtures for the tracked competition's league+season and upserts Match by ExternalApiFootballId.</summary>
    Task<FixtureSyncResultDto> SyncFixturesAsync(Guid trackedCompetitionId, CancellationToken cancellationToken);

    /// <summary>
    /// Incremental sync (Phase 6, CLAUDE.md section 17): re-fetches only the next
    /// <paramref name="upcomingCount"/> fixtures and the last <paramref name="recentCount"/>
    /// played fixtures for the tracked competition's league — never the whole season —
    /// and upserts them. Old, fully-completed fixtures are never re-touched.
    /// </summary>
    Task<FixtureSyncResultDto> RefreshRecentAndUpcomingFixturesAsync(
        Guid trackedCompetitionId,
        int upcomingCount,
        int recentCount,
        CancellationToken cancellationToken);
}

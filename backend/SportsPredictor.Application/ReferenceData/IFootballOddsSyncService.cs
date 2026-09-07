namespace SportsPredictor.Application.ReferenceData;

/// <summary>
/// Corresponds to CLAUDE.md's odds ingestion (section 24). Operates per-match, since
/// API-Football's /odds is per-fixture. Every call appends fresh OddsSnapshot rows —
/// never overwrites (opening/intermediate/closing lines are reconstructed from history).
/// </summary>
public interface IFootballOddsSyncService
{
    Task<OddsSyncResultDto> SyncOddsAsync(Guid matchId, CancellationToken cancellationToken);
}

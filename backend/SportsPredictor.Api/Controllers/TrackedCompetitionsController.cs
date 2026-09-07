using Microsoft.AspNetCore.Mvc;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Matches;
using SportsPredictor.Application.ReferenceData;

namespace SportsPredictor.Api.Controllers;

/// <summary>
/// Manages which competitions the app tracks and syncs their reference data
/// (teams, venues — Phase 4), historical fixtures/results/standings (Phase 5), and
/// incremental fixture refresh / injuries (Phase 6) from API-Football.
/// </summary>
[ApiController]
[Route("api/tracked-competitions")]
public sealed class TrackedCompetitionsController : ControllerBase
{
    private readonly IFootballReferenceService _referenceService;
    private readonly IFootballFixtureSyncService _fixtureSyncService;
    private readonly IFootballStandingsSyncService _standingsSyncService;
    private readonly IFootballInjurySyncService _injurySyncService;
    private readonly IMatchQueryService _matchQueryService;

    public TrackedCompetitionsController(
        IFootballReferenceService referenceService,
        IFootballFixtureSyncService fixtureSyncService,
        IFootballStandingsSyncService standingsSyncService,
        IFootballInjurySyncService injurySyncService,
        IMatchQueryService matchQueryService)
    {
        _referenceService = referenceService;
        _fixtureSyncService = fixtureSyncService;
        _standingsSyncService = standingsSyncService;
        _injurySyncService = injurySyncService;
        _matchQueryService = matchQueryService;
    }

    [HttpGet("{id:guid}/matches")]
    [ProducesResponseType(typeof(IReadOnlyList<MatchDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMatches(Guid id, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(await _matchQueryService.GetMatchesForTrackedCompetitionAsync(id, cancellationToken)));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TrackedCompetitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await _referenceService.GetTrackedCompetitionsAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TrackedCompetitionDto), StatusCodes.Status201Created)]
    public Task<IActionResult> Create([FromBody] CreateTrackedCompetitionRequest request, CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            var created = await _referenceService.CreateTrackedCompetitionAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetAll), new { }, created);
        });

    [HttpPatch("{id:guid}/enabled")]
    [ProducesResponseType(typeof(TrackedCompetitionDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SetEnabled(Guid id, [FromBody] bool enabled, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(await _referenceService.SetEnabledAsync(id, enabled, cancellationToken)));

    [HttpPost("{id:guid}/sync-teams")]
    [ProducesResponseType(typeof(TeamSyncResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SyncTeams(Guid id, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(await _referenceService.SyncTeamsAsync(id, cancellationToken)));

    [HttpPost("{id:guid}/sync-fixtures")]
    [ProducesResponseType(typeof(FixtureSyncResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SyncFixtures(Guid id, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(await _fixtureSyncService.SyncFixturesAsync(id, cancellationToken)));

    /// <summary>
    /// Incremental refresh (Phase 6): only the next <paramref name="upcoming"/>
    /// fixtures and the last <paramref name="recent"/> played ones — never the whole
    /// season. See docs/api-football.md for why this differs from sync-fixtures.
    /// </summary>
    [HttpPost("{id:guid}/refresh-fixtures")]
    [ProducesResponseType(typeof(FixtureSyncResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> RefreshFixtures(Guid id, [FromQuery] int upcoming = 10, [FromQuery] int recent = 10, CancellationToken cancellationToken = default) =>
        RunAsync(async () => Ok(await _fixtureSyncService.RefreshRecentAndUpcomingFixturesAsync(id, upcoming, recent, cancellationToken)));

    [HttpPost("{id:guid}/sync-standings")]
    [ProducesResponseType(typeof(StandingsSyncResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SyncStandings(Guid id, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(await _standingsSyncService.SyncStandingsAsync(id, cancellationToken)));

    [HttpPost("{id:guid}/sync-injuries")]
    [ProducesResponseType(typeof(InjurySyncResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SyncInjuries(Guid id, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(await _injurySyncService.SyncInjuriesAsync(id, cancellationToken)));

    /// <summary>Shared error translation for every action that may touch API-Football or a missing entity.</summary>
    private async Task<IActionResult> RunAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (ApiFootballNotConfiguredException ex)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "API-Football not configured", detail: ex.Message);
        }
        catch (ApiFootballException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "API-Football request failed", detail: ex.Message);
        }
    }
}

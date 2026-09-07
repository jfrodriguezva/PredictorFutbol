using Microsoft.AspNetCore.Mvc;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Matches;
using SportsPredictor.Application.ReferenceData;

namespace SportsPredictor.Api.Controllers;

/// <summary>
/// Per-match operations from API-Football: lineups (Phase 6), odds and API-Football's
/// own predictions (Phase 7) — all per-fixture endpoints on the provider's side.
/// </summary>
[ApiController]
[Route("api/matches")]
public sealed class MatchesController : ControllerBase
{
    private readonly IFootballLineupSyncService _lineupSyncService;
    private readonly IFootballOddsSyncService _oddsSyncService;
    private readonly IFootballPredictionSyncService _predictionSyncService;
    private readonly IMatchQueryService _matchQueryService;

    public MatchesController(
        IFootballLineupSyncService lineupSyncService,
        IFootballOddsSyncService oddsSyncService,
        IFootballPredictionSyncService predictionSyncService,
        IMatchQueryService matchQueryService)
    {
        _lineupSyncService = lineupSyncService;
        _oddsSyncService = oddsSyncService;
        _predictionSyncService = predictionSyncService;
        _matchQueryService = matchQueryService;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MatchDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(await _matchQueryService.GetMatchAsync(id, cancellationToken)));

    [HttpPost("{id:guid}/sync-lineups")]
    [ProducesResponseType(typeof(LineupSyncResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SyncLineups(Guid id, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(await _lineupSyncService.SyncLineupsAsync(id, cancellationToken)));

    [HttpPost("{id:guid}/sync-odds")]
    [ProducesResponseType(typeof(OddsSyncResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SyncOdds(Guid id, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(await _oddsSyncService.SyncOddsAsync(id, cancellationToken)));

    [HttpPost("{id:guid}/sync-prediction")]
    [ProducesResponseType(typeof(PredictionSyncResultDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SyncPrediction(Guid id, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(await _predictionSyncService.SyncPredictionAsync(id, cancellationToken)));

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

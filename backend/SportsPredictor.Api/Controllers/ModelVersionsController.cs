using Microsoft.AspNetCore.Mvc;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.MlService;
using SportsPredictor.Application.ModelRegistry;

namespace SportsPredictor.Api.Controllers;

/// <summary>
/// Phase 9: trains and registers Football 1X2 baseline model versions. This is the
/// only endpoint that writes ModelVersion/TrainingRun rows — the Python ML service
/// only ever returns metadata (see docs/football-model.md).
/// </summary>
[ApiController]
[Route("api/model-versions")]
public sealed class ModelVersionsController : ControllerBase
{
    private readonly IModelTrainingService _modelTrainingService;

    public ModelVersionsController(IModelTrainingService modelTrainingService)
    {
        _modelTrainingService = modelTrainingService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ModelVersionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var versions = await _modelTrainingService.GetModelVersionsAsync(cancellationToken);
        return Ok(versions);
    }

    [HttpPost("train-football-1x2/{trackedCompetitionId:guid}")]
    [ProducesResponseType(typeof(ModelVersionDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> TrainFootball1X2(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _modelTrainingService.TrainFootball1X2Async(trackedCompetitionId, cancellationToken);
            return CreatedAtAction(nameof(GetAll), new { }, created);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (MlServiceException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "ML service request failed", detail: ex.Message);
        }
    }

    /// <summary>Same as train-football-1x2 but pools every tracked competition (all leagues/cups/seasons) into one dataset.</summary>
    [HttpPost("train-football-1x2-global")]
    [ProducesResponseType(typeof(ModelVersionDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> TrainFootball1X2Global(CancellationToken cancellationToken)
    {
        try
        {
            var created = await _modelTrainingService.TrainFootball1X2GlobalAsync(cancellationToken);
            return CreatedAtAction(nameof(GetAll), new { }, created);
        }
        catch (MlServiceException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "ML service request failed", detail: ex.Message);
        }
    }

    /// <summary>Same as evaluate-football-1x2 but over the pooled all-competitions dataset. Diagnostic only.</summary>
    [HttpPost("evaluate-football-1x2-global")]
    [ProducesResponseType(typeof(EvaluateFootball1X2ResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> EvaluateFootball1X2Global([FromQuery] int windows = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _modelTrainingService.EvaluateFootball1X2GlobalAsync(windows, cancellationToken);
            return Ok(result);
        }
        catch (MlServiceException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "ML service request failed", detail: ex.Message);
        }
    }

    /// <summary>Phase 10: diagnostic only — calibration + walk-forward backtest + benchmarks. Persists nothing.</summary>
    [HttpPost("evaluate-football-1x2/{trackedCompetitionId:guid}")]
    [ProducesResponseType(typeof(EvaluateFootball1X2ResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> EvaluateFootball1X2(Guid trackedCompetitionId, [FromQuery] int windows = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _modelTrainingService.EvaluateFootball1X2Async(trackedCompetitionId, windows, cancellationToken);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (MlServiceException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "ML service request failed", detail: ex.Message);
        }
    }

    /// <summary>
    /// Marks this version as the one GeneratePrediction1X2Async/AnalyzeMatchAsync should
    /// use, deactivating any other version of the same model. Without ever calling this,
    /// both fall back to "most recently trained" (unchanged prior behavior).
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(ModelVersionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _modelTrainingService.ActivateAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
    }

    /// <summary>Phase 11: Expected Goals (Poisson/Dixon-Coles) for a hypothetical fixture. Diagnostic only, persists nothing.</summary>
    [HttpPost("predict-goals/{trackedCompetitionId:guid}")]
    [ProducesResponseType(typeof(PredictGoalsResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PredictGoals(
        Guid trackedCompetitionId,
        [FromQuery] Guid homeTeamId,
        [FromQuery] Guid awayTeamId,
        [FromQuery] double? dixonColesRho = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _modelTrainingService.PredictGoalsAsync(trackedCompetitionId, homeTeamId, awayTeamId, dixonColesRho, cancellationToken);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (MlServiceException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "ML service request failed", detail: ex.Message);
        }
    }
}

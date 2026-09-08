using Microsoft.AspNetCore.Mvc;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Datasets;
using SportsPredictor.Application.MlService;
using SportsPredictor.Application.ModelRegistry;
using SportsPredictor.Application.Predictions;

namespace SportsPredictor.Api.Controllers;

/// <summary>
/// The real "generate a prediction" endpoint: scores a match with a trained model and
/// prices it against known odds. Saved as a permanent snapshot — never edited after
/// the fact (CLAUDE.md section 29).
/// </summary>
[ApiController]
[Route("api/predictions")]
public sealed class PredictionsController : ControllerBase
{
    private readonly IPredictionGenerationService _predictionService;
    private readonly IPredictionAnalysisService _predictionAnalysisService;
    private readonly IDatasetBuilderService _datasetBuilderService;
    private readonly IModelTrainingService _modelTrainingService;

    public PredictionsController(
        IPredictionGenerationService predictionService,
        IPredictionAnalysisService predictionAnalysisService,
        IDatasetBuilderService datasetBuilderService,
        IModelTrainingService modelTrainingService)
    {
        _predictionService = predictionService;
        _predictionAnalysisService = predictionAnalysisService;
        _datasetBuilderService = datasetBuilderService;
        _modelTrainingService = modelTrainingService;
    }

    [HttpPost("generate/{matchId:guid}")]
    [ProducesResponseType(typeof(GeneratePredictionResultDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Generate(Guid matchId, [FromQuery] Guid? modelVersionId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _predictionService.GeneratePrediction1X2Async(matchId, modelVersionId, cancellationToken);
            return CreatedAtAction(nameof(GetForMatch), new { matchId }, result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "No trained model available", detail: ex.Message);
        }
        catch (MlServiceException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "ML service request failed", detail: ex.Message);
        }
    }

    [HttpGet("match/{matchId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PredictionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForMatch(Guid matchId, CancellationToken cancellationToken)
    {
        var result = await _predictionService.GetPredictionsForMatchAsync(matchId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("recommended")]
    [ProducesResponseType(typeof(IReadOnlyList<PredictionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecommended(CancellationToken cancellationToken)
    {
        var result = await _predictionService.GetRecommendedPredictionsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Records the real result against every prediction made for this match. The match must already be Finished (synced).</summary>
    [HttpPost("evaluate/{matchId:guid}")]
    [ProducesResponseType(typeof(EvaluateMatchResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Evaluate(Guid matchId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _predictionService.EvaluateMatchAsync(matchId, cancellationToken);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Match not finished yet", detail: ex.Message);
        }
    }

    /// <summary>Hit-rate across every evaluated prediction so far — "where has the model been wrong".</summary>
    [HttpGet("accuracy")]
    [ProducesResponseType(typeof(AccuracySummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccuracy([FromQuery] Guid? modelVersionId, CancellationToken cancellationToken)
    {
        var result = await _predictionService.GetAccuracySummaryAsync(modelVersionId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The "expert analyst" explanation for this match: SHAP feature attribution,
    /// Kelly-Criterion stake sizing (when market odds are available), and a narrative
    /// (Claude-generated, or template-based without ANTHROPIC_API_KEY configured in
    /// ml/). Persists a new PredictionExplanation snapshot every call.
    /// </summary>
    [HttpPost("{matchId:guid}/analyze")]
    [ProducesResponseType(typeof(PredictionExplanationDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Analyze(Guid matchId, [FromQuery] Guid? modelVersionId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _predictionAnalysisService.AnalyzeMatchAsync(matchId, modelVersionId, cancellationToken);
            return CreatedAtAction(nameof(GetAnalysis), new { matchId }, result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "No trained model available", detail: ex.Message);
        }
        catch (MlServiceException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "ML service request failed", detail: ex.Message);
        }
        catch (RateLimitExceededException ex)
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many analysis requests", detail: ex.Message);
        }
    }

    /// <summary>The most recently generated analysis for this match, or 404 if Analyze has never been called for it.</summary>
    [HttpGet("{matchId:guid}/analysis")]
    [ProducesResponseType(typeof(PredictionExplanationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalysis(Guid matchId, CancellationToken cancellationToken)
    {
        var result = await _predictionAnalysisService.GetLatestAnalysisForMatchAsync(matchId, cancellationToken);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <summary>Every analysis snapshot generated for this match, most recent first.</summary>
    [HttpGet("{matchId:guid}/analyses")]
    [ProducesResponseType(typeof(IReadOnlyList<PredictionExplanationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalysisHistory(Guid matchId, CancellationToken cancellationToken)
    {
        var result = await _predictionAnalysisService.GetAnalysisHistoryForMatchAsync(matchId, cancellationToken);
        return Ok(result);
    }

    public sealed record LearnFromMatchResultDto(EvaluateMatchResultDto Evaluation, ModelVersionDto NewModelVersion);

    /// <summary>
    /// The full "learn from this match" loop: records the real result against this
    /// match's predictions, recomputes features for the whole tracked competition
    /// (now including this match's result), and retrains — producing a new
    /// ModelVersion that has seen this match. Not online learning: it's a full
    /// retrain, just triggered per-match instead of on a schedule (no scheduler
    /// exists yet — see docs/football-model.md).
    /// </summary>
    [HttpPost("learn-from-match/{matchId:guid}")]
    [ProducesResponseType(typeof(LearnFromMatchResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> LearnFromMatch(Guid matchId, [FromQuery] Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        try
        {
            var evaluation = await _predictionService.EvaluateMatchAsync(matchId, cancellationToken);
            await _datasetBuilderService.BuildFeaturesAsync(trackedCompetitionId, cancellationToken);
            var newModelVersion = await _modelTrainingService.TrainFootball1X2Async(trackedCompetitionId, cancellationToken);

            return Ok(new LearnFromMatchResultDto(evaluation, newModelVersion));
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Cannot learn from this match yet", detail: ex.Message);
        }
        catch (MlServiceException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "ML service request failed", detail: ex.Message);
        }
    }
}

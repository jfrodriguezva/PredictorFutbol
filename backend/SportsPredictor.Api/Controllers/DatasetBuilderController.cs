using Microsoft.AspNetCore.Mvc;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Datasets;

namespace SportsPredictor.Api.Controllers;

/// <summary>
/// Phase 8: the only channel through which the Python ML service consumes data —
/// it never touches SQLite directly (CLAUDE.md section 1, "C# es el orquestador
/// principal").
/// </summary>
[ApiController]
[Route("api/dataset-builder")]
public sealed class DatasetBuilderController : ControllerBase
{
    private readonly IDatasetBuilderService _datasetBuilderService;

    public DatasetBuilderController(IDatasetBuilderService datasetBuilderService)
    {
        _datasetBuilderService = datasetBuilderService;
    }

    [HttpPost("{trackedCompetitionId:guid}/build-features")]
    [ProducesResponseType(typeof(BuildFeaturesResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> BuildFeatures(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _datasetBuilderService.BuildFeaturesAsync(trackedCompetitionId, cancellationToken);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
    }

    [HttpGet("{trackedCompetitionId:guid}/export")]
    public async Task<IActionResult> Export(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        try
        {
            var csv = await _datasetBuilderService.ExportDatasetCsvAsync(trackedCompetitionId, cancellationToken);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"dataset-{trackedCompetitionId}.csv");
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
    }

    /// <summary>Pools every finished match with built features across ALL tracked competitions (every league/cup/season).</summary>
    [HttpGet("export-global")]
    public async Task<IActionResult> ExportGlobal(CancellationToken cancellationToken)
    {
        var csv = await _datasetBuilderService.ExportGlobalDatasetCsvAsync(cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "dataset-global.csv");
    }
}

using Microsoft.AspNetCore.Mvc;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ExternalData;

namespace SportsPredictor.Api.Controllers;

/// <summary>Connection status and manual connection test for external data providers (API-Football, Phase 3).</summary>
[ApiController]
[Route("api/data-sources/api-football")]
public sealed class DataSourcesController : ControllerBase
{
    private readonly IApiFootballClient _apiFootballClient;

    public DataSourcesController(IApiFootballClient apiFootballClient)
    {
        _apiFootballClient = apiFootballClient;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiFootballStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiFootballStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _apiFootballClient.GetStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpPost("test")]
    [ProducesResponseType(typeof(ApiFootballConnectionTestResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _apiFootballClient.TestConnectionAsync(cancellationToken);
            if (!result.Success)
            {
                return Problem(statusCode: StatusCodes.Status502BadGateway, title: "Connection test failed", detail: result.ErrorMessage);
            }

            return Ok(result);
        }
        catch (ApiFootballNotConfiguredException ex)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "API-Football not configured", detail: ex.Message);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.ExternalData;

namespace SportsPredictor.Api.Controllers;

/// <summary>Read-only football reference data sourced from API-Football (Phase 3: countries and leagues only).</summary>
[ApiController]
[Route("api/reference")]
public sealed class ReferenceController : ControllerBase
{
    private readonly IApiFootballClient _apiFootballClient;

    public ReferenceController(IApiFootballClient apiFootballClient)
    {
        _apiFootballClient = apiFootballClient;
    }

    [HttpGet("countries")]
    [ProducesResponseType(typeof(IReadOnlyList<CountryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCountries(CancellationToken cancellationToken)
    {
        try
        {
            var countries = await _apiFootballClient.GetCountriesAsync(cancellationToken);
            return Ok(countries);
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

    [HttpGet("leagues")]
    [ProducesResponseType(typeof(IReadOnlyList<LeagueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeagues(CancellationToken cancellationToken)
    {
        try
        {
            var leagues = await _apiFootballClient.GetLeaguesAsync(cancellationToken);
            return Ok(leagues);
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

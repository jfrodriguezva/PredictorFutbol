using Microsoft.AspNetCore.Mvc;
using SportsPredictor.Application.Common.Interfaces;
using SportsPredictor.Application.Health;

namespace SportsPredictor.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public HealthController(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApplicationHealthInfo), StatusCodes.Status200OK)]
    public ActionResult<ApplicationHealthInfo> Get()
    {
        var version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        var info = new ApplicationHealthInfo("Healthy", _dateTimeProvider.UtcNow, version);
        return Ok(info);
    }
}

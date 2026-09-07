using Microsoft.AspNetCore.Mvc;
using SportsPredictor.Application.Progol;

namespace SportsPredictor.Api.Controllers;

/// <summary>CLAUDE.md section 30. Stateless — no persistence, just an optimization calculation.</summary>
[ApiController]
[Route("api/progol")]
public sealed class ProgolController : ControllerBase
{
    private readonly IProgolOptimizer _optimizer;

    public ProgolController(IProgolOptimizer optimizer)
    {
        _optimizer = optimizer;
    }

    public sealed record OptimizeRequest(
        IReadOnlyList<ProgolMatchInput> Matches,
        int Budget,
        int MaxDoubles,
        int MaxTriples);

    [HttpPost("optimize")]
    [ProducesResponseType(typeof(ProgolOptimizationResult), StatusCodes.Status200OK)]
    public IActionResult Optimize([FromBody] OptimizeRequest request)
    {
        try
        {
            var result = _optimizer.Optimize(request.Matches, request.Budget, request.MaxDoubles, request.MaxTriples);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Invalid Progol request", detail: ex.Message);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Notifications;

namespace SportsPredictor.Api.Controllers;

/// <summary>
/// In-app value-bet feed populated by ValueBetWatcherBackgroundService. No email/push,
/// no per-user scoping — there is no auth system yet, so this is one global feed.
/// </summary>
[ApiController]
[Route("api/notifications/value-bets")]
public sealed class NotificationsController : ControllerBase
{
    private readonly IValueBetNotificationService _notificationService;

    public NotificationsController(IValueBetNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ValueBetNotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] bool unreadOnly, CancellationToken cancellationToken)
    {
        var result = await _notificationService.GetAsync(unreadOnly, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(typeof(ValueBetNotificationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _notificationService.MarkReadAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
    }
}

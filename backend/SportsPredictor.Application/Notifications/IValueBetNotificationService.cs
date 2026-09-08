namespace SportsPredictor.Application.Notifications;

/// <summary>
/// Read/mark-read side of the in-app value-bet feed. Notifications themselves are
/// created by <c>ValueBetWatcherBackgroundService</c> (Infrastructure/Predictions) —
/// no user/auth system exists yet, so this is a single global feed, not per-user.
/// </summary>
public interface IValueBetNotificationService
{
    Task<IReadOnlyList<ValueBetNotificationDto>> GetAsync(bool unreadOnly, CancellationToken cancellationToken);

    /// <summary>Throws NotFoundException if the id doesn't exist.</summary>
    Task<ValueBetNotificationDto> MarkReadAsync(Guid id, CancellationToken cancellationToken);
}

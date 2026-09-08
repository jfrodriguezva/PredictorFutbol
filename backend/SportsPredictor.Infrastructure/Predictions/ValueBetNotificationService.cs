using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Notifications;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.Predictions;

public sealed class ValueBetNotificationService : IValueBetNotificationService
{
    private readonly SportsPredictorDbContext _dbContext;

    public ValueBetNotificationService(SportsPredictorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ValueBetNotificationDto>> GetAsync(bool unreadOnly, CancellationToken cancellationToken)
    {
        var query = _dbContext.ValueBetNotifications.AsQueryable();
        if (unreadOnly)
        {
            query = query.Where(n => !n.Read);
        }

        var rows = await query.OrderByDescending(n => n.DetectedAt).ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ValueBetNotificationDto> MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await _dbContext.ValueBetNotifications.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException(nameof(ValueBetNotification), id);

        notification.Read = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(notification);
    }

    private static ValueBetNotificationDto ToDto(ValueBetNotification n) =>
        new(n.Id, n.MatchId, n.PredictionId, n.Selection, n.ExpectedValue, n.DetectedAt, n.Read);
}

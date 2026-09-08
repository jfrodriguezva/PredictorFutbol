namespace SportsPredictor.Application.Notifications;

public sealed record ValueBetNotificationDto(
    Guid Id,
    Guid MatchId,
    Guid PredictionId,
    string Selection,
    double ExpectedValue,
    DateTime DetectedAt,
    bool Read);

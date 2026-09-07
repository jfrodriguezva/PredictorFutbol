namespace SportsPredictor.Application.ReferenceData;

public sealed record InjurySyncResultDto(int SnapshotsCreated, int TeamsCreated, int PlayersCreated, DateTime CapturedAtUtc);

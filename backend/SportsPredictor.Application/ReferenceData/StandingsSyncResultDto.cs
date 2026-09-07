namespace SportsPredictor.Application.ReferenceData;

public sealed record StandingsSyncResultDto(int SnapshotsCreated, int SkippedUnknownTeam, DateTime CapturedAtUtc);

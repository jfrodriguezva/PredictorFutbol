namespace SportsPredictor.Application.ReferenceData;

public sealed record FixtureSyncResultDto(int FixturesUpserted, int TeamsCreated, int VenuesCreated, int SkippedNoSeasonMatch);

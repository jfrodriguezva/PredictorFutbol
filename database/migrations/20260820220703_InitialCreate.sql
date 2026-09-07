CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "ApiQuotaSnapshots" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ApiQuotaSnapshots" PRIMARY KEY,
    "Provider" TEXT NOT NULL,
    "CapturedAt" TEXT NOT NULL,
    "DailyLimit" INTEGER NULL,
    "DailyRemaining" INTEGER NULL,
    "MinuteLimit" INTEGER NULL,
    "MinuteRemaining" INTEGER NULL
);

CREATE TABLE "ApiRawSnapshots" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ApiRawSnapshots" PRIMARY KEY,
    "Provider" TEXT NOT NULL,
    "Endpoint" TEXT NOT NULL,
    "ExternalEntityId" TEXT NULL,
    "CapturedAt" TEXT NOT NULL,
    "PayloadJson" TEXT NOT NULL
);

CREATE TABLE "ModelVersions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ModelVersions" PRIMARY KEY,
    "ModelName" TEXT NOT NULL,
    "Sport" TEXT NOT NULL,
    "Version" TEXT NOT NULL,
    "Algorithm" TEXT NOT NULL,
    "TrainedAt" TEXT NOT NULL,
    "TrainingStartDate" TEXT NOT NULL,
    "TrainingEndDate" TEXT NOT NULL,
    "BrierScore" REAL NULL,
    "LogLoss" REAL NULL,
    "Accuracy" REAL NULL,
    "Roi" REAL NULL,
    "ArtifactPath" TEXT NOT NULL,
    "Active" INTEGER NOT NULL
);

CREATE TABLE "Sports" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Sports" PRIMARY KEY,
    "Name" TEXT NOT NULL
);

CREATE TABLE "Venues" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Venues" PRIMARY KEY,
    "ExternalApiFootballId" INTEGER NULL,
    "Name" TEXT NOT NULL,
    "City" TEXT NULL,
    "Country" TEXT NULL,
    "AltitudeMeters" INTEGER NULL,
    "Latitude" REAL NULL,
    "Longitude" REAL NULL
);

CREATE TABLE "TrainingRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_TrainingRuns" PRIMARY KEY,
    "ModelVersionId" TEXT NOT NULL,
    "StartedAt" TEXT NOT NULL,
    "FinishedAt" TEXT NULL,
    "DatasetSize" INTEGER NOT NULL,
    "TrainingParametersJson" TEXT NOT NULL,
    "MetricsJson" TEXT NULL,
    CONSTRAINT "FK_TrainingRuns_ModelVersions_ModelVersionId" FOREIGN KEY ("ModelVersionId") REFERENCES "ModelVersions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Competitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Competitions" PRIMARY KEY,
    "SportId" TEXT NOT NULL,
    "ExternalApiFootballId" INTEGER NULL,
    "Name" TEXT NOT NULL,
    "Country" TEXT NULL,
    "CompetitionType" TEXT NOT NULL,
    CONSTRAINT "FK_Competitions_Sports_SportId" FOREIGN KEY ("SportId") REFERENCES "Sports" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Teams" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Teams" PRIMARY KEY,
    "SportId" TEXT NOT NULL,
    "ExternalApiFootballId" INTEGER NULL,
    "Name" TEXT NOT NULL,
    "Country" TEXT NULL,
    CONSTRAINT "FK_Teams_Sports_SportId" FOREIGN KEY ("SportId") REFERENCES "Sports" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Seasons" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Seasons" PRIMARY KEY,
    "CompetitionId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "StartDate" TEXT NOT NULL,
    "EndDate" TEXT NOT NULL,
    CONSTRAINT "FK_Seasons_Competitions_CompetitionId" FOREIGN KEY ("CompetitionId") REFERENCES "Competitions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "TrackedCompetitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_TrackedCompetitions" PRIMARY KEY,
    "CompetitionId" TEXT NOT NULL,
    "ExternalLeagueId" INTEGER NOT NULL,
    "Season" INTEGER NOT NULL,
    "Enabled" INTEGER NOT NULL,
    "Priority" INTEGER NOT NULL,
    "HistoricalSeasonsToImport" INTEGER NOT NULL,
    "SyncFixtures" INTEGER NOT NULL,
    "SyncStandings" INTEGER NOT NULL,
    "SyncPlayers" INTEGER NOT NULL,
    "SyncStatistics" INTEGER NOT NULL,
    "SyncInjuries" INTEGER NOT NULL,
    "SyncOdds" INTEGER NOT NULL,
    "SyncPredictions" INTEGER NOT NULL,
    CONSTRAINT "FK_TrackedCompetitions_Competitions_CompetitionId" FOREIGN KEY ("CompetitionId") REFERENCES "Competitions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Players" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Players" PRIMARY KEY,
    "TeamId" TEXT NOT NULL,
    "ExternalApiFootballId" INTEGER NULL,
    "Name" TEXT NOT NULL,
    "Position" TEXT NOT NULL,
    "BirthDate" TEXT NULL,
    CONSTRAINT "FK_Players_Teams_TeamId" FOREIGN KEY ("TeamId") REFERENCES "Teams" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Matches" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Matches" PRIMARY KEY,
    "ExternalApiFootballId" INTEGER NULL,
    "CompetitionId" TEXT NOT NULL,
    "SeasonId" TEXT NOT NULL,
    "HomeTeamId" TEXT NOT NULL,
    "AwayTeamId" TEXT NOT NULL,
    "MatchDate" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "HomeScore" INTEGER NULL,
    "AwayScore" INTEGER NULL,
    "VenueId" TEXT NULL,
    CONSTRAINT "CK_Matches_HomeAwayDifferent" CHECK ("HomeTeamId" <> "AwayTeamId"),
    CONSTRAINT "FK_Matches_Competitions_CompetitionId" FOREIGN KEY ("CompetitionId") REFERENCES "Competitions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Matches_Seasons_SeasonId" FOREIGN KEY ("SeasonId") REFERENCES "Seasons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Matches_Teams_AwayTeamId" FOREIGN KEY ("AwayTeamId") REFERENCES "Teams" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Matches_Teams_HomeTeamId" FOREIGN KEY ("HomeTeamId") REFERENCES "Teams" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Matches_Venues_VenueId" FOREIGN KEY ("VenueId") REFERENCES "Venues" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "StandingSnapshots" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_StandingSnapshots" PRIMARY KEY,
    "CompetitionId" TEXT NOT NULL,
    "SeasonId" TEXT NOT NULL,
    "TeamId" TEXT NOT NULL,
    "CapturedAt" TEXT NOT NULL,
    "Rank" INTEGER NOT NULL,
    "Points" INTEGER NOT NULL,
    "Played" INTEGER NOT NULL,
    "Wins" INTEGER NOT NULL,
    "Draws" INTEGER NOT NULL,
    "Losses" INTEGER NOT NULL,
    "GoalsFor" INTEGER NOT NULL,
    "GoalsAgainst" INTEGER NOT NULL,
    "GoalDifference" INTEGER NOT NULL,
    "Form" TEXT NULL,
    CONSTRAINT "FK_StandingSnapshots_Competitions_CompetitionId" FOREIGN KEY ("CompetitionId") REFERENCES "Competitions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_StandingSnapshots_Seasons_SeasonId" FOREIGN KEY ("SeasonId") REFERENCES "Seasons" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_StandingSnapshots_Teams_TeamId" FOREIGN KEY ("TeamId") REFERENCES "Teams" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "DataAvailabilities" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_DataAvailabilities" PRIMARY KEY,
    "MatchId" TEXT NOT NULL,
    "HasEvents" INTEGER NOT NULL,
    "HasLineups" INTEGER NOT NULL,
    "HasStatistics" INTEGER NOT NULL,
    "HasPlayerStatistics" INTEGER NOT NULL,
    "HasInjuries" INTEGER NOT NULL,
    "HasOdds" INTEGER NOT NULL,
    "HasPredictions" INTEGER NOT NULL,
    "CapturedAt" TEXT NOT NULL,
    CONSTRAINT "FK_DataAvailabilities_Matches_MatchId" FOREIGN KEY ("MatchId") REFERENCES "Matches" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "FeatureValues" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_FeatureValues" PRIMARY KEY,
    "MatchId" TEXT NOT NULL,
    "FeatureName" TEXT NOT NULL,
    "NumericValue" REAL NOT NULL,
    "AvailableAt" TEXT NOT NULL,
    "CapturedAt" TEXT NOT NULL,
    CONSTRAINT "FK_FeatureValues_Matches_MatchId" FOREIGN KEY ("MatchId") REFERENCES "Matches" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "OddsSnapshots" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_OddsSnapshots" PRIMARY KEY,
    "MatchId" TEXT NOT NULL,
    "Sportsbook" TEXT NOT NULL,
    "Market" TEXT NOT NULL,
    "Selection" TEXT NOT NULL,
    "AmericanOdds" INTEGER NULL,
    "DecimalOdds" TEXT NOT NULL,
    "ImpliedProbability" REAL NOT NULL,
    "CapturedAt" TEXT NOT NULL,
    CONSTRAINT "FK_OddsSnapshots_Matches_MatchId" FOREIGN KEY ("MatchId") REFERENCES "Matches" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Predictions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Predictions" PRIMARY KEY,
    "MatchId" TEXT NOT NULL,
    "ModelVersionId" TEXT NOT NULL,
    "PredictionDate" TEXT NOT NULL,
    "Market" TEXT NOT NULL,
    "Selection" TEXT NOT NULL,
    "Probability" REAL NOT NULL,
    "ExpectedValue" REAL NULL,
    "Recommended" INTEGER NOT NULL,
    "ActualOutcome" TEXT NULL,
    "IsCorrect" INTEGER NULL,
    CONSTRAINT "FK_Predictions_Matches_MatchId" FOREIGN KEY ("MatchId") REFERENCES "Matches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Predictions_ModelVersions_ModelVersionId" FOREIGN KEY ("ModelVersionId") REFERENCES "ModelVersions" ("Id") ON DELETE RESTRICT
);

INSERT INTO "Sports" ("Id", "Name")
VALUES ('00000000-0000-0000-0000-000000000001', 'Football');
SELECT changes();


CREATE INDEX "IX_ApiQuotaSnapshots_Provider_CapturedAt" ON "ApiQuotaSnapshots" ("Provider", "CapturedAt");

CREATE INDEX "IX_ApiRawSnapshots_Provider_Endpoint_ExternalEntityId_CapturedAt" ON "ApiRawSnapshots" ("Provider", "Endpoint", "ExternalEntityId", "CapturedAt");

CREATE UNIQUE INDEX "IX_Competitions_ExternalApiFootballId" ON "Competitions" ("ExternalApiFootballId") WHERE "ExternalApiFootballId" IS NOT NULL;

CREATE INDEX "IX_Competitions_SportId" ON "Competitions" ("SportId");

CREATE UNIQUE INDEX "IX_DataAvailabilities_MatchId" ON "DataAvailabilities" ("MatchId");

CREATE INDEX "IX_FeatureValues_AvailableAt" ON "FeatureValues" ("AvailableAt");

CREATE INDEX "IX_FeatureValues_MatchId_FeatureName_CapturedAt" ON "FeatureValues" ("MatchId", "FeatureName", "CapturedAt");

CREATE INDEX "IX_Matches_AwayTeamId" ON "Matches" ("AwayTeamId");

CREATE INDEX "IX_Matches_CompetitionId" ON "Matches" ("CompetitionId");

CREATE UNIQUE INDEX "IX_Matches_ExternalApiFootballId" ON "Matches" ("ExternalApiFootballId") WHERE "ExternalApiFootballId" IS NOT NULL;

CREATE INDEX "IX_Matches_HomeTeamId" ON "Matches" ("HomeTeamId");

CREATE INDEX "IX_Matches_MatchDate" ON "Matches" ("MatchDate");

CREATE INDEX "IX_Matches_SeasonId_CompetitionId" ON "Matches" ("SeasonId", "CompetitionId");

CREATE INDEX "IX_Matches_VenueId" ON "Matches" ("VenueId");

CREATE UNIQUE INDEX "IX_ModelVersions_ModelName_Version" ON "ModelVersions" ("ModelName", "Version");

CREATE INDEX "IX_OddsSnapshots_MatchId_Sportsbook_Market_CapturedAt" ON "OddsSnapshots" ("MatchId", "Sportsbook", "Market", "CapturedAt");

CREATE UNIQUE INDEX "IX_Players_ExternalApiFootballId" ON "Players" ("ExternalApiFootballId") WHERE "ExternalApiFootballId" IS NOT NULL;

CREATE INDEX "IX_Players_TeamId" ON "Players" ("TeamId");

CREATE INDEX "IX_Predictions_MatchId_ModelVersionId_Market_PredictionDate" ON "Predictions" ("MatchId", "ModelVersionId", "Market", "PredictionDate");

CREATE INDEX "IX_Predictions_ModelVersionId" ON "Predictions" ("ModelVersionId");

CREATE UNIQUE INDEX "IX_Seasons_CompetitionId_Name" ON "Seasons" ("CompetitionId", "Name");

CREATE UNIQUE INDEX "IX_Sports_Name" ON "Sports" ("Name");

CREATE INDEX "IX_StandingSnapshots_CompetitionId" ON "StandingSnapshots" ("CompetitionId");

CREATE INDEX "IX_StandingSnapshots_SeasonId_TeamId_CapturedAt" ON "StandingSnapshots" ("SeasonId", "TeamId", "CapturedAt");

CREATE INDEX "IX_StandingSnapshots_TeamId" ON "StandingSnapshots" ("TeamId");

CREATE UNIQUE INDEX "IX_Teams_ExternalApiFootballId" ON "Teams" ("ExternalApiFootballId") WHERE "ExternalApiFootballId" IS NOT NULL;

CREATE INDEX "IX_Teams_SportId" ON "Teams" ("SportId");

CREATE UNIQUE INDEX "IX_TrackedCompetitions_CompetitionId_Season" ON "TrackedCompetitions" ("CompetitionId", "Season");

CREATE INDEX "IX_TrackedCompetitions_ExternalLeagueId" ON "TrackedCompetitions" ("ExternalLeagueId");

CREATE INDEX "IX_TrainingRuns_ModelVersionId" ON "TrainingRuns" ("ModelVersionId");

CREATE UNIQUE INDEX "IX_Venues_ExternalApiFootballId" ON "Venues" ("ExternalApiFootballId") WHERE "ExternalApiFootballId" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260820220703_InitialCreate', '10.0.11');

COMMIT;


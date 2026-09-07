BEGIN TRANSACTION;
CREATE TABLE "ApiFootballPredictionSnapshots" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ApiFootballPredictionSnapshots" PRIMARY KEY,
    "MatchId" TEXT NOT NULL,
    "CapturedAt" TEXT NOT NULL,
    "HomeProbability" REAL NOT NULL,
    "DrawProbability" REAL NOT NULL,
    "AwayProbability" REAL NOT NULL,
    "PredictedWinner" TEXT NULL,
    "PredictedScore" TEXT NULL,
    "RawJson" TEXT NOT NULL,
    CONSTRAINT "FK_ApiFootballPredictionSnapshots_Matches_MatchId" FOREIGN KEY ("MatchId") REFERENCES "Matches" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_ApiFootballPredictionSnapshots_MatchId_CapturedAt" ON "ApiFootballPredictionSnapshots" ("MatchId", "CapturedAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260821025325_AddApiFootballPredictionSnapshot', '10.0.11');

COMMIT;


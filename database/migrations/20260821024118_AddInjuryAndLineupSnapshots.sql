BEGIN TRANSACTION;
CREATE TABLE "InjurySnapshots" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_InjurySnapshots" PRIMARY KEY,
    "PlayerId" TEXT NOT NULL,
    "TeamId" TEXT NOT NULL,
    "MatchId" TEXT NULL,
    "Type" TEXT NOT NULL,
    "Reason" TEXT NULL,
    "StartDate" TEXT NULL,
    "ExpectedReturn" TEXT NULL,
    "CapturedAt" TEXT NOT NULL,
    "Source" TEXT NOT NULL,
    CONSTRAINT "FK_InjurySnapshots_Matches_MatchId" FOREIGN KEY ("MatchId") REFERENCES "Matches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_InjurySnapshots_Players_PlayerId" FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_InjurySnapshots_Teams_TeamId" FOREIGN KEY ("TeamId") REFERENCES "Teams" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "LineupSnapshots" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_LineupSnapshots" PRIMARY KEY,
    "MatchId" TEXT NOT NULL,
    "TeamId" TEXT NOT NULL,
    "Formation" TEXT NOT NULL,
    "ExternalCoachId" INTEGER NULL,
    "CoachName" TEXT NULL,
    "CapturedAt" TEXT NOT NULL,
    CONSTRAINT "FK_LineupSnapshots_Matches_MatchId" FOREIGN KEY ("MatchId") REFERENCES "Matches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_LineupSnapshots_Teams_TeamId" FOREIGN KEY ("TeamId") REFERENCES "Teams" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_InjurySnapshots_MatchId" ON "InjurySnapshots" ("MatchId");

CREATE INDEX "IX_InjurySnapshots_PlayerId_CapturedAt" ON "InjurySnapshots" ("PlayerId", "CapturedAt");

CREATE INDEX "IX_InjurySnapshots_TeamId" ON "InjurySnapshots" ("TeamId");

CREATE INDEX "IX_LineupSnapshots_MatchId_TeamId_CapturedAt" ON "LineupSnapshots" ("MatchId", "TeamId", "CapturedAt");

CREATE INDEX "IX_LineupSnapshots_TeamId" ON "LineupSnapshots" ("TeamId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260821024118_AddInjuryAndLineupSnapshots', '10.0.11');

COMMIT;


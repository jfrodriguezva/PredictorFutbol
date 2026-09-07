using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPredictor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiQuotaSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DailyLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    DailyRemaining = table.Column<int>(type: "INTEGER", nullable: true),
                    MinuteLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    MinuteRemaining = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiQuotaSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiRawSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ExternalEntityId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiRawSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Sport = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Algorithm = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TrainedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TrainingStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TrainingEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BrierScore = table.Column<double>(type: "REAL", nullable: true),
                    LogLoss = table.Column<double>(type: "REAL", nullable: true),
                    Accuracy = table.Column<double>(type: "REAL", nullable: true),
                    Roi = table.Column<double>(type: "REAL", nullable: true),
                    ArtifactPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalApiFootballId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AltitudeMeters = table.Column<int>(type: "INTEGER", nullable: true),
                    Latitude = table.Column<double>(type: "REAL", nullable: true),
                    Longitude = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DatasetSize = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainingParametersJson = table.Column<string>(type: "TEXT", nullable: false),
                    MetricsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingRuns_ModelVersions_ModelVersionId",
                        column: x => x.ModelVersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Competitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalApiFootballId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CompetitionType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Competitions_Sports_SportId",
                        column: x => x.SportId,
                        principalTable: "Sports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalApiFootballId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Sports_SportId",
                        column: x => x.SportId,
                        principalTable: "Sports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Seasons_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrackedCompetitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalLeagueId = table.Column<int>(type: "INTEGER", nullable: false),
                    Season = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    HistoricalSeasonsToImport = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncFixtures = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncStandings = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncPlayers = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncStatistics = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncInjuries = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncOdds = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncPredictions = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedCompetitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackedCompetitions_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalApiFootballId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Position = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Players_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalApiFootballId = table.Column<int>(type: "INTEGER", nullable: true),
                    CompetitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeasonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HomeTeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AwayTeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    HomeScore = table.Column<int>(type: "INTEGER", nullable: true),
                    AwayScore = table.Column<int>(type: "INTEGER", nullable: true),
                    VenueId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.CheckConstraint("CK_Matches_HomeAwayDifferent", "\"HomeTeamId\" <> \"AwayTeamId\"");
                    table.ForeignKey(
                        name: "FK_Matches_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StandingSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompetitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeasonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    Played = table.Column<int>(type: "INTEGER", nullable: false),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false),
                    Draws = table.Column<int>(type: "INTEGER", nullable: false),
                    Losses = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsFor = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalDifference = table.Column<int>(type: "INTEGER", nullable: false),
                    Form = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandingSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandingSnapshots_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StandingSnapshots_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StandingSnapshots_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DataAvailabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HasEvents = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasLineups = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasStatistics = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasPlayerStatistics = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasInjuries = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasOdds = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasPredictions = table.Column<bool>(type: "INTEGER", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataAvailabilities_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeatureValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FeatureName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NumericValue = table.Column<double>(type: "REAL", nullable: false),
                    AvailableAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureValues_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OddsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sportsbook = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Market = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Selection = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AmericanOdds = table.Column<int>(type: "INTEGER", nullable: true),
                    DecimalOdds = table.Column<decimal>(type: "TEXT", nullable: false),
                    ImpliedProbability = table.Column<double>(type: "REAL", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OddsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OddsSnapshots_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PredictionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Market = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Selection = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Probability = table.Column<double>(type: "REAL", nullable: false),
                    ExpectedValue = table.Column<double>(type: "REAL", nullable: true),
                    Recommended = table.Column<bool>(type: "INTEGER", nullable: false),
                    ActualOutcome = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsCorrect = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Predictions_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Predictions_ModelVersions_ModelVersionId",
                        column: x => x.ModelVersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Sports",
                columns: new[] { "Id", "Name" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "Football" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiQuotaSnapshots_Provider_CapturedAt",
                table: "ApiQuotaSnapshots",
                columns: new[] { "Provider", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRawSnapshots_Provider_Endpoint_ExternalEntityId_CapturedAt",
                table: "ApiRawSnapshots",
                columns: new[] { "Provider", "Endpoint", "ExternalEntityId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_ExternalApiFootballId",
                table: "Competitions",
                column: "ExternalApiFootballId",
                unique: true,
                filter: "\"ExternalApiFootballId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_SportId",
                table: "Competitions",
                column: "SportId");

            migrationBuilder.CreateIndex(
                name: "IX_DataAvailabilities_MatchId",
                table: "DataAvailabilities",
                column: "MatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureValues_AvailableAt",
                table: "FeatureValues",
                column: "AvailableAt");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureValues_MatchId_FeatureName_CapturedAt",
                table: "FeatureValues",
                columns: new[] { "MatchId", "FeatureName", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_AwayTeamId",
                table: "Matches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_CompetitionId",
                table: "Matches",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_ExternalApiFootballId",
                table: "Matches",
                column: "ExternalApiFootballId",
                unique: true,
                filter: "\"ExternalApiFootballId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_HomeTeamId",
                table: "Matches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_MatchDate",
                table: "Matches",
                column: "MatchDate");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_SeasonId_CompetitionId",
                table: "Matches",
                columns: new[] { "SeasonId", "CompetitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_VenueId",
                table: "Matches",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ModelName_Version",
                table: "ModelVersions",
                columns: new[] { "ModelName", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OddsSnapshots_MatchId_Sportsbook_Market_CapturedAt",
                table: "OddsSnapshots",
                columns: new[] { "MatchId", "Sportsbook", "Market", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_ExternalApiFootballId",
                table: "Players",
                column: "ExternalApiFootballId",
                unique: true,
                filter: "\"ExternalApiFootballId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Players_TeamId",
                table: "Players",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_MatchId_ModelVersionId_Market_PredictionDate",
                table: "Predictions",
                columns: new[] { "MatchId", "ModelVersionId", "Market", "PredictionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_ModelVersionId",
                table: "Predictions",
                column: "ModelVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Seasons_CompetitionId_Name",
                table: "Seasons",
                columns: new[] { "CompetitionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sports_Name",
                table: "Sports",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandingSnapshots_CompetitionId",
                table: "StandingSnapshots",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_StandingSnapshots_SeasonId_TeamId_CapturedAt",
                table: "StandingSnapshots",
                columns: new[] { "SeasonId", "TeamId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StandingSnapshots_TeamId",
                table: "StandingSnapshots",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ExternalApiFootballId",
                table: "Teams",
                column: "ExternalApiFootballId",
                unique: true,
                filter: "\"ExternalApiFootballId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_SportId",
                table: "Teams",
                column: "SportId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedCompetitions_CompetitionId_Season",
                table: "TrackedCompetitions",
                columns: new[] { "CompetitionId", "Season" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackedCompetitions_ExternalLeagueId",
                table: "TrackedCompetitions",
                column: "ExternalLeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingRuns_ModelVersionId",
                table: "TrainingRuns",
                column: "ModelVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Venues_ExternalApiFootballId",
                table: "Venues",
                column: "ExternalApiFootballId",
                unique: true,
                filter: "\"ExternalApiFootballId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiQuotaSnapshots");

            migrationBuilder.DropTable(
                name: "ApiRawSnapshots");

            migrationBuilder.DropTable(
                name: "DataAvailabilities");

            migrationBuilder.DropTable(
                name: "FeatureValues");

            migrationBuilder.DropTable(
                name: "OddsSnapshots");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Predictions");

            migrationBuilder.DropTable(
                name: "StandingSnapshots");

            migrationBuilder.DropTable(
                name: "TrackedCompetitions");

            migrationBuilder.DropTable(
                name: "TrainingRuns");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "ModelVersions");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Venues");

            migrationBuilder.DropTable(
                name: "Competitions");

            migrationBuilder.DropTable(
                name: "Sports");
        }
    }
}

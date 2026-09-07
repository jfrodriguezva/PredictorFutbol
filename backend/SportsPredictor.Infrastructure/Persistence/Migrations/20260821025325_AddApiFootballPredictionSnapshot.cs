using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPredictor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApiFootballPredictionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiFootballPredictionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HomeProbability = table.Column<double>(type: "REAL", nullable: false),
                    DrawProbability = table.Column<double>(type: "REAL", nullable: false),
                    AwayProbability = table.Column<double>(type: "REAL", nullable: false),
                    PredictedWinner = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PredictedScore = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RawJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiFootballPredictionSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiFootballPredictionSnapshots_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiFootballPredictionSnapshots_MatchId_CapturedAt",
                table: "ApiFootballPredictionSnapshots",
                columns: new[] { "MatchId", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiFootballPredictionSnapshots");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPredictor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddValueBetNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValueBetNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PredictionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Selection = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedValue = table.Column<double>(type: "REAL", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Read = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValueBetNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValueBetNotifications_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ValueBetNotifications_Predictions_PredictionId",
                        column: x => x.PredictionId,
                        principalTable: "Predictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValueBetNotifications_MatchId",
                table: "ValueBetNotifications",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ValueBetNotifications_PredictionId",
                table: "ValueBetNotifications",
                column: "PredictionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValueBetNotifications");
        }
    }
}

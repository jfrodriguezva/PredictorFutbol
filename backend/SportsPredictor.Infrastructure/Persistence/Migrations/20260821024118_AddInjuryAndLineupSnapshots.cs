using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPredictor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInjuryAndLineupSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InjurySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ExpectedReturn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InjurySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InjurySnapshots_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InjurySnapshots_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InjurySnapshots_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineupSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Formation = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ExternalCoachId = table.Column<int>(type: "INTEGER", nullable: true),
                    CoachName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineupSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineupSnapshots_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineupSnapshots_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InjurySnapshots_MatchId",
                table: "InjurySnapshots",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InjurySnapshots_PlayerId_CapturedAt",
                table: "InjurySnapshots",
                columns: new[] { "PlayerId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InjurySnapshots_TeamId",
                table: "InjurySnapshots",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_LineupSnapshots_MatchId_TeamId_CapturedAt",
                table: "LineupSnapshots",
                columns: new[] { "MatchId", "TeamId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LineupSnapshots_TeamId",
                table: "LineupSnapshots",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InjurySnapshots");

            migrationBuilder.DropTable(
                name: "LineupSnapshots");
        }
    }
}

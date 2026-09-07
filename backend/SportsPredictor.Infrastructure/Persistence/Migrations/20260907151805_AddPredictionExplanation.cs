using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsPredictor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictionExplanation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PredictionExplanations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProbabilityHome = table.Column<double>(type: "REAL", nullable: false),
                    ProbabilityDraw = table.Column<double>(type: "REAL", nullable: false),
                    ProbabilityAway = table.Column<double>(type: "REAL", nullable: false),
                    ShapTopFeaturesJson = table.Column<string>(type: "TEXT", nullable: false),
                    StakesJson = table.Column<string>(type: "TEXT", nullable: true),
                    NarrativeText = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionExplanations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PredictionExplanations_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PredictionExplanations_ModelVersions_ModelVersionId",
                        column: x => x.ModelVersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PredictionExplanations_MatchId",
                table: "PredictionExplanations",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionExplanations_ModelVersionId",
                table: "PredictionExplanations",
                column: "ModelVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PredictionExplanations");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorApplicationReviewDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorApplicationReviewDecisions",
                columns: table => new
                {
                    TutorApplicationReviewDecisionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    ReviewerBcUserId = table.Column<int>(type: "int", nullable: false),
                    PreviousStage = table.Column<int>(type: "int", nullable: false),
                    NewStage = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorApplicationReviewDecisions", x => x.TutorApplicationReviewDecisionId);
                    table.ForeignKey(
                        name: "FK_TutorApplicationReviewDecisions_BcUsers_ReviewerBcUserId",
                        column: x => x.ReviewerBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorApplicationReviewDecisions_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorApplicationReviewDecisions_ReviewerBcUserId",
                table: "TutorApplicationReviewDecisions",
                column: "ReviewerBcUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorApplicationReviewDecisions_TutorId_ReviewedAt",
                table: "TutorApplicationReviewDecisions",
                columns: new[] { "TutorId", "ReviewedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorApplicationReviewDecisions");
        }
    }
}

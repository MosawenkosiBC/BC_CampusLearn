using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackSurveyResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedbackSurveyResponses",
                columns: table => new
                {
                    FeedbackSurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BcUserId = table.Column<int>(type: "int", nullable: false),
                    SurveyRevision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AnswersJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackSurveyResponses", x => new { x.FeedbackSurveyId, x.BcUserId });
                    table.ForeignKey(
                        name: "FK_FeedbackSurveyResponses_BcUsers_BcUserId",
                        column: x => x.BcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeedbackSurveyResponses_FeedbackSurveys_FeedbackSurveyId",
                        column: x => x.FeedbackSurveyId,
                        principalTable: "FeedbackSurveys",
                        principalColumn: "FeedbackSurveyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackSurveyResponses_BcUserId",
                table: "FeedbackSurveyResponses",
                column: "BcUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackSurveyResponses");
        }
    }
}

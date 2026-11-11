using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackSurveys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedbackSurveys",
                columns: table => new
                {
                    FeedbackSurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    QuestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackSurveys", x => x.FeedbackSurveyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackSurveys_Audience_Title",
                table: "FeedbackSurveys",
                columns: new[] { "Audience", "Title" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackSurveys");
        }
    }
}

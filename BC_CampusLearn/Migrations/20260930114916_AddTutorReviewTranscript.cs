using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorReviewTranscript : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TranscriptContentType",
                table: "TutorStudentEvaluations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscriptOriginalFileName",
                table: "TutorStudentEvaluations",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TranscriptSizeBytes",
                table: "TutorStudentEvaluations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscriptStoragePath",
                table: "TutorStudentEvaluations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TranscriptContentType",
                table: "TutorStudentEvaluations");

            migrationBuilder.DropColumn(
                name: "TranscriptOriginalFileName",
                table: "TutorStudentEvaluations");

            migrationBuilder.DropColumn(
                name: "TranscriptSizeBytes",
                table: "TutorStudentEvaluations");

            migrationBuilder.DropColumn(
                name: "TranscriptStoragePath",
                table: "TutorStudentEvaluations");
        }
    }
}

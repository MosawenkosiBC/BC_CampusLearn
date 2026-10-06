using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedTutorHeadReviewColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceConsistency",
                table: "SessionReviews");

            migrationBuilder.DropColumn(
                name: "ExplanationClarity",
                table: "SessionReviews");

            migrationBuilder.DropColumn(
                name: "ModuleAndTopicCoverage",
                table: "SessionReviews");

            migrationBuilder.DropColumn(
                name: "SessionStructure",
                table: "SessionReviews");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceConsistency",
                table: "SessionReviews",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExplanationClarity",
                table: "SessionReviews",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModuleAndTopicCoverage",
                table: "SessionReviews",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionStructure",
                table: "SessionReviews",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyAdminReviewQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllReviewsSubmitted",
                table: "AdminSessionReview");

            migrationBuilder.RenameColumn(
                name: "HeadConfirmedSession",
                table: "AdminSessionReview",
                newName: "ReviewEvidenceIsConsistent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReviewEvidenceIsConsistent",
                table: "AdminSessionReview",
                newName: "HeadConfirmedSession");

            migrationBuilder.AddColumn<bool>(
                name: "AllReviewsSubmitted",
                table: "AdminSessionReview",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}

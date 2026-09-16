using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminNameToTutorApplicationReviewDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminName",
                table: "TutorApplicationReviewDecisions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE decisions
                SET decisions.AdminName = COALESCE(
                    NULLIF(LTRIM(RTRIM(users.DisplayName)), N''),
                    users.PersonnelNumber,
                    N'Administrator')
                FROM TutorApplicationReviewDecisions AS decisions
                INNER JOIN BcUsers AS users
                    ON users.BcUserId = decisions.ReviewerBcUserId;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "AdminName",
                table: "TutorApplicationReviewDecisions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminName",
                table: "TutorApplicationReviewDecisions");
        }
    }
}

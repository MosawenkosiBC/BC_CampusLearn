using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorHeadReviewDeadlineRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTutorHeadReviewDeadlineRecurring",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "PlatformSettings",
                keyColumn: "PlatformSettingsId",
                keyValue: 1,
                column: "IsTutorHeadReviewDeadlineRecurring",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTutorHeadReviewDeadlineRecurring",
                table: "PlatformSettings");
        }
    }
}

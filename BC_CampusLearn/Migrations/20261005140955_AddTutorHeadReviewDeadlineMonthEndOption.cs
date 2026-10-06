using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorHeadReviewDeadlineMonthEndOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UseLastDayOfMonthForTutorHeadReviewDeadline",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "PlatformSettings",
                keyColumn: "PlatformSettingsId",
                keyValue: 1,
                column: "UseLastDayOfMonthForTutorHeadReviewDeadline",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseLastDayOfMonthForTutorHeadReviewDeadline",
                table: "PlatformSettings");
        }
    }
}

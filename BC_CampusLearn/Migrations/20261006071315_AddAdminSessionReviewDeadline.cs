using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSessionReviewDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AdminSessionReviewDeadline",
                table: "PlatformSettings",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<bool>(
                name: "IsAdminSessionReviewDeadlineRecurring",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseLastDayOfMonthForAdminSessionReviewDeadline",
                table: "PlatformSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "PlatformSettings",
                keyColumn: "PlatformSettingsId",
                keyValue: 1,
                columns: new[] { "AdminSessionReviewDeadline", "IsAdminSessionReviewDeadlineRecurring", "UseLastDayOfMonthForAdminSessionReviewDeadline" },
                values: new object[] { new DateOnly(2026, 10, 10), true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminSessionReviewDeadline",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "IsAdminSessionReviewDeadlineRecurring",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "UseLastDayOfMonthForAdminSessionReviewDeadline",
                table: "PlatformSettings");
        }
    }
}

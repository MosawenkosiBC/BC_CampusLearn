using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorHeadReviewPeriodSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "TutorHeadReviewDeadline",
                table: "PlatformSettings",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2026, 10, 5));

            migrationBuilder.AddColumn<DateOnly>(
                name: "TutorHeadReviewPeriodEndDate",
                table: "PlatformSettings",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2026, 9, 30));

            migrationBuilder.AddColumn<DateOnly>(
                name: "TutorHeadReviewPeriodStartDate",
                table: "PlatformSettings",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2026, 9, 1));

            migrationBuilder.UpdateData(
                table: "PlatformSettings",
                keyColumn: "PlatformSettingsId",
                keyValue: 1,
                columns: new[] { "TutorHeadReviewDeadline", "TutorHeadReviewPeriodEndDate", "TutorHeadReviewPeriodStartDate" },
                values: new object[] { new DateOnly(2026, 10, 5), new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1) });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlatformSettings_TutorHeadReviewPeriod",
                table: "PlatformSettings",
                sql: "[TutorHeadReviewPeriodEndDate] >= [TutorHeadReviewPeriodStartDate] AND [TutorHeadReviewDeadline] >= [TutorHeadReviewPeriodEndDate]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlatformSettings_TutorHeadReviewPeriod",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "TutorHeadReviewDeadline",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "TutorHeadReviewPeriodEndDate",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "TutorHeadReviewPeriodStartDate",
                table: "PlatformSettings");
        }
    }
}

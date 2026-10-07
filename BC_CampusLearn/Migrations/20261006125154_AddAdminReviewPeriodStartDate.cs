using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminReviewPeriodStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AdminSessionReviewPeriodStartDate",
                table: "PlatformSettings",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [PlatformSettings] " +
                "SET [AdminSessionReviewPeriodStartDate] = " +
                "DATEADD(day, 1, DATEADD(month, -1, " +
                "[AdminSessionReviewDeadline]))");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "AdminSessionReviewPeriodStartDate",
                table: "PlatformSettings",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlatformSettings_AdminSessionReviewPeriod",
                table: "PlatformSettings",
                sql: "[AdminSessionReviewDeadline] >= [AdminSessionReviewPeriodStartDate]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlatformSettings_AdminSessionReviewPeriod",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "AdminSessionReviewPeriodStartDate",
                table: "PlatformSettings");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class DefaultAdminReviewDeadlineToMonthEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [PlatformSettings] SET " +
                "[AdminSessionReviewDeadline] = " +
                "EOMONTH([AdminSessionReviewDeadline]), " +
                "[AdminSessionReviewPeriodStartDate] = CASE " +
                "WHEN [AdminSessionReviewPeriodStartDate] = '2026-09-11' " +
                "THEN '2026-09-06' " +
                "ELSE [AdminSessionReviewPeriodStartDate] END, " +
                "[UseLastDayOfMonthForAdminSessionReviewDeadline] = 1 " +
                "WHERE [PlatformSettingsId] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [PlatformSettings] SET " +
                "[AdminSessionReviewPeriodStartDate] = CASE " +
                "WHEN [AdminSessionReviewPeriodStartDate] = '2026-09-06' " +
                "THEN '2026-09-11' " +
                "ELSE [AdminSessionReviewPeriodStartDate] END, " +
                "[UseLastDayOfMonthForAdminSessionReviewDeadline] = 0 " +
                "WHERE [PlatformSettingsId] = 1");
        }
    }
}

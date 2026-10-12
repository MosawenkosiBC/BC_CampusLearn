using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddSeniorTutorApplicationSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SeniorTutorCloseDate",
                table: "TutorApplicationSettings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SeniorTutorOpenDate",
                table: "TutorApplicationSettings",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TutorApplicationSettings",
                keyColumn: "TutorApplicationSettingsId",
                keyValue: 1,
                columns: new[] { "SeniorTutorCloseDate", "SeniorTutorOpenDate" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeniorTutorCloseDate",
                table: "TutorApplicationSettings");

            migrationBuilder.DropColumn(
                name: "SeniorTutorOpenDate",
                table: "TutorApplicationSettings");
        }
    }
}

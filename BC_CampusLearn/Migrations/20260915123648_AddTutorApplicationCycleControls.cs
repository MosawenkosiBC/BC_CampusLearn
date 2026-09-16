using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorApplicationCycleControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CloseDate",
                table: "TutorApplicationSettings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContinueAfterShortlistLimit",
                table: "TutorApplicationSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenDate",
                table: "TutorApplicationSettings",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "TutorApplicationSettings",
                keyColumn: "TutorApplicationSettingsId",
                keyValue: 1,
                columns: new[] { "CloseDate", "OpenDate" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloseDate",
                table: "TutorApplicationSettings");

            migrationBuilder.DropColumn(
                name: "ContinueAfterShortlistLimit",
                table: "TutorApplicationSettings");

            migrationBuilder.DropColumn(
                name: "OpenDate",
                table: "TutorApplicationSettings");
        }
    }
}

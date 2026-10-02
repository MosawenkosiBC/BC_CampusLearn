using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyAreaDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "StudyAreas",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 1,
                column: "Description",
                value: null);

            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 2,
                column: "Description",
                value: "Located next to the Chi classroom.");

            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 3,
                column: "Description",
                value: "Located next to the Pi classroom on Main Campus.");

            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 4,
                column: "Description",
                value: "Located at the Waterloop residence.");

            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 5,
                columns: new[] { "Description", "Name" },
                values: new object[] { "Located at the Ostend residence.", "Florenville" });

            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 6,
                column: "Description",
                value: "Located at the West Campus residence.");

            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 7,
                column: "Description",
                value: null);

            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 8,
                column: "Description",
                value: "Located next to the Academia building.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "StudyAreas");

            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 5,
                column: "Name",
                value: "Flourrenville");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFlorenvilleDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 5,
                column: "Description",
                value: "Located at the Florenville residence.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StudyAreas",
                keyColumn: "StudyAreaId",
                keyValue: 5,
                column: "Description",
                value: "Located at the Ostend residence.");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StudyAreaId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudyAreas",
                columns: table => new
                {
                    StudyAreaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CampusName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyAreas", x => x.StudyAreaId);
                });

            migrationBuilder.InsertData(
                table: "StudyAreas",
                columns: new[] { "StudyAreaId", "CampusName", "DisplayOrder", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, null, 1, true, "Online" },
                    { 2, "Pretoria Campus", 2, true, "Chi study" },
                    { 3, "Pretoria Campus", 3, true, "Rou" },
                    { 4, "Pretoria Campus", 4, true, "Waterloop" },
                    { 5, "Pretoria Campus", 5, true, "Flourrenville" },
                    { 6, "Pretoria Campus", 6, true, "West Campus" },
                    { 7, "Pretoria Campus", 7, true, "Brugge" },
                    { 8, "Pretoria Campus", 8, true, "Main Library & Study area" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_StudyAreaId",
                table: "Bookings",
                column: "StudyAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAreas_Name",
                table: "StudyAreas",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_StudyAreas_StudyAreaId",
                table: "Bookings",
                column: "StudyAreaId",
                principalTable: "StudyAreas",
                principalColumn: "StudyAreaId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_StudyAreas_StudyAreaId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "StudyAreas");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_StudyAreaId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "StudyAreaId",
                table: "Bookings");
        }
    }
}

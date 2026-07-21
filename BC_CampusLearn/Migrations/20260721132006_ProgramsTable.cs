using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class ProgramsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgrammeOfStudy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgrammeOfStudy", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ProgrammeOfStudy",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Bachelor of Computing" },
                    { 2, "Bachelor of Information Technology" },
                    { 3, "Diploma in Information Technology" },
                    { 4, "Diploma for Deaf Students" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeOfStudy_Name",
                table: "ProgrammeOfStudy",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgrammeOfStudy");
        }
    }
}

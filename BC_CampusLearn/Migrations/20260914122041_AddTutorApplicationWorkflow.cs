using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorApplicationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicationStage",
                table: "Tutors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE [Tutors] SET [ApplicationStage] = 3 WHERE [Status] = 1");

            migrationBuilder.CreateTable(
                name: "TutorApplicationSettings",
                columns: table => new
                {
                    TutorApplicationSettingsId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ShortlistLimit = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorApplicationSettings", x => x.TutorApplicationSettingsId);
                    table.CheckConstraint("CK_TutorApplicationSettings_ShortlistLimit", "[ShortlistLimit] IS NULL OR [ShortlistLimit] > 0");
                    table.CheckConstraint("CK_TutorApplicationSettings_Singleton", "[TutorApplicationSettingsId] = 1");
                });

            migrationBuilder.InsertData(
                table: "TutorApplicationSettings",
                columns: new[] { "TutorApplicationSettingsId", "ShortlistLimit", "UpdatedAt" },
                values: new object[] { 1, null, new DateTime(2026, 9, 14, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorApplicationSettings");

            migrationBuilder.DropColumn(
                name: "ApplicationStage",
                table: "Tutors");
        }
    }
}

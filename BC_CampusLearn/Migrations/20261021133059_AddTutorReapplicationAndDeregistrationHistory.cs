using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorReapplicationAndDeregistrationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReapplication",
                table: "Tutors",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "TutorDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReapplicationSelection",
                table: "TutorCourseModules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TutorDeregistrationEvents",
                columns: table => new
                {
                    TutorDeregistrationEventId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    DeregisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AdministratorBcUserId = table.Column<int>(type: "int", nullable: true),
                    AdministratorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorDeregistrationEvents", x => x.TutorDeregistrationEventId);
                    table.ForeignKey(
                        name: "FK_TutorDeregistrationEvents_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorDeregistrationEvents_TutorId_DeregisteredAt",
                table: "TutorDeregistrationEvents",
                columns: new[] { "TutorId", "DeregisteredAt" });

            // Preserve the latest deregistration already recorded before this migration.
            migrationBuilder.Sql("""
                INSERT INTO [TutorDeregistrationEvents]
                    ([TutorId], [DeregisteredAt], [AdministratorBcUserId], [AdministratorName], [Reason])
                SELECT [TutorId], [DeregisteredAt], [DeregisteredByBcUserId], [DeregisteredByName], [DeregistrationReason]
                FROM [Tutors]
                WHERE [DeregisteredAt] IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorDeregistrationEvents");

            migrationBuilder.DropColumn(
                name: "IsReapplication",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "TutorDocuments");

            migrationBuilder.DropColumn(
                name: "IsReapplicationSelection",
                table: "TutorCourseModules");
        }
    }
}

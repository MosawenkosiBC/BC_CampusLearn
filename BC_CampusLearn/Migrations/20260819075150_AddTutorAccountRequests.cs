using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorAccountRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorDeregistrationRequests",
                columns: table => new
                {
                    TutorDeregistrationRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorDeregistrationRequests", x => x.TutorDeregistrationRequestId);
                    table.ForeignKey(
                        name: "FK_TutorDeregistrationRequests_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorModuleChangeRequests",
                columns: table => new
                {
                    TutorModuleChangeRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    ProgrammeModuleId = table.Column<int>(type: "int", nullable: false),
                    RequestType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorModuleChangeRequests", x => x.TutorModuleChangeRequestId);
                    table.ForeignKey(
                        name: "FK_TutorModuleChangeRequests_ProgrammeModule_ProgrammeModuleId",
                        column: x => x.ProgrammeModuleId,
                        principalTable: "ProgrammeModule",
                        principalColumn: "ProgrammeModuleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TutorModuleChangeRequests_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorDeregistrationRequests_TutorId_Status",
                table: "TutorDeregistrationRequests",
                columns: new[] { "TutorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TutorModuleChangeRequests_ProgrammeModuleId",
                table: "TutorModuleChangeRequests",
                column: "ProgrammeModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorModuleChangeRequests_TutorId_ProgrammeModuleId_Status",
                table: "TutorModuleChangeRequests",
                columns: new[] { "TutorId", "ProgrammeModuleId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorDeregistrationRequests");

            migrationBuilder.DropTable(
                name: "TutorModuleChangeRequests");
        }
    }
}

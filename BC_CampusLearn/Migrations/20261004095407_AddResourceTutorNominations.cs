using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceTutorNominations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResourceTutorNominations",
                columns: table => new
                {
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    ProgrammeModuleId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    NominatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NominatedByBcUserId = table.Column<int>(type: "int", nullable: false),
                    DenominatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DenominatedByBcUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceTutorNominations", x => new { x.TutorId, x.ProgrammeModuleId });
                    table.ForeignKey(
                        name: "FK_ResourceTutorNominations_BcUsers_DenominatedByBcUserId",
                        column: x => x.DenominatedByBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceTutorNominations_BcUsers_NominatedByBcUserId",
                        column: x => x.NominatedByBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceTutorNominations_ProgrammeModule_ProgrammeModuleId",
                        column: x => x.ProgrammeModuleId,
                        principalTable: "ProgrammeModule",
                        principalColumn: "ProgrammeModuleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceTutorNominations_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceTutorNominations_DenominatedByBcUserId",
                table: "ResourceTutorNominations",
                column: "DenominatedByBcUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceTutorNominations_NominatedByBcUserId",
                table: "ResourceTutorNominations",
                column: "NominatedByBcUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceTutorNominations_ProgrammeModuleId",
                table: "ResourceTutorNominations",
                column: "ProgrammeModuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceTutorNominations");
        }
    }
}

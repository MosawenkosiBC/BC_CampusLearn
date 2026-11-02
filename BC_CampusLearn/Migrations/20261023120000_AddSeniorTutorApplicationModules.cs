using BC_CampusLearn.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261023120000_AddSeniorTutorApplicationModules")]
public sealed class AddSeniorTutorApplicationModules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SeniorTutorApplicationModules",
            columns: table => new
            {
                SeniorTutorApplicationId = table.Column<int>(type: "int", nullable: false),
                ProgrammeModuleId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_SeniorTutorApplicationModules",
                    item => new
                    {
                        item.SeniorTutorApplicationId,
                        item.ProgrammeModuleId
                    });
                table.ForeignKey(
                    name: "FK_SeniorTutorApplicationModules_ProgrammeModule_ProgrammeModuleId",
                    column: item => item.ProgrammeModuleId,
                    principalTable: "ProgrammeModule",
                    principalColumn: "ProgrammeModuleId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SeniorTutorApplicationModules_SeniorTutorApplications_SeniorTutorApplicationId",
                    column: item => item.SeniorTutorApplicationId,
                    principalTable: "SeniorTutorApplications",
                    principalColumn: "SeniorTutorApplicationId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SeniorTutorApplicationModules_ProgrammeModuleId",
            table: "SeniorTutorApplicationModules",
            column: "ProgrammeModuleId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SeniorTutorApplicationModules");
    }
}

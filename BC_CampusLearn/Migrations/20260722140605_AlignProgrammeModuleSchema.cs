using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations;

/// <inheritdoc />
public partial class AlignProgrammeModuleSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [TutorAvailabilities])
                OR EXISTS (SELECT 1 FROM [TutorCourseModules])
                THROW 51006,
                    'Programme module alignment requires empty tutor assignment and availability tables because the legacy module IDs cannot be mapped safely.',
                    1;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_TutorAvailabilities_CourseModules_CourseModuleId",
            table: "TutorAvailabilities");

        migrationBuilder.DropForeignKey(
            name: "FK_TutorCourseModules_CourseModules_CourseModuleId",
            table: "TutorCourseModules");

        migrationBuilder.DropPrimaryKey(
            name: "PK_TutorCourseModules",
            table: "TutorCourseModules");

        migrationBuilder.DropIndex(
            name: "IX_TutorAvailabilities_CourseModuleId",
            table: "TutorAvailabilities");

        migrationBuilder.DropIndex(
            name: "IX_TutorAvailabilities_TutorId_CourseModuleId_AvailableTime",
            table: "TutorAvailabilities");

        migrationBuilder.DropIndex(
            name: "IX_TutorCourseModules_CourseModuleId",
            table: "TutorCourseModules");

        migrationBuilder.DropPrimaryKey(
            name: "PK_ProgrammeModule",
            table: "ProgrammeModule");

        migrationBuilder.AddColumn<int>(
            name: "ProgrammeModuleId",
            table: "ProgrammeModule",
            type: "int",
            nullable: false,
            defaultValue: 0)
            .Annotation("SqlServer:Identity", "1, 1");

        migrationBuilder.RenameColumn(
            name: "CourseModuleId",
            table: "TutorAvailabilities",
            newName: "ProgrammeModuleId");

        migrationBuilder.RenameColumn(
            name: "CourseModuleId",
            table: "TutorCourseModules",
            newName: "ProgrammeModuleId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_ProgrammeModule",
            table: "ProgrammeModule",
            column: "ProgrammeModuleId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_TutorCourseModules",
            table: "TutorCourseModules",
            columns: new[] { "TutorId", "ProgrammeModuleId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProgrammeModule_ProgrammeId",
            table: "ProgrammeModule",
            column: "ProgrammeId");

        migrationBuilder.CreateIndex(
            name: "IX_TutorAvailabilities_ProgrammeModuleId",
            table: "TutorAvailabilities",
            column: "ProgrammeModuleId");

        migrationBuilder.CreateIndex(
            name: "IX_TutorAvailabilities_TutorId_ProgrammeModuleId_AvailableTime",
            table: "TutorAvailabilities",
            columns: new[] { "TutorId", "ProgrammeModuleId", "AvailableTime" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TutorCourseModules_ProgrammeModuleId",
            table: "TutorCourseModules",
            column: "ProgrammeModuleId");

        migrationBuilder.AddForeignKey(
            name: "FK_TutorAvailabilities_ProgrammeModule_ProgrammeModuleId",
            table: "TutorAvailabilities",
            column: "ProgrammeModuleId",
            principalTable: "ProgrammeModule",
            principalColumn: "ProgrammeModuleId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_TutorCourseModules_ProgrammeModule_ProgrammeModuleId",
            table: "TutorCourseModules",
            column: "ProgrammeModuleId",
            principalTable: "ProgrammeModule",
            principalColumn: "ProgrammeModuleId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.DropTable(
            name: "CourseModules");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [TutorAvailabilities])
                OR EXISTS (SELECT 1 FROM [TutorCourseModules])
                THROW 51005,
                    'Programme module alignment cannot be reversed while tutor assignment or availability data exists.',
                    1;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_TutorAvailabilities_ProgrammeModule_ProgrammeModuleId",
            table: "TutorAvailabilities");

        migrationBuilder.DropForeignKey(
            name: "FK_TutorCourseModules_ProgrammeModule_ProgrammeModuleId",
            table: "TutorCourseModules");

        migrationBuilder.DropPrimaryKey(
            name: "PK_TutorCourseModules",
            table: "TutorCourseModules");

        migrationBuilder.DropIndex(
            name: "IX_ProgrammeModule_ProgrammeId",
            table: "ProgrammeModule");

        migrationBuilder.DropIndex(
            name: "IX_TutorAvailabilities_ProgrammeModuleId",
            table: "TutorAvailabilities");

        migrationBuilder.DropIndex(
            name: "IX_TutorAvailabilities_TutorId_ProgrammeModuleId_AvailableTime",
            table: "TutorAvailabilities");

        migrationBuilder.DropIndex(
            name: "IX_TutorCourseModules_ProgrammeModuleId",
            table: "TutorCourseModules");

        migrationBuilder.DropPrimaryKey(
            name: "PK_ProgrammeModule",
            table: "ProgrammeModule");

        migrationBuilder.DropColumn(
            name: "ProgrammeModuleId",
            table: "ProgrammeModule");

        migrationBuilder.RenameColumn(
            name: "ProgrammeModuleId",
            table: "TutorAvailabilities",
            newName: "CourseModuleId");

        migrationBuilder.RenameColumn(
            name: "ProgrammeModuleId",
            table: "TutorCourseModules",
            newName: "CourseModuleId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_ProgrammeModule",
            table: "ProgrammeModule",
            columns: new[] { "ProgrammeId", "ModuleCode" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_TutorCourseModules",
            table: "TutorCourseModules",
            columns: new[] { "TutorId", "CourseModuleId" });

        migrationBuilder.CreateTable(
            name: "CourseModules",
            columns: table => new
            {
                CourseModuleId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CourseModules", x => x.CourseModuleId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CourseModules_Code",
            table: "CourseModules",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TutorAvailabilities_CourseModuleId",
            table: "TutorAvailabilities",
            column: "CourseModuleId");

        migrationBuilder.CreateIndex(
            name: "IX_TutorAvailabilities_TutorId_CourseModuleId_AvailableTime",
            table: "TutorAvailabilities",
            columns: new[] { "TutorId", "CourseModuleId", "AvailableTime" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TutorCourseModules_CourseModuleId",
            table: "TutorCourseModules",
            column: "CourseModuleId");

        migrationBuilder.AddForeignKey(
            name: "FK_TutorAvailabilities_CourseModules_CourseModuleId",
            table: "TutorAvailabilities",
            column: "CourseModuleId",
            principalTable: "CourseModules",
            principalColumn: "CourseModuleId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_TutorCourseModules_CourseModules_CourseModuleId",
            table: "TutorCourseModules",
            column: "CourseModuleId",
            principalTable: "CourseModules",
            principalColumn: "CourseModuleId",
            onDelete: ReferentialAction.Cascade);
    }
}

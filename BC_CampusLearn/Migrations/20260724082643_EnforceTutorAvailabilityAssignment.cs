using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class EnforceTutorAvailabilityAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT [TutorId], [ProgrammeModuleId]
                    FROM [TutorCourseModules]
                    GROUP BY [TutorId], [ProgrammeModuleId]
                    HAVING COUNT(*) > 1
                )
                    THROW 51008,
                        'Tutor assignment migration stopped: duplicate tutor/module assignments must be removed.',
                        1;

                IF EXISTS
                (
                    SELECT 1
                    FROM [TutorAvailabilities] AS availability
                    LEFT JOIN [TutorCourseModules] AS assignment
                        ON assignment.[TutorId] = availability.[TutorId]
                        AND assignment.[ProgrammeModuleId] =
                            availability.[ProgrammeModuleId]
                    WHERE assignment.[TutorId] IS NULL
                )
                    THROW 51007,
                        'Tutor availability migration stopped: every availability must reference a module assigned to that tutor.',
                        1;
                """);

            // The ProgrammeModule rename was applied manually in an earlier
            // migration, and some databases no longer have a candidate key in
            // this column order. Restore it before creating the composite FK.
            migrationBuilder.CreateIndex(
                name: "UX_TutorCourseModules_TutorId_ProgrammeModuleId",
                table: "TutorCourseModules",
                columns: new[] { "TutorId", "ProgrammeModuleId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorAvailabilities_TutorCourseModules_TutorId_ProgrammeModuleId",
                table: "TutorAvailabilities",
                columns: new[] { "TutorId", "ProgrammeModuleId" },
                principalTable: "TutorCourseModules",
                principalColumns: new[] { "TutorId", "ProgrammeModuleId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TutorAvailabilities_TutorCourseModules_TutorId_ProgrammeModuleId",
                table: "TutorAvailabilities");

            migrationBuilder.DropIndex(
                name: "UX_TutorCourseModules_TutorId_ProgrammeModuleId",
                table: "TutorCourseModules");
        }
    }
}

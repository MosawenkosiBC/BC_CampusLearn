using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class MoveModuleSelectionToBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgrammeModuleId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TutorId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE booking
                SET booking.[TutorId] = availability.[TutorId],
                    booking.[ProgrammeModuleId] =
                        availability.[ProgrammeModuleId],
                    booking.[Duration] = 1
                FROM [Bookings] AS booking
                INNER JOIN [TutorAvailabilities] AS availability
                    ON availability.[TutorAvailabilityId] =
                        booking.[TutorAvailabilityId];

                IF EXISTS
                (
                    SELECT 1
                    FROM [Bookings]
                    WHERE [TutorId] IS NULL
                       OR [ProgrammeModuleId] IS NULL
                )
                    THROW 51009,
                        'Booking migration stopped: every booking must reference a valid tutor availability.',
                        1;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "TutorId",
                table: "Bookings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProgrammeModuleId",
                table: "Bookings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT availability.[TutorId],
                           availability.[AvailableTime]
                    FROM [TutorAvailabilities] AS availability
                    INNER JOIN [Bookings] AS booking
                        ON booking.[TutorAvailabilityId] =
                            availability.[TutorAvailabilityId]
                    GROUP BY availability.[TutorId],
                             availability.[AvailableTime]
                    HAVING COUNT(*) > 1
                )
                    THROW 51011,
                        'Availability migration stopped: multiple bookings exist for the same tutor and time.',
                        1;

                ;WITH RankedAvailability AS
                (
                    SELECT availability.[TutorAvailabilityId],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY availability.[TutorId],
                                            availability.[AvailableTime]
                               ORDER BY
                                   CASE WHEN booking.[BookingId] IS NULL
                                       THEN 1 ELSE 0 END,
                                   availability.[TutorAvailabilityId]
                           ) AS [Position]
                    FROM [TutorAvailabilities] AS availability
                    LEFT JOIN [Bookings] AS booking
                        ON booking.[TutorAvailabilityId] =
                            availability.[TutorAvailabilityId]
                )
                DELETE availability
                FROM [TutorAvailabilities] AS availability
                INNER JOIN RankedAvailability AS ranked
                    ON ranked.[TutorAvailabilityId] =
                        availability.[TutorAvailabilityId]
                WHERE ranked.[Position] > 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_TutorAvailabilities_TutorAvailabilityId",
                table: "Bookings");

            migrationBuilder.Sql(
                """
                DECLARE @dropModuleForeignKeys nvarchar(max) = N'';

                SELECT @dropModuleForeignKeys +=
                    N'ALTER TABLE [TutorAvailabilities] DROP CONSTRAINT ' +
                    QUOTENAME(foreignKey.[name]) + N';'
                FROM sys.foreign_keys AS foreignKey
                WHERE foreignKey.[parent_object_id] =
                        OBJECT_ID(N'[TutorAvailabilities]')
                  AND foreignKey.[referenced_object_id] =
                        OBJECT_ID(N'[ProgrammeModule]');

                IF @dropModuleForeignKeys <> N''
                    EXEC sp_executesql @dropModuleForeignKeys;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_TutorAvailabilities_TutorCourseModules_TutorId_ProgrammeModuleId",
                table: "TutorAvailabilities");

            migrationBuilder.Sql(
                """
                DECLARE @dropModuleIndexes nvarchar(max) = N'';

                SELECT @dropModuleIndexes +=
                    N'DROP INDEX ' + QUOTENAME(indexInfo.[name]) +
                    N' ON [TutorAvailabilities];'
                FROM sys.indexes AS indexInfo
                WHERE indexInfo.[object_id] =
                        OBJECT_ID(N'[TutorAvailabilities]')
                  AND indexInfo.[is_primary_key] = 0
                  AND indexInfo.[is_unique_constraint] = 0
                  AND EXISTS
                  (
                      SELECT 1
                      FROM sys.index_columns AS indexColumn
                      INNER JOIN sys.columns AS columnInfo
                          ON columnInfo.[object_id] =
                                indexColumn.[object_id]
                         AND columnInfo.[column_id] =
                                indexColumn.[column_id]
                      WHERE indexColumn.[object_id] =
                                indexInfo.[object_id]
                        AND indexColumn.[index_id] =
                                indexInfo.[index_id]
                        AND columnInfo.[name] =
                                N'ProgrammeModuleId'
                  );

                IF @dropModuleIndexes <> N''
                    EXEC sp_executesql @dropModuleIndexes;
                """);

            migrationBuilder.DropColumn(
                name: "ProgrammeModuleId",
                table: "TutorAvailabilities");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TutorAvailabilities_TutorAvailabilityId_TutorId",
                table: "TutorAvailabilities",
                columns: new[] { "TutorAvailabilityId", "TutorId" });

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilities_TutorId_AvailableTime",
                table: "TutorAvailabilities",
                columns: new[] { "TutorId", "AvailableTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ProgrammeModuleId",
                table: "Bookings",
                column: "ProgrammeModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TutorAvailabilityId_TutorId",
                table: "Bookings",
                columns: new[] { "TutorAvailabilityId", "TutorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TutorId_ProgrammeModuleId",
                table: "Bookings",
                columns: new[] { "TutorId", "ProgrammeModuleId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Duration_OneHour",
                table: "Bookings",
                sql: "[Duration] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_ProgrammeModule_ProgrammeModuleId",
                table: "Bookings",
                column: "ProgrammeModuleId",
                principalTable: "ProgrammeModule",
                principalColumn: "ProgrammeModuleId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_TutorAvailabilities_TutorAvailabilityId_TutorId",
                table: "Bookings",
                columns: new[] { "TutorAvailabilityId", "TutorId" },
                principalTable: "TutorAvailabilities",
                principalColumns: new[] { "TutorAvailabilityId", "TutorId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_TutorCourseModules_TutorId_ProgrammeModuleId",
                table: "Bookings",
                columns: new[] { "TutorId", "ProgrammeModuleId" },
                principalTable: "TutorCourseModules",
                principalColumns: new[] { "TutorId", "ProgrammeModuleId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_ProgrammeModule_ProgrammeModuleId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_TutorAvailabilities_TutorAvailabilityId_TutorId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_TutorCourseModules_TutorId_ProgrammeModuleId",
                table: "Bookings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TutorAvailabilities_TutorAvailabilityId_TutorId",
                table: "TutorAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_TutorAvailabilities_TutorId_AvailableTime",
                table: "TutorAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ProgrammeModuleId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TutorAvailabilityId_TutorId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TutorId_ProgrammeModuleId",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Duration_OneHour",
                table: "Bookings");

            migrationBuilder.AddColumn<int>(
                name: "ProgrammeModuleId",
                table: "TutorAvailabilities",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE availability
                SET availability.[ProgrammeModuleId] =
                    booking.[ProgrammeModuleId]
                FROM [TutorAvailabilities] AS availability
                INNER JOIN [Bookings] AS booking
                    ON booking.[TutorAvailabilityId] =
                        availability.[TutorAvailabilityId];

                UPDATE availability
                SET availability.[ProgrammeModuleId] =
                    assignment.[ProgrammeModuleId]
                FROM [TutorAvailabilities] AS availability
                CROSS APPLY
                (
                    SELECT TOP (1) item.[ProgrammeModuleId]
                    FROM [TutorCourseModules] AS item
                    WHERE item.[TutorId] = availability.[TutorId]
                    ORDER BY item.[ProgrammeModuleId]
                ) AS assignment
                WHERE availability.[ProgrammeModuleId] IS NULL;

                IF EXISTS
                (
                    SELECT 1
                    FROM [TutorAvailabilities]
                    WHERE [ProgrammeModuleId] IS NULL
                )
                    THROW 51010,
                        'Availability rollback stopped: every tutor with availability requires at least one assigned module.',
                        1;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ProgrammeModuleId",
                table: "TutorAvailabilities",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "ProgrammeModuleId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TutorId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilities_ProgrammeModuleId",
                table: "TutorAvailabilities",
                column: "ProgrammeModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilities_TutorId_ProgrammeModuleId_AvailableTime",
                table: "TutorAvailabilities",
                columns: new[] { "TutorId", "ProgrammeModuleId", "AvailableTime" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_TutorAvailabilities_TutorAvailabilityId",
                table: "Bookings",
                column: "TutorAvailabilityId",
                principalTable: "TutorAvailabilities",
                principalColumn: "TutorAvailabilityId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorAvailabilities_ProgrammeModule_ProgrammeModuleId",
                table: "TutorAvailabilities",
                column: "ProgrammeModuleId",
                principalTable: "ProgrammeModule",
                principalColumn: "ProgrammeModuleId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorAvailabilities_TutorCourseModules_TutorId_ProgrammeModuleId",
                table: "TutorAvailabilities",
                columns: new[] { "TutorId", "ProgrammeModuleId" },
                principalTable: "TutorCourseModules",
                principalColumns: new[] { "TutorId", "ProgrammeModuleId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}

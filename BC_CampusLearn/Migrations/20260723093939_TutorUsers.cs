using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class TutorUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This is the finalization migration. Stage-one data must be
            // backfilled and verified before any legacy tutor columns are
            // removed or nullable columns are made required.
            migrationBuilder.Sql(
                """
                UPDATE [Tutors]
                SET [Status] = CASE WHEN [IsApproved] = 1 THEN 1 ELSE 0 END
                WHERE [Status] IS NULL;

                IF EXISTS
                (
                    SELECT 1
                    FROM [Tutors] t
                    WHERE t.[BcUserId] IS NULL
                       OR t.[ProgrammeId] IS NULL
                       OR t.[OverallAverage] IS NULL
                       OR t.[YearOfStudy] IS NULL
                       OR NULLIF(LTRIM(RTRIM(t.[ReasonForTutoring])), N'') IS NULL
                       OR NULLIF(LTRIM(RTRIM(t.[TeachingStyle])), N'') IS NULL
                       OR NULLIF(LTRIM(RTRIM(t.[DemonstrationVideoUrl])), N'') IS NULL
                       OR t.[SubmittedAt] IS NULL
                       OR t.[CreatedAt] IS NULL
                       OR t.[Status] IS NULL
                )
                    THROW 51000,
                        'Tutor finalization stopped: complete the stage-one tutor backfill before applying TutorUsers.',
                        1;

                IF EXISTS
                (
                    SELECT 1
                    FROM [Tutors]
                    WHERE [YearOfStudy] NOT BETWEEN 1 AND 4
                       OR [OverallAverage] NOT BETWEEN 0 AND 100
                )
                    THROW 51001,
                        'Tutor finalization stopped: YearOfStudy or OverallAverage contains an invalid value.',
                        1;

                IF EXISTS
                (
                    SELECT 1
                    FROM [Tutors]
                    WHERE LEN([Biography]) > 500
                )
                    THROW 51002,
                        'Tutor finalization stopped: one or more biographies exceed 500 characters.',
                        1;

                IF EXISTS
                (
                    SELECT 1
                    FROM [BcUsers]
                    WHERE NULLIF(LTRIM(RTRIM([PersonnelNumber])), N'') IS NULL
                       OR [EntraObjectId] IS NULL
                       OR [EntraTenantId] IS NULL
                       OR [EntraObjectId] = '00000000-0000-0000-0000-000000000000'
                       OR [EntraTenantId] = '00000000-0000-0000-0000-000000000000'
                )
                    THROW 51003,
                        'Tutor finalization stopped: every BC user requires verified personnel and Entra identifiers.',
                        1;

                IF EXISTS
                (
                    SELECT [BcUserId]
                    FROM [Tutors]
                    GROUP BY [BcUserId]
                    HAVING COUNT(*) > 1
                )
                    THROW 51004,
                        'Tutor finalization stopped: more than one tutor is linked to the same BC user.',
                        1;

                IF EXISTS
                (
                    SELECT [PersonnelNumber]
                    FROM [BcUsers]
                    GROUP BY [PersonnelNumber]
                    HAVING COUNT(*) > 1
                )
                    THROW 51005,
                        'Tutor finalization stopped: duplicate personnel numbers exist.',
                        1;

                IF EXISTS
                (
                    SELECT [EntraTenantId], [EntraObjectId]
                    FROM [BcUsers]
                    GROUP BY [EntraTenantId], [EntraObjectId]
                    HAVING COUNT(*) > 1
                )
                    THROW 51006,
                        'Tutor finalization stopped: duplicate Entra tenant/object pairs exist.',
                        1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Tutors_BcUserId",
                table: "Tutors");

            migrationBuilder.DropIndex(
                name: "IX_Tutors_EntraTenantId_EntraObjectId",
                table: "Tutors");

            migrationBuilder.AlterColumn<int>(
                name: "YearOfStudy",
                table: "Tutors",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TeachingStyle",
                table: "Tutors",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "Tutors",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Tutors",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReasonForTutoring",
                table: "Tutors",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProgrammeId",
                table: "Tutors",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OverallAverage",
                table: "Tutors",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Tutors",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "DemonstrationVideoUrl",
                table: "Tutors",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Tutors",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Biography",
                table: "Tutors",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<int>(
                name: "BcUserId",
                table: "Tutors",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PersonnelNumber",
                table: "BcUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EntraTenantId",
                table: "BcUsers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EntraObjectId",
                table: "BcUsers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "EntraObjectId",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "EntraTenantId",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Tutors");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_BcUserId",
                table: "Tutors",
                column: "BcUserId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tutors_OverallAverage",
                table: "Tutors",
                sql: "[OverallAverage] BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tutors_YearOfStudy",
                table: "Tutors",
                sql: "[YearOfStudy] BETWEEN 1 AND 4");

            migrationBuilder.CreateIndex(
                name: "IX_BcUsers_EntraTenantId_EntraObjectId",
                table: "BcUsers",
                columns: new[] { "EntraTenantId", "EntraObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BcUsers_PersonnelNumber",
                table: "BcUsers",
                column: "PersonnelNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tutors_BcUserId",
                table: "Tutors");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tutors_OverallAverage",
                table: "Tutors");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tutors_YearOfStudy",
                table: "Tutors");

            migrationBuilder.DropIndex(
                name: "IX_BcUsers_EntraTenantId_EntraObjectId",
                table: "BcUsers");

            migrationBuilder.DropIndex(
                name: "IX_BcUsers_PersonnelNumber",
                table: "BcUsers");

            migrationBuilder.AlterColumn<int>(
                name: "YearOfStudy",
                table: "Tutors",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "TeachingStyle",
                table: "Tutors",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmittedAt",
                table: "Tutors",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Tutors",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ReasonForTutoring",
                table: "Tutors",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<int>(
                name: "ProgrammeId",
                table: "Tutors",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "OverallAverage",
                table: "Tutors",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Tutors",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "DemonstrationVideoUrl",
                table: "Tutors",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Tutors",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<string>(
                name: "Biography",
                table: "Tutors",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BcUserId",
                table: "Tutors",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Tutors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Tutors",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntraObjectId",
                table: "Tutors",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntraTenantId",
                table: "Tutors",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Tutors",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "PersonnelNumber",
                table: "BcUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<Guid>(
                name: "EntraTenantId",
                table: "BcUsers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "EntraObjectId",
                table: "BcUsers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_BcUserId",
                table: "Tutors",
                column: "BcUserId",
                unique: true,
                filter: "[BcUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_EntraTenantId_EntraObjectId",
                table: "Tutors",
                columns: new[] { "EntraTenantId", "EntraObjectId" },
                unique: true,
                filter: "[EntraTenantId] IS NOT NULL AND [EntraObjectId] IS NOT NULL");
        }
    }
}

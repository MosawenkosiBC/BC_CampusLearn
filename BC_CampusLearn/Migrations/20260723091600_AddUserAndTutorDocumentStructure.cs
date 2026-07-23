using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAndTutorDocumentStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BcUserId",
                table: "Tutors",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Tutors",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DemonstrationVideoUrl",
                table: "Tutors",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverallAverage",
                table: "Tutors",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgrammeId",
                table: "Tutors",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonForTutoring",
                table: "Tutors",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Tutors",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Tutors",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Tutors",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeachingStyle",
                table: "Tutors",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Tutors",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearOfStudy",
                table: "Tutors",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BcUsers",
                columns: table => new
                {
                    BcUserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonnelNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EntraObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntraTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsPublicActivityEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PublicActivityDisabledReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublicActivityDisabledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BcUsers", x => x.BcUserId);
                });

            migrationBuilder.CreateTable(
                name: "TutorDocuments",
                columns: table => new
                {
                    TutorDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorDocuments", x => x.TutorDocumentId);
                    table.ForeignKey(
                        name: "FK_TutorDocuments_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_BcUserId",
                table: "Tutors",
                column: "BcUserId",
                unique: true,
                filter: "[BcUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_ProgrammeId",
                table: "Tutors",
                column: "ProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorDocuments_TutorId",
                table: "TutorDocuments",
                column: "TutorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tutors_BcUsers_BcUserId",
                table: "Tutors",
                column: "BcUserId",
                principalTable: "BcUsers",
                principalColumn: "BcUserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tutors_ProgrammeOfStudy_ProgrammeId",
                table: "Tutors",
                column: "ProgrammeId",
                principalTable: "ProgrammeOfStudy",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tutors_BcUsers_BcUserId",
                table: "Tutors");

            migrationBuilder.DropForeignKey(
                name: "FK_Tutors_ProgrammeOfStudy_ProgrammeId",
                table: "Tutors");

            migrationBuilder.DropTable(
                name: "BcUsers");

            migrationBuilder.DropTable(
                name: "TutorDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Tutors_BcUserId",
                table: "Tutors");

            migrationBuilder.DropIndex(
                name: "IX_Tutors_ProgrammeId",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "BcUserId",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "DemonstrationVideoUrl",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "OverallAverage",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "ProgrammeId",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "ReasonForTutoring",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "TeachingStyle",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "YearOfStudy",
                table: "Tutors");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningResource",
                columns: table => new
                {
                    LearningResourceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    ProgrammeModuleId = table.Column<int>(type: "int", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllowSubscriberComments = table.Column<bool>(type: "bit", nullable: false),
                    Link1 = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Link2 = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DatePublished = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateUpdated = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningResource", x => x.LearningResourceId);
                    table.ForeignKey(
                        name: "FK_LearningResource_ProgrammeModule_ProgrammeModuleId",
                        column: x => x.ProgrammeModuleId,
                        principalTable: "ProgrammeModule",
                        principalColumn: "ProgrammeModuleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningResource_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceSubscriptions",
                columns: table => new
                {
                    ResourceSubscriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonnelNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModuleCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DateSubscribed = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastAccessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceSubscriptions", x => x.ResourceSubscriptionId);
                });

            migrationBuilder.CreateTable(
                name: "LearningResourceDocuments",
                columns: table => new
                {
                    ResourceDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    DocumentName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DateUploaded = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningResourceDocuments", x => x.ResourceDocumentId);
                    table.ForeignKey(
                        name: "FK_LearningResourceDocuments_LearningResource_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "LearningResource",
                        principalColumn: "LearningResourceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningResource_ProgrammeModuleId",
                table: "LearningResource",
                column: "ProgrammeModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningResource_TutorId_Status_DateCreated",
                table: "LearningResource",
                columns: new[] { "TutorId", "Status", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningResourceDocuments_ResourceId",
                table: "LearningResourceDocuments",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceSubscriptions_PersonnelNumber_ModuleCode",
                table: "ResourceSubscriptions",
                columns: new[] { "PersonnelNumber", "ModuleCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningResourceDocuments");

            migrationBuilder.DropTable(
                name: "ResourceSubscriptions");

            migrationBuilder.DropTable(
                name: "LearningResource");
        }
    }
}

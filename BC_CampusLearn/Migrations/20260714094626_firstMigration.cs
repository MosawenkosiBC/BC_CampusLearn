using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class firstMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "Tutors",
                columns: table => new
                {
                    TutorId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntraObjectId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    EntraTenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Biography = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ProfileImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GitHubUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tutors", x => x.TutorId);
                });

            migrationBuilder.CreateTable(
                name: "TutorAvailabilities",
                columns: table => new
                {
                    TutorAvailabilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsBooked = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorAvailabilities", x => x.TutorAvailabilityId);
                    table.ForeignKey(
                        name: "FK_TutorAvailabilities_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorCourseModules",
                columns: table => new
                {
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    CourseModuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorCourseModules", x => new { x.TutorId, x.CourseModuleId });
                    table.ForeignKey(
                        name: "FK_TutorCourseModules_CourseModules_CourseModuleId",
                        column: x => x.CourseModuleId,
                        principalTable: "CourseModules",
                        principalColumn: "CourseModuleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutorCourseModules_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    BookingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    TutorAvailabilityId = table.Column<int>(type: "int", nullable: false),
                    StudentObjectId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    StudentTenantId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    StudentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StudentEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    SessionStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SessionEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.BookingId);
                    table.ForeignKey(
                        name: "FK_Bookings_TutorAvailabilities_TutorAvailabilityId",
                        column: x => x.TutorAvailabilityId,
                        principalTable: "TutorAvailabilities",
                        principalColumn: "TutorAvailabilityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bookings_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TutorAvailabilityId",
                table: "Bookings",
                column: "TutorAvailabilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TutorId",
                table: "Bookings",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseModules_Code",
                table: "CourseModules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilities_TutorId",
                table: "TutorAvailabilities",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorCourseModules_CourseModuleId",
                table: "TutorCourseModules",
                column: "CourseModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_EntraTenantId_EntraObjectId",
                table: "Tutors",
                columns: new[] { "EntraTenantId", "EntraObjectId" },
                unique: true,
                filter: "[EntraTenantId] IS NOT NULL AND [EntraObjectId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "TutorCourseModules");

            migrationBuilder.DropTable(
                name: "TutorAvailabilities");

            migrationBuilder.DropTable(
                name: "CourseModules");

            migrationBuilder.DropTable(
                name: "Tutors");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorInterviewMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "InterviewLocation",
                table: "Tutors",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "TutorInterviewMeetings",
                columns: table => new
                {
                    TutorInterviewMeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    ApplicationSubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OrganizerObjectId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", maxLength: 30000, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    JoinUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ScheduledStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByBcUserId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorInterviewMeetings", x => x.TutorInterviewMeetingId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorInterviewMeetings_TutorId_ApplicationSubmittedAt",
                table: "TutorInterviewMeetings",
                columns: new[] { "TutorId", "ApplicationSubmittedAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorInterviewMeetings");

            migrationBuilder.AlterColumn<string>(
                name: "InterviewLocation",
                table: "Tutors",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);
        }
    }
}

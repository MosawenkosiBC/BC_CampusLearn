using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewPreparationWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedInterviewer",
                table: "Tutors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InterviewDurationMinutes",
                table: "Tutors",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterviewLocation",
                table: "Tutors",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterviewPreparationNotes",
                table: "Tutors",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InterviewScheduledAt",
                table: "Tutors",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tutors_InterviewDurationMinutes",
                table: "Tutors",
                sql: "[InterviewDurationMinutes] IS NULL OR [InterviewDurationMinutes] BETWEEN 15 AND 240");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Tutors_InterviewDurationMinutes",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "AssignedInterviewer",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "InterviewDurationMinutes",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "InterviewLocation",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "InterviewPreparationNotes",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "InterviewScheduledAt",
                table: "Tutors");
        }
    }
}

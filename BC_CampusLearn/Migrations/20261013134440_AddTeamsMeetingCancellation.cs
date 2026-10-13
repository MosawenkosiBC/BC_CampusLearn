using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamsMeetingCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TeamsCancellationLastAttemptAt",
                table: "MeetingLink",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TeamsCancellationRequestedAt",
                table: "MeetingLink",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TeamsCancelledAt",
                table: "MeetingLink",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamsCancellationLastAttemptAt",
                table: "MeetingLink");

            migrationBuilder.DropColumn(
                name: "TeamsCancellationRequestedAt",
                table: "MeetingLink");

            migrationBuilder.DropColumn(
                name: "TeamsCancelledAt",
                table: "MeetingLink");
        }
    }
}

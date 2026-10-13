using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamsCalendarEventMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TeamsCreationRequestId",
                table: "MeetingLink",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsEventId",
                table: "MeetingLink",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsOrganizerIdentityHash",
                table: "MeetingLink",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamsCreationRequestId",
                table: "MeetingLink");

            migrationBuilder.DropColumn(
                name: "TeamsEventId",
                table: "MeetingLink");

            migrationBuilder.DropColumn(
                name: "TeamsOrganizerIdentityHash",
                table: "MeetingLink");
        }
    }
}

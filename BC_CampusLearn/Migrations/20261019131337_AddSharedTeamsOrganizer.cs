using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedTeamsOrganizer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeamsSharedOrganizerEmail",
                table: "PlatformSettings",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsSharedOrganizerObjectId",
                table: "PlatformSettings",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedTeamsCoOrganizerObjectId",
                table: "MeetingLink",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedTeamsOrganizerObjectId",
                table: "MeetingLink",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedTeamsOrganizerTenantId",
                table: "MeetingLink",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TeamsCoOrganizerAssignedAt",
                table: "MeetingLink",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsesSharedTeamsOrganizer",
                table: "MeetingLink",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "PlatformSettings",
                keyColumn: "PlatformSettingsId",
                keyValue: 1,
                columns: new[] { "TeamsSharedOrganizerEmail", "TeamsSharedOrganizerObjectId" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamsSharedOrganizerEmail",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "TeamsSharedOrganizerObjectId",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "EncryptedTeamsCoOrganizerObjectId",
                table: "MeetingLink");

            migrationBuilder.DropColumn(
                name: "EncryptedTeamsOrganizerObjectId",
                table: "MeetingLink");

            migrationBuilder.DropColumn(
                name: "EncryptedTeamsOrganizerTenantId",
                table: "MeetingLink");

            migrationBuilder.DropColumn(
                name: "TeamsCoOrganizerAssignedAt",
                table: "MeetingLink");

            migrationBuilder.DropColumn(
                name: "UsesSharedTeamsOrganizer",
                table: "MeetingLink");
        }
    }
}

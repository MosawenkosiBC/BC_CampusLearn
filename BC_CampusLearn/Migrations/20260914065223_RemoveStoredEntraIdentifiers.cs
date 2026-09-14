using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStoredEntraIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BcUsers_EntraTenantId_EntraObjectId",
                table: "BcUsers");

            migrationBuilder.DropColumn(
                name: "StudentObjectId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "StudentTenantId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "EntraObjectId",
                table: "BcUsers");

            migrationBuilder.DropColumn(
                name: "EntraTenantId",
                table: "BcUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StudentObjectId",
                table: "Bookings",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StudentTenantId",
                table: "Bookings",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "EntraObjectId",
                table: "BcUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EntraTenantId",
                table: "BcUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BcUsers_EntraTenantId_EntraObjectId",
                table: "BcUsers",
                columns: new[] { "EntraTenantId", "EntraObjectId" },
                unique: true,
                filter: "[EntraTenantId] IS NOT NULL AND [EntraObjectId] IS NOT NULL");
        }
    }
}

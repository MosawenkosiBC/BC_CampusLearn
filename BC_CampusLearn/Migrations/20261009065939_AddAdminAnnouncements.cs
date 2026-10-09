using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AnnouncementId",
                table: "UserNotifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    AnnouncementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    SenderBcUserId = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.AnnouncementId);
                    table.ForeignKey(
                        name: "FK_Announcements_BcUsers_SenderBcUserId",
                        column: x => x.SenderBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_AnnouncementId",
                table: "UserNotifications",
                column: "AnnouncementId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_SenderBcUserId",
                table: "Announcements",
                column: "SenderBcUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_SentAt",
                table: "Announcements",
                column: "SentAt");

            migrationBuilder.AddForeignKey(
                name: "FK_UserNotifications_Announcements_AnnouncementId",
                table: "UserNotifications",
                column: "AnnouncementId",
                principalTable: "Announcements",
                principalColumn: "AnnouncementId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserNotifications_Announcements_AnnouncementId",
                table: "UserNotifications");

            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_AnnouncementId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "AnnouncementId",
                table: "UserNotifications");
        }
    }
}

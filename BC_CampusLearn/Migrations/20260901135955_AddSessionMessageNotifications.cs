using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionMessageNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadAt",
                table: "SessionMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipientBcUserId",
                table: "SessionMessages",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE message
                SET RecipientBcUserId =
                    CASE
                        WHEN message.SenderBcUserId = tutor.BcUserId
                            THEN booking.StudentBcUserId
                        ELSE tutor.BcUserId
                    END
                FROM SessionMessages AS message
                INNER JOIN Bookings AS booking
                    ON booking.BookingId = message.BookingId
                INNER JOIN Tutors AS tutor
                    ON tutor.TutorId = booking.TutorId
                WHERE booking.StudentBcUserId IS NOT NULL
                    AND message.SenderBcUserId IN (
                        booking.StudentBcUserId,
                        tutor.BcUserId);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SessionMessages_RecipientBcUserId_ReadAt_SentAt",
                table: "SessionMessages",
                columns: new[] { "RecipientBcUserId", "ReadAt", "SentAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_SessionMessages_BcUsers_RecipientBcUserId",
                table: "SessionMessages",
                column: "RecipientBcUserId",
                principalTable: "BcUsers",
                principalColumn: "BcUserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionMessages_BcUsers_RecipientBcUserId",
                table: "SessionMessages");

            migrationBuilder.DropIndex(
                name: "IX_SessionMessages_RecipientBcUserId_ReadAt_SentAt",
                table: "SessionMessages");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "SessionMessages");

            migrationBuilder.DropColumn(
                name: "RecipientBcUserId",
                table: "SessionMessages");
        }
    }
}

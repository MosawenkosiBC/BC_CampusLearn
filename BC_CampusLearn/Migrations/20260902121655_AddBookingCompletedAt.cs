using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Bookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE booking
                SET booking.CompletedAt = COALESCE(
                    execution.CompletedAt,
                    completedHistory.ChangedAt)
                FROM Bookings AS booking
                LEFT JOIN SessionExecutions AS execution
                    ON execution.BookingId = booking.BookingId
                OUTER APPLY
                (
                    SELECT TOP (1) history.ChangedAt
                    FROM BookingStatusHistory AS history
                    WHERE history.BookingId = booking.BookingId
                      AND history.NewStatus = 3
                    ORDER BY history.ChangedAt DESC
                ) AS completedHistory
                WHERE booking.Status = 3
                  AND booking.CompletedAt IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Bookings");
        }
    }
}

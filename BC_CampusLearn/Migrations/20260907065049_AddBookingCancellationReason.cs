using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCancellationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Bookings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE booking
                SET booking.CancellationReason = cancellation.Reason
                FROM Bookings AS booking
                OUTER APPLY
                (
                    SELECT TOP (1) history.Reason
                    FROM BookingStatusHistory AS history
                    WHERE history.BookingId = booking.BookingId
                      AND history.NewStatus = 4
                      AND history.Reason IS NOT NULL
                    ORDER BY history.ChangedAt DESC
                ) AS cancellation
                WHERE booking.Status = 4
                  AND cancellation.Reason IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Bookings");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedSessionCompensation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CompensationAmount",
                table: "SuperAdminSessionReview",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE review
                SET review.CompensationAmount = settings.TutorPaymentAmount
                FROM SuperAdminSessionReview AS review
                INNER JOIN Bookings AS booking ON booking.BookingId = review.BookingId
                CROSS JOIN PlatformSettings AS settings
                WHERE settings.PlatformSettingsId = 1
                  AND settings.TutorPaymentAmount IS NOT NULL
                  AND review.IsAccepted = 1
                  AND booking.Status = 3;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompensationAmount",
                table: "SuperAdminSessionReview");
        }
    }
}

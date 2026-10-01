using BC_CampusLearn.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261001120000_AddTutorHeadSessionReviewNotifications")]
public partial class AddTutorHeadSessionReviewNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "TutorHeadReviewAvailableAt",
            table: "Bookings",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SessionReviewsLastViewedAt",
            table: "BcUsers",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Bookings_TutorHeadReviewAvailableAt",
            table: "Bookings",
            column: "TutorHeadReviewAvailableAt");

        migrationBuilder.Sql(
            """
            UPDATE booking
            SET TutorHeadReviewAvailableAt =
                COALESCE(booking.CompletedAt, booking.ScheduledStartTime)
            FROM Bookings AS booking
            WHERE booking.Status = 3
              AND EXISTS (
                  SELECT 1
                  FROM StudentEvaluations AS studentReview
                  WHERE studentReview.BookingId = booking.BookingId)
              AND EXISTS (
                  SELECT 1
                  FROM TutorStudentEvaluations AS tutorReview
                  WHERE tutorReview.BookingId = booking.BookingId);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Bookings_TutorHeadReviewAvailableAt",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "TutorHeadReviewAvailableAt",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "SessionReviewsLastViewedAt",
            table: "BcUsers");
    }
}

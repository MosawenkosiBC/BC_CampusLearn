using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingSlotReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReservationExpiresAt",
                table: "TutorAvailabilities",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationToken",
                table: "TutorAvailabilities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservedByBcUserId",
                table: "TutorAvailabilities",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilities_ReservationExpiresAt",
                table: "TutorAvailabilities",
                column: "ReservationExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TutorAvailabilities_ReservationExpiresAt",
                table: "TutorAvailabilities");

            migrationBuilder.DropColumn(
                name: "ReservationExpiresAt",
                table: "TutorAvailabilities");

            migrationBuilder.DropColumn(
                name: "ReservationToken",
                table: "TutorAvailabilities");

            migrationBuilder.DropColumn(
                name: "ReservedByBcUserId",
                table: "TutorAvailabilities");
        }
    }
}

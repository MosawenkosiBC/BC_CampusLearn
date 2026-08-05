using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class MoveBookedSlotsIntoBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledStartTime",
                table: "Bookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE booking
                SET booking.[ScheduledStartTime] = availability.[AvailableTime]
                FROM [Bookings] AS booking
                INNER JOIN [TutorAvailabilities] AS availability
                    ON availability.[TutorAvailabilityId] =
                        booking.[TutorAvailabilityId]
                    AND availability.[TutorId] = booking.[TutorId];

                IF EXISTS
                (
                    SELECT 1
                    FROM [Bookings]
                    WHERE [ScheduledStartTime] IS NULL
                )
                    THROW 51009,
                        'Booking migration stopped: a booking has no matching availability slot.',
                        1;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ScheduledStartTime",
                table: "Bookings",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_TutorAvailabilities_TutorAvailabilityId_TutorId",
                table: "Bookings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TutorAvailabilities_TutorAvailabilityId_TutorId",
                table: "TutorAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TutorAvailabilityId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TutorAvailabilityId_TutorId",
                table: "Bookings");

            migrationBuilder.Sql(
                """
                DELETE availability
                FROM [TutorAvailabilities] AS availability
                INNER JOIN [Bookings] AS booking
                    ON booking.[TutorAvailabilityId] =
                        availability.[TutorAvailabilityId]
                    AND booking.[TutorId] = availability.[TutorId];
                """);

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TutorAvailabilities");

            migrationBuilder.DropColumn(
                name: "TutorAvailabilityId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TutorId_ScheduledStartTime",
                table: "Bookings",
                columns: new[] { "TutorId", "ScheduledStartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_TutorId_ScheduledStartTime",
                table: "Bookings");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TutorAvailabilities",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TutorAvailabilityId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO [TutorAvailabilities]
                    ([TutorId], [AvailableTime], [IsActive])
                SELECT DISTINCT
                    booking.[TutorId],
                    booking.[ScheduledStartTime],
                    0
                FROM [Bookings] AS booking
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM [TutorAvailabilities] AS availability
                    WHERE availability.[TutorId] = booking.[TutorId]
                        AND availability.[AvailableTime] =
                            booking.[ScheduledStartTime]
                );

                UPDATE booking
                SET booking.[TutorAvailabilityId] =
                    availability.[TutorAvailabilityId]
                FROM [Bookings] AS booking
                INNER JOIN [TutorAvailabilities] AS availability
                    ON availability.[TutorId] = booking.[TutorId]
                    AND availability.[AvailableTime] =
                        booking.[ScheduledStartTime];
                """);

            migrationBuilder.AlterColumn<int>(
                name: "TutorAvailabilityId",
                table: "Bookings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "ScheduledStartTime",
                table: "Bookings");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TutorAvailabilities_TutorAvailabilityId_TutorId",
                table: "TutorAvailabilities",
                columns: new[] { "TutorAvailabilityId", "TutorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TutorAvailabilityId",
                table: "Bookings",
                column: "TutorAvailabilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TutorAvailabilityId_TutorId",
                table: "Bookings",
                columns: new[] { "TutorAvailabilityId", "TutorId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_TutorAvailabilities_TutorAvailabilityId_TutorId",
                table: "Bookings",
                columns: new[] { "TutorAvailabilityId", "TutorId" },
                principalTable: "TutorAvailabilities",
                principalColumns: new[] { "TutorAvailabilityId", "TutorId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationNotificationDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SeniorTutorApplicationsNotificationSentAt",
                table: "TutorApplicationSettings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TutorApplicationsNotificationSentAt",
                table: "TutorApplicationSettings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [TutorApplicationSettings]
                SET [TutorApplicationsNotificationSentAt] = [UpdatedAt]
                WHERE [IsOpen] = 1;

                UPDATE [TutorApplicationSettings]
                SET [SeniorTutorApplicationsNotificationSentAt] = [UpdatedAt]
                WHERE [SeniorTutorApplicationsOpen] = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeniorTutorApplicationsNotificationSentAt",
                table: "TutorApplicationSettings");

            migrationBuilder.DropColumn(
                name: "TutorApplicationsNotificationSentAt",
                table: "TutorApplicationSettings");
        }
    }
}

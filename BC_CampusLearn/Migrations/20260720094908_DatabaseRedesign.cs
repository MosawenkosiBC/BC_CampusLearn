using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class DatabaseRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Tutors_TutorId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_TutorAvailabilities_TutorId",
                table: "TutorAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TutorId",
                table: "Bookings");

            migrationBuilder.Sql(
                "UPDATE [TutorAvailabilities] " +
                "SET [IsActive] = 0 " +
                "WHERE [IsBooked] = 1;");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "TutorAvailabilities");

            migrationBuilder.DropColumn(
                name: "IsBooked",
                table: "TutorAvailabilities");

            migrationBuilder.DropColumn(
                name: "SessionEnd",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SessionStart",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "TutorId",
                table: "Bookings");

            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "TutorAvailabilities",
                newName: "AvailableTime");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Bookings",
                newName: "DateBooked");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "Bookings",
                newName: "Summary");

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                table: "Bookings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CourseModuleId",
                table: "TutorAvailabilities",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE availability " +
                "SET [CourseModuleId] = module.[CourseModuleId] " +
                "FROM [TutorAvailabilities] AS availability " +
                "CROSS APPLY (" +
                "SELECT TOP (1) assignment.[CourseModuleId] " +
                "FROM [TutorCourseModules] AS assignment " +
                "WHERE assignment.[TutorId] = availability.[TutorId] " +
                "ORDER BY assignment.[CourseModuleId]" +
                ") AS module;");

            migrationBuilder.Sql(
                "IF EXISTS (" +
                "SELECT 1 FROM [TutorAvailabilities] " +
                "WHERE [CourseModuleId] IS NULL" +
                ") THROW 50001, " +
                "'Every tutor availability must have an assigned course module.', 1;");

            migrationBuilder.AlterColumn<int>(
                name: "CourseModuleId",
                table: "TutorAvailabilities",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Bookings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "To be confirmed");

            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "BookingDocuments",
                columns: table => new
                {
                    BookingDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<byte>(type: "tinyint", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingDocuments", x => x.BookingDocumentId);
                    table.CheckConstraint("CK_BookingDocuments_Position", "[Position] BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_BookingDocuments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingPreparationLinks",
                columns: table => new
                {
                    BookingPreparationLinkId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<byte>(type: "tinyint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingPreparationLinks", x => x.BookingPreparationLinkId);
                    table.CheckConstraint("CK_BookingPreparationLinks_Position", "[Position] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_BookingPreparationLinks_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilities_CourseModuleId",
                table: "TutorAvailabilities",
                column: "CourseModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilities_TutorId_CourseModuleId_AvailableTime",
                table: "TutorAvailabilities",
                columns: new[] { "TutorId", "CourseModuleId", "AvailableTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingDocuments_BookingId_Position",
                table: "BookingDocuments",
                columns: new[] { "BookingId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingPreparationLinks_BookingId_Position",
                table: "BookingPreparationLinks",
                columns: new[] { "BookingId", "Position" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TutorAvailabilities_CourseModules_CourseModuleId",
                table: "TutorAvailabilities",
                column: "CourseModuleId",
                principalTable: "CourseModules",
                principalColumn: "CourseModuleId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TutorAvailabilities_CourseModules_CourseModuleId",
                table: "TutorAvailabilities");

            migrationBuilder.DropTable(
                name: "BookingDocuments");

            migrationBuilder.DropTable(
                name: "BookingPreparationLinks");

            migrationBuilder.DropIndex(
                name: "IX_TutorAvailabilities_CourseModuleId",
                table: "TutorAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_TutorAvailabilities_TutorId_CourseModuleId_AvailableTime",
                table: "TutorAvailabilities");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndTime",
                table: "TutorAvailabilities",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBooked",
                table: "TutorAvailabilities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TutorId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SessionStart",
                table: "Bookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SessionEnd",
                table: "Bookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE availability " +
                "SET [EndTime] = DATEADD(hour, 1, [AvailableTime]), " +
                "[IsBooked] = CASE WHEN booking.[BookingId] IS NULL " +
                "THEN 0 ELSE 1 END " +
                "FROM [TutorAvailabilities] AS availability " +
                "LEFT JOIN [Bookings] AS booking " +
                "ON booking.[TutorAvailabilityId] = " +
                "availability.[TutorAvailabilityId];");

            migrationBuilder.Sql(
                "UPDATE booking " +
                "SET [TutorId] = availability.[TutorId], " +
                "[SessionStart] = availability.[AvailableTime], " +
                "[SessionEnd] = DATEADD(" +
                "hour, booking.[Duration], availability.[AvailableTime]) " +
                "FROM [Bookings] AS booking " +
                "INNER JOIN [TutorAvailabilities] AS availability " +
                "ON availability.[TutorAvailabilityId] = " +
                "booking.[TutorAvailabilityId];");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndTime",
                table: "TutorAvailabilities",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TutorId",
                table: "Bookings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SessionStart",
                table: "Bookings",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SessionEnd",
                table: "Bookings",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                table: "Bookings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "CourseModuleId",
                table: "TutorAvailabilities");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Bookings");

            migrationBuilder.RenameColumn(
                name: "AvailableTime",
                table: "TutorAvailabilities",
                newName: "StartTime");

            migrationBuilder.RenameColumn(
                name: "DateBooked",
                table: "Bookings",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "Summary",
                table: "Bookings",
                newName: "Reason");

            migrationBuilder.CreateIndex(
                name: "IX_TutorAvailabilities_TutorId",
                table: "TutorAvailabilities",
                column: "TutorId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TutorId",
                table: "Bookings",
                column: "TutorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Tutors_TutorId",
                table: "Bookings",
                column: "TutorId",
                principalTable: "Tutors",
                principalColumn: "TutorId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

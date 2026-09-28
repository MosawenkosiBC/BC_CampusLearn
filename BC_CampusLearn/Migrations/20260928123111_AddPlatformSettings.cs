using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdministrativeAccessActive",
                table: "BcUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    PlatformSettingsId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupportEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    CampusTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    AcademicYear = table.Column<int>(type: "int", nullable: false),
                    AcademicSemester = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DateTimeFormat = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Announcement = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsAnnouncementEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsMaintenanceModeEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BookingTermsAndConditions = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    UpdatedByBcUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.PlatformSettingsId);
                    table.CheckConstraint("CK_PlatformSettings_AcademicYear", "[AcademicYear] BETWEEN 2000 AND 2200");
                    table.CheckConstraint("CK_PlatformSettings_Singleton", "[PlatformSettingsId] = 1");
                    table.ForeignKey(
                        name: "FK_PlatformSettings_BcUsers_UpdatedByBcUserId",
                        column: x => x.UpdatedByBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SettingAuditLogs",
                columns: table => new
                {
                    SettingAuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SettingName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ChangedByBcUserId = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingAuditLogs", x => x.SettingAuditLogId);
                    table.ForeignKey(
                        name: "FK_SettingAuditLogs_BcUsers_ChangedByBcUserId",
                        column: x => x.ChangedByBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "PlatformSettings",
                columns: new[] { "PlatformSettingsId", "AcademicSemester", "AcademicYear", "Announcement", "BookingTermsAndConditions", "CampusTimeZoneId", "CurrencyCode", "DateTimeFormat", "IsAnnouncementEnabled", "IsMaintenanceModeEnabled", "SupportEmail", "UpdatedAt", "UpdatedByBcUserId" },
                values: new object[] { 1, "Semester 1", 2026, null, "All appointments with tutors must be scheduled a day ahead.\nAll sessions are limited to 1 hour.\nYou must come prepared for the sessions.\nYou will be given exercises to complete during your sessions.\nAll online sessions via MS Teams are recorded.\nAll face-to-face sessions are held in the study room.\nRespect the time and effort of your tutor.\nYou will be required to complete a tutor evaluation form.", "Africa/Johannesburg", "ZAR", "dd MMMM yyyy, HH:mm", false, false, "tutors@belgiumcampus.ac.za", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettings_UpdatedByBcUserId",
                table: "PlatformSettings",
                column: "UpdatedByBcUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SettingAuditLogs_ChangedAt",
                table: "SettingAuditLogs",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SettingAuditLogs_ChangedByBcUserId",
                table: "SettingAuditLogs",
                column: "ChangedByBcUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformSettings");

            migrationBuilder.DropTable(
                name: "SettingAuditLogs");

            migrationBuilder.DropColumn(
                name: "IsAdministrativeAccessActive",
                table: "BcUsers");
        }
    }
}

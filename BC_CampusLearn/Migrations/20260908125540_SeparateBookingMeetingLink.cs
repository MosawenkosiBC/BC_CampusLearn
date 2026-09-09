using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class SeparateBookingMeetingLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingLink",
                columns: table => new
                {
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingLink", x => x.BookingId);
                    table.ForeignKey(
                        name: "FK_MeetingLink_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [MeetingLink] ([BookingId], [Url])
                SELECT [BookingId], [MeetingLink]
                FROM [Bookings]
                WHERE [MeetingLink] IS NOT NULL
                  AND LTRIM(RTRIM([MeetingLink])) <> '';
                """);

            migrationBuilder.DropColumn(
                name: "MeetingLink",
                table: "Bookings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MeetingLink",
                table: "Bookings",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE bookings
                SET bookings.[MeetingLink] = meetingLinks.[Url]
                FROM [Bookings] AS bookings
                INNER JOIN [MeetingLink] AS meetingLinks
                    ON meetingLinks.[BookingId] = bookings.[BookingId];
                """);

            migrationBuilder.DropTable(
                name: "MeetingLink");
        }
    }
}

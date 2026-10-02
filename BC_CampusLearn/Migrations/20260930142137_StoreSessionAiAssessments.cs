using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class StoreSessionAiAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessionAiAssessments",
                columns: table => new
                {
                    SessionAiAssessmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    GeneratedByBcUserId = table.Column<int>(type: "int", nullable: false),
                    AssessmentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionAiAssessments", x => x.SessionAiAssessmentId);
                    table.ForeignKey(
                        name: "FK_SessionAiAssessments_BcUsers_GeneratedByBcUserId",
                        column: x => x.GeneratedByBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionAiAssessments_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionAiAssessments_BookingId",
                table: "SessionAiAssessments",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionAiAssessments_GeneratedByBcUserId",
                table: "SessionAiAssessments",
                column: "GeneratedByBcUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionAiAssessments");
        }
    }
}

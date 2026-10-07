using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminSessionReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SuperAdminSessionReview",
                columns: table => new
                {
                    SuperAdminSessionReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    ReviewerBcUserId = table.Column<int>(type: "int", nullable: false),
                    IsAccepted = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuperAdminSessionReview", x => x.SuperAdminSessionReviewId);
                    table.ForeignKey(
                        name: "FK_SuperAdminSessionReview_BcUsers_ReviewerBcUserId",
                        column: x => x.ReviewerBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SuperAdminSessionReview_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuperAdminSessionReview_BookingId",
                table: "SuperAdminSessionReview",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuperAdminSessionReview_ReviewerBcUserId",
                table: "SuperAdminSessionReview",
                column: "ReviewerBcUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuperAdminSessionReview");
        }
    }
}

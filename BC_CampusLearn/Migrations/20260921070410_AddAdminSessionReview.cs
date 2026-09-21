using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSessionReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminSessionReview",
                columns: table => new
                {
                    AdminSessionReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    ReviewerBcUserId = table.Column<int>(type: "int", nullable: false),
                    AllReviewsSubmitted = table.Column<bool>(type: "bit", nullable: false),
                    HeadConfirmedSession = table.Column<bool>(type: "bit", nullable: false),
                    HeadConfirmedQuality = table.Column<bool>(type: "bit", nullable: false),
                    ConcernsResolvedOrDocumented = table.Column<bool>(type: "bit", nullable: false),
                    EvidenceSupportsApproval = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSessionReview", x => x.AdminSessionReviewId);
                    table.ForeignKey(
                        name: "FK_AdminSessionReview_BcUsers_ReviewerBcUserId",
                        column: x => x.ReviewerBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminSessionReview_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessionReview_BookingId",
                table: "AdminSessionReview",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessionReview_ReviewerBcUserId",
                table: "AdminSessionReview",
                column: "ReviewerBcUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminSessionReview");
        }
    }
}

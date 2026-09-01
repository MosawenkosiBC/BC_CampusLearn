using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorSessionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Bookings",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "StudentBcUserId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE booking
                SET booking.[StudentBcUserId] = users.[BcUserId]
                FROM [Bookings] AS booking
                INNER JOIN [BcUsers] AS users
                    ON TRY_CONVERT(uniqueidentifier, booking.[StudentObjectId]) = users.[EntraObjectId]
                    AND TRY_CONVERT(uniqueidentifier, booking.[StudentTenantId]) = users.[EntraTenantId]
                WHERE booking.[StudentBcUserId] IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "BookingStatusHistory",
                columns: table => new
                {
                    BookingStatusHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedByBcUserId = table.Column<int>(type: "int", nullable: true),
                    ChangedBySystem = table.Column<bool>(type: "bit", nullable: false),
                    AvailabilityReopened = table.Column<bool>(type: "bit", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingStatusHistory", x => x.BookingStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_BookingStatusHistory_BcUsers_ChangedByBcUserId",
                        column: x => x.ChangedByBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingStatusHistory_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionExecutions",
                columns: table => new
                {
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpectedCompletionAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartSource = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionExecutions", x => x.BookingId);
                    table.ForeignKey(
                        name: "FK_SessionExecutions_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionMessages",
                columns: table => new
                {
                    SessionMessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    SenderBcUserId = table.Column<int>(type: "int", nullable: false),
                    MessageText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EditedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionMessages", x => x.SessionMessageId);
                    table.ForeignKey(
                        name: "FK_SessionMessages_BcUsers_SenderBcUserId",
                        column: x => x.SenderBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionMessages_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionReviews",
                columns: table => new
                {
                    SessionReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    ReviewerBcUserId = table.Column<int>(type: "int", nullable: false),
                    RevieweeBcUserId = table.Column<int>(type: "int", nullable: true),
                    Rating = table.Column<byte>(type: "tinyint", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionReviews", x => x.SessionReviewId);
                    table.CheckConstraint("CK_SessionReviews_Rating", "[Rating] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_SessionReviews_BcUsers_RevieweeBcUserId",
                        column: x => x.RevieweeBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionReviews_BcUsers_ReviewerBcUserId",
                        column: x => x.ReviewerBcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionReviews_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_StudentBcUserId",
                table: "Bookings",
                column: "StudentBcUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistory_BookingId_ChangedAt",
                table: "BookingStatusHistory",
                columns: new[] { "BookingId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistory_ChangedByBcUserId",
                table: "BookingStatusHistory",
                column: "ChangedByBcUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionExecutions_ExpectedCompletionAt",
                table: "SessionExecutions",
                column: "ExpectedCompletionAt");

            migrationBuilder.CreateIndex(
                name: "IX_SessionMessages_BookingId_SentAt",
                table: "SessionMessages",
                columns: new[] { "BookingId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionMessages_SenderBcUserId",
                table: "SessionMessages",
                column: "SenderBcUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionReviews_BookingId_ReviewerBcUserId",
                table: "SessionReviews",
                columns: new[] { "BookingId", "ReviewerBcUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionReviews_RevieweeBcUserId",
                table: "SessionReviews",
                column: "RevieweeBcUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionReviews_ReviewerBcUserId",
                table: "SessionReviews",
                column: "ReviewerBcUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_BcUsers_StudentBcUserId",
                table: "Bookings",
                column: "StudentBcUserId",
                principalTable: "BcUsers",
                principalColumn: "BcUserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_BcUsers_StudentBcUserId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "BookingStatusHistory");

            migrationBuilder.DropTable(
                name: "SessionExecutions");

            migrationBuilder.DropTable(
                name: "SessionMessages");

            migrationBuilder.DropTable(
                name: "SessionReviews");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_StudentBcUserId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "StudentBcUserId",
                table: "Bookings");
        }
    }
}

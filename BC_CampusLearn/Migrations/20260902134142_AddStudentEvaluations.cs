using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentEvaluations",
                columns: table => new
                {
                    StudentEvaluationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    TutoringMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlatformExperience = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ModeRating = table.Column<byte>(type: "tinyint", nullable: false),
                    TutorResponse = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TutorInterest = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TutorFriendliness = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TutorExplanation = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TutorParticipation = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TutorPunctuality = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TutorAdvice = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TutorHelp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TutorTopic = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TutoringService = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ImproveBCProgramme = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PlatformRating = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentEvaluations", x => x.StudentEvaluationId);
                    table.CheckConstraint("CK_StudentEvaluations_ModeRating", "[ModeRating] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_StudentEvaluations_PlatformRating", "[PlatformRating] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_StudentEvaluations_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentEvaluations_BookingId",
                table: "StudentEvaluations",
                column: "BookingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentEvaluations");
        }
    }
}

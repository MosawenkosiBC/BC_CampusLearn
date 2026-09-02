using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorStudentEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorStudentEvaluations",
                columns: table => new
                {
                    TutorEvaluationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<int>(type: "int", nullable: false),
                    SessionPlan = table.Column<bool>(type: "bit", nullable: false),
                    StudentPreparationInfo = table.Column<bool>(type: "bit", nullable: false),
                    StudentPunctuality = table.Column<bool>(type: "bit", nullable: false),
                    StudentPrepared = table.Column<bool>(type: "bit", nullable: false),
                    PreviousHomework = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    StudentInteract = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    StudentFocus = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    StudentIssues = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    TutorComments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordingLink = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorStudentEvaluations", x => x.TutorEvaluationId);
                    table.ForeignKey(
                        name: "FK_TutorStudentEvaluations_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorStudentEvaluations_BookingId",
                table: "TutorStudentEvaluations",
                column: "BookingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorStudentEvaluations");
        }
    }
}

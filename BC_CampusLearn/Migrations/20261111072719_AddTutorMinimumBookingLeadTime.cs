using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorMinimumBookingLeadTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumBookingLeadTimeMinutes",
                table: "Tutors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tutors_MinimumBookingLeadTimeMinutes",
                table: "Tutors",
                sql: "[MinimumBookingLeadTimeMinutes] BETWEEN 0 AND 525600");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Tutors_MinimumBookingLeadTimeMinutes",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "MinimumBookingLeadTimeMinutes",
                table: "Tutors");
        }
    }
}

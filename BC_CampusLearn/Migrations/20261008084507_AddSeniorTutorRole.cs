using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddSeniorTutorRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BcUsers_Role",
                table: "BcUsers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BcUsers_Role",
                table: "BcUsers",
                sql: "[Role] BETWEEN 1 AND 7");

            // Value 6 previously identified Developer accounts.
            migrationBuilder.Sql("UPDATE [BcUsers] SET [Role] = 7 WHERE [Role] = 6;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BcUsers_Role",
                table: "BcUsers");

            // Senior Tutors return to Tutor; Developers retain their original value.
            migrationBuilder.Sql("UPDATE [BcUsers] SET [Role] = 2 WHERE [Role] = 6;");
            migrationBuilder.Sql("UPDATE [BcUsers] SET [Role] = 6 WHERE [Role] = 7;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BcUsers_Role",
                table: "BcUsers",
                sql: "[Role] BETWEEN 1 AND 6");
        }
    }
}

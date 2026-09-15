using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddBcUserRolesAndAdmins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "BcUsers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE users
                SET users.Role = 2
                FROM BcUsers AS users
                INNER JOIN Tutors AS tutors
                    ON tutors.BcUserId = users.BcUserId
                WHERE tutors.Status = 1
                    AND tutors.IsActive = 1;
                """);

            migrationBuilder.CreateTable(
                name: "Admins",
                columns: table => new
                {
                    AdminId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BcUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admins", x => x.AdminId);
                    table.ForeignKey(
                        name: "FK_Admins_BcUsers_BcUserId",
                        column: x => x.BcUserId,
                        principalTable: "BcUsers",
                        principalColumn: "BcUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_BcUsers_Role",
                table: "BcUsers",
                sql: "[Role] BETWEEN 1 AND 6");

            migrationBuilder.CreateIndex(
                name: "IX_Admins_BcUserId",
                table: "Admins",
                column: "BcUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Admins");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BcUsers_Role",
                table: "BcUsers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "BcUsers");
        }
    }
}

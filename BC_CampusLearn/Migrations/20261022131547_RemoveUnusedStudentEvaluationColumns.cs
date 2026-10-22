using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedStudentEvaluationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImproveBCProgramme",
                table: "StudentEvaluations");

            migrationBuilder.DropColumn(
                name: "PlatformExperience",
                table: "StudentEvaluations");

            migrationBuilder.DropColumn(
                name: "TutorPunctuality",
                table: "StudentEvaluations");

            migrationBuilder.DropColumn(
                name: "TutorTopic",
                table: "StudentEvaluations");

            migrationBuilder.DropColumn(
                name: "TutoringService",
                table: "StudentEvaluations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImproveBCProgramme",
                table: "StudentEvaluations",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlatformExperience",
                table: "StudentEvaluations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TutorPunctuality",
                table: "StudentEvaluations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TutorTopic",
                table: "StudentEvaluations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TutoringService",
                table: "StudentEvaluations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }
    }
}

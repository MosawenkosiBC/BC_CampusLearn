using BC_CampusLearn.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261002100346_DropStudyAreaCampusName")]
public partial class DropStudyAreaCampusName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CampusName",
            table: "StudyAreas");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CampusName",
            table: "StudyAreas",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }
}

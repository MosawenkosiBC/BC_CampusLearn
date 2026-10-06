using System;
using BC_CampusLearn.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261006123000_AddTutorDeregistration")]
public partial class AddTutorDeregistration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>("DeregisteredAt", "Tutors", type: "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<int>("DeregisteredByBcUserId", "Tutors", type: "int", nullable: true);
        migrationBuilder.AddColumn<string>("DeregisteredByName", "Tutors", type: "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>("DeregistrationReason", "Tutors", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("DeregisteredAt", "Tutors");
        migrationBuilder.DropColumn("DeregisteredByBcUserId", "Tutors");
        migrationBuilder.DropColumn("DeregisteredByName", "Tutors");
        migrationBuilder.DropColumn("DeregistrationReason", "Tutors");
    }
}

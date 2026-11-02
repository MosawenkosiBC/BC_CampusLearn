using BC_CampusLearn.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261024100000_AddSeniorApplicationHeadDecision")]
public sealed class AddSeniorApplicationHeadDecision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HeadOfTutorApproved",
            table: "SeniorTutorApplications",
            type: "bit",
            nullable: true);
        migrationBuilder.Sql("""
            UPDATE [SeniorTutorApplications]
            SET [HeadOfTutorApproved] = CASE WHEN [Status] = 1 THEN 1 ELSE 0 END
            WHERE [IsHeadOfTutorApplication] = 1 AND [Status] <> 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn("HeadOfTutorApproved", "SeniorTutorApplications");
}

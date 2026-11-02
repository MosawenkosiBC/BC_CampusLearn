using BC_CampusLearn.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261023110000_AddHeadOfTutorChoiceToSeniorApplications")]
public sealed class AddHeadOfTutorChoiceToSeniorApplications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsHeadOfTutorApplication",
            table: "SeniorTutorApplications",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AlterColumn<string>(
            name: "BestDescription",
            table: "SeniorTutorApplications",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(50)",
            oldMaxLength: 50);

        migrationBuilder.AlterColumn<string>(
            name: "SuitabilityReason",
            table: "SeniorTutorApplications",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(2000)",
            oldMaxLength: 2000);

        // Applications created before this choice was introduced were all
        // explicitly submitted for the Head of Tutor role.
        migrationBuilder.Sql(
            "UPDATE [SeniorTutorApplications] SET [IsHeadOfTutorApplication] = 1;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [SeniorTutorApplications]
            SET [BestDescription] = COALESCE([BestDescription], ''),
                [SuitabilityReason] = COALESCE([SuitabilityReason], '');
            """);

        migrationBuilder.DropColumn(
            name: "IsHeadOfTutorApplication",
            table: "SeniorTutorApplications");

        migrationBuilder.AlterColumn<string>(
            name: "BestDescription",
            table: "SeniorTutorApplications",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "nvarchar(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "SuitabilityReason",
            table: "SeniorTutorApplications",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "nvarchar(2000)",
            oldMaxLength: 2000,
            oldNullable: true);
    }
}

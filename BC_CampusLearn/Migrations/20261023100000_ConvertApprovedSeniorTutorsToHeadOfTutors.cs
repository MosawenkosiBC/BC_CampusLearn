using BC_CampusLearn.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261023100000_ConvertApprovedSeniorTutorsToHeadOfTutors")]
public sealed class ConvertApprovedSeniorTutorsToHeadOfTutors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [users]
            SET [users].[Role] = 3
            FROM [BcUsers] AS [users]
            INNER JOIN [Tutors] AS [tutors]
                ON [tutors].[BcUserId] = [users].[BcUserId]
            WHERE [users].[Role] = 6
                AND EXISTS (
                    SELECT 1
                    FROM [SeniorTutorApplications] AS [applications]
                    WHERE [applications].[TutorId] = [tutors].[TutorId]
                        AND [applications].[Status] = 1
                );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [users]
            SET [users].[Role] = 6
            FROM [BcUsers] AS [users]
            INNER JOIN [Tutors] AS [tutors]
                ON [tutors].[BcUserId] = [users].[BcUserId]
            WHERE [users].[Role] = 3
                AND EXISTS (
                    SELECT 1
                    FROM [SeniorTutorApplications] AS [applications]
                    WHERE [applications].[TutorId] = [tutors].[TutorId]
                        AND [applications].[Status] = 1
                );
            """);
    }
}

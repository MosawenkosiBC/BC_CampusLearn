using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class BackfillApprovedSeniorTutorRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [users]
                SET [users].[Role] = 6
                FROM [BcUsers] AS [users]
                INNER JOIN [Tutors] AS [tutors]
                    ON [tutors].[BcUserId] = [users].[BcUserId]
                WHERE [users].[Role] = 2
                    AND EXISTS (
                        SELECT 1
                        FROM [SeniorTutorApplications] AS [applications]
                        WHERE [applications].[TutorId] = [tutors].[TutorId]
                            AND [applications].[Status] = 1
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [users]
                SET [users].[Role] = 2
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
    }
}

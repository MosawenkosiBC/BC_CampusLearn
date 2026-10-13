using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDemotedSeniorTutorApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE [applications]
                FROM [SeniorTutorApplications] AS [applications]
                INNER JOIN [Tutors] AS [tutors]
                    ON [tutors].[TutorId] = [applications].[TutorId]
                INNER JOIN [BcUsers] AS [users]
                    ON [users].[BcUserId] = [tutors].[BcUserId]
                WHERE [users].[Role] <> 6
                    AND EXISTS (
                        SELECT 1
                        FROM [SeniorTutorApplications] AS [approved]
                        WHERE [approved].[TutorId] = [tutors].[TutorId]
                            AND [approved].[Status] = 1
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deleted application records cannot be reconstructed safely.
        }
    }
}

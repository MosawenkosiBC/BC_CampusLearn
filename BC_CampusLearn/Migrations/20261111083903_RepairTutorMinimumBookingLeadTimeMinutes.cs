using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class RepairTutorMinimumBookingLeadTimeMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Tutors', 'MinimumBookingLeadTimeHours') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM sys.check_constraints
                        WHERE [name] = N'CK_Tutors_MinimumBookingLeadTimeHours'
                          AND [parent_object_id] = OBJECT_ID(N'dbo.Tutors'))
                    BEGIN
                        ALTER TABLE [dbo].[Tutors]
                            DROP CONSTRAINT [CK_Tutors_MinimumBookingLeadTimeHours];
                    END;

                    IF COL_LENGTH('dbo.Tutors', 'MinimumBookingLeadTimeMinutes') IS NULL
                    BEGIN
                        EXEC sp_rename
                            N'[dbo].[Tutors].[MinimumBookingLeadTimeHours]',
                            N'MinimumBookingLeadTimeMinutes',
                            N'COLUMN';

                        EXEC(N'
                            UPDATE [dbo].[Tutors]
                            SET [MinimumBookingLeadTimeMinutes] =
                                [MinimumBookingLeadTimeMinutes] * 60;
                        ');
                    END
                    ELSE
                    BEGIN
                        EXEC(N'
                            UPDATE [dbo].[Tutors]
                            SET [MinimumBookingLeadTimeMinutes] =
                                [MinimumBookingLeadTimeHours] * 60
                            WHERE [MinimumBookingLeadTimeHours] > 0;
                        ');
                    END;
                END;

                IF COL_LENGTH('dbo.Tutors', 'MinimumBookingLeadTimeMinutes') IS NULL
                BEGIN
                    EXEC(N'
                        ALTER TABLE [dbo].[Tutors]
                        ADD [MinimumBookingLeadTimeMinutes] int NOT NULL
                            CONSTRAINT [DF_Tutors_MinimumBookingLeadTimeMinutes]
                            DEFAULT 0;
                    ');
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.check_constraints
                    WHERE [name] = N'CK_Tutors_MinimumBookingLeadTimeMinutes'
                      AND [parent_object_id] = OBJECT_ID(N'dbo.Tutors'))
                BEGIN
                    EXEC(N'
                        ALTER TABLE [dbo].[Tutors]
                        ADD CONSTRAINT [CK_Tutors_MinimumBookingLeadTimeMinutes]
                            CHECK ([MinimumBookingLeadTimeMinutes]
                                BETWEEN 0 AND 525600);
                    ');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This compatibility repair is intentionally irreversible. On a
            // clean database it is a no-op; on an upgraded database it may
            // convert stored hours to minutes.
        }
    }
}

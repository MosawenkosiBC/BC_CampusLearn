using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class SplitTutorPersonalAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "TutorPersonalInformation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "TutorPersonalInformation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "South Africa");

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "TutorPersonalInformation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                table: "TutorPersonalInformation",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Suburb",
                table: "TutorPersonalInformation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE [TutorPersonalInformation]
                SET [StreetAddress] = LEFT([PhysicalHomeAddress], 200)
                WHERE [StreetAddress] = N'';
                """);

            // Keep the legacy column as a compatibility backup. It is no longer
            // mapped by EF, but retaining it makes this migration non-destructive.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE [TutorPersonalInformation]
                SET [PhysicalHomeAddress] = LEFT(CONCAT_WS(N', ',
                    NULLIF([StreetAddress], N''),
                    NULLIF([Suburb], N''),
                    NULLIF([City], N''),
                    NULLIF([Province], N''),
                    NULLIF([Country], N'')), 500);
                """);

            migrationBuilder.DropColumn(name: "City", table: "TutorPersonalInformation");
            migrationBuilder.DropColumn(name: "Country", table: "TutorPersonalInformation");
            migrationBuilder.DropColumn(name: "Province", table: "TutorPersonalInformation");
            migrationBuilder.DropColumn(name: "StreetAddress", table: "TutorPersonalInformation");
            migrationBuilder.DropColumn(name: "Suburb", table: "TutorPersonalInformation");
        }
    }
}

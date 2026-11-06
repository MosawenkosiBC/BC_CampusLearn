using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorPersonalInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorPersonalInformation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    Surname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FullNames = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhysicalHomeAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IncomeTaxNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HomeLanguage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdentityOrPassportNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WorkPermitExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AsylumNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmergencyContactPrimaryPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmergencyContactRelationship = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SignatureName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorPersonalInformation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorPersonalInformation_Tutors_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutors",
                        principalColumn: "TutorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorPersonalInformation_TutorId",
                table: "TutorPersonalInformation",
                column: "TutorId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TutorPersonalInformation");
        }
    }
}

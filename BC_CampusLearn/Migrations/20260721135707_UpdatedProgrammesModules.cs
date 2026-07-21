using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedProgrammesModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ProgrammeModule",
                columns: new[] { "ModuleCode", "ProgrammeId", "ModuleName", "YearOfStudy" },
                values: new object[,]
                {
                    { "D-AIT361", 4, "Applied Information Technology 361", 4 },
                    { "D-BME161", 4, "Business Management & Entrepreneurship 161", 1 },
                    { "D-BUC161", 4, "Business Communication 161", 1 },
                    { "D-COA161", 4, "Computer Architecture 161", 1 },
                    { "D-DBA261", 4, "Database Administration 261", 3 },
                    { "D-DBC161", 4, "Database Concepts 161", 1 },
                    { "D-DBD261", 4, "Database Development 261", 2 },
                    { "D-DBD262", 4, "Database Development 262", 3 },
                    { "D-DBF161", 4, "Database Functionality 161", 1 },
                    { "D-DBR261", 4, "Database Reporting 261", 3 },
                    { "D-ERP261", 4, "Enterprise Systems 261", 2 },
                    { "D-EUC161", 4, "End-User Computing 161", 1 },
                    { "D-ILE261", 4, "IT Law & Ethics 261", 2 },
                    { "D-INL161", 4, "Innovation and Leadership 161", 1 },
                    { "D-INL261", 4, "Innovation and Leadership 261", 2 },
                    { "D-INL361", 4, "Innovation & Leadership 361", 4 },
                    { "D-IOT161", 4, "Internet of Things 161", 1 },
                    { "D-NWD161", 4, "Network Development 161", 1 },
                    { "D-PMM261", 4, "Project Management 261", 2 },
                    { "D-PRG161", 4, "Programming 161", 1 },
                    { "D-PRG261", 4, "Programming 261", 2 },
                    { "D-PRG262", 4, "Programming 262", 2 },
                    { "D-PRJ361", 4, "Project 361", 4 },
                    { "D-PRL161", 4, "Programming Preliminaries 161", 1 },
                    { "D-STA161", 4, "Statistics 161", 1 },
                    { "D-SWA261", 4, "Software Analysis & Design 261", 2 },
                    { "D-SWT261", 4, "Software Testing 261", 2 },
                    { "D-SWT262", 4, "Software Testing 262", 2 },
                    { "D-UXD261", 4, "User Experience & Design 261", 2 },
                    { "D-WDB361", 4, "Web Database 361", 4 },
                    { "D-WFS361", 4, "Web Front-End Scripting 361", 3 },
                    { "D-WPR161", 4, "Web Programming 161", 1 },
                    { "D-WPR261", 4, "Web Programming 261", 2 },
                    { "D-WSE361", 4, "Web Servers 361", 4 },
                    { "D-WSP361", 4, "Work-Simulation Project 361", 4 },
                    { "MAT161", 4, "Applied Mathematics 161", 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-AIT361", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-BME161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-BUC161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-COA161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-DBA261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-DBC161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-DBD261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-DBD262", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-DBF161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-DBR261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-ERP261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-EUC161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-ILE261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-INL161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-INL261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-INL361", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-IOT161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-NWD161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-PMM261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-PRG161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-PRG261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-PRG262", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-PRJ361", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-PRL161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-STA161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-SWA261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-SWT261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-SWT262", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-UXD261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-WDB361", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-WFS361", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-WPR161", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-WPR261", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-WSE361", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "D-WSP361", 4 });

            migrationBuilder.DeleteData(
                table: "ProgrammeModule",
                keyColumns: new[] { "ModuleCode", "ProgrammeId" },
                keyValues: new object[] { "MAT161", 4 });
        }
    }
}

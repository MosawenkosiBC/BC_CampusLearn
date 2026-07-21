using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class Modules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgrammeModule",
                columns: table => new
                {
                    ProgrammeId = table.Column<int>(type: "int", nullable: false),
                    ModuleCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    YearOfStudy = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgrammeModule", x => new { x.ProgrammeId, x.ModuleCode });
                    table.ForeignKey(
                        name: "FK_ProgrammeModule_ProgrammeOfStudy_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalTable: "ProgrammeOfStudy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ProgrammeModule",
                columns: new[] { "ModuleCode", "ProgrammeId", "ModuleName", "YearOfStudy" },
                values: new object[,]
                {
                    { "ACW181", 1, "Academic Writing 181", 1 },
                    { "AIT481", 1, "Applied Information Technology 481", 4 },
                    { "AIT482", 1, "Applied Information Technology 482", 4 },
                    { "BIN381", 1, "Data Science 381", 3 },
                    { "BUM181", 1, "Business Management 181", 1 },
                    { "COA181", 1, "Computer Architecture 181", 1 },
                    { "DBA381", 1, "Database Administration 381", 3 },
                    { "DBD181", 1, "Database Development 181", 1 },
                    { "DBD281", 1, "Database Development 281", 2 },
                    { "DBD381", 1, "Database Development 381", 3 },
                    { "DST481", 1, "Dissertation 481", 4 },
                    { "DWH281", 1, "Data Warehousing 281", 2 },
                    { "ENT181", 1, "Entrepreneurship 181", 1 },
                    { "INF181", 1, "Information Systems 181", 1 },
                    { "INF281", 1, "Information Systems 281", 2 },
                    { "INL101", 1, "Innovation and Leadership 101", 1 },
                    { "INL102", 1, "Innovation and Leadership 102", 1 },
                    { "INL201", 1, "Innovation and Leadership 201", 2 },
                    { "INL202", 1, "Innovation and Leadership 202", 2 },
                    { "INL321", 1, "Innovation and Leadership 321", 3 },
                    { "INM381", 1, "Innovation Management 381", 3 },
                    { "IOT281", 1, "Internet Of Things 281", 2 },
                    { "LPR181", 1, "Linear Programming 181", 1 },
                    { "LPR281", 1, "Linear Programming 281", 2 },
                    { "LPR381", 1, "Linear Programming 381", 3 },
                    { "MAT181", 1, "Mathematics 181", 1 },
                    { "MAT281", 1, "Mathematics 281", 2 },
                    { "MLG381", 1, "Machine Learning 381", 3 },
                    { "MLG382", 1, "Machine Learning 382", 3 },
                    { "NWD181", 1, "Networking Development 181", 1 },
                    { "PMM281", 1, "Project Management 281", 2 },
                    { "PMM381", 1, "Project Management 381", 3 },
                    { "PRG181", 1, "Programming 181", 1 },
                    { "PRG182", 1, "Programming 182", 1 },
                    { "PRG281", 1, "Programming 281", 2 },
                    { "PRG282", 1, "Programming 282", 2 },
                    { "PRG381", 1, "Programming 381", 3 },
                    { "PRJ381", 1, "Project 381", 3 },
                    { "RSH381", 1, "Research Methods 381", 3 },
                    { "SAD281", 1, "Software Analysis & Design 281", 2 },
                    { "SEN381", 1, "Software Engineering 381", 3 },
                    { "STA181", 1, "Statistics 181", 1 },
                    { "STA281", 1, "Statistics 281", 2 },
                    { "STA381", 1, "Statistics 381", 3 },
                    { "SWT281", 1, "Software Testing 281", 2 },
                    { "UAX381", 1, "User Experience Design 381", 3 },
                    { "WPR181", 1, "Web Programming 181", 1 },
                    { "WPR281", 1, "Web Programming 281", 2 },
                    { "WPR381", 1, "Web Programming 381", 3 },
                    { "ACW171", 2, "Academic Writing 171", 1 },
                    { "BIN371", 2, "Business Intelligence 371", 3 },
                    { "BUM171", 2, "Business Management 171", 1 },
                    { "CNA271", 2, "Cloud-Native Application Architecture 271", 2 },
                    { "CNA371", 2, "Cloud-Native Application Programming 371", 3 },
                    { "COA171", 2, "Computer Architecture 171", 1 },
                    { "DAL371", 2, "Data Analytics 371", 3 },
                    { "DBD171", 2, "Database Development 171", 1 },
                    { "DBD221", 2, "Database Development 221", 2 },
                    { "DBD371", 2, "Database Development 371", 3 },
                    { "ENG171", 2, "English Communication 171", 1 },
                    { "ENT171", 2, "Entrepreneurship 171", 1 },
                    { "ERP271", 2, "Enterprise Systems 271", 2 },
                    { "ETH271", 2, "Ethics 271", 2 },
                    { "INF171", 2, "Information Systems 171", 1 },
                    { "INF271", 2, "Information Systems 271", 2 },
                    { "INL101", 2, "Innovation and Leadership 101", 1 },
                    { "INL102", 2, "Innovation and Leadership 102", 1 },
                    { "INL201", 2, "Innovation and Leadership 201", 2 },
                    { "INL202", 2, "Innovation and Leadership 202", 2 },
                    { "INL371", 2, "Innovation and Leadership 371", 3 },
                    { "INM371", 2, "Innovation Management 371", 3 },
                    { "IOT271", 2, "Internet Of Things 271", 2 },
                    { "LPR171", 2, "Linear Programming 171", 2 },
                    { "MAT171", 2, "Mathematics 171", 1 },
                    { "NWD171", 2, "Networking Development 171", 1 },
                    { "PMM271", 2, "Project Management 271", 2 },
                    { "PMM371", 2, "Project Management 371", 3 },
                    { "PRG171", 2, "Programming 171", 1 },
                    { "PRG172", 2, "Programming 172", 1 },
                    { "PRG271", 2, "Programming 271", 2 },
                    { "PRG272", 2, "Programming 272", 2 },
                    { "PRG371", 2, "Programming 371", 3 },
                    { "PRJ371", 2, "Project 371", 3 },
                    { "SAD371", 2, "Software Analysis & Design 371", 3 },
                    { "SEN371", 2, "Software Engineering 371", 3 },
                    { "STA171", 2, "Statistics 171", 1 },
                    { "STA271", 2, "Statistics 271", 2 },
                    { "SWT271", 2, "Software Testing 271", 2 },
                    { "UAX371", 2, "User Experience Design 371", 3 },
                    { "WPR171", 2, "Web Programming 171", 1 },
                    { "WPR271", 2, "Web Programming 271", 2 },
                    { "WPR371", 2, "Web Programming 371", 3 },
                    { "BME161", 3, "Business Management and Entrepreneurship 161", 1 },
                    { "BUC161", 3, "Business Communication 161", 1 },
                    { "CNA261", 3, "Cloud-Native Application Architecture 261", 2 },
                    { "COA161", 3, "Computer Architecture 161", 1 },
                    { "DBA261", 3, "Database Administration 261", 2 },
                    { "DBC161", 3, "Database Concept 161", 1 },
                    { "DBD261", 3, "Database Development 261", 2 },
                    { "DBD262", 3, "Database Development 262", 2 },
                    { "DBF161", 3, "Database Functionality 161", 1 },
                    { "DBR261", 3, "Database Reporting 261", 2 },
                    { "ERP261", 3, "Enterprise Systems 261", 2 },
                    { "EUC161", 3, "End User Computing 161", 1 },
                    { "ILE261", 3, "IT Law and Ethics 261", 2 },
                    { "INL161", 3, "Innovation and Leadership 161", 1 },
                    { "INL261", 3, "Innovation and Leadership 261", 2 },
                    { "IOT161", 3, "Internet of Things 161", 1 },
                    { "IOT261", 3, "Internet of Things 261", 2 },
                    { "MAT161", 3, "Mathematics 161", 1 },
                    { "NWD161", 3, "Network Development 161", 1 },
                    { "OPS261", 3, "Operating Systems 261", 2 },
                    { "OPS262", 3, "Operating Systems 262", 2 },
                    { "OPS263", 3, "Operating Systems 263", 2 },
                    { "PMM261", 3, "Project Management 261", 2 },
                    { "PRG161", 3, "Programming 161", 1 },
                    { "PRG261", 3, "Programming 261", 2 },
                    { "PRG262", 3, "Programming 262", 2 },
                    { "PRL161", 3, "Programming Preliminaries 161", 1 },
                    { "SEC261", 3, "Security 261", 2 },
                    { "STA161", 3, "Statistics 161", 1 },
                    { "SWA261", 3, "Software Analysis and Design 261", 2 },
                    { "SWT261", 3, "Software Testing 261", 2 },
                    { "SWT262", 3, "Software Testing 262 (Elective)", 2 },
                    { "UXD261", 3, "User Experience Design 261 (Elective)", 2 },
                    { "WPR161", 3, "Web Programming 161", 1 },
                    { "WPR261", 3, "Web Programming 261", 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgrammeModule");
        }
    }
}

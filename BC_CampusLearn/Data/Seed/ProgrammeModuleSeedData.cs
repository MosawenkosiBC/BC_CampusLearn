using BC_CampusLearn.Models.Entities;

namespace BC_CampusLearn.Data.Seed
{
    public class ProgrammeModuleSeedData
    {
        private const int BachelorOfComputingId = 1;
        private const int BachelorOfInformationTechnologyId = 2;
        private const int DiplomaInInformationTechnologyId = 3;
        private const int DiplomaForDeafStudentsId = 4;

        public static ProgrammeModule[] GetModules()
        {
            return new[]
            {
                /*
                 * ==========================================================
                 * BACHELOR OF COMPUTING
                 * ProgrammeId: 1
                 * ==========================================================
                 */
 
                // First Academic Year
                Create(
                    BachelorOfComputingId,
                    "Academic Writing 181",
                    "ACW181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Computer Architecture 181",
                    "COA181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Database Development 181",
                    "DBD181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Information Systems 181",
                    "INF181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Innovation and Leadership 101",
                    "INL101",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Innovation and Leadership 102",
                    "INL102",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Linear Programming 181",
                    "LPR181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Mathematics 181",
                    "MAT181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Networking Development 181",
                    "NWD181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Programming 181",
                    "PRG181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Programming 182",
                    "PRG182",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Statistics 181",
                    "STA181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Web Programming 181",
                    "WPR181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Business Management 181",
                    "BUM181",
                    1),

                Create(
                    BachelorOfComputingId,
                    "Entrepreneurship 181",
                    "ENT181",
                    1),
 
                // Second Academic Year
                Create(
                    BachelorOfComputingId,
                    "Database Development 281",
                    "DBD281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Information Systems 281",
                    "INF281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Innovation and Leadership 201",
                    "INL201",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Innovation and Leadership 202",
                    "INL202",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Linear Programming 281",
                    "LPR281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Mathematics 281",
                    "MAT281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Programming 281",
                    "PRG281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Programming 282",
                    "PRG282",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Project Management 281",
                    "PMM281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Statistics 281",
                    "STA281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Web Programming 281",
                    "WPR281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Software Analysis & Design 281",
                    "SAD281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Data Warehousing 281",
                    "DWH281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Internet Of Things 281",
                    "IOT281",
                    2),

                Create(
                    BachelorOfComputingId,
                    "Software Testing 281",
                    "SWT281",
                    2),
 
                // Third Academic Year
                Create(
                    BachelorOfComputingId,
                    "Research Methods 381",
                    "RSH381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Database Development 381",
                    "DBD381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Innovation and Leadership 321",
                    "INL321",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Linear Programming 381",
                    "LPR381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Machine Learning 381",
                    "MLG381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Project 381",
                    "PRJ381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Project Management 381",
                    "PMM381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Programming 381",
                    "PRG381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Software Engineering 381",
                    "SEN381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Web Programming 381",
                    "WPR381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Data Science 381",
                    "BIN381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Database Administration 381",
                    "DBA381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Statistics 381",
                    "STA381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Innovation Management 381",
                    "INM381",
                    3),

                Create(
                    BachelorOfComputingId,
                    "Machine Learning 382",
                    "MLG382",
                    3),

                Create(
                    BachelorOfComputingId,
                    "User Experience Design 381",
                    "UAX381",
                    3),
 
                // Fourth Experiential Learning Year
                Create(
                    BachelorOfComputingId,
                    "Applied Information Technology 481",
                    "AIT481",
                    4),

                Create(
                    BachelorOfComputingId,
                    "Applied Information Technology 482",
                    "AIT482",
                    4),

                Create(
                    BachelorOfComputingId,
                    "Dissertation 481",
                    "DST481",
                    4),
 
                /*
                 * ==========================================================
                 * BACHELOR OF INFORMATION TECHNOLOGY
                 * ProgrammeId: 2
                 * ==========================================================
                 */
 
                // First Academic Year
                Create(
                    BachelorOfInformationTechnologyId,
                    "Academic Writing 171",
                    "ACW171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Computer Architecture 171",
                    "COA171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Database Development 171",
                    "DBD171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "English Communication 171",
                    "ENG171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Information Systems 171",
                    "INF171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Innovation and Leadership 101",
                    "INL101",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Innovation and Leadership 102",
                    "INL102",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Mathematics 171",
                    "MAT171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Networking Development 171",
                    "NWD171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Programming 171",
                    "PRG171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Programming 172",
                    "PRG172",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Statistics 171",
                    "STA171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Web Programming 171",
                    "WPR171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Business Management 171",
                    "BUM171",
                    1),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Entrepreneurship 171",
                    "ENT171",
                    1),
 
                // Second Academic Year
                Create(
                    BachelorOfInformationTechnologyId,
                    "Cloud-Native Application Architecture 271",
                    "CNA271",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Database Development 221",
                    "DBD221",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Enterprise Systems 271",
                    "ERP271",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Ethics 271",
                    "ETH271",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Information Systems 271",
                    "INF271",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Innovation and Leadership 201",
                    "INL201",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Innovation and Leadership 202",
                    "INL202",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Linear Programming 171",
                    "LPR171",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Programming 271",
                    "PRG271",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Programming 272",
                    "PRG272",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Project Management 271",
                    "PMM271",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Statistics 271",
                    "STA271",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Web Programming 271",
                    "WPR271",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Internet Of Things 271",
                    "IOT271",
                    2),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Software Testing 271",
                    "SWT271",
                    2),
 
                // Third Academic Year
                Create(
                    BachelorOfInformationTechnologyId,
                    "Business Intelligence 371",
                    "BIN371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Cloud-Native Application Programming 371",
                    "CNA371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Data Analytics 371",
                    "DAL371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Database Development 371",
                    "DBD371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Innovation and Leadership 371",
                    "INL371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Programming 371",
                    "PRG371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Project 371",
                    "PRJ371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Project Management 371",
                    "PMM371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Software Analysis & Design 371",
                    "SAD371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Software Engineering 371",
                    "SEN371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Web Programming 371",
                    "WPR371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "Innovation Management 371",
                    "INM371",
                    3),

                Create(
                    BachelorOfInformationTechnologyId,
                    "User Experience Design 371",
                    "UAX371",
                    3),
 
                /*
                 * ==========================================================
                 * DIPLOMA IN INFORMATION TECHNOLOGY
                 * ProgrammeId: 3
                 * ==========================================================
                 */
 
                // First Academic Year
                Create(
                    DiplomaInInformationTechnologyId,
                    "Business Communication 161",
                    "BUC161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Business Management and Entrepreneurship 161",
                    "BME161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Computer Architecture 161",
                    "COA161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Database Concept 161",
                    "DBC161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Database Functionality 161",
                    "DBF161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "End User Computing 161",
                    "EUC161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Innovation and Leadership 161",
                    "INL161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Internet of Things 161",
                    "IOT161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Mathematics 161",
                    "MAT161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Network Development 161",
                    "NWD161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Programming 161",
                    "PRG161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Programming Preliminaries 161",
                    "PRL161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Web Programming 161",
                    "WPR161",
                    1),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Statistics 161",
                    "STA161",
                    1),
 
                // Second Academic Year: Core
                Create(
                    DiplomaInInformationTechnologyId,
                    "Database Development 261",
                    "DBD261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Enterprise Systems 261",
                    "ERP261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Innovation and Leadership 261",
                    "INL261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "IT Law and Ethics 261",
                    "ILE261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Project Management 261",
                    "PMM261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Database Development 262",
                    "DBD262",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Database Reporting 261",
                    "DBR261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Database Administration 261",
                    "DBA261",
                    2),
 
                // Second Academic Year: Infrastructure
                Create(
                    DiplomaInInformationTechnologyId,
                    "Cloud-Native Application Architecture 261",
                    "CNA261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Internet of Things 261",
                    "IOT261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Operating Systems 261",
                    "OPS261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Operating Systems 262",
                    "OPS262",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Operating Systems 263",
                    "OPS263",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Security 261",
                    "SEC261",
                    2),
 
                // Second Academic Year: Software Development
                Create(
                    DiplomaInInformationTechnologyId,
                    "Programming 261",
                    "PRG261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Programming 262",
                    "PRG262",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Software Analysis and Design 261",
                    "SWA261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Software Testing 261",
                    "SWT261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Software Testing 262 (Elective)",
                    "SWT262",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "User Experience Design 261 (Elective)",
                    "UXD261",
                    2),

                Create(
                    DiplomaInInformationTechnologyId,
                    "Web Programming 261",
                    "WPR261",
                    2),
                 /*
                * ==========================================================
                * DIPLOMA FOR DEAF STUDENTS
                * ProgrammeId: 4
                * ==========================================================
                */
 
                // First Academic Year
                Create(
                    DiplomaForDeafStudentsId,
                    "Innovation and Leadership 161",
                    "D-INL161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "End-User Computing 161",
                    "D-EUC161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Business Communication 161",
                    "D-BUC161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Business Management & Entrepreneurship 161",
                    "D-BME161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Programming Preliminaries 161",
                    "D-PRL161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Programming 161",
                    "D-PRG161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Web Programming 161",
                    "D-WPR161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Database Concepts 161",
                    "D-DBC161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Database Functionality 161",
                    "D-DBF161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Applied Mathematics 161",
                    "MAT161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Statistics 161",
                    "D-STA161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Computer Architecture 161",
                    "D-COA161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Network Development 161",
                    "D-NWD161",
                    1),

                Create(
                    DiplomaForDeafStudentsId,
                    "Internet of Things 161",
                    "D-IOT161",
                    1),
 
                // Second Academic Year
                Create(
                    DiplomaForDeafStudentsId,
                    "Innovation and Leadership 261",
                    "D-INL261",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "Enterprise Systems 261",
                    "D-ERP261",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "IT Law & Ethics 261",
                    "D-ILE261",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "Project Management 261",
                    "D-PMM261",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "Database Development 261",
                    "D-DBD261",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "Programming 261",
                    "D-PRG261",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "Programming 262",
                    "D-PRG262",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "Software Testing 261",
                    "D-SWT261",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "Software Analysis & Design 261",
                    "D-SWA261",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "Web Programming 261",
                    "D-WPR261",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "Software Testing 262",
                    "D-SWT262",
                    2),

                Create(
                    DiplomaForDeafStudentsId,
                    "User Experience & Design 261",
                    "D-UXD261",
                    2),
 
                // Third Academic Year
                Create(
                    DiplomaForDeafStudentsId,
                    "Database Development 262",
                    "D-DBD262",
                    3),

                Create(
                    DiplomaForDeafStudentsId,
                    "Database Reporting 261",
                    "D-DBR261",
                    3),

                Create(
                    DiplomaForDeafStudentsId,
                    "Database Administration 261",
                    "D-DBA261",
                    3),

                Create(
                    DiplomaForDeafStudentsId,
                    "Web Front-End Scripting 361",
                    "D-WFS361",
                    3),
 
                // Fourth Academic Year
                Create(
                    DiplomaForDeafStudentsId,
                    "Project 361",
                    "D-PRJ361",
                    4),

                Create(
                    DiplomaForDeafStudentsId,
                    "Innovation & Leadership 361",
                    "D-INL361",
                    4),

                Create(
                    DiplomaForDeafStudentsId,
                    "Applied Information Technology 361",
                    "D-AIT361",
                    4),

                Create(
                    DiplomaForDeafStudentsId,
                    "Work-Simulation Project 361",
                    "D-WSP361",
                    4),

                Create(
                    DiplomaForDeafStudentsId,
                    "Web Servers 361",
                    "D-WSE361",
                    4),

                Create(
                    DiplomaForDeafStudentsId,
                    "Web Database 361",
                    "D-WDB361",
                    4)

            };
        }

        private static ProgrammeModule Create(
            int programmeId,
            string moduleName,
            string moduleCode,
            int yearOfStudy)
        {
            return new ProgrammeModule
            {
                ProgrammeId = programmeId,
                ModuleName = moduleName,
                ModuleCode = moduleCode,
                YearOfStudy = yearOfStudy
            };
        }
    }
}


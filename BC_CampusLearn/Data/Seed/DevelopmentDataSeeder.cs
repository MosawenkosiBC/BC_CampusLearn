using BC_CampusLearn.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Data.Seed;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext context)
    {
        if (await context.Tutors.AnyAsync())
        {
            return;
        }

        ProgrammeModule programming =
            await GetProgrammeModuleAsync(
                context,
                programmeId: 1,
                moduleCode: "PRG181");

        ProgrammeModule database =
            await GetProgrammeModuleAsync(
                context,
                programmeId: 1,
                moduleCode: "DBD181");

        ProgrammeModule webDevelopment =
            await GetProgrammeModuleAsync(
                context,
                programmeId: 1,
                moduleCode: "WPR181");

        var tutorOne = new Tutor
        {
            BcUser = CreateUser("DEV-TUTOR-001", "11111111-1111-1111-1111-111111111101"),
            ProgrammeId = 1,
            Biography =
                "Software development tutor specialising in C# and ASP.NET.",
            ProfileImagePath =
                "/Media/tutorsProfiles/Tutor 1.webp",
            OverallAverage = 82.5m,
            YearOfStudy = 3,
            ReasonForTutoring = "I enjoy helping students master software development.",
            TeachingStyle = "Practical examples followed by guided exercises.",
            DemonstrationVideoUrl = "https://example.invalid/tutors/dev-tutor-001",
            Status = TutorStatus.Approved,
            IsActive = true,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var tutorTwo = new Tutor
        {
            BcUser = CreateUser("DEV-TUTOR-002", "11111111-1111-1111-1111-111111111102"),
            ProgrammeId = 1,
            Biography =
                "Database tutor specialising in SQL Server and data modelling.",
            ProfileImagePath =
                "/Media/tutorsProfiles/Tutor 3.webp",
            OverallAverage = 86m,
            YearOfStudy = 3,
            ReasonForTutoring = "I want to make database design approachable.",
            TeachingStyle = "Visual modelling and incremental SQL exercises.",
            DemonstrationVideoUrl = "https://example.invalid/tutors/dev-tutor-002",
            Status = TutorStatus.Approved,
            IsActive = true,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        tutorOne.TutorCourseModules.Add(
            new TutorCourseModule
            {
                Tutor = tutorOne,
                ProgrammeModule = programming
            });

        tutorOne.TutorCourseModules.Add(
            new TutorCourseModule
            {
                Tutor = tutorOne,
                ProgrammeModule = webDevelopment
            });

        tutorTwo.TutorCourseModules.Add(
            new TutorCourseModule
            {
                Tutor = tutorTwo,
                ProgrammeModule = database
            });

        TimeSpan southAfricaOffset =
            TimeSpan.FromHours(2);

        DateTime tomorrow =
            DateTime.Today.AddDays(1);

        tutorOne.TutorAvailabilities.Add(
            CreateSlot(
                tutorOne,
                programming,
                tomorrow,
                10,
                southAfricaOffset));

        tutorOne.TutorAvailabilities.Add(
            CreateSlot(
                tutorOne,
                webDevelopment,
                tomorrow,
                14,
                southAfricaOffset));

        tutorTwo.TutorAvailabilities.Add(
            CreateSlot(
                tutorTwo,
                database,
                tomorrow.AddDays(1),
                11,
                southAfricaOffset));

        tutorTwo.TutorAvailabilities.Add(
            CreateSlot(
                tutorTwo,
                database,
                tomorrow.AddDays(2),
                13,
                southAfricaOffset));

        context.Tutors.AddRange(tutorOne, tutorTwo);

        await context.SaveChangesAsync();
    }

    private static BcUser CreateUser(string personnelNumber, string objectId)
    {
        return new BcUser
        {
            PersonnelNumber = personnelNumber,
            EntraObjectId = Guid.Parse(objectId),
            EntraTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Task<ProgrammeModule> GetProgrammeModuleAsync(
        ApplicationDbContext context,
        int programmeId,
        string moduleCode)
    {
        return context.ProgrammeModules.SingleAsync(module =>
            module.ProgrammeId == programmeId &&
            module.ModuleCode == moduleCode);
    }

    private static TutorAvailability CreateSlot(
        Tutor tutor,
        ProgrammeModule programmeModule,
        DateTime date,
        int startHour,
        TimeSpan offset)
    {
        DateTime startDateTime =
            date.Date.AddHours(startHour);

        var start =
            new DateTimeOffset(startDateTime, offset);

        return new TutorAvailability
        {
            Tutor = tutor,
            ProgrammeModule = programmeModule,
            AvailableTime = start,
            IsActive = true
        };
    }
}

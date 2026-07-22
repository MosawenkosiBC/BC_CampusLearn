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
            DisplayName = "Thabo Test Tutor",
            Email = "thabo.test@belgiumcampus.ac.za",
            Biography =
                "Software development tutor specialising in C# and ASP.NET.",
            ProfileImagePath =
                "/Media/tutorsProfiles/Tutor 1.webp",
            IsApproved = true,
            IsActive = true
        };

        var tutorTwo = new Tutor
        {
            DisplayName = "Naledi Test Tutor",
            Email = "naledi.test@belgiumcampus.ac.za",
            Biography =
                "Database tutor specialising in SQL Server and data modelling.",
            ProfileImagePath =
                "/Media/tutorsProfiles/Tutor 3.webp",
            IsApproved = true,
            IsActive = true
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

        tutorOne.AvailabilitySlots.Add(
            CreateSlot(
                tutorOne,
                programming,
                tomorrow,
                10,
                southAfricaOffset));

        tutorOne.AvailabilitySlots.Add(
            CreateSlot(
                tutorOne,
                webDevelopment,
                tomorrow,
                14,
                southAfricaOffset));

        tutorTwo.AvailabilitySlots.Add(
            CreateSlot(
                tutorTwo,
                database,
                tomorrow.AddDays(1),
                11,
                southAfricaOffset));

        tutorTwo.AvailabilitySlots.Add(
            CreateSlot(
                tutorTwo,
                database,
                tomorrow.AddDays(2),
                13,
                southAfricaOffset));

        context.Tutors.AddRange(tutorOne, tutorTwo);

        await context.SaveChangesAsync();
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
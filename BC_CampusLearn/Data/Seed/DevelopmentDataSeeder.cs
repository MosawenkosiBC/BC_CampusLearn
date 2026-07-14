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

        var programming = new CourseModule
        {
            Code = "PROG",
            Name = "Programming"
        };

        var database = new CourseModule
        {
            Code = "DB",
            Name = "Database Development"
        };

        var webDevelopment = new CourseModule
        {
            Code = "WEB",
            Name = "Web Development"
        };

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
                CourseModule = programming
            });

        tutorOne.TutorCourseModules.Add(
            new TutorCourseModule
            {
                Tutor = tutorOne,
                CourseModule = webDevelopment
            });

        tutorTwo.TutorCourseModules.Add(
            new TutorCourseModule
            {
                Tutor = tutorTwo,
                CourseModule = database
            });

        TimeSpan southAfricaOffset =
            TimeSpan.FromHours(2);

        DateTime tomorrow =
            DateTime.Today.AddDays(1);

        tutorOne.AvailabilitySlots.Add(
            CreateSlot(
                tutorOne,
                tomorrow,
                10,
                southAfricaOffset));

        tutorOne.AvailabilitySlots.Add(
            CreateSlot(
                tutorOne,
                tomorrow,
                14,
                southAfricaOffset));

        tutorTwo.AvailabilitySlots.Add(
            CreateSlot(
                tutorTwo,
                tomorrow.AddDays(1),
                11,
                southAfricaOffset));

        tutorTwo.AvailabilitySlots.Add(
            CreateSlot(
                tutorTwo,
                tomorrow.AddDays(2),
                13,
                southAfricaOffset));

        context.Tutors.AddRange(tutorOne, tutorTwo);

        await context.SaveChangesAsync();
    }

    private static TutorAvailability CreateSlot(
        Tutor tutor,
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
            StartTime = start,
            EndTime = start.AddHours(1),
            IsBooked = false,
            IsActive = true
        };
    }
}
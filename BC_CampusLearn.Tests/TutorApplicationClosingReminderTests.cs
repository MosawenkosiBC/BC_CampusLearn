using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Tutors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class TutorApplicationClosingReminderTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(1)]
    public async Task QueuesOneReminderPerActiveSuperAdminOnDueDate(
        int daysRemaining)
    {
        await using ApplicationDbContext context = CreateContext();
        context.BcUsers.AddRange(
            User(1, "First Super Admin", "first@example.com", BcUserRole.SuperAdmin),
            User(2, "Second Super Admin", "second@example.com", BcUserRole.SuperAdmin),
            User(3, "Inactive Super Admin", "inactive@example.com", BcUserRole.SuperAdmin, false),
            User(4, "Administrator", "admin@example.com", BcUserRole.Admin));
        await context.SaveChangesAsync();

        DateTimeOffset now = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
        var settings = new TutorApplicationSettings
        {
            IsOpen = true,
            CloseDate = now.UtcDateTime.Date.AddDays(daysRemaining)
        };

        int queued = await TutorApplicationClosingReminderDispatcher
            .AddDueEmailsAsync(context, settings, now, CancellationToken.None);
        await context.SaveChangesAsync();

        List<EmailNotificationOutbox> emails = await context
            .EmailNotificationOutbox.OrderBy(email => email.RecipientEmail)
            .ToListAsync();
        Assert.Equal(2, queued);
        Assert.Equal(2, emails.Count);
        Assert.All(emails, email =>
        {
            Assert.Contains($"close in {daysRemaining}", email.Subject);
            Assert.Contains($"closes in {daysRemaining}", email.Body);
            Assert.Equal("/Administrator/Admin/Applications", email.ActionPath);
        });
        Assert.DoesNotContain(emails, email =>
            email.RecipientEmail is "inactive@example.com" or "admin@example.com");
    }

    [Fact]
    public async Task DoesNotQueueDuplicateReminderForSameCycleAndDay()
    {
        await using ApplicationDbContext context = CreateContext();
        context.BcUsers.Add(User(
            1, "Super Admin", "super@example.com", BcUserRole.SuperAdmin));
        await context.SaveChangesAsync();

        DateTimeOffset now = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
        var settings = new TutorApplicationSettings
        {
            IsOpen = true,
            CloseDate = now.UtcDateTime.Date.AddDays(3)
        };

        await TutorApplicationClosingReminderDispatcher.AddDueEmailsAsync(
            context, settings, now, CancellationToken.None);
        await context.SaveChangesAsync();
        await TutorApplicationClosingReminderDispatcher.AddDueEmailsAsync(
            context, settings, now.AddHours(1), CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.Equal(1, await context.EmailNotificationOutbox.CountAsync());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(2)]
    [InlineData(0)]
    public async Task DoesNotQueueOutsideReminderDates(int daysRemaining)
    {
        await using ApplicationDbContext context = CreateContext();
        context.BcUsers.Add(User(
            1, "Super Admin", "super@example.com", BcUserRole.SuperAdmin));
        await context.SaveChangesAsync();

        DateTimeOffset now = new(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
        var settings = new TutorApplicationSettings
        {
            IsOpen = true,
            CloseDate = now.UtcDateTime.Date.AddDays(daysRemaining)
        };

        int queued = await TutorApplicationClosingReminderDispatcher
            .AddDueEmailsAsync(context, settings, now, CancellationToken.None);

        Assert.Equal(0, queued);
        Assert.Empty(context.EmailNotificationOutbox);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static BcUser User(
        int id,
        string name,
        string email,
        BcUserRole role,
        bool active = true) => new()
        {
            BcUserId = id,
            DisplayName = name,
            Email = email,
            PersonnelNumber = $"USER{id}",
            Role = role,
            IsAdministrativeAccessActive = active
        };
}

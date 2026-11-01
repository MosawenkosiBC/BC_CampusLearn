using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class NotificationEmailQueueTests
{
    [Theory]
    [InlineData("pending")]
    [InlineData("sent")]
    [InlineData("failed")]
    public async Task ExistingEvent_PreservesOriginalEmailAndDeliveryState(string deliveryState)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        DateTimeOffset now = new(2026, 10, 29, 20, 0, 0, TimeSpan.Zero);
        await using (var originalContext = new ApplicationDbContext(options))
        {
            originalContext.EmailNotificationOutbox.Add(new EmailNotificationOutbox
            {
                EventKey = "booking:1:status:Completed:student",
                RecipientEmail = "student@example.com",
                Subject = "Original subject",
                Body = "Original body",
                CreatedAt = now.AddHours(-1),
                NextAttemptAt = now.AddMinutes(5),
                SentAt = deliveryState == "sent" ? now.AddMinutes(-10) : null,
                FailedAt = deliveryState == "failed" ? now.AddMinutes(-10) : null,
                AttemptCount = 2,
                LastError = "Previous attempt"
            });
            await originalContext.SaveChangesAsync();
        }
        await using var context = new ApplicationDbContext(options);

        NotificationEmailQueue.Add(context, "booking:1:status:Completed:student",
            "student@example.com", "Student", "Replacement", "Replacement", null, now);
        await context.SaveChangesAsync();

        EmailNotificationOutbox email = await context.EmailNotificationOutbox.SingleAsync();
        Assert.Equal("Original subject", email.Subject);
        Assert.Equal("Original body", email.Body);
        Assert.Equal(now.AddHours(-1), email.CreatedAt);
        Assert.Equal(now.AddMinutes(5), email.NextAttemptAt);
        Assert.Equal(deliveryState == "sent" ? now.AddMinutes(-10) : (DateTimeOffset?)null, email.SentAt);
        Assert.Equal(deliveryState == "failed" ? now.AddMinutes(-10) : (DateTimeOffset?)null, email.FailedAt);
        Assert.Equal(2, email.AttemptCount);
        Assert.Equal("Previous attempt", email.LastError);
    }

    [Fact]
    public async Task RepeatedEventsBeforeSave_QueueOncePerEventAndKeepDistinctRecipients()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            NotificationEmailQueue.Add(context, "booking:1:status:Completed:student",
                "student@example.com", "Student", "Completion", "Student body", null, now);
            NotificationEmailQueue.Add(context, "booking:1:completed-review:tutor",
                "tutor@example.com", "Tutor", "Review", "Tutor body", null, now);
        }
        await context.SaveChangesAsync();

        List<EmailNotificationOutbox> emails = await context.EmailNotificationOutbox.ToListAsync();
        Assert.Equal(2, emails.Count);
        Assert.All(emails, email =>
        {
            Assert.Null(email.SentAt);
            Assert.Null(email.FailedAt);
            Assert.Equal(now, email.NextAttemptAt);
        });
        Assert.Contains(emails, email => email.RecipientEmail == "student@example.com");
        Assert.Contains(emails, email => email.RecipientEmail == "tutor@example.com");
    }
}

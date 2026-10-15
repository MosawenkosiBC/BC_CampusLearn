using System.Net;
using System.Text;
using System.Text.Json;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Sessions;
using BC_CampusLearn.Services.Teams;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Xunit;

namespace BC_CampusLearn.Tests;

public sealed class TeamsMeetingTests
{
    private const string TenantId = "11111111-1111-1111-1111-111111111111";
    private const string ObjectId = "22222222-2222-2222-2222-222222222222";
    private const string JoinUrl = "https://teams.microsoft.com/l/meetup-join/test";

    [Fact]
    public async Task Create_SavesTutorEventAndLink_WithoutConfirmingBooking_AndReusesSavedMeeting()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        fixture.Context.ChangeTracker.Clear();
        MeetingLink link = await fixture.Context.MeetingLinks.SingleAsync();
        Assert.Equal(JoinUrl, link.Url);
        Assert.Equal("event-1", link.TeamsEventId);
        Assert.NotNull(link.TeamsCreationRequestId);
        Assert.Equal(fixture.Identities.CreateLookupHash(TenantId, ObjectId), link.TeamsOrganizerIdentityHash);
        Assert.Equal(BookingStatus.Pending, (await fixture.Context.Bookings.SingleAsync()).Status);
        Assert.Empty(fixture.Context.BookingStatusHistory);
        TeamsCalendarRequest request = Assert.Single(fixture.Calendar.Requests);
        Assert.Equal(ObjectId, request.OrganizerObjectId);
        Assert.Equal("student@institution.example", request.StudentEmail);
        Assert.Equal(fixture.Now.AddHours(2), request.Start);
        Assert.Equal(request.Start.AddHours(1), request.End);
        Assert.Equal("Mzala Connect tutoring session: Module", request.Subject);
        Assert.DoesNotContain("MOD101", request.Subject);
        Assert.True((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        Assert.Single(fixture.Calendar.Requests);
    }

    [Fact]
    public async Task Create_PendingJoinLink_RetryRetrievesSameEvent()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Calendar.Result = new("event-1", null);
        Assert.False((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal("event-1", (await fixture.Context.MeetingLinks.SingleAsync()).TeamsEventId);
        fixture.Calendar.Result = new("event-1", JoinUrl);
        Assert.True((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        Assert.Single(fixture.Calendar.Requests);
        Assert.Equal(1, fixture.Calendar.GetCalls);
    }

    [Fact]
    public async Task Create_AmbiguousFailure_RetryUsesPersistedTransactionId()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Calendar.FailCreate = true;
        Assert.False((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        fixture.Context.ChangeTracker.Clear();
        Guid? persistedId = (await fixture.Context.MeetingLinks.SingleAsync()).TeamsCreationRequestId;
        fixture.Calendar.FailCreate = false;
        Assert.True((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        Assert.Equal(2, fixture.Calendar.Requests.Count);
        Assert.All(fixture.Calendar.Requests, request => Assert.Equal(persistedId, request.TransactionId));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("not-allowlisted")]
    [InlineData("other-tutor")]
    [InlineData("inactive")]
    [InlineData("not-approved")]
    [InlineData("past")]
    [InlineData("cancelled")]
    [InlineData("missing-identity")]
    [InlineData("corrupt-identity")]
    [InlineData("other-tenant")]
    [InlineData("bad-email")]
    [InlineData("development-login")]
    [InlineData("manual-link")]
    public async Task Create_InvalidEligibility_DoesNotContactMicrosoft(string scenario)
    {
        await using var fixture = await Fixture.CreateAsync();
        Booking booking = await fixture.Context.Bookings.Include(item => item.StudyArea).SingleAsync();
        Tutor tutor = await fixture.Context.Tutors.Include(item => item.BcUser).SingleAsync();
        int actor = 8;
        switch (scenario)
        {
            case "disabled": fixture.Options.Enabled = false; break;
            case "not-allowlisted": fixture.Options.PilotTutorBcUserIds = []; break;
            case "other-tutor": actor = 9; fixture.Options.PilotTutorBcUserIds = [9]; break;
            case "inactive": tutor.IsActive = false; break;
            case "not-approved": tutor.Status = TutorStatus.Deregistered; break;
            case "past": booking.ScheduledStartTime = fixture.Now; break;
            case "cancelled": booking.Status = BookingStatus.Cancelled; break;
            case "missing-identity": tutor.BcUser.EncryptedEntraObjectId = null; break;
            case "corrupt-identity": tutor.BcUser.EncryptedEntraObjectId = "invalid"; break;
            case "other-tenant": fixture.Configuration["AzureAd:TenantId"] = Guid.NewGuid().ToString(); break;
            case "bad-email": booking.StudentEmail = "invalid"; break;
            case "development-login": fixture.Environment.EnvironmentName = "Development"; fixture.Configuration["Authentication:UseDevelopmentAuthentication"] = "true"; break;
            case "manual-link": booking.MeetingLink = new MeetingLink { Url = "https://example.com/existing" }; break;
        }
        await fixture.Context.SaveChangesAsync();
        Assert.False((await fixture.Service.CreateAsync(actor, 1)).Succeeded);
        Assert.Equal(0, fixture.Calendar.SupportCalls);
        Assert.Empty(fixture.Calendar.Requests);
    }

    [Fact]
    public async Task Create_UnsupportedCalendar_DoesNotCreateOrReserveMeeting()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Calendar.SupportsTeams = false;
        Assert.False((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        Assert.Empty(fixture.Context.MeetingLinks);
        Assert.Empty(fixture.Calendar.Requests);
    }

    [Fact]
    public async Task Confirm_CannotOverwriteGeneratedLink()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now));
        Assert.False((await lifecycle.ConfirmAsync(12, 8, 1, "https://example.com/replacement")).Succeeded);
        Assert.Equal(JoinUrl, (await fixture.Context.MeetingLinks.SingleAsync()).Url);
        Assert.True((await lifecycle.ConfirmAsync(12, 8, 1, JoinUrl)).Succeeded);
    }

    [Theory]
    [InlineData("Online")]
    [InlineData("Library")]
    public async Task Confirm_AutomaticallyCreatesTeamsMeeting_ForOnlineAndPhysicalSessions(string location)
    {
        await using var fixture = await Fixture.CreateAsync();
        Booking booking = await fixture.Context.Bookings.Include(item => item.ProgrammeModule).Include(item => item.StudyArea).SingleAsync();
        booking.StudyArea!.Name = location;
        booking.Location = location;
        booking.ProgrammeModule.ModuleName = "Linear Programming 281";
        await fixture.Context.SaveChangesAsync();
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);

        Assert.True((await lifecycle.ConfirmAsync(12, 8, 1, null)).Succeeded);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(JoinUrl, booking.MeetingLink!.Url);
        Assert.False(booking.MeetingLink.TeamsManualFallbackAllowed);
        Assert.Equal(location, booking.Location);
        Assert.Equal("Mzala Connect tutoring session: Linear Programming 281", Assert.Single(fixture.Calendar.Requests).Subject);
        Assert.Single(fixture.Context.BookingStatusHistory);
        Assert.False((await lifecycle.ConfirmAsync(12, 8, 1, null)).Succeeded);
        Assert.Single(fixture.Calendar.Requests);
    }

    [Fact]
    public async Task Confirm_FailedAutomaticCreation_AllowsManualFallback_AndRetainsRequestMetadata()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Calendar.FailCreate = true;
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);
        SessionLifecycleResult failed = await lifecycle.ConfirmAsync(12, 8, 1, null);
        Assert.False(failed.Succeeded);
        Assert.Contains("manual meeting link", failed.ErrorMessage);
        Assert.Equal(BookingStatus.Pending, (await fixture.Context.Bookings.SingleAsync()).Status);
        Assert.Empty(fixture.Context.BookingStatusHistory);
        MeetingLink reserved = await fixture.Context.MeetingLinks.SingleAsync();
        Guid? transactionId = reserved.TeamsCreationRequestId;
        Assert.True(reserved.TeamsManualFallbackAllowed);
        fixture.Context.ChangeTracker.Clear();

        Assert.True((await lifecycle.ConfirmAsync(12, 8, 1, "https://example.com/manual")).Succeeded);
        MeetingLink saved = await fixture.Context.MeetingLinks.SingleAsync();
        Assert.Equal("https://example.com/manual", saved.Url);
        Assert.Equal(transactionId, saved.TeamsCreationRequestId);
        Assert.Single(fixture.Calendar.Requests);
        Assert.Equal(BookingStatus.Confirmed, (await fixture.Context.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task Confirm_ManualLinkDoesNotContactMicrosoft()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);
        Assert.True((await lifecycle.ConfirmAsync(12, 8, 1, "https://example.com/manual")).Succeeded);
        Assert.Empty(fixture.Calendar.Requests);
        Assert.Equal(0, fixture.Calendar.SupportCalls);
    }

    [Fact]
    public async Task Confirm_PendingLink_CanRetryAndConfirmTheSameTeamsEvent()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Calendar.Result = new("event-1", null);
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);
        Assert.False((await lifecycle.ConfirmAsync(12, 8, 1, null)).Succeeded);
        fixture.Context.ChangeTracker.Clear();
        fixture.Calendar.Result = new("event-1", JoinUrl);
        Assert.True((await lifecycle.ConfirmAsync(12, 8, 1, null)).Succeeded);
        Assert.Single(fixture.Calendar.Requests);
        Assert.Equal(1, fixture.Calendar.GetCalls);
        Assert.False((await fixture.Context.MeetingLinks.SingleAsync()).TeamsManualFallbackAllowed);
    }

    [Fact]
    public async Task Confirm_DoesNotAllowManualReplacementWhileCreationIsStillInProgress()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.MeetingLinks.Add(new MeetingLink { BookingId = 1, TeamsCreationRequestId = Guid.NewGuid() });
        await fixture.Context.SaveChangesAsync();
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);
        Assert.False((await lifecycle.ConfirmAsync(12, 8, 1, "https://example.com/manual")).Succeeded);
        Assert.Empty(fixture.Calendar.Requests);
        Assert.Equal(BookingStatus.Pending, (await fixture.Context.Bookings.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ConfirmationPages_RequireManualLink_AndAllowManualFallbackAfterFailure(bool detailsPage, bool fail)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Calendar.FailCreate = fail;
        var time = new FixedTimeProvider(fixture.Now);
        var lifecycle = new SessionLifecycleService(fixture.Context, time, fixture.Service);
        PageModel page = detailsPage
            ? new BC_CampusLearn.Pages.Tutors.SessionDetailsModel(fixture.Context, new TutorUser(), fixture.Environment, lifecycle, time,
                teamsOptions: Options.Create(fixture.Options))
            : new BC_CampusLearn.Pages.Tutors.SessionsModel(fixture.Context, new TutorUser(), lifecycle, Options.Create(fixture.Options));
        var http = new DefaultHttpContext();
        http.Request.Headers.Accept = "application/json";
        page.PageContext = new PageContext { HttpContext = http };
        page.TempData = new TempDataDictionary(http, new EmptyTempData());

        async Task<IActionResult> Submit(string? link)
        {
            if (page is BC_CampusLearn.Pages.Tutors.SessionDetailsModel details)
            {
                details.MeetingLink = link;
                return await details.OnPostConfirmAsync(1, default);
            }
            return await ((BC_CampusLearn.Pages.Tutors.SessionsModel)page).OnPostConfirmAsync(1, link, default);
        }

        JsonResult response = Assert.IsType<JsonResult>(await Submit(null));
        JsonElement payload = JsonSerializer.SerializeToElement(response.Value);
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Contains("Enter a meeting link", payload.GetProperty("message").GetString());
        Assert.Equal(BookingStatus.Pending, (await fixture.Context.Bookings.SingleAsync()).Status);
        Assert.Empty(fixture.Calendar.Requests);

        JsonResult invalid = Assert.IsType<JsonResult>(await Submit("not a meeting link"));
        Assert.False(JsonSerializer.SerializeToElement(invalid.Value).GetProperty("success").GetBoolean());
        Assert.Equal(BookingStatus.Pending, (await fixture.Context.Bookings.SingleAsync()).Status);
        Assert.Empty(fixture.Calendar.Requests);

        Assert.Equal(!fail, (await fixture.Service.CreateAsync(8, 1)).Succeeded);
        if (fail)
        {
            Assert.Equal(BookingStatus.Pending, (await fixture.Context.Bookings.SingleAsync()).Status);
            JsonResult retry = Assert.IsType<JsonResult>(await Submit("https://example.com/manual"));
            Assert.True(JsonSerializer.SerializeToElement(retry.Value).GetProperty("success").GetBoolean());
        }
        else
        {
            JsonResult confirmation = Assert.IsType<JsonResult>(await Submit(JoinUrl));
            Assert.True(JsonSerializer.SerializeToElement(confirmation.Value).GetProperty("success").GetBoolean());
        }
        Assert.Equal(BookingStatus.Confirmed, (await fixture.Context.Bookings.SingleAsync()).Status);
        Assert.Single(fixture.Calendar.Requests);
    }

    [Fact]
    public async Task ConcurrentReservation_OnlyWinnerContactsMicrosoftToCreateMeeting()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.MeetingLinks.Add(new MeetingLink { BookingId = 1 });
        await fixture.Context.SaveChangesAsync();
        await using var competingContext = new ApplicationDbContext(fixture.ContextOptions);
        var competingCalendar = new FakeCalendar();
        var competingService = new BookingTeamsMeetingService(competingContext, competingCalendar,
            fixture.Identities, Options.Create(fixture.Options), fixture.Configuration, fixture.Environment,
            new FixedTimeProvider(fixture.Now), NullLogger<BookingTeamsMeetingService>.Instance);
        fixture.Calendar.BeforeSupport = async () => Assert.True((await competingService.CreateAsync(8, 1)).Succeeded);

        Assert.False((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        Assert.Empty(fixture.Calendar.Requests);
        Assert.Single(competingCalendar.Requests);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task TutorAndStudentCancellation_CancelTheOrganizerEvent(bool student, bool confirm)
    {
        await using var fixture = await Fixture.CreateAsync();
        Booking booking = await fixture.Context.Bookings.SingleAsync();
        var studentUser = new BcUser { BcUserId = 21, DisplayName = "Student", Email = "student@institution.example" };
        fixture.Context.BcUsers.Add(studentUser);
        booking.StudentBcUser = studentUser;
        await fixture.Context.SaveChangesAsync();
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);
        Assert.True((confirm ? await lifecycle.ConfirmAsync(12, 8, 1, null) : await fixture.Service.CreateAsync(8, 1)).Succeeded);
        SessionLifecycleResult result = student
            ? await lifecycle.CancelByStudentAsync(21, 1, "Schedule conflict")
            : await lifecycle.DeclineAsync(12, 8, 1, "Schedule conflict", false);
        Assert.True(result.Succeeded);
        Assert.Null(result.WarningMessage);
        Assert.Equal(student || confirm ? BookingStatus.Cancelled : BookingStatus.Declined, booking.Status);
        Assert.NotNull(booking.MeetingLink!.TeamsCancellationRequestedAt);
        Assert.NotNull(booking.MeetingLink.TeamsCancelledAt);
        Assert.Equal(1, fixture.Calendar.CancelCalls);
        Assert.True((await fixture.Service.CancelAsync(1)).Succeeded);
        Assert.Equal(1, fixture.Calendar.CancelCalls);
    }

    [Fact]
    public async Task CancellationFailure_IsPersistedAndRetriedByWorkerProcessing()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);
        Assert.True((await lifecycle.ConfirmAsync(12, 8, 1, null)).Succeeded);
        fixture.Calendar.FailCancel = true;
        SessionLifecycleResult result = await lifecycle.DeclineAsync(12, 8, 1, "Schedule conflict", false);
        Assert.True(result.Succeeded);
        Assert.Contains("pending", result.WarningMessage);
        fixture.Context.ChangeTracker.Clear();
        MeetingLink pending = await fixture.Context.MeetingLinks.SingleAsync();
        Assert.NotNull(pending.TeamsCancellationRequestedAt);
        Assert.Null(pending.TeamsCancelledAt);
        fixture.Calendar.FailCancel = false;
        var retry = new BookingTeamsMeetingService(fixture.Context, fixture.Calendar, fixture.Identities,
            Options.Create(fixture.Options), fixture.Configuration, fixture.Environment,
            new FixedTimeProvider(fixture.Now.AddMinutes(2)), NullLogger<BookingTeamsMeetingService>.Instance);
        await retry.ProcessPendingCancellationsAsync();
        Assert.NotNull((await fixture.Context.MeetingLinks.SingleAsync()).TeamsCancelledAt);
        Assert.Equal(2, fixture.Calendar.CancelCalls);
        await retry.ProcessPendingCancellationsAsync();
        Assert.Equal(2, fixture.Calendar.CancelCalls);
    }

    [Fact]
    public async Task CancellationCannotBeTriggeredForAnActiveBookingOrAnotherStudentsBooking()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);
        Assert.True((await lifecycle.ConfirmAsync(12, 8, 1, null)).Succeeded);
        Assert.False((await fixture.Service.CancelAsync(1)).Succeeded);
        Assert.False((await lifecycle.CancelByStudentAsync(999, 1, "Schedule conflict")).Succeeded);
        Assert.False((await lifecycle.DeclineAsync(999, 8, 1, "Schedule conflict", false)).Succeeded);
        Assert.Equal(0, fixture.Calendar.CancelCalls);
    }

    [Fact]
    public async Task AutomaticBookingCancellation_QueuesItsGeneratedMeetingForCleanup()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);
        Assert.True((await lifecycle.ConfirmAsync(12, 8, 1, null)).Succeeded);
        var due = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now.AddHours(3)), fixture.Service);
        await due.ProcessDueTransitionsAsync();
        Assert.Equal(BookingStatus.Cancelled, (await fixture.Context.Bookings.SingleAsync()).Status);
        Assert.NotNull((await fixture.Context.MeetingLinks.SingleAsync()).TeamsCancellationRequestedAt);
        await fixture.Service.ProcessPendingCancellationsAsync();
        Assert.NotNull((await fixture.Context.MeetingLinks.SingleAsync()).TeamsCancelledAt);
        Assert.Equal(1, fixture.Calendar.CancelCalls);
    }

    [Fact]
    public async Task ManualMeetingCancellation_ReportsOrganizerActionWithoutCallingMicrosoft()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);
        Assert.True((await lifecycle.ConfirmAsync(12, 8, 1, "https://example.com/manual")).Succeeded);
        SessionLifecycleResult cancellation = await lifecycle.DeclineAsync(12, 8, 1, "Schedule conflict", false);
        Assert.True(cancellation.Succeeded);
        Assert.Contains("manually supplied meeting", cancellation.WarningMessage);
        Assert.Equal(0, fixture.Calendar.CancelCalls);
    }

    [Fact]
    public async Task CancellationRecoversEventIdAfterAnAmbiguousCreateFailure()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Calendar.FailCreate = true;
        Assert.False((await fixture.Service.CreateAsync(8, 1)).Succeeded);
        var lifecycle = new SessionLifecycleService(fixture.Context, new FixedTimeProvider(fixture.Now), fixture.Service);
        Assert.True((await lifecycle.DeclineAsync(12, 8, 1, null, false)).Succeeded);
        Assert.Equal("event-1", (await fixture.Context.MeetingLinks.SingleAsync()).TeamsEventId);
        Assert.NotNull((await fixture.Context.MeetingLinks.SingleAsync()).TeamsCancelledAt);
        Assert.Single(fixture.Calendar.Requests);
        Assert.Equal(1, fixture.Calendar.CancelCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GenerateLinkPages_ConfirmBookingAndShowSuccess_OnlyAfterCreationSucceeds(bool detailsPage)
    {
        await using var fixture = await Fixture.CreateAsync();
        var student = new BcUser
        {
            BcUserId = 9, DisplayName = "Student", Email = "student@institution.example"
        };
        fixture.Context.BcUsers.Add(student);
        (await fixture.Context.Bookings.SingleAsync()).StudentBcUser = student;
        await fixture.Context.SaveChangesAsync();
        var time = new FixedTimeProvider(fixture.Now);
        var lifecycle = new SessionLifecycleService(fixture.Context, time, fixture.Service);
        PageModel page = detailsPage
            ? new BC_CampusLearn.Pages.Tutors.SessionDetailsModel(fixture.Context, new TutorUser(), fixture.Environment, lifecycle, time,
                teamsOptions: Options.Create(fixture.Options), teamsMeetings: fixture.Service)
            : new BC_CampusLearn.Pages.Tutors.SessionsModel(fixture.Context, new TutorUser(), lifecycle, Options.Create(fixture.Options), fixture.Service);
        var http = new DefaultHttpContext();
        page.PageContext = new PageContext { HttpContext = http };
        page.TempData = new TempDataDictionary(http, new EmptyTempData());

        Task<IActionResult> Generate() => detailsPage
            ? ((BC_CampusLearn.Pages.Tutors.SessionDetailsModel)page).OnPostGenerateTeamsLinkAsync(1, default)
            : ((BC_CampusLearn.Pages.Tutors.SessionsModel)page).OnPostGenerateTeamsLinkAsync(1, default);
        bool ShowSuccess() => detailsPage
            ? ((BC_CampusLearn.Pages.Tutors.SessionDetailsModel)page).TeamsMeetingCreated
            : ((BC_CampusLearn.Pages.Tutors.SessionsModel)page).TeamsMeetingCreated;

        fixture.Calendar.Result = new("event-1", null);
        JsonElement pending = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await Generate()).Value);
        Assert.False(pending.GetProperty("success").GetBoolean());
        Assert.False(ShowSuccess());
        Assert.Equal(BookingStatus.Pending, (await fixture.Context.Bookings.SingleAsync()).Status);
        Assert.Empty(fixture.Context.BookingStatusHistory);
        Assert.Empty(fixture.Context.UserNotifications);

        fixture.Calendar.Result = new("event-1", JoinUrl);
        IActionResult result = await Generate();
        JsonElement json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(JoinUrl, json.GetProperty("link").GetString());
        Assert.Equal(BookingStatus.Confirmed, (await fixture.Context.Bookings.SingleAsync()).Status);
        Assert.True(ShowSuccess());
        Assert.Single(fixture.Context.BookingStatusHistory);
        Assert.Single(fixture.Context.UserNotifications);
        Assert.Single(fixture.Calendar.Requests);
        Assert.Equal(1, fixture.Calendar.GetCalls);
    }

    [Theory]
    [InlineData(202)]
    [InlineData(404)]
    public async Task CalendarCancellation_UsesOrganizerAndAcceptsEmptyOrAlreadyRemovedResponses(int status)
    {
        using var http = new HttpClient(new FakeHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"/v1.0/users/{ObjectId}/events/event-1/cancel", request.RequestUri!.AbsolutePath);
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Contains("Mzala Connect", body.RootElement.GetProperty("comment").GetString());
            return new HttpResponseMessage((HttpStatusCode)status);
        })) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        await new TeamsCalendarClient(http, new FakeTokens()).CancelAsync(ObjectId, "event-1", default);
    }

    [Fact]
    public async Task CalendarCancellation_RecognizesAnAlreadyCancelledEvent()
    {
        using var http = new HttpClient(new FakeHandler(request => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.BadRequest)
            : JsonResponse(HttpStatusCode.OK, "{\"isCancelled\":true}")))) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        await new TeamsCalendarClient(http, new FakeTokens()).CancelAsync(ObjectId, "event-1", default);
    }

    [Fact]
    public async Task CalendarSearch_OnlyMatchesTheSavedTransaction_AndRejectsUnsafePagination()
    {
        Guid transaction = Guid.NewGuid();
        string body = JsonSerializer.Serialize(new { value = new[] {
            new { id = "unrelated-event", transactionId = Guid.NewGuid().ToString() },
            new { id = "event-1", transactionId = transaction.ToString() } } });
        using var http = new HttpClient(new FakeHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, body))))
            { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var calendar = new TeamsCalendarClient(http, new FakeTokens());
        Assert.Equal("event-1", await calendar.FindByTransactionAsync(ObjectId, transaction, DateTimeOffset.UtcNow, default));
        body = "{\"value\":[],\"@odata.nextLink\":\"https://untrusted.example/events\"}";
        await Assert.ThrowsAsync<TeamsMeetingException>(() => calendar.FindByTransactionAsync(ObjectId, transaction, DateTimeOffset.UtcNow, default));
    }

    [Fact]
    public async Task CalendarRequest_UsesTutorCalendar_ApplicationToken_AndUtcTimes()
    {
        string? capturedBody = null;
        var handler = new FakeHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"https://graph.microsoft.com/v1.0/users/{ObjectId}/calendar/events", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization.Parameter);
            Assert.Contains("IdType=\"ImmutableId\"", request.Headers.GetValues("Prefer"));
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.Created, "{\"id\":\"event-1\",\"onlineMeeting\":{\"joinUrl\":\"" + JoinUrl + "\"}}");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var client = new TeamsCalendarClient(http, new FakeTokens());
        var transactionId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 10, 14, 14, 0, 0, TimeSpan.FromHours(2));
        TeamsCalendarEvent result = await client.CreateAsync(new(ObjectId, transactionId, "MOD101", start, start.AddHours(1), "student@institution.example", "Student"), default);
        using var body = JsonDocument.Parse(capturedBody!);
        Assert.True(body.RootElement.GetProperty("isOnlineMeeting").GetBoolean());
        Assert.Equal("teamsForBusiness", body.RootElement.GetProperty("onlineMeetingProvider").GetString());
        Assert.Equal(transactionId.ToString("D"), body.RootElement.GetProperty("transactionId").GetString());
        Assert.Equal("2026-10-14T12:00:00", body.RootElement.GetProperty("start").GetProperty("dateTime").GetString());
        Assert.Equal("2026-10-14T13:00:00", body.RootElement.GetProperty("end").GetProperty("dateTime").GetString());
        Assert.Equal("UTC", body.RootElement.GetProperty("start").GetProperty("timeZone").GetString());
        Assert.Equal("student@institution.example", body.RootElement.GetProperty("attendees")[0].GetProperty("emailAddress").GetProperty("address").GetString());
        Assert.Equal(JoinUrl, result.JoinUrl);
    }

    [Theory]
    [InlineData(403, "calendar access")]
    [InlineData(429, "busy")]
    [InlineData(500, "retry")]
    public async Task CalendarErrors_AreActionable_AndDoNotExposeResponseBody(int status, string message)
    {
        using var http = new HttpClient(new FakeHandler(_ => Task.FromResult(JsonResponse((HttpStatusCode)status, "sensitive-details"))))
            { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var client = new TeamsCalendarClient(http, new FakeTokens());
        TeamsMeetingException exception = await Assert.ThrowsAsync<TeamsMeetingException>(() => client.SupportsTeamsAsync(ObjectId, default));
        Assert.Contains(message, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sensitive-details", exception.Message);
    }

    [Theory]
    [InlineData("{\"allowedOnlineMeetingProviders\":[\"teamsForBusiness\"]}", true)]
    [InlineData("{\"allowedOnlineMeetingProviders\":[\"skypeForBusiness\"]}", false)]
    [InlineData("{\"allowedOnlineMeetingProviders\":[null,123]}", false)]
    public async Task CalendarCapability_ChecksTutorsDefaultCalendar(string json, bool supported)
    {
        using var http = new HttpClient(new FakeHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal($"/v1.0/users/{ObjectId}/calendar", request.RequestUri!.AbsolutePath);
            Assert.Equal("?$select=allowedOnlineMeetingProviders", request.RequestUri.Query);
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, json));
        })) { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        Assert.Equal(supported, await new TeamsCalendarClient(http, new FakeTokens()).SupportsTeamsAsync(ObjectId, default));
    }

    [Fact]
    public async Task TokenProvider_MissingCredentialsFailsWithoutNetworkAccess()
    {
        var provider = new TeamsGraphTokenProvider(new ConfigurationBuilder().AddInMemoryCollection().Build());
        TeamsMeetingException exception = await Assert.ThrowsAsync<TeamsMeetingException>(() => provider.GetTokenAsync(default));
        Assert.Contains("not configured", exception.Message);
    }

    [Theory]
    [InlineData("AADSTS7000215", "secret value")]
    [InlineData("AADSTS7000222", "expired")]
    [InlineData("AADSTS700016", "ClientId")]
    [InlineData("AADSTS90002", "TenantId")]
    [InlineData("AADSTS7000112", "disabled")]
    public void AuthenticationDiagnostics_ReportsSafeCodeAndAction_WithoutRawDetails(string code, string action)
    {
        var exception = new MsalServiceException("invalid_client", $"{code}: sensitive-account-details and test-sensitive-secret");
        TeamsAuthenticationDiagnostics failure = TeamsAuthenticationDiagnostics.FromException(exception);
        Assert.Equal(code, failure.EntraErrorCode);
        Assert.Equal("invalid_client", failure.ErrorCode);
        Assert.Contains(action, failure.UserMessage);
        Assert.Contains(code, failure.UserMessage);
        Assert.DoesNotContain("sensitive-account-details", failure.ToString());
        Assert.DoesNotContain("test-sensitive-secret", failure.ToString());
    }

    [Fact]
    public void AuthenticationDiagnostics_NetworkFailureDoesNotMisidentifyCredentials()
    {
        var exception = new MsalClientException("http_request_failed", "raw-network-details");
        TeamsAuthenticationDiagnostics failure = TeamsAuthenticationDiagnostics.FromException(exception);
        Assert.Contains("network access", failure.UserMessage);
        Assert.Equal("http_request_failed", failure.ErrorCode);
        Assert.Null(failure.EntraErrorCode);
        Assert.DoesNotContain("raw-network-details", failure.ToString());
    }

    [Fact]
    public void AuthenticationDiagnostics_DoesNotEchoMalformedErrorCode()
    {
        var exception = new MsalClientException("error-with-sensitive-content!", "raw-secret-value");
        TeamsAuthenticationDiagnostics failure = TeamsAuthenticationDiagnostics.FromException(exception);
        Assert.Equal("unknown", failure.ErrorCode);
        Assert.DoesNotContain("sensitive-content", failure.ToString());
        Assert.DoesNotContain("raw-secret-value", failure.ToString());
    }

    [Theory]
    [InlineData("https://teams.microsoft.com/valid", true)]
    [InlineData("http://teams.microsoft.com/insecure", false)]
    [InlineData("javascript:alert(1)", false)]
    public async Task CalendarEvent_RejectsUnsafeLinks(string url, bool valid)
    {
        using var http = new HttpClient(new FakeHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { id = "event-1", onlineMeeting = new { joinUrl = url } })))))
            { BaseAddress = new Uri("https://graph.microsoft.com/v1.0/") };
        var client = new TeamsCalendarClient(http, new FakeTokens());
        if (valid) Assert.Equal(url, (await client.GetAsync(ObjectId, "event-1", default)).JoinUrl);
        else await Assert.ThrowsAsync<TeamsMeetingException>(() => client.GetAsync(ObjectId, "event-1", default));
    }

    [Fact]
    public void Identity_CanBeRecoveredWithSameProtector_WhileStoredValuesRemainEncrypted()
    {
        IEntraIdentityProtector protector = CreateProtector();
        ProtectedEntraIdentity stored = protector.Protect(TenantId, ObjectId);
        Assert.DoesNotContain(TenantId, stored.TenantId);
        Assert.DoesNotContain(ObjectId, stored.ObjectId);
        Assert.Equal(new EntraIdentity(TenantId, ObjectId), protector.Unprotect(stored.TenantId, stored.ObjectId));
    }

    [Fact]
    public void PersistentKeyDirectoryKeepsIdentityReadableAfterProviderRecreation()
    {
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        var directory = new DirectoryInfo(Path.Combine(temporaryRoot, "CampusLearn-KeyPersistence-" + Guid.NewGuid().ToString("N")));
        var options = Options.Create(new IdentityProtectionOptions { LookupKey = Convert.ToBase64String(new byte[32]) });
        try
        {
            var original = new EntraIdentityProtector(
                DataProtectionProvider.Create(directory, builder => builder.SetApplicationName("BC_CampusLearn")), options);
            ProtectedEntraIdentity stored = original.Protect(TenantId, ObjectId);
            var recreated = new EntraIdentityProtector(
                DataProtectionProvider.Create(directory, builder => builder.SetApplicationName("BC_CampusLearn")), options);
            Assert.Equal(new EntraIdentity(TenantId, ObjectId), recreated.Unprotect(stored.TenantId, stored.ObjectId));
        }
        finally
        {
            // Only clean up the explicitly created temporary directory.
            if (directory.Exists && string.Equals(directory.Parent?.FullName.TrimEnd(Path.DirectorySeparatorChar), temporaryRoot, StringComparison.OrdinalIgnoreCase))
                directory.Delete(recursive: true);
        }
    }

    private static IEntraIdentityProtector CreateProtector() => new EntraIdentityProtector(
        new EphemeralDataProtectionProvider(), Options.Create(new IdentityProtectionOptions { LookupKey = Convert.ToBase64String(new byte[32]) }));
    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
    private sealed class FakeTokens : ITeamsGraphTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken) => Task.FromResult("test-token");
    }
    private sealed class FakeCalendar : ITeamsCalendarClient
    {
        public List<TeamsCalendarRequest> Requests { get; } = [];
        public TeamsCalendarEvent Result { get; set; } = new("event-1", JoinUrl);
        public bool FailCreate { get; set; }
        public bool SupportsTeams { get; set; } = true;
        public int SupportCalls { get; private set; }
        public int GetCalls { get; private set; }
        public int CancelCalls { get; private set; }
        public bool FailCancel { get; set; }
        public Task<string?> FindByTransactionAsync(string organizerObjectId, Guid transactionId, DateTimeOffset start, CancellationToken cancellationToken) => Task.FromResult<string?>("event-1");
        public Task CancelAsync(string organizerObjectId, string eventId, CancellationToken cancellationToken)
        {
            Assert.Equal(ObjectId, organizerObjectId);
            Assert.Equal("event-1", eventId);
            CancelCalls++;
            if (FailCancel) throw new TeamsMeetingException("Microsoft could not be reached.");
            return Task.CompletedTask;
        }
        public Func<Task>? BeforeSupport { get; set; }
        public async Task<bool> SupportsTeamsAsync(string organizerObjectId, CancellationToken cancellationToken)
        {
            SupportCalls++;
            if (BeforeSupport is not null) await BeforeSupport();
            return SupportsTeams;
        }
        public Task<TeamsCalendarEvent> CreateAsync(TeamsCalendarRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (FailCreate) throw new TeamsMeetingException("Microsoft took too long to respond.");
            return Task.FromResult(Result);
        }
        public Task<TeamsCalendarEvent> GetAsync(string organizerObjectId, string eventId, CancellationToken cancellationToken)
        {
            GetCalls++;
            Assert.Equal(ObjectId, organizerObjectId);
            Assert.Equal("event-1", eventId);
            return Task.FromResult(Result);
        }
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    private sealed class TutorUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => new(8, null, "Tutor", "tutor@institution.example", BcUserRole.Tutor);
    }
    private sealed class EmptyTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public DbContextOptions<ApplicationDbContext> ContextOptions { get; } = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public ApplicationDbContext Context { get; }
        public IEntraIdentityProtector Identities { get; } = CreateProtector();
        public FakeCalendar Calendar { get; } = new();
        public TeamsMeetingOptions Options { get; } = new() { Enabled = true, PilotTutorBcUserIds = [8] };
        public IConfiguration Configuration { get; } = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AzureAd:TenantId"] = TenantId }).Build();
        public TestEnvironment Environment { get; } = new();
        public DateTimeOffset Now { get; } = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);
        public BookingTeamsMeetingService Service => new(Context, Calendar, Identities, Microsoft.Extensions.Options.Options.Create(Options), Configuration, Environment, new FixedTimeProvider(Now), NullLogger<BookingTeamsMeetingService>.Instance);

        public Fixture() => Context = new(ContextOptions);

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            ProtectedEntraIdentity stored = fixture.Identities.Protect(TenantId, ObjectId);
            var tutor = new Tutor
            {
                TutorId = 12, BcUserId = 8, Status = TutorStatus.Approved, ApplicationStage = TutorApplicationStage.Placement, IsActive = true,
                ReasonForTutoring = "Help", TeachingStyle = "Practical", PreviousTutoringExperience = "Peer", CampusOfStudy = "Pretoria", DemonstrationVideoUrl = "",
                BcUser = new BcUser { BcUserId = 8, DisplayName = "Tutor", Email = "tutor@institution.example", EncryptedEntraTenantId = stored.TenantId, EncryptedEntraObjectId = stored.ObjectId, EntraIdentityLookupHash = stored.LookupHash }
            };
            var module = new ProgrammeModule { ProgrammeModuleId = 3, ModuleCode = "MOD101", ModuleName = "Module" };
            var assignment = new TutorCourseModule { TutorId = 12, Tutor = tutor, ProgrammeModuleId = 3, ProgrammeModule = module, IsActive = true };
            fixture.Context.Bookings.Add(new Booking
            {
                BookingId = 1, TutorId = 12, TutorCourseModule = assignment, ProgrammeModuleId = 3, ProgrammeModule = module,
                StudyArea = new StudyArea { Name = "Online" }, StudentName = "Student", StudentEmail = "student@institution.example",
                Location = "Online", Status = BookingStatus.Pending, Duration = SessionDuration.OneHour, ScheduledStartTime = fixture.Now.AddHours(2), DateBooked = fixture.Now
            });
            await fixture.Context.SaveChangesAsync();
            return fixture;
        }
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}

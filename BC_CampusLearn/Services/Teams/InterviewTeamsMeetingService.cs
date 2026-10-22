using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Tutors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BC_CampusLearn.Services.Teams;

public sealed class InterviewMeetingInput
{
    public string? Title { get; set; }
    public string? Date { get; set; }
    public string? Time { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public string? Message { get; set; }
    public List<InterviewPanelInput> Panel { get; set; } = [];

    public string? Validate(DateTimeOffset now, out DateTimeOffset start)
    {
        start = default;
        if (string.IsNullOrWhiteSpace(Title) || Title.Trim().Length > 200)
            return "Enter a meeting title of up to 200 characters.";
        if (!DateOnly.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ||
            !TimeOnly.TryParseExact(Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return "Enter a valid interview date and start time.";
        start = new DateTimeOffset(day.ToDateTime(time, DateTimeKind.Unspecified), TimeSpan.FromHours(2));
        if (start <= now || start > now.AddYears(1))
            return "Choose an interview time in the future, within the next year (South Africa Standard Time).";
        if (DurationMinutes is < 15 or > 240) return "Choose a duration between 15 and 240 minutes.";
        if ((Message?.Length ?? 0) > 4000) return "The message cannot exceed 4,000 characters.";
        if (Panel is null || Panel.Count == 0) return "Add at least one interview panel member.";
        if (Panel.Count > 20) return "Add no more than 20 panel members.";
        if (Panel.Any(p => !IsEmail(p.Email) || (p.Name?.Length ?? 0) > 200))
            return "Each panel member needs a valid email and a name of up to 200 characters.";
        if (Panel.Select(p => p.Email!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Panel.Count)
            return "A panel member has been added more than once. Remove the duplicate.";
        return null;
    }

    public static bool IsEmail(string? email) => !string.IsNullOrWhiteSpace(email) && email.Length <= 254 &&
        MailAddress.TryCreate(email.Trim(), out var parsed) && parsed.Address == email.Trim();

    public static InterviewMeetingInput FromRequest(TeamsCalendarRequest request) => new()
    {
        Title = request.Subject,
        Date = request.Start.ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-dd"),
        Time = request.Start.ToOffset(TimeSpan.FromHours(2)).ToString("HH:mm"),
        DurationMinutes = (int)(request.End - request.Start).TotalMinutes,
        Message = request.Message,
        Panel = (request.Panel ?? []).Select(p => new InterviewPanelInput { Email = p.Email, Name = p.Name, Optional = p.Optional }).ToList()
    };
}

public sealed class InterviewPanelInput
{
    public string? Email { get; set; }
    public string? Name { get; set; }
    public bool Optional { get; set; }
}

public sealed class InterviewTeamsMeetingService(
    ApplicationDbContext context,
    ITeamsCalendarClient calendar,
    TeamsOrganizerSettingsStore organizerSettings,
    IOptions<TeamsMeetingOptions> options,
    IConfiguration configuration,
    IHostEnvironment environment,
    TimeProvider clock,
    ILogger<InterviewTeamsMeetingService> logger)
{
    public async Task<ShortlistResult> CreateAsync(int tutorId, int adminId, InterviewMeetingInput input, CancellationToken ct)
    {
        if (!options.Value.Enabled) return ShortlistResult.Failure("Teams meeting creation is not enabled. Contact the administrator.");
        if (environment.IsDevelopment() && configuration.GetValue<bool>("Authentication:UseDevelopmentAuthentication"))
            return ShortlistResult.Failure("Sign in with your institutional Microsoft account to create a Teams meeting. Development sign-in cannot send invitations.");
        var admin = await context.BcUsers.AsNoTracking().SingleOrDefaultAsync(u => u.BcUserId == adminId, ct);
        if (admin is null || !admin.IsAdministrativeAccessActive || admin.Role is not (BcUserRole.Admin or BcUserRole.SuperAdmin or BcUserRole.Dev))
            return ShortlistResult.Failure("Only an active administrator can schedule an interview.");
        var candidate = await context.Tutors.Include(t => t.BcUser).SingleOrDefaultAsync(t => t.TutorId == tutorId, ct);
        if (candidate is null) return ShortlistResult.Failure("The application could not be found.");
        var saved = await context.TutorInterviewMeetings.SingleOrDefaultAsync(m => m.TutorId == tutorId && m.ApplicationSubmittedAt == candidate.SubmittedAt, ct);
        if (saved?.CompletedAt is not null && candidate.ApplicationStage == TutorApplicationStage.Interview)
            return ShortlistResult.Success("This interview is already scheduled. The existing meeting has been kept.");
        if (candidate.Status != TutorStatus.Pending || candidate.ApplicationStage != TutorApplicationStage.Shortlisted)
            return ShortlistResult.Failure("This candidate is no longer in the shortlist.");
        if (saved?.CancelledAt is not null) return ShortlistResult.Failure("This interview invitation was cancelled. Contact the administrator.");

        try
        {
            if (saved is null)
            {
                string? error = input.Validate(clock.GetUtcNow(), out var start);
                if (error is not null) return ShortlistResult.Failure(error);
                if (!InterviewMeetingInput.IsEmail(candidate.BcUser.Email))
                    return ShortlistResult.Failure("The candidate needs a valid email address before an invitation can be sent.");
                var organizer = await organizerSettings.GetAsync(ct);
                string organizerId = organizer.ValidatedObjectId();
                if (!await calendar.SupportsTeamsAsync(organizerId, ct))
                    return ShortlistResult.Failure("The Mzala Connect organiser's calendar does not support Teams meetings. Check the Teams meeting account settings.");
                Guid requestId = Guid.NewGuid();
                var panel = input.Panel.Where(p => !string.Equals(p.Email?.Trim(), candidate.BcUser.Email?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(p.Email?.Trim(), organizer.Email, StringComparison.OrdinalIgnoreCase))
                    .Select(p => new TeamsCalendarAttendee(p.Email!.Trim(), string.IsNullOrWhiteSpace(p.Name) ? p.Email.Trim() : p.Name.Trim(), p.Optional)).ToArray();
                var request = new TeamsCalendarRequest(organizerId, requestId, input.Title!.Trim(), start, start.AddMinutes(input.DurationMinutes),
                    candidate.BcUser.Email!.Trim(), candidate.BcUser.DisplayName, Panel: panel, Message: input.Message?.Trim() ?? "");
                saved = new TutorInterviewMeeting
                {
                    TutorInterviewMeetingId = requestId, TutorId = tutorId, ApplicationSubmittedAt = candidate.SubmittedAt,
                    OrganizerObjectId = organizerId, RequestJson = JsonSerializer.Serialize(request), ScheduledStart = start,
                    CreatedByBcUserId = adminId, Version = Guid.NewGuid()
                };
                context.TutorInterviewMeetings.Add(saved);
                // A unique application-cycle key arbitrates concurrent first submissions.
                // No invitation is sent until this reservation is durable.
                await context.SaveChangesAsync(ct);
            }

            var reservedRequest = JsonSerializer.Deserialize<TeamsCalendarRequest>(saved.RequestJson)!;
            // Always retry the original organiser, payload and transaction ID.
            var meeting = saved.EventId is { } eventId
                ? await calendar.GetAsync(saved.OrganizerObjectId, eventId, ct)
                : await calendar.CreateAsync(reservedRequest, ct);
            saved.EventId = meeting.Id;
            saved.JoinUrl = meeting.JoinUrl;
            saved.Version = Guid.NewGuid();
            await context.SaveChangesAsync(ct);
            await context.Entry(candidate).ReloadAsync(ct);
            if (candidate.Status != TutorStatus.Pending || candidate.ApplicationStage != TutorApplicationStage.Shortlisted)
            {
                if (candidate.ApplicationStage == TutorApplicationStage.Interview)
                    return ShortlistResult.Success("This interview is already scheduled.");
                await calendar.CancelAsync(saved.OrganizerObjectId, meeting.Id, ct);
                saved.CancelledAt = clock.GetUtcNow();
                await context.SaveChangesAsync(ct);
                return ShortlistResult.Failure("The application changed during scheduling. Its meeting has been cancelled.");
            }
            if (!Uri.TryCreate(meeting.JoinUrl, UriKind.Absolute, out var link) || link.Scheme != Uri.UriSchemeHttps || meeting.JoinUrl!.Length > 2048)
                return ShortlistResult.Failure("The invitation exists, but Microsoft is still preparing its Teams link. Retry to finish the same meeting.");

            var localStart = reservedRequest.Start.ToOffset(TimeSpan.FromHours(2));
            saved.CompletedAt = clock.GetUtcNow();
            saved.Version = Guid.NewGuid();
            return await TutorApplicationReview.MoveToInterviewAsync(context, tutorId,
                new InterviewPreparationDetails(candidate.InterviewPreparationNotes, localStart.Date, localStart.TimeOfDay,
                    (int)(reservedRequest.End - reservedRequest.Start).TotalMinutes, meeting.JoinUrl), ct, adminId);
        }
        catch (TeamsMeetingException ex)
        {
            return ShortlistResult.Failure(ex.Message);
        }
        catch (DbUpdateException)
        {
            logger.LogWarning("Interview meeting save conflicted for application {TutorId}. Retry the saved request.", tutorId);
            return ShortlistResult.Failure("The application changed or the meeting could not be saved. Reload and retry; the saved meeting request will be reused.");
        }
    }

    public async Task<ShortlistResult> UpdateAsync(int tutorId, int adminId, InterviewMeetingInput input, CancellationToken ct)
    {
        if (!options.Value.Enabled || (environment.IsDevelopment() && configuration.GetValue<bool>("Authentication:UseDevelopmentAuthentication")))
            return ShortlistResult.Failure("Sign in with your institutional Microsoft account with Teams meeting creation enabled to update an interview.");
        var admin = await context.BcUsers.AsNoTracking().SingleOrDefaultAsync(u => u.BcUserId == adminId, ct);
        if (admin is null || !admin.IsAdministrativeAccessActive || admin.Role is not (BcUserRole.Admin or BcUserRole.SuperAdmin or BcUserRole.Dev))
            return ShortlistResult.Failure("Only an active administrator can update an interview.");
        var candidate = await context.Tutors.SingleOrDefaultAsync(t => t.TutorId == tutorId, ct);
        if (candidate is null || candidate.Status != TutorStatus.Pending || candidate.ApplicationStage != TutorApplicationStage.Interview)
            return ShortlistResult.Failure("This application is no longer in the interview stage.");
        var saved = await context.TutorInterviewMeetings.SingleOrDefaultAsync(m => m.TutorId == tutorId && m.ApplicationSubmittedAt == candidate.SubmittedAt, ct);
        if (saved?.EventId is null || saved.CancelledAt is not null)
            return ShortlistResult.Failure("There is no active Mzala Connect interview meeting to edit.");
        try
        {
            var request = JsonSerializer.Deserialize<TeamsCalendarRequest>(saved.RequestJson)!;
            if (saved.CompletedAt is not null)
            {
                string? error = input.Validate(clock.GetUtcNow(), out var start);
                if (error is not null) return ShortlistResult.Failure(error);
                request = request with
                {
                    Subject = input.Title!.Trim(), Start = start, End = start.AddMinutes(input.DurationMinutes),
                    Panel = input.Panel.Where(p => !string.Equals(p.Email?.Trim(), request.StudentEmail, StringComparison.OrdinalIgnoreCase))
                        .Select(p => new TeamsCalendarAttendee(p.Email!.Trim(), string.IsNullOrWhiteSpace(p.Name) ? p.Email.Trim() : p.Name.Trim(), p.Optional)).ToArray()
                };
                string updatedJson = JsonSerializer.Serialize(request);
                if (updatedJson == saved.RequestJson)
                    return ShortlistResult.Success("The interview is already up to date.");
                saved.RequestJson = updatedJson;
                saved.CompletedAt = null;
                saved.Version = Guid.NewGuid();
                await context.SaveChangesAsync(ct);
            }
            var meeting = await calendar.UpdateInterviewAsync(saved.EventId, request, ct);
            // PATCH can omit onlineMeeting in its response; the original link remains valid.
            saved.JoinUrl = meeting.JoinUrl ?? saved.JoinUrl;
            await context.Entry(candidate).ReloadAsync(ct);
            if (candidate.Status != TutorStatus.Pending || candidate.ApplicationStage != TutorApplicationStage.Interview)
                return ShortlistResult.Failure("The application changed during the update. Reload the page.");
            var local = request.Start.ToOffset(TimeSpan.FromHours(2));
            candidate.InterviewScheduledAt = local.DateTime;
            candidate.InterviewDurationMinutes = (int)(request.End - request.Start).TotalMinutes;
            candidate.InterviewLocation = saved.JoinUrl;
            candidate.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            saved.ScheduledStart = request.Start;
            saved.CompletedAt = clock.GetUtcNow();
            saved.Version = Guid.NewGuid();
            context.TutorApplicationReviewDecisions.Add(new()
            {
                TutorId = tutorId, ReviewerBcUserId = adminId, AdminName = admin.DisplayName,
                PreviousStage = TutorApplicationStage.Interview, NewStage = TutorApplicationStage.Interview,
                Reason = "Interview schedule or panel updated. Calendar updates sent through Mzala Connect.", ReviewedAt = clock.GetUtcNow().UtcDateTime
            });
            await context.SaveChangesAsync(ct);
            return ShortlistResult.Success("Interview updated. Calendar updates have been sent to the attendees.");
        }
        catch (TeamsMeetingException ex) { return ShortlistResult.Failure(ex.Message); }
        catch (DbUpdateException) { return ShortlistResult.Failure("The interview changed or the update could not be saved. Reload and retry the saved update."); }
    }

    public async Task ProcessCancellationsAsync(CancellationToken ct)
    {
        // Keep invitations from outliving rejected, deleted or replaced applications.
        var orphaned = await context.TutorInterviewMeetings.Where(m => m.CancelledAt == null &&
            !context.Tutors.Any(t => t.TutorId == m.TutorId && t.SubmittedAt == m.ApplicationSubmittedAt &&
                t.Status != TutorStatus.Rejected && t.ApplicationStage != TutorApplicationStage.Rejected))
            .OrderBy(m => m.EventId == null).ThenBy(m => m.ScheduledStart)
            .Take(25).ToListAsync(ct);
        foreach (var meeting in orphaned)
        {
            try
            {
                string? eventId = meeting.EventId ?? await calendar.FindByTransactionAsync(meeting.OrganizerObjectId,
                    meeting.TutorInterviewMeetingId, meeting.ScheduledStart, ct);
                // An ambiguous create may still be in flight. Leave missing events
                // recoverable instead of marking them cancelled prematurely.
                if (eventId is null)
                {
                    if (meeting.ScheduledStart < clock.GetUtcNow().AddDays(-1))
                    {
                        meeting.CancelledAt = clock.GetUtcNow();
                        meeting.Version = Guid.NewGuid();
                        await context.SaveChangesAsync(ct);
                    }
                    continue;
                }
                await calendar.CancelAsync(meeting.OrganizerObjectId, eventId, ct);
                meeting.EventId = eventId;
                meeting.CancelledAt = clock.GetUtcNow();
                meeting.Version = Guid.NewGuid();
                await context.SaveChangesAsync(ct);
            }
            catch (TeamsMeetingException) { logger.LogWarning("Interview cancellation will retry for application {TutorId}.", meeting.TutorId); }
            catch (DbUpdateException) { context.Entry(meeting).State = EntityState.Detached; }
        }
    }
}

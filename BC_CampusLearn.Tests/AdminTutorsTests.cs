using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Students;
using System.Text.Json;
using BC_CampusLearn.Pages.Administrator.Tutors;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminTutorsTests
{
    [Theory]
    [InlineData(TutorStatus.Approved, false)]
    [InlineData(TutorStatus.Suspended, false)]
    [InlineData(TutorStatus.Deregistered, false)]
    [InlineData(TutorStatus.Approved, true)]
    [InlineData(TutorStatus.Suspended, true)]
    [InlineData(TutorStatus.Deregistered, true)]
    public async Task FormerTutorsCanBeLookedUpAndAddedWithoutLosingHistory(
        TutorStatus formerStatus, bool placementPage)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        Tutor former = await context.Tutors.Include(t => t.BcUser)
            .Include(t => t.TutorCourseModules).SingleAsync(t => t.TutorId == 1);
        former.IsActive = false;
        former.Status = formerStatus;
        former.DeregisteredAt = DateTimeOffset.UtcNow.AddDays(-7);
        former.DeregistrationReason = "Completed previous term";
        var originalCreatedAt = former.CreatedAt;
        var originalDeregisteredAt = former.DeregisteredAt;
        former.TutorCourseModules.Single().IsActive = false;
        context.ProgrammeModules.AddRange(
            new ProgrammeModule { ProgrammeModuleId = 2, ProgrammeId = 1, YearOfStudy = 1,
                ModuleCode = "OLD101", ModuleName = "Previous module" },
            new ProgrammeModule { ProgrammeModuleId = 3, ProgrammeId = 1, YearOfStudy = 1,
                ModuleCode = "NEW101", ModuleName = "New module" });
        former.TutorCourseModules.Add(new TutorCourseModule { ProgrammeModuleId = 2, IsActive = false });
        context.Bookings.Add(new Booking
        {
            BookingId = 100, TutorId = former.TutorId, ProgrammeModuleId = 2,
            Status = BookingStatus.Completed, StudentName = "Previous Student"
        });
        await context.SaveChangesAsync();
        Assert.Equal(BcUserRole.Student, former.BcUser.Role);

        var service = new TestStudentDetailsService();
        BC_CampusLearn.Pages.Administrator.ManualTutorPageModel page = placementPage
            ? new BC_CampusLearn.Pages.Administrator.Admin.ApplicationsModel(context, studentDetailsService: service)
                { Stage = "placement" }
            : new IndexModel(context, service);
        SetPageContext(page);
        if (page is IndexModel directory)
            await directory.OnGetAsync(CancellationToken.None);
        else
            await ((BC_CampusLearn.Pages.Administrator.Admin.ApplicationsModel)page).OnGetAsync(CancellationToken.None);
        Assert.Contains(page.StudentOptions, option => option.Value == "1");
        var lookup = Assert.IsType<JsonResult>(await page.OnGetStudentDetailsAsync(1, CancellationToken.None));
        Assert.Null(lookup.StatusCode);
        Assert.Equal("New Tutor", JsonSerializer.SerializeToElement(lookup.Value).GetProperty("displayName").GetString());
        Assert.Equal(["S1"], service.RequestedNumbers);

        page.ManualTutor = new()
        {
            BcUserId = 1, OverallAverage = 82, ProgrammeModuleIds = [1, 3],
            ProgrammeId = 999, YearOfStudy = 4, CampusOfStudy = "Untrusted campus"
        };
        await page.OnPostAddTutorAsync(CancellationToken.None);
        Assert.Null(page.PageError);
        context.ChangeTracker.Clear();
        Tutor restored = await context.Tutors.Include(t => t.BcUser)
            .Include(t => t.TutorCourseModules).SingleAsync(t => t.BcUserId == 1);
        Assert.Equal(18, await context.Tutors.CountAsync());
        Assert.Equal(1, restored.TutorId);
        Assert.Equal(BcUserRole.Tutor, restored.BcUser.Role);
        Assert.Equal(TutorStatus.Approved, restored.Status);
        Assert.True(restored.IsActive);
        Assert.Equal(TutorApplicationStage.Placement, restored.ApplicationStage);
        Assert.Equal(1, restored.ProgrammeId);
        Assert.Equal(2, restored.YearOfStudy);
        Assert.Equal("Pretoria Campus", restored.CampusOfStudy);
        Assert.Equal(originalCreatedAt, restored.CreatedAt);
        Assert.Equal(originalDeregisteredAt, restored.DeregisteredAt);
        Assert.Equal("Completed previous term", restored.DeregistrationReason);
        Assert.Equal(3, restored.TutorCourseModules.Count);
        Assert.True(restored.TutorCourseModules.Single(a => a.ProgrammeModuleId == 1).IsActive);
        Assert.False(restored.TutorCourseModules.Single(a => a.ProgrammeModuleId == 2).IsActive);
        Assert.True(restored.TutorCourseModules.Single(a => a.ProgrammeModuleId == 3).IsActive);
        var historicalBooking = await context.Bookings.Include(b => b.TutorCourseModule).SingleAsync();
        Assert.Equal(restored.TutorId, historicalBooking.TutorId);
        Assert.Equal(2, historicalBooking.TutorCourseModule.ProgrammeModuleId);
        Assert.Equal(BookingStatus.Completed, historicalBooking.Status);

        var placements = new BC_CampusLearn.Pages.Administrator.Admin.ApplicationsModel(context) { Stage = "placement" };
        await placements.OnGetAsync(CancellationToken.None);
        var candidate = Assert.Single(placements.Candidates.Where(c => c.TutorId == 1));
        Assert.Equal(2, candidate.ModuleCount);
        Assert.Equal(2, candidate.Modules.Count);
        Assert.DoesNotContain(candidate.Modules, module => module.Code == "OLD101");
        Assert.DoesNotContain(placements.StudentOptions, option => option.Value == "1");
    }

    [Theory]
    [InlineData(TutorStatus.Pending)]
    [InlineData(TutorStatus.Rejected)]
    public async Task ExistingApplicantsCannotBeReactivatedThroughAddTutor(TutorStatus status)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        Tutor tutor = await context.Tutors.Include(t => t.BcUser).SingleAsync(t => t.TutorId == 1);
        tutor.Status = status;
        tutor.IsActive = false;
        await context.SaveChangesAsync();
        var service = new TestStudentDetailsService();
        var page = new IndexModel(context, service) { ManualTutor = new() { BcUserId = 1 } };
        SetPageContext(page);
        await page.OnGetAsync(CancellationToken.None);
        Assert.DoesNotContain(page.StudentOptions, option => option.Value == "1");
        Assert.Equal(400, Assert.IsType<JsonResult>(await page.OnGetStudentDetailsAsync(1, CancellationToken.None)).StatusCode);
        await page.OnPostAddTutorAsync(CancellationToken.None);
        Assert.NotNull(page.PageError);
        Assert.Empty(service.RequestedNumbers);
        Assert.Equal(status, tutor.Status);
        Assert.False(tutor.IsActive);
    }

    [Theory]
    [InlineData("/Administrator/Admin/Compensation", true)]
    [InlineData("/Administrator/Admin/Compensation?Period=custom&From=2026-09-21&To=2026-10-31", true)]
    [InlineData(null, false)]
    [InlineData("https://example.com/Administrator/Admin/Compensation", false)]
    [InlineData("//example.com/Administrator/Admin/Compensation", false)]
    [InlineData("/Administrator/Admin/Compensation/../Dashboard", false)]
    [InlineData("/Administrator/Admin/Compensation?x=\\example.com", false)]
    public void ProfileReturnLinkAcceptsOnlyCompensationPage(string? returnUrl, bool accepted)
    {
        using var context = CreateContext();
        var page = new ProfileModel(context) { ReturnUrl = returnUrl };
        Assert.Equal(accepted ? returnUrl : null, page.CompensationReturnUrl);
    }

    [Fact]
    public async Task StudentLookupUsesSelectedStudentNumberAndReturnsVerifiedDetails()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 100, PersonnelNumber = "S100", DisplayName = "Outdated Name", Role = BcUserRole.Student
        });
        await context.SaveChangesAsync();
        var service = new TestStudentDetailsService();
        var page = new IndexModel(context, service);
        var response = Assert.IsType<JsonResult>(await page.OnGetStudentDetailsAsync(100, CancellationToken.None));
        var payload = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal("New Tutor", payload.GetProperty("displayName").GetString());
        Assert.Equal(1, payload.GetProperty("programmeId").GetInt32());
        Assert.Equal(2, payload.GetProperty("YearOfStudy").GetInt32());
        Assert.Equal(["S100"], service.RequestedNumbers);

        var rejected = Assert.IsType<JsonResult>(await page.OnGetStudentDetailsAsync(1, CancellationToken.None));
        Assert.Equal(400, rejected.StatusCode);
        Assert.Single(service.RequestedNumbers);
        Assert.Equal("Outdated Name", (await context.BcUsers.FindAsync(100))!.DisplayName);
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("not-found")]
    [InlineData("invalid")]
    [InlineData("wrong-student")]
    [InlineData("unsupported-programme")]
    [InlineData("missing-campus")]
    [InlineData("invalid-year")]
    [InlineData("missing-student-number")]
    public async Task UnverifiedStudentsCannotBeAdded(string scenario)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var student = new BcUser
        {
            BcUserId = 100, PersonnelNumber = scenario == "missing-student-number" ? null : "S100",
            DisplayName = "Original Name", Role = BcUserRole.Student
        };
        context.BcUsers.Add(student);
        await context.SaveChangesAsync();
        var details = new StudentDetails("S100", "New", null, "Tutor", "verified@example.test", "Computing", 2, "Pretoria Campus");
        var result = scenario switch
        {
            "unavailable" => new StudentDetailsResult(StudentDetailsStatus.Unavailable),
            "not-found" => new StudentDetailsResult(StudentDetailsStatus.NotFound),
            "invalid" => new StudentDetailsResult(StudentDetailsStatus.InvalidResponse),
            "wrong-student" => StudentDetailsResult.Success(details with { StudentNumber = "S101" }),
            "unsupported-programme" => StudentDetailsResult.Success(details with { Programme = "Unknown" }),
            "invalid-year" => StudentDetailsResult.Success(details with { YearOfStudy = 0 }),
            _ => StudentDetailsResult.Success(details with { Campus = " " })
        };
        var page = new IndexModel(context, new TestStudentDetailsService(result))
        {
            ManualTutor = new IndexModel.ManualTutorInput
            {
                BcUserId = 100, ProgrammeId = 1, YearOfStudy = 2, OverallAverage = 80,
                CampusOfStudy = "Pretoria Campus", ProgrammeModuleIds = [1]
            }
        };
        SetPageContext(page);
        var lookup = Assert.IsType<JsonResult>(await page.OnGetStudentDetailsAsync(100, CancellationToken.None));
        Assert.Equal(400, lookup.StatusCode);
        await page.OnPostAddTutorAsync(CancellationToken.None);
        Assert.NotNull(page.PageError);
        Assert.Equal(18, await context.Tutors.CountAsync());
        Assert.Null(student.Tutor);
        Assert.Equal("Original Name", student.DisplayName);
        Assert.Equal(BcUserRole.Student, student.Role);
    }

    [Theory]
    [InlineData("Pretoria")]
    [InlineData("pretoria campus")]
    [InlineData("Kempton Park")]
    [InlineData("Belgium Campus Pretoria")]
    public async Task VerifiedCampusLabelsAreDisplayedAndSavedAsReturnedByApi(string campus)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        context.BcUsers.Add(new BcUser
        {
            BcUserId = 100, PersonnelNumber = "S100", DisplayName = "New Tutor", Role = BcUserRole.Student
        });
        await context.SaveChangesAsync();
        var details = new StudentDetails("S100", "New", null, "Tutor", "verified@example.test", "Computing", 2, campus);
        var page = new IndexModel(context, new TestStudentDetailsService(StudentDetailsResult.Success(details)))
        {
            ManualTutor = new IndexModel.ManualTutorInput
            {
                BcUserId = 100, OverallAverage = 80, ProgrammeModuleIds = [1],
                CampusOfStudy = "Untrusted submitted campus"
            }
        };
        SetPageContext(page);
        var response = Assert.IsType<JsonResult>(await page.OnGetStudentDetailsAsync(100, CancellationToken.None));
        Assert.Null(response.StatusCode);
        Assert.Equal(campus, JsonSerializer.SerializeToElement(response.Value).GetProperty("campus").GetString());

        await page.OnPostAddTutorAsync(CancellationToken.None);
        Assert.Null(page.PageError);
        var tutor = await context.Tutors.SingleAsync(tutor => tutor.BcUserId == 100);
        Assert.Equal(campus, tutor.CampusOfStudy);
        Assert.Equal(2, tutor.YearOfStudy);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(999, false)]
    public async Task DirectoryAddsEligibleStudentAndRejectsInvalidModules(int moduleId, bool valid)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var student = new BcUser
        {
            BcUserId = 100, PersonnelNumber = "S100", DisplayName = "New Tutor",
            Role = BcUserRole.Student
        };
        context.BcUsers.Add(student);
        await context.SaveChangesAsync();
        var page = new IndexModel(context, new TestStudentDetailsService())
        {
            ManualTutor = new IndexModel.ManualTutorInput
            {
                BcUserId = 100, ProgrammeId = 999, YearOfStudy = 4,
                OverallAverage = 80, CampusOfStudy = "Online",
                ProgrammeModuleIds = [moduleId]
            }
        };
        SetPageContext(page);
        await page.OnGetAsync(CancellationToken.None);
        Assert.Single(page.StudentOptions);
        Assert.Equal("100", page.StudentOptions[0].Value);

        var redirect = Assert.IsType<RedirectToPageResult>(
            await page.OnPostAddTutorAsync(CancellationToken.None));
        Assert.Null(redirect.PageName);
        Assert.True(redirect.RouteValues is null || !redirect.RouteValues.ContainsKey("Stage"));
        await page.OnGetAsync(CancellationToken.None);
        if (valid)
        {
            Assert.Equal(19, page.TotalTutors);
            Assert.Equal(BcUserRole.Tutor, student.Role);
            Assert.NotNull(student.Tutor);
            Assert.Equal(TutorStatus.Approved, student.Tutor.Status);
            Assert.Equal(TutorApplicationStage.Placement, student.Tutor.ApplicationStage);
            Assert.True(student.Tutor.IsActive);
            Assert.Equal(1, student.Tutor.ProgrammeId);
            Assert.Equal(2, student.Tutor.YearOfStudy);
            Assert.Equal("Pretoria Campus", student.Tutor.CampusOfStudy);
            Assert.Equal("verified@example.test", student.Email);
            Assert.Equal(1, Assert.Single(student.Tutor.TutorCourseModules).ProgrammeModuleId);
            Assert.Empty(page.StudentOptions);
            Assert.Equal("New Tutor was added as a tutor.", page.PageMessage);

            await page.OnPostAddTutorAsync(CancellationToken.None);
            Assert.Equal(19, await context.Tutors.CountAsync());
            Assert.Equal("This student already has a tutor profile.", page.PageError);
        }
        else
        {
            Assert.Equal(18, page.TotalTutors);
            Assert.Equal(BcUserRole.Student, student.Role);
            Assert.Null(student.Tutor);
            Assert.NotNull(page.PageError);
        }
    }

    [Fact]
    public async Task AdminReviewSavesFourResponsesAndRecordingTime()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        TutorCourseModule assignment = await context.TutorCourseModules.FirstAsync();
        Booking booking = new()
        {
            TutorId = assignment.TutorId,
            TutorCourseModule = assignment,
            ProgrammeModuleId = assignment.ProgrammeModuleId,
            Status = BookingStatus.Completed,
            StudentEvaluation = new StudentEvaluation(),
            TutorEvaluation = new TutorStudentEvaluation(),
            SessionReviews = [new SessionReview
            {
                ReviewerBcUserId = 50,
                Reviewer = new BcUser
                {
                    BcUserId = 50,
                    PersonnelNumber = "H50",
                    DisplayName = "Tutor Head",
                    Role = BcUserRole.HeadOfTutors
                },
                Rating = 5,
                CreatedAt = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.Zero)
            }]
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        DateTimeOffset recordedAt = new(2026, 9, 21, 10, 30, 0, TimeSpan.Zero);
        var page = new SessionDetailsModel(
            context, new TestWebHostEnvironment(), new TestCurrentUserService(),
            new TestTimeProvider(recordedAt))
        {
            AdminReviewInput = new AdminSessionReviewInput
            {
                HeadConfirmedQuality = false,
                ReviewEvidenceIsConsistent = false,
                ConcernsResolvedOrDocumented = true,
                EvidenceSupportsApproval = false
            }
        };
        SetPageContext(page);

        RedirectToPageResult result = Assert.IsType<RedirectToPageResult>(
            await page.OnPostAdminReviewAsync(booking.BookingId, CancellationToken.None));
        Assert.Equal("/Administrator/Tutors/SessionDetails", result.PageName);
        Assert.Equal(booking.BookingId, result.RouteValues?["id"]);
        Assert.Equal("Session declined successfully.", page.AdminReviewMessage);

        AdminSessionReview saved = await context.AdminSessionReviews.SingleAsync();
        Assert.Equal(booking.BookingId, saved.BookingId);
        Assert.Equal(1, saved.ReviewerBcUserId);
        Assert.False(saved.ReviewEvidenceIsConsistent);
        Assert.False(saved.HeadConfirmedQuality);
        Assert.True(saved.ConcernsResolvedOrDocumented);
        Assert.False(saved.EvidenceSupportsApproval);
        Assert.Equal(recordedAt, saved.RecordedAt);
    }

    [Fact]
    public async Task SuperAdminCanRecordFinalDecisionAfterEveryPriorReview()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        TutorCourseModule assignment = await context.TutorCourseModules.FirstAsync();
        var superAdmin = new BcUser
        {
            BcUserId = 60,
            PersonnelNumber = "SA60",
            DisplayName = "Superadmin",
            Role = BcUserRole.SuperAdmin
        };
        context.BcUsers.Add(superAdmin);
        Booking booking = new()
        {
            TutorId = assignment.TutorId,
            TutorCourseModule = assignment,
            ProgrammeModuleId = assignment.ProgrammeModuleId,
            Status = BookingStatus.Completed,
            StudentEvaluation = new StudentEvaluation(),
            TutorEvaluation = new TutorStudentEvaluation(),
            AdminSessionReview = new AdminSessionReview
            {
                ReviewerBcUserId = 1,
                HeadConfirmedQuality = true,
                ReviewEvidenceIsConsistent = true,
                ConcernsResolvedOrDocumented = true,
                EvidenceSupportsApproval = true,
                RecordedAt = DateTimeOffset.UtcNow
            },
            SessionReviews = [new SessionReview
            {
                ReviewerBcUserId = 50,
                Reviewer = new BcUser
                {
                    BcUserId = 50,
                    PersonnelNumber = "H50",
                    DisplayName = "Tutor Head",
                    Role = BcUserRole.HeadOfTutors
                },
                Rating = 5,
                CreatedAt = DateTimeOffset.UtcNow
            }]
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        DateTimeOffset recordedAt = new(2026, 10, 7, 9, 30, 0, TimeSpan.Zero);
        var page = new SessionDetailsModel(
            context,
            new TestWebHostEnvironment(),
            new TestCurrentUserService(BcUserRole.SuperAdmin, superAdmin.BcUserId),
            new TestTimeProvider(recordedAt));
        SetPageContext(page);

        RedirectToPageResult result = Assert.IsType<RedirectToPageResult>(
            await page.OnPostSuperAdminReviewAsync(
            booking.BookingId,
            accepted: false,
            CancellationToken.None));
        Assert.Equal("/Administrator/Admin/BookingsAndSessions", result.PageName);

        SuperAdminSessionReview saved =
            await context.SuperAdminSessionReviews.SingleAsync();
        Assert.False(saved.IsAccepted);
        Assert.Equal(superAdmin.BcUserId, saved.ReviewerBcUserId);
        Assert.Equal(recordedAt, saved.RecordedAt);
    }

    [Fact]
    public async Task SessionDetailsLoadsTutorHeadReviewFromSessionReviews()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        TutorCourseModule assignment = await context.TutorCourseModules.FirstAsync();
        var tutorHead = new BcUser
        {
            BcUserId = 50,
            PersonnelNumber = "H50",
            DisplayName = "Tutor Head",
            Role = BcUserRole.HeadOfTutors
        };
        var review = new SessionReview
        {
            Reviewer = tutorHead,
            ReviewerBcUserId = tutorHead.BcUserId,
            Rating = 4,
            StudentEngagement = "Yes",
            ConcernLevel = "Concerns",
            OverallAssessment = "Good",
            Decision = "Approve",
            Comment = "Strong session.",
            CreatedAt = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.Zero)
        };
        var booking = new Booking
        {
            TutorId = assignment.TutorId,
            TutorCourseModule = assignment,
            ProgrammeModuleId = assignment.ProgrammeModuleId,
            Status = BookingStatus.Completed,
            StudentEvaluation = new StudentEvaluation(),
            TutorEvaluation = new TutorStudentEvaluation(),
            SessionReviews = [review]
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var page = new SessionDetailsModel(
            context, new TestWebHostEnvironment(), new TestCurrentUserService(),
            TimeProvider.System);
        SetPageContext(page);

        Assert.IsType<PageResult>(
            await page.OnGetAsync(booking.BookingId, CancellationToken.None));
        Assert.Equal(review.SessionReviewId, page.TutorHeadReview?.SessionReviewId);
        Assert.True(page.CanRecordAdminReview);
        Assert.True(page.HasVisibleTutorHeadConcern);
        Assert.Contains(page.TutorHeadReviewAnswers,
            answer => answer.Question == "4. Decision" && answer.Value == "Approve");
        Assert.Equal(5, page.TutorHeadReviewAnswers.Count);
        Assert.Contains(page.TutorHeadReviewAnswers,
            answer => answer.Question == "Additional comments" &&
                answer.Value == "Strong session.");

        booking.AdminSessionReview = new AdminSessionReview
        {
            ReviewerBcUserId = 1,
            EvidenceSupportsApproval = true,
            RecordedAt = DateTimeOffset.UtcNow
        };
        await context.SaveChangesAsync();
        var approvedPage = new SessionDetailsModel(
            context, new TestWebHostEnvironment(), new TestCurrentUserService(),
            TimeProvider.System);
        SetPageContext(approvedPage);
        Assert.IsType<PageResult>(await approvedPage.OnGetAsync(
            booking.BookingId,
            CancellationToken.None));
        Assert.False(approvedPage.HasVisibleTutorHeadConcern);
    }

    [Fact]
    public async Task ProfilePaginatesSessionsByReviewCompletenessBeforeDate()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        TutorCourseModule assignment = await context.TutorCourseModules.FirstAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        DateTimeOffset date = new(now.Year, now.Month, 15, 12, 0, 0, now.Offset);
        Booking[] bookings =
        [
            new() { ScheduledStartTime = date.AddHours(5) },
            new() { ScheduledStartTime = date.AddHours(4), TutorEvaluation = new TutorStudentEvaluation() },
            new() { ScheduledStartTime = date.AddHours(1), StudentEvaluation = new StudentEvaluation(), TutorEvaluation = new TutorStudentEvaluation() },
            new() { ScheduledStartTime = date.AddHours(3), StudentEvaluation = new StudentEvaluation() },
            new() { ScheduledStartTime = date.AddHours(2), StudentEvaluation = new StudentEvaluation(), TutorEvaluation = new TutorStudentEvaluation() },
            new() { ScheduledStartTime = date }
        ];
        foreach (Booking booking in bookings)
        {
            booking.TutorId = assignment.TutorId;
            booking.TutorCourseModule = assignment;
            booking.ProgrammeModuleId = assignment.ProgrammeModuleId;
            booking.Status = BookingStatus.Completed;
        }
        context.Bookings.AddRange(bookings);
        await context.SaveChangesAsync();

        var page = new ProfileModel(context);
        await page.OnGetAsync(assignment.TutorId, CancellationToken.None);
        Assert.Equal(new[] { bookings[4], bookings[2], bookings[1], bookings[3], bookings[0] }
            .Select(item => item.BookingId), page.RecentSessions.Select(item => item.BookingId));

        page.SessionPage = 2;
        await page.OnGetAsync(assignment.TutorId, CancellationToken.None);
        Assert.Equal(bookings[5].BookingId, Assert.Single(page.RecentSessions).BookingId);
    }

    [Theory]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    public async Task ProfileChartCountsOnlyCompletedSessionsInPeriodAndReturnsTopFive(string period)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var now = DateTimeOffset.UtcNow;
        for (int moduleId = 2; moduleId <= 7; moduleId++)
        {
            var module = new ProgrammeModule { ProgrammeModuleId = moduleId, ProgrammeId = 1, ModuleCode = $"MOD{moduleId}", ModuleName = $"Module {moduleId}" };
            var assignment = new TutorCourseModule { TutorId = 1, ProgrammeModule = module };
            context.TutorCourseModules.Add(assignment);
            for (int session = 0; session < moduleId; session++)
            {
                context.Bookings.Add(new Booking { TutorId = 1, TutorCourseModule = assignment, ProgrammeModule = module, Status = BookingStatus.Completed, CompletedAt = now, ScheduledStartTime = now });
            }
            context.Bookings.Add(new Booking { TutorId = 1, TutorCourseModule = assignment, ProgrammeModule = module, Status = BookingStatus.Cancelled, CompletedAt = now, ScheduledStartTime = now });
            context.Bookings.Add(new Booking { TutorId = 1, TutorCourseModule = assignment, ProgrammeModule = module, Status = BookingStatus.Completed, CompletedAt = now.AddMonths(-2), ScheduledStartTime = now.AddMonths(-2) });
        }
        await context.SaveChangesAsync();
        var existingBooking = await context.Bookings.FirstAsync();
        var hiddenBooking = new Booking
        {
            TutorId = 1, ProgrammeModuleId = existingBooking.ProgrammeModuleId,
            Status = BookingStatus.Confirmed, ScheduledStartTime = now.AddDays(1)
        };
        context.Bookings.Add(hiddenBooking);
        await context.SaveChangesAsync();
        var page = new ProfileModel(context) { Period = period };
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(new[] { 7, 6, 5, 4, 3 }, page.TopModules.Select(module => module.Count));
        Assert.Equal(27, page.CompletedSessions);
        Assert.Equal(27, page.PendingStudentReviews);
        Assert.Equal(5, page.RecentSessions.Count);
        Assert.All(page.RecentSessions, item => Assert.Contains(item.Status, new[] { BookingStatus.Completed, BookingStatus.Cancelled }));
        var details = new SessionDetailsModel(
            context, new TestWebHostEnvironment(),
            new TestCurrentUserService(), TimeProvider.System);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(await details.OnGetAsync(existingBooking.BookingId, CancellationToken.None));
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await details.OnGetAsync(hiddenBooking.BookingId, CancellationToken.None));
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await details.OnGetAsync(int.MaxValue, CancellationToken.None));
        Assert.Equal(7, page.TotalSessionPages);
        var firstPageIds = page.RecentSessions.Select(item => item.BookingId).ToArray();
        page.SessionPage = 2;
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(5, page.RecentSessions.Count);
        Assert.DoesNotContain(page.RecentSessions, item => firstPageIds.Contains(item.BookingId));
        page.SessionPage = int.MaxValue;
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(7, page.SessionPage);
        Assert.Equal(3, page.RecentSessions.Count);
        Assert.Null(page.AverageRating);
        await page.OnGetAsync(18, CancellationToken.None);
        Assert.Empty(page.TopModules);
    }

    [Fact]
    public async Task PendingTutorReviewsExcludeReviewedUncompletedAndOutOfPeriodSessions()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var date = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.FromHours(2));
        var assignment = await context.TutorCourseModules.FirstAsync(item => item.TutorId == 1);
        foreach (var kind in new[] { "pending", "reviewed", "cancelled", "outside" })
        {
            context.Bookings.Add(new Booking
            {
                TutorId = 1, TutorCourseModule = assignment, ProgrammeModuleId = 1,
                Status = kind == "cancelled" ? BookingStatus.Cancelled : BookingStatus.Completed,
                CompletedAt = date,
                ScheduledStartTime = kind == "outside" ? date.AddDays(-1) : date,
                TutorEvaluation = kind == "reviewed" ? new TutorStudentEvaluation() : null
            });
        }
        await context.SaveChangesAsync();
        var page = new ProfileModel(context)
        {
            Period = "custom", StartDate = new DateOnly(2026, 9, 10), EndDate = new DateOnly(2026, 9, 10)
        };
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(1, page.PendingTutorReviews);
        Assert.Equal(2, page.PendingStudentReviews);
        Assert.Equal(2, page.CompletedSessions);
    }

    [Fact]
    public async Task DefaultMonthAndEachPresetFilterEverySummaryCard()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(2));
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var assignment = await context.TutorCourseModules.FirstAsync(item => item.TutorId == 1);
        for (int days = -40; days <= 1; days++)
        {
            var date = today.AddDays(days).AddHours(12);
            context.Bookings.Add(new Booking
            {
                TutorId = 1, TutorCourseModule = assignment, ProgrammeModuleId = 1,
                Status = BookingStatus.Completed, CompletedAt = now, ScheduledStartTime = date
            });
            context.TutorModuleChangeRequests.Add(new TutorModuleChangeRequest
            {
                TutorId = 1, ProgrammeModuleId = 1, Status = TutorAccountRequestStatus.Pending,
                SubmittedAt = date.UtcDateTime
            });
        }
        await context.SaveChangesAsync();
        var page = new ProfileModel(context);
        Assert.Equal("month", page.Period);
        foreach (var period in new[] { "month", "week", "day" })
        {
            page.Period = period;
            await page.OnGetAsync(1, CancellationToken.None);
            var start = period switch
            {
                "day" => today,
                "week" => today.AddDays(-((int)today.DayOfWeek + 6) % 7),
                _ => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset)
            };
            var end = period == "day" ? start.AddDays(1) : period == "week" ? start.AddDays(7) : start.AddMonths(1);
            var expected = Enumerable.Range(-40, 42).Count(days => today.AddDays(days).AddHours(12) >= start
                && today.AddDays(days).AddHours(12) < end);
            Assert.Equal(expected, page.CompletedSessions);
            Assert.Equal(expected, page.PendingStudentReviews);
            Assert.Equal(expected, page.PendingTutorReviews);
            Assert.Equal(expected, Assert.Single(page.TopModules).Count);
            Assert.Equal(expected, page.ModuleChangeRequests);
        }
    }

    [Fact]
    public async Task CustomDatesFilterAllActivityUsingInclusiveSouthAfricanDates()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var start = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.FromHours(2));
        var end = start.AddDays(2);
        var assignment = await context.TutorCourseModules.FirstAsync(item => item.TutorId == 1);
        foreach (var date in new[] { start.AddTicks(-1), start, end.AddTicks(-1), end })
        {
            var booking = new Booking
            {
                TutorId = 1, TutorCourseModule = assignment, ProgrammeModuleId = 1,
                Status = BookingStatus.Completed, CompletedAt = date.ToUniversalTime(), ScheduledStartTime = date.ToUniversalTime()
            };
            context.Bookings.Add(booking);
            context.StudentEvaluations.Add(new StudentEvaluation
            {
                Booking = booking,
                ModeRating = date >= start && date < end ? (byte)5 : (byte)1,
                PlatformRating = 5
            });
            context.TutorModuleChangeRequests.Add(new TutorModuleChangeRequest
            {
                TutorId = 1, ProgrammeModuleId = 1, Status = TutorAccountRequestStatus.Pending,
                SubmittedAt = date.UtcDateTime
            });
        }
        await context.SaveChangesAsync();
        var page = new ProfileModel(context)
        {
            Period = "custom", StartDate = new DateOnly(2026, 9, 10), EndDate = new DateOnly(2026, 9, 11), SessionPage = 99
        };
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Null(page.DateFilterError);
        Assert.Equal(2, page.CompletedSessions);
        Assert.Equal(0, page.PendingStudentReviews);
        Assert.Equal(2, page.ModuleChangeRequests);
        Assert.Equal(2, page.ReviewCount);
        Assert.Equal(5d, page.AverageRating);
        Assert.Equal(2, Assert.Single(page.TopModules).Count);
        Assert.Equal(2, page.RecentSessions.Count);
        Assert.Equal(1, page.SessionPage);

        page.EndDate = page.StartDate;
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(1, page.CompletedSessions);
        Assert.Single(page.RecentSessions);
        Assert.Equal(1, page.ReviewCount);
        Assert.Equal(5d, page.AverageRating);
        page.StartDate = new DateOnly(2020, 1, 1);
        page.EndDate = page.StartDate;
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.Equal(0, page.CompletedSessions);
        Assert.Empty(page.RecentSessions);
        Assert.Empty(page.TopModules);
        Assert.Null(page.AverageRating);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("2026-09-12", "2026-09-10")]
    [InlineData("2026-09-12", "9999-12-31")]
    public async Task InvalidCustomDatesShowValidationWithoutThrowing(string? start, string? end)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var page = new ProfileModel(context)
        {
            Period = "custom", StartDate = start is null ? null : DateOnly.Parse(start),
            EndDate = end is null ? null : DateOnly.Parse(end)
        };
        await page.OnGetAsync(1, CancellationToken.None);
        Assert.NotNull(page.DateFilterError);
    }

    [Fact]
    public async Task TutorListExcludesUnplacedInactiveAndUnapprovedTutorsBeforeCountingAndPaging()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var tutors = await context.Tutors.OrderBy(tutor => tutor.TutorId).ToListAsync();
        tutors[0].IsActive = false;
        tutors[1].ApplicationStage = TutorApplicationStage.Interview;
        tutors[2].Status = TutorStatus.Pending;
        tutors[3].Status = TutorStatus.Rejected;
        tutors[4].Status = TutorStatus.Suspended;
        tutors[5].Status = TutorStatus.Deregistered;
        tutors[5].IsActive = false;
        await context.SaveChangesAsync();

        var page = new IndexModel(context, new TestStudentDetailsService());
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(12, page.TotalTutors);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(Enumerable.Range(7, 8), page.Tutors.Select(tutor => tutor.TutorId));
        page.TutorPage = 2;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(Enumerable.Range(15, 4), page.Tutors.Select(tutor => tutor.TutorId));

        page.SearchName = "Tutor 01";
        await page.OnGetAsync(CancellationToken.None);
        Assert.Empty(page.Tutors);
        Assert.Equal(0, page.TotalTutors);
        Assert.Equal(1, page.TutorPage);
    }

    [Fact]
    public async Task PaginationReturnsEightRowsWithoutOverlapAndClampsInvalidPages()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var page = new IndexModel(context, new TestStudentDetailsService());
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(8, page.Tutors.Count);
        Assert.Equal(18, page.TotalTutors);
        Assert.Equal(3, page.TotalPages);
        var firstIds = page.Tutors.Select(tutor => tutor.TutorId).ToArray();
        page.TutorPage = 2;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(8, page.Tutors.Count);
        Assert.DoesNotContain(page.Tutors, tutor => firstIds.Contains(tutor.TutorId));
        page.TutorPage = int.MaxValue;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(3, page.TutorPage);
        Assert.Equal(2, page.Tutors.Count);
        page.TutorPage = -5;
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(1, page.TutorPage);
    }

    [Fact]
    public async Task TutorDirectoryExcludesRejectedApplications()
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var rejectedApplicant = new Tutor
        {
            TutorId = 19,
            BcUser = new BcUser
            {
                BcUserId = 19,
                PersonnelNumber = "S19",
                DisplayName = "Rejected Applicant"
            },
            ProgrammeId = 1,
            YearOfStudy = 2,
            ReasonForTutoring = "Reason",
            TeachingStyle = "Style",
            PreviousTutoringExperience = "Experience",
            CampusOfStudy = "Pretoria",
            DemonstrationVideoUrl = "",
            Status = TutorStatus.Rejected
        };
        context.Tutors.Add(rejectedApplicant);
        await context.SaveChangesAsync();

        var page = new IndexModel(context, new TestStudentDetailsService()) { SearchName = "Rejected Applicant" };
        await page.OnGetAsync(CancellationToken.None);

        Assert.Empty(page.Tutors);
        Assert.Equal(0, page.TotalTutors);
    }

    [Theory]
    [InlineData("PRG")]
    [InlineData("Programming")]
    public async Task CombinedFiltersApplyBeforePaginationAndSupportMultipleYears(string module)
    {
        await using var context = CreateContext();
        await SeedTutors(context);
        var page = new IndexModel(context, new TestStudentDetailsService())
        {
            SearchName = " Tutor ", SearchModule = module,
            SearchCourse = " Computing ", Years = [2, 3], TutorPage = 2
        };
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal(9, page.TotalTutors);
        Assert.Single(page.Tutors);
        Assert.All(page.Tutors, tutor => Assert.Contains(tutor.YearOfStudy, new[] { 2, 3 }));
        page.SearchName = "No matching tutor";
        await page.OnGetAsync(CancellationToken.None);
        Assert.Empty(page.Tutors);
        Assert.Equal(0, page.TotalTutors);
        Assert.Equal(1, page.TutorPage);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SeedTutors(ApplicationDbContext context)
    {
        var programme = new ProgrammeOfStudy { Id = 1, Name = "Computing" };
        var module = new ProgrammeModule
        {
            ProgrammeModuleId = 1, Programme = programme,
            ModuleCode = "PRG101", ModuleName = "Programming"
        };
        for (int id = 1; id <= 18; id++)
        {
            context.Tutors.Add(new Tutor
            {
                TutorId = id,
                BcUser = new BcUser { BcUserId = id, PersonnelNumber = $"S{id}", DisplayName = $"Tutor {id:00}" },
                Programme = programme, YearOfStudy = 2 + (id % 3),
                ReasonForTutoring = "Reason", TeachingStyle = "Style",
                PreviousTutoringExperience = "Experience", CampusOfStudy = "Pretoria",
                DemonstrationVideoUrl = "", Status = TutorStatus.Approved,
                ApplicationStage = TutorApplicationStage.Placement, IsActive = true,
                TutorCourseModules = id <= 13
                    ? [new TutorCourseModule { ProgrammeModule = module }] : []
            });
        }
        await context.SaveChangesAsync();
    }

    private sealed class TestStudentDetailsService(StudentDetailsResult? result = null) : IStudentDetailsService
    {
        public List<string> RequestedNumbers { get; } = [];
        public Task<StudentDetailsResult> GetAsync(string personnelNumber, CancellationToken cancellationToken = default)
        {
            RequestedNumbers.Add(personnelNumber);
            return Task.FromResult(result ?? StudentDetailsResult.Success(new StudentDetails(
                personnelNumber, "New", null, "Tutor", "verified@example.test", "Computing", 2, "Pretoria Campus")));
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BC_CampusLearn.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestCurrentUserService(
        BcUserRole role = BcUserRole.Admin,
        int userId = 1) : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public CurrentUser GetRequiredUser() =>
            new(userId, "A1", "Administrator", null, role);
    }

    private static void SetPageContext(PageModel page)
    {
        var httpContext = new DefaultHttpContext();
        page.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(
                new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
        page.TempData = new TempDataDictionary(
            httpContext, new TestTempDataProvider());
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(
            HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

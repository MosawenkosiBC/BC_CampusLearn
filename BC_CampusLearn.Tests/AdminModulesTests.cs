using System.Security.Claims;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Modules;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BC_CampusLearn.Tests;

public class AdminModulesTests
{
    [Fact]
    public async Task CreateNormalizesCodeAndRejectsDuplicateWithinProgramme()
    {
        await using var db = await Seed();
        var page = Setup(new IndexModel(db));
        page.Input = new() { ProgrammeId = 1, ModuleCode = " new101 ", ModuleName = " New module ", YearOfStudy = 2 };
        Assert.IsType<RedirectToPageResult>(await page.OnPostCreateAsync(default));
        var module = await db.ProgrammeModules.SingleAsync(m => m.ModuleCode == "NEW101");
        Assert.Equal("New module", module.ModuleName);
        Assert.Equal(2, module.YearOfStudy);
        var duplicate = Setup(new IndexModel(db));
        duplicate.Input = new() { ProgrammeId = 1, ModuleCode = "new101", ModuleName = "Duplicate", YearOfStudy = 1 };
        Assert.IsType<PageResult>(await duplicate.OnPostCreateAsync(default));
        Assert.False(duplicate.ModelState.IsValid);
        Assert.Equal(2, await db.ProgrammeModules.CountAsync());
    }

    [Fact]
    public async Task InvalidModuleEditReopensEditModal()
    {
        await using var db = await Seed();
        var page = Setup(new DetailsModel(db));
        page.Input = new() { ProgrammeId = 1, ModuleCode = " ", ModuleName = " ", YearOfStudy = 1 };

        Assert.IsType<PageResult>(await page.OnPostSaveAsync(1, default));
        Assert.True(page.ShowEdit);
        Assert.False(page.ModelState.IsValid);
    }

    [Fact]
    public async Task ApproveAddsAssignmentNotifiesTutorAndCannotReviewTwice()
    {
        await using var db = await Seed();
        var request = new TutorModuleChangeRequest { TutorId = 1, ProgrammeModuleId = 1, RequestType = TutorModuleChangeRequestType.Add, SubmittedAt = DateTime.UtcNow };
        db.TutorModuleChangeRequests.Add(request); await db.SaveChangesAsync();
        var page = Setup(new IndexModel(db)
        {
            SelectedRequestIds = [request.TutorModuleChangeRequestId]
        });
        Assert.IsType<RedirectToPageResult>(await page.OnPostReviewAsync(request.TutorModuleChangeRequestId, true, "Approved", default));
        Assert.True((await db.TutorCourseModules.SingleAsync()).IsActive);
        Assert.Equal(TutorAccountRequestStatus.Approved, request.Status);
        Assert.Equal("Test Admin", request.ReviewedBy);
        Assert.NotNull(request.ReviewedAt);
        Assert.Single(db.UserNotifications);
        Assert.IsType<PageResult>(await page.OnPostReviewAsync(request.TutorModuleChangeRequestId, false, "Second decision", default));
        Assert.Equal(TutorAccountRequestStatus.Approved, request.Status);
        Assert.Single(db.UserNotifications);
    }

    [Fact]
    public async Task DeclineRequiresReasonAndLeavesAssignmentUnchanged()
    {
        await using var db = await Seed();
        var request = new TutorModuleChangeRequest { TutorId = 1, ProgrammeModuleId = 1 };
        db.TutorModuleChangeRequests.Add(request); await db.SaveChangesAsync();
        var page = Setup(new IndexModel(db)
        {
            SelectedRequestIds = [request.TutorModuleChangeRequestId]
        });
        Assert.IsType<PageResult>(await page.OnPostReviewAsync(request.TutorModuleChangeRequestId, false, " ", default));
        Assert.Equal(TutorAccountRequestStatus.Pending, request.Status);
        Assert.IsType<RedirectToPageResult>(await page.OnPostReviewAsync(request.TutorModuleChangeRequestId, false, "Not qualified yet", default));
        Assert.Empty(db.TutorCourseModules);
        Assert.Equal(TutorAccountRequestStatus.Declined, request.Status);
        Assert.Contains("Not qualified yet", (await db.UserNotifications.SingleAsync()).Message);
    }

    [Fact]
    public async Task ModuleRequestsAreGroupedByTutorAndSubmission()
    {
        await using var db = await Seed();
        db.ProgrammeModules.Add(new()
        {
            ProgrammeModuleId = 2,
            ProgrammeId = 1,
            ModuleCode = "DBS201",
            ModuleName = "Databases",
            YearOfStudy = 2
        });
        DateTime submittedAt = DateTime.UtcNow;
        db.TutorModuleChangeRequests.AddRange(
            new()
            {
                TutorId = 1,
                ProgrammeModuleId = 1,
                RequestType = TutorModuleChangeRequestType.Add,
                Reason = "Strong results in both modules.",
                SubmittedAt = submittedAt
            },
            new()
            {
                TutorId = 1,
                ProgrammeModuleId = 2,
                RequestType = TutorModuleChangeRequestType.Add,
                Reason = "Strong results in both modules.",
                SubmittedAt = submittedAt
            });
        await db.SaveChangesAsync();
        var page = Setup(new IndexModel(db) { Tab = "requests" });

        await page.OnGetAsync(default);

        var tutorGroup = Assert.Single(page.RequestGroups);
        var submission = Assert.Single(tutorGroup.Submissions);
        Assert.Equal(2, submission.Modules.Count);
        Assert.Equal(1, page.PendingRequests);
    }

    [Fact]
    public async Task ApprovingGroupedSubmissionUpdatesEverySelectedModule()
    {
        await using var db = await Seed();
        db.ProgrammeModules.Add(new()
        {
            ProgrammeModuleId = 2,
            ProgrammeId = 1,
            ModuleCode = "DBS201",
            ModuleName = "Databases",
            YearOfStudy = 2
        });
        DateTime submittedAt = DateTime.UtcNow;
        var requests = new[]
        {
            new TutorModuleChangeRequest
            {
                TutorId = 1,
                ProgrammeModuleId = 1,
                RequestType = TutorModuleChangeRequestType.Add,
                Reason = "Strong results in both modules.",
                SubmittedAt = submittedAt
            },
            new TutorModuleChangeRequest
            {
                TutorId = 1,
                ProgrammeModuleId = 2,
                RequestType = TutorModuleChangeRequestType.Add,
                Reason = "Strong results in both modules.",
                SubmittedAt = submittedAt
            }
        };
        db.TutorModuleChangeRequests.AddRange(requests);
        await db.SaveChangesAsync();
        var page = Setup(new IndexModel(db)
        {
            SelectedRequestIds = requests
                .Select(request => request.TutorModuleChangeRequestId)
                .ToList()
        });

        Assert.IsType<RedirectToPageResult>(await page.OnPostReviewAsync(
            requests[0].TutorModuleChangeRequestId,
            true,
            "Approved together",
            default));

        Assert.Equal(2, await db.TutorCourseModules.CountAsync(a => a.IsActive));
        Assert.All(requests, request =>
            Assert.Equal(TutorAccountRequestStatus.Approved, request.Status));
        Assert.Equal(2, await db.UserNotifications.CountAsync());
    }

    [Fact]
    public async Task ReviewingSubsetLeavesUnselectedModulesPending()
    {
        await using var db = await Seed();
        db.ProgrammeModules.Add(new()
        {
            ProgrammeModuleId = 2,
            ProgrammeId = 1,
            ModuleCode = "DBS201",
            ModuleName = "Databases",
            YearOfStudy = 2
        });
        DateTime submittedAt = DateTime.UtcNow;
        var selected = new TutorModuleChangeRequest
        {
            TutorId = 1,
            ProgrammeModuleId = 1,
            RequestType = TutorModuleChangeRequestType.Add,
            Reason = "Strong results in both modules.",
            SubmittedAt = submittedAt
        };
        var unselected = new TutorModuleChangeRequest
        {
            TutorId = 1,
            ProgrammeModuleId = 2,
            RequestType = TutorModuleChangeRequestType.Add,
            Reason = "Strong results in both modules.",
            SubmittedAt = submittedAt
        };
        db.TutorModuleChangeRequests.AddRange(selected, unselected);
        await db.SaveChangesAsync();
        var page = Setup(new IndexModel(db)
        {
            SelectedRequestIds = [selected.TutorModuleChangeRequestId]
        });

        Assert.IsType<RedirectToPageResult>(await page.OnPostReviewAsync(
            selected.TutorModuleChangeRequestId,
            true,
            "Approved selected module",
            default));

        Assert.Equal(TutorAccountRequestStatus.Approved, selected.Status);
        Assert.Equal(TutorAccountRequestStatus.Pending, unselected.Status);
        Assert.Equal(1, await db.TutorCourseModules.CountAsync(a => a.IsActive));
        Assert.Equal(1, await db.UserNotifications.CountAsync());
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.InProgress)]
    public async Task RemovalBlocksOutstandingSessions(BookingStatus status)
    {
        await using var db = await Seed();
        db.TutorCourseModules.Add(new() { TutorId = 1, ProgrammeModuleId = 1 });
        db.Bookings.Add(new() { TutorId = 1, ProgrammeModuleId = 1, Status = status });
        await db.SaveChangesAsync();
        var error = await new AdminModuleManagement(db).ChangeAssignmentAsync(1, 1, false, default);
        Assert.Contains("outstanding", error);
        Assert.True((await db.TutorCourseModules.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task RemovalPreservesHistoryAndReassignmentReusesExistingRow()
    {
        await using var db = await Seed();
        db.TutorCourseModules.Add(new() { TutorId = 1, ProgrammeModuleId = 1 });
        db.Bookings.Add(new() { TutorId = 1, ProgrammeModuleId = 1, Status = BookingStatus.Completed });
        await db.SaveChangesAsync();
        var management = new AdminModuleManagement(db);
        Assert.Null(await management.ChangeAssignmentAsync(1, 1, false, default));
        await db.SaveChangesAsync();
        Assert.False((await db.TutorCourseModules.SingleAsync()).IsActive);
        var booking = await db.Bookings.Include(b => b.TutorCourseModule).SingleAsync();
        Assert.NotNull(booking.TutorCourseModule);
        Assert.Null(await management.ChangeAssignmentAsync(1, 1, true, default));
        await db.SaveChangesAsync();
        Assert.True((await db.TutorCourseModules.SingleAsync()).IsActive);
        Assert.Single(db.Bookings);
    }

    [Fact]
    public async Task AssignmentAllowsDifferentProgrammeButRejectsInactiveTutor()
    {
        await using var db = await Seed();
        var tutor = await db.Tutors.SingleAsync();
        tutor.ProgrammeId = 2;
        var management = new AdminModuleManagement(db);
        Assert.Null(await management.ChangeAssignmentAsync(1, 1, true, default));
        await db.SaveChangesAsync();
        tutor.IsActive = false;
        Assert.Contains("active", await management.ChangeAssignmentAsync(1, 1, true, default));
        Assert.Single(db.TutorCourseModules);
    }

    [Fact]
    public async Task ModuleDetailsOffersEligibleTutorsFromEveryProgramme()
    {
        await using var db = await Seed();
        db.ProgrammesOfStudy.Add(new() { Id = 2, Name = "Another programme" });
        var tutor = CreateTutor(2, "T2");
        tutor.ProgrammeId = 2;
        db.Tutors.Add(tutor);
        await db.SaveChangesAsync();

        var page = Setup(new DetailsModel(db));
        Assert.IsType<PageResult>(await page.OnGetAsync(1, default));

        Assert.Contains(page.AvailableTutors, available => available.TutorId == 2);
    }

    [Fact]
    public async Task ModuleDetailsFiltersAssignedTutorsBySearchAndProgramme()
    {
        await using var db = await Seed();
        db.ProgrammesOfStudy.Add(new() { Id = 2, Name = "Another programme" });
        var tutor = CreateTutor(2, "T2");
        tutor.ProgrammeId = 2;
        db.Tutors.Add(tutor);
        db.TutorCourseModules.AddRange(
            new() { TutorId = 1, ProgrammeModuleId = 1 },
            new() { TutorId = 2, ProgrammeModuleId = 1 });
        await db.SaveChangesAsync();

        var page = Setup(new DetailsModel(db) { TutorSearch = "Tutor 2", TutorProgrammeId = 2 });
        Assert.IsType<PageResult>(await page.OnGetAsync(1, default));

        Assert.Equal(2, page.AssignedTutorCount);
        Assert.Equal(2, Assert.Single(page.AssignedTutors).TutorId);
        Assert.Equal(2, page.AssignedTutorProgrammes.Count);
    }

    [Fact]
    public async Task ModuleDetailsPaginatesAssignedTutors()
    {
        await using var db = await Seed();
        var tutors = Enumerable.Range(2, 12).Select(id => CreateTutor(id, $"T{id}")).ToArray();
        db.Tutors.AddRange(tutors);
        db.TutorCourseModules.Add(new() { TutorId = 1, ProgrammeModuleId = 1 });
        db.TutorCourseModules.AddRange(tutors.Select(tutor => new TutorCourseModule
            { TutorId = tutor.TutorId, ProgrammeModuleId = 1 }));
        await db.SaveChangesAsync();

        var page = Setup(new DetailsModel(db) { TutorPage = 2 });
        Assert.IsType<PageResult>(await page.OnGetAsync(1, default));

        Assert.Equal(13, page.AssignedTutorCount);
        Assert.Equal(13, page.FilteredAssignedTutorCount);
        Assert.Equal(2, page.TutorTotalPages);
        Assert.Single(page.AssignedTutors);
    }

    [Fact]
    public async Task ModuleDetailsCountsOutstandingSessionsBeforeRemoval()
    {
        await using var db = await Seed();
        db.TutorCourseModules.Add(new() { TutorId = 1, ProgrammeModuleId = 1 });
        db.Bookings.AddRange(
            new() { TutorId = 1, ProgrammeModuleId = 1, Status = BookingStatus.Pending },
            new() { TutorId = 1, ProgrammeModuleId = 1, Status = BookingStatus.Confirmed },
            new() { TutorId = 1, ProgrammeModuleId = 1, Status = BookingStatus.Completed });
        await db.SaveChangesAsync();

        var page = Setup(new DetailsModel(db));
        Assert.IsType<PageResult>(await page.OnGetAsync(1, default));

        Assert.Equal(2, page.OutstandingSessionCounts[1]);
    }

    [Fact]
    public async Task DirectAssignmentResolvesPendingRequestAndUpdatesCounts()
    {
        await using var db = await Seed();
        var request = new TutorModuleChangeRequest { TutorId = 1, ProgrammeModuleId = 1 };
        db.TutorModuleChangeRequests.Add(request); await db.SaveChangesAsync();
        var page = Setup(new DetailsModel(db));
        Assert.IsType<RedirectToPageResult>(await page.OnPostAssignmentAsync(1, 1, true, default));
        Assert.Equal(TutorAccountRequestStatus.Approved, request.Status);
        var catalogue = Setup(new IndexModel(db));
        await catalogue.OnGetAsync(default);
        Assert.Equal(1, Assert.Single(catalogue.Modules).TutorCount);
        Assert.Equal(0, catalogue.PendingRequests);
        Assert.Equal(0, catalogue.UncoveredModules);
        Assert.IsType<RedirectToPageResult>(await page.OnPostAssignmentAsync(1, 1, false, default));
        await catalogue.OnGetAsync(default);
        Assert.Equal(0, Assert.Single(catalogue.Modules).TutorCount);
        Assert.Equal(1, catalogue.UncoveredModules);
    }

    [Fact]
    public async Task CatalogueFiltersByProgrammeYearAndCoverage()
    {
        await using var db = await Seed();
        db.ProgrammesOfStudy.Add(new() { Id = 2, Name = "Another programme" });
        db.ProgrammeModules.Add(new() { ProgrammeModuleId = 2, ProgrammeId = 2, ModuleCode = "TWO", ModuleName = "Other", YearOfStudy = 2 });
        db.TutorCourseModules.Add(new() { TutorId = 1, ProgrammeModuleId = 1 });
        await db.SaveChangesAsync();
        var page = Setup(new IndexModel(db) { ProgrammeId = 2, Year = 2, Search = "TWO", Unassigned = true });
        await page.OnGetAsync(default);
        Assert.Equal(2, Assert.Single(page.Modules).Id);
        page.ProgrammeId = 1;
        await page.OnGetAsync(default);
        Assert.Empty(page.Modules);
    }

    [Fact]
    public async Task CatalogueOrdersModulesByActiveApprovedTutorCount()
    {
        await using var db = await Seed();
        db.ProgrammeModules.AddRange(
            new() { ProgrammeModuleId = 2, ProgrammeId = 1, ModuleCode = "ONE201", ModuleName = "One tutor", YearOfStudy = 2 },
            new() { ProgrammeModuleId = 3, ProgrammeId = 1, ModuleCode = "TWO301", ModuleName = "Two tutors", YearOfStudy = 3 });
        db.Tutors.AddRange(
            CreateTutor(2, "T2"),
            CreateTutor(3, "T3"));
        db.TutorCourseModules.AddRange(
            new() { TutorId = 1, ProgrammeModuleId = 2 },
            new() { TutorId = 1, ProgrammeModuleId = 3 },
            new() { TutorId = 2, ProgrammeModuleId = 3 },
            new() { TutorId = 3, ProgrammeModuleId = 1, IsActive = false });
        await db.SaveChangesAsync();

        var page = Setup(new IndexModel(db));
        await page.OnGetAsync(default);

        Assert.Equal([3, 2, 1], page.Modules.Select(module => module.Id));
        Assert.Equal([2, 1, 0], page.Modules.Select(module => module.TutorCount));
    }

    [Theory]
    [InlineData("module", "asc", new[] { "AAA101", "PRG101", "ZZZ301" })]
    [InlineData("module", "desc", new[] { "ZZZ301", "PRG101", "AAA101" })]
    [InlineData("year", "asc", new[] { "PRG101", "AAA101", "ZZZ301" })]
    [InlineData("year", "desc", new[] { "ZZZ301", "AAA101", "PRG101" })]
    [InlineData("tutors", "asc", new[] { "AAA101", "PRG101", "ZZZ301" })]
    public async Task CatalogueSupportsColumnSorting(string sort, string direction, string[] expectedCodes)
    {
        await using var db = await Seed();
        db.ProgrammeModules.AddRange(
            new() { ProgrammeModuleId = 2, ProgrammeId = 1, ModuleCode = "AAA101", ModuleName = "Alpha", YearOfStudy = 2 },
            new() { ProgrammeModuleId = 3, ProgrammeId = 1, ModuleCode = "ZZZ301", ModuleName = "Zulu", YearOfStudy = 3 });
        db.TutorCourseModules.AddRange(
            new() { TutorId = 1, ProgrammeModuleId = 1 },
            new() { TutorId = 1, ProgrammeModuleId = 3 });
        await db.SaveChangesAsync();

        var page = Setup(new IndexModel(db) { Sort = sort, SortDirection = direction });
        await page.OnGetAsync(default);

        Assert.Equal(expectedCodes, page.Modules.Select(module => module.Code));
    }

    [Fact]
    public async Task CatalogueNormalizesMissingSortValuesToTutorCountDescending()
    {
        await using var db = await Seed();
        var page = Setup(new IndexModel(db) { Sort = null, SortDirection = null });

        await page.OnGetAsync(default);

        Assert.Equal("tutors", page.Sort);
        Assert.Equal("desc", page.SortDirection);
    }

    [Fact]
    public async Task ModuleViewsOnlyShowActivePlacementTutors()
    {
        await using var db = await Seed();
        var interviewTutor = CreateTutor(2, "T2");
        interviewTutor.ApplicationStage = TutorApplicationStage.Interview;
        var inactiveTutor = CreateTutor(3, "T3");
        inactiveTutor.IsActive = false;
        db.Tutors.AddRange(interviewTutor, inactiveTutor);
        db.TutorCourseModules.AddRange(
            new() { TutorId = 2, ProgrammeModuleId = 1 },
            new() { TutorId = 3, ProgrammeModuleId = 1 });
        db.TutorModuleChangeRequests.AddRange(
            new() { TutorId = 2, ProgrammeModuleId = 1 },
            new() { TutorId = 3, ProgrammeModuleId = 1 });
        await db.SaveChangesAsync();

        var catalogue = Setup(new IndexModel(db));
        await catalogue.OnGetAsync(default);
        Assert.Equal(0, Assert.Single(catalogue.Modules).TutorCount);
        Assert.Equal(1, catalogue.UncoveredModules);
        Assert.Equal(0, catalogue.PendingRequests);
        Assert.Empty(catalogue.Requests);

        var details = Setup(new DetailsModel(db));
        Assert.IsType<PageResult>(await details.OnGetAsync(1, default));
        Assert.Empty(details.Module.TutorCourseModules);
        Assert.Equal(1, Assert.Single(details.AvailableTutors).TutorId);
    }

    [Fact]
    public async Task InactiveAssignmentsAreHiddenFromTutorDirectoryAndProfile()
    {
        await using var db = await Seed();
        db.TutorCourseModules.Add(new() { TutorId = 1, ProgrammeModuleId = 1, IsActive = false });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new TutorService(db);
        Assert.Empty(await service.GetTutorsAsync(1));
        Assert.Empty((await service.GetTutorDetailsAsync(1))!.Modules);
    }

    private static T Setup<T>(T page) where T : PageModel
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Test Admin")], "Test")) };
        page.PageContext = new PageContext { HttpContext = http };
        page.TempData = new TempDataDictionary(http, new MemoryTempData());
        return page;
    }
    private sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private static async Task<ApplicationDbContext> Seed()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.ProgrammesOfStudy.Add(new() { Id = 1, Name = "Computing" });
        db.ProgrammeModules.Add(new() { ProgrammeModuleId = 1, ProgrammeId = 1, ModuleCode = "PRG101", ModuleName = "Programming", YearOfStudy = 1 });
        db.Tutors.Add(new() { TutorId = 1, ProgrammeId = 1, BcUser = new() { BcUserId = 1, PersonnelNumber = "T1", DisplayName = "Test Tutor" }, Status = TutorStatus.Approved, IsActive = true, ApplicationStage = TutorApplicationStage.Placement, ReasonForTutoring = "Reason", TeachingStyle = "Style", PreviousTutoringExperience = "None", CampusOfStudy = "Campus", DemonstrationVideoUrl = "" });
        await db.SaveChangesAsync();
        return db;
    }

    private static Tutor CreateTutor(int id, string personnelNumber) => new()
    {
        TutorId = id,
        ProgrammeId = 1,
        BcUser = new() { BcUserId = id, PersonnelNumber = personnelNumber, DisplayName = $"Tutor {id}" },
        Status = TutorStatus.Approved,
        IsActive = true,
        ApplicationStage = TutorApplicationStage.Placement,
        ReasonForTutoring = "Reason",
        TeachingStyle = "Style",
        PreviousTutoringExperience = "None",
        CampusOfStudy = "Campus",
        DemonstrationVideoUrl = ""
    };
}

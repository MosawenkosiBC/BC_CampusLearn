using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BC_CampusLearn.Services.Sessions;

namespace BC_CampusLearn.Pages.Student;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISessionLifecycleService _lifecycleService;

    public DashboardModel(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISessionLifecycleService lifecycleService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _lifecycleService = lifecycleService;
    }

    public CurrentUser CurrentUser { get; private set; }
        = null!;

    public DashboardSummaryViewModel Summary { get; private set; }
        = new();

    public BookingListItemViewModel? NextSession
    { get; private set; }

    public IReadOnlyList<BookingListItemViewModel>
        Sessions
    { get; private set; }
        = new List<BookingListItemViewModel>();

    public IReadOnlyList<ResourceModuleSubscriptionItem> AvailableResourceModules
    { get; private set; } = Array.Empty<ResourceModuleSubscriptionItem>();

    public IReadOnlyList<ResourceModuleSubscriptionItem> SubscribableResourceModules
    { get; private set; } = Array.Empty<ResourceModuleSubscriptionItem>();

    public IReadOnlyList<ResourceModuleSubscriptionItem> SubscribedResourceModules
    { get; private set; } = Array.Empty<ResourceModuleSubscriptionItem>();

    public IReadOnlyList<StudentLearningResourceListItem> RecentSubscribedResources
    { get; private set; } = Array.Empty<StudentLearningResourceListItem>();

    [TempData]
    public string? ResourceSubscriptionMessage { get; set; }

    [TempData]
    public bool ResourceSubscriptionError { get; set; }

    [TempData]
    public string? SessionActionMessage { get; set; }

    [TempData]
    public bool SessionActionError { get; set; }

    public async Task OnGetAsync(
        CancellationToken cancellationToken)
    {
        CurrentUser =
            _currentUserService.GetRequiredUser();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        await _lifecycleService.ProcessDueTransitionsAsync(cancellationToken);

        IQueryable<Booking> studentBookings =
            _context.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.StudentObjectId ==
                        CurrentUser.ObjectId &&
                    booking.StudentTenantId ==
                        CurrentUser.TenantId);

        Summary =
            await studentBookings
                .GroupBy(_ => 1)
                .Select(bookings =>
                    new DashboardSummaryViewModel
                    {
                        UpcomingSessionCount =
                            bookings.Count(booking =>
                                booking.Status ==
                                    BookingStatus.Confirmed &&
                                booking.ScheduledStartTime > now),

                        PendingSessionCount =
                            bookings.Count(booking =>
                                booking.Status ==
                                    BookingStatus.Pending),

                        CompletedSessionCount =
                            bookings.Count(booking =>
                                booking.Status ==
                                    BookingStatus.Completed),

                        CancelledSessionCount =
                            bookings.Count(booking =>
                                booking.Status ==
                                    BookingStatus.Cancelled ||
                                booking.Status ==
                                    BookingStatus.Declined)
                    })
                .FirstOrDefaultAsync(cancellationToken)
            ?? new DashboardSummaryViewModel();

        NextSession =
            await studentBookings
                .Where(booking =>
                    booking.ScheduledStartTime > now &&
                    booking.Status == BookingStatus.Confirmed)
                .OrderBy(booking =>
                    booking.ScheduledStartTime)
                .Select(booking =>
                    new BookingListItemViewModel
                    {
                        BookingId =
                            booking.BookingId,

                        TutorId = booking.TutorId,

                        TutorName = string.IsNullOrWhiteSpace(
                            booking.TutorCourseModule.Tutor.BcUser.DisplayName)
                            ? booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber
                            : booking.TutorCourseModule.Tutor.BcUser.DisplayName,

                        ModuleName =
                            booking.ProgrammeModule.ModuleName,

                        ModuleCode =
                            booking.ProgrammeModule.ModuleCode,

                        Location = booking.Location,

                        AvailableTime =
                            booking.ScheduledStartTime,

                        Duration = booking.Duration,

                        Status = booking.Status,

                        Summary = booking.Summary,
                        MeetingLink = booking.MeetingLink
                    })
                .FirstOrDefaultAsync(cancellationToken);

        Sessions =
            await studentBookings
                .OrderByDescending(booking =>
                    booking.ScheduledStartTime)
                .Select(booking =>
                    new BookingListItemViewModel
                    {
                        BookingId = booking.BookingId,

                        TutorId = booking.TutorId,

                        TutorName = string.IsNullOrWhiteSpace(
                            booking.TutorCourseModule.Tutor.BcUser.DisplayName)
                            ? booking.TutorCourseModule.Tutor.BcUser.PersonnelNumber
                            : booking.TutorCourseModule.Tutor.BcUser.DisplayName,

                        ModuleName =
                            booking.ProgrammeModule.ModuleName,

                        ModuleCode =
                            booking.ProgrammeModule.ModuleCode,

                        Location = booking.Location,

                        AvailableTime =
                            booking.ScheduledStartTime,

                        Duration = booking.Duration,

                        Status = booking.Status,

                        Summary = booking.Summary
                    })
                .Take(5)
                .ToListAsync(cancellationToken);

        await LoadResourceSubscriptionsAsync(
            CurrentUser.PersonnelNumber,
            cancellationToken);

    }

    public async Task<IActionResult> OnPostSubscribeResourceModuleAsync(
        string moduleCode,
        CancellationToken cancellationToken)
    {
        CurrentUser = _currentUserService.GetRequiredUser();
        string requestedCode = moduleCode?.Trim() ?? string.Empty;

        List<ResourceModuleSubscriptionItem> matchingResources =
            await _context.LearningResources
                .AsNoTracking()
                .Where(resource =>
                    resource.ProgrammeModule.ModuleCode == requestedCode &&
                    resource.Status == LearningResourceStatus.Published)
                .Select(resource => new ResourceModuleSubscriptionItem
                {
                    ModuleCode = resource.ProgrammeModule.ModuleCode,
                    ModuleName = resource.ProgrammeModule.ModuleName
                })
                .ToListAsync(cancellationToken);
        ResourceModuleSubscriptionItem? availableModule = matchingResources
            .GroupBy(module => module.ModuleCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ResourceModuleSubscriptionItem
            {
                ModuleCode = group.Key,
                ModuleName = group.First().ModuleName,
                PublishedResourceCount = group.Count()
            })
            .FirstOrDefault();

        if (availableModule is null)
        {
            ResourceSubscriptionError = true;
            ResourceSubscriptionMessage =
                "That module does not currently have published learning resources.";
            return RedirectToPage(null, null, null, "subscribed-modules");
        }

        ResourceSubscription? subscription =
            await _context.ResourceSubscriptions
                .SingleOrDefaultAsync(item =>
                    item.PersonnelNumber == CurrentUser.PersonnelNumber &&
                    item.ModuleCode == availableModule.ModuleCode,
                    cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (subscription is null)
        {
            subscription = new ResourceSubscription
            {
                PersonnelNumber = CurrentUser.PersonnelNumber,
                ModuleCode = availableModule.ModuleCode,
                DateSubscribed = now,
                IsActive = true
            };
            _context.ResourceSubscriptions.Add(subscription);
        }
        else
        {
            subscription.IsActive = true;
            subscription.DateSubscribed = now;
            subscription.LastAccessedAt = null;
        }

        await _context.SaveChangesAsync(cancellationToken);
        ResourceSubscriptionMessage =
            $"You subscribed to {availableModule.ModuleCode}.";
        return RedirectToPage(null, null, null, "subscribed-modules");
    }

    public async Task<IActionResult> OnPostCancelSessionAsync(
        int bookingId,
        string? cancellationReason,
        CancellationToken cancellationToken)
    {
        CurrentUser student = _currentUserService.GetRequiredUser();
        SessionLifecycleResult result =
            await _lifecycleService.CancelByStudentAsync(
                student.BcUserId,
                student.ObjectId,
                student.TenantId,
                bookingId,
                cancellationReason,
                cancellationToken);

        SessionActionError = !result.Succeeded;
        SessionActionMessage = result.Succeeded
            ? "Session cancelled."
            : result.ErrorMessage;
        return RedirectToPage(null, null, null, "student-all-sessions-title");
    }

    public async Task<IActionResult> OnPostUnsubscribeResourceModuleAsync(
        string moduleCode,
        CancellationToken cancellationToken)
    {
        CurrentUser = _currentUserService.GetRequiredUser();
        string requestedCode = moduleCode?.Trim() ?? string.Empty;

        ResourceSubscription? subscription =
            await _context.ResourceSubscriptions
                .SingleOrDefaultAsync(item =>
                    item.PersonnelNumber == CurrentUser.PersonnelNumber &&
                    item.ModuleCode == requestedCode &&
                    item.IsActive,
                    cancellationToken);

        if (subscription is null)
        {
            ResourceSubscriptionError = true;
            ResourceSubscriptionMessage =
                "That module subscription is no longer active.";
            return RedirectToPage(null, null, null, "subscribed-modules");
        }

        subscription.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        ResourceSubscriptionMessage =
            $"You unsubscribed from {subscription.ModuleCode}.";
        return RedirectToPage(null, null, null, "subscribed-modules");
    }

    private async Task LoadResourceSubscriptionsAsync(
        string personnelNumber,
        CancellationToken cancellationToken)
    {
        List<ResourceModuleSubscriptionItem> publishedResourceModules =
            await _context.LearningResources
                .AsNoTracking()
                .Where(resource =>
                    resource.Status == LearningResourceStatus.Published)
                .Select(resource => new ResourceModuleSubscriptionItem
                {
                    ModuleCode = resource.ProgrammeModule.ModuleCode,
                    ModuleName = resource.ProgrammeModule.ModuleName
                })
                .ToListAsync(cancellationToken);
        List<ResourceModuleSubscriptionItem> availableModules =
            publishedResourceModules
                .GroupBy(module => module.ModuleCode, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ResourceModuleSubscriptionItem
                {
                    ModuleCode = group.Key,
                    ModuleName = group.First().ModuleName,
                    PublishedResourceCount = group.Count()
                })
                .OrderBy(module => module.ModuleCode)
                .ToList();

        List<string> activeModuleCodes =
            await _context.ResourceSubscriptions
                .AsNoTracking()
                .Where(subscription =>
                    subscription.PersonnelNumber == personnelNumber &&
                    subscription.IsActive)
                .Select(subscription => subscription.ModuleCode)
                .ToListAsync(cancellationToken);

        HashSet<string> activeCodeSet =
            activeModuleCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (ResourceModuleSubscriptionItem module in availableModules)
        {
            module.IsSubscribed = activeCodeSet.Contains(module.ModuleCode);
        }

        AvailableResourceModules = availableModules;
        SubscribableResourceModules = availableModules
            .Where(module => !module.IsSubscribed)
            .ToList();
        List<ResourceModuleSubscriptionItem> subscribedModules = availableModules
            .Where(module => module.IsSubscribed)
            .ToList();

        List<string> unavailableActiveCodes = activeModuleCodes
            .Where(code => !availableModules.Any(module =>
                module.ModuleCode.Equals(code, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (unavailableActiveCodes.Count > 0)
        {
            List<ResourceModuleSubscriptionItem> unavailableModules =
                await _context.ProgrammeModules
                    .AsNoTracking()
                    .Where(module => unavailableActiveCodes.Contains(module.ModuleCode))
                    .Select(module => new ResourceModuleSubscriptionItem
                    {
                        ModuleCode = module.ModuleCode,
                        ModuleName = module.ModuleName,
                        PublishedResourceCount = 0,
                        IsSubscribed = true
                    })
                    .ToListAsync(cancellationToken);

            subscribedModules.AddRange(unavailableModules);
            HashSet<string> foundCodes = unavailableModules
                .Select(module => module.ModuleCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            subscribedModules.AddRange(unavailableActiveCodes
                .Where(code => !foundCodes.Contains(code))
                .Select(code => new ResourceModuleSubscriptionItem
                {
                    ModuleCode = code,
                    ModuleName = "Module no longer available",
                    PublishedResourceCount = 0,
                    IsSubscribed = true
                }));
        }

        SubscribedResourceModules = subscribedModules
            .OrderBy(module => module.ModuleCode)
            .ToList();

        RecentSubscribedResources = await _context.LearningResources
            .AsNoTracking()
            .Where(resource =>
                resource.Status == LearningResourceStatus.Published &&
                activeModuleCodes.Contains(resource.ProgrammeModule.ModuleCode))
            .OrderByDescending(resource => resource.DatePublished ?? resource.DateCreated)
            .Take(4)
            .Select(resource => new StudentLearningResourceListItem
            {
                LearningResourceId = resource.LearningResourceId,
                Topic = resource.Topic,
                Content = resource.Content,
                ModuleCode = resource.ProgrammeModule.ModuleCode,
                ModuleName = resource.ProgrammeModule.ModuleName,
                TutorId = resource.TutorId,
                TutorName = string.IsNullOrWhiteSpace(resource.Tutor.BcUser.DisplayName)
                    ? resource.Tutor.BcUser.PersonnelNumber
                    : resource.Tutor.BcUser.DisplayName,
                TutorProfileImagePath = resource.Tutor.ProfileImagePath,
                DatePublished = resource.DatePublished
            })
            .ToListAsync(cancellationToken);
    }
}

public class ResourceModuleSubscriptionItem
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public int PublishedResourceCount { get; set; }
    public bool IsSubscribed { get; set; }
}

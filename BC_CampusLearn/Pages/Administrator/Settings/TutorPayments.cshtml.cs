using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BC_CampusLearn.Pages.Administrator.Settings;

[Authorize(Roles = nameof(BcUserRole.SuperAdmin))]
public class TutorPaymentsModel(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    SettingsAuditService auditService) : PageModel
{
    [BindProperty]
    public PaymentSettingsInput Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public string CurrencyCode { get; private set; } = "ZAR";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (currentUserService.GetRequiredUser().Role != BcUserRole.SuperAdmin)
            return Forbid();

        PlatformSettings settings = await GetSettingsAsync(cancellationToken);
        CurrencyCode = settings.CurrencyCode;
        Input = new()
        {
            TutorPaymentAmount = settings.TutorPaymentAmount,
            TutorHeadPaymentAmount = settings.TutorHeadPaymentAmount
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        CurrentUser currentUser = currentUserService.GetRequiredUser();
        if (currentUser.Role != BcUserRole.SuperAdmin) return Forbid();

        ValidateAmount(Input.TutorPaymentAmount, nameof(Input.TutorPaymentAmount));
        ValidateAmount(Input.TutorHeadPaymentAmount, nameof(Input.TutorHeadPaymentAmount));
        PlatformSettings settings = await GetSettingsAsync(cancellationToken);
        CurrencyCode = settings.CurrencyCode;
        if (!ModelState.IsValid) return Page();

        bool changed = auditService.Record("Tutor payments", "Tutor payment amount",
            Amount(settings.TutorPaymentAmount), Amount(Input.TutorPaymentAmount), currentUser);
        changed |= auditService.Record("Tutor payments", "Tutor head payment amount",
            Amount(settings.TutorHeadPaymentAmount), Amount(Input.TutorHeadPaymentAmount), currentUser);
        if (changed)
        {
            settings.TutorPaymentAmount = Input.TutorPaymentAmount;
            settings.TutorHeadPaymentAmount = Input.TutorHeadPaymentAmount;
            settings.UpdatedByBcUserId = currentUser.BcUserId;
            settings.UpdatedAt = timeProvider.GetUtcNow();
            // Assign the first configured amount to approvals that have no saved rate.
            var unpricedApprovals = await context.SuperAdminSessionReviews
                .Where(review => review.IsAccepted && review.CompensationAmount == null &&
                    review.Booking.Status == BookingStatus.Completed)
                .ToListAsync(cancellationToken);
            foreach (var review in unpricedApprovals)
                review.CompensationAmount = Input.TutorPaymentAmount;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty,
                    "Settings changed while you were saving. Reload the page and try again.");
                return Page();
            }
        }

        SuccessMessage = changed ? "Tutor payment amounts saved."
            : "Tutor payment amounts are already up to date.";
        return RedirectToPage();
    }

    private void ValidateAmount(decimal? amount, string propertyName)
    {
        if (amount is null || amount < 0 || amount > 999999999.99m)
            ModelState.AddModelError($"Input.{propertyName}",
                "Enter an amount from 0 to 999,999,999.99.");
        else if (decimal.Round(amount.Value, 2) != amount)
            ModelState.AddModelError($"Input.{propertyName}",
                "Use no more than two decimal places.");
    }

    private Task<PlatformSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        context.PlatformSettings.SingleAsync(settings =>
            settings.PlatformSettingsId == PlatformSettings.SingletonId, cancellationToken);

    private static string? Amount(decimal? amount) =>
        amount?.ToString("0.00", CultureInfo.InvariantCulture);

    public sealed class PaymentSettingsInput
    {
        [Required, Range(typeof(decimal), "0", "999999999.99",
            ParseLimitsInInvariantCulture = true)]
        [Display(Name = "Tutor payment amount")]
        public decimal? TutorPaymentAmount { get; set; }

        [Required, Range(typeof(decimal), "0", "999999999.99",
            ParseLimitsInInvariantCulture = true)]
        [Display(Name = "Tutor head payment amount")]
        public decimal? TutorHeadPaymentAmount { get; set; }
    }
}

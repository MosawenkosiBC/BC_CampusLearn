using BC_CampusLearn.Authentication;
using BC_CampusLearn.Authentication.Development;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace BC_CampusLearn.Pages.Account;

[AllowAnonymous]
public class SignInModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly DevelopmentUserOptions _developmentUser;
    private readonly DevelopmentStudentOptions _developmentStudent;
    private readonly DevelopmentAdminOptions _developmentAdmin;

    public SignInModel(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IOptions<DevelopmentUserOptions> developmentUserOptions,
        IOptions<DevelopmentStudentOptions> developmentStudentOptions,
        IOptions<DevelopmentAdminOptions> developmentAdminOptions)
    {
        _environment = environment;
        _configuration = configuration;
        _developmentUser = developmentUserOptions.Value;
        _developmentStudent = developmentStudentOptions.Value;
        _developmentAdmin = developmentAdminOptions.Value;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string? DevelopmentAccount { get; set; }

    public string DevelopmentUserDisplayName =>
        _developmentUser.DisplayName;

    public string DevelopmentStudentDisplayName =>
        _developmentStudent.DisplayName;

    public string DevelopmentAdminDisplayName =>
        _developmentAdmin.DisplayName;

    public bool IsDevelopmentAuthentication =>
        _environment.IsDevelopment() &&
        _configuration.GetValue<bool>(
            "Authentication:UseDevelopmentAuthentication");

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Account/PostLogin");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        string redirectUrl = ResolveReturnUrl(ReturnUrl);

        if (!IsDevelopmentAuthentication)
        {
            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = redirectUrl
                },
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        bool isDevelopmentAdmin = string.Equals(
            DevelopmentAccount,
            "admin",
            StringComparison.OrdinalIgnoreCase);

        DevelopmentUserOptions selectedUser = DevelopmentAccount?.ToLowerInvariant() switch
        {
            "student" => _developmentStudent,
            "admin" => _developmentAdmin,
            _ => _developmentUser
        };

        if (string.IsNullOrWhiteSpace(selectedUser.ObjectId) ||
            string.IsNullOrWhiteSpace(selectedUser.TenantId) ||
            string.IsNullOrWhiteSpace(selectedUser.PersonnelNumber))
        {
            ModelState.AddModelError(
                string.Empty,
                "The development user has not been configured.");

            return Page();
        }

        var claims = new List<Claim>
        {
            new Claim(
                EntraClaimTypes.ObjectId,
                selectedUser.ObjectId),

            new Claim(
                EntraClaimTypes.TenantId,
                selectedUser.TenantId),

            new Claim(
                EntraClaimTypes.DisplayName,
                selectedUser.DisplayName),

            new Claim(
                EntraClaimTypes.PreferredUsername,
                selectedUser.Email),

            new Claim(
                ClaimTypes.Name,
                selectedUser.DisplayName),

            new Claim(
                ClaimTypes.Email,
                selectedUser.Email),
            new Claim(
                EntraClaimTypes.PersonnelNumber,
                selectedUser.PersonnelNumber)
        };

        if (isDevelopmentAdmin)
        {
            claims.Add(new Claim(
                EntraClaimTypes.DevelopmentRole,
                nameof(BcUserRole.Admin)));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false
            });

        return LocalRedirect(redirectUrl);
    }

    private string ResolveReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        return Url.Page("/Account/PostLogin")
            ?? "/Account/PostLogin";
    }
}

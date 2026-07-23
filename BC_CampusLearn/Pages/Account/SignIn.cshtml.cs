using BC_CampusLearn.Authentication;
using BC_CampusLearn.Authentication.Development;
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

    public SignInModel(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IOptions<DevelopmentUserOptions> developmentUserOptions)
    {
        _environment = environment;
        _configuration = configuration;
        _developmentUser = developmentUserOptions.Value;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool IsDevelopmentAuthentication =>
        _environment.IsDevelopment() &&
        _configuration.GetValue<bool>(
            "Authentication:UseDevelopmentAuthentication");

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Student/Dashboard");
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

        if (string.IsNullOrWhiteSpace(_developmentUser.ObjectId) ||
            string.IsNullOrWhiteSpace(_developmentUser.TenantId))
        {
            ModelState.AddModelError(
                string.Empty,
                "The development user has not been configured.");

            return Page();
        }

        Claim[] claims =
        {
            new Claim(
                EntraClaimTypes.ObjectId,
                _developmentUser.ObjectId),

            new Claim(
                EntraClaimTypes.TenantId,
                _developmentUser.TenantId),

            new Claim(
                EntraClaimTypes.DisplayName,
                _developmentUser.DisplayName),

            new Claim(
                EntraClaimTypes.PreferredUsername,
                _developmentUser.Email),

            new Claim(
                ClaimTypes.Name,
                _developmentUser.DisplayName),

            new Claim(
                ClaimTypes.Email,
                _developmentUser.Email),
            new Claim(
                EntraClaimTypes.PersonnelNumber,
                _developmentUser.PersonnelNumber)
        };

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

        return Url.Page("/Student/Dashboard")
            ?? "/Student/Dashboard";
    }
}

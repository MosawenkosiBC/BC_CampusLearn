using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BC_CampusLearn.Pages.Account;

[Authorize]
public class SignOutModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public SignOutModel(
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        bool useDevelopmentAuthentication =
            _environment.IsDevelopment() &&
            _configuration.GetValue<bool>(
                "Authentication:UseDevelopmentAuthentication");

        if (useDevelopmentAuthentication)
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToPage("/Index");
        }

        string returnUrl =
            Url.Page(
                "/Index",
                pageHandler: null,
                values: null,
                protocol: Request.Scheme)
            ?? "/";

        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = returnUrl
            },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
using System.Reflection;
using System.Security.Claims;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Admin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BC_CampusLearn.Tests;

public class ApplicationAccessTests
{
    [Theory]
    [InlineData(BcUserRole.Admin, true)]
    [InlineData(BcUserRole.SuperAdmin, true)]
    [InlineData(BcUserRole.Dev, false)]
    public async Task DashboardRendersApplicationsInAdministratorNavigation(BcUserRole role, bool visible)
    {
        const string applicationsPath = "/Administrator/Admin/Applications";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ApplicationsModel).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorPages();
        builder.Services.AddAuthentication("Test").AddCookie("Test");
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        string database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddSingleton<ICurrentUserService>(new TestCurrentUserService(role));

        await using var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.UseRouting();
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role.ToString())], "Test"));
            await next();
        });
        app.UseAuthorization();
        app.MapRazorPages();
        await app.StartAsync();

        string address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(address)
        };
        string dashboard = await http.GetStringAsync("/Administrator/Admin/Dashboard");
        int sidebarStart = dashboard.IndexOf("<aside class=\"admin-sidebar\"", StringComparison.Ordinal);
        Assert.True(sidebarStart >= 0);
        int sidebarEnd = dashboard.IndexOf("</aside>", sidebarStart, StringComparison.Ordinal);
        Assert.True(sidebarEnd > sidebarStart);
        string sidebar = dashboard[sidebarStart..sidebarEnd];

        Assert.Equal(visible, sidebar.Contains($"href=\"{applicationsPath}\"", StringComparison.Ordinal));
        Assert.Equal(visible, dashboard.Contains("<span class=\"admin-notification-label\">Tutor applications", StringComparison.Ordinal));
        if (visible)
        {
            using var response = await http.GetAsync(applicationsPath);
            string applications = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {applications}");
            Assert.Contains("<h1 id=\"applications-title\">Applications</h1>", applications);
        }
    }

    [Theory]
    [InlineData(nameof(BcUserRole.Admin), true)]
    [InlineData(nameof(BcUserRole.SuperAdmin), true)]
    [InlineData(nameof(BcUserRole.Dev), false)]
    [InlineData(nameof(BcUserRole.Student), false)]
    [InlineData(nameof(BcUserRole.Tutor), false)]
    [InlineData(nameof(BcUserRole.SeniorTutor), false)]
    [InlineData(nameof(BcUserRole.HeadOfTutors), false)]
    [InlineData(null, false)]
    public async Task ApplicationPagesAuthorizeOnlyAdminsAndSuperAdmins(string? role, bool allowed)
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .AddAuthorization()
            .BuildServiceProvider();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var provider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var user = new ClaimsPrincipal(role is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));

        foreach (Type page in new[] { typeof(ApplicationsModel), typeof(ApplicationDetailsModel) })
        {
            var policy = await AuthorizationPolicy.CombineAsync(
                provider, page.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
            Assert.NotNull(policy);
            var result = await authorization.AuthorizeAsync(user, null, policy);
            Assert.Equal(allowed, result.Succeeded);
        }
    }

    [Theory]
    [InlineData(BcUserRole.Admin, true)]
    [InlineData(BcUserRole.SuperAdmin, true)]
    [InlineData(BcUserRole.Dev, false)]
    [InlineData(BcUserRole.Student, false)]
    public async Task ApplicationPlacementStudentLookupUsesManualTutorPermission(BcUserRole role, bool allowed)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ApplicationDbContext(options);
        var page = new ApplicationsModel(context, new TestCurrentUserService(role));

        var result = await page.OnGetStudentDetailsAsync(999, CancellationToken.None);

        Assert.Equal(allowed, page.CanAddTutor);
        if (allowed)
        {
            // Reaching validation proves the handler passed the role check.
            Assert.Equal(400, Assert.IsType<JsonResult>(result).StatusCode);
        }
        else
        {
            Assert.IsType<ForbidResult>(result);
        }
    }

    private sealed class TestCurrentUserService(BcUserRole role) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => new(1, null, "Test Admin", null, role);
    }
}

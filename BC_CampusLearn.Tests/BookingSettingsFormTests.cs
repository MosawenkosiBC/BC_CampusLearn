using System.Net;
using System.Text.Json;
using BC_CampusLearn.Authentication;
using BC_CampusLearn.Data;
using BC_CampusLearn.Models.Entities;
using BC_CampusLearn.Pages.Administrator.Settings;
using BC_CampusLearn.Services.Settings;
using BC_CampusLearn.Services.Teams;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BC_CampusLearn.Tests;

public class BookingSettingsFormTests
{
    private const string ObjectId = "33333333-3333-3333-3333-333333333333";
    private const string Terms = "Existing booking terms must remain unchanged during Teams account updates.";

    [Theory]
    [InlineData("SaveTeamsOrganizer", true)]
    [InlineData("TestTeamsOrganizer", true)]
    [InlineData("", true)]
    [InlineData("SaveTeamsOrganizer", false)]
    [InlineData("TestTeamsOrganizer", false)]
    [InlineData("", false)]
    public async Task EachFormBindsAndValidatesOnlyItsOwnFields(string handler, bool valid)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BookingsModel).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorPages(options =>
        {
            options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
            options.Conventions.ConfigureFilter(new CaptureValidationErrors());
        });
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        // Use the same database across the seed and HTTP request scopes.
        string database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddSingleton<ICurrentUserService, SuperAdmin>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<SettingsAuditService>();
        builder.Services.AddSingleton<ITeamsCalendarClient, Calendar>();
        builder.Services.Configure<TeamsMeetingOptions>(options => options.SharedOrganizerEnabled = true);

        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try { await next(); }
            catch (Exception error)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { message = error.ToString() });
            }
        });
        app.MapRazorPages();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.BcUsers.Add(new() { BcUserId = 900, Role = BcUserRole.SuperAdmin });
            db.PlatformSettings.Add(new() { BookingTermsAndConditions = Terms });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
        var fields = handler.Length > 0
            ? new Dictionary<string, string>
            {
                ["TeamsOrganizer.Email"] = "mzalaconnect@belgiumcampus.ac.za",
                ["TeamsOrganizer.ObjectId"] = valid ? ObjectId : ""
            }
            : new Dictionary<string, string>
            {
                ["Input.SupportEmail"] = "support@example.com",
                ["Input.Terms"] = valid ? "The booking terms have been updated independently of Teams account settings." : "",
                ["Input.ReviewDeadline"] = "2026-10-30",
                ["Input.AdminReviewPeriodStartDate"] = "2026-10-01",
                ["Input.AdminReviewDeadline"] = "2026-10-31"
            };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/Administrator/Settings/Bookings{(handler.Length > 0 ? "?handler=" + handler : "")}")
            { Content = new FormUrlEncodedContent(fields) };
        if (handler.Length > 0) request.Headers.Accept.ParseAdd("application/json");
        using var response = await http.SendAsync(request);
        if (!valid)
        {
            string body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(handler.Length > 0 ? "Object" : "Booking terms", body);
            Assert.DoesNotContain(handler.Length > 0 ? "Booking terms" : "ObjectId", body);
        }
        else if (handler.Length > 0)
        {
            string body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, body);
            using var json = JsonDocument.Parse(body);
            Assert.True(json.RootElement.GetProperty("succeeded").GetBoolean(), body);
        }
        else Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync());

        using var savedScope = app.Services.CreateScope();
        var savedDb = savedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await savedDb.PlatformSettings.SingleAsync();
        Assert.Equal(valid && handler == "SaveTeamsOrganizer" ? ObjectId : null, settings.TeamsSharedOrganizerObjectId);
        Assert.Equal(handler.Length > 0 || !valid ? Terms : fields["Input.Terms"], settings.BookingTermsAndConditions);
        await app.StopAsync();
    }

    // Capture an invalid PageResult before layout rendering, without replacing
    // Razor's real handler selection, model binding, or validation pipeline.
    private sealed class CaptureValidationErrors : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is PageResult && !context.ModelState.IsValid)
                context.Result = new JsonResult(new
                {
                    message = string.Join(" ", context.ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage))
                }) { StatusCode = 400 };
        }
        public void OnResultExecuted(ResultExecutedContext context) { }
    }

    private sealed class SuperAdmin : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public CurrentUser GetRequiredUser() => new(900, "ADMIN", "Admin", "admin@example.com", BcUserRole.SuperAdmin);
    }

    private sealed class Calendar : ITeamsCalendarClient
    {
        public Task<bool> SupportsTeamsAsync(string id, CancellationToken ct) => Task.FromResult(id == ObjectId);
        public Task<TeamsCalendarEvent> CreateAsync(TeamsCalendarRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<TeamsCalendarEvent> GetAsync(string id, string eventId, CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync(string id, string eventId, CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> FindByTransactionAsync(string id, Guid transactionId, DateTimeOffset start, CancellationToken ct) => throw new NotSupportedException();
    }
}

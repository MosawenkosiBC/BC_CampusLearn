using BC_CampusLearn.Authentication;
using BC_CampusLearn.Authentication.Development;
using BC_CampusLearn.Data;
using BC_CampusLearn.Services.Bookings;
using BC_CampusLearn.Services.Availability;
using BC_CampusLearn.Services.Tutors;
using BC_CampusLearn.Services.Sessions;
using BC_CampusLearn.Services.Students;
using BC_CampusLearn.Services.Notifications;
using BC_CampusLearn.Services.Settings;
using BC_CampusLearn.Hubs;
using BC_CampusLearn.Models.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataProtection()
    .SetApplicationName("BC_CampusLearn");
builder.Services.AddOptions<IdentityProtectionOptions>()
    .Bind(builder.Configuration.GetSection(
        IdentityProtectionOptions.SectionName))
    .Validate(
        options => options.TryGetLookupKey(out _),
        "IdentityProtection:LookupKey must be a Base64-encoded key of at least 32 bytes.")
    .ValidateOnStart();

builder.Services.AddScoped<UserNotificationSignalRInterceptor>();
builder.Services.AddScoped<SettingsAuditService>();

string connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection was not configured.");

builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure();
        });
    options.AddInterceptors(
        serviceProvider.GetRequiredService<
            UserNotificationSignalRInterceptor>());
});

builder.Services.Configure<DevelopmentUserOptions>(
    builder.Configuration.GetSection(
        DevelopmentUserOptions.SectionName));

builder.Services.Configure<DevelopmentStudentOptions>(
    builder.Configuration.GetSection(
        DevelopmentStudentOptions.SectionName));

builder.Services.Configure<DevelopmentAdminOptions>(
    builder.Configuration.GetSection(
        DevelopmentAdminOptions.SectionName));

bool useDevelopmentAuthentication =
    builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>(
        "Authentication:UseDevelopmentAuthentication");

if (useDevelopmentAuthentication)
{
    builder.Services
        .AddAuthentication(
            CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/SignIn";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });
}
else
{
    builder.Services
        .AddAuthentication(
            OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(
            builder.Configuration.GetSection("AzureAd"));
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthorizationPolicies.TutorAccess,
        policy => policy.RequireRole(
            nameof(BcUserRole.Tutor),
            nameof(BcUserRole.HeadOfTutors)));

    options.AddPolicy(
        AuthorizationPolicies.AdminAccess,
        policy => policy.RequireRole(
            nameof(BcUserRole.Admin),
            nameof(BcUserRole.SuperAdmin),
            nameof(BcUserRole.Dev)));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<
    IEntraIdentityProtector,
    EntraIdentityProtector>();
builder.Services.AddScoped<IClaimsTransformation, BcUserClaimsTransformation>();

builder.Services.AddScoped<
    ICurrentUserService,
    ClaimsCurrentUserService>();

builder.Services.AddOptions<StudentDetailsApiOptions>()
    .Bind(builder.Configuration.GetSection(
        StudentDetailsApiOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<
    Microsoft.Extensions.Options.IValidateOptions<StudentDetailsApiOptions>,
    StudentDetailsApiOptionsValidator>();
builder.Services.AddHttpClient<
    IStudentDetailsService,
    StudentDetailsService>((serviceProvider, client) =>
    {
        StudentDetailsApiOptions options = serviceProvider
            .GetRequiredService<
                Microsoft.Extensions.Options.IOptions<StudentDetailsApiOptions>>()
            .Value;

        if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            client.BaseAddress = baseUri;
        }

        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.MaxResponseContentBufferSize = 64 * 1024;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

builder.Services.AddScoped<
    ITutorService,
    TutorService>();

builder.Services.Configure<TutorApplicationEmailOptions>(
    builder.Configuration.GetSection(
        TutorApplicationEmailOptions.SectionName));
builder.Services.AddScoped<
    ITutorApplicationEmailSender,
    TutorApplicationEmailSender>();

builder.Services.AddScoped<
    IBookingService,
    BookingService>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<
    ISessionLifecycleService,
    SessionLifecycleService>();
builder.Services.AddSignalR();

builder.Services.AddHostedService<
    ExpiredAvailabilityCleanupService>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage(
        "/Account/SignIn");
    options.Conventions.AllowAnonymousToPage(
        "/Account/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/NotFound");
    options.Conventions.AllowAnonymousToPage("/Maintenance");

    options.Conventions.AuthorizeFolder("/Student");
    options.Conventions.AuthorizeFolder("/Tutors");
    options.Conventions.AuthorizeFolder("/Bookings");
    options.Conventions.AuthorizeFolder(
        "/Administrator",
        AuthorizationPolicies.AdminAccess);

    string[] tutorOnlyPages =
    {
        "/Tutors/TutorDashboard",
        "/Tutors/ManageAvailability",
        "/Tutors/ManageResources",
        "/Tutors/Profile",
        "/Tutors/PublicProfile",
        "/Tutors/Sessions",
        "/Tutors/SessionDetails",
        "/Tutors/StatisticsOverview"
    };

    foreach (string page in tutorOnlyPages)
    {
        options.Conventions.AuthorizePage(
            page,
            AuthorizationPolicies.TutorAccess);
    }
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/NotFound");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (httpContext, next) =>
{
    PathString path = httpContext.Request.Path;
    bool bypass = path.StartsWithSegments("/Maintenance") ||
        path.StartsWithSegments("/Account") ||
        httpContext.User.IsInRole(nameof(BcUserRole.Dev));

    if (!bypass)
    {
        await using AsyncServiceScope scope =
            httpContext.RequestServices.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        bool maintenanceEnabled = await dbContext.PlatformSettings
            .AsNoTracking()
            .Where(settings => settings.PlatformSettingsId ==
                PlatformSettings.SingletonId)
            .Select(settings => settings.IsMaintenanceModeEnabled)
            .SingleAsync(httpContext.RequestAborted);

        if (maintenanceEnabled)
        {
            httpContext.Response.Redirect("/Maintenance");
            return;
        }
    }

    await next(httpContext);
});

app.MapRazorPages();
app.MapHub<SessionHub>("/hubs/session");

app.Run();

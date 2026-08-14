using BC_CampusLearn.Authentication;
using BC_CampusLearn.Authentication.Development;
using BC_CampusLearn.Data;
using BC_CampusLearn.Services.Bookings;
using BC_CampusLearn.Services.Availability;
using BC_CampusLearn.Services.Tutors;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

string connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection was not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure();
        });
});

builder.Services.Configure<DevelopmentUserOptions>(
    builder.Configuration.GetSection(
        DevelopmentUserOptions.SectionName));

builder.Services.Configure<DevelopmentStudentOptions>(
    builder.Configuration.GetSection(
        DevelopmentStudentOptions.SectionName));

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

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IClaimsTransformation, BcUserClaimsTransformation>();

builder.Services.AddScoped<
    ICurrentUserService,
    ClaimsCurrentUserService>();

builder.Services.AddScoped<
    ITutorService,
    TutorService>();

builder.Services.AddScoped<
    IBookingService,
    BookingService>();

builder.Services.AddHostedService<
    ExpiredAvailabilityCleanupService>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage(
        "/Account/SignIn");
    options.Conventions.AllowAnonymousToPage(
        "/Account/AccessDenied");

    options.Conventions.AuthorizeFolder("/Student");
    options.Conventions.AuthorizeFolder("/Tutors");
    options.Conventions.AuthorizeFolder("/Bookings");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

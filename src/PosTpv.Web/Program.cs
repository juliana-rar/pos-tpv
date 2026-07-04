using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using PosTpv.Application;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.Services;
using PosTpv.Infrastructure;
using PosTpv.Web.Components;
using PosTpv.Web.Hubs;
using PosTpv.Web.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Clean-architecture layers.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Real-time transport.
builder.Services.AddSignalR();
builder.Services.AddScoped<IKitchenNotifier, SignalRKitchenNotifier>();

// Authentication & authorization (cookie + roles).
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// Localization (English / Spanish) driven by a culture cookie. English is the default;
// the active culture flows into CultureInfo.CurrentUICulture, which Loc reads.
var supportedCultures = Loc.Supported.Select(c => new CultureInfo(c)).ToArray();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider { CookieName = Loc.CookieName }
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Language switch: persists the chosen culture in a cookie and reloads to it.
app.MapGet("/culture/set", (string culture, string? redirect, HttpContext http) =>
{
    if (Loc.Supported.Contains(culture))
    {
        http.Response.Cookies.Append(
            Loc.CookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
    }
    return Results.LocalRedirect(string.IsNullOrWhiteSpace(redirect) ? "/" : redirect);
}).AllowAnonymous();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<KitchenHub>(KitchenHub.Path);

// Cookie sign-out endpoint (posted from the top bar).
app.MapPost("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).DisableAntiforgery();

// Billing report download (CSV / Excel / PDF).
app.MapGet("/export/billing", async (DateTime? from, DateTime? to, string? format,
    IBillingService billing, IReportExporter exporter) =>
{
    var f = from ?? DateTime.Today.AddDays(-7);
    var t = to ?? DateTime.Today;
    var fmt = format?.ToLowerInvariant() switch
    {
        "xlsx" or "excel" => ExportFormat.Excel,
        "pdf" => ExportFormat.Pdf,
        _ => ExportFormat.Csv
    };
    var report = await billing.GetReportAsync(f, t);
    var file = exporter.ExportBilling(report, f, t, fmt);
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization(policy => policy.RequireRole("Admin", "Cashier"));

// Apply migrations and seed the demo catalogue on startup.
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IDbSeeder>();
    await seeder.SeedAsync();
}

app.Run();

using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using PosTpv.Application;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Application.Services;
using PosTpv.Domain.Enums;
using PosTpv.Infrastructure;
using PosTpv.Web.Components;
using PosTpv.Web.Hubs;
using PosTpv.Web.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Compresses the server-rendered page HTML and any non-fingerprinted responses (MapStaticAssets
// already pre-compresses its own build-time assets, but the actual page shell/SignalR negotiate
// responses weren't covered by that).
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

// Clean-architecture layers.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Real-time transport.
builder.Services.AddSignalR();
builder.Services.AddScoped<IKitchenNotifier, SignalRKitchenNotifier>();
builder.Services.AddScoped<PosTpv.Web.Components.Layout.NavMoreState>();

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

// Throttles the one anonymous write path into the app (public reservation intake) — everything
// else is behind staff cookie auth and doesn't need this.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("public-reservations", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
    });
});

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

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
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

// MapStaticAssets only serves files known at build time (via its manifest), so
// uploaded product/category images (written to wwwroot at runtime) need the
// classic static file middleware as well.
app.UseStaticFiles();
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

// Public reservation intake for the separate "pos-reserva" presentation site. This is the one
// anonymous write path into the app — deliberately narrow: it can only create a brand-new
// Pending, tableless reservation (staff assign tables afterwards from the existing dashboard,
// exactly as they would for a phone booking). Called server-to-server from pos-reserva only,
// never directly from a customer's browser, and gated by a shared API key plus rate limiting.
app.MapPost("/api/public/reservations", async (
    PublicReservationRequestDto request,
    HttpContext http,
    IConfiguration config,
    IReservationService reservations) =>
{
    var expectedKey = config["PublicApi:ReservaApiKey"];
    if (string.IsNullOrEmpty(expectedKey)
        || !http.Request.Headers.TryGetValue("X-Reserva-Api-Key", out var providedKey)
        || providedKey != expectedKey)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.CustomerName) || request.CustomerName.Length > 100)
        return Results.BadRequest("CustomerName is required (max 100 characters).");
    if (request.Phone is { Length: > 30 })
        return Results.BadRequest("Phone is too long (max 30 characters).");
    if (request.Date.Date < DateTime.Today)
        return Results.BadRequest("Date cannot be in the past.");
    if (request.PartySize is < 1 or > 20)
        return Results.BadRequest("PartySize must be between 1 and 20.");
    if (request.ChildrenCount < 0 || request.HighChairCount < 0)
        return Results.BadRequest("ChildrenCount/HighChairCount cannot be negative.");
    if (request.Comments is { Length: > 500 })
        return Results.BadRequest("Comments too long (max 500 characters).");

    var form = new ReservationFormDto
    {
        CustomerName = request.CustomerName.Trim(),
        Phone = request.Phone?.Trim(),
        Date = request.Date.Date,
        Time = request.Time,
        PartySize = request.PartySize,
        ChildrenCount = request.ChildrenCount,
        HighChairCount = request.HighChairCount,
        Comments = string.IsNullOrWhiteSpace(request.Comments)
            ? "Reserva web"
            : $"Reserva web: {request.Comments.Trim()}",
        Status = ReservationStatus.Pending,
        TableIds = new List<int>()
    };

    var id = await reservations.CreateAsync(form);
    return Results.Created($"/api/public/reservations/{id}", new { id });
})
.AllowAnonymous()
.DisableAntiforgery()
.RequireRateLimiting("public-reservations");

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

    // Warm the in-memory app-settings cache so branding/schedule are available from the
    // very first page render, without every component needing its own DB round-trip.
    var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
    await settings.GetAsync();
}

app.Run();

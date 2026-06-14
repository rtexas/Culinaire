using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Portal.Models;
using Portal.Services;

// ── File logger — first line of defence before SQL ────────────────────────────
using var fileLogger = new FileLoggerService();

// ── Load connection string ────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

var connectionString = builder.Configuration.GetConnectionString("Culinaire")
    ?? throw new InvalidOperationException("Connection string 'Culinaire' missing from appsettings.json.");

// ── Load settings & theme ─────────────────────────────────────────────────────
var settingsService = new SettingsService(connectionString);
IReadOnlyDictionary<string, string> rawSettings;
try
{
    rawSettings = await settingsService.LoadAsync();
    fileLogger.WriteEntry(LogLevel.Information, $"Loaded {rawSettings.Count} setting(s) from [dbo].[Settings].");
}
catch (Exception ex)
{
    fileLogger.WriteException("Cannot connect to database — check connection string in appsettings.json", ex);
    Console.Error.WriteLine($"[CRITICAL] {ex.Message}");
    return 1;
}

var appSettings = AppSettings.From(rawSettings);
var sqlLogger   = new SqlLoggerService(connectionString, appSettings.LoggingMinLevel, fileLogger);
await sqlLogger.PurgeOldLogsAsync();

var themeService = new ThemeService();
themeService.Load(rawSettings);

// ── Seed Administrator account if no users exist ──────────────────────────────
var userService = new UserService(connectionString);
await userService.SeedAdminAsync();
await sqlLogger.LogAsync("Culinaire Portal started.", LogLevel.Information);

// ── DI registration ───────────────────────────────────────────────────────────
builder.Services.AddSingleton(fileLogger);
builder.Services.AddSingleton(sqlLogger);
builder.Services.AddSingleton(themeService);
builder.Services.AddSingleton<SettingsService>(_ => settingsService);
builder.Services.AddSingleton<UserService>(_ => new UserService(connectionString));
builder.Services.AddSingleton<AuthService>(_ => new AuthService(connectionString));
builder.Services.AddSingleton<PermissionService>(_ => new PermissionService(connectionString));
builder.Services.AddSingleton<AccountCategoryService>(_ => new AccountCategoryService(connectionString));
builder.Services.AddSingleton<AccountTypeService>(_ => new AccountTypeService(connectionString));
builder.Services.AddSingleton<StateRegionService>(_ => new StateRegionService(connectionString));
builder.Services.AddSingleton<CountryService>(_ => new CountryService(connectionString));
builder.Services.AddSingleton<VendorService>(_ => new VendorService(connectionString));
builder.Services.AddSingleton<ChartOfAccountsService>(sp => new ChartOfAccountsService(
    connectionString,
    sp.GetRequiredService<AccountCategoryService>(),
    sp.GetRequiredService<AccountTypeService>()));
builder.Services.AddSingleton<ItemService>(_ => new ItemService(connectionString));
builder.Services.AddSingleton<ShippingMethodService>(_ => new ShippingMethodService(connectionString));
builder.Services.AddSingleton<PayableService>(_ => new PayableService(connectionString));
builder.Services.AddSingleton<EodRowService>(_ => new EodRowService(connectionString));
builder.Services.AddSingleton<EodColumnService>(_ => new EodColumnService(connectionString));
builder.Services.AddSingleton<EodSectionService>(_ => new EodSectionService(connectionString));
builder.Services.AddSingleton<EodSetupService>(_ => new EodSetupService(connectionString));
builder.Services.AddSingleton<EodSalesService>(sp =>
    new EodSalesService(connectionString, sp.GetRequiredService<EodSetupService>()));
builder.Services.AddSingleton<CheckSetupService>(_ => new CheckSetupService(connectionString));
builder.Services.AddSingleton<CheckService>(_ => new CheckService(connectionString));
builder.Services.AddSingleton<EmployeeService>(_ => new EmployeeService(connectionString));
builder.Services.AddSingleton<JobRoleService>(_ => new JobRoleService(connectionString));
builder.Services.AddSingleton<PayrollBatchService>(_ => new PayrollBatchService(connectionString));
builder.Services.AddSingleton<PayrollRunService>(_ => new PayrollRunService(connectionString));
builder.Services.AddSingleton<LocationService>(_ => new LocationService(connectionString));
builder.Services.AddSingleton<DepartmentService>(_ => new DepartmentService(connectionString));
builder.Services.AddSingleton<CoaSegmentsService>(_ => new CoaSegmentsService(connectionString));
builder.Services.AddScoped<LocationSessionService>();

// ── Authentication ────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath         = "/login";
        options.LogoutPath        = "/logout";
        options.AccessDeniedPath  = "/unauthorized";
        options.Cookie.Name       = "CulinairePortal";
        options.Cookie.HttpOnly   = true;
        options.Cookie.SameSite   = SameSiteMode.Strict;
        options.ExpireTimeSpan    = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly",    p => p.RequireRole("Administrator"));
    options.AddPolicy("NotViewer",    p => p.RequireRole("Administrator", "User"));
    options.AddPolicy("Authenticated",p => p.RequireAuthenticatedUser());
});

// ── Blazor + Razor Pages ──────────────────────────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// ── File upload limit (logo) ──────────────────────────────────────────────────
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 5 * 1024 * 1024; // 5 MB
});

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Login / Logout endpoints (Razor Pages handle cookie auth) ─────────────────
app.MapRazorPages();

// ── Blazor ────────────────────────────────────────────────────────────────────
app.MapRazorComponents<Portal.Components.App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
return 0;


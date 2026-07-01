using Microsoft.AspNetCore.Localization;
using MMCA.Common.Aspire;
using MMCA.Common.Shared.Globalization;
using MMCA.Common.UI.Services;
using MMCA.Helpdesk.UI.Web.Components;
using MMCA.Helpdesk.UI.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery, and HTTP resilience (MMCA.Common.Aspire).
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Internationalization (ADR-027) + Day/Dark theme (ADR-028). The shared UseCommonRequestLocalization /
// MapCultureEndpoint helpers live in MMCA.Common.API; this seed does not reference the API layer, so the
// few lines are inlined here against the same SupportedCultures allowlist + ThemeService.
builder.Services.AddLocalization();
builder.Services.AddScoped<ThemeService>();

// Server-side typed client to the API. The base address ("https+http://web") comes from config so
// the service-discovery handler (from AddServiceDefaults) resolves the "web" API resource at runtime.
var apiBaseAddress = builder.Configuration["Api:BaseAddress"]
    ?? throw new InvalidOperationException("Api:BaseAddress is not configured.");
builder.Services.AddHttpClient<HelpdeskApiClient>(client =>
    client.BaseAddress = new Uri(apiBaseAddress));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Set CurrentUICulture from the culture cookie / Accept-Language so SSR prerender uses the right locale.
string[] supportedCultures = [.. SupportedCultures.All];
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(SupportedCultures.Default)
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapDefaultEndpoints();

// Culture switch endpoint (ADR-027): writes the standard ASP.NET culture cookie and reloads.
app.MapGet("/culture/set", (string culture, string? redirectUri, HttpContext context) =>
{
    if (SupportedCultures.IsSupported(culture))
    {
        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
            });
    }

    return Results.LocalRedirect(string.IsNullOrWhiteSpace(redirectUri) ? "/" : redirectUri);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // The shared SSR /Error page (MMCA.Common.UI.Web): UseExceptionHandler("/Error") above re-executes
    // to that route, which this host does not define itself.
    .AddAdditionalAssemblies(typeof(MMCA.Common.UI.Web._Imports).Assembly);

await app.RunAsync();

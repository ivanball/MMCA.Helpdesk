using MMCA.Common.API.Startup;
using MMCA.Common.Aspire;
using MMCA.Common.UI.Services.Culture;
using MMCA.Common.UI.Theme;
using MMCA.Helpdesk.UI.Web.Components;
using MMCA.Helpdesk.UI.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery, and HTTP resilience (MMCA.Common.Aspire).
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Internationalization (ADR-027) + Day/Dark theme (ADR-028). The request-localization middleware and
// the /culture/set endpoint below come from the shared MMCA.Common.API helpers (reached through
// MMCA.Common.UI.Web), so the SupportedCultures allowlist and the Development-only pseudo locale are
// the framework's, not a local copy.
builder.Services.AddLocalization();
builder.Services.AddScoped<ThemeService>();

// CultureSwitcher delegates the actual switch to ICultureApplier, which AddUIShared would normally
// register; this seed does not call AddUIShared (it has no ApiSettings-backed APIClient pipeline), so
// the Blazor Web default is registered here. It navigates to the /culture/set endpoint mapped below.
builder.Services.AddScoped<ICultureApplier, EndpointCultureApplier>();

// Server-side typed client to the API. The base address ("https+http://web") comes from config so
// the service-discovery handler (from AddServiceDefaults) resolves the "web" API resource at runtime.
var apiBaseAddress = builder.Configuration["Api:BaseAddress"]
    ?? throw new InvalidOperationException("Api:BaseAddress is not configured.");
// Multi-tenancy demo: the API resolves the tenant from a claim first, then the X-Tenant-Id header.
// This client calls the API server-side and carries no user token, so it stamps the header from
// config. Point Api:TenantId at "globex" to browse the tenant whose rows live in their own database,
// with no other change anywhere in the UI. Leave it empty to call as the system caller, which sees
// every tenant's rows.
var tenantId = builder.Configuration["Api:TenantId"];
builder.Services.AddHttpClient<HelpdeskApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
    if (!string.IsNullOrWhiteSpace(tenantId))
    {
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
    }
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Set CurrentUICulture from the culture cookie / Accept-Language so SSR prerender uses the right locale.
// The helper accepts the Development-only pseudo locale (ADR-027 section 8) under the same condition as
// the /culture/set endpoint below, so the CultureSwitcher's pseudo entry never silently no-ops.
app.UseCommonRequestLocalization();

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapDefaultEndpoints();

// Culture switch endpoint (ADR-027): writes the standard ASP.NET culture cookie and reloads. The
// helper maps it AllowAnonymous (SEC-Common-16), so it stays public the day this host gains an
// authenticated surface. httpOnly: true because this is a Server-only host with no WebAssembly
// client to read the cookie; a host that serves WASM keeps the parameterless overload.
app.MapCultureEndpoint(httpOnly: true);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // The shared SSR /Error page (MMCA.Common.UI.Web): UseExceptionHandler("/Error") above re-executes
    // to that route, which this host does not define itself.
    .AddAdditionalAssemblies(typeof(MMCA.Common.UI.Web._Imports).Assembly);

await app.RunAsync();

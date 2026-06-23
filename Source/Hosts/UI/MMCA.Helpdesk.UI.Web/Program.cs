using MMCA.Common.Aspire;
using MMCA.Helpdesk.UI.Web.Components;
using MMCA.Helpdesk.UI.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery, and HTTP resilience (MMCA.Common.Aspire).
builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

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

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapDefaultEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();

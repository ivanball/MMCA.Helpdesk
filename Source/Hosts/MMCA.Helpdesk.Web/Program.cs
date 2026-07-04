using MMCA.Common.API;
using MMCA.Common.API.Startup;
using MMCA.Common.Application;
using MMCA.Common.Application.Modules;
using MMCA.Common.Application.Settings;
using MMCA.Common.Aspire;
using MMCA.Common.Infrastructure;
using MMCA.Helpdesk.Tickets.API.Resources;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery, and HTTP resilience (MMCA.Common.Aspire).
builder.AddServiceDefaults();

var services = builder.Services;

services.AddOptions<ApplicationSettings>()
    .Bind(builder.Configuration.GetSection(ApplicationSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var applicationSettings = builder.Configuration.GetSection(ApplicationSettings.SectionName).Get<ApplicationSettings>()
    ?? throw new InvalidOperationException("ApplicationSettings section is not configured.");

services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("SQLServerConnectionString")
        ?? throw new InvalidOperationException("SQLServerConnectionString is not configured."));

services.AddCommonCors(builder.Configuration);
services.AddCommonApiVersioning();
services.AddCommonRateLimiting();
services.AddOutputCache(options => options.AddBasePolicy(policy => policy.NoCache()));
services.AddCommonResponseCompression();

// Authentication. When an Identity issuer is configured (Authentication:JwtBearer:Authority), validate
// RS256 tokens against its JWKS. This monolith seed ships WITHOUT an Identity module, so by default the
// authority is unset and we register a bare auth scheme to satisfy the middleware pipeline; the Tickets
// endpoints are [AllowAnonymous] until you add Identity (GETTING-STARTED.md Phase 8) and set the
// authority, at which point you flip the controller back to [Authorize].
var jwtAuthority = builder.Configuration["Authentication:JwtBearer:Authority"];
if (!string.IsNullOrWhiteSpace(jwtAuthority))
{
    services.AddForwardedJwtBearer(
        authority: jwtAuthority,
        audience: builder.Configuration["Jwt:Audience"] ?? "helpdesk");
}
else
{
    services.AddAuthentication();
    services.AddAuthorization();
}

services.AddCommonExceptionHandlers();

// Fixed DI sequence: AddApplication -> module scans (via ModuleLoader) -> AddApplicationDecorators last.
services.AddApplication();
services.AddInfrastructure(builder.Configuration);

services.AddOptions<ModulesSettings>()
    .Bind(builder.Configuration.GetSection(ModulesSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var modulesSettings = builder.Configuration.GetSection(ModulesSettings.SectionName).Get<ModulesSettings>() ?? [];

services.AddAPI(modulesSettings);

// Contribute the Tickets module's error-code translations to the edge localizer (ADR-027): domain
// error text (TicketInvariants codes like "Ticket.Closed") now returns localized ProblemDetails
// messages; request localization itself already runs inside UseCommonMiddlewarePipeline().
services.AddErrorResources<TicketsErrorResources>();

using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var moduleLoader = new ModuleLoader
{
    Logger = loggerFactory.CreateLogger<ModuleLoader>(),
};
moduleLoader.DiscoverAndRegister(services, builder.Configuration, applicationSettings, modulesSettings, builder.Environment.EnvironmentName);
services.AddSingleton(moduleLoader);

// In-process message bus in the monolith (no MessageBus:Provider configured). When a module is
// extracted, the AppHost's WithBroker injects the provider and this same call wires the broker.
services.AddBrokerMessaging(builder.Configuration);

services.AddApplicationDecorators();

services.AddModuleHealthChecks(moduleLoader);

var app = builder.Build();

// Applies migrations / runs seeders per ApplicationSettings.DatabaseInitStrategy.
await app.Services.InitializeDatabaseAsync(applicationSettings, moduleLoader);

app.MapDefaultEndpoints();
app.UseCommonMiddlewarePipeline();

await app.RunAsync();

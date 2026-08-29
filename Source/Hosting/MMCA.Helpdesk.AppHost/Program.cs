// Aspire AppHost for the MMCA.Helpdesk monolith: a SQL Server container, one database, and the
// single Web host. WithSQLServerDataSource injects both the routing key and ConnectionStrings__
// SQLServerConnectionString; with one data source they collapse onto a single physical database,
// so the app behaves as a classic single-DB monolith. The extraction phase (GETTING-STARTED.md
// Phase 8) adds per-service databases, a broker, a gateway, and JWKS discovery here.
using MMCA.Common.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var helpdeskDb = sql.AddDatabase("helpdesk", "Helpdesk");

// Multi-tenancy demo (MMCA.Common v1.150.0). Two tenants show the two isolation modes the framework
// supports at once: "acme" is any tenant WITHOUT an override and shares the pooled database above
// (shared schema, rows separated by the TenantId query filter), while "globex" is routed onto its own
// database by the per-tenant DataSources override below. The override is keyed by PHYSICAL source
// name, and the seed's single "Tickets" logical source collapses onto Default, so the key is "Default".
// The web host migrates this database on startup like any other tenant target.
var globexDb = sql.AddDatabase("helpdesk-globex", "Helpdesk_Globex");

// WaitFor the SQL server (healthy once the container accepts connections), not the database
// resource. The web host CREATES the database via EF Migrate at startup, so waiting on the
// database's existence would deadlock: it never exists until the app that is waiting runs.
var web = builder.AddProject<Projects.MMCA_Helpdesk_Web>("web")
    .WithSQLServerDataSource(helpdeskDb, "Tickets")
    .WithEnvironment(
        "Tenancy__Tenants__globex__DataSources__Default__SQLServerConnectionString",
        globexDb.Resource.ConnectionStringExpression)
    .WaitFor(sql)
    // Declares the readiness probe as this resource's health check, which is what makes a downstream
    // WaitFor(web) mean "wait until the API is HEALTHY" instead of "wait until its process started".
    // MapDefaultEndpoints() serves /health/ready from the same MMCA.Common.Aspire pipeline the Azure
    // Container Apps readiness probe uses, so local orchestration and the deployed probe gate on the
    // same signal: the required SQL check passing (optional dependencies deliberately do not gate it).
    .WithHttpHealthCheck("/health/ready")
    .WithExternalHttpEndpoints();

// Blazor Server UI. Calls the API server-side via service discovery ("web"); WithReference injects
// the endpoint the UI's typed HttpClient resolves. WaitFor(web) holds the UI back until the API's
// health check above reports healthy, so the first page render cannot race a host that is still
// migrating the database.
builder.AddProject<Projects.MMCA_Helpdesk_UI_Web>("ui")
    .WithReference(web)
    .WaitFor(web)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();

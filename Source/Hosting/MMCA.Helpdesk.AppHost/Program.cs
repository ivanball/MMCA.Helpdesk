// Aspire AppHost for the MMCA.Helpdesk monolith: a SQL Server container, one database, and the
// single Web host. WithDataSource injects both the routing key and ConnectionStrings__
// SQLServerConnectionString; with one data source they collapse onto a single physical database,
// so the app behaves as a classic single-DB monolith. The extraction phase (GETTING-STARTED.md
// Phase 8) adds per-service databases, a broker, a gateway, and JWKS discovery here.
using MMCA.Common.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var helpdeskDb = sql.AddDatabase("helpdesk", "Helpdesk");

// WaitFor the SQL server (healthy once the container accepts connections), not the database
// resource. The web host CREATES the database via EF Migrate at startup, so waiting on the
// database's existence would deadlock: it never exists until the app that is waiting runs.
var web = builder.AddProject<Projects.MMCA_Helpdesk_Web>("web")
    .WithDataSource(helpdeskDb, "Tickets")
    .WaitFor(sql)
    .WithExternalHttpEndpoints();

// Blazor Server UI. Calls the API server-side via service discovery ("web"); WithReference injects
// the endpoint the UI's typed HttpClient resolves. WaitFor(web) gates the UI until the API is healthy.
builder.AddProject<Projects.MMCA_Helpdesk_UI_Web>("ui")
    .WithReference(web)
    .WaitFor(web)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();

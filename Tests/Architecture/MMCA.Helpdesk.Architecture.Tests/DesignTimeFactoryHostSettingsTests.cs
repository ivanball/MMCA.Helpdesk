using System.Runtime.Loader;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using MMCA.Common.Infrastructure.Persistence.AuditTrail;
using MMCA.Common.Infrastructure.Persistence.DbContexts;
using MMCA.Common.Infrastructure.Scheduling;

namespace MMCA.Helpdesk.Architecture.Tests;

/// <summary>
/// Keeps the design-time factory's framework-table flags in step with the host settings they mirror.
/// <c>EnableAuditTrail</c> and <c>EnableScheduler</c> are set by hand in the migrations project, and
/// <c>AuditTrail:Enabled</c> / <c>Scheduler:Enabled</c> are set in the Web host's appsettings: if the
/// two disagree, the next scaffolded migration adds or drops a table the running model does not
/// have, and nothing fails until a database is migrated.
/// <para>
/// The factory is the REAL one, loaded from the migrations project's build output rather than
/// referenced: a referenced migrations assembly lands in this project's output and its generated
/// migration code would count toward coverage. The test project carries a build-order-only
/// reference to it, so the output read here is never stale. Each flag is read through its effect,
/// whether the model the factory builds maps the table the flag gates, which is exactly the property
/// that has to match the host. No database is involved: the connection is never opened.
/// </para>
/// </summary>
public sealed class DesignTimeFactoryHostSettingsTests
{
    private const string MigrationsProjectName = "MMCA.Helpdesk.Migrations.SqlServer.Tickets";

    private static readonly string RepoRoot = ArchitectureMapBase.FindRepoRoot("MMCA.Helpdesk.slnx");

    private static readonly string HostDirectory = Path.Combine(RepoRoot, "Source", "Hosts", "MMCA.Helpdesk.Web");

    [Fact]
    public void FactoryFlags_MatchTheHostSettings_InEveryEnvironment()
    {
        var model = BuildFactoryModel();
        (string Flag, string HostKey, bool FactoryValue)[] flags =
        [
            ("EnableAuditTrail", $"{AuditTrailSettings.SectionName}:{nameof(AuditTrailSettings.Enabled)}", model.FindEntityType(typeof(AuditTrailEntry)) is not null),
            ("EnableScheduler", $"{SchedulerSettings.SectionName}:{nameof(SchedulerSettings.Enabled)}", model.FindEntityType(typeof(ScheduledJobEntry)) is not null),
        ];

        var mismatches = new List<string>();
        foreach (var (files, configuration) in HostConfigurations())
        {
            foreach (var (flag, hostKey, factoryValue) in flags)
            {
                bool hostValue = configuration.GetValue<bool>(hostKey);
                if (hostValue != factoryValue)
                {
                    mismatches.Add($"{flag} is {factoryValue} on the design-time factory, but {hostKey} is {hostValue} in {files}");
                }
            }
        }

        mismatches.Should().BeEmpty(
            "the scaffolded model must match the running one; fix whichever side is wrong: {0}",
            string.Join("; ", mismatches));
    }

    /// <summary>
    /// The host's effective settings for its base file alone and for the base file overlaid with each
    /// <c>appsettings.{Environment}.json</c> beside it, the order the host itself layers them in.
    /// </summary>
    /// <returns>Each layering, labelled by the files that produced it.</returns>
    private static IEnumerable<(string Files, IConfiguration Configuration)> HostConfigurations()
    {
        const string baseFile = "appsettings.json";
        File.Exists(Path.Combine(HostDirectory, baseFile)).Should().BeTrue($"the Web host's settings are read from {HostDirectory}");

        yield return (baseFile, new ConfigurationBuilder().SetBasePath(HostDirectory).AddJsonFile(baseFile).Build());

        foreach (string environmentFile in Directory.GetFiles(HostDirectory, "appsettings.*.json").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal))
        {
            yield return (
                $"{baseFile} + {environmentFile}",
                new ConfigurationBuilder().SetBasePath(HostDirectory).AddJsonFile(baseFile).AddJsonFile(environmentFile).Build());
        }
    }

    /// <summary>
    /// Builds the model through the migrations project's own design-time factory, found by its
    /// interface so the test does not depend on the factory's class name.
    /// </summary>
    /// <returns>The finalized model the next <c>dotnet ef migrations add</c> would diff against.</returns>
    private static IModel BuildFactoryModel()
    {
        // The test runs from bin/<configuration>/<target framework>/, and the migrations project
        // builds to the same pair under its own folder.
        var testOutput = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        string assemblyPath = Path.Combine(
            RepoRoot,
            "Source",
            "Hosting",
            MigrationsProjectName,
            "bin",
            testOutput.Parent!.Name,
            testOutput.Name,
            $"{MigrationsProjectName}.dll");
        File.Exists(assemblyPath).Should().BeTrue($"the migrations project is built before this test project, to {assemblyPath}");

        var factoryType = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath).GetTypes()
            .Should().ContainSingle(t => t.IsClass && !t.IsAbstract && typeof(IDesignTimeDbContextFactory<SQLServerDbContext>).IsAssignableFrom(t))
            .Subject;

        var factory = (IDesignTimeDbContextFactory<SQLServerDbContext>)Activator.CreateInstance(factoryType)!;
        using var context = factory.CreateDbContext([]);
        return context.Model;
    }
}

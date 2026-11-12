using System.Runtime.Loader;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MMCA.Common.Domain.Webhooks;
using MMCA.Common.Infrastructure.Persistence.Webhooks;

namespace MMCA.Helpdesk.Architecture.Tests.Tickets;

/// <summary>
/// Pins that the worked example's webhook table is actually migrated (item 35): the migrations
/// project's model snapshot, which is what <c>dotnet ef migrations add AddWebhookSubscriptions</c>
/// leaves behind, maps <see cref="WebhookSubscription"/> to the framework's table. The design-time
/// factory flag alone is not enough: <c>DesignTimeFactoryHostSettingsTests</c> proves the factory and
/// the host agree, and only this proves a migration was scaffolded from that agreement, so a database
/// migrated at startup has the table the running model reads.
/// <para>
/// The snapshot is read from the migrations project's own build output, the way
/// <c>DesignTimeFactoryHostSettingsTests</c> loads the factory, so no database is involved.
/// </para>
/// </summary>
public sealed class WebhookMigrationTests
{
    private const string MigrationsProjectName = "MMCA.Helpdesk.Migrations.SqlServer.Tickets";

    private static readonly string RepoRoot = ArchitectureMapBase.FindRepoRoot("MMCA.Helpdesk.slnx");

    [Fact]
    public void ModelSnapshot_MapsTheWebhookSubscriptionsTable()
    {
        var entity = LoadSnapshotModel().FindEntityType(typeof(WebhookSubscription).FullName!);

        entity.Should().NotBeNull("the migrations must create the table the host's Webhooks:Enabled model maps");
        entity!.GetTableName().Should().Be(WebhookSubscriptionModelBuilderExtensions.TableName);
    }

    /// <summary>
    /// Loads the migrations assembly from its own build output and returns the model its snapshot
    /// describes, the model the next migration is diffed against.
    /// </summary>
    /// <returns>The snapshot model.</returns>
    private static IModel LoadSnapshotModel()
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

        var snapshotType = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath).GetTypes()
            .Should().ContainSingle(t => t.IsClass && !t.IsAbstract && typeof(ModelSnapshot).IsAssignableFrom(t))
            .Which;

        var snapshot = (ModelSnapshot)Activator.CreateInstance(snapshotType, nonPublic: true)!;
        return snapshot.Model;
    }
}

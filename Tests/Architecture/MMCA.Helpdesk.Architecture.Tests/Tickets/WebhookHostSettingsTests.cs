using Microsoft.Extensions.Configuration;
using MMCA.Common.Application.Webhooks;
using MMCA.Common.Infrastructure.Persistence.InternalCommands.Administration;

namespace MMCA.Helpdesk.Architecture.Tests.Tickets;

/// <summary>
/// Pins the Web host's webhook settings for the worked example (item 35), read from the same
/// <c>appsettings.json</c> plus <c>appsettings.{Environment}.json</c> layering the host applies, so no
/// host is started and no database is involved.
/// <list type="bullet">
/// <item>Webhooks are on in every environment: the seed demonstrates the feature rather than shipping it
/// dormant.</item>
/// <item>Insecure targets (plain http, a loopback or private address) are allowed in Development only,
/// so a developer can point a subscription at a local receiver and no other environment can.</item>
/// <item>Internal commands are never switched off. Deliveries are internal commands, and the dedicated
/// delivery lane exists only while <c>Webhooks:Enabled</c> and <c>InternalCommands:Enabled</c> are both
/// on; with the queue off every delivery would be scheduled and never sent.</item>
/// </list>
/// </summary>
public sealed class WebhookHostSettingsTests
{
    private const string DevelopmentFile = "appsettings.Development.json";

    private const string EnabledKey = $"{WebhookSettings.SectionName}:{nameof(WebhookSettings.Enabled)}";

    private const string InsecureTargetsKey =
        $"{WebhookSettings.SectionName}:{nameof(WebhookSettings.AllowInsecureTargetsInDevelopment)}";

    private static readonly string HostDirectory = Path.Combine(
        ArchitectureMapBase.FindRepoRoot("MMCA.Helpdesk.slnx"),
        "Source",
        "Hosts",
        "MMCA.Helpdesk.Web");

    private static readonly string InternalCommandsEnabledKey =
        $"{InternalCommandsSettings.SectionName}:{nameof(InternalCommandsSettings.Enabled)}";

    [Fact]
    public void Webhooks_AreEnabled_InEveryEnvironment()
    {
        var off = HostConfigurations()
            .Where(layering => !layering.Configuration.GetValue<bool>(EnabledKey))
            .Select(layering => layering.Files)
            .ToList();

        off.Should().BeEmpty("{0} is the worked example's opt-in; it is off in: {1}", EnabledKey, string.Join("; ", off));
    }

    [Fact]
    public void InsecureTargets_AreAllowed_InDevelopmentOnly()
    {
        var allowed = HostConfigurations()
            .Where(layering => layering.Configuration.GetValue<bool>(InsecureTargetsKey))
            .Select(layering => layering.Files)
            .ToList();

        allowed.Should().ContainSingle(
            "{0} relaxes the target checks for a local receiver, which belongs to Development alone",
            InsecureTargetsKey)
            .Which.Should().EndWith(DevelopmentFile);
    }

    [Fact]
    public void InternalCommands_AreNeverSwitchedOff_SoTheDeliveryLaneRuns()
    {
        var off = HostConfigurations()
            .Where(layering => !layering.Configuration.GetValue(InternalCommandsEnabledKey, defaultValue: true))
            .Select(layering => layering.Files)
            .ToList();

        off.Should().BeEmpty(
            "webhook deliveries are internal commands and the delivery lane runs only while both switches are on; {0} is false in: {1}",
            InternalCommandsEnabledKey,
            string.Join("; ", off));
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
}

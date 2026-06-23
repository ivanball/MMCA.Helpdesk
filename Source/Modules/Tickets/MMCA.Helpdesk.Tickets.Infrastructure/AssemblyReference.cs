using System.Reflection;

namespace MMCA.Helpdesk.Tickets.Infrastructure;

/// <summary>
/// Assembly anchor. The framework's <c>DefaultEntityConfigurationAssemblyProvider</c> auto-discovers
/// this assembly at runtime by its ".Infrastructure" name; the design-time migrations factory passes
/// it explicitly to <c>AddConfigurationAssembly</c>.
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    public static readonly string AssemblyName = Assembly.GetName().Name ?? string.Empty;
}

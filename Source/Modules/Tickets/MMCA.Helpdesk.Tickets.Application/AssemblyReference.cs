using System.Reflection;

namespace MMCA.Helpdesk.Tickets.Application;

/// <summary>
/// Assembly anchor for reflection-based discovery (configuration assemblies, etc.).
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    public static readonly string AssemblyName = Assembly.GetName().Name ?? string.Empty;
}

/// <summary>
/// Type anchor passed to <c>ScanModuleApplicationServices&lt;T&gt;()</c> so Scrutor scans this assembly.
/// </summary>
public class ClassReference;

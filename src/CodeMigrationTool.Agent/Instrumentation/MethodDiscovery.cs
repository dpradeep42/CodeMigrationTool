using System.Reflection;
using CodeMigrationTool.Shared.Models;

namespace CodeMigrationTool.Agent.Instrumentation;

/// <summary>
/// Discovers methods in loaded assemblies via reflection.
/// Identifies WCF service contracts, operation contracts, and other attributed methods.
/// </summary>
public class MethodDiscovery
{
    private readonly List<DiscoveredMethodInfo> _discoveredMethods = [];
    private readonly List<Assembly> _loadedAssemblies = [];

    /// <summary>
    /// Well-known attribute names that indicate interesting methods for migration.
    /// </summary>
    private static readonly HashSet<string> InterestingAttributes =
    [
        "ServiceContract",
        "OperationContract",
        "DataContract",
        "WebGet",
        "WebInvoke",
        "HttpGet",
        "HttpPost",
        "HttpPut",
        "HttpDelete",
        "Route",
        "ApiController"
    ];

    public Assembly LoadAssembly(string appPath)
    {
        var assembly = Assembly.LoadFrom(appPath);
        _loadedAssemblies.Add(assembly);

        // Also load referenced assemblies to resolve dependencies
        foreach (var refName in assembly.GetReferencedAssemblies())
        {
            try
            {
                var refAssembly = Assembly.Load(refName);
                _loadedAssemblies.Add(refAssembly);
            }
            catch
            {
                // Referenced assembly may not be available; that's okay
            }
        }

        return assembly;
    }

    public List<DiscoveredMethodInfo> DiscoverMethods(Assembly assembly)
    {
        _discoveredMethods.Clear();

        foreach (var type in assembly.GetExportedTypes())
        {
            var typeAttributes = type.GetCustomAttributes(false)
                .Select(a => a.GetType().Name.Replace("Attribute", ""))
                .ToList();

            var methods = type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance |
                BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                // Skip property accessors, event methods, and compiler-generated methods
                if (method.IsSpecialName)
                    continue;

                var methodAttributes = method.GetCustomAttributes(false)
                    .Select(a => a.GetType().Name.Replace("Attribute", ""))
                    .ToList();

                var allAttributes = typeAttributes.Concat(methodAttributes).ToList();

                var info = new DiscoveredMethodInfo
                {
                    FullName = $"{type.FullName}.{method.Name}",
                    DeclaringType = type.FullName ?? type.Name,
                    ReturnType = FormatTypeName(method.ReturnType),
                    ParameterTypes = method.GetParameters()
                        .Select(p => FormatTypeName(p.ParameterType))
                        .ToList(),
                    Attributes = allAttributes
                };

                _discoveredMethods.Add(info);
            }
        }

        return _discoveredMethods.ToList();
    }

    public List<DiscoveredMethodInfo> GetDiscoveredMethods(string? namespaceFilter = null)
    {
        if (string.IsNullOrEmpty(namespaceFilter))
            return _discoveredMethods.ToList();

        return _discoveredMethods
            .Where(m => m.DeclaringType.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Returns methods that have WCF or Web API attributes — the most likely migration targets.
    /// </summary>
    public List<DiscoveredMethodInfo> GetServiceEndpoints()
    {
        return _discoveredMethods
            .Where(m => m.Attributes.Any(a => InterestingAttributes.Contains(a)))
            .ToList();
    }

    private static string FormatTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var baseName = type.Name[..type.Name.IndexOf('`')];
        var genericArgs = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
        return $"{baseName}<{genericArgs}>";
    }
}

using System.Reflection;
using System.Runtime.Loader;
using CodeMigrationTool.Agent.Instrumentation;
using CodeMigrationTool.Agent.Serialization;
using CodeMigrationTool.Shared.Models;
using Microsoft.Extensions.Logging;

namespace CodeMigrationTool.Agent.Services;

/// <summary>
/// In-process instrumentation service that loads a target assembly,
/// discovers methods, and provides execution tracing and state inspection.
/// Replaces the previous gRPC-based service with direct method calls.
/// </summary>
public class InstrumentationService : IDisposable
{
    private readonly ILogger<InstrumentationService> _logger;
    private readonly SafeObjectSerializer _serializer;
    private readonly ExecutionTracer _tracer;
    private readonly MethodDiscovery _discovery;
    private readonly HarmonyPatcher _patcher;
    private AssemblyLoadContext? _loadContext;
    private Assembly? _loadedAssembly;

    public InstrumentationService(ILogger<InstrumentationService> logger)
    {
        _logger = logger;
        _serializer = new SafeObjectSerializer();
        _tracer = new ExecutionTracer(_serializer);
        _discovery = new MethodDiscovery();
        _patcher = new HarmonyPatcher(_tracer);
    }

    public ExecutionTracer Tracer => _tracer;
    public MethodDiscovery Discovery => _discovery;
    public HarmonyPatcher Patcher => _patcher;
    public SafeObjectSerializer Serializer => _serializer;
    public Assembly? LoadedAssembly => _loadedAssembly;

    public List<DiscoveredMethodInfo> LoadApplication(string appPath, Dictionary<string, string>? envVars = null)
    {
        _logger.LogInformation("Loading application from {AppPath}", appPath);

        // Load in an isolated AssemblyLoadContext for unloadability
        _loadContext = new AssemblyLoadContext($"Sandbox-{Guid.NewGuid():N}", isCollectible: true);
        _loadedAssembly = _loadContext.LoadFromAssemblyPath(Path.GetFullPath(appPath));

        // Set environment variables
        if (envVars is not null)
        {
            foreach (var (key, value) in envVars)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        // Discover methods
        var methods = _discovery.DiscoverMethods(_loadedAssembly);

        _logger.LogInformation("Loaded {Count} methods from {Assembly}",
            methods.Count, _loadedAssembly.GetName().Name);

        return methods;
    }

    public async Task<ExecutionResult> ExecuteMethodAsync(
        string methodName, string argsJson, bool captureTrace)
    {
        return await _tracer.ExecuteAndTraceAsync(methodName, argsJson, captureTrace);
    }

    public List<CallFrameInfo> GetCallStack(string traceId)
    {
        return _tracer.GetCallStack(traceId);
    }

    public ObjectState InspectObject(string expression, int maxDepth)
    {
        return _tracer.InspectObject(expression, maxDepth);
    }

    public List<DiscoveredMethodInfo> ListMethods(string? namespaceFilter)
    {
        return _discovery.GetDiscoveredMethods(namespaceFilter);
    }

    public void Dispose()
    {
        _patcher.UnpatchAll();

        _loadedAssembly = null;
        _loadContext?.Unload();
        _loadContext = null;

        GC.SuppressFinalize(this);
    }
}

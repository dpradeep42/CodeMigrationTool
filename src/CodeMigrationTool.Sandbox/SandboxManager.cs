using System.Collections.Concurrent;
using CodeMigrationTool.Agent.Services;
using CodeMigrationTool.Sandbox.Models;
using Microsoft.Extensions.Logging;

namespace CodeMigrationTool.Sandbox;

/// <summary>
/// Manages in-process sandboxed runtime sessions.
/// Each sandbox loads the target assembly in an isolated AssemblyLoadContext
/// and provides an InstrumentationService for method execution and tracing.
/// </summary>
public class SandboxManager : IDisposable
{
    private readonly ILogger<SandboxManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, SandboxInfo> _sandboxes = new();

    public SandboxManager(ILogger<SandboxManager> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public SandboxInfo Create(
        string appPath,
        string runtime,
        Dictionary<string, string>? envVars = null)
    {
        if (runtime.ToLowerInvariant() != "dotnet")
            throw new ArgumentException($"Unsupported runtime: {runtime}. Only 'dotnet' is currently supported.");

        var sandboxId = Guid.NewGuid().ToString("N")[..12];
        _logger.LogInformation("Creating sandbox {SandboxId} for {AppPath}", sandboxId, appPath);

        var service = new InstrumentationService(
            _loggerFactory.CreateLogger<InstrumentationService>());

        var sandbox = new SandboxInfo
        {
            SandboxId = sandboxId,
            Runtime = runtime,
            AppPath = appPath,
            Service = service,
            Status = SandboxStatus.Creating
        };

        try
        {
            service.LoadApplication(appPath, envVars);
            sandbox.Status = SandboxStatus.Ready;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load application in sandbox {SandboxId}", sandboxId);
            sandbox.Status = SandboxStatus.Failed;
            service.Dispose();
            throw;
        }

        _sandboxes[sandboxId] = sandbox;
        _logger.LogInformation("Sandbox {SandboxId} is ready", sandboxId);

        return sandbox;
    }

    public SandboxInfo Get(string sandboxId)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out var sandbox))
            throw new KeyNotFoundException($"Sandbox not found: {sandboxId}");
        return sandbox;
    }

    public void Destroy(string sandboxId)
    {
        if (!_sandboxes.TryRemove(sandboxId, out var sandbox))
            throw new KeyNotFoundException($"Sandbox not found: {sandboxId}");

        _logger.LogInformation("Destroying sandbox {SandboxId}", sandboxId);

        sandbox.Service?.Dispose();
        sandbox.Service = null;
        sandbox.Status = SandboxStatus.Destroyed;
    }

    public IReadOnlyCollection<SandboxInfo> ListActive()
    {
        return _sandboxes.Values
            .Where(s => s.Status is SandboxStatus.Ready or SandboxStatus.Running)
            .ToList();
    }

    public void Dispose()
    {
        foreach (var sandbox in _sandboxes.Values)
        {
            sandbox.Service?.Dispose();
        }
        _sandboxes.Clear();
        GC.SuppressFinalize(this);
    }
}

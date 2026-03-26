using System.Collections.Concurrent;
using CodeMigrationTool.Sandbox.Models;
using Microsoft.Extensions.Logging;

namespace CodeMigrationTool.Sandbox;

/// <summary>
/// Maintains a pool of pre-loaded runtime hosts to reduce initialization latency.
/// Phase 2: Currently a stub.
/// </summary>
public class SandboxPool
{
    private readonly ILogger<SandboxPool> _logger;
    private readonly ConcurrentQueue<SandboxInfo> _warmPool = new();
    private readonly int _targetPoolSize;

    public SandboxPool(ILogger<SandboxPool> logger, int targetPoolSize = 2)
    {
        _logger = logger;
        _targetPoolSize = targetPoolSize;
    }

    public int AvailableCount => _warmPool.Count;

    public SandboxInfo? TryAcquire(string runtime)
    {
        if (_warmPool.TryDequeue(out var sandbox) && sandbox.Runtime == runtime)
        {
            _logger.LogInformation("Acquired warm sandbox {SandboxId}", sandbox.SandboxId);
            return sandbox;
        }
        return null;
    }

    public void Return(SandboxInfo sandbox)
    {
        if (_warmPool.Count < _targetPoolSize)
        {
            _warmPool.Enqueue(sandbox);
            _logger.LogInformation("Returned sandbox {SandboxId} to pool", sandbox.SandboxId);
        }
    }
}

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using CodeMigrationTool.Sandbox.Models;

namespace CodeMigrationTool.Sandbox;

/// <summary>
/// Maintains a pool of pre-warmed sandbox containers to reduce latency.
/// Phase 2: Currently a stub that will be implemented with warm container management.
/// </summary>
public class SandboxPool
{
    private readonly ILogger<SandboxPool> _logger;
    private readonly SandboxManager _sandboxManager;
    private readonly ConcurrentQueue<SandboxInfo> _warmPool = new();
    private readonly int _targetPoolSize;

    public SandboxPool(
        ILogger<SandboxPool> logger,
        SandboxManager sandboxManager,
        int targetPoolSize = 2)
    {
        _logger = logger;
        _sandboxManager = sandboxManager;
        _targetPoolSize = targetPoolSize;
    }

    public int AvailableCount => _warmPool.Count;

    /// <summary>
    /// Try to acquire a pre-warmed sandbox from the pool.
    /// Returns null if no warm sandboxes are available.
    /// </summary>
    public SandboxInfo? TryAcquire(string runtime)
    {
        // TODO: Phase 2 - implement warm pool with runtime-specific queues
        if (_warmPool.TryDequeue(out var sandbox) && sandbox.Runtime == runtime)
        {
            _logger.LogInformation("Acquired warm sandbox {SandboxId}", sandbox.SandboxId);
            return sandbox;
        }

        return null;
    }

    /// <summary>
    /// Return a sandbox to the pool instead of destroying it.
    /// </summary>
    public void Return(SandboxInfo sandbox)
    {
        // TODO: Phase 2 - reset sandbox state before returning to pool
        if (_warmPool.Count < _targetPoolSize)
        {
            _warmPool.Enqueue(sandbox);
            _logger.LogInformation("Returned sandbox {SandboxId} to pool", sandbox.SandboxId);
        }
    }

    /// <summary>
    /// Pre-warm the pool with the specified number of sandboxes.
    /// </summary>
    public async Task WarmUpAsync(string runtime, int count)
    {
        // TODO: Phase 2 - implement async warm-up
        _logger.LogInformation("Pool warm-up requested for {Count} {Runtime} sandboxes (not yet implemented)",
            count, runtime);
        await Task.CompletedTask;
    }
}

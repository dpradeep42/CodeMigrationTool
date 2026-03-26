using System.Collections.Concurrent;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using CodeMigrationTool.Sandbox.Models;

namespace CodeMigrationTool.Sandbox;

/// <summary>
/// Manages sandbox snapshots for deterministic replay.
/// Uses docker commit to capture container state and restores by creating
/// a new container from the committed image.
/// </summary>
public class SnapshotManager : IDisposable
{
    private readonly DockerClient _docker;
    private readonly ILogger<SnapshotManager> _logger;
    private readonly ConcurrentDictionary<string, SnapshotInfo> _snapshots = new();

    public SnapshotManager(ILogger<SnapshotManager> logger)
    {
        _docker = new DockerClientConfiguration().CreateClient();
        _logger = logger;
    }

    public async Task<SnapshotInfo> CreateSnapshotAsync(SandboxInfo sandbox, string? name = null)
    {
        var snapshotId = Guid.NewGuid().ToString("N")[..12];
        var imageName = $"cmt-snapshot-{snapshotId}";

        _logger.LogInformation(
            "Creating snapshot {SnapshotId} for sandbox {SandboxId}",
            snapshotId, sandbox.SandboxId);

        var commitResponse = await _docker.Images.CommitContainerChangesAsync(
            new CommitContainerChangesParameters
            {
                ContainerID = sandbox.ContainerId,
                RepositoryName = imageName,
                Tag = "latest",
                Comment = $"Snapshot {name ?? snapshotId} of sandbox {sandbox.SandboxId}"
            });

        var snapshot = new SnapshotInfo
        {
            SnapshotId = snapshotId,
            SandboxId = sandbox.SandboxId,
            Name = name,
            DockerImageId = commitResponse.ID
        };

        _snapshots[snapshotId] = snapshot;
        sandbox.SnapshotIds.Add(snapshotId);

        _logger.LogInformation("Snapshot {SnapshotId} created as image {ImageId}",
            snapshotId, commitResponse.ID);

        return snapshot;
    }

    public SnapshotInfo GetSnapshot(string snapshotId)
    {
        if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
            throw new KeyNotFoundException($"Snapshot not found: {snapshotId}");
        return snapshot;
    }

    public SnapshotInfo? GetLatestSnapshot(string sandboxId)
    {
        return _snapshots.Values
            .Where(s => s.SandboxId == sandboxId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();
    }

    public async Task DeleteSnapshotAsync(string snapshotId)
    {
        if (!_snapshots.TryRemove(snapshotId, out var snapshot))
            return;

        try
        {
            await _docker.Images.DeleteImageAsync($"cmt-snapshot-{snapshotId}:latest",
                new ImageDeleteParameters { Force = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete snapshot image for {SnapshotId}", snapshotId);
        }
    }

    public IReadOnlyCollection<SnapshotInfo> ListSnapshots(string sandboxId)
    {
        return _snapshots.Values
            .Where(s => s.SandboxId == sandboxId)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
    }

    public void Dispose()
    {
        _docker.Dispose();
        GC.SuppressFinalize(this);
    }
}

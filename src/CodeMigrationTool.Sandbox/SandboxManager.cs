using System.Collections.Concurrent;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using CodeMigrationTool.Sandbox.Models;

namespace CodeMigrationTool.Sandbox;

/// <summary>
/// Manages the lifecycle of sandboxed Docker containers for legacy application execution.
/// Each sandbox runs the Instrumentation Agent and the target application in isolation.
/// </summary>
public class SandboxManager : IDisposable
{
    private readonly DockerClient _docker;
    private readonly ILogger<SandboxManager> _logger;
    private readonly ConcurrentDictionary<string, SandboxInfo> _sandboxes = new();
    private readonly SnapshotManager _snapshotManager;

    private const string DefaultDotnetImage = "codemigrationtool-sandbox-dotnet:latest";
    private const int GrpcPortBase = 50100;
    private int _nextPort = GrpcPortBase;

    public SandboxManager(ILogger<SandboxManager> logger, SnapshotManager snapshotManager)
    {
        _docker = new DockerClientConfiguration().CreateClient();
        _logger = logger;
        _snapshotManager = snapshotManager;
    }

    public async Task<SandboxInfo> CreateAsync(
        string appPath,
        string runtime,
        Dictionary<string, string>? envVars = null)
    {
        var sandboxId = Guid.NewGuid().ToString("N")[..12];
        var grpcPort = Interlocked.Increment(ref _nextPort);

        var imageName = runtime.ToLowerInvariant() switch
        {
            "dotnet" => DefaultDotnetImage,
            _ => throw new ArgumentException($"Unsupported runtime: {runtime}")
        };

        _logger.LogInformation("Creating sandbox {SandboxId} with runtime {Runtime}", sandboxId, runtime);

        var envList = new List<string> { $"GRPC_PORT={grpcPort}" };
        if (envVars is not null)
        {
            envList.AddRange(envVars.Select(kv => $"{kv.Key}={kv.Value}"));
        }

        var createResponse = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = imageName,
            Name = $"cmt-sandbox-{sandboxId}",
            Env = envList,
            HostConfig = new HostConfig
            {
                // Security: no network access
                NetworkMode = "none",
                // Resource limits
                Memory = 512 * 1024 * 1024, // 512 MB
                NanoCPUs = 1_000_000_000,    // 1 CPU
                PidsLimit = 256,
                // Mount the app directory read-only
                Binds = [$"{Path.GetFullPath(appPath)}:/app:ro"],
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    [$"{grpcPort}/tcp"] = [new PortBinding { HostPort = grpcPort.ToString() }]
                },
                // Drop all capabilities, add back only what's needed
                CapDrop = ["ALL"],
            },
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                [$"{grpcPort}/tcp"] = default
            }
        });

        var sandbox = new SandboxInfo
        {
            SandboxId = sandboxId,
            ContainerId = createResponse.ID,
            Runtime = runtime,
            AppPath = appPath,
            GrpcPort = grpcPort,
            Status = SandboxStatus.Creating
        };

        _sandboxes[sandboxId] = sandbox;

        // Start the container
        await _docker.Containers.StartContainerAsync(createResponse.ID, new ContainerStartParameters());
        sandbox.Status = SandboxStatus.Ready;

        _logger.LogInformation("Sandbox {SandboxId} is ready on port {Port}", sandboxId, grpcPort);

        return sandbox;
    }

    public SandboxInfo Get(string sandboxId)
    {
        if (!_sandboxes.TryGetValue(sandboxId, out var sandbox))
            throw new KeyNotFoundException($"Sandbox not found: {sandboxId}");
        return sandbox;
    }

    public async Task DestroyAsync(string sandboxId)
    {
        if (!_sandboxes.TryRemove(sandboxId, out var sandbox))
            throw new KeyNotFoundException($"Sandbox not found: {sandboxId}");

        _logger.LogInformation("Destroying sandbox {SandboxId}", sandboxId);

        try
        {
            await _docker.Containers.StopContainerAsync(sandbox.ContainerId, new ContainerStopParameters
            {
                WaitBeforeKillSeconds = 5
            });
        }
        catch (DockerContainerNotFoundException)
        {
            // Already removed
        }

        try
        {
            await _docker.Containers.RemoveContainerAsync(sandbox.ContainerId, new ContainerRemoveParameters
            {
                Force = true
            });
        }
        catch (DockerContainerNotFoundException)
        {
            // Already removed
        }

        sandbox.Status = SandboxStatus.Destroyed;

        // Clean up associated snapshots
        foreach (var snapshotId in sandbox.SnapshotIds)
        {
            await _snapshotManager.DeleteSnapshotAsync(snapshotId);
        }
    }

    public IReadOnlyCollection<SandboxInfo> ListActive()
    {
        return _sandboxes.Values
            .Where(s => s.Status is SandboxStatus.Ready or SandboxStatus.Running)
            .ToList();
    }

    public void Dispose()
    {
        _docker.Dispose();
        GC.SuppressFinalize(this);
    }
}

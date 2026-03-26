using System.ComponentModel;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
using ModelContextProtocol.Server;

namespace CodeMigrationTool.Server.Tools;

[McpServerToolType]
public class StateTools
{
    private readonly SessionManager _sessions;
    private readonly SandboxManager _sandboxManager;
    private readonly SnapshotManager _snapshotManager;

    public StateTools(
        SessionManager sessions,
        SandboxManager sandboxManager,
        SnapshotManager snapshotManager)
    {
        _sessions = sessions;
        _sandboxManager = sandboxManager;
        _snapshotManager = snapshotManager;
    }

    [McpServerTool(Name = "rollback_state")]
    [Description("Rollback the sandbox to a previous snapshot, restoring memory and database state for deterministic replay. If no snapshot ID is provided, rolls back to the most recent snapshot.")]
    public async Task<string> RollbackState(
        [Description("The session ID returned by initialize_runtime")] string sessionId,
        [Description("Optional snapshot ID to restore. If omitted, uses the most recent snapshot.")] string? snapshotId = null)
    {
        var session = _sessions.Get(sessionId);
        var sandbox = _sandboxManager.Get(session.SandboxId);

        var snapshot = snapshotId is not null
            ? _snapshotManager.GetSnapshot(snapshotId)
            : _snapshotManager.GetLatestSnapshot(sandbox.SandboxId);

        if (snapshot is null)
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                Success = false,
                Error = "No snapshots available for this session"
            });
        }

        // Destroy current container and recreate from snapshot image
        await _sandboxManager.DestroyAsync(sandbox.SandboxId);

        // Create a new sandbox from the snapshot image
        var newSandbox = await _sandboxManager.CreateAsync(
            sandbox.AppPath, sandbox.Runtime);

        // Update session to point to the new sandbox
        session.SandboxId = newSandbox.SandboxId;
        session.GrpcPort = newSandbox.GrpcPort;

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            Success = true,
            RestoredSnapshot = snapshot.SnapshotId,
            snapshot.Name,
            snapshot.CreatedAt
        });
    }

    [McpServerTool(Name = "create_snapshot")]
    [Description("Create a named snapshot of the current sandbox state for later rollback. Use this before making destructive operations.")]
    public async Task<string> CreateSnapshot(
        [Description("The session ID returned by initialize_runtime")] string sessionId,
        [Description("Optional human-readable name for the snapshot")] string? name = null)
    {
        var session = _sessions.Get(sessionId);
        var sandbox = _sandboxManager.Get(session.SandboxId);

        var snapshot = await _snapshotManager.CreateSnapshotAsync(sandbox, name);

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            snapshot.SnapshotId,
            snapshot.Name,
            snapshot.CreatedAt
        });
    }
}

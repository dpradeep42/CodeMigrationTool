using System.ComponentModel;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
using ModelContextProtocol.Server;

namespace CodeMigrationTool.Server.Tools;

[McpServerToolType]
public class ExecutionTools
{
    private readonly SessionManager _sessions;
    private readonly SandboxManager _sandboxManager;
    private readonly SnapshotManager _snapshotManager;

    public ExecutionTools(
        SessionManager sessions,
        SandboxManager sandboxManager,
        SnapshotManager snapshotManager)
    {
        _sessions = sessions;
        _sandboxManager = sandboxManager;
        _snapshotManager = snapshotManager;
    }

    [McpServerTool(Name = "execute_and_trace")]
    [Description("Execute a specific method with arguments and capture the full execution trace including call stack, return value, timing, and side effects. Automatically creates a snapshot before execution for rollback.")]
    public async Task<string> ExecuteAndTrace(
        [Description("The session ID returned by initialize_runtime")] string sessionId,
        [Description("Fully qualified method name, e.g. 'MyNamespace.MyClass.MyMethod'")] string methodName,
        [Description("JSON-serialized arguments as array or object, e.g. '[1, \"hello\"]' or '{\"age\": 35}'")] string? argsJson = null,
        [Description("If true, creates a snapshot before execution for rollback")] bool autoSnapshot = true)
    {
        var session = _sessions.Get(sessionId);
        var sandbox = _sandboxManager.Get(session.SandboxId);
        var service = sandbox.Service
            ?? throw new InvalidOperationException("Sandbox service not available");

        // Auto-snapshot before execution for deterministic replay
        if (autoSnapshot)
        {
            _snapshotManager.CreateSnapshot(sandbox, $"pre-exec-{DateTime.UtcNow:HHmmss}");
        }

        var result = await service.ExecuteMethodAsync(methodName, argsJson ?? "", true);

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            result.TraceId,
            ReturnValue = result.ReturnValueJson,
            result.ExecutionTimeMs,
            result.SideEffects,
            Exception = result.ExceptionInfo,
            result.Success
        });
    }
}

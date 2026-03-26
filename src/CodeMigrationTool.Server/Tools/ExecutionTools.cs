using System.ComponentModel;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
using CodeMigrationTool.Shared.Protos;
using Grpc.Net.Client;
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

        // Auto-snapshot before execution for deterministic replay
        if (autoSnapshot)
        {
            await _snapshotManager.CreateSnapshotAsync(sandbox, $"pre-exec-{DateTime.UtcNow:HHmmss}");
        }

        using var channel = GrpcChannel.ForAddress($"http://localhost:{session.GrpcPort}");
        var client = new InstrumentationAgent.InstrumentationAgentClient(channel);

        var response = await client.ExecuteMethodAsync(new ExecuteRequest
        {
            MethodName = methodName,
            ArgsJson = argsJson ?? "",
            CaptureTrace = true
        });

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            response.TraceId,
            ReturnValue = string.IsNullOrEmpty(response.ReturnValueJson) ? null : response.ReturnValueJson,
            response.ExecutionTimeMs,
            SideEffects = response.SideEffects.ToList(),
            Exception = string.IsNullOrEmpty(response.ExceptionInfo) ? null : response.ExceptionInfo,
            Success = string.IsNullOrEmpty(response.ExceptionInfo)
        });
    }
}

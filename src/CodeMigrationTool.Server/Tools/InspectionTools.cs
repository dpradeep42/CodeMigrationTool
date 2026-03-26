using System.ComponentModel;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
using ModelContextProtocol.Server;

namespace CodeMigrationTool.Server.Tools;

[McpServerToolType]
public class InspectionTools
{
    private readonly SessionManager _sessions;
    private readonly SandboxManager _sandboxManager;

    public InspectionTools(SessionManager sessions, SandboxManager sandboxManager)
    {
        _sessions = sessions;
        _sandboxManager = sandboxManager;
    }

    [McpServerTool(Name = "get_call_stack")]
    [Description("Get the complete call stack from a previous traced execution, including hidden middleware, internal framework calls, and local variable values at each frame.")]
    public string GetCallStack(
        [Description("The session ID returned by initialize_runtime")] string sessionId,
        [Description("The trace ID returned by execute_and_trace")] string traceId)
    {
        var session = _sessions.Get(sessionId);
        var sandbox = _sandboxManager.Get(session.SandboxId);
        var service = sandbox.Service
            ?? throw new InvalidOperationException("Sandbox service not available");

        var frames = service.GetCallStack(traceId);

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            Frames = frames.Select(f => new
            {
                f.MethodSignature,
                f.DeclaringType,
                f.FilePath,
                f.LineNumber,
                Locals = f.LocalsJson
            })
        });
    }

    [McpServerTool(Name = "inspect_memory_state")]
    [Description("Inspect the runtime state of an object, variable, or static field. Returns a JSON serialization of the object with its current field values, handling circular references and deeply nested structures.")]
    public string InspectMemoryState(
        [Description("The session ID returned by initialize_runtime")] string sessionId,
        [Description("Expression to inspect, e.g. 'MyNamespace.MyClass.StaticField'")] string expression,
        [Description("Maximum serialization depth (default 3)")] int maxDepth = 3)
    {
        var session = _sessions.Get(sessionId);
        var sandbox = _sandboxManager.Get(session.SandboxId);
        var service = sandbox.Service
            ?? throw new InvalidOperationException("Sandbox service not available");

        var state = service.InspectObject(expression, maxDepth);

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            state.TypeName,
            Value = state.ValueJson
        });
    }

    [McpServerTool(Name = "list_methods")]
    [Description("List all discoverable methods in the loaded application, optionally filtered by namespace. Shows method signatures, return types, parameters, and WCF/Web API attributes.")]
    public string ListMethods(
        [Description("The session ID returned by initialize_runtime")] string sessionId,
        [Description("Optional namespace prefix to filter by, e.g. 'MyApp.Services'")] string? namespaceFilter = null)
    {
        var session = _sessions.Get(sessionId);
        var sandbox = _sandboxManager.Get(session.SandboxId);
        var service = sandbox.Service
            ?? throw new InvalidOperationException("Sandbox service not available");

        var methods = service.ListMethods(namespaceFilter);

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            Methods = methods.Select(m => new
            {
                m.FullName,
                m.DeclaringType,
                m.ReturnType,
                m.ParameterTypes,
                m.Attributes
            }),
            Count = methods.Count
        });
    }
}

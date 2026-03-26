using System.ComponentModel;
using CodeMigrationTool.Server.Sessions;
using CodeMigrationTool.Shared.Protos;
using Grpc.Net.Client;
using ModelContextProtocol.Server;

namespace CodeMigrationTool.Server.Tools;

[McpServerToolType]
public class InspectionTools
{
    private readonly SessionManager _sessions;

    public InspectionTools(SessionManager sessions)
    {
        _sessions = sessions;
    }

    [McpServerTool(Name = "get_call_stack")]
    [Description("Get the complete call stack from a previous traced execution, including hidden middleware, internal framework calls, and local variable values at each frame.")]
    public async Task<string> GetCallStack(
        [Description("The session ID returned by initialize_runtime")] string sessionId,
        [Description("The trace ID returned by execute_and_trace")] string traceId)
    {
        var session = _sessions.Get(sessionId);

        using var channel = GrpcChannel.ForAddress($"http://localhost:{session.GrpcPort}");
        var client = new InstrumentationAgent.InstrumentationAgentClient(channel);

        var response = await client.GetCallStackAsync(new CallStackRequest { TraceId = traceId });

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            Frames = response.Frames.Select(f => new
            {
                f.MethodSignature,
                f.DeclaringType,
                FilePath = string.IsNullOrEmpty(f.FilePath) ? null : f.FilePath,
                f.LineNumber,
                Locals = string.IsNullOrEmpty(f.LocalsJson) ? null : f.LocalsJson
            })
        });
    }

    [McpServerTool(Name = "inspect_memory_state")]
    [Description("Inspect the runtime state of an object, variable, or static field. Returns a JSON serialization of the object with its current field values, handling circular references and deeply nested structures.")]
    public async Task<string> InspectMemoryState(
        [Description("The session ID returned by initialize_runtime")] string sessionId,
        [Description("Expression to inspect, e.g. 'MyNamespace.MyClass.StaticField'")] string expression,
        [Description("Maximum serialization depth (default 3)")] int maxDepth = 3)
    {
        var session = _sessions.Get(sessionId);

        using var channel = GrpcChannel.ForAddress($"http://localhost:{session.GrpcPort}");
        var client = new InstrumentationAgent.InstrumentationAgentClient(channel);

        var response = await client.InspectObjectAsync(new InspectRequest
        {
            Expression = expression,
            MaxDepth = maxDepth
        });

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            response.TypeName,
            Value = response.ValueJson
        });
    }

    [McpServerTool(Name = "list_methods")]
    [Description("List all discoverable methods in the loaded application, optionally filtered by namespace. Shows method signatures, return types, parameters, and WCF/Web API attributes.")]
    public async Task<string> ListMethods(
        [Description("The session ID returned by initialize_runtime")] string sessionId,
        [Description("Optional namespace prefix to filter by, e.g. 'MyApp.Services'")] string? namespaceFilter = null)
    {
        var session = _sessions.Get(sessionId);

        using var channel = GrpcChannel.ForAddress($"http://localhost:{session.GrpcPort}");
        var client = new InstrumentationAgent.InstrumentationAgentClient(channel);

        var response = await client.ListMethodsAsync(new ListMethodsRequest
        {
            NamespaceFilter = namespaceFilter ?? ""
        });

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            Methods = response.Methods.Select(m => new
            {
                m.FullName,
                m.DeclaringType,
                m.ReturnType,
                ParameterTypes = m.ParameterTypes.ToList(),
                Attributes = m.Attributes.ToList()
            }),
            Count = response.Methods.Count
        });
    }
}

using System.ComponentModel;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
using CodeMigrationTool.Shared.Protos;
using Grpc.Net.Client;
using ModelContextProtocol.Server;

namespace CodeMigrationTool.Server.Tools;

[McpServerToolType]
public class RuntimeTools
{
    private readonly SessionManager _sessions;
    private readonly SandboxManager _sandboxManager;

    public RuntimeTools(SessionManager sessions, SandboxManager sandboxManager)
    {
        _sessions = sessions;
        _sandboxManager = sandboxManager;
    }

    [McpServerTool(Name = "initialize_runtime")]
    [Description("Boot a legacy .NET application in an isolated sandbox container. Returns a session ID and list of discovered methods.")]
    public async Task<string> InitializeRuntime(
        [Description("Path to the application assembly (.dll) to load")] string appPath,
        [Description("Runtime type: 'dotnet'")] string runtime = "dotnet",
        [Description("Environment variables as JSON object, e.g. {\"KEY\": \"VALUE\"}")] string? envVarsJson = null)
    {
        var envVars = string.IsNullOrEmpty(envVarsJson)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(envVarsJson);

        var sandbox = await _sandboxManager.CreateAsync(appPath, runtime, envVars);
        var session = _sessions.Create(sandbox.SandboxId, sandbox.GrpcPort);

        // Connect to the agent inside the sandbox and load the application
        using var channel = GrpcChannel.ForAddress($"http://localhost:{sandbox.GrpcPort}");
        var client = new InstrumentationAgent.InstrumentationAgentClient(channel);

        var loadResponse = await client.LoadApplicationAsync(new LoadRequest
        {
            AppPath = "/app",
            EnvVars = { envVars ?? new Dictionary<string, string>() }
        });

        if (!loadResponse.Success)
        {
            await _sandboxManager.DestroyAsync(sandbox.SandboxId);
            _sessions.Remove(session.SessionId);
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                Error = $"Failed to load application: {loadResponse.ErrorMessage}"
            });
        }

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            session.SessionId,
            Status = "ready",
            Methods = loadResponse.Methods.Select(m => new
            {
                m.FullName,
                m.DeclaringType,
                m.ReturnType,
                ParameterTypes = m.ParameterTypes.ToList(),
                Attributes = m.Attributes.ToList()
            })
        });
    }

    [McpServerTool(Name = "shutdown_runtime")]
    [Description("Shut down a sandbox session and clean up all resources.")]
    public async Task<string> ShutdownRuntime(
        [Description("The session ID returned by initialize_runtime")] string sessionId)
    {
        var session = _sessions.Get(sessionId);
        await _sandboxManager.DestroyAsync(session.SandboxId);
        _sessions.Remove(sessionId);

        return System.Text.Json.JsonSerializer.Serialize(new { Success = true });
    }
}

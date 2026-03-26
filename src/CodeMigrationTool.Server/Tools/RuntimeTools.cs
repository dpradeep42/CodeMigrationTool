using System.ComponentModel;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
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
    [Description("Boot a legacy .NET application in an isolated in-process sandbox. Returns a session ID and list of discovered methods.")]
    public string InitializeRuntime(
        [Description("Path to the application assembly (.dll) to load")] string appPath,
        [Description("Runtime type: 'dotnet'")] string runtime = "dotnet",
        [Description("Environment variables as JSON object, e.g. {\"KEY\": \"VALUE\"}")] string? envVarsJson = null)
    {
        var envVars = string.IsNullOrEmpty(envVarsJson)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(envVarsJson);

        var sandbox = _sandboxManager.Create(appPath, runtime, envVars);
        var session = _sessions.Create(sandbox.SandboxId);

        var methods = sandbox.Service!.ListMethods(null);

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            session.SessionId,
            Status = "ready",
            Methods = methods.Select(m => new
            {
                m.FullName,
                m.DeclaringType,
                m.ReturnType,
                m.ParameterTypes,
                m.Attributes
            })
        });
    }

    [McpServerTool(Name = "shutdown_runtime")]
    [Description("Shut down a sandbox session and clean up all resources.")]
    public string ShutdownRuntime(
        [Description("The session ID returned by initialize_runtime")] string sessionId)
    {
        var session = _sessions.Get(sessionId);
        _sandboxManager.Destroy(session.SandboxId);
        _sessions.Remove(sessionId);

        return System.Text.Json.JsonSerializer.Serialize(new { Success = true });
    }
}

using System.Text.Json;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
using CodeMigrationTool.Server.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using SampleWcfService;
using Xunit;

namespace CodeMigrationTool.Server.Tests.Tools;

public class RuntimeToolsTests : IDisposable
{
    private readonly SessionManager _sessions = new();
    private readonly SandboxManager _sandboxManager;
    private readonly RuntimeTools _tools;

    public RuntimeToolsTests()
    {
        _sandboxManager = new SandboxManager(
            NullLogger<SandboxManager>.Instance,
            NullLoggerFactory.Instance);
        _tools = new RuntimeTools(_sessions, _sandboxManager);
    }

    public void Dispose()
    {
        _sandboxManager.Dispose();
    }

    [Fact]
    public void InitializeRuntime_ValidAssembly_ReturnsSessionAndMethods()
    {
        var result = _tools.InitializeRuntime(
            typeof(CalculatorService).Assembly.Location);

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("SessionId", out var sessionId));
        Assert.False(string.IsNullOrEmpty(sessionId.GetString()));
        Assert.True(json.RootElement.TryGetProperty("Methods", out var methods));
        Assert.True(methods.GetArrayLength() > 0);

        // Verify Add method is discovered
        var methodsText = result;
        Assert.Contains("Add", methodsText);
    }

    [Fact]
    public void InitializeRuntime_InvalidRuntime_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => _tools.InitializeRuntime(
                typeof(CalculatorService).Assembly.Location, "python"));
    }

    [Fact]
    public void ShutdownRuntime_ValidSession_ReturnsSuccess()
    {
        var initResult = _tools.InitializeRuntime(
            typeof(CalculatorService).Assembly.Location);
        var sessionId = JsonDocument.Parse(initResult)
            .RootElement.GetProperty("SessionId").GetString()!;

        var result = _tools.ShutdownRuntime(sessionId);

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("Success").GetBoolean());
    }

    [Fact]
    public void ShutdownRuntime_InvalidSession_Throws()
    {
        Assert.Throws<KeyNotFoundException>(
            () => _tools.ShutdownRuntime("nonexistent"));
    }
}

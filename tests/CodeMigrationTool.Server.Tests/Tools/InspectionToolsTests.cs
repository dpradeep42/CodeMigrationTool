using System.Text.Json;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
using CodeMigrationTool.Server.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using SampleWcfService;
using Xunit;

namespace CodeMigrationTool.Server.Tests.Tools;

public class InspectionToolsTests : IDisposable
{
    private readonly SessionManager _sessions = new();
    private readonly SandboxManager _sandboxManager;
    private readonly InspectionTools _tools;
    private readonly string _sessionId;

    public InspectionToolsTests()
    {
        _sandboxManager = new SandboxManager(
            NullLogger<SandboxManager>.Instance,
            NullLoggerFactory.Instance);

        _tools = new InspectionTools(_sessions, _sandboxManager);

        CalculatorService.ResetState();
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");
        var session = _sessions.Create(sandbox.SandboxId);
        _sessionId = session.SessionId;
    }

    public void Dispose()
    {
        _sandboxManager.Dispose();
    }

    [Fact]
    public void ListMethods_ReturnsAllMethods()
    {
        var result = _tools.ListMethods(_sessionId);

        var json = JsonDocument.Parse(result);
        var count = json.RootElement.GetProperty("Count").GetInt32();
        Assert.True(count > 0);
        Assert.Contains("Add", result);
    }

    [Fact]
    public void ListMethods_WithNamespaceFilter_Filters()
    {
        var result = _tools.ListMethods(_sessionId, "SampleWcfService");

        var json = JsonDocument.Parse(result);
        var count = json.RootElement.GetProperty("Count").GetInt32();
        Assert.True(count > 0);

        // All methods should be from SampleWcfService namespace
        var methods = json.RootElement.GetProperty("Methods");
        foreach (var method in methods.EnumerateArray())
        {
            Assert.StartsWith("SampleWcfService",
                method.GetProperty("DeclaringType").GetString());
        }
    }

    [Fact]
    public async Task GetCallStack_AfterExecution_ReturnsFrames()
    {
        // Execute a method first to generate a trace
        var sandbox = _sandboxManager.Get(
            _sessions.Get(_sessionId).SandboxId);
        var execResult = await sandbox.Service!.ExecuteMethodAsync(
            "SampleWcfService.CalculatorService.Add", "[1, 2]", true);

        var result = _tools.GetCallStack(_sessionId, execResult.TraceId);
        var json = JsonDocument.Parse(result);

        Assert.True(json.RootElement.TryGetProperty("Frames", out _));
    }

    [Fact]
    public void InspectMemoryState_StaticField_ReturnsValue()
    {
        var result = _tools.InspectMemoryState(
            _sessionId, "SampleWcfService.CalculatorService.CallCount");

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("TypeName", out var typeName));
        Assert.Contains("Int32", typeName.GetString());
        Assert.True(json.RootElement.TryGetProperty("Value", out _));
    }
}

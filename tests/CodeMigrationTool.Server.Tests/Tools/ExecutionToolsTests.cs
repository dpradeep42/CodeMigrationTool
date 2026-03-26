using System.Text.Json;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
using CodeMigrationTool.Server.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using SampleWcfService;
using Xunit;

namespace CodeMigrationTool.Server.Tests.Tools;

public class ExecutionToolsTests : IDisposable
{
    private readonly SessionManager _sessions = new();
    private readonly SandboxManager _sandboxManager;
    private readonly SnapshotManager _snapshotManager;
    private readonly ExecutionTools _tools;
    private readonly string _sessionId;
    private readonly string _sandboxId;

    public ExecutionToolsTests()
    {
        _sandboxManager = new SandboxManager(
            NullLogger<SandboxManager>.Instance,
            NullLoggerFactory.Instance);
        _snapshotManager = new SnapshotManager(NullLogger<SnapshotManager>.Instance);

        _tools = new ExecutionTools(_sessions, _sandboxManager, _snapshotManager);

        // Initialize a sandbox for all tests
        CalculatorService.ResetState();
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");
        _sandboxId = sandbox.SandboxId;
        var session = _sessions.Create(sandbox.SandboxId);
        _sessionId = session.SessionId;
    }

    public void Dispose()
    {
        _sandboxManager.Dispose();
    }

    [Fact]
    public async Task ExecuteAndTrace_SimpleMethod_ReturnsResult()
    {
        var result = await _tools.ExecuteAndTrace(
            _sessionId,
            "SampleWcfService.CalculatorService.Add",
            "[5, 3]",
            autoSnapshot: false);

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("Success").GetBoolean());
        Assert.Contains("8", json.RootElement.GetProperty("ReturnValue").GetString());
    }

    [Fact]
    public async Task ExecuteAndTrace_WithAutoSnapshot_CreatesSnapshot()
    {
        await _tools.ExecuteAndTrace(
            _sessionId,
            "SampleWcfService.CalculatorService.Add",
            "[1, 1]",
            autoSnapshot: true);

        var snapshots = _snapshotManager.ListSnapshots(_sandboxId);
        Assert.NotEmpty(snapshots);
    }

    [Fact]
    public async Task ExecuteAndTrace_AutoSnapshotFalse_NoSnapshot()
    {
        await _tools.ExecuteAndTrace(
            _sessionId,
            "SampleWcfService.CalculatorService.Add",
            "[1, 1]",
            autoSnapshot: false);

        var snapshots = _snapshotManager.ListSnapshots(_sandboxId);
        Assert.Empty(snapshots);
    }

    [Fact]
    public async Task ExecuteAndTrace_InvalidMethod_ReturnsError()
    {
        var result = await _tools.ExecuteAndTrace(
            _sessionId,
            "SampleWcfService.CalculatorService.DoesNotExist",
            "[]",
            autoSnapshot: false);

        var json = JsonDocument.Parse(result);
        Assert.False(json.RootElement.GetProperty("Success").GetBoolean());
    }
}

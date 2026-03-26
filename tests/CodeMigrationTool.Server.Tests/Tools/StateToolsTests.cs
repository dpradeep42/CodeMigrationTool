using System.Text.Json;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
using CodeMigrationTool.Server.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using SampleWcfService;
using Xunit;

namespace CodeMigrationTool.Server.Tests.Tools;

public class StateToolsTests : IDisposable
{
    private readonly SessionManager _sessions = new();
    private readonly SandboxManager _sandboxManager;
    private readonly SnapshotManager _snapshotManager;
    private readonly StateTools _tools;
    private readonly ExecutionTools _execTools;
    private readonly string _sessionId;

    public StateToolsTests()
    {
        _sandboxManager = new SandboxManager(
            NullLogger<SandboxManager>.Instance,
            NullLoggerFactory.Instance);
        _snapshotManager = new SnapshotManager(NullLogger<SnapshotManager>.Instance);

        _tools = new StateTools(_sessions, _sandboxManager, _snapshotManager);
        _execTools = new ExecutionTools(_sessions, _sandboxManager, _snapshotManager);

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
    public void CreateSnapshot_ReturnsSnapshotInfo()
    {
        var result = _tools.CreateSnapshot(_sessionId, "test-snap");

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("SnapshotId", out var snapId));
        Assert.False(string.IsNullOrEmpty(snapId.GetString()));
        Assert.Equal("test-snap",
            json.RootElement.GetProperty("Name").GetString());
    }

    [Fact]
    public void RollbackState_WithSnapshotId_RestoresState()
    {
        // Create snapshot at clean state
        var snapResult = _tools.CreateSnapshot(_sessionId, "clean");
        var snapId = JsonDocument.Parse(snapResult)
            .RootElement.GetProperty("SnapshotId").GetString()!;

        // Mutate state on the sandbox's loaded assembly directly
        var sandbox = _sandboxManager.Get(_sessions.Get(_sessionId).SandboxId);
        var calcType = sandbox.Service!.LoadedAssembly!
            .GetType("SampleWcfService.CalculatorService")!;
        var instance = Activator.CreateInstance(calcType)!;
        calcType.GetMethod("Add")!.Invoke(instance, new object[] { 1m, 1m });

        // Rollback
        var result = _tools.RollbackState(_sessionId, snapId);
        var json = JsonDocument.Parse(result);

        Assert.True(json.RootElement.GetProperty("Success").GetBoolean());
        Assert.Equal("clean",
            json.RootElement.GetProperty("Name").GetString());
    }

    [Fact]
    public void RollbackState_NoSnapshotId_UsesLatest()
    {
        _tools.CreateSnapshot(_sessionId, "latest-snap");

        var result = _tools.RollbackState(_sessionId);

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("Success").GetBoolean());
    }

    [Fact]
    public void RollbackState_NoSnapshots_ReturnsError()
    {
        var result = _tools.RollbackState(_sessionId);

        var json = JsonDocument.Parse(result);
        Assert.False(json.RootElement.GetProperty("Success").GetBoolean());
    }
}

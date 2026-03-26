using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Sandbox.Models;
using Microsoft.Extensions.Logging.Abstractions;
using SampleWcfService;
using Xunit;

namespace CodeMigrationTool.Integration.Tests;

public class SnapshotManagerTests : IDisposable
{
    private readonly SandboxManager _sandboxManager;
    private readonly SnapshotManager _snapshotManager;

    public SnapshotManagerTests()
    {
        _sandboxManager = new SandboxManager(
            NullLogger<SandboxManager>.Instance,
            NullLoggerFactory.Instance);
        _snapshotManager = new SnapshotManager(NullLogger<SnapshotManager>.Instance);
        CalculatorService.ResetState();
    }

    public void Dispose()
    {
        _sandboxManager.Dispose();
    }

    [Fact]
    public void CreateSnapshot_ReturnsSnapshotInfo_WithSerializedState()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");

        var snapshot = _snapshotManager.CreateSnapshot(sandbox, "test-snap");

        Assert.NotNull(snapshot.SnapshotId);
        Assert.Equal(sandbox.SandboxId, snapshot.SandboxId);
        Assert.Equal("test-snap", snapshot.Name);
        Assert.False(string.IsNullOrEmpty(snapshot.SerializedState));
        Assert.Contains(snapshot.SnapshotId, sandbox.SnapshotIds);
    }

    [Fact]
    public void CreateSnapshot_SkipsConstants_CapturesMutableFields()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");

        var snapshot = _snapshotManager.CreateSnapshot(sandbox);

        // s_callCount is a mutable static field — should be captured
        Assert.Contains("s_callCount", snapshot.SerializedState);
        // s_tierMultipliers is static readonly (not const/literal) — should be captured
        Assert.Contains("s_tierMultipliers", snapshot.SerializedState);
    }

    [Fact]
    public void CreateSnapshot_NoAssemblyLoaded_Throws()
    {
        var sandbox = new SandboxInfo
        {
            SandboxId = "test-sandbox",
            Runtime = "dotnet",
            AppPath = "/fake/path.dll",
            Service = null
        };

        Assert.Throws<InvalidOperationException>(
            () => _snapshotManager.CreateSnapshot(sandbox));
    }

    [Fact]
    public void GetSnapshot_NonExistent_ThrowsKeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(
            () => _snapshotManager.GetSnapshot("nonexistent"));
    }

    [Fact]
    public void GetLatestSnapshot_NoSnapshots_ReturnsNull()
    {
        var result = _snapshotManager.GetLatestSnapshot("no-such-sandbox");
        Assert.Null(result);
    }

    [Fact]
    public void DeleteSnapshot_RemovesFromStore()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");

        var snapshot = _snapshotManager.CreateSnapshot(sandbox, "to-delete");
        _snapshotManager.DeleteSnapshot(snapshot.SnapshotId);

        Assert.Throws<KeyNotFoundException>(
            () => _snapshotManager.GetSnapshot(snapshot.SnapshotId));
    }

    [Fact]
    public void ListSnapshots_FiltersBySandboxId()
    {
        var sandbox1 = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");
        var sandbox2 = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");

        _snapshotManager.CreateSnapshot(sandbox1, "s1-snap");
        _snapshotManager.CreateSnapshot(sandbox2, "s2-snap");

        var s1Snapshots = _snapshotManager.ListSnapshots(sandbox1.SandboxId);
        Assert.Single(s1Snapshots);
        Assert.All(s1Snapshots, s => Assert.Equal(sandbox1.SandboxId, s.SandboxId));
    }

    [Fact]
    public void RestoreSnapshot_SkipsCaptureFailedMarkers()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");

        // Create a snapshot with a manually crafted failed-capture marker
        var snapshot = new SnapshotInfo
        {
            SnapshotId = "test-failed",
            SandboxId = sandbox.SandboxId,
            Name = "failed-marker-test",
            SerializedState = "{\"SampleWcfService.CalculatorService\":{\"s_callCount\":\"0\",\"s_tierMultipliers\":\"\\\"<capture failed: some error>\\\"\"}}"
        };

        // Should not throw — fields with capture-failed markers are skipped
        _snapshotManager.RestoreSnapshot(sandbox, snapshot);
        Assert.Equal(0, CalculatorService.CallCount);
    }
}

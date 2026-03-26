using System.Reflection;
using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Sandbox.Models;
using Microsoft.Extensions.Logging.Abstractions;
using SampleWcfService;
using Xunit;

namespace CodeMigrationTool.Integration.Tests;

/// <summary>
/// Tests the flagship snapshot → execute → rollback workflow
/// that enables deterministic replay of legacy application state.
///
/// Note: The sandbox loads the assembly in a separate AssemblyLoadContext,
/// so we must read state via reflection on the sandbox's loaded assembly
/// rather than directly via CalculatorService.CallCount (which reads the test's copy).
/// </summary>
public class SnapshotRollbackTests : IDisposable
{
    private readonly SandboxManager _sandboxManager;
    private readonly SnapshotManager _snapshotManager;

    public SnapshotRollbackTests()
    {
        _sandboxManager = new SandboxManager(
            NullLogger<SandboxManager>.Instance,
            NullLoggerFactory.Instance);
        _snapshotManager = new SnapshotManager(NullLogger<SnapshotManager>.Instance);
    }

    public void Dispose()
    {
        _sandboxManager.Dispose();
    }

    /// <summary>
    /// Reads the static s_callCount field from the sandbox's loaded assembly via reflection.
    /// </summary>
    private static int GetCallCount(SandboxInfo sandbox)
    {
        var type = sandbox.Service!.LoadedAssembly!
            .GetType("SampleWcfService.CalculatorService")!;
        var field = type.GetField("s_callCount",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (int)field.GetValue(null)!;
    }

    /// <summary>
    /// Resets the static s_callCount field on the sandbox's loaded assembly.
    /// </summary>
    private static void ResetCallCount(SandboxInfo sandbox)
    {
        var type = sandbox.Service!.LoadedAssembly!
            .GetType("SampleWcfService.CalculatorService")!;
        var method = type.GetMethod("ResetState",
            BindingFlags.Static | BindingFlags.Public)!;
        method.Invoke(null, null);
    }

    /// <summary>
    /// Executes Add on the sandbox's loaded assembly to increment callCount.
    /// </summary>
    private static void ExecuteAdd(SandboxInfo sandbox, decimal a, decimal b)
    {
        var type = sandbox.Service!.LoadedAssembly!
            .GetType("SampleWcfService.CalculatorService")!;
        var instance = Activator.CreateInstance(type)!;
        var method = type.GetMethod("Add")!;
        method.Invoke(instance, new object[] { a, b });
    }

    [Fact]
    public void Snapshot_ThenExecute_ThenRollback_RestoresStaticState()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");
        ResetCallCount(sandbox);

        // Snapshot at callCount=0
        var snapshot = _snapshotManager.CreateSnapshot(sandbox, "before-calls");
        Assert.Equal(0, GetCallCount(sandbox));

        // Execute Add several times to mutate state
        ExecuteAdd(sandbox, 1, 2);
        ExecuteAdd(sandbox, 3, 4);
        ExecuteAdd(sandbox, 5, 6);
        Assert.Equal(3, GetCallCount(sandbox));

        // Rollback
        _snapshotManager.RestoreSnapshot(sandbox, snapshot);
        Assert.Equal(0, GetCallCount(sandbox));
    }

    [Fact]
    public void MultipleSnapshots_RollbackToSpecific_RestoresCorrectState()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");
        ResetCallCount(sandbox);

        // Snapshot A at callCount=0
        var snapshotA = _snapshotManager.CreateSnapshot(sandbox, "snapshot-A");

        // Execute 3 times → callCount=3
        for (int i = 0; i < 3; i++)
            ExecuteAdd(sandbox, 1, 1);
        Assert.Equal(3, GetCallCount(sandbox));

        // Snapshot B at callCount=3
        var snapshotB = _snapshotManager.CreateSnapshot(sandbox, "snapshot-B");

        // Execute 2 more → callCount=5
        for (int i = 0; i < 2; i++)
            ExecuteAdd(sandbox, 1, 1);
        Assert.Equal(5, GetCallCount(sandbox));

        // Rollback to A → callCount=0
        _snapshotManager.RestoreSnapshot(sandbox, snapshotA);
        Assert.Equal(0, GetCallCount(sandbox));

        // Rollback to B → callCount=3
        _snapshotManager.RestoreSnapshot(sandbox, snapshotB);
        Assert.Equal(3, GetCallCount(sandbox));
    }

    [Fact]
    public void RollbackToLatest_WhenNoSnapshotIdProvided_ReturnsNewest()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");

        var first = _snapshotManager.CreateSnapshot(sandbox, "first");
        var second = _snapshotManager.CreateSnapshot(sandbox, "second");

        var latest = _snapshotManager.GetLatestSnapshot(sandbox.SandboxId);
        Assert.NotNull(latest);
        Assert.Equal(second.SnapshotId, latest!.SnapshotId);
    }

    [Fact]
    public void Snapshot_CapturesDictionaryState()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");
        ResetCallCount(sandbox);

        var snapshot = _snapshotManager.CreateSnapshot(sandbox, "with-dict");

        // The serialized state should contain the tier multipliers dictionary
        Assert.Contains("s_tierMultipliers", snapshot.SerializedState);
        Assert.Contains("s_callCount", snapshot.SerializedState);

        // Execute to mutate state, then rollback and verify
        ExecuteAdd(sandbox, 1, 1);
        Assert.Equal(1, GetCallCount(sandbox));

        _snapshotManager.RestoreSnapshot(sandbox, snapshot);
        Assert.Equal(0, GetCallCount(sandbox));
    }

    [Fact]
    public void Snapshot_EnvironmentVariables_NotRestoredByRollback()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");

        Environment.SetEnvironmentVariable("SNAPSHOT_TEST_VAR", "original");
        _snapshotManager.CreateSnapshot(sandbox, "env-snap");

        Environment.SetEnvironmentVariable("SNAPSHOT_TEST_VAR", "modified");

        var snapshot = _snapshotManager.GetLatestSnapshot(sandbox.SandboxId)!;
        _snapshotManager.RestoreSnapshot(sandbox, snapshot);

        // Environment variables are process-wide, NOT captured by snapshots
        // This documents the known limitation
        Assert.Equal("modified", Environment.GetEnvironmentVariable("SNAPSHOT_TEST_VAR"));

        // Clean up
        Environment.SetEnvironmentVariable("SNAPSHOT_TEST_VAR", null);
    }
}

using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Sandbox.Models;
using Microsoft.Extensions.Logging.Abstractions;
using SampleWcfService;
using Xunit;

namespace CodeMigrationTool.Integration.Tests;

public class SandboxManagerTests : IDisposable
{
    private readonly SandboxManager _sandboxManager;

    public SandboxManagerTests()
    {
        _sandboxManager = new SandboxManager(
            NullLogger<SandboxManager>.Instance,
            NullLoggerFactory.Instance);
    }

    public void Dispose()
    {
        _sandboxManager.Dispose();
    }

    [Fact]
    public void Create_ValidAssembly_ReturnsSandboxWithReadyStatus()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");

        Assert.NotNull(sandbox.SandboxId);
        Assert.Equal(SandboxStatus.Ready, sandbox.Status);
        Assert.NotNull(sandbox.Service);
        Assert.NotNull(sandbox.Service!.LoadedAssembly);
    }

    [Fact]
    public void Create_UnsupportedRuntime_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => _sandboxManager.Create(
                typeof(CalculatorService).Assembly.Location, "java"));

        Assert.Contains("Unsupported runtime", ex.Message);
    }

    [Fact]
    public void Create_InvalidPath_Throws()
    {
        Assert.ThrowsAny<Exception>(
            () => _sandboxManager.Create("/nonexistent/path.dll", "dotnet"));
    }

    [Fact]
    public void Get_ExistingSandbox_ReturnsSandbox()
    {
        var created = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");

        var retrieved = _sandboxManager.Get(created.SandboxId);

        Assert.Equal(created.SandboxId, retrieved.SandboxId);
    }

    [Fact]
    public void Get_NonExistent_ThrowsKeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(
            () => _sandboxManager.Get("nonexistent"));
    }

    [Fact]
    public void Destroy_DisposesServiceAndRemoves()
    {
        var sandbox = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");
        var sandboxId = sandbox.SandboxId;

        _sandboxManager.Destroy(sandboxId);

        Assert.Null(sandbox.Service);
        Assert.Equal(SandboxStatus.Destroyed, sandbox.Status);
        Assert.Throws<KeyNotFoundException>(() => _sandboxManager.Get(sandboxId));
    }

    [Fact]
    public void Destroy_NonExistent_ThrowsKeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(
            () => _sandboxManager.Destroy("nonexistent"));
    }

    [Fact]
    public void ListActive_FiltersDestroyedSandboxes()
    {
        var sandbox1 = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");
        var sandbox2 = _sandboxManager.Create(
            typeof(CalculatorService).Assembly.Location, "dotnet");

        _sandboxManager.Destroy(sandbox1.SandboxId);

        var active = _sandboxManager.ListActive();
        Assert.Single(active);
        Assert.Equal(sandbox2.SandboxId, active.First().SandboxId);
    }
}

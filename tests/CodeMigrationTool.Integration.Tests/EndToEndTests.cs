using CodeMigrationTool.Agent.Instrumentation;
using CodeMigrationTool.Agent.Serialization;
using SampleWcfService;

namespace CodeMigrationTool.Integration.Tests;

/// <summary>
/// Integration tests that verify the full instrumentation pipeline
/// without Docker (agent-level tests running in-process).
/// Full Docker-based tests will be added in Phase 2 with Testcontainers.
/// </summary>
public class EndToEndTests
{
    [Fact]
    public void MethodDiscovery_LoadsSampleAssembly_FindsServiceMethods()
    {
        var discovery = new MethodDiscovery();
        var assembly = typeof(CalculatorService).Assembly;

        var methods = discovery.DiscoverMethods(assembly);

        Assert.NotEmpty(methods);

        var addMethod = methods.FirstOrDefault(m => m.FullName.Contains("Add"));
        Assert.NotNull(addMethod);
        Assert.Equal("Decimal", addMethod.ReturnType);
        Assert.Contains("ServiceContract", addMethod.Attributes);
    }

    [Fact]
    public void MethodDiscovery_FindsServiceEndpoints()
    {
        var discovery = new MethodDiscovery();
        discovery.DiscoverMethods(typeof(CalculatorService).Assembly);

        var endpoints = discovery.GetServiceEndpoints();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e =>
            Assert.True(e.Attributes.Any(a =>
                a is "ServiceContract" or "OperationContract")));
    }

    [Fact]
    public void MethodDiscovery_FilterByNamespace_FiltersCorrectly()
    {
        var discovery = new MethodDiscovery();
        discovery.DiscoverMethods(typeof(CalculatorService).Assembly);

        var filtered = discovery.GetDiscoveredMethods("SampleWcfService");
        Assert.NotEmpty(filtered);

        var noResults = discovery.GetDiscoveredMethods("NonExistent.Namespace");
        Assert.Empty(noResults);
    }

    [Fact]
    public async Task FullPipeline_ExecuteMethod_GetCallStack()
    {
        var serializer = new SafeObjectSerializer();
        var tracer = new ExecutionTracer(serializer);
        var patcher = new HarmonyPatcher(tracer);

        // Ensure assembly is loaded
        _ = typeof(CalculatorService);
        CalculatorService.ResetState();

        // Patch methods to capture trace
        patcher.PatchAllMethodsInType(typeof(CalculatorService));

        try
        {
            // Execute and trace
            var result = await tracer.ExecuteAndTraceAsync(
                "SampleWcfService.CalculatorService.CalculatePremium",
                "[35, \"Gold\"]",
                captureTrace: true);

            Assert.True(result.Success, $"Execution failed: {result.ExceptionInfo}");
            Assert.NotNull(result.ReturnValueJson);

            // The premium for age 35, Gold tier should be 350 * 0.75 = 262.5
            Assert.Contains("262.5", result.ReturnValueJson);

            // Verify we can retrieve the call stack
            var callStack = tracer.GetCallStack(result.TraceId);
            // Call stack may have entries from Harmony hooks
            Assert.NotNull(callStack);
        }
        finally
        {
            patcher.UnpatchAll();
        }
    }

    [Fact]
    public async Task FullPipeline_CircularReference_SerializesCleanly()
    {
        var serializer = new SafeObjectSerializer();
        var tracer = new ExecutionTracer(serializer);

        _ = typeof(CalculatorService);

        var result = await tracer.ExecuteAndTraceAsync(
            "SampleWcfService.CalculatorService.GetPricingMatrix",
            "[\"Gold\"]",
            captureTrace: true);

        Assert.True(result.Success, $"Execution failed: {result.ExceptionInfo}");
        Assert.NotNull(result.ReturnValueJson);
        Assert.Contains("Gold", result.ReturnValueJson);
        // Circular ref should be handled via $id/$ref, not throw
    }

    [Fact]
    public async Task FullPipeline_EnvironmentVariable_AffectsOutput()
    {
        var serializer = new SafeObjectSerializer();
        var tracer = new ExecutionTracer(serializer);

        _ = typeof(CalculatorService);
        CalculatorService.ResetState();

        // Set environment variable that changes behavior
        Environment.SetEnvironmentVariable("PREMIUM_OVERRIDE_RATE", "100");

        try
        {
            var result = await tracer.ExecuteAndTraceAsync(
                "SampleWcfService.CalculatorService.CalculatePremium",
                "[35, \"Gold\"]",
                captureTrace: true);

            Assert.True(result.Success);
            // With override rate 100, result should be 100 * 35 = 3500
            Assert.Contains("3500", result.ReturnValueJson!);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PREMIUM_OVERRIDE_RATE", null);
        }
    }
}

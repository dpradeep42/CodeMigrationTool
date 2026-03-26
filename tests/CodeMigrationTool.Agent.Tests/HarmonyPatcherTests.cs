using CodeMigrationTool.Agent.Instrumentation;
using CodeMigrationTool.Agent.Serialization;
using SampleWcfService;

namespace CodeMigrationTool.Agent.Tests;

public class HarmonyPatcherTests
{
    [Fact]
    public void PatchMethod_ValidMethod_DoesNotThrow()
    {
        var serializer = new SafeObjectSerializer();
        var tracer = new ExecutionTracer(serializer);
        var patcher = new HarmonyPatcher(tracer);

        var method = typeof(CalculatorService).GetMethod(nameof(CalculatorService.Add))!;

        patcher.PatchMethod(method);
        patcher.UnpatchAll();
    }

    [Fact]
    public void PatchAllMethodsInType_SampleService_PatchesWithoutError()
    {
        var serializer = new SafeObjectSerializer();
        var tracer = new ExecutionTracer(serializer);
        var patcher = new HarmonyPatcher(tracer);

        patcher.PatchAllMethodsInType(typeof(CalculatorService));
        patcher.UnpatchAll();
    }

    [Fact]
    public void PatchMethod_DuplicatePatch_IsIdempotent()
    {
        var serializer = new SafeObjectSerializer();
        var tracer = new ExecutionTracer(serializer);
        var patcher = new HarmonyPatcher(tracer);

        var method = typeof(CalculatorService).GetMethod(nameof(CalculatorService.Add))!;

        patcher.PatchMethod(method);
        patcher.PatchMethod(method); // Should not throw
        patcher.UnpatchAll();
    }
}

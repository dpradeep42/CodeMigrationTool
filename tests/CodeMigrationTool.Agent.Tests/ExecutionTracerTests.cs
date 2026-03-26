using CodeMigrationTool.Agent.Instrumentation;
using CodeMigrationTool.Agent.Serialization;
using SampleWcfService;

namespace CodeMigrationTool.Agent.Tests;

public class ExecutionTracerTests
{
    private readonly SafeObjectSerializer _serializer = new();

    [Fact]
    public async Task ExecuteAndTrace_SimpleMethod_ReturnsResult()
    {
        var tracer = new ExecutionTracer(_serializer);

        // Ensure the SampleWcfService assembly is loaded
        _ = typeof(CalculatorService);

        var result = await tracer.ExecuteAndTraceAsync(
            "SampleWcfService.CalculatorService.Add",
            "[10, 20]",
            captureTrace: true);

        Assert.NotNull(result.TraceId);
        Assert.True(result.Success);
        Assert.NotNull(result.ReturnValueJson);
        Assert.Contains("30", result.ReturnValueJson);
    }

    [Fact]
    public async Task ExecuteAndTrace_InvalidMethod_ReturnsException()
    {
        var tracer = new ExecutionTracer(_serializer);

        var result = await tracer.ExecuteAndTraceAsync(
            "SampleWcfService.CalculatorService.NonExistentMethod",
            "[]",
            captureTrace: true);

        Assert.False(result.Success);
        Assert.NotNull(result.ExceptionInfo);
    }

    [Fact]
    public async Task ExecuteAndTrace_RecordsExecutionTime()
    {
        var tracer = new ExecutionTracer(_serializer);
        _ = typeof(CalculatorService);

        var result = await tracer.ExecuteAndTraceAsync(
            "SampleWcfService.CalculatorService.Add",
            "[1, 2]",
            captureTrace: true);

        Assert.True(result.ExecutionTimeMs >= 0);
    }

    [Fact]
    public async Task ExecuteAndTrace_WithNamedArgs_Works()
    {
        var tracer = new ExecutionTracer(_serializer);
        _ = typeof(CalculatorService);
        CalculatorService.ResetState();

        var result = await tracer.ExecuteAndTraceAsync(
            "SampleWcfService.CalculatorService.CalculatePremium",
            "{\"age\": 35, \"tier\": \"Gold\"}",
            captureTrace: true);

        Assert.True(result.Success);
        Assert.NotNull(result.ReturnValueJson);
    }

    [Fact]
    public void GetCallStack_UnknownTraceId_ReturnsEmptyList()
    {
        var tracer = new ExecutionTracer(_serializer);

        var stack = tracer.GetCallStack("nonexistent-trace-id");

        Assert.Empty(stack);
    }
}

using CodeMigrationTool.Agent.Instrumentation;
using CodeMigrationTool.Agent.Serialization;
using SampleWcfService;
using Xunit;

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

    [Fact]
    public void InspectObject_StaticProperty_ReturnsValue()
    {
        var tracer = new ExecutionTracer(_serializer);
        _ = typeof(CalculatorService);
        CalculatorService.ResetState();

        // CallCount is a static property accessor for s_callCount
        var state = tracer.InspectObject("SampleWcfService.CalculatorService.CallCount", 3);

        Assert.Contains("Int32", state.TypeName);
        Assert.Equal("0", state.ValueJson);
    }

    [Fact]
    public void InspectObject_InvalidExpression_ReturnsError()
    {
        var tracer = new ExecutionTracer(_serializer);

        var state = tracer.InspectObject("NoSuch.Type.Field", 3);

        Assert.Equal("Error", state.TypeName);
        Assert.Contains("Failed to inspect", state.ValueJson);
    }

    [Fact]
    public async Task ExecuteAndTrace_VoidMethod_Succeeds()
    {
        var tracer = new ExecutionTracer(_serializer);
        _ = typeof(CalculatorService);

        var result = await tracer.ExecuteAndTraceAsync(
            "SampleWcfService.CalculatorService.ResetState",
            "",
            captureTrace: true);

        Assert.True(result.Success);
        // Void methods return null for ReturnValueJson
        Assert.Null(result.ReturnValueJson);
    }

    [Fact]
    public async Task ExecuteAndTrace_MethodWithException_CapturesExceptionInfo()
    {
        var tracer = new ExecutionTracer(_serializer);

        // Calling a method that doesn't exist triggers an exception
        var result = await tracer.ExecuteAndTraceAsync(
            "SampleWcfService.CalculatorService.NonExistentMethod",
            "[]",
            captureTrace: true);

        Assert.False(result.Success);
        Assert.NotNull(result.ExceptionInfo);
        Assert.NotEmpty(result.ExceptionInfo);
    }
}

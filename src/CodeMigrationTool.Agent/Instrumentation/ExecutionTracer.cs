using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using CodeMigrationTool.Agent.Serialization;
using CodeMigrationTool.Shared.Models;

namespace CodeMigrationTool.Agent.Instrumentation;

/// <summary>
/// Records method execution traces including call stacks, timing, arguments,
/// return values, and variable state. Thread-safe for concurrent executions.
/// </summary>
public class ExecutionTracer
{
    private readonly SafeObjectSerializer _serializer;
    private readonly ConcurrentDictionary<string, TraceSession> _traces = new();

    // Per-thread trace context to correlate Harmony callbacks with traces
    [ThreadStatic]
    private static string? t_currentTraceId;

    [ThreadStatic]
    private static Stopwatch? t_stopwatch;

    public ExecutionTracer(SafeObjectSerializer serializer)
    {
        _serializer = serializer;
    }

    public async Task<ExecutionResult> ExecuteAndTraceAsync(
        string methodName,
        string argsJson,
        bool captureTrace)
    {
        var traceId = Guid.NewGuid().ToString("N");
        var session = new TraceSession { TraceId = traceId };
        _traces[traceId] = session;

        t_currentTraceId = traceId;
        t_stopwatch = Stopwatch.StartNew();

        try
        {
            // Resolve the method from loaded assemblies
            var (methodInfo, instance) = ResolveMethod(methodName);
            var args = DeserializeArguments(methodInfo, argsJson);

            // Execute the method
            object? result;
            if (methodInfo.ReturnType.IsAssignableTo(typeof(Task)))
            {
                var task = (Task)methodInfo.Invoke(instance, args)!;
                await task;
                // Extract result from Task<T> if applicable
                var resultProperty = task.GetType().GetProperty("Result");
                result = resultProperty?.GetValue(task);
            }
            else
            {
                result = methodInfo.Invoke(instance, args);
            }

            t_stopwatch.Stop();

            return new ExecutionResult
            {
                TraceId = traceId,
                ReturnValueJson = result != null ? _serializer.Serialize(result) : null,
                ExecutionTimeMs = t_stopwatch.ElapsedMilliseconds,
                SideEffects = session.SideEffects.ToList()
            };
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            t_stopwatch?.Stop();
            return new ExecutionResult
            {
                TraceId = traceId,
                ExecutionTimeMs = t_stopwatch?.ElapsedMilliseconds ?? 0,
                ExceptionInfo = ex.InnerException.ToString(),
                SideEffects = session.SideEffects.ToList()
            };
        }
        catch (Exception ex)
        {
            t_stopwatch?.Stop();
            return new ExecutionResult
            {
                TraceId = traceId,
                ExecutionTimeMs = t_stopwatch?.ElapsedMilliseconds ?? 0,
                ExceptionInfo = ex.ToString(),
                SideEffects = session.SideEffects.ToList()
            };
        }
        finally
        {
            t_currentTraceId = null;
            t_stopwatch = null;
        }
    }

    public void OnMethodEntry(MethodBase method, object?[]? args)
    {
        if (t_currentTraceId is null || !_traces.TryGetValue(t_currentTraceId, out var session))
            return;

        var frame = new CallFrameInfo
        {
            MethodSignature = FormatMethodSignature(method),
            DeclaringType = method.DeclaringType?.FullName ?? "Unknown",
            LineNumber = 0
        };

        if (args is { Length: > 0 })
        {
            try
            {
                frame.LocalsJson = _serializer.Serialize(args);
            }
            catch
            {
                frame.LocalsJson = "\"<serialization error>\"";
            }
        }

        session.CallStack.Add(frame);
    }

    public void OnMethodExit(MethodBase method, object? result, Exception? exception)
    {
        if (t_currentTraceId is null || !_traces.TryGetValue(t_currentTraceId, out var session))
            return;

        if (exception is not null)
        {
            session.SideEffects.Add($"Exception in {method.Name}: {exception.Message}");
        }
    }

    public List<CallFrameInfo> GetCallStack(string traceId)
    {
        return _traces.TryGetValue(traceId, out var session)
            ? session.CallStack.ToList()
            : [];
    }

    public ObjectState InspectObject(string expression, int maxDepth)
    {
        // Look for the variable in the most recent trace's captured locals
        // For MVP, this inspects static fields and simple expressions
        try
        {
            var value = ResolveExpression(expression);
            return new ObjectState
            {
                TypeName = value?.GetType().FullName ?? "null",
                ValueJson = _serializer.Serialize(value, maxDepth)
            };
        }
        catch (Exception ex)
        {
            return new ObjectState
            {
                TypeName = "Error",
                ValueJson = $"\"Failed to inspect '{expression}': {ex.Message}\""
            };
        }
    }

    private static string FormatMethodSignature(MethodBase method)
    {
        var parameters = method.GetParameters();
        var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
        var returnType = method is MethodInfo mi ? mi.ReturnType.Name : "void";
        return $"{returnType} {method.DeclaringType?.Name}.{method.Name}({paramStr})";
    }

    private static (MethodInfo method, object? instance) ResolveMethod(string methodName)
    {
        // Parse "Namespace.Type.Method" format
        var lastDot = methodName.LastIndexOf('.');
        if (lastDot < 0)
            throw new ArgumentException($"Method name must be in 'Type.Method' format: {methodName}");

        var typeName = methodName[..lastDot];
        var methodPart = methodName[(lastDot + 1)..];

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeName);
            if (type is null) continue;

            var method = type.GetMethod(methodPart,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);

            if (method is null) continue;

            object? instance = method.IsStatic ? null : Activator.CreateInstance(type);
            return (method, instance);
        }

        throw new InvalidOperationException($"Could not resolve method: {methodName}");
    }

    private object?[]? DeserializeArguments(MethodInfo method, string argsJson)
    {
        if (string.IsNullOrEmpty(argsJson))
            return null;

        var parameters = method.GetParameters();
        if (parameters.Length == 0)
            return null;

        var jsonDoc = System.Text.Json.JsonDocument.Parse(argsJson);
        var args = new object?[parameters.Length];

        if (jsonDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            for (int i = 0; i < parameters.Length && i < jsonDoc.RootElement.GetArrayLength(); i++)
            {
                args[i] = System.Text.Json.JsonSerializer.Deserialize(
                    jsonDoc.RootElement[i].GetRawText(), parameters[i].ParameterType);
            }
        }
        else if (jsonDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var param in parameters)
            {
                if (jsonDoc.RootElement.TryGetProperty(param.Name!, out var value))
                {
                    args[param.Position] = System.Text.Json.JsonSerializer.Deserialize(
                        value.GetRawText(), param.ParameterType);
                }
            }
        }

        return args;
    }

    private static object? ResolveExpression(string expression)
    {
        // Simple expression resolver: "Namespace.Type.FieldName"
        var lastDot = expression.LastIndexOf('.');
        if (lastDot < 0)
            throw new ArgumentException($"Expression must be in 'Type.Field' format: {expression}");

        var typeName = expression[..lastDot];
        var memberName = expression[(lastDot + 1)..];

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(typeName);
            if (type is null) continue;

            // Try static field
            var field = type.GetField(memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field is not null)
                return field.GetValue(null);

            // Try static property
            var prop = type.GetProperty(memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (prop is not null)
                return prop.GetValue(null);
        }

        throw new InvalidOperationException($"Could not resolve expression: {expression}");
    }

    private class TraceSession
    {
        public required string TraceId { get; init; }
        public ConcurrentBag<CallFrameInfo> CallStack { get; } = [];
        public ConcurrentBag<string> SideEffects { get; } = [];
    }
}

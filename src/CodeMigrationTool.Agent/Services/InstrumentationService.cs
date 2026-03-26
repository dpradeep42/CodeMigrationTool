using Grpc.Core;
using CodeMigrationTool.Shared.Protos;
using CodeMigrationTool.Agent.Instrumentation;
using CodeMigrationTool.Agent.Serialization;

namespace CodeMigrationTool.Agent.Services;

public class InstrumentationService : Shared.Protos.InstrumentationAgent.InstrumentationAgentBase
{
    private readonly ILogger<InstrumentationService> _logger;
    private readonly ExecutionTracer _tracer;
    private readonly MethodDiscovery _discovery;
    private readonly SafeObjectSerializer _serializer;
    private readonly HarmonyPatcher _patcher;

    public InstrumentationService(ILogger<InstrumentationService> logger)
    {
        _logger = logger;
        _serializer = new SafeObjectSerializer();
        _tracer = new ExecutionTracer(_serializer);
        _discovery = new MethodDiscovery();
        _patcher = new HarmonyPatcher(_tracer);
    }

    public override Task<LoadResponse> LoadApplication(LoadRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Loading application from {AppPath}", request.AppPath);

            var assembly = _discovery.LoadAssembly(request.AppPath);
            var methods = _discovery.DiscoverMethods(assembly);

            foreach (var envVar in request.EnvVars)
            {
                Environment.SetEnvironmentVariable(envVar.Key, envVar.Value);
            }

            var response = new LoadResponse { Success = true };
            foreach (var method in methods)
            {
                response.Methods.Add(new MethodSignature
                {
                    FullName = method.FullName,
                    DeclaringType = method.DeclaringType,
                    ReturnType = method.ReturnType,
                });
                response.Methods[^1].ParameterTypes.AddRange(method.ParameterTypes);
                response.Methods[^1].Attributes.AddRange(method.Attributes);
            }

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load application");
            return Task.FromResult(new LoadResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    public override async Task<ExecuteResponse> ExecuteMethod(ExecuteRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Executing method {MethodName}", request.MethodName);

            var result = await _tracer.ExecuteAndTraceAsync(
                request.MethodName,
                request.ArgsJson,
                request.CaptureTrace);

            var response = new ExecuteResponse
            {
                TraceId = result.TraceId,
                ReturnValueJson = result.ReturnValueJson ?? "",
                ExecutionTimeMs = result.ExecutionTimeMs,
                ExceptionInfo = result.ExceptionInfo ?? ""
            };
            response.SideEffects.AddRange(result.SideEffects);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute method {MethodName}", request.MethodName);
            return new ExecuteResponse
            {
                TraceId = "",
                ExceptionInfo = ex.ToString()
            };
        }
    }

    public override Task<CallStackResponse> GetCallStack(CallStackRequest request, ServerCallContext context)
    {
        var frames = _tracer.GetCallStack(request.TraceId);
        var response = new CallStackResponse();

        foreach (var frame in frames)
        {
            response.Frames.Add(new Shared.Protos.CallFrame
            {
                MethodSignature = frame.MethodSignature,
                DeclaringType = frame.DeclaringType,
                FilePath = frame.FilePath ?? "",
                LineNumber = frame.LineNumber,
                LocalsJson = frame.LocalsJson ?? ""
            });
        }

        return Task.FromResult(response);
    }

    public override Task<InspectResponse> InspectObject(InspectRequest request, ServerCallContext context)
    {
        var state = _tracer.InspectObject(request.Expression, request.MaxDepth);

        return Task.FromResult(new InspectResponse
        {
            TypeName = state.TypeName,
            ValueJson = state.ValueJson
        });
    }

    public override Task<ListMethodsResponse> ListMethods(ListMethodsRequest request, ServerCallContext context)
    {
        var methods = _discovery.GetDiscoveredMethods(request.NamespaceFilter);
        var response = new ListMethodsResponse();

        foreach (var method in methods)
        {
            var sig = new MethodSignature
            {
                FullName = method.FullName,
                DeclaringType = method.DeclaringType,
                ReturnType = method.ReturnType,
            };
            sig.ParameterTypes.AddRange(method.ParameterTypes);
            sig.Attributes.AddRange(method.Attributes);
            response.Methods.Add(sig);
        }

        return Task.FromResult(response);
    }

    public override Task<HealthResponse> HealthCheck(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new HealthResponse { Healthy = true });
    }
}

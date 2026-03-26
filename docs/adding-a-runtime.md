# Adding a New Runtime

This guide explains how to add support for a new language runtime (e.g., Python, Java) to CodeMigrationTool.

## What You Need to Build

1. **An InstrumentationService implementation** for your runtime
2. **Runtime registration** in the SandboxManager
3. **Tests** with a sample application

## Step 1: Implement InstrumentationService

Create a new class that provides the same capabilities as the .NET `InstrumentationService`:

- **LoadApplication** — Load the target application from a given path
- **ExecuteMethod** — Execute a method by name with JSON arguments
- **GetCallStack** — Return the call stack from a traced execution
- **InspectObject** — Serialize an object/variable to JSON
- **ListMethods** — Enumerate all discoverable methods

For non-.NET runtimes, you have two options:

### Option A: In-Process (if the runtime can be embedded in .NET)
Use a .NET embedding of the target runtime (e.g., IronPython, IKVM for Java). Implement the service as a C# class that delegates to the embedded runtime.

### Option B: Child Process (for native runtimes)
Launch the target runtime as a child process and communicate via the gRPC proto definition in `src/CodeMigrationTool.Shared/Protos/instrumentation.proto`. The proto and generated stubs are already in the Shared project for this purpose.

## Step 2: Register the Runtime

In `SandboxManager.cs`, add your runtime to the creation logic:

```csharp
if (runtime.ToLowerInvariant() != "dotnet")
    throw new ArgumentException($"Unsupported runtime: {runtime}");

// Becomes:
var service = runtime.ToLowerInvariant() switch
{
    "dotnet" => new DotNetInstrumentationService(logger),
    "python" => new PythonInstrumentationService(logger),
    _ => throw new ArgumentException($"Unsupported runtime: {runtime}")
};
```

## Step 3: Add Tests

Create a test fixture app in `tests/SampleApps/` and integration tests that verify your runtime works through the full pipeline (load, execute, trace, inspect).

## Instrumentation Techniques by Runtime

| Runtime | Recommended Approach |
|---------|---------------------|
| .NET | Harmony (Prefix/Postfix hooks) — current implementation |
| Python | `sys.settrace` + `inspect` module |
| Java | Java Agent + Byte Buddy (child process with gRPC) |
| Node.js | `--require` hook + `async_hooks` |
| Go | Delve debugger API (child process with gRPC) |

## gRPC Proto (for Child Process Runtimes)

The gRPC service definition is at `src/CodeMigrationTool.Shared/Protos/instrumentation.proto`. Implement this service in your target language and the SandboxManager will connect to it via `Grpc.Net.Client`.

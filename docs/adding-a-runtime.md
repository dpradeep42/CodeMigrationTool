# Adding a New Runtime

This guide explains how to add support for a new language runtime (e.g., Python, Java) to CodeMigrationTool.

## What You Need to Build

1. **An Instrumentation Agent** that implements the gRPC `InstrumentationAgent` service
2. **A Dockerfile** for the sandbox image
3. **Runtime registration** in the SandboxManager

## Step 1: Implement the gRPC Service

Your agent must implement the service defined in `src/CodeMigrationTool.Shared/Protos/instrumentation.proto`:

- `LoadApplication` — Load the target application from `/app`
- `ExecuteMethod` — Execute a method by name with JSON arguments
- `GetCallStack` — Return the call stack from a traced execution
- `InspectObject` — Serialize an object/variable to JSON
- `ListMethods` — Enumerate all discoverable methods
- `HealthCheck` — Return healthy status

The agent can be written in any language that supports gRPC.

## Step 2: Create a Dockerfile

Add a Dockerfile at `src/CodeMigrationTool.Sandbox/Docker/Dockerfile.<runtime>`:

```dockerfile
FROM <base-image>

# Install your agent
COPY agent/ /agent/

# Target app mounted at /app (read-only)
VOLUME /app

# gRPC port
EXPOSE 50100

# Run as non-root
USER appuser

ENTRYPOINT ["your-agent-binary"]
```

## Step 3: Register the Runtime

In `SandboxManager.cs`, add your runtime to the image lookup:

```csharp
var imageName = runtime.ToLowerInvariant() switch
{
    "dotnet" => DefaultDotnetImage,
    "python" => "codemigrationtool-sandbox-python:latest",  // Add this
    _ => throw new ArgumentException($"Unsupported runtime: {runtime}")
};
```

## Step 4: Add Tests

Create a test fixture app in `tests/SampleApps/` and integration tests that verify your agent works through the full pipeline.

## Instrumentation Techniques by Runtime

| Runtime | Recommended Approach |
|---------|---------------------|
| .NET | Harmony (Prefix/Postfix hooks) |
| Python | `sys.settrace` + `inspect` module |
| Java | Java Agent + Byte Buddy |
| Node.js | `--require` hook + `async_hooks` |
| Go | eBPF or Delve debugger API |

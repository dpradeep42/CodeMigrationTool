# CodeMigrationTool

**Dynamic Runtime & Legacy Execution MCP** — Give AI agents runtime introspection into legacy applications during code migration.

## The Problem

AI agents assisting with legacy code migration rely purely on static code analysis. They can read `.cs` files, but they are completely blind to runtime realities like reflection, dynamic dependency injection, environment variables, and hidden state mutations.

## The Solution

CodeMigrationTool is an MCP (Model Context Protocol) server that gives AI agents the ability to:

- **Load** a legacy .NET assembly in an isolated in-process sandbox
- **Execute** specific methods with test data and observe the results
- **Inspect** live memory state, variable values, and object graphs
- **Trace** full call stacks including hidden middleware and framework internals
- **Rollback** state for deterministic replay (the "Time Machine")

## Architecture

```
AI Agent (Claude, etc.)
  ↕ MCP Protocol (stdio/SSE)
MCP Server (C# / ModelContextProtocol SDK)
  ↕ Direct method calls
Sandbox Manager (AssemblyLoadContext isolation)
  ↕ Direct method calls
Instrumentation Engine (Harmony runtime hooks)
```

All C#/.NET 8, running in a single process:

| Layer | Role | Key Technology |
|-------|------|---------------|
| **MCP Server** | Exposes tools to the LLM | `ModelContextProtocol` NuGet SDK |
| **Sandbox Manager** | Assembly lifecycle, state snapshots | `AssemblyLoadContext` (collectible) |
| **Instrumentation Engine** | Runtime method hooking, tracing | `Lib.Harmony` |

## MCP Tools

| Tool | Description |
|------|-------------|
| `initialize_runtime` | Load a .NET assembly in an isolated sandbox |
| `execute_and_trace` | Run a method and capture the full execution trace |
| `get_call_stack` | Get the complete call stack from a traced execution |
| `inspect_memory_state` | Inspect runtime state of objects and variables |
| `list_methods` | Discover all methods in the loaded application |
| `create_snapshot` | Snapshot current state for later rollback |
| `rollback_state` | Restore a previous snapshot for deterministic replay |
| `shutdown_runtime` | Clean up sandbox resources |

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build

```bash
dotnet build CodeMigrationTool.sln
```

### Run the MCP server

```bash
dotnet run --project src/CodeMigrationTool.Server
```

### Configure Claude Desktop

Add to your Claude Desktop config (`~/Library/Application Support/Claude/claude_desktop_config.json` on macOS):

```json
{
  "mcpServers": {
    "code-migration-tool": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/CodeMigrationTool/src/CodeMigrationTool.Server"]
    }
  }
}
```

### Run tests

```bash
dotnet test CodeMigrationTool.sln
```

## Example: Migrating a WCF Endpoint

1. The AI reads the legacy WCF `CalculatePremium` method via static analysis
2. It's unsure how a `PricingMatrix` object is constructed at runtime
3. It uses `execute_and_trace` to call the method with test data
4. The MCP returns the serialized runtime state, revealing an environment variable that modifies pricing
5. The AI writes a correct, modernized Web API endpoint

## Project Structure

```
src/
  CodeMigrationTool.Server/     # MCP server with tool definitions
  CodeMigrationTool.Sandbox/    # In-process sandbox management
  CodeMigrationTool.Agent/      # Instrumentation engine (Harmony + tracing)
  CodeMigrationTool.Shared/     # Shared models and gRPC proto definitions
tests/
  CodeMigrationTool.Server.Tests/
  CodeMigrationTool.Agent.Tests/
  CodeMigrationTool.Integration.Tests/
  SampleApps/SampleWcfService/  # Test fixture
```

## How Isolation Works

Instead of Docker containers, each session loads the target assembly in a separate **collectible `AssemblyLoadContext`**. This provides:

- **Assembly isolation** — different versions of the same DLL can coexist
- **Unloadability** — assemblies are fully unloaded when the session ends
- **State snapshots** — static field values are captured via reflection and serialized to JSON
- **Deterministic rollback** — snapshots are restored by deserializing and setting static fields back

## Security Considerations

This tool executes arbitrary .NET code in the MCP server process. For production use:

- Run the MCP server on a dedicated machine or VM
- Limit which assemblies can be loaded via configuration
- Monitor resource usage
- Consider adding process-level sandboxing (e.g., AppArmor, seccomp)

## Roadmap

- [x] Phase 1: Project scaffold + in-process .NET instrumentation
- [ ] Phase 2: Integration tests, warm pool, session TTL
- [ ] Phase 3: Process-level isolation, resource limits, mock DB provisioning
- [ ] Phase 4: Python/Java runtimes, execution diff tool, NuGet publishing

## Contributing

Contributions are welcome! See [docs/adding-a-runtime.md](docs/adding-a-runtime.md) for how to add support for new language runtimes.

## License

MIT License - see [LICENSE](LICENSE) for details.

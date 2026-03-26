# Architecture

## Overview

CodeMigrationTool uses an in-process architecture where all layers run in a single .NET process:

```
┌─────────────────────────────────────────────────────┐
│  AI Agent (Claude, Copilot, etc.)                   │
│  ↕ MCP Protocol (stdio / SSE)                       │
├─────────────────────────────────────────────────────┤
│  Layer A: MCP Server                                │
│  - Exposes tools to LLM via [McpServerTool]         │
│  - Session management                               │
│  - Translates semantic requests to direct calls      │
│  ↕ Direct method calls                              │
├─────────────────────────────────────────────────────┤
│  Layer B: Sandbox Manager                           │
│  - AssemblyLoadContext lifecycle                     │
│  - State snapshots via reflection + JSON             │
│  - Assembly isolation and unloading                  │
│  ↕ Direct method calls                              │
├─────────────────────────────────────────────────────┤
│  Layer C: Instrumentation Engine                    │
│  - Runs in-process as a library                     │
│  - Harmony patches for method interception          │
│  - Reflection-based method discovery                │
│  - Object serialization to JSON                     │
└─────────────────────────────────────────────────────┘
```

## Layer A: MCP Server

The MCP Server is the entry point for AI agents. It exposes tools via the Model Context Protocol using the official C# SDK (`ModelContextProtocol` NuGet package).

**Key classes:**
- `Tools/RuntimeTools.cs` — Session lifecycle (init, shutdown)
- `Tools/ExecutionTools.cs` — Method execution with tracing
- `Tools/InspectionTools.cs` — Call stack and memory inspection
- `Tools/StateTools.cs` — Snapshot and rollback
- `Sessions/SessionManager.cs` — Maps session IDs to sandbox instances

## Layer B: Sandbox Manager

Manages in-process sandboxes using .NET's `AssemblyLoadContext` for assembly isolation.

**Key classes:**
- `SandboxManager.cs` — Creates/destroys sandboxes, each with its own `AssemblyLoadContext`
- `SnapshotManager.cs` — Captures static field state via reflection, serializes to JSON, restores by deserializing
- `SandboxPool.cs` — Warm pool for pre-loaded assemblies (Phase 2)

**Isolation model:**
- Each session loads the target assembly in a separate **collectible `AssemblyLoadContext`**
- Assemblies can be fully unloaded when the session ends
- Different versions of the same DLL can coexist across sessions

## Layer C: Instrumentation Engine

A library that provides runtime method hooking and execution tracing. Called directly by the MCP Server via the Sandbox Manager.

**Key classes:**
- `Instrumentation/HarmonyPatcher.cs` — Runtime method patching via Harmony Prefix/Postfix
- `Instrumentation/ExecutionTracer.cs` — Records call stacks, timing, and variable state
- `Instrumentation/MethodDiscovery.cs` — Reflection-based method enumeration, WCF attribute detection
- `Serialization/SafeObjectSerializer.cs` — JSON serialization with circular reference handling
- `Services/InstrumentationService.cs` — Facade that coordinates all instrumentation components

## State Snapshots ("Time Machine")

Instead of container snapshots, the system captures and restores state via reflection:

1. **Capture:** Enumerate all static fields in the loaded assembly's exported types, serialize their values to JSON
2. **Restore:** Deserialize the JSON and set static fields back to their captured values

This enables deterministic replay: execute a method, observe the result, rollback state, try again with different inputs.

## Security Model

See [security.md](security.md) for details on the isolation model and threat considerations.

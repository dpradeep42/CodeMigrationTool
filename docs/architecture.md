# Architecture

## Overview

CodeMigrationTool uses a three-layer architecture where each layer has a single responsibility:

```
┌─────────────────────────────────────────────────────┐
│  AI Agent (Claude, Copilot, etc.)                   │
│  ↕ MCP Protocol (stdio / SSE)                       │
├─────────────────────────────────────────────────────┤
│  Layer A: MCP Server                                │
│  - Exposes tools to LLM via [McpServerTool]         │
│  - Session management                               │
│  - Translates semantic requests to gRPC calls        │
│  ↕ gRPC                                             │
├─────────────────────────────────────────────────────┤
│  Layer B: Sandbox Manager                           │
│  - Docker container orchestration                   │
│  - Snapshot/rollback                                │
│  - Network isolation, resource limits               │
│  ↕ gRPC (localhost, within container)               │
├─────────────────────────────────────────────────────┤
│  Layer C: Instrumentation Agent                     │
│  - Runs inside the sandbox container                │
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

Manages Docker containers that provide isolated execution environments.

**Key classes:**
- `SandboxManager.cs` — Container create/start/stop/destroy via Docker.DotNet
- `SnapshotManager.cs` — `docker commit` based snapshots for state rollback
- `SandboxPool.cs` — Warm pool for low-latency sandbox acquisition (Phase 2)

## Layer C: Instrumentation Agent

A gRPC server that runs inside each sandbox container. It loads the target application, hooks into its methods, and reports execution traces.

**Key classes:**
- `Instrumentation/HarmonyPatcher.cs` — Runtime method patching via Harmony Prefix/Postfix
- `Instrumentation/ExecutionTracer.cs` — Records call stacks, timing, and variable state
- `Instrumentation/MethodDiscovery.cs` — Reflection-based method enumeration
- `Serialization/SafeObjectSerializer.cs` — JSON serialization with circular reference handling

## Communication

All inter-layer communication uses gRPC with protobuf contracts defined in `src/CodeMigrationTool.Shared/Protos/instrumentation.proto`.

## Security Model

See [security.md](security.md) for details on sandbox isolation and threat model.

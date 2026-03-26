# Security Model

CodeMigrationTool is designed to allow AI agents to execute arbitrary code. This is inherently dangerous and requires strict isolation.

## Threat Model

The primary threat is **Remote Code Execution (RCE) by design**. The system explicitly allows an LLM to run code. The security model ensures this cannot affect the host system or network.

## Isolation Layers

### Network Isolation
- Sandbox containers run with `--network none`
- Zero outbound internet access
- No access to the host machine's network interfaces
- gRPC communication only on localhost via mapped port

### Filesystem Isolation
- Target application directory mounted as **read-only** (`/app:ro`)
- No access to host filesystem beyond the mounted directory
- Temporary writes only within the container's ephemeral filesystem

### Process Isolation
- Containers run as non-root user (`appuser`)
- All Linux capabilities dropped (`--cap-drop ALL`)
- Process limit enforced (`--pids-limit 256`)

### Resource Limits
- Memory: 512 MB default (`--memory`)
- CPU: 1 core default (`--cpus`)
- Execution timeout: 30 seconds per tool invocation

### Session Security
- Session IDs are `Guid.NewGuid()` (128-bit random, unguessable)
- Sessions have configurable TTL with automatic cleanup
- Each session maps to exactly one sandbox

## What This Does NOT Protect Against

- **Bugs in Docker itself** — if Docker has a container escape vulnerability, this system is vulnerable
- **Resource exhaustion within limits** — a malicious app can use its full 512 MB allocation
- **Side-channel attacks** — timing attacks or CPU cache attacks within the container

## Recommendations for Production Use

1. Run the MCP server on a dedicated VM, not on a developer workstation
2. Use a container runtime with additional isolation (gVisor, Kata Containers)
3. Rotate sandbox containers regularly (don't reuse across sessions)
4. Monitor container resource usage and set alerts
5. Keep Docker and the host OS updated

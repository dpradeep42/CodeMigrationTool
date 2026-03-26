# Security Model

CodeMigrationTool loads and executes arbitrary .NET assemblies in-process. This section describes the isolation model and security considerations.

## Isolation Model

### AssemblyLoadContext Isolation
Each sandbox session loads the target assembly in a separate **collectible `AssemblyLoadContext`**. This provides:

- **Assembly isolation** — Types from different sessions don't interfere with each other
- **Unloadability** — Assemblies are fully unloaded when the session ends, freeing memory
- **Version isolation** — Different versions of the same dependency can coexist

### What AssemblyLoadContext Does NOT Isolate
- **Process memory** — All sessions share the same process address space
- **Environment variables** — `Environment.SetEnvironmentVariable` affects the whole process
- **Static state in shared assemblies** — Types loaded in the default context (like `System.*`) share state
- **File system** — The loaded assembly has the same file system access as the MCP server process
- **Network** — No network isolation; the loaded code can make network calls

## Threat Model

The primary risk is that a malicious or buggy assembly loaded via `initialize_runtime` has **full access** to the MCP server process. Specifically:

- **Code execution** — The loaded assembly can run arbitrary code
- **Memory access** — It can read/modify any memory in the process
- **File I/O** — It can read/write files with the process's permissions
- **Network access** — It can make outbound network connections
- **Process control** — It can terminate the process or spawn child processes

## Mitigations

### Current (Phase 1)
- **Session isolation via AssemblyLoadContext** — Prevents accidental type conflicts
- **Session tokens** — `Guid.NewGuid()` (128-bit random), unguessable
- **Assembly unloading** — Clean disposal when sessions end
- **Snapshot/rollback** — State can be restored for deterministic testing

### Recommended for Production
1. **Run on a dedicated machine or VM** — Don't run the MCP server on a developer workstation with access to sensitive resources
2. **OS-level sandboxing** — Use AppArmor, seccomp, or SELinux profiles to restrict the MCP server process
3. **Least-privilege user** — Run the server as a dedicated low-privilege user account
4. **File system restrictions** — Use read-only mounts or chroot to limit file access
5. **Network restrictions** — Use firewall rules (iptables/nftables) to block outbound traffic from the server process
6. **Resource limits** — Use cgroups or ulimits to cap CPU, memory, and process count

### Future Phases
- **Process-level isolation** — Run each sandbox as a separate child process for true memory isolation
- **Container support** — Optional Docker/Podman integration for users who want stronger isolation
- **Configurable allow-lists** — Restrict which assemblies and namespaces can be loaded

namespace CodeMigrationTool.Server.Configuration;

public class ServerConfiguration
{
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int MaxConcurrentSessions { get; set; } = 10;
    public int ExecutionTimeoutSeconds { get; set; } = 30;
    public long SandboxMemoryLimitBytes { get; set; } = 512 * 1024 * 1024; // 512 MB
    public int SandboxCpuLimit { get; set; } = 1;
    public int WarmPoolSize { get; set; } = 2;
    public string DefaultRuntime { get; set; } = "dotnet";
}

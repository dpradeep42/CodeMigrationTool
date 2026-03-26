namespace CodeMigrationTool.Server.Sessions;

public class SessionInfo
{
    public required string SessionId { get; set; }
    public required string SandboxId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
}

namespace CodeMigrationTool.Sandbox.Models;

public class SandboxInfo
{
    public required string SandboxId { get; set; }
    public required string ContainerId { get; set; }
    public required string Runtime { get; set; }
    public required string AppPath { get; set; }
    public int GrpcPort { get; set; }
    public SandboxStatus Status { get; set; } = SandboxStatus.Creating;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<string> SnapshotIds { get; set; } = [];
}

public enum SandboxStatus
{
    Creating,
    Ready,
    Running,
    Stopped,
    Failed,
    Destroyed
}

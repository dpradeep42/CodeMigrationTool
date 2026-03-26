namespace CodeMigrationTool.Sandbox.Models;

public class SnapshotInfo
{
    public required string SnapshotId { get; set; }
    public required string SandboxId { get; set; }
    public string? Name { get; set; }
    public required string DockerImageId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

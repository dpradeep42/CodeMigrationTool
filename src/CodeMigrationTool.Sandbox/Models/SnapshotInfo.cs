namespace CodeMigrationTool.Sandbox.Models;

public class SnapshotInfo
{
    public required string SnapshotId { get; set; }
    public required string SandboxId { get; set; }
    public string? Name { get; set; }

    /// <summary>
    /// Serialized static field state captured at snapshot time.
    /// </summary>
    public required string SerializedState { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

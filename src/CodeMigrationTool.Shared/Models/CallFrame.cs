namespace CodeMigrationTool.Shared.Models;

public class CallFrameInfo
{
    public required string MethodSignature { get; set; }
    public required string DeclaringType { get; set; }
    public string? FilePath { get; set; }
    public int LineNumber { get; set; }
    public string? LocalsJson { get; set; }
}

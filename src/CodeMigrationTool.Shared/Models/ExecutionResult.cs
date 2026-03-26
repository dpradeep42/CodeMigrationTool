namespace CodeMigrationTool.Shared.Models;

public class ExecutionResult
{
    public required string TraceId { get; set; }
    public string? ReturnValueJson { get; set; }
    public long ExecutionTimeMs { get; set; }
    public List<string> SideEffects { get; set; } = [];
    public string? ExceptionInfo { get; set; }
    public bool Success => ExceptionInfo is null;
}

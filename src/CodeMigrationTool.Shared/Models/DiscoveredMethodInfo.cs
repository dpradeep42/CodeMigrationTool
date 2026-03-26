namespace CodeMigrationTool.Shared.Models;

public class DiscoveredMethodInfo
{
    public required string FullName { get; set; }
    public required string DeclaringType { get; set; }
    public required string ReturnType { get; set; }
    public List<string> ParameterTypes { get; set; } = [];
    public List<string> Attributes { get; set; } = [];
}

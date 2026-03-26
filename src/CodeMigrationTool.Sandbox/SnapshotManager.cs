using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeMigrationTool.Sandbox.Models;
using Microsoft.Extensions.Logging;

namespace CodeMigrationTool.Sandbox;

/// <summary>
/// Manages state snapshots for deterministic replay.
/// Captures static field values from the loaded assembly's types via reflection
/// and serializes them to JSON. Restores by deserializing and setting fields back.
/// </summary>
public class SnapshotManager
{
    private readonly ILogger<SnapshotManager> _logger;
    private readonly ConcurrentDictionary<string, SnapshotInfo> _snapshots = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        MaxDepth = 10,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SnapshotManager(ILogger<SnapshotManager> logger)
    {
        _logger = logger;
    }

    public SnapshotInfo CreateSnapshot(SandboxInfo sandbox, string? name = null)
    {
        var snapshotId = Guid.NewGuid().ToString("N")[..12];

        _logger.LogInformation(
            "Creating snapshot {SnapshotId} for sandbox {SandboxId}",
            snapshotId, sandbox.SandboxId);

        var assembly = sandbox.Service?.LoadedAssembly
            ?? throw new InvalidOperationException("No assembly loaded in sandbox");

        var state = CaptureStaticState(assembly);

        var snapshot = new SnapshotInfo
        {
            SnapshotId = snapshotId,
            SandboxId = sandbox.SandboxId,
            Name = name,
            SerializedState = state
        };

        _snapshots[snapshotId] = snapshot;
        sandbox.SnapshotIds.Add(snapshotId);

        _logger.LogInformation("Snapshot {SnapshotId} created ({Bytes} bytes)",
            snapshotId, state.Length);

        return snapshot;
    }

    public void RestoreSnapshot(SandboxInfo sandbox, SnapshotInfo snapshot)
    {
        var assembly = sandbox.Service?.LoadedAssembly
            ?? throw new InvalidOperationException("No assembly loaded in sandbox");

        _logger.LogInformation("Restoring snapshot {SnapshotId} for sandbox {SandboxId}",
            snapshot.SnapshotId, sandbox.SandboxId);

        RestoreStaticState(assembly, snapshot.SerializedState);
    }

    public SnapshotInfo GetSnapshot(string snapshotId)
    {
        if (!_snapshots.TryGetValue(snapshotId, out var snapshot))
            throw new KeyNotFoundException($"Snapshot not found: {snapshotId}");
        return snapshot;
    }

    public SnapshotInfo? GetLatestSnapshot(string sandboxId)
    {
        return _snapshots.Values
            .Where(s => s.SandboxId == sandboxId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();
    }

    public void DeleteSnapshot(string snapshotId)
    {
        _snapshots.TryRemove(snapshotId, out _);
    }

    public IReadOnlyCollection<SnapshotInfo> ListSnapshots(string sandboxId)
    {
        return _snapshots.Values
            .Where(s => s.SandboxId == sandboxId)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
    }

    private string CaptureStaticState(Assembly assembly)
    {
        var state = new Dictionary<string, Dictionary<string, string>>();

        foreach (var type in assembly.GetExportedTypes())
        {
            var fields = type.GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (fields.Length == 0)
                continue;

            var fieldState = new Dictionary<string, string>();
            foreach (var field in fields)
            {
                if (field.IsLiteral) continue; // Skip constants

                try
                {
                    var value = field.GetValue(null);
                    fieldState[field.Name] = JsonSerializer.Serialize(value, SerializerOptions);
                }
                catch (Exception ex)
                {
                    fieldState[field.Name] = $"\"<capture failed: {ex.Message}>\"";
                }
            }

            if (fieldState.Count > 0)
            {
                state[type.FullName ?? type.Name] = fieldState;
            }
        }

        return JsonSerializer.Serialize(state);
    }

    private void RestoreStaticState(Assembly assembly, string serializedState)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(serializedState);
        if (state is null) return;

        foreach (var (typeName, fields) in state)
        {
            var type = assembly.GetType(typeName);
            if (type is null) continue;

            foreach (var (fieldName, valueJson) in fields)
            {
                if (valueJson.StartsWith("\"<capture failed"))
                    continue;

                var field = type.GetField(fieldName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (field is null || field.IsLiteral) continue;

                try
                {
                    var value = JsonSerializer.Deserialize(valueJson, field.FieldType, SerializerOptions);
                    field.SetValue(null, value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to restore field {Type}.{Field}", typeName, fieldName);
                }
            }
        }
    }
}

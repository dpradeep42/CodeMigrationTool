using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeMigrationTool.Agent.Serialization;

/// <summary>
/// Safely serializes .NET objects to JSON, handling:
/// - Circular references (via $id/$ref metadata)
/// - Deep object graphs (configurable max depth)
/// - Large collections (truncation)
/// - Unserializable types (graceful fallback)
/// </summary>
public class SafeObjectSerializer
{
    private const int DefaultMaxDepth = 5;
    private const int MaxCollectionElements = 100;

    public string Serialize(object? value, int maxDepth = DefaultMaxDepth)
    {
        if (value is null)
            return "null";

        try
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.Preserve,
                MaxDepth = maxDepth,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new SafeObjectConverter(maxDepth, MaxCollectionElements) }
            };

            return JsonSerializer.Serialize(value, value.GetType(), options);
        }
        catch (Exception ex)
        {
            // Fallback: return a basic representation
            return JsonSerializer.Serialize(new
            {
                __type = value.GetType().FullName,
                __toString = value.ToString(),
                __error = $"Full serialization failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Custom converter that handles edge cases System.Text.Json can't handle natively.
    /// </summary>
    private class SafeObjectConverter : JsonConverter<object>
    {
        private readonly int _maxDepth;
        private readonly int _maxCollectionElements;

        public SafeObjectConverter(int maxDepth, int maxCollectionElements)
        {
            _maxDepth = maxDepth;
            _maxCollectionElements = maxCollectionElements;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            // Only handle types that aren't natively supported
            return !typeToConvert.IsPrimitive
                && typeToConvert != typeof(string)
                && typeToConvert != typeof(decimal)
                && typeToConvert != typeof(DateTime)
                && typeToConvert != typeof(DateTimeOffset)
                && typeToConvert != typeof(Guid);
        }

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Deserialization not supported by SafeObjectConverter");
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            var type = value.GetType();

            // Handle collections with truncation
            if (value is System.Collections.IEnumerable enumerable && type != typeof(string))
            {
                writer.WriteStartArray();
                var count = 0;
                foreach (var item in enumerable)
                {
                    if (count >= _maxCollectionElements)
                    {
                        writer.WriteStringValue($"... truncated (showing {_maxCollectionElements} of more items)");
                        break;
                    }
                    JsonSerializer.Serialize(writer, item, options);
                    count++;
                }
                writer.WriteEndArray();
                return;
            }

            // Handle types with no parameterless constructor or that throw during serialization
            try
            {
                // Use default serialization for most objects
                JsonSerializer.Serialize(writer, value, type, new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.Preserve,
                    MaxDepth = _maxDepth,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
            catch
            {
                // Final fallback: just write type info and ToString
                writer.WriteStartObject();
                writer.WriteString("__type", type.FullName);
                writer.WriteString("__value", value.ToString());
                writer.WriteEndObject();
            }
        }
    }
}

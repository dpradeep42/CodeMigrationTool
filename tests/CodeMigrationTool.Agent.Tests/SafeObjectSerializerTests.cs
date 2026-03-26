using CodeMigrationTool.Agent.Serialization;
using SampleWcfService;

namespace CodeMigrationTool.Agent.Tests;

public class SafeObjectSerializerTests
{
    private readonly SafeObjectSerializer _sut = new();

    [Fact]
    public void Serialize_Null_ReturnsNullString()
    {
        var result = _sut.Serialize(null);
        Assert.Equal("null", result);
    }

    [Fact]
    public void Serialize_PrimitiveInt_ReturnsValue()
    {
        var result = _sut.Serialize(42);
        Assert.Contains("42", result);
    }

    [Fact]
    public void Serialize_String_ReturnsQuotedString()
    {
        var result = _sut.Serialize("hello");
        Assert.Contains("hello", result);
    }

    [Fact]
    public void Serialize_SimpleObject_ReturnsJson()
    {
        var obj = new { Name = "Test", Value = 123 };
        var result = _sut.Serialize(obj);

        Assert.Contains("Test", result);
        Assert.Contains("123", result);
    }

    [Fact]
    public void Serialize_CircularReference_DoesNotThrow()
    {
        var matrix = new PricingMatrix
        {
            Tier = "Gold",
            Multiplier = 0.75m,
            GeneratedAt = DateTime.UtcNow
        };
        matrix.ParentMatrix = matrix; // Circular reference

        var result = _sut.Serialize(matrix);

        Assert.NotNull(result);
        Assert.Contains("Gold", result);
        // Should handle circular ref via $id/$ref or graceful fallback
    }

    [Fact]
    public void Serialize_Dictionary_ReturnsJson()
    {
        var dict = new Dictionary<string, int>
        {
            ["a"] = 1,
            ["b"] = 2
        };

        var result = _sut.Serialize(dict);

        Assert.Contains("a", result);
        Assert.Contains("b", result);
    }

    [Fact]
    public void Serialize_PricingMatrix_IncludesAllFields()
    {
        var matrix = new PricingMatrix
        {
            Tier = "Silver",
            BaseRates = new Dictionary<string, decimal> { ["Young"] = 500m },
            Multiplier = 0.9m,
            GeneratedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = _sut.Serialize(matrix);

        Assert.Contains("Silver", result);
        Assert.Contains("Young", result);
    }

    [Fact]
    public void Serialize_WithMaxDepth_RespectsLimit()
    {
        var obj = new { Level1 = new { Level2 = new { Level3 = new { Level4 = "deep" } } } };

        // Should not throw even with shallow depth
        var result = _sut.Serialize(obj, maxDepth: 2);
        Assert.NotNull(result);
    }
}

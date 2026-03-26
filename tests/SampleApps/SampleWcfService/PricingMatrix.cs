namespace SampleWcfService;

/// <summary>
/// A complex nested object that tests serialization edge cases:
/// - Circular reference (ParentMatrix)
/// - Dictionary properties
/// - Nullable fields
/// - DateTime values
/// </summary>
public class PricingMatrix
{
    public required string Tier { get; set; }
    public Dictionary<string, decimal> BaseRates { get; set; } = [];
    public decimal Multiplier { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Intentional circular reference to test safe serialization.
    /// </summary>
    public PricingMatrix? ParentMatrix { get; set; }

    public decimal GetEffectiveRate(string ageGroup)
    {
        return BaseRates.GetValueOrDefault(ageGroup, 0m) * Multiplier;
    }
}

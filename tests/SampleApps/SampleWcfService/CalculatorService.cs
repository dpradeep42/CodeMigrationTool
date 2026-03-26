namespace SampleWcfService;

/// <summary>
/// A sample WCF-style service with hidden runtime behavior that static analysis would miss:
/// - Environment variable-dependent pricing
/// - Mutable static state
/// - Complex nested object construction
/// </summary>
[ServiceContract]
public class CalculatorService : ICalculatorService
{
    // Hidden mutable state — a common pattern in legacy WCF services
    private static int s_callCount;
    private static readonly Dictionary<string, decimal> s_tierMultipliers = new()
    {
        ["Bronze"] = 1.0m,
        ["Silver"] = 0.9m,
        ["Gold"] = 0.75m,
        ["Platinum"] = 0.6m
    };

    [OperationContract]
    public decimal Add(decimal a, decimal b)
    {
        s_callCount++;
        return a + b;
    }

    [OperationContract]
    public decimal CalculatePremium(int age, string tier)
    {
        s_callCount++;

        // Hidden behavior: environment variable overrides pricing
        var overrideRate = Environment.GetEnvironmentVariable("PREMIUM_OVERRIDE_RATE");
        if (!string.IsNullOrEmpty(overrideRate) && decimal.TryParse(overrideRate, out var rate))
        {
            return rate * age;
        }

        var basePremium = age switch
        {
            < 25 => 500m,
            < 35 => 350m,
            < 50 => 400m,
            < 65 => 600m,
            _ => 900m
        };

        var multiplier = s_tierMultipliers.GetValueOrDefault(tier, 1.0m);

        // Hidden side effect: modifies the multiplier after 10 calls
        if (s_callCount > 10)
        {
            multiplier *= 1.1m;
        }

        return basePremium * multiplier;
    }

    [OperationContract]
    public PricingMatrix GetPricingMatrix(string tier)
    {
        s_callCount++;

        var matrix = new PricingMatrix
        {
            Tier = tier,
            BaseRates = new Dictionary<string, decimal>
            {
                ["Young"] = 500m,
                ["Adult"] = 350m,
                ["Middle"] = 400m,
                ["Senior"] = 600m,
                ["Elder"] = 900m
            },
            Multiplier = s_tierMultipliers.GetValueOrDefault(tier, 1.0m),
            GeneratedAt = DateTime.UtcNow
        };

        // Circular reference: the matrix references itself as parent
        matrix.ParentMatrix = matrix;

        return matrix;
    }

    // Static accessor for testing inspection
    public static int CallCount => s_callCount;
    public static void ResetState() => s_callCount = 0;
}

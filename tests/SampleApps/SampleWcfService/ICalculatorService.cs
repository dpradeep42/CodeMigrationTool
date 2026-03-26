namespace SampleWcfService;

/// <summary>
/// Simulates a WCF [ServiceContract] interface.
/// In a real WCF project, this would have [ServiceContract] and [OperationContract] attributes.
/// We use custom attributes here since System.ServiceModel isn't available in .NET 8 by default.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
public class ServiceContractAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class OperationContractAttribute : Attribute { }

[ServiceContract]
public interface ICalculatorService
{
    [OperationContract]
    decimal Add(decimal a, decimal b);

    [OperationContract]
    decimal CalculatePremium(int age, string tier);

    [OperationContract]
    PricingMatrix GetPricingMatrix(string tier);
}

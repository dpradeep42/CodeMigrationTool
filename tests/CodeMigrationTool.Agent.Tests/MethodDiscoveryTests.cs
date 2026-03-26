using CodeMigrationTool.Agent.Instrumentation;
using SampleWcfService;
using Xunit;

namespace CodeMigrationTool.Agent.Tests;

public class MethodDiscoveryTests
{
    [Fact]
    public void DiscoverMethods_FindsAllPublicMethods()
    {
        var discovery = new MethodDiscovery();
        var methods = discovery.DiscoverMethods(typeof(CalculatorService).Assembly);

        var methodNames = methods.Select(m => m.FullName).ToList();

        Assert.Contains(methodNames, n => n.Contains("Add"));
        Assert.Contains(methodNames, n => n.Contains("CalculatePremium"));
        Assert.Contains(methodNames, n => n.Contains("GetPricingMatrix"));
    }

    [Fact]
    public void DiscoverMethods_SkipsPropertyAccessors()
    {
        var discovery = new MethodDiscovery();
        var methods = discovery.DiscoverMethods(typeof(CalculatorService).Assembly);

        // Property accessors (get_CallCount) should be filtered out via IsSpecialName
        Assert.DoesNotContain(methods, m => m.FullName.Contains("get_CallCount"));
    }

    [Fact]
    public void DiscoverMethods_DetectsOperationContractAttribute()
    {
        var discovery = new MethodDiscovery();
        var methods = discovery.DiscoverMethods(typeof(CalculatorService).Assembly);

        var addMethod = methods.First(m => m.FullName.Contains(".Add"));
        Assert.Contains("OperationContract", addMethod.Attributes);
    }

    [Fact]
    public void GetServiceEndpoints_ReturnsOnlyAttributedMethods()
    {
        var discovery = new MethodDiscovery();
        discovery.DiscoverMethods(typeof(CalculatorService).Assembly);

        var endpoints = discovery.GetServiceEndpoints();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e =>
            Assert.True(e.Attributes.Any(a =>
                a is "ServiceContract" or "OperationContract")));
    }

    [Fact]
    public void GetDiscoveredMethods_NullFilter_ReturnsAll()
    {
        var discovery = new MethodDiscovery();
        var allMethods = discovery.DiscoverMethods(typeof(CalculatorService).Assembly);

        var filtered = discovery.GetDiscoveredMethods(null);

        Assert.Equal(allMethods.Count, filtered.Count);
    }
}

using NetArchTest.Rules;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Orders;
using Shouldly;
using System.Reflection;

namespace OrderProcessing.ArchitectureTests;

public class ArchitectureTests
{
    private const string DomainNs = "OrderProcessing.Domain";
    private const string ApplicationNs = "OrderProcessing.Application";
    private const string InfrastructureNs = "OrderProcessing.Infrastructure";
    private const string ApiNs = "OrderProcessing.Api";

    private static readonly Assembly Domain = typeof(Order).Assembly;
    private static readonly Assembly Application = typeof(IPaymentGateway).Assembly;
    private static readonly Assembly Infrastructure = typeof(OrderProcessing.Infrastructure.InfrastructureServiceCollectionExtensions).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private static void ShouldPass(TestResult result) =>
        result.IsSuccessful.ShouldBeTrue(
            "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []));

    [Fact]
    public void Domain_does_not_depend_on_other_layers_or_frameworks() =>
        ShouldPass(Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs, "Microsoft.AspNetCore", "Microsoft.Extensions")
            .GetResult());

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_or_Api() =>
        ShouldPass(Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs, "Microsoft.AspNetCore.Mvc")
            .GetResult());

    [Fact]
    public void Infrastructure_does_not_depend_on_Api() =>
        ShouldPass(Types.InAssembly(Infrastructure).ShouldNot().HaveDependencyOn(ApiNs).GetResult());

    [Fact]
    public void Only_Infrastructure_Payments_implements_IPaymentGateway() =>
        ShouldPass(Types.InAssemblies([Domain, Application, Infrastructure, Api])
            .That().ImplementInterface(typeof(IPaymentGateway))
            .Should().ResideInNamespace($"{InfrastructureNs}.Payments")
            .GetResult());

    [Fact]
    public void Only_Infrastructure_Persistence_implements_IOrderRepository() =>
        ShouldPass(Types.InAssemblies([Domain, Application, Infrastructure, Api])
            .That().ImplementInterface(typeof(IOrderRepository))
            .Should().ResideInNamespace($"{InfrastructureNs}.Persistence")
            .GetResult());

    [Fact]
    public void Controllers_do_not_depend_on_Infrastructure() =>
        ShouldPass(Types.InAssembly(Api)
            .That().ResideInNamespace($"{ApiNs}.Controllers")
            .ShouldNot().HaveDependencyOn(InfrastructureNs)
            .GetResult());

    [Fact]
    public void Rules_actually_find_types()
    {
        // Guards against vacuous passes if a namespace is renamed.
        Types.InAssembly(Api).That().ResideInNamespace($"{ApiNs}.Controllers").GetTypes().ShouldNotBeEmpty();
        Types.InAssembly(Infrastructure).That().ImplementInterface(typeof(IPaymentGateway)).GetTypes().Count().ShouldBe(2);
        Types.InAssembly(Infrastructure).That().ImplementInterface(typeof(IOrderRepository)).GetTypes().ShouldNotBeEmpty();
    }
}

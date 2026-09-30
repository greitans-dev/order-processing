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
    public void Domain_ShouldNotDependOnOtherLayersOrFrameworks() =>
        ShouldPass(Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs, "Microsoft.AspNetCore", "Microsoft.Extensions")
            .GetResult());

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrApi() =>
        ShouldPass(Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs, "Microsoft.AspNetCore.Mvc")
            .GetResult());

    [Fact]
    public void Infrastructure_ShouldNotDependOnApi() =>
        ShouldPass(Types.InAssembly(Infrastructure).ShouldNot().HaveDependencyOn(ApiNs).GetResult());

    [Fact]
    public void PaymentGatewayImplementations_ShouldResideOnlyInInfrastructurePayments() =>
        ShouldPass(Types.InAssemblies([Domain, Application, Infrastructure, Api])
            .That().ImplementInterface(typeof(IPaymentGateway))
            .Should().ResideInNamespace($"{InfrastructureNs}.Payments")
            .GetResult());

    [Fact]
    public void OrderRepositoryImplementations_ShouldResideOnlyInInfrastructurePersistence() =>
        ShouldPass(Types.InAssemblies([Domain, Application, Infrastructure, Api])
            .That().ImplementInterface(typeof(IOrderRepository))
            .Should().ResideInNamespace($"{InfrastructureNs}.Persistence")
            .GetResult());

    [Fact]
    public void Controllers_ShouldNotDependOnInfrastructure() =>
        ShouldPass(Types.InAssembly(Api)
            .That().ResideInNamespace($"{ApiNs}.Controllers")
            .ShouldNot().HaveDependencyOn(InfrastructureNs)
            .GetResult());

    [Fact]
    public void ArchitectureRules_ShouldFindTypesToInspect()
    {
        // Guards against vacuous passes if a namespace is renamed.
        Types.InAssembly(Api).That().ResideInNamespace($"{ApiNs}.Controllers").GetTypes().ShouldNotBeEmpty();
        Types.InAssembly(Infrastructure).That().ImplementInterface(typeof(IPaymentGateway)).GetTypes().ShouldNotBeEmpty();
        Types.InAssembly(Infrastructure).That().ImplementInterface(typeof(IOrderRepository)).GetTypes().ShouldNotBeEmpty();
    }
}

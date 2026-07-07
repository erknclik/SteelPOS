using FluentAssertions;
using NetArchTest.Rules;
using SanalPOS.Application;
using SanalPOS.Domain.Entities;
using Xunit;

namespace SanalPOS.Architecture.Tests;

/// <summary>Katman bağımlılık kuralları (bkz. docs/10-proje-klasor-yapisi.md §4).</summary>
public class LayerDependencyTests
{
    private const string DomainNamespace = "SanalPOS.Domain";
    private const string ApplicationNamespace = "SanalPOS.Application";
    private const string InfrastructureNamespace = "SanalPOS.Infrastructure";
    private const string ApiNamespace = "SanalPOS.API";

    [Fact]
    public void Domain_ShouldNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(typeof(PaymentTransaction).Assembly)
            .Should()
            .NotHaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Domain_ShouldNotDependOnFrameworks()
    {
        var result = Types.InAssembly(typeof(PaymentTransaction).Assembly)
            .Should()
            .NotHaveDependencyOnAny("MediatR", "Microsoft.EntityFrameworkCore", "NHibernate", "MassTransit", "FluentValidation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Application_ShouldNotDependOnConcreteOrmOrTransport()
    {
        var result = Types.InAssembly(typeof(DependencyInjection).Assembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "NHibernate", "MassTransit", "StackExchange.Redis", "RabbitMQ", "Confluent.Kafka")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    [Fact]
    public void Contracts_ShouldBeIndependent()
    {
        var result = Types.InAssembly(typeof(Contracts.PaymentCompletedEvent).Assembly)
            .Should()
            .NotHaveDependencyOnAny(DomainNamespace, ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailures(result));
    }

    private static string FormatFailures(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Kural ihlali: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? []);
}

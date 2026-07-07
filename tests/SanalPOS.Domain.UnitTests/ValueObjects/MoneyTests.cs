using FluentAssertions;
using SanalPOS.Domain.Exceptions;
using SanalPOS.Domain.ValueObjects;
using Xunit;

namespace SanalPOS.Domain.UnitTests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_NegativeAmount_ShouldThrow()
    {
        var act = () => new Money(-1, "TRY");
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("TL")]
    [InlineData("TRYX")]
    public void Constructor_InvalidCurrency_ShouldThrow(string currency)
    {
        var act = () => new Money(10, currency);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Constructor_ShouldRoundToTwoDecimals()
    {
        new Money(10.005m, "try").Amount.Should().Be(10.01m);
        new Money(10, "try").Currency.Should().Be("TRY");
    }

    [Fact]
    public void Add_DifferentCurrencies_ShouldThrow()
    {
        var act = () => new Money(10, "TRY").Add(new Money(5, "USD"));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Percentage_ShouldComputeCorrectly()
    {
        new Money(1250.50m, "TRY").Percentage(2.5m).Amount.Should().Be(31.26m);
    }

    [Fact]
    public void Equals_SameAmountAndCurrency_ShouldBeTrue()
    {
        new Money(10, "TRY").Should().Be(new Money(10, "TRY"));
    }
}

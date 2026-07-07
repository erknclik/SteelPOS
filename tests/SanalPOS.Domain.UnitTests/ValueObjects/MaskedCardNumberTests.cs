using FluentAssertions;
using SanalPOS.Domain.Exceptions;
using SanalPOS.Domain.ValueObjects;
using Xunit;

namespace SanalPOS.Domain.UnitTests.ValueObjects;

public class MaskedCardNumberTests
{
    [Fact]
    public void FromPan_ShouldMaskMiddleDigits()
    {
        var masked = MaskedCardNumber.FromPan("4021220000001234");

        masked.Value.Should().Be("4021 22** **** 1234");
        masked.Value.Should().NotContain("000000");
    }

    [Fact]
    public void FromPan_WithSpaces_ShouldNormalize()
    {
        MaskedCardNumber.FromPan("4021 2200 0000 1234").Value.Should().Be("4021 22** **** 1234");
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("12345678901234567890123")]
    public void FromPan_InvalidLength_ShouldThrow(string pan)
    {
        var act = () => MaskedCardNumber.FromPan(pan);
        act.Should().Throw<DomainException>();
    }
}

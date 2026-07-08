using FluentAssertions;
using SanalPOS.Infrastructure.Iso8583.Adapters;
using SanalPOS.Infrastructure.Iso8583.Diagnostics;
using SanalPOS.Infrastructure.Iso8583.Dialects;
using SanalPOS.Infrastructure.Iso8583.Messages;
using SanalPOS.Infrastructure.Iso8583.Network;
using Xunit;

namespace SanalPOS.Iso8583.UnitTests;

public class Iso8583MessageMaskerTests
{
    [Fact]
    public void Describe_MasksPanButKeepsBinAndLastFour()
    {
        var message = new Iso8583Message("0200")
        {
            [2] = "4546711234567894",
            [4] = "000000012550",
            [11] = "123456"
        };

        var text = Iso8583MessageMasker.Describe(message, Iso8583Dialects.Iso87Ascii);

        text.Should().Contain("DE2=454671******7894");
        text.Should().NotContain("4546711234567894");
        text.Should().Contain("DE4=000000012550", "hassas olmayan alanlar açık kalır");
    }

    [Fact]
    public void Describe_FullyMasksExpiryAndCvvAndTrack2Tail()
    {
        var message = new Iso8583Message("0200")
        {
            [14] = "2812",
            [35] = "4546711234567894D2812201123456789",
            [48] = "123"
        };

        var text = Iso8583MessageMasker.Describe(message, Iso8583Dialects.Iso87Ascii);

        text.Should().Contain("DE14=****");
        text.Should().Contain("DE48=***");
        text.Should().Contain("DE35=454671");
        text.Should().NotContain("2812");
        text.Should().NotContain("D2812201");
    }

    [Fact]
    public void Describe_MasksFieldsUnknownToDialect()
    {
        var message = new Iso8583Message("0200") { [59] = "gizli" };

        var text = Iso8583MessageMasker.Describe(message, Iso8583Dialects.Iso87Ascii);

        text.Should().Contain("DE59=*****");
    }
}

public class InMemoryStanSequenceTests
{
    [Fact]
    public void Next_ReturnsSixDigitZeroPaddedValues()
    {
        var sequence = new InMemoryStanSequence();

        var stan = sequence.Next();

        stan.Should().HaveLength(6).And.MatchRegex("^[0-9]{6}$");
    }

    [Fact]
    public void Next_NeverReturnsZeroAndDoesNotRepeatWithinRange()
    {
        var sequence = new InMemoryStanSequence();

        var values = Enumerable.Range(0, 5000).Select(_ => sequence.Next()).ToList();

        values.Should().NotContain("000000");
        values.Distinct().Should().HaveCount(values.Count);
    }

    [Fact]
    public async Task Next_IsThreadSafe()
    {
        var sequence = new InMemoryStanSequence();
        var bag = new System.Collections.Concurrent.ConcurrentBag<string>();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
                bag.Add(sequence.Next());
        })));

        bag.Distinct().Should().HaveCount(8000);
    }
}

public class Iso8583ResponseCodesTests
{
    [Theory]
    [InlineData("00", true)]
    [InlineData("05", false)]
    [InlineData(null, false)]
    public void IsApproved_OnlyForCodeZeroZero(string? code, bool expected) =>
        Iso8583ResponseCodes.IsApproved(code).Should().Be(expected);

    [Fact]
    public void MessageOf_UnknownCode_ReturnsGenericMessageWithCode() =>
        Iso8583ResponseCodes.MessageOf("XX").Should().Contain("XX");
}

public class Iso8583DialectRegistryTests
{
    [Fact]
    public void StandardDialects_AreRegisteredByDefault()
    {
        var registry = new Iso8583DialectRegistry();

        registry.Get("Iso87Ascii").Should().BeSameAs(Iso8583Dialects.Iso87Ascii);
        registry.Get("iso87bcd").Should().BeSameAs(Iso8583Dialects.Iso87Bcd, "dialekt adı büyük/küçük harf duyarsız");
    }

    [Fact]
    public void UnknownDialect_ThrowsWithRegisteredNames()
    {
        var registry = new Iso8583DialectRegistry();

        var act = () => registry.Get("AcmeBank");

        act.Should().Throw<InvalidOperationException>().WithMessage("*AcmeBank*Iso87Ascii*");
    }

    [Fact]
    public void BankSpecificDialect_CanBeRegisteredAsOverride()
    {
        var registry = new Iso8583DialectRegistry();
        registry.Register(Iso8583Dialects.Iso87Bcd.WithOverrides("AcmeBank"));

        registry.Get("AcmeBank").MtiFormat.Should().Be(Iso8583Dialects.Iso87Bcd.MtiFormat);
    }
}

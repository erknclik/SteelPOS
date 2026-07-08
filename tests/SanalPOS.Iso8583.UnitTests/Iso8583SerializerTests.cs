using System.Text;
using FluentAssertions;
using SanalPOS.Infrastructure.Iso8583.Dialects;
using SanalPOS.Infrastructure.Iso8583.Messages;
using SanalPOS.Infrastructure.Iso8583.Serialization;
using SanalPOS.Infrastructure.Iso8583.Spec;
using Xunit;

namespace SanalPOS.Iso8583.UnitTests;

public class Iso8583SerializerTests
{
    private static Iso8583Message SampleAuthorization(string mti = "0200")
    {
        return new Iso8583Message(mti)
        {
            [2] = "4546711234567894",
            [3] = "000000",
            [4] = "000000012550", // 125,50
            [7] = "0708143512",
            [11] = "123456",
            [12] = "173512",
            [13] = "0708",
            [14] = "2812",
            [22] = "010",
            [25] = "59",
            [37] = "618914123456",
            [41] = "TERM0001",
            [42] = "000000000000001",
            [49] = "949"
        };
    }

    [Fact]
    public void AsciiDialect_RoundTrip_PreservesAllFields()
    {
        var original = SampleAuthorization();

        var bytes = Iso8583Serializer.Serialize(original, Iso8583Dialects.Iso87Ascii);
        var parsed = Iso8583Serializer.Deserialize(bytes, Iso8583Dialects.Iso87Ascii);

        parsed.Mti.Should().Be("0200");
        parsed.Fields.Should().BeEquivalentTo(original.Fields);
    }

    [Fact]
    public void BcdDialect_RoundTrip_PreservesAllFields()
    {
        var original = SampleAuthorization();

        var bytes = Iso8583Serializer.Serialize(original, Iso8583Dialects.Iso87Bcd);
        var parsed = Iso8583Serializer.Deserialize(bytes, Iso8583Dialects.Iso87Bcd);

        parsed.Fields.Should().BeEquivalentTo(original.Fields);
    }

    [Fact]
    public void BcdDialect_IsMoreCompactThanAscii()
    {
        var message = SampleAuthorization();

        var ascii = Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Ascii);
        var bcd = Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Bcd);

        bcd.Length.Should().BeLessThan(ascii.Length);
    }

    [Fact]
    public void AsciiDialect_ProducesExpectedWireFormatPrefix()
    {
        var message = new Iso8583Message("0800")
        {
            [11] = "000001",
            [70] = "301"
        };

        var bytes = Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Ascii);
        var text = Encoding.ASCII.GetString(bytes);

        // MTI + 16 hex bitmap + DE11 + DE70. Bit 11 ve 70 set: ikincil bitmap gerekir.
        text.Should().StartWith("0800");
        text.Should().EndWith("000001" + "301");
        bytes.Length.Should().Be(4 + 32 + 6 + 3); // MTI + iki hex bitmap + alanlar
    }

    [Fact]
    public void SecondaryBitmap_IsEmittedOnlyWhenFieldAbove64Present()
    {
        var withoutSecondary = new Iso8583Message("0800") { [11] = "000001" };
        var withSecondary = new Iso8583Message("0800") { [11] = "000001", [70] = "301" };

        var plain = Iso8583Serializer.Serialize(withoutSecondary, Iso8583Dialects.Iso87Ascii);
        var extended = Iso8583Serializer.Serialize(withSecondary, Iso8583Dialects.Iso87Ascii);

        plain.Length.Should().Be(4 + 16 + 6);
        Encoding.ASCII.GetString(extended, 4, 1).Should().Be("8", "ikincil bitmap biti (bit 1) set olmalı");
    }

    [Fact]
    public void FixedNumericField_IsLeftPaddedWithZeros()
    {
        var message = new Iso8583Message("0200") { [4] = "12550", [11] = "1" };

        var parsed = Iso8583Serializer.Deserialize(
            Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Ascii),
            Iso8583Dialects.Iso87Ascii);

        parsed[4].Should().Be("000000012550");
        parsed[11].Should().Be("000001");
    }

    [Fact]
    public void FixedAlphaNumericField_IsRightPaddedWithSpaces()
    {
        var message = new Iso8583Message("0210") { [11] = "000001", [39] = "0" };

        var bytes = Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Ascii);
        var parsed = Iso8583Serializer.Deserialize(bytes, Iso8583Dialects.Iso87Ascii);

        parsed[39].Should().Be("0 ");
    }

    [Fact]
    public void LlvarField_CarriesActualLengthNotMax()
    {
        var message = new Iso8583Message("0200") { [2] = "4546711234567894", [11] = "000001" };

        var text = Encoding.ASCII.GetString(Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Ascii));

        text.Should().Contain("164546711234567894", "PAN 16 haneli olduğu için LLVAR başlığı '16' olmalı");
    }

    [Fact]
    public void BinaryLllvarField_RoundTripsAsHex()
    {
        var message = new Iso8583Message("0200")
        {
            [11] = "000001",
            [55] = "9F2608AABBCCDDEEFF0011"
        };

        var parsed = Iso8583Serializer.Deserialize(
            Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Ascii),
            Iso8583Dialects.Iso87Ascii);

        parsed[55].Should().Be("9F2608AABBCCDDEEFF0011");
    }

    [Fact]
    public void OddLengthBcdField_RoundTrips()
    {
        // DE22 (3 hane) BCD'de yarım byte pad gerektirir.
        var message = new Iso8583Message("0200") { [11] = "000001", [22] = "010" };

        var parsed = Iso8583Serializer.Deserialize(
            Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Bcd),
            Iso8583Dialects.Iso87Bcd);

        parsed[22].Should().Be("010");
    }

    [Fact]
    public void Track2WithSeparator_RoundTripsInBcdDialect()
    {
        var message = new Iso8583Message("0200") { [11] = "000001", [35] = "4546711234567894D28122011234567890123" };

        var parsed = Iso8583Serializer.Deserialize(
            Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Bcd),
            Iso8583Dialects.Iso87Bcd);

        parsed[35].Should().Be("4546711234567894D28122011234567890123");
    }

    [Fact]
    public void ValueLongerThanMax_Throws()
    {
        var message = new Iso8583Message("0200") { [2] = new string('4', 20), [11] = "000001" };

        var act = () => Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Ascii);

        act.Should().Throw<Iso8583Exception>().WithMessage("*DE2*en fazla 19*");
    }

    [Fact]
    public void NonNumericValueInNumericField_Throws()
    {
        var message = new Iso8583Message("0200") { [4] = "12AB", [11] = "000001" };

        var act = () => Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Ascii);

        act.Should().Throw<Iso8583Exception>().WithMessage("*DE4*");
    }

    [Fact]
    public void UndefinedFieldInDialect_Throws()
    {
        var message = new Iso8583Message("0200") { [11] = "000001", [59] = "x" };

        var act = () => Iso8583Serializer.Serialize(message, Iso8583Dialects.Iso87Ascii);

        act.Should().Throw<Iso8583Exception>().WithMessage("*DE59*tanımlı değil*");
    }

    [Fact]
    public void TruncatedMessage_ThrowsInsteadOfHanging()
    {
        var bytes = Iso8583Serializer.Serialize(SampleAuthorization(), Iso8583Dialects.Iso87Ascii);

        var act = () => Iso8583Serializer.Deserialize(bytes.AsSpan(0, bytes.Length - 5).ToArray(), Iso8583Dialects.Iso87Ascii);

        act.Should().Throw<Iso8583Exception>().WithMessage("*kısa*");
    }

    [Fact]
    public void TrailingGarbage_Throws()
    {
        var bytes = Iso8583Serializer.Serialize(SampleAuthorization(), Iso8583Dialects.Iso87Ascii).Concat(new byte[] { 0x00 }).ToArray();

        var act = () => Iso8583Serializer.Deserialize(bytes, Iso8583Dialects.Iso87Ascii);

        act.Should().Throw<Iso8583Exception>().WithMessage("*beklenmeyen*");
    }

    [Fact]
    public void ResponseMti_IsDerivedFromRequestMti()
    {
        Iso8583Message.ResponseMtiOf("0200").Should().Be("0210");
        Iso8583Message.ResponseMtiOf("0100").Should().Be("0110");
        Iso8583Message.ResponseMtiOf("0400").Should().Be("0410");
        Iso8583Message.ResponseMtiOf("0800").Should().Be("0810");
    }

    [Fact]
    public void DialectOverride_ChangesOnlyTargetedField()
    {
        var custom = Iso8583Dialects.Iso87Ascii.WithOverrides(
            "TestBank",
            new FieldSpec(48, "CVV2", Iso8583Content.AlphaNumeric, Iso8583LengthKind.LLVar, 10, Iso8583BodyEncoding.Ascii, Sensitive: true));

        custom.Name.Should().Be("TestBank");
        custom.GetField(48).LengthKind.Should().Be(Iso8583LengthKind.LLVar);
        custom.GetField(2).Should().Be(Iso8583Dialects.Iso87Ascii.GetField(2));

        var message = new Iso8583Message("0200") { [11] = "000001", [48] = "123" };
        var parsed = Iso8583Serializer.Deserialize(Iso8583Serializer.Serialize(message, custom), custom);
        parsed[48].Should().Be("123");
    }
}

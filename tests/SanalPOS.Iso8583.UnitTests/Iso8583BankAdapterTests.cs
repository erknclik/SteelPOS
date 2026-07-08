using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Infrastructure.Iso8583.Adapters;
using SanalPOS.Infrastructure.Iso8583.Messages;
using SanalPOS.Infrastructure.Iso8583.Network;
using Xunit;

namespace SanalPOS.Iso8583.UnitTests;

public class Iso8583BankAdapterTests
{
    private sealed class FakeChannel : IIso8583Channel
    {
        public List<Iso8583Message> Sent { get; } = new();
        public Func<Iso8583Message, Iso8583Message> Responder { get; set; } = ApproveAll;

        public Task<Iso8583Message> SendAsync(Iso8583Message request, CancellationToken ct = default)
        {
            Sent.Add(request);
            return Task.FromResult(Responder(request));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public static Iso8583Message ApproveAll(Iso8583Message request) =>
            new(Iso8583Message.ResponseMtiOf(request.Mti))
            {
                [11] = request[11],
                [38] = "A12345",
                [39] = "00"
            };

        public static Func<Iso8583Message, Iso8583Message> DeclineWith(string code) => request =>
            new(Iso8583Message.ResponseMtiOf(request.Mti))
            {
                [11] = request[11],
                [39] = code
            };
    }

    private static readonly Iso8583BankOptions Options = new()
    {
        ProviderCode = "TESTBANK",
        Dialect = "Iso87Ascii",
        TerminalId = "TERM0001",
        MerchantId = "000000000000001",
        MerchantNameLocation = "SanalPOS Test Merchant Istanbul TR"
    };

    private static ChargeRequest SampleCharge(decimal amount = 125.50m, int installments = 1) => new(
        "4546711234567894", "AHMET YILMAZ", 12, 2028, "123", amount, "TRY", installments, "ORDER-42");

    private static Iso8583BankAdapter CreateAdapter(FakeChannel channel) => new(
        Options, channel, new InMemoryStanSequence(), TimeProvider.System, NullLogger<Iso8583BankAdapter>.Instance);

    [Fact]
    public async Task Charge_BuildsSaleMessageWithMandatoryFields()
    {
        var channel = new FakeChannel();
        var adapter = CreateAdapter(channel);

        var result = await adapter.ChargeAsync(SampleCharge());

        result.IsApproved.Should().BeTrue();
        result.AuthCode.Should().Be("A12345");

        var sent = channel.Sent.Single();
        sent.Mti.Should().Be("0200");
        sent[2].Should().Be("4546711234567894");
        sent[3].Should().Be("000000");
        sent[4].Should().Be("000000012550");
        sent[14].Should().Be("2812");
        sent[41].Should().Be("TERM0001");
        sent[42].Should().Be("000000000000001");
        sent[48].Should().Be("123");
        sent[49].Should().Be("949");
        sent[62].Should().Be("ORDER-42");
        sent[11].Should().HaveLength(6);
        sent[37].Should().HaveLength(12);
        sent.Has(67).Should().BeFalse("tek çekimde taksit alanı gönderilmez");
    }

    [Fact]
    public async Task PreAuth_UsesAuthorizationMti()
    {
        var channel = new FakeChannel();

        await CreateAdapter(channel).PreAuthAsync(SampleCharge());

        channel.Sent.Single().Mti.Should().Be("0100");
    }

    [Fact]
    public async Task Charge_WithInstallments_SendsExtendedPaymentCode()
    {
        var channel = new FakeChannel();

        await CreateAdapter(channel).ChargeAsync(SampleCharge(installments: 6));

        channel.Sent.Single()[67].Should().Be("06");
    }

    [Fact]
    public async Task Charge_WhenDeclined_MapsResponseCodeToTurkishMessage()
    {
        var channel = new FakeChannel { Responder = FakeChannel.DeclineWith("51") };

        var result = await CreateAdapter(channel).ChargeAsync(SampleCharge());

        result.IsApproved.Should().BeFalse();
        result.ReasonCode.Should().Be("51");
        result.ReasonMessage.Should().Be("Yetersiz bakiye / limit.");
    }

    [Fact]
    public async Task Charge_OnTimeout_SendsReversalAndReturnsDeclined()
    {
        var channel = new FakeChannel
        {
            Responder = request => request.Mti == "0400"
                ? FakeChannel.ApproveAll(request)
                : throw new Iso8583TimeoutException("timeout")
        };

        var result = await CreateAdapter(channel).ChargeAsync(SampleCharge());

        result.IsApproved.Should().BeFalse();
        result.ReasonCode.Should().Be("TIMEOUT");

        channel.Sent.Should().HaveCount(2);
        var reversal = channel.Sent[1];
        var original = channel.Sent[0];
        reversal.Mti.Should().Be("0400");
        reversal[4].Should().Be(original[4]);
        reversal[37].Should().Be(original[37]);
        reversal[90].Should().StartWith("0200" + original[11]).And.HaveLength(42);
    }

    [Fact]
    public async Task Refund_UsesCreditProcessingCode()
    {
        var channel = new FakeChannel();

        var result = await CreateAdapter(channel).RefundAsync("A12345", 50m);

        result.IsSuccessful.Should().BeTrue();
        var sent = channel.Sent.Single();
        sent.Mti.Should().Be("0200");
        sent[3].Should().Be("200000");
        sent[4].Should().Be("000000005000");
        sent[38].Should().Be("A12345");
    }

    [Fact]
    public async Task Void_UsesReversalMtiAndVoidProcessingCode()
    {
        var channel = new FakeChannel();

        await CreateAdapter(channel).VoidAsync("A12345");

        var sent = channel.Sent.Single();
        sent.Mti.Should().Be("0400");
        sent[3].Should().Be("020000");
    }

    [Fact]
    public async Task Capture_SendsAdviceWithAmountAndAuthCode()
    {
        var channel = new FakeChannel();

        await CreateAdapter(channel).CaptureAsync("A12345", 125.50m);

        var sent = channel.Sent.Single();
        sent.Mti.Should().Be("0220");
        sent[4].Should().Be("000000012550");
        sent[38].Should().Be("A12345");
    }

    [Fact]
    public async Task Operation_WhenDeclined_ReturnsMappedReason()
    {
        var channel = new FakeChannel { Responder = FakeChannel.DeclineWith("25") };

        var result = await CreateAdapter(channel).VoidAsync("A12345");

        result.IsSuccessful.Should().BeFalse();
        result.ReasonCode.Should().Be("25");
        result.ReasonMessage.Should().Be("Kayıt bulunamadı.");
    }

    [Fact]
    public async Task UnsupportedCurrency_Throws()
    {
        var adapter = CreateAdapter(new FakeChannel());
        var request = SampleCharge() with { Currency = "JPY" };

        var act = () => adapter.ChargeAsync(request);

        await act.Should().ThrowAsync<SanalPOS.Infrastructure.Iso8583.Spec.Iso8583Exception>();
    }
}

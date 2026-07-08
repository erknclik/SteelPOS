using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SanalPOS.Infrastructure.Iso8583.Dialects;
using SanalPOS.Infrastructure.Iso8583.Messages;
using SanalPOS.Infrastructure.Iso8583.Network;
using SanalPOS.Infrastructure.Iso8583.Serialization;
using SanalPOS.Infrastructure.Iso8583.Spec;
using Xunit;

namespace SanalPOS.Iso8583.UnitTests;

/// <summary>Loopback TCP üzerinde sahte banka host'u ile kanal davranış testleri.</summary>
public class TcpIso8583ChannelTests : IAsyncLifetime
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _serverCts = new();
    private Task? _serverTask;
    private int _port;

    /// <summary>Gelen her isteğe verilecek yanıtı üretir; null dönerse yanıt gönderilmez (timeout senaryosu).</summary>
    private Func<Iso8583Message, Iso8583Message?> _bankLogic = request =>
        new Iso8583Message(Iso8583Message.ResponseMtiOf(request.Mti))
        {
            [11] = request[11],
            [39] = "00"
        };

    public Task InitializeAsync()
    {
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serverTask = RunFakeBankAsync(_serverCts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _serverCts.Cancel();
        _listener.Stop();
        if (_serverTask is not null)
            await Task.WhenAny(_serverTask, Task.Delay(1000));
    }

    private async Task RunFakeBankAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(ct);
                var stream = client.GetStream();

                while (!ct.IsCancellationRequested)
                {
                    var header = new byte[2];
                    await stream.ReadExactlyAsync(header, ct);
                    var body = new byte[BinaryPrimitives.ReadUInt16BigEndian(header)];
                    await stream.ReadExactlyAsync(body, ct);

                    var request = Iso8583Serializer.Deserialize(body, Iso8583Dialects.Iso87Ascii);
                    var response = _bankLogic(request);
                    if (response is null)
                        continue;

                    var payload = Iso8583Serializer.Serialize(response, Iso8583Dialects.Iso87Ascii);
                    var responseHeader = new byte[2];
                    BinaryPrimitives.WriteUInt16BigEndian(responseHeader, (ushort)payload.Length);
                    await stream.WriteAsync(responseHeader, ct);
                    await stream.WriteAsync(payload, ct);
                }
            }
        }
        catch (Exception)
        {
            // Test sonu: dinleyici kapandı veya istemci ayrıldı.
        }
    }

    private TcpIso8583Channel CreateChannel(TimeSpan? responseTimeout = null) => new(
        new Iso8583ChannelOptions
        {
            Host = "127.0.0.1",
            Port = _port,
            ResponseTimeout = responseTimeout ?? TimeSpan.FromSeconds(5)
        },
        Iso8583Dialects.Iso87Ascii,
        NullLogger<TcpIso8583Channel>.Instance);

    private static Iso8583Message EchoRequest(string stan) => new("0800")
    {
        [11] = stan,
        [70] = "301"
    };

    [Fact]
    public async Task SendAsync_ReceivesMatchingResponse()
    {
        await using var channel = CreateChannel();

        var response = await channel.SendAsync(EchoRequest("000001"));

        response.Mti.Should().Be("0810");
        response[39].Should().Be("00");
    }

    [Fact]
    public async Task SendAsync_CorrelatesConcurrentRequestsByStan()
    {
        // Sahte banka yanıtları ters sırada göndersin diye küçük bir gecikme ekler.
        _bankLogic = request =>
        {
            if (request[11] == "000001")
                Thread.Sleep(150);
            return new Iso8583Message(Iso8583Message.ResponseMtiOf(request.Mti))
            {
                [11] = request[11],
                [39] = "00",
                [37] = "ECHO" + request[11]
            };
        };

        await using var channel = CreateChannel();

        var first = channel.SendAsync(EchoRequest("000001"));
        var second = channel.SendAsync(EchoRequest("000002"));
        var responses = await Task.WhenAll(first, second);

        responses[0][37]!.TrimEnd().Should().Be("ECHO000001");
        responses[1][37]!.TrimEnd().Should().Be("ECHO000002");
    }

    [Fact]
    public async Task SendAsync_WhenBankStaysSilent_ThrowsTimeout()
    {
        _bankLogic = _ => null;
        await using var channel = CreateChannel(responseTimeout: TimeSpan.FromMilliseconds(300));

        var act = () => channel.SendAsync(EchoRequest("000003"));

        await act.Should().ThrowAsync<Iso8583TimeoutException>();
    }

    [Fact]
    public async Task SendAsync_WhenHostUnreachable_ThrowsConnectError()
    {
        var channel = new TcpIso8583Channel(
            new Iso8583ChannelOptions { Host = "127.0.0.1", Port = 1, ConnectTimeout = TimeSpan.FromSeconds(2) },
            Iso8583Dialects.Iso87Ascii,
            NullLogger<TcpIso8583Channel>.Instance);
        await using var _ = channel;

        var act = () => channel.SendAsync(EchoRequest("000004"));

        await act.Should().ThrowAsync<Iso8583Exception>().Where(e => e.Message.Contains("bağlanılamadı"));
    }

    [Fact]
    public async Task SendAsync_ReconnectsAfterServerRestart()
    {
        await using var channel = CreateChannel();
        (await channel.SendAsync(EchoRequest("000005"))).Mti.Should().Be("0810");

        // Sunucu tarafı bağlantıyı koparır; kanal bir sonraki istekte yeniden bağlanmalı.
        _serverCts.Cancel();
        _listener.Stop();
        await Task.Delay(100);

        _listener.Server.Dispose();
        var revived = new TcpListener(IPAddress.Loopback, _port);
        revived.Start();
        var reviveTask = ReviveServerAsync(revived);

        try
        {
            // İlk deneme kopuşa denk gelebilir; bir kez daha denemek meşru.
            Iso8583Message response;
            try
            {
                response = await channel.SendAsync(EchoRequest("000006"));
            }
            catch (Iso8583Exception)
            {
                response = await channel.SendAsync(EchoRequest("000007"));
            }

            response.Mti.Should().Be("0810");
        }
        finally
        {
            revived.Stop();
            await Task.WhenAny(reviveTask, Task.Delay(1000));
        }
    }

    private static async Task ReviveServerAsync(TcpListener listener)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            while (true)
            {
                var header = new byte[2];
                await stream.ReadExactlyAsync(header);
                var body = new byte[BinaryPrimitives.ReadUInt16BigEndian(header)];
                await stream.ReadExactlyAsync(body);

                var request = Iso8583Serializer.Deserialize(body, Iso8583Dialects.Iso87Ascii);
                var payload = Iso8583Serializer.Serialize(
                    new Iso8583Message(Iso8583Message.ResponseMtiOf(request.Mti)) { [11] = request[11], [39] = "00" },
                    Iso8583Dialects.Iso87Ascii);

                var responseHeader = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(responseHeader, (ushort)payload.Length);
                await stream.WriteAsync(responseHeader);
                await stream.WriteAsync(payload);
            }
        }
        catch
        {
            // Test bitti.
        }
    }
}

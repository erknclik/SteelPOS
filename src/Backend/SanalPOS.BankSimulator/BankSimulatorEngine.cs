using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using SanalPOS.Infrastructure.Iso8583.Diagnostics;
using SanalPOS.Infrastructure.Iso8583.Messages;
using SanalPOS.Infrastructure.Iso8583.Serialization;
using SanalPOS.Infrastructure.Iso8583.Spec;

namespace SanalPOS.BankSimulator;

/// <summary>
/// ISO 8583 konuşan sahte banka host'u. Sertifikasyon/geliştirme senaryolarını
/// deterministik kurallarla üretir (bkz. Scenarios sınıfı). Çoklu eşzamanlı
/// bağlantıyı destekler; 2-byte big-endian uzunluk çerçevesi kullanır.
/// Hem konsol uygulaması (Program) hem de entegrasyon testleri in-process kullanır.
/// </summary>
public sealed class BankSimulatorEngine : IAsyncDisposable
{
    private readonly Iso8583Spec _spec;
    private readonly ILogger _logger;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly SimulatorLedger _ledger = new();
    private Task? _acceptLoop;

    public BankSimulatorEngine(Iso8583Spec spec, ILogger logger, int port = 0)
    {
        _spec = spec;
        _logger = logger;
        _listener = new TcpListener(IPAddress.Any, port);
    }

    /// <summary>Dinlenen gerçek port (0 ile başlatıldıysa işletim sistemi atar).</summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        _listener.Start();
        _acceptLoop = AcceptLoopAsync(_cts.Token);
        _logger.LogInformation("Banka simülatörü dinliyor: port {Port}, dialekt '{Dialect}'.", Port, _spec.Name);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Kapanış.
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        var remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
        _logger.LogInformation("Bağlantı alındı: {Remote}", remote);

        try
        {
            using (client)
            {
                var stream = client.GetStream();
                while (!ct.IsCancellationRequested)
                {
                    var request = await ReadMessageAsync(stream, ct);
                    _logger.LogInformation("İstek: {Message}", Iso8583MessageMasker.Describe(request, _spec));

                    var response = Scenarios.RespondTo(request, _ledger);
                    if (response is null)
                    {
                        _logger.LogWarning("Senaryo gereği yanıt gönderilmiyor (timeout simülasyonu). STAN: {Stan}", request[11]);
                        continue;
                    }

                    await WriteMessageAsync(stream, response, ct);
                    _logger.LogInformation("Yanıt: {Message}", Iso8583MessageMasker.Describe(response, _spec));
                }
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or OperationCanceledException or ObjectDisposedException)
        {
            _logger.LogInformation("Bağlantı kapandı: {Remote}", remote);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bağlantı hatası: {Remote}", remote);
        }
    }

    private async Task<Iso8583Message> ReadMessageAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[2];
        await ReadExactAsync(stream, header, ct);
        var body = new byte[BinaryPrimitives.ReadUInt16BigEndian(header)];
        await ReadExactAsync(stream, body, ct);
        return Iso8583Serializer.Deserialize(body, _spec);
    }

    private async Task WriteMessageAsync(Stream stream, Iso8583Message message, CancellationToken ct)
    {
        var payload = Iso8583Serializer.Serialize(message, _spec);
        var header = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(header, checked((ushort)payload.Length));
        await stream.WriteAsync(header, ct);
        await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), ct);
            if (n == 0)
                throw new EndOfStreamException();
            read += n;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        if (_acceptLoop is not null)
            await Task.WhenAny(_acceptLoop, Task.Delay(1000));
        _cts.Dispose();
    }
}

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using SanalPOS.Infrastructure.Iso8583.Diagnostics;
using SanalPOS.Infrastructure.Iso8583.Messages;
using SanalPOS.Infrastructure.Iso8583.Serialization;
using SanalPOS.Infrastructure.Iso8583.Spec;

namespace SanalPOS.Infrastructure.Iso8583.Network;

/// <summary>ISO 8583 istek/yanıt kanalı. Adaptörler bu soyutlama üzerinden konuşur (test edilebilirlik).</summary>
public interface IIso8583Channel : IAsyncDisposable
{
    Task<Iso8583Message> SendAsync(Iso8583Message request, CancellationToken ct = default);
}

/// <summary>
/// Kalıcı TCP bağlantısı üzerinden ISO 8583 mesajlaşması. Banka host'ları tipik olarak
/// tek soket üzerinden asenkron (sıra garantisi olmayan) yanıt döndürür; bu yüzden
/// yanıtlar arka plandaki okuma döngüsünde STAN (DE11) + yanıt MTI anahtarıyla eşleştirilir.
/// Bağlantı koptuğunda bekleyen tüm istekler hata ile sonuçlanır ve bir sonraki istek
/// yeniden bağlanır.
/// </summary>
public sealed class TcpIso8583Channel : IIso8583Channel
{
    private readonly Iso8583ChannelOptions _options;
    private readonly Iso8583Spec _spec;
    private readonly ILogger _logger;
    private readonly byte[] _tpdu;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Iso8583Message>> _pending = new();

    private TcpClient? _client;
    private Stream? _stream;
    private CancellationTokenSource? _readLoopCts;
    private bool _disposed;

    public TcpIso8583Channel(Iso8583ChannelOptions options, Iso8583Spec spec, ILogger<TcpIso8583Channel> logger)
    {
        _options = options;
        _spec = spec;
        _logger = logger;
        _tpdu = string.IsNullOrWhiteSpace(options.TpduHeaderHex)
            ? Array.Empty<byte>()
            : Convert.FromHexString(options.TpduHeaderHex);
    }

    public async Task<Iso8583Message> SendAsync(Iso8583Message request, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var stan = request.GetRequired(11);
        var correlationKey = BuildKey(Iso8583Message.ResponseMtiOf(request.Mti), stan);

        var tcs = new TaskCompletionSource<Iso8583Message>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(correlationKey, tcs))
            throw new Iso8583Exception($"Aynı STAN ({stan}) ile bekleyen başka bir istek var; STAN üreteci çakışıyor.");

        try
        {
            var stream = await EnsureConnectedAsync(ct);
            var payload = Iso8583Serializer.Serialize(request, _spec);

            _logger.LogDebug("ISO 8583 gönderiliyor ({Host}:{Port}): {Message}",
                _options.Host, _options.Port, Iso8583MessageMasker.Describe(request, _spec));

            await WriteFrameAsync(stream, payload, ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_options.ResponseTimeout);

            try
            {
                return await tcs.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new Iso8583TimeoutException(
                    $"Banka yanıtı {_options.ResponseTimeout.TotalSeconds:0} sn içinde gelmedi (MTI {request.Mti}, STAN {stan}).");
            }
        }
        finally
        {
            _pending.TryRemove(correlationKey, out _);
        }
    }

    private async Task<Stream> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_stream is { } existing && _client is { Connected: true })
            return existing;

        await _connectLock.WaitAsync(ct);
        try
        {
            if (_stream is { } current && _client is { Connected: true })
                return current;

            // Ölü taşıma katmanı kapatılır; bekleyen istekler (bu isteğin kendisi dahil,
            // SendAsync TCS'i bağlanmadan önce kaydeder) burada DÜŞÜRÜLMEZ.
            await CloseTransportAsync();

            var client = new TcpClient { NoDelay = true };
            using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                connectCts.CancelAfter(_options.ConnectTimeout);
                try
                {
                    await client.ConnectAsync(_options.Host, _options.Port, connectCts.Token);
                }
                catch (Exception ex)
                {
                    client.Dispose();
                    throw new Iso8583Exception($"Bankaya bağlanılamadı ({_options.Host}:{_options.Port}).", ex);
                }
            }

            Stream stream = client.GetStream();
            if (_options.UseTls)
            {
                var ssl = new SslStream(stream, leaveInnerStreamOpen: false);
                try
                {
                    await ssl.AuthenticateAsClientAsync(
                        new SslClientAuthenticationOptions { TargetHost = _options.TlsServerName ?? _options.Host }, ct);
                }
                catch (Exception ex)
                {
                    await ssl.DisposeAsync();
                    client.Dispose();
                    throw new Iso8583Exception($"TLS el sıkışması başarısız ({_options.Host}:{_options.Port}).", ex);
                }

                stream = ssl;
            }

            _client = client;
            _stream = stream;
            _readLoopCts = new CancellationTokenSource();
            _ = ReadLoopAsync(stream, _readLoopCts.Token);

            _logger.LogInformation("ISO 8583 bağlantısı kuruldu: {Host}:{Port} (TLS: {Tls})",
                _options.Host, _options.Port, _options.UseTls);

            return stream;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task ReadLoopAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var payload = await ReadFrameAsync(stream, ct);
                DispatchResponse(payload);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "ISO 8583 okuma döngüsü sonlandı ({Host}:{Port}); bağlantı kapatılıyor.",
                _options.Host, _options.Port);
            await CloseConnectionAsync(new Iso8583Exception("Banka bağlantısı koptu.", ex));
        }
    }

    private void DispatchResponse(byte[] payload)
    {
        Iso8583Message response;
        try
        {
            response = Iso8583Serializer.Deserialize(payload, _spec);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Banka yanıtı çözümlenemedi ({Length} byte); yanıt yok sayıldı.", payload.Length);
            return;
        }

        _logger.LogDebug("ISO 8583 alındı ({Host}:{Port}): {Message}",
            _options.Host, _options.Port, Iso8583MessageMasker.Describe(response, _spec));

        var stan = response[11];
        if (stan is null || !_pending.TryRemove(BuildKey(response.Mti, stan), out var tcs))
        {
            _logger.LogWarning("Eşleşmeyen banka yanıtı: MTI {Mti}, STAN {Stan}.", response.Mti, stan ?? "-");
            return;
        }

        tcs.TrySetResult(response);
    }

    private async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken ct)
    {
        var frameBody = _tpdu.Length == 0 ? payload : Combine(_tpdu, payload);
        var header = EncodeFrameLength(frameBody.Length);

        await _writeLock.WaitAsync(ct);
        try
        {
            await stream.WriteAsync(header, ct);
            await stream.WriteAsync(frameBody, ct);
            await stream.FlushAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await CloseConnectionAsync(new Iso8583Exception("Bankaya yazma hatası; bağlantı kapatıldı.", ex));
            throw new Iso8583Exception("Mesaj bankaya gönderilemedi.", ex);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var headerLength = _options.FrameLengthFormat == FrameLengthFormat.TwoByteBinary ? 2 : 4;
        var header = await ReadExactAsync(stream, headerLength, ct);
        var frameLength = DecodeFrameLength(header);

        if (frameLength <= _tpdu.Length || frameLength > 65_535)
            throw new Iso8583Exception($"Geçersiz çerçeve uzunluğu: {frameLength}.");

        var body = await ReadExactAsync(stream, frameLength, ct);
        return _tpdu.Length == 0 ? body : body[_tpdu.Length..];
    }

    private byte[] EncodeFrameLength(int length)
    {
        if (_options.FrameLengthFormat == FrameLengthFormat.FourAsciiDigits)
        {
            if (length > 9999)
                throw new Iso8583Exception($"Mesaj 4 haneli ASCII uzunluk başlığına sığmıyor: {length} byte.");
            return Encoding.ASCII.GetBytes(length.ToString("D4"));
        }

        var header = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(header, checked((ushort)length));
        return header;
    }

    private int DecodeFrameLength(byte[] header)
    {
        if (_options.FrameLengthFormat == FrameLengthFormat.FourAsciiDigits)
        {
            var text = Encoding.ASCII.GetString(header);
            return int.TryParse(text, out var length)
                ? length
                : throw new Iso8583Exception($"ASCII çerçeve uzunluğu sayısal değil: '{text}'.");
        }

        return BinaryPrimitives.ReadUInt16BigEndian(header);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, count - read), ct);
            if (n == 0)
                throw new Iso8583Exception("Banka bağlantıyı kapattı (EOF).");
            read += n;
        }

        return buffer;
    }

    private async Task CloseConnectionAsync(Exception reason)
    {
        await CloseTransportAsync();

        // Kopan bağlantının yanıtı asla gelmeyecek; bekleyenleri hemen hata ile sonlandır.
        foreach (var key in _pending.Keys.ToArray())
        {
            if (_pending.TryRemove(key, out var tcs))
                tcs.TrySetException(reason);
        }
    }

    private async Task CloseTransportAsync()
    {
        var cts = Interlocked.Exchange(ref _readLoopCts, null);
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        var stream = Interlocked.Exchange(ref _stream, null);
        var client = Interlocked.Exchange(ref _client, null);

        if (stream is not null)
            await stream.DisposeAsync();
        client?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await CloseConnectionAsync(new ObjectDisposedException(nameof(TcpIso8583Channel)));
        _connectLock.Dispose();
        _writeLock.Dispose();
    }

    private static string BuildKey(string mti, string stan) => $"{mti}:{stan.TrimStart('0').PadLeft(1, '0')}";

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var combined = new byte[first.Length + second.Length];
        first.CopyTo(combined, 0);
        second.CopyTo(combined, first.Length);
        return combined;
    }
}

/// <summary>Banka yanıtı zaman aşımı; adaptör bu durumda otomatik reversal (0400) dener.</summary>
public sealed class Iso8583TimeoutException : Iso8583Exception
{
    public Iso8583TimeoutException(string message) : base(message)
    {
    }
}

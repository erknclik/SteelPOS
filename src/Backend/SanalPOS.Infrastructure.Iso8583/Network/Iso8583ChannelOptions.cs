namespace SanalPOS.Infrastructure.Iso8583.Network;

/// <summary>TCP çerçevesindeki mesaj uzunluğu başlığının formatı.</summary>
public enum FrameLengthFormat
{
    /// <summary>2 byte big-endian binary uzunluk (en yaygın form).</summary>
    TwoByteBinary,

    /// <summary>4 ASCII rakam uzunluk (bazı banka simülatörleri).</summary>
    FourAsciiDigits
}

/// <summary>Tek bir banka bağlantısının kanal ayarları.</summary>
public sealed class Iso8583ChannelOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }

    /// <summary>Bankaya TLS ile mi bağlanılacak (üretimde zorunlu tutulmalı).</summary>
    public bool UseTls { get; init; }

    /// <summary>TLS sertifika doğrulamasında beklenen sunucu adı; boşsa Host kullanılır.</summary>
    public string? TlsServerName { get; init; }

    public FrameLengthFormat FrameLengthFormat { get; init; } = FrameLengthFormat.TwoByteBinary;

    /// <summary>
    /// Bazı bankalar mesajın önünde sabit TPDU başlığı bekler (hex, örn. "6000030000").
    /// Boşsa TPDU kullanılmaz. Yanıtta aynı uzunlukta başlık okunup atılır.
    /// </summary>
    public string? TpduHeaderHex { get; init; }

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Banka yanıtı bu süre içinde gelmezse istek zaman aşımına düşer (adaptör reversal gönderir).</summary>
    public TimeSpan ResponseTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

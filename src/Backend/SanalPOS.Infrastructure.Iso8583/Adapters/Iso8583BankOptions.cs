using SanalPOS.Infrastructure.Iso8583.Network;

namespace SanalPOS.Infrastructure.Iso8583.Adapters;

/// <summary>
/// Konfigürasyondan ("Iso8583:Banks" dizisi) bağlanan, tek bir bankaya ait adaptör ayarları.
/// Her kayıt için bir Iso8583BankAdapter oluşturulur ve BankAdapterFactory'ye
/// ProviderCode ile kaydedilir; Terminal.BankProviderCode bu koda işaret eder.
/// </summary>
public sealed class Iso8583BankOptions
{
    public const string SectionName = "Iso8583:Banks";

    /// <summary>false ise kayıt atlanır (örn. sandbox tanımı repoda dursun ama açılmasın).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>BankAdapterFactory çözümleme anahtarı (örn. "ISBANK", "GARANTI").</summary>
    public string ProviderCode { get; init; } = string.Empty;

    /// <summary>Kayıtlı dialekt adı (Iso87Ascii, Iso87Bcd veya banka özel kayıtlı dialekt).</summary>
    public string Dialect { get; init; } = string.Empty;

    /// <summary>Banka tarafından atanan terminal numarası (DE41, 8 karakter).</summary>
    public string TerminalId { get; init; } = string.Empty;

    /// <summary>Banka tarafından atanan üye işyeri numarası (DE42, 15 karakter).</summary>
    public string MerchantId { get; init; } = string.Empty;

    /// <summary>Üye işyeri ad/konum bilgisi (DE43, 40 karakter); boşsa gönderilmez.</summary>
    public string? MerchantNameLocation { get; init; }

    /// <summary>Üye işyeri kategori kodu (DE18); e-ticaret varsayılanı 5999.</summary>
    public string MerchantCategoryCode { get; init; } = "5999";

    public Iso8583ChannelOptions Channel { get; init; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProviderCode))
            throw new InvalidOperationException($"{SectionName}: ProviderCode zorunludur.");
        if (string.IsNullOrWhiteSpace(Dialect))
            throw new InvalidOperationException($"{SectionName} ({ProviderCode}): Dialect zorunludur.");
        if (string.IsNullOrWhiteSpace(TerminalId))
            throw new InvalidOperationException($"{SectionName} ({ProviderCode}): TerminalId (DE41) zorunludur.");
        if (string.IsNullOrWhiteSpace(MerchantId))
            throw new InvalidOperationException($"{SectionName} ({ProviderCode}): MerchantId (DE42) zorunludur.");
        if (string.IsNullOrWhiteSpace(Channel.Host) || Channel.Port is <= 0 or > 65_535)
            throw new InvalidOperationException($"{SectionName} ({ProviderCode}): geçerli Channel.Host ve Channel.Port zorunludur.");
    }
}

using SanalPOS.Infrastructure.Iso8583.Messages;

namespace SanalPOS.BankSimulator;

/// <summary>
/// Deterministik sertifikasyon senaryoları. MockBankAdapter kurallarıyla uyumludur
/// (bkz. docs/00-proje-genel-bakis.md §6) ve test kartıyla tetiklenir:
///
///   PAN sonu "0002"  -> 51 (yetersiz bakiye)
///   PAN sonu "0004"  -> yanıt yok (timeout / otomatik reversal senaryosu)
///   PAN sonu "0041"  -> 41 (kayıp kart)
///   CVV (DE48) "999" -> 82 (CVV doğrulaması başarısız)
///   Diğerleri        -> 00 (onay) + DE38 otorizasyon kodu
///
/// 0800 (network management) ve 0400 (reversal) her zaman onaylanır; DE38 içeren
/// finansal olmayan işlemler (kapama/iptal/iade) referans kontrolü olmadan onaylanır.
/// </summary>
public static class Scenarios
{
    /// <summary>İsteğe verilecek yanıtı üretir; null dönerse yanıt gönderilmez (timeout).</summary>
    public static Iso8583Message? RespondTo(Iso8583Message request)
    {
        var pan = request[2];

        if (IsFinancialAuthorization(request.Mti) && pan is not null && pan.EndsWith("0004", StringComparison.Ordinal))
            return null;

        var response = new Iso8583Message(Iso8583Message.ResponseMtiOf(request.Mti))
        {
            [3] = request[3],
            [7] = request[7],
            [11] = request.GetRequired(11),
            [12] = request[12],
            [13] = request[13],
            [37] = request[37],
            [39] = DecideResponseCode(request),
            [41] = request[41],
            [42] = request[42],
            [49] = request[49]
        };

        if (response[39] == "00" && IsFinancialAuthorization(request.Mti))
            response[38] = $"S{Random.Shared.Next(10_000, 99_999)}";

        return response;
    }

    private static string DecideResponseCode(Iso8583Message request)
    {
        if (!IsFinancialAuthorization(request.Mti))
            return "00";

        var pan = request[2] ?? string.Empty;
        if (pan.EndsWith("0002", StringComparison.Ordinal))
            return "51";
        if (pan.EndsWith("0041", StringComparison.Ordinal))
            return "41";
        if (request[48]?.Trim() == "999")
            return "82";

        return "00";
    }

    /// <summary>Yeni finansal otorizasyon isteği mi (satış/ön otorizasyon; iade DE38 taşır)?</summary>
    private static bool IsFinancialAuthorization(string mti) =>
        mti is "0100" or "0200";
}

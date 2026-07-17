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
/// 0800 (network management) ve 0400 (reversal) her zaman onaylanır. Onaylanan finansal
/// işlemler ledger'a (banka defteri) yazılır; 0500 batch-close gelen toplamları ledger
/// ile karşılaştırır (eşit -> 00, değil -> 95).
/// </summary>
public static class Scenarios
{
    private const string SaleProcessingCode = "000000";
    private const string VoidProcessingCode = "020000";
    private const string RefundProcessingCode = "200000";
    private const string BatchCloseProcessingCode = "920000";

    /// <summary>İsteğe verilecek yanıtı üretir; null dönerse yanıt gönderilmez (timeout).</summary>
    public static Iso8583Message? RespondTo(Iso8583Message request, SimulatorLedger ledger)
    {
        if (request.Mti == "0500" && request[3] == BatchCloseProcessingCode)
            return RespondToBatchClose(request, ledger);

        var pan = request[2];

        if (IsFinancialAuthorization(request.Mti) && pan is not null && pan.EndsWith("0004", StringComparison.Ordinal))
            return null;

        var responseCode = DecideResponseCode(request);
        var response = new Iso8583Message(Iso8583Message.ResponseMtiOf(request.Mti))
        {
            [3] = request[3],
            [7] = request[7],
            [11] = request.GetRequired(11),
            [12] = request[12],
            [13] = request[13],
            [37] = request[37],
            [39] = responseCode,
            [41] = request[41],
            [42] = request[42],
            [49] = request[49]
        };

        if (responseCode == "00")
        {
            if (IsFinancialAuthorization(request.Mti))
                response[38] = $"S{Random.Shared.Next(10_000, 99_999)}";

            RecordApproved(request, ledger);
        }

        return response;
    }

    private static Iso8583Message RespondToBatchClose(Iso8583Message request, SimulatorLedger ledger)
    {
        var currency = request[49] ?? "949";
        var balanced = ledger.Matches(
            currency,
            saleCount: int.Parse(request.GetRequired(76)),
            saleMinor: long.Parse(request.GetRequired(88)),
            refundCount: int.Parse(request.GetRequired(74)),
            refundMinor: long.Parse(request.GetRequired(86)),
            voidCount: int.Parse(request.GetRequired(77)),
            voidMinor: long.Parse(request.GetRequired(89)));

        return new Iso8583Message("0510")
        {
            [3] = request[3],
            [11] = request.GetRequired(11),
            [37] = request[37],
            [39] = balanced ? "00" : "95",
            [41] = request[41],
            [42] = request[42],
            [49] = request[49]
        };
    }

    /// <summary>Onaylanan finansal işlemi banka defterine yazar (mutabakat karşılaştırması için).</summary>
    private static void RecordApproved(Iso8583Message request, SimulatorLedger ledger)
    {
        var currency = request[49] ?? "949";
        var amountMinor = request[4] is { } amount ? long.Parse(amount) : 0L;

        switch (request.Mti, request[3])
        {
            // 0200 satış ve 0220 provizyon kapama gün sonunda debit kalemidir; 0100 preauth
            // henüz tahsilat olmadığı için deftere yazılmaz.
            case ("0200", SaleProcessingCode):
            case ("0220", SaleProcessingCode):
                ledger.RecordSale(currency, amountMinor);
                break;
            case ("0200", RefundProcessingCode):
                ledger.RecordRefund(currency, amountMinor);
                break;
            case ("0400", VoidProcessingCode):
                ledger.RecordVoid(currency, amountMinor);
                break;
        }
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

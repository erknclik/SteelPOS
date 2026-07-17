using Microsoft.Extensions.Logging;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Infrastructure.Iso8583.Messages;
using SanalPOS.Infrastructure.Iso8583.Network;
using SanalPOS.Infrastructure.Iso8583.Spec;

namespace SanalPOS.Infrastructure.Iso8583.Adapters;

/// <summary>
/// ISO 8583 konuşan bankalar için genel IBankProviderAdapter implementasyonu.
/// Banka farkları koda değil konfigürasyona (Iso8583BankOptions) ve dialekte
/// (Iso8583Spec) taşınır; böylece yeni banka eklemek kod değişikliği gerektirmez,
/// gerekirse yalnızca yeni bir dialekt kaydı yapılır.
///
/// MTI eşlemesi:
///   Satış           -> 0200 (PC 000000)
///   Ön otorizasyon  -> 0100 (PC 000000)
///   Kapama (capture)-> 0220 advice (PC 000000, DE37/DE38 = orijinal RRN/otorizasyon kodu)
///   İptal (void)    -> 0400 (PC 020000, DE37/DE38 + DE90 orijinal STAN)
///   İade (refund)   -> 0200 (PC 200000, DE37/DE38)
///
/// Onay yanıtındaki RRN (DE37) ve STAN (DE11) ChargeResult ile üst katmana döner;
/// sonraki operasyonlar bu referansları BankTransactionReference ile geri taşır.
/// Tam PAN/CVV yalnızca bu çağrı süresince bellekte yaşar; loglara maskeli yazılır.
/// </summary>
public sealed class Iso8583BankAdapter : IBankProviderAdapter
{
    private const string SaleProcessingCode = "000000";
    private const string VoidProcessingCode = "020000";
    private const string RefundProcessingCode = "200000";

    private readonly Iso8583BankOptions _options;
    private readonly IIso8583Channel _channel;
    private readonly IStanSequence _stanSequence;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;

    public Iso8583BankAdapter(
        Iso8583BankOptions options,
        IIso8583Channel channel,
        IStanSequence stanSequence,
        TimeProvider clock,
        ILogger<Iso8583BankAdapter> logger)
    {
        _options = options;
        _channel = channel;
        _stanSequence = stanSequence;
        _clock = clock;
        _logger = logger;
    }

    public string ProviderCode => _options.ProviderCode;

    public Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct = default) =>
        AuthorizeAsync("0200", request, ct);

    public Task<ChargeResult> PreAuthAsync(ChargeRequest request, CancellationToken ct = default) =>
        AuthorizeAsync("0100", request, ct);

    public async Task<BankOperationResult> CaptureAsync(BankTransactionReference original, decimal amount, CancellationToken ct = default)
    {
        var message = await NewMessageAsync("0220", SaleProcessingCode, ct);
        message[4] = ToMinorUnits(amount);
        ApplyOriginalReference(message, original);

        return await SendOperationAsync(message, ct);
    }

    public async Task<BankOperationResult> VoidAsync(BankTransactionReference original, CancellationToken ct = default)
    {
        var message = await NewMessageAsync("0400", VoidProcessingCode, ct);
        message[4] = ToMinorUnits(original.OriginalAmount);
        message[49] = CurrencyNumericCode(original.Currency);
        ApplyOriginalReference(message, original);

        // DE90: orijinal işlemin kimliği. Zaman bilgisi elde yoksa sıfır dolgu (bankalar
        // eşleştirmeyi STAN + RRN üzerinden yapar).
        if (original.Stan is not null)
            message[90] = $"0200{original.Stan}{new string('0', 32)}";

        return await SendOperationAsync(message, ct);
    }

    public async Task<BankOperationResult> RefundAsync(BankTransactionReference original, decimal amount, CancellationToken ct = default)
    {
        var message = await NewMessageAsync("0200", RefundProcessingCode, ct);
        message[4] = ToMinorUnits(amount);
        message[49] = CurrencyNumericCode(original.Currency);
        ApplyOriginalReference(message, original);

        return await SendOperationAsync(message, ct);
    }

    public async Task<BankOperationResult> SettleAsync(SettlementTotals totals, CancellationToken ct = default)
    {
        // 0500 batch close (PC 920000): toplamlar ISO 8583 mutabakat alanlarında gönderilir.
        // Satışlar debit (DE76/88), iadeler credit (DE74/86), iptaller debit reversal (DE77/89).
        var message = await NewMessageAsync("0500", "920000", ct);
        message[49] = CurrencyNumericCode(totals.Currency);
        message[74] = totals.RefundCount.ToString("D10");
        message[76] = totals.SaleCount.ToString("D10");
        message[77] = totals.VoidCount.ToString("D10");
        message[86] = ToMinorUnits16(totals.RefundAmount);
        message[88] = ToMinorUnits16(totals.SaleAmount);
        message[89] = ToMinorUnits16(totals.VoidAmount);

        _logger.LogInformation(
            "{Provider}: gün sonu mutabakatı gönderiliyor. Gün: {Day}, Satış: {SaleCount}/{SaleAmount}, İade: {RefundCount}/{RefundAmount}, İptal: {VoidCount}/{VoidAmount}",
            ProviderCode, totals.Day, totals.SaleCount, totals.SaleAmount,
            totals.RefundCount, totals.RefundAmount, totals.VoidCount, totals.VoidAmount);

        return await SendOperationAsync(message, ct);
    }

    /// <summary>Orijinal işlem referansını mesaja işler: DE37 = RRN, DE38 = otorizasyon kodu.</summary>
    private static void ApplyOriginalReference(Iso8583Message message, BankTransactionReference original)
    {
        if (original.Rrn is not null)
            message[37] = original.Rrn;
        if (!string.IsNullOrEmpty(original.AuthCode))
            message[38] = original.AuthCode;
    }

    private async Task<ChargeResult> AuthorizeAsync(string mti, ChargeRequest request, CancellationToken ct)
    {
        var message = await NewMessageAsync(mti, SaleProcessingCode, ct);
        message[2] = request.CardNumber;
        message[4] = ToMinorUnits(request.Amount);
        message[14] = $"{request.ExpireYear % 100:D2}{request.ExpireMonth:D2}";
        message[49] = CurrencyNumericCode(request.Currency);
        message[62] = request.OrderReference;

        if (!string.IsNullOrEmpty(request.Cvv))
            message[48] = request.Cvv;

        if (request.InstallmentCount > 1)
            message[67] = request.InstallmentCount.ToString("D2");

        // 3D Secure kanıtı (MPI çıktısı): ECI/CAVV DE47'de taşınır (dialekt override edilebilir).
        if (request.ThreeDSecure is { } threeDs)
        {
            message[47] = threeDs.Xid is null
                ? $"ECI={threeDs.Eci};CAVV={threeDs.Cavv}"
                : $"ECI={threeDs.Eci};CAVV={threeDs.Cavv};XID={threeDs.Xid}";
        }

        Iso8583Message response;
        try
        {
            response = await _channel.SendAsync(message, ct);
        }
        catch (Iso8583TimeoutException ex)
        {
            // Yanıt gelmeyen finansal istek belirsiz durumdadır: banka tarafında onaylanmış
            // olabilir. Best-effort otomatik reversal ile geri alınır (ISO 8583 zorunluluğu).
            _logger.LogWarning(ex, "{Provider}: banka yanıt vermedi, otomatik reversal gönderiliyor. STAN: {Stan}",
                ProviderCode, message[11]);
            await TryReverseAsync(message, ct);
            return new ChargeResult(false, null, "TIMEOUT", "Banka yanıt vermedi; işlem otomatik olarak geri alındı.");
        }

        var responseCode = response[39];
        if (Iso8583ResponseCodes.IsApproved(responseCode))
        {
            // Banka DE37'yi echo'lamadıysa bizim ürettiğimiz RRN referans kabul edilir.
            var rrn = response[37]?.Trim() ?? message[37];
            return new ChargeResult(true, response.GetRequired(38).Trim(), null, null, rrn, response[11] ?? message[11]);
        }

        return new ChargeResult(false, null, responseCode ?? "UNKNOWN", Iso8583ResponseCodes.MessageOf(responseCode));
    }

    private async Task<BankOperationResult> SendOperationAsync(Iso8583Message message, CancellationToken ct)
    {
        Iso8583Message response;
        try
        {
            response = await _channel.SendAsync(message, ct);
        }
        catch (Iso8583TimeoutException)
        {
            return new BankOperationResult(false, "TIMEOUT", "Banka yanıt vermedi; işlemi daha sonra tekrar deneyiniz.");
        }

        var responseCode = response[39];
        return Iso8583ResponseCodes.IsApproved(responseCode)
            ? new BankOperationResult(true, null, null)
            : new BankOperationResult(false, responseCode ?? "UNKNOWN", Iso8583ResponseCodes.MessageOf(responseCode));
    }

    private async Task TryReverseAsync(Iso8583Message original, CancellationToken ct)
    {
        try
        {
            var reversal = await NewMessageAsync("0400", original.GetRequired(3), ct);
            reversal[2] = original[2];
            reversal[4] = original.GetRequired(4);
            reversal[14] = original[14];
            reversal[37] = original[37];
            reversal[49] = original[49];

            // DE90: orijinal mesajın kimliği (MTI + STAN + DE7 + acquirer/forwarding id, sıfır dolgulu).
            reversal[90] = $"{original.Mti}{original.GetRequired(11)}{original.GetRequired(7)}{new string('0', 22)}";

            await _channel.SendAsync(reversal, ct);
            _logger.LogInformation("{Provider}: otomatik reversal tamamlandı. Orijinal STAN: {Stan}",
                ProviderCode, original[11]);
        }
        catch (Exception ex)
        {
            // Reversal da başarısızsa gün sonu mutabakatı (reconciliation) düzeltir; işlem yine reddedilir.
            _logger.LogError(ex, "{Provider}: otomatik reversal gönderilemedi. Orijinal STAN: {Stan}",
                ProviderCode, original[11]);
        }
    }

    /// <summary>Tüm mesajlarda ortak zorunlu alanları doldurur.</summary>
    private async Task<Iso8583Message> NewMessageAsync(string mti, string processingCode, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var local = _clock.GetLocalNow();
        var stan = await _stanSequence.NextAsync(ct);

        var message = new Iso8583Message(mti)
        {
            [3] = processingCode,
            [7] = now.ToString("MMddHHmmss"),
            [11] = stan,
            [12] = local.ToString("HHmmss"),
            [13] = local.ToString("MMdd"),
            [18] = _options.MerchantCategoryCode,
            [22] = "010", // Kart numarası elle girildi (e-ticaret).
            [25] = "59",  // POS condition: e-commerce.
            [37] = BuildRrn(now, stan),
            [41] = _options.TerminalId,
            [42] = _options.MerchantId
        };

        if (!string.IsNullOrWhiteSpace(_options.MerchantNameLocation))
            message[43] = _options.MerchantNameLocation;

        return message;
    }

    /// <summary>RRN (DE37): yıl son hanesi + jülyen gün + saat + STAN = 12 hane, gün içinde tekil.</summary>
    private static string BuildRrn(DateTimeOffset now, string stan) =>
        $"{now.Year % 10}{now.DayOfYear:D3}{now.Hour:D2}{stan}";

    /// <summary>DE4: tutar, kuruş cinsinden 12 hane (ISO 8583 minor units).</summary>
    private static string ToMinorUnits(decimal amount) =>
        ((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)).ToString("D12");

    /// <summary>Mutabakat toplam alanları (DE86/88/89): kuruş cinsinden 16 hane.</summary>
    private static string ToMinorUnits16(decimal amount) =>
        ((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)).ToString("D16");

    /// <summary>DE49: ISO 4217 sayısal para birimi kodu.</summary>
    private static string CurrencyNumericCode(string alphaCode) => alphaCode.ToUpperInvariant() switch
    {
        "TRY" or "TL" => "949",
        "USD" => "840",
        "EUR" => "978",
        "GBP" => "826",
        _ => throw new Iso8583Exception($"Desteklenmeyen para birimi: '{alphaCode}'.")
    };
}

using Microsoft.Extensions.Logging;
using SanalPOS.Application.Common.Interfaces;

namespace SanalPOS.Infrastructure.BankAdapters;

/// <summary>
/// İlk faz mock/sandbox banka adaptörü (bkz. docs/00-proje-genel-bakis.md §6).
/// Deterministik senaryolar: sonu "0002" ile biten kart -> red (yetersiz bakiye),
/// CVV "999" -> red (güvenlik doğrulaması). Diğer tüm geçerli kartlar onaylanır.
/// </summary>
public class MockBankAdapter : IBankProviderAdapter
{
    public const string Code = "MOCKBANK";

    private readonly ILogger<MockBankAdapter> _logger;

    public MockBankAdapter(ILogger<MockBankAdapter> logger) => _logger = logger;

    public string ProviderCode => Code;

    public Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct = default) =>
        Task.FromResult(Evaluate(request));

    public Task<ChargeResult> PreAuthAsync(ChargeRequest request, CancellationToken ct = default) =>
        Task.FromResult(Evaluate(request));

    public Task<BankOperationResult> CaptureAsync(string bankAuthCode, decimal amount, CancellationToken ct = default) =>
        Task.FromResult(string.IsNullOrEmpty(bankAuthCode)
            ? new BankOperationResult(false, "51", "Provizyon bulunamadı.")
            : new BankOperationResult(true, null, null));

    public Task<BankOperationResult> VoidAsync(string bankAuthCode, CancellationToken ct = default) =>
        Task.FromResult(string.IsNullOrEmpty(bankAuthCode)
            ? new BankOperationResult(false, "51", "İşlem bulunamadı.")
            : new BankOperationResult(true, null, null));

    public Task<BankOperationResult> RefundAsync(string bankAuthCode, decimal amount, CancellationToken ct = default) =>
        Task.FromResult(string.IsNullOrEmpty(bankAuthCode)
            ? new BankOperationResult(false, "51", "İşlem bulunamadı.")
            : new BankOperationResult(true, null, null));

    private ChargeResult Evaluate(ChargeRequest request)
    {
        // Log'a asla tam PAN yazılmaz.
        _logger.LogInformation("Mock banka işlemi. OrderReference: {OrderReference}, Amount: {Amount} {Currency}",
            request.OrderReference, request.Amount, request.Currency);

        if (request.CardNumber.EndsWith("0002", StringComparison.Ordinal))
            return new ChargeResult(false, null, "05", "Yetersiz bakiye (mock senaryo).");

        if (request.Cvv == "999")
            return new ChargeResult(false, null, "82", "CVV doğrulaması başarısız (mock senaryo).");

        var authCode = Random.Shared.Next(100_000, 999_999).ToString();
        return new ChargeResult(true, authCode, null, null);
    }
}

/// <summary>Kayıtlı adaptörler arasından provider koduna göre çözümleme yapar.</summary>
public class BankAdapterFactory : IBankAdapterFactory
{
    private readonly IReadOnlyDictionary<string, IBankProviderAdapter> _adapters;

    public BankAdapterFactory(IEnumerable<IBankProviderAdapter> adapters) =>
        _adapters = adapters.ToDictionary(a => a.ProviderCode, StringComparer.OrdinalIgnoreCase);

    public IBankProviderAdapter Resolve(string providerCode)
    {
        if (_adapters.TryGetValue(providerCode, out var adapter))
            return adapter;

        throw new InvalidOperationException(
            $"'{providerCode}' için kayıtlı banka adaptörü yok. Kayıtlı adaptörler: {string.Join(", ", _adapters.Keys)}");
    }
}

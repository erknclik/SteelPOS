namespace SanalPOS.Application.Common.Interfaces;

/// <summary>
/// Banka/ödeme kuruluşu adaptör sözleşmesi (adapter pattern). İlk fazda MockBankAdapter,
/// ileride gerçek banka adaptörleri (İş Bankası, Garanti vb.) bu arayüzü uygular.
/// Tam PAN/CVV sadece bu çağrı sırasında bellekte yaşar; asla loglanmaz/saklanmaz.
/// </summary>
public interface IBankProviderAdapter
{
    string ProviderCode { get; }
    Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct = default);
    Task<ChargeResult> PreAuthAsync(ChargeRequest request, CancellationToken ct = default);
    Task<BankOperationResult> CaptureAsync(BankTransactionReference original, decimal amount, CancellationToken ct = default);
    Task<BankOperationResult> VoidAsync(BankTransactionReference original, CancellationToken ct = default);
    Task<BankOperationResult> RefundAsync(BankTransactionReference original, decimal amount, CancellationToken ct = default);
}

/// <summary>
/// Sonraki operasyonların (capture/void/refund) bankaya taşıdığı orijinal işlem kimliği.
/// Bankalar iptal/iadede yalnızca otorizasyon kodunu değil RRN/STAN'i de ister;
/// bu değerler onay anında ChargeResult'tan alınıp PaymentTransaction'da saklanır.
/// </summary>
public sealed record BankTransactionReference(
    string AuthCode,
    string? Rrn,
    string? Stan,
    decimal OriginalAmount,
    string Currency);

/// <summary>Aktif banka adaptörünü provider koduna göre çözer.</summary>
public interface IBankAdapterFactory
{
    IBankProviderAdapter Resolve(string providerCode);
}

public sealed record ChargeRequest(
    string CardNumber,
    string CardHolderName,
    int ExpireMonth,
    int ExpireYear,
    string Cvv,
    decimal Amount,
    string Currency,
    int InstallmentCount,
    string OrderReference,
    ThreeDSecureData? ThreeDSecure = null);

/// <summary>Başarılı 3D Secure doğrulamasının otorizasyona taşınan kanıtı (MPI çıktısı).</summary>
public sealed record ThreeDSecureData(string Eci, string Cavv, string? Xid);

public sealed record ChargeResult(
    bool IsApproved,
    string? AuthCode,
    string? ReasonCode,
    string? ReasonMessage,
    string? Rrn = null,
    string? Stan = null);

public sealed record BankOperationResult(bool IsSuccessful, string? ReasonCode, string? ReasonMessage);

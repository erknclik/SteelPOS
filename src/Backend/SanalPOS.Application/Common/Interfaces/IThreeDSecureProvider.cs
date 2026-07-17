namespace SanalPOS.Application.Common.Interfaces;

/// <summary>
/// 3D Secure MPI (Merchant Plug-In) soyutlaması. İlk fazda SimulatedThreeDSecureProvider,
/// üretimde gerçek MPI/banka 3D gateway adaptörleri bu arayüzü uygular
/// (provider pattern; seçim "ThreeDSecure:Provider" konfigürasyonundan yapılır).
/// Kart verisi yalnızca çağrı süresince bellekte yaşar; loglanmaz/saklanmaz.
/// </summary>
public interface IThreeDSecureProvider
{
    /// <summary>
    /// Kartın 3DS kaydını sorgular. Kayıtlıysa kart hamilinin yönlendirileceği
    /// ACS adresi ile MD (oturum belirteci) ve PaReq döner.
    /// </summary>
    Task<ThreeDSEnrollmentResult> InitiateAsync(ThreeDSEnrollmentRequest request, CancellationToken ct = default);

    /// <summary>ACS dönüşünde PaRes'i doğrular; başarılıysa otorizasyonda kullanılacak ECI/CAVV üretir.</summary>
    Task<ThreeDSVerificationResult> VerifyAsync(string md, string paRes, CancellationToken ct = default);
}

public sealed record ThreeDSEnrollmentRequest(
    Guid TransactionId,
    string CardNumber,
    int ExpireMonth,
    int ExpireYear,
    decimal Amount,
    string Currency,
    string OrderReference,
    string CallbackUrl);

/// <summary>IsEnrolled=false ise kart 3DS'e kayıtlı değildir; işlem doğrudan (3DS'siz) otorize edilir.</summary>
public sealed record ThreeDSEnrollmentResult(bool IsEnrolled, string? Md, string? AcsUrl, string? PaReq);

public sealed record ThreeDSVerificationResult(
    bool IsAuthenticated,
    string? Eci,
    string? Cavv,
    string? Xid,
    string? FailureReason);

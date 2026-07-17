using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SanalPOS.Application.Common.Interfaces;

namespace SanalPOS.Infrastructure.ThreeDSecure;

/// <summary>
/// Geliştirme/test MPI'ı: gerçek kart şeması dizinine gitmeden 3DS akışını simüle eder.
/// Deterministik senaryolar (test kartının son 4 hanesiyle tetiklenir):
///
///   PAN sonu "0006" -> kart 3DS'e kayıtlı değil (doğrudan otorizasyon fallback'i)
///   PAN sonu "0005" -> ACS doğrulaması başarısız (PaReq içine "N" ipucu gömülür)
///   Diğerleri       -> kayıtlı; ACS simülatörü PaRes="Y" ile döner, doğrulama başarılı
///
/// VerifyAsync: PaRes "Y" ise ECI 05 + rastgele CAVV üretir; aksi halde başarısız.
/// AcsUrl konfigürasyonu API'deki dev ACS simülatör endpoint'ini işaret eder.
/// </summary>
public sealed class SimulatedThreeDSecureProvider : IThreeDSecureProvider
{
    public const string ProviderName = "Simulated";

    private readonly SimulatedThreeDSecureOptions _options;
    private readonly ILogger<SimulatedThreeDSecureProvider> _logger;

    public SimulatedThreeDSecureProvider(SimulatedThreeDSecureOptions options, ILogger<SimulatedThreeDSecureProvider> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<ThreeDSEnrollmentResult> InitiateAsync(ThreeDSEnrollmentRequest request, CancellationToken ct = default)
    {
        if (request.CardNumber.EndsWith("0006", StringComparison.Ordinal))
        {
            _logger.LogInformation("Simüle MPI: kart 3DS'e kayıtlı değil. TransactionId: {TransactionId}", request.TransactionId);
            return Task.FromResult(new ThreeDSEnrollmentResult(false, null, null, null));
        }

        var md = Guid.NewGuid().ToString("N");
        var authOutcome = request.CardNumber.EndsWith("0005", StringComparison.Ordinal) ? "N" : "Y";

        // PaReq normalde şifreli şema mesajıdır; simülasyonda ACS'in okuyacağı sonucu taşır.
        var paReq = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{request.TransactionId}|{authOutcome}"));

        _logger.LogInformation("Simüle MPI: 3DS kaydı bulundu, ACS'e yönlendirilecek. TransactionId: {TransactionId}",
            request.TransactionId);

        return Task.FromResult(new ThreeDSEnrollmentResult(true, md, _options.AcsUrl, paReq));
    }

    public Task<ThreeDSVerificationResult> VerifyAsync(string md, string paRes, CancellationToken ct = default)
    {
        if (!string.Equals(paRes, "Y", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ThreeDSVerificationResult(
                false, null, null, null, "Kart hamili doğrulaması (ACS) başarısız."));
        }

        var cavv = Convert.ToBase64String(RandomNumberGenerator.GetBytes(20));
        return Task.FromResult(new ThreeDSVerificationResult(true, "05", cavv, md, null));
    }
}

public sealed class SimulatedThreeDSecureOptions
{
    /// <summary>Kart hamilinin yönlendirileceği (sahte) ACS adresi; API'deki dev endpoint'i işaret eder.</summary>
    public string AcsUrl { get; init; } = "/api/v1/acs-simulator";
}

using SanalPOS.Domain.Enums;

namespace SanalPOS.Application.Payments.Commands.ThreeDSecure;

/// <summary>
/// Initiate ile Complete arasında yaşayan 3DS oturum bağlamı. Yalnızca ISecretProtector
/// ile şifrelenmiş halde, kısa TTL'li cache'te (MD anahtarıyla) saklanır; callback'te
/// tek kullanımlık okunup silinir. Asla düz metin loglanmaz/persist edilmez.
/// </summary>
public sealed record ThreeDSCardContext(
    Guid TransactionId,
    Guid MerchantId,
    string CardNumber,
    string CardHolderName,
    int ExpireMonth,
    int ExpireYear,
    string Cvv,
    decimal Amount,
    string Currency,
    short InstallmentCount,
    string OrderReference,
    TransactionType TransactionType);

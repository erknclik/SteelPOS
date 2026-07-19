using SanalPOS.Domain.Common;
using SanalPOS.Domain.Exceptions;

namespace SanalPOS.Domain.Entities;

/// <summary>
/// Bir gün sonu mutabakat koşumunun kalıcı kaydı (banka + para birimi başına bir satır).
/// Dengede olmayan koşumlar operasyon ekibinin inceleme kuyruğudur; geçmiş koşumlar
/// denetim ve raporlama için saklanır.
/// </summary>
public class ReconciliationRun : BaseEntity, IAuditableEntity
{
    /// <summary>Mutabakatlanan gün (UTC, saat bileşeni olmadan).</summary>
    public DateTime Day { get; private set; }

    public string ProviderCode { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;

    public int SaleCount { get; private set; }
    public decimal SaleAmount { get; private set; }
    public int RefundCount { get; private set; }
    public decimal RefundAmount { get; private set; }
    public int VoidCount { get; private set; }
    public decimal VoidAmount { get; private set; }

    public bool IsBalanced { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? ReasonMessage { get; private set; }

    public DateTime ExecutedAt { get; private set; } = DateTime.UtcNow;

    protected ReconciliationRun()
    {
    }

    public ReconciliationRun(
        DateTime day,
        string providerCode,
        string currency,
        int saleCount, decimal saleAmount,
        int refundCount, decimal refundAmount,
        int voidCount, decimal voidAmount,
        bool isBalanced,
        string? reasonCode,
        string? reasonMessage)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
            throw new DomainException("Mutabakat kaydı için banka sağlayıcı kodu zorunludur.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Mutabakat kaydı için para birimi zorunludur.");
        if (saleCount < 0 || refundCount < 0 || voidCount < 0)
            throw new DomainException("Mutabakat adetleri negatif olamaz.");

        Day = day.Date;
        ProviderCode = providerCode;
        Currency = currency;
        SaleCount = saleCount;
        SaleAmount = saleAmount;
        RefundCount = refundCount;
        RefundAmount = refundAmount;
        VoidCount = voidCount;
        VoidAmount = voidAmount;
        IsBalanced = isBalanced;
        ReasonCode = reasonCode;
        ReasonMessage = reasonMessage;
    }
}

using SanalPOS.Domain.Common;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Events;
using SanalPOS.Domain.Exceptions;
using SanalPOS.Domain.ValueObjects;

namespace SanalPOS.Domain.Entities;

/// <summary>Çekirdek ödeme işlemi aggregate'i. Durum geçişleri bu sınıf üzerinden yapılır.</summary>
public class PaymentTransaction : BaseEntity, IAuditableEntity
{
    public Guid MerchantId { get; private set; }
    public Guid TerminalId { get; private set; }
    public string OrderReference { get; private set; } = string.Empty;
    public Money Amount { get; private set; } = null!;
    public short InstallmentCount { get; private set; } = 1;
    public TransactionType TransactionType { get; private set; }
    public TransactionStatus Status { get; private set; } = TransactionStatus.Pending;
    public MaskedCardNumber MaskedCard { get; private set; } = null!;
    public string CardHolderName { get; private set; } = string.Empty;
    public string? BankAuthCode { get; private set; }
    public string BankProviderCode { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public decimal CommissionAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public DateTime RequestedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }
    public decimal RefundedTotal { get; private set; }

    public virtual ICollection<TransactionStatusHistory> StatusHistory { get; protected set; } = new List<TransactionStatusHistory>();

    protected PaymentTransaction()
    {
    }

    public PaymentTransaction(
        Guid merchantId,
        Guid terminalId,
        string orderReference,
        Money amount,
        short installmentCount,
        TransactionType transactionType,
        MaskedCardNumber maskedCard,
        string cardHolderName,
        string bankProviderCode,
        string idempotencyKey)
    {
        if (amount.Amount <= 0)
            throw new DomainException("İşlem tutarı sıfırdan büyük olmalıdır.");
        if (installmentCount is < 1 or > 12)
            throw new DomainException("Taksit sayısı 1-12 aralığında olmalıdır.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Idempotency anahtarı zorunludur.");
        if (transactionType is not (TransactionType.Sale or TransactionType.PreAuth))
            throw new DomainException("Yeni işlem sadece Sale veya PreAuth tipinde oluşturulabilir.");

        MerchantId = merchantId;
        TerminalId = terminalId;
        OrderReference = orderReference;
        Amount = amount;
        InstallmentCount = installmentCount;
        TransactionType = transactionType;
        MaskedCard = maskedCard;
        CardHolderName = cardHolderName;
        BankProviderCode = bankProviderCode;
        IdempotencyKey = idempotencyKey;
    }

    public void Approve(string bankAuthCode, decimal commissionRate, string changedBy)
    {
        EnsureStatus(TransactionStatus.Pending, "onaylanabilir");

        BankAuthCode = bankAuthCode;
        CommissionAmount = decimal.Round(Amount.Amount * commissionRate / 100m, 2, MidpointRounding.AwayFromZero);
        NetAmount = Amount.Amount - CommissionAmount;
        CompletedAt = DateTime.UtcNow;
        ChangeStatus(TransactionStatus.Approved, changedBy);

        AddDomainEvent(new PaymentCompletedDomainEvent(this));
    }

    public void Decline(string reasonCode, string reasonMessage, string changedBy)
    {
        EnsureStatus(TransactionStatus.Pending, "reddedilebilir");

        CompletedAt = DateTime.UtcNow;
        ChangeStatus(TransactionStatus.Declined, changedBy);

        AddDomainEvent(new PaymentFailedDomainEvent(this, reasonCode, reasonMessage));
    }

    /// <summary>PreAuth işlemini tahsilata çevirir (provizyon kapama).</summary>
    public void Capture(string changedBy)
    {
        if (TransactionType != TransactionType.PreAuth)
            throw new DomainException("Sadece ön provizyon işlemleri capture edilebilir.");
        EnsureStatus(TransactionStatus.Approved, "capture edilebilir");

        TransactionType = TransactionType.Capture;
        CompletedAt = DateTime.UtcNow;
        RecordHistory(Status, Status, changedBy);

        AddDomainEvent(new PaymentCompletedDomainEvent(this));
    }

    /// <summary>Aynı gün iptali (void). Gün sonu kapanmışsa iade kullanılmalıdır.</summary>
    public void Void(string changedBy)
    {
        EnsureStatus(TransactionStatus.Approved, "iptal edilebilir");
        if (CompletedAt is null || CompletedAt.Value.Date != DateTime.UtcNow.Date)
            throw new DomainException("Void sadece aynı gün içinde yapılabilir; iade (refund) kullanın.");

        ChangeStatus(TransactionStatus.Reversed, changedBy);
    }

    public void ApplyRefund(RefundTransaction refund, string changedBy)
    {
        if (Status is not (TransactionStatus.Approved or TransactionStatus.PartiallyRefunded))
            throw new DomainException($"'{Status}' durumundaki işlem iade edilemez.");
        if (refund.RefundAmount <= 0)
            throw new DomainException("İade tutarı sıfırdan büyük olmalıdır.");
        if (RefundedTotal + refund.RefundAmount > Amount.Amount)
            throw new DomainException("İade toplamı işlem tutarını aşamaz.");

        RefundedTotal += refund.RefundAmount;
        var newStatus = RefundedTotal == Amount.Amount
            ? TransactionStatus.Refunded
            : TransactionStatus.PartiallyRefunded;
        ChangeStatus(newStatus, changedBy);

        AddDomainEvent(new RefundCompletedDomainEvent(refund, this));
    }

    private void ChangeStatus(TransactionStatus newStatus, string changedBy)
    {
        var old = Status;
        Status = newStatus;
        RecordHistory(old, newStatus, changedBy);
    }

    private void RecordHistory(TransactionStatus oldStatus, TransactionStatus newStatus, string changedBy) =>
        StatusHistory.Add(new TransactionStatusHistory(Id, oldStatus, newStatus, changedBy));

    private void EnsureStatus(TransactionStatus expected, string action)
    {
        if (Status != expected)
            throw new DomainException($"'{Status}' durumundaki işlem {action} değildir.");
    }
}

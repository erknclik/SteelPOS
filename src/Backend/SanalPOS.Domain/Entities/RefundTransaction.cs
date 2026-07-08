using SanalPOS.Domain.Common;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Exceptions;

namespace SanalPOS.Domain.Entities;

public class RefundTransaction : BaseEntity, IAuditableEntity
{
    public Guid OriginalTransactionId { get; private set; }
    public decimal RefundAmount { get; private set; }
    public string? Reason { get; private set; }
    public RefundStatus Status { get; private set; } = RefundStatus.Pending;

    protected RefundTransaction()
    {
    }

    public RefundTransaction(Guid originalTransactionId, decimal refundAmount, string? reason)
    {
        if (refundAmount <= 0)
            throw new DomainException("İade tutarı sıfırdan büyük olmalıdır.");

        OriginalTransactionId = originalTransactionId;
        RefundAmount = decimal.Round(refundAmount, 2, MidpointRounding.AwayFromZero);
        Reason = reason;
    }

    public void Complete() => Status = RefundStatus.Completed;
    public void Fail() => Status = RefundStatus.Failed;
}

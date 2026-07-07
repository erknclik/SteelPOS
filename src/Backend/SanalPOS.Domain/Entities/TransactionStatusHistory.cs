using SanalPOS.Domain.Common;
using SanalPOS.Domain.Enums;

namespace SanalPOS.Domain.Entities;

public class TransactionStatusHistory : BaseEntity
{
    public Guid TransactionId { get; private set; }
    public TransactionStatus OldStatus { get; private set; }
    public TransactionStatus NewStatus { get; private set; }
    public DateTime ChangedAt { get; private set; } = DateTime.UtcNow;
    public string ChangedBy { get; private set; } = string.Empty;

    protected TransactionStatusHistory()
    {
    }

    public TransactionStatusHistory(Guid transactionId, TransactionStatus oldStatus, TransactionStatus newStatus, string changedBy)
    {
        TransactionId = transactionId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        ChangedBy = changedBy;
    }
}

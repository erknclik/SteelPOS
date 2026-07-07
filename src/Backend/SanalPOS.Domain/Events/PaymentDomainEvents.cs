using SanalPOS.Domain.Common;
using SanalPOS.Domain.Entities;

namespace SanalPOS.Domain.Events;

public sealed record PaymentCompletedDomainEvent(PaymentTransaction Transaction) : IDomainEvent
{
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

public sealed record PaymentFailedDomainEvent(PaymentTransaction Transaction, string ReasonCode, string ReasonMessage) : IDomainEvent
{
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

public sealed record RefundCompletedDomainEvent(RefundTransaction Refund, PaymentTransaction Original) : IDomainEvent
{
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

public sealed record MerchantSuspendedDomainEvent(Merchant Merchant) : IDomainEvent
{
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

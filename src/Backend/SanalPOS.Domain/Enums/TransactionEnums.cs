namespace SanalPOS.Domain.Enums;

public enum TransactionType
{
    Sale = 1,
    PreAuth = 2,
    Capture = 3,
    Refund = 4,
    Void = 5
}

public enum TransactionStatus
{
    Pending = 1,
    Approved = 2,
    Declined = 3,
    Reversed = 4,
    Refunded = 5,
    PartiallyRefunded = 6,

    /// <summary>Kart hamili 3D Secure doğrulamasına (ACS) yönlendirildi; sonuç bekleniyor.</summary>
    Pending3DS = 7
}

public enum MerchantStatus
{
    Active = 1,
    Suspended = 2,
    Closed = 3
}

public enum RefundStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3
}

public enum AuditAction
{
    Create = 1,
    Update = 2,
    Delete = 3,
    StatusChange = 4
}

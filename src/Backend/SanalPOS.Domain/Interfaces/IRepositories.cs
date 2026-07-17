using SanalPOS.Domain.Common;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;

namespace SanalPOS.Domain.Interfaces;

/// <summary>
/// ORM bağımsız generic repository arayüzü. EF Core ve NHibernate implementasyonları
/// aynı sözleşmeyi uygular; Application katmanı hangi ORM'in aktif olduğunu bilmez.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    /// <summary>Değişiklikleri kaydeder ve biriken domain event'leri dispatch eder.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IPaymentTransactionRepository : IRepository<PaymentTransaction>
{
    Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<PaymentTransaction?> GetWithHistoryAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetPagedAsync(
        Guid? merchantId,
        TransactionStatus? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? terminalId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<DailySummary> GetDailySummaryAsync(Guid? merchantId, DateTime dayUtc, CancellationToken ct = default);

    /// <summary>
    /// Gün sonu mutabakatı için banka sağlayıcısı + para birimi bazında toplamlar.
    /// Satışlar: o gün tamamlanmış Sale/Capture (Approved/Refunded/PartiallyRefunded dahil —
    /// iade ayrı bir credit kalemidir); iptaller: Reversed.
    /// </summary>
    Task<IReadOnlyList<ProviderDailyTotals>> GetProviderDailyTotalsAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

/// <summary>Mutabakat için sağlayıcı bazında günlük satış/iptal toplamları.</summary>
public sealed record ProviderDailyTotals(
    string BankProviderCode,
    string Currency,
    int SaleCount,
    decimal SaleAmount,
    int VoidCount,
    decimal VoidAmount);

/// <summary>Mutabakat için sağlayıcı bazında günlük iade toplamları.</summary>
public sealed record ProviderRefundTotals(
    string BankProviderCode,
    string Currency,
    int RefundCount,
    decimal RefundAmount);

/// <summary>Günlük özet raporu için aggregate sonucu.</summary>
public sealed record DailySummary(
    DateTime Day,
    int TotalCount,
    int ApprovedCount,
    int DeclinedCount,
    decimal TotalAmount,
    decimal TotalCommission,
    decimal TotalNet,
    decimal TotalRefunded);

public interface IMerchantRepository : IRepository<Merchant>
{
    Task<Merchant?> GetWithCommissionRulesAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAndActiveAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Merchant> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
}

public interface IStoreRepository : IRepository<Store>
{
    Task<IReadOnlyList<Store>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default);
}

public interface ITerminalRepository : IRepository<Terminal>
{
    Task<IReadOnlyList<Terminal>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default);
    Task<Terminal?> GetByCodeAsync(string terminalCode, CancellationToken ct = default);
}

public interface IRefundTransactionRepository : IRepository<RefundTransaction>
{
    Task<IReadOnlyList<RefundTransaction>> GetByOriginalTransactionAsync(Guid originalTransactionId, CancellationToken ct = default);

    /// <summary>O gün tamamlanmış iadelerin sağlayıcı + para birimi bazında toplamları (orijinal işlem üzerinden).</summary>
    Task<IReadOnlyList<ProviderRefundTotals>> GetProviderDailyTotalsAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public interface ICommissionRuleRepository : IRepository<CommissionRule>
{
    Task<IReadOnlyList<CommissionRule>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default);
}

public interface IWebhookSubscriptionRepository : IRepository<WebhookSubscription>
{
    Task<IReadOnlyList<WebhookSubscription>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookSubscription>> GetActiveByEventTypeAsync(Guid merchantId, string eventType, CancellationToken ct = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default);
    Task<User?> GetWithRolesAsync(Guid id, CancellationToken ct = default);
}

public interface IRoleRepository : IRepository<Role>
{
    Task<Role?> GetByNameAsync(string name, CancellationToken ct = default);
}

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}

using NHibernate;
using NHibernate.Linq;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Domain.Common;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Infrastructure.NHibernate.Repositories;

public class NhRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly ISession Session;
    private readonly ICurrentUserService _currentUser;

    public NhRepository(ISession session, ICurrentUserService currentUser)
    {
        Session = session;
        _currentUser = currentUser;
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Session.Query<T>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
    {
        entity.CreatedBy ??= _currentUser.UserName;
        await Session.SaveAsync(entity, ct);
    }

    public virtual void Update(T entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserName;
        Session.Update(entity);
    }

    /// <summary>Soft delete: kayıt fiziksel silinmez, is_deleted işaretlenir.</summary>
    public virtual void Remove(T entity)
    {
        entity.IsDeleted = true;
        Update(entity);
    }
}

public class NhPaymentTransactionRepository : NhRepository<PaymentTransaction>, IPaymentTransactionRepository
{
    public NhPaymentTransactionRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        await Session.Query<PaymentTransaction>().FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

    public async Task<PaymentTransaction?> GetWithHistoryAsync(Guid id, CancellationToken ct = default) =>
        await Session.Query<PaymentTransaction>()
            .FetchMany(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetPagedAsync(
        Guid? merchantId, TransactionStatus? status, DateTime? fromUtc, DateTime? toUtc,
        Guid? terminalId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Session.Query<PaymentTransaction>();

        if (merchantId is not null) query = query.Where(x => x.MerchantId == merchantId);
        if (status is not null) query = query.Where(x => x.Status == status);
        if (fromUtc is not null) query = query.Where(x => x.RequestedAt >= fromUtc);
        if (toUtc is not null) query = query.Where(x => x.RequestedAt <= toUtc);
        if (terminalId is not null) query = query.Where(x => x.TerminalId == terminalId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<DailySummary> GetDailySummaryAsync(Guid? merchantId, DateTime dayUtc, CancellationToken ct = default)
    {
        var dayStart = DateTime.SpecifyKind(dayUtc.Date, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var query = Session.Query<PaymentTransaction>()
            .Where(x => x.RequestedAt >= dayStart && x.RequestedAt < dayEnd);
        if (merchantId is not null)
            query = query.Where(x => x.MerchantId == merchantId);

        var rows = await query
            .Select(x => new { x.Status, x.Amount.Amount, x.CommissionAmount, x.NetAmount, x.RefundedTotal })
            .ToListAsync(ct);

        var approvedStatuses = new[] { TransactionStatus.Approved, TransactionStatus.Refunded, TransactionStatus.PartiallyRefunded };
        var approved = rows.Where(x => approvedStatuses.Contains(x.Status)).ToList();

        return new DailySummary(
            dayStart,
            rows.Count,
            approved.Count,
            rows.Count(x => x.Status == TransactionStatus.Declined),
            approved.Sum(x => x.Amount),
            approved.Sum(x => x.CommissionAmount),
            approved.Sum(x => x.NetAmount),
            rows.Sum(x => x.RefundedTotal));
    }

    public async Task<IReadOnlyList<ProviderDailyTotals>> GetProviderDailyTotalsAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        // Banka batch semantiği: void edilen işlem hem satış (otorizasyon) hem reversal
        // kalemidir; net tutar banka tarafında debits - reversals olarak hesaplanır.
        var settledStatuses = new[]
        {
            TransactionStatus.Approved, TransactionStatus.Refunded,
            TransactionStatus.PartiallyRefunded, TransactionStatus.Reversed
        };
        var saleTypes = new[] { TransactionType.Sale, TransactionType.Capture };

        var rows = await Session.Query<PaymentTransaction>()
            .Where(x => x.CompletedAt >= fromUtc && x.CompletedAt < toUtc)
            .Where(x => saleTypes.Contains(x.TransactionType) && settledStatuses.Contains(x.Status))
            .Select(x => new { x.BankProviderCode, x.Amount.Currency, x.Status, x.Amount.Amount })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => new { x.BankProviderCode, x.Currency })
            .Select(g => new ProviderDailyTotals(
                g.Key.BankProviderCode,
                g.Key.Currency,
                g.Count(),
                g.Sum(x => x.Amount),
                g.Count(x => x.Status == TransactionStatus.Reversed),
                g.Where(x => x.Status == TransactionStatus.Reversed).Sum(x => x.Amount)))
            .OrderBy(x => x.BankProviderCode)
            .ToList();
    }
}

public class NhMerchantRepository : NhRepository<Merchant>, IMerchantRepository
{
    public NhMerchantRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<Merchant?> GetWithCommissionRulesAsync(Guid id, CancellationToken ct = default) =>
        await Session.Query<Merchant>()
            .FetchMany(x => x.CommissionRules)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<bool> ExistsAndActiveAsync(Guid id, CancellationToken ct = default) =>
        await Session.Query<Merchant>().AnyAsync(x => x.Id == id && x.Status == MerchantStatus.Active, ct);

    public async Task<(IReadOnlyList<Merchant> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var totalCount = await Session.Query<Merchant>().CountAsync(ct);
        var items = await Session.Query<Merchant>()
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }
}

public class NhStoreRepository : NhRepository<Store>, IStoreRepository
{
    public NhStoreRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<IReadOnlyList<Store>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default) =>
        await Session.Query<Store>().Where(x => x.MerchantId == merchantId).OrderBy(x => x.Name).ToListAsync(ct);
}

public class NhTerminalRepository : NhRepository<Terminal>, ITerminalRepository
{
    public NhTerminalRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<IReadOnlyList<Terminal>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default)
    {
        var storeIds = Session.Query<Store>().Where(s => s.MerchantId == merchantId).Select(s => s.Id);
        return await Session.Query<Terminal>()
            .Where(t => storeIds.Contains(t.StoreId))
            .OrderBy(t => t.TerminalCode)
            .ToListAsync(ct);
    }

    public async Task<Terminal?> GetByCodeAsync(string terminalCode, CancellationToken ct = default) =>
        await Session.Query<Terminal>().FirstOrDefaultAsync(x => x.TerminalCode == terminalCode, ct);
}

public class NhRefundTransactionRepository : NhRepository<RefundTransaction>, IRefundTransactionRepository
{
    public NhRefundTransactionRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<IReadOnlyList<RefundTransaction>> GetByOriginalTransactionAsync(Guid originalTransactionId, CancellationToken ct = default) =>
        await Session.Query<RefundTransaction>().Where(x => x.OriginalTransactionId == originalTransactionId).ToListAsync(ct);

    public async Task<IReadOnlyList<ProviderRefundTotals>> GetProviderDailyTotalsAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var rows = await (
            from r in Session.Query<RefundTransaction>()
            join t in Session.Query<PaymentTransaction>() on r.OriginalTransactionId equals t.Id
            where r.Status == RefundStatus.Completed && r.CreatedAt >= fromUtc && r.CreatedAt < toUtc
            select new { t.BankProviderCode, t.Amount.Currency, r.RefundAmount }).ToListAsync(ct);

        return rows
            .GroupBy(x => new { x.BankProviderCode, x.Currency })
            .Select(g => new ProviderRefundTotals(
                g.Key.BankProviderCode, g.Key.Currency, g.Count(), g.Sum(x => x.RefundAmount)))
            .OrderBy(x => x.BankProviderCode)
            .ToList();
    }
}

public class NhReconciliationRunRepository : NhRepository<ReconciliationRun>, IReconciliationRunRepository
{
    public NhReconciliationRunRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<IReadOnlyList<ReconciliationRun>> GetRecentAsync(int count, CancellationToken ct = default) =>
        await Session.Query<ReconciliationRun>()
            .OrderByDescending(x => x.ExecutedAt)
            .Take(count)
            .ToListAsync(ct);
}

public class NhCommissionRuleRepository : NhRepository<CommissionRule>, ICommissionRuleRepository
{
    public NhCommissionRuleRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<IReadOnlyList<CommissionRule>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default) =>
        await Session.Query<CommissionRule>().Where(x => x.MerchantId == merchantId)
            .OrderBy(x => x.InstallmentCount).ThenBy(x => x.ValidFrom).ToListAsync(ct);
}

public class NhWebhookSubscriptionRepository : NhRepository<WebhookSubscription>, IWebhookSubscriptionRepository
{
    public NhWebhookSubscriptionRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<IReadOnlyList<WebhookSubscription>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default) =>
        await Session.Query<WebhookSubscription>().Where(x => x.MerchantId == merchantId).ToListAsync(ct);

    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveByEventTypeAsync(Guid merchantId, string eventType, CancellationToken ct = default) =>
        await Session.Query<WebhookSubscription>()
            .Where(x => x.MerchantId == merchantId && x.EventType == eventType && x.IsActive)
            .ToListAsync(ct);
}

public class NhAuditLogRepository : IAuditLogRepository
{
    private readonly ISession _session;

    public NhAuditLogRepository(ISession session) => _session = session;

    public async Task AddAsync(AuditLog auditLog, CancellationToken ct = default) =>
        await _session.SaveAsync(auditLog, ct);
}

public class NhUserRepository : NhRepository<User>, IUserRepository
{
    public NhUserRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default) =>
        await Session.Query<User>()
            .FetchMany(x => x.Roles)
            .FirstOrDefaultAsync(x => x.UserName == userName, ct);

    public async Task<User?> GetWithRolesAsync(Guid id, CancellationToken ct = default) =>
        await Session.Query<User>()
            .FetchMany(x => x.Roles)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
}

public class NhRoleRepository : NhRepository<Role>, IRoleRepository
{
    public NhRoleRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) =>
        await Session.Query<Role>().FirstOrDefaultAsync(x => x.Name == name, ct);
}

public class NhRefreshTokenRepository : NhRepository<RefreshToken>, IRefreshTokenRepository
{
    public NhRefreshTokenRepository(ISession session, ICurrentUserService currentUser)
        : base(session, currentUser)
    {
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        await Session.Query<RefreshToken>().FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await Session.Query<RefreshToken>()
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in tokens)
        {
            token.Revoke();
            Update(token);
        }
    }
}

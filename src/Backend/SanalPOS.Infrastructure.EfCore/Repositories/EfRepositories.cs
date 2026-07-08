using Microsoft.EntityFrameworkCore;
using SanalPOS.Domain.Common;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Infrastructure.EfCore.Repositories;

public class EfRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly SanalPosDbContext Context;
    protected DbSet<T> Set => Context.Set<T>();

    public EfRepository(SanalPosDbContext context) => Context = context;

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(x => x.Id == id, ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    public virtual void Update(T entity) => Set.Update(entity);

    /// <summary>Soft delete: kayıt fiziksel silinmez, is_deleted işaretlenir.</summary>
    public virtual void Remove(T entity)
    {
        entity.IsDeleted = true;
        Set.Update(entity);
    }
}

public class EfPaymentTransactionRepository : EfRepository<PaymentTransaction>, IPaymentTransactionRepository
{
    public EfPaymentTransactionRepository(SanalPosDbContext context) : base(context)
    {
    }

    public Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

    public Task<PaymentTransaction?> GetWithHistoryAsync(Guid id, CancellationToken ct = default) =>
        Set.Include(x => x.StatusHistory).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<PaymentTransaction> Items, int TotalCount)> GetPagedAsync(
        Guid? merchantId, TransactionStatus? status, DateTime? fromUtc, DateTime? toUtc,
        Guid? terminalId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

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

        var query = Set.AsNoTracking()
            .Where(x => x.RequestedAt >= dayStart && x.RequestedAt < dayEnd);
        if (merchantId is not null)
            query = query.Where(x => x.MerchantId == merchantId);

        var rows = await query
            .Select(x => new { x.Status, Amount = x.Amount.Amount, x.CommissionAmount, x.NetAmount, x.RefundedTotal })
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
}

public class EfMerchantRepository : EfRepository<Merchant>, IMerchantRepository
{
    public EfMerchantRepository(SanalPosDbContext context) : base(context)
    {
    }

    public Task<Merchant?> GetWithCommissionRulesAsync(Guid id, CancellationToken ct = default) =>
        Set.Include(x => x.CommissionRules).FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExistsAndActiveAsync(Guid id, CancellationToken ct = default) =>
        Set.AnyAsync(x => x.Id == id && x.Status == MerchantStatus.Active, ct);

    public async Task<(IReadOnlyList<Merchant> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var totalCount = await Set.CountAsync(ct);
        var items = await Set.AsNoTracking()
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }
}

public class EfStoreRepository : EfRepository<Store>, IStoreRepository
{
    public EfStoreRepository(SanalPosDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Store>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(x => x.MerchantId == merchantId).OrderBy(x => x.Name).ToListAsync(ct);
}

public class EfTerminalRepository : EfRepository<Terminal>, ITerminalRepository
{
    public EfTerminalRepository(SanalPosDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Terminal>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(t => Context.Stores.Any(s => s.Id == t.StoreId && s.MerchantId == merchantId))
            .OrderBy(t => t.TerminalCode)
            .ToListAsync(ct);

    public Task<Terminal?> GetByCodeAsync(string terminalCode, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(x => x.TerminalCode == terminalCode, ct);
}

public class EfRefundTransactionRepository : EfRepository<RefundTransaction>, IRefundTransactionRepository
{
    public EfRefundTransactionRepository(SanalPosDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<RefundTransaction>> GetByOriginalTransactionAsync(Guid originalTransactionId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(x => x.OriginalTransactionId == originalTransactionId).ToListAsync(ct);
}

public class EfCommissionRuleRepository : EfRepository<CommissionRule>, ICommissionRuleRepository
{
    public EfCommissionRuleRepository(SanalPosDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<CommissionRule>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(x => x.MerchantId == merchantId)
            .OrderBy(x => x.InstallmentCount).ThenBy(x => x.ValidFrom).ToListAsync(ct);
}

public class EfWebhookSubscriptionRepository : EfRepository<WebhookSubscription>, IWebhookSubscriptionRepository
{
    public EfWebhookSubscriptionRepository(SanalPosDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<WebhookSubscription>> GetByMerchantAsync(Guid merchantId, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(x => x.MerchantId == merchantId).ToListAsync(ct);

    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveByEventTypeAsync(Guid merchantId, string eventType, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(x => x.MerchantId == merchantId && x.EventType == eventType && x.IsActive)
            .ToListAsync(ct);
}

public class EfAuditLogRepository : IAuditLogRepository
{
    private readonly SanalPosDbContext _context;

    public EfAuditLogRepository(SanalPosDbContext context) => _context = context;

    public async Task AddAsync(AuditLog auditLog, CancellationToken ct = default) =>
        await _context.AuditLogs.AddAsync(auditLog, ct);
}

public class EfUserRepository : EfRepository<User>, IUserRepository
{
    public EfUserRepository(SanalPosDbContext context) : base(context)
    {
    }

    public Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default) =>
        Set.Include(x => x.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(x => x.UserName == userName, ct);

    public Task<User?> GetWithRolesAsync(Guid id, CancellationToken ct = default) =>
        Set.Include(x => x.Roles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
}

public class EfRoleRepository : EfRepository<Role>, IRoleRepository
{
    public EfRoleRepository(SanalPosDbContext context) : base(context)
    {
    }

    public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(x => x.Name == name, ct);
}

public class EfRefreshTokenRepository : EfRepository<RefreshToken>, IRefreshTokenRepository
{
    public EfRefreshTokenRepository(SanalPosDbContext context) : base(context)
    {
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await Set.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(ct);
        foreach (var token in tokens)
            token.Revoke();
    }
}

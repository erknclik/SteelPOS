using Microsoft.EntityFrameworkCore;
using SanalPOS.Domain.Entities;

namespace SanalPOS.Infrastructure.EfCore;

public class SanalPosDbContext : DbContext
{
    public const string Schema = "sanalpos";

    public SanalPosDbContext(DbContextOptions<SanalPosDbContext> options) : base(options)
    {
    }

    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<TransactionStatusHistory> TransactionStatusHistories => Set<TransactionStatusHistory>();
    public DbSet<RefundTransaction> RefundTransactions => Set<RefundTransaction>();
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ReconciliationRun> ReconciliationRuns => Set<ReconciliationRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SanalPosDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SanalPOS.Domain.Common;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;

namespace SanalPOS.Infrastructure.EfCore.Configurations;

// Kolon adları EFCore.NamingConventions (snake_case) ile otomatik üretilir;
// burada sadece tablo adları, tipler, indeksler ve value object mapping'leri tanımlanır.

internal static class ConfigurationExtensions
{
    public static void ConfigureBase<T>(this EntityTypeBuilder<T> builder) where T : BaseEntity
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.UpdatedBy).HasMaxLength(100);
        builder.Ignore(x => x.DomainEvents);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.ToTable("merchants");
        builder.ConfigureBase();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TaxNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DefaultCommissionRate).HasColumnType("numeric(5,2)");
        builder.OwnsOne(x => x.Iban, iban =>
            iban.Property(i => i.Value).HasColumnName("iban").HasMaxLength(34).IsRequired());
        builder.HasMany(x => x.Stores).WithOne().HasForeignKey(s => s.MerchantId);
        builder.HasMany(x => x.CommissionRules).WithOne().HasForeignKey(r => r.MerchantId);
    }
}

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("stores");
        builder.ConfigureBase();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Address).HasColumnType("text");
        builder.HasMany(x => x.Terminals).WithOne().HasForeignKey(t => t.StoreId);
    }
}

public class TerminalConfiguration : IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> builder)
    {
        builder.ToTable("terminals");
        builder.ConfigureBase();
        builder.Property(x => x.TerminalCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BankProviderCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.TerminalCode).IsUnique();
    }
}

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions");
        builder.ConfigureBase();

        builder.Property(x => x.OrderReference).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TransactionType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CardHolderName).HasMaxLength(150);
        builder.Property(x => x.BankAuthCode).HasMaxLength(50);
        builder.Property(x => x.BankRrn).HasMaxLength(24);
        builder.Property(x => x.BankStan).HasMaxLength(12);
        builder.Property(x => x.BankProviderCode).HasMaxLength(50);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CommissionAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.NetAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.RefundedTotal).HasColumnType("numeric(18,2)");

        builder.OwnsOne(x => x.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.OwnsOne(x => x.MaskedCard, card =>
            card.Property(c => c.Value).HasColumnName("masked_card_number").HasMaxLength(25).IsRequired());

        builder.HasMany(x => x.StatusHistory).WithOne().HasForeignKey(h => h.TransactionId);

        // İndeksleme stratejisi (bkz. docs/03-veritabani-tasarimi.md §6)
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.MerchantId, x.RequestedAt }).IsDescending(false, true);
        builder.HasIndex(x => x.Status);
    }
}

public class TransactionStatusHistoryConfiguration : IEntityTypeConfiguration<TransactionStatusHistory>
{
    public void Configure(EntityTypeBuilder<TransactionStatusHistory> builder)
    {
        builder.ToTable("transaction_status_history");
        builder.ConfigureBase();
        builder.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ChangedBy).HasMaxLength(100);
        builder.HasIndex(x => x.TransactionId);
    }
}

public class RefundTransactionConfiguration : IEntityTypeConfiguration<RefundTransaction>
{
    public void Configure(EntityTypeBuilder<RefundTransaction> builder)
    {
        builder.ToTable("refund_transactions");
        builder.ConfigureBase();
        builder.Property(x => x.RefundAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.OriginalTransactionId);
    }
}

public class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> builder)
    {
        builder.ToTable("commission_rules");
        builder.ConfigureBase();
        builder.Property(x => x.Rate).HasColumnType("numeric(5,2)");
    }
}

public class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");
        builder.ConfigureBase();
        builder.Property(x => x.EventType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TargetUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Secret).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.MerchantId, x.EventType });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.ConfigureBase();
        builder.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PerformedBy).HasMaxLength(100);
        builder.Property(x => x.PayloadSnapshot).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.ConfigureBase();
        builder.Property(x => x.UserName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.FullName).HasMaxLength(200);
        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.UserName).IsUnique();
        builder.HasMany(x => x.Roles).WithOne().HasForeignKey(r => r.UserId);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.ConfigureBase();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.ConfigureBase();
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        builder.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.ConfigureBase();
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

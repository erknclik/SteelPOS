using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;
using NHibernate.Type;
using SanalPOS.Domain.Common;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Infrastructure.NHibernate.Types;

namespace SanalPOS.Infrastructure.NHibernate.Mappings;

// EF Core ile AYNI fiziksel şemaya yazar (tek kaynak şema; migration'lar EF Core/Flyway ile yönetilir).

internal static class MappingExtensions
{
    public static void MapBase<T>(this ClassMapping<T> map) where T : BaseEntity
    {
        map.Schema("sanalpos");
        map.Lazy(false);
        map.Id(x => x.Id, id =>
        {
            id.Column("id");
            id.Generator(Generators.Assigned);
        });
        map.Property(x => x.CreatedAt, p => p.Column("created_at"));
        map.Property(x => x.CreatedBy, p => p.Column("created_by"));
        map.Property(x => x.UpdatedAt, p => p.Column("updated_at"));
        map.Property(x => x.UpdatedBy, p => p.Column("updated_by"));
        map.Property(x => x.IsDeleted, p => p.Column("is_deleted"));
        map.Where("is_deleted = false");
        map.DynamicUpdate(true);
    }
}

public class MerchantMap : ClassMapping<Merchant>
{
    public MerchantMap()
    {
        Table("merchants");
        this.MapBase();
        Property(x => x.Name, p => { p.Column("name"); p.NotNullable(true); });
        Property(x => x.TaxNumber, p => p.Column("tax_number"));
        Property(x => x.Status, p =>
        {
            p.Column("status");
            p.Type<EnumStringType<MerchantStatus>>();
        });
        Property(x => x.DefaultCommissionRate, p => p.Column("default_commission_rate"));
        Component(x => x.Iban, c =>
        {
            c.Property(i => i.Value, p => p.Column("iban"));
        });
        Bag(x => x.Stores, bag =>
        {
            bag.Key(k => k.Column("merchant_id"));
            bag.Inverse(true);
            bag.Cascade(Cascade.None);
        }, rel => rel.OneToMany());
        Bag(x => x.CommissionRules, bag =>
        {
            bag.Key(k => k.Column("merchant_id"));
            bag.Inverse(true);
            bag.Cascade(Cascade.None);
        }, rel => rel.OneToMany());
    }
}

public class StoreMap : ClassMapping<Store>
{
    public StoreMap()
    {
        Table("stores");
        this.MapBase();
        Property(x => x.MerchantId, p => p.Column("merchant_id"));
        Property(x => x.Name, p => p.Column("name"));
        Property(x => x.Address, p => p.Column("address"));
        Bag(x => x.Terminals, bag =>
        {
            bag.Key(k => k.Column("store_id"));
            bag.Inverse(true);
            bag.Cascade(Cascade.None);
        }, rel => rel.OneToMany());
    }
}

public class TerminalMap : ClassMapping<Terminal>
{
    public TerminalMap()
    {
        Table("terminals");
        this.MapBase();
        Property(x => x.StoreId, p => p.Column("store_id"));
        Property(x => x.TerminalCode, p => p.Column("terminal_code"));
        Property(x => x.BankProviderCode, p => p.Column("bank_provider_code"));
        Property(x => x.IsActive, p => p.Column("is_active"));
    }
}

public class PaymentTransactionMap : ClassMapping<PaymentTransaction>
{
    public PaymentTransactionMap()
    {
        Table("payment_transactions");
        this.MapBase();
        Property(x => x.MerchantId, p => p.Column("merchant_id"));
        Property(x => x.TerminalId, p => p.Column("terminal_id"));
        Property(x => x.OrderReference, p => p.Column("order_reference"));
        Component(x => x.Amount, c =>
        {
            c.Property(m => m.Amount, p => p.Column("amount"));
            c.Property(m => m.Currency, p => p.Column("currency"));
        });
        Property(x => x.InstallmentCount, p => p.Column("installment_count"));
        Property(x => x.TransactionType, p =>
        {
            p.Column("transaction_type");
            p.Type<EnumStringType<TransactionType>>();
        });
        Property(x => x.Status, p =>
        {
            p.Column("status");
            p.Type<EnumStringType<TransactionStatus>>();
        });
        Component(x => x.MaskedCard, c =>
        {
            c.Property(m => m.Value, p => p.Column("masked_card_number"));
        });
        Property(x => x.CardHolderName, p => p.Column("card_holder_name"));
        Property(x => x.BankAuthCode, p => p.Column("bank_auth_code"));
        Property(x => x.BankProviderCode, p => p.Column("bank_provider_code"));
        Property(x => x.IdempotencyKey, p => { p.Column("idempotency_key"); p.Unique(true); });
        Property(x => x.CommissionAmount, p => p.Column("commission_amount"));
        Property(x => x.NetAmount, p => p.Column("net_amount"));
        Property(x => x.RefundedTotal, p => p.Column("refunded_total"));
        Property(x => x.RequestedAt, p => p.Column("requested_at"));
        Property(x => x.CompletedAt, p => p.Column("completed_at"));
        Bag(x => x.StatusHistory, bag =>
        {
            bag.Key(k => k.Column("transaction_id"));
            bag.Inverse(true);
            bag.Cascade(Cascade.All);
        }, rel => rel.OneToMany());
    }
}

public class TransactionStatusHistoryMap : ClassMapping<TransactionStatusHistory>
{
    public TransactionStatusHistoryMap()
    {
        Table("transaction_status_history");
        this.MapBase();
        Property(x => x.TransactionId, p => p.Column("transaction_id"));
        Property(x => x.OldStatus, p =>
        {
            p.Column("old_status");
            p.Type<EnumStringType<TransactionStatus>>();
        });
        Property(x => x.NewStatus, p =>
        {
            p.Column("new_status");
            p.Type<EnumStringType<TransactionStatus>>();
        });
        Property(x => x.ChangedAt, p => p.Column("changed_at"));
        Property(x => x.ChangedBy, p => p.Column("changed_by"));
    }
}

public class RefundTransactionMap : ClassMapping<RefundTransaction>
{
    public RefundTransactionMap()
    {
        Table("refund_transactions");
        this.MapBase();
        Property(x => x.OriginalTransactionId, p => p.Column("original_transaction_id"));
        Property(x => x.RefundAmount, p => p.Column("refund_amount"));
        Property(x => x.Reason, p => p.Column("reason"));
        Property(x => x.Status, p =>
        {
            p.Column("status");
            p.Type<EnumStringType<RefundStatus>>();
        });
    }
}

public class CommissionRuleMap : ClassMapping<CommissionRule>
{
    public CommissionRuleMap()
    {
        Table("commission_rules");
        this.MapBase();
        Property(x => x.MerchantId, p => p.Column("merchant_id"));
        Property(x => x.InstallmentCount, p => p.Column("installment_count"));
        Property(x => x.Rate, p => p.Column("rate"));
        Property(x => x.ValidFrom, p => p.Column("valid_from"));
        Property(x => x.ValidTo, p => p.Column("valid_to"));
    }
}

public class WebhookSubscriptionMap : ClassMapping<WebhookSubscription>
{
    public WebhookSubscriptionMap()
    {
        Table("webhook_subscriptions");
        this.MapBase();
        Property(x => x.MerchantId, p => p.Column("merchant_id"));
        Property(x => x.EventType, p => p.Column("event_type"));
        Property(x => x.TargetUrl, p => p.Column("target_url"));
        Property(x => x.Secret, p => p.Column("secret"));
        Property(x => x.IsActive, p => p.Column("is_active"));
    }
}

public class AuditLogMap : ClassMapping<AuditLog>
{
    public AuditLogMap()
    {
        Table("audit_logs");
        this.MapBase();
        Property(x => x.EntityName, p => p.Column("entity_name"));
        Property(x => x.EntityId, p => p.Column("entity_id"));
        Property(x => x.Action, p =>
        {
            p.Column("action");
            p.Type<EnumStringType<AuditAction>>();
        });
        Property(x => x.PerformedBy, p => p.Column("performed_by"));
        Property(x => x.PerformedAt, p => p.Column("performed_at"));
        Property(x => x.PayloadSnapshot, p =>
        {
            p.Column("payload_snapshot");
            p.Type<JsonbType>();
        });
    }
}

public class UserMap : ClassMapping<User>
{
    public UserMap()
    {
        Table("users");
        this.MapBase();
        Property(x => x.MerchantId, p => p.Column("merchant_id"));
        Property(x => x.UserName, p => p.Column("user_name"));
        Property(x => x.Email, p => p.Column("email"));
        Property(x => x.FullName, p => p.Column("full_name"));
        Property(x => x.PasswordHash, p => p.Column("password_hash"));
        Property(x => x.IsActive, p => p.Column("is_active"));
        Property(x => x.FailedLoginCount, p => p.Column("failed_login_count"));
        Property(x => x.LockoutEndAt, p => p.Column("lockout_end_at"));
        Bag(x => x.Roles, bag =>
        {
            bag.Key(k => k.Column("user_id"));
            bag.Inverse(true);
            bag.Cascade(Cascade.All);
        }, rel => rel.OneToMany());
    }
}

public class RoleMap : ClassMapping<Role>
{
    public RoleMap()
    {
        Table("roles");
        this.MapBase();
        Property(x => x.Name, p => p.Column("name"));
    }
}

public class UserRoleMap : ClassMapping<UserRole>
{
    public UserRoleMap()
    {
        Table("user_roles");
        this.MapBase();
        Property(x => x.UserId, p => p.Column("user_id"));
        ManyToOne(x => x.Role, m =>
        {
            m.Column("role_id");
            m.Insert(false);
            m.Update(false);
            m.Fetch(FetchKind.Join);
        });
        Property(x => x.RoleId, p => p.Column("role_id"));
    }
}

public class RefreshTokenMap : ClassMapping<RefreshToken>
{
    public RefreshTokenMap()
    {
        Table("refresh_tokens");
        this.MapBase();
        Property(x => x.UserId, p => p.Column("user_id"));
        Property(x => x.TokenHash, p => p.Column("token_hash"));
        Property(x => x.ExpiresAt, p => p.Column("expires_at"));
        Property(x => x.RevokedAt, p => p.Column("revoked_at"));
        Property(x => x.ReplacedByTokenHash, p => p.Column("replaced_by_token_hash"));
    }
}

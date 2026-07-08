# SanalPOS - Veritabanı Tasarımı

## 1. Genel Yaklaşım

- Veritabanı: **PostgreSQL 16**
- Şema adı: `sanalpos`
- Her tablo `snake_case` isimlendirme kullanır (PostgreSQL konvansiyonu), .NET tarafında `PascalCase` entity/property isimleri EF Core / NHibernate mapping katmanında dönüştürülür
- Tüm tablolarda: `id (uuid, PK)`, `created_at`, `created_by`, `updated_at`, `updated_by`, `is_deleted` (soft delete) standart kolonları bulunur
- Para tutarları `numeric(18,2)` tipinde, para birimi ayrı bir `currency` (char(3), ISO 4217) kolonunda tutulur
- Hassas veriler (kart numarası vb.) **asla düz metin olarak saklanmaz**; sadece maskeli/tokenize edilmiş halleri saklanır (bkz. [11-guvenlik.md](./11-guvenlik.md))

## 2. Varlık İlişki Diyagramı (Kavramsal)

```
Merchant 1---* Store 1---* Terminal
Merchant 1---* User (rol: MerchantAdmin, Operator)
Merchant 1---* PaymentTransaction
PaymentTransaction 1---1 CardInfo (masked)
PaymentTransaction 1---* TransactionStatusHistory
PaymentTransaction *---1 BankProvider
PaymentTransaction 1---0..1 RefundTransaction
Merchant 1---* CommissionRule
Merchant 1---* WebhookSubscription
User *---* Role (many-to-many, RBAC)
PaymentTransaction 1---* AuditLog (append-only)
```

## 3. Temel Tablolar

### 3.1 `merchants` (Üye İşyerleri)
| Kolon | Tip | Açıklama |
|---|---|---|
| id | uuid PK | |
| name | varchar(200) | İşyeri adı |
| tax_number | varchar(20) | Vergi no |
| iban | varchar(34) | Tahsilat IBAN |
| status | varchar(20) | Active / Suspended / Closed |
| default_commission_rate | numeric(5,2) | Varsayılan komisyon oranı % |
| created_at / updated_at | timestamptz | |

### 3.2 `stores` (Mağazalar)
| Kolon | Tip | Açıklama |
|---|---|---|
| id | uuid PK | |
| merchant_id | uuid FK -> merchants.id | |
| name | varchar(200) | |
| address | text | |

### 3.3 `terminals` (Sanal POS Terminalleri)
| Kolon | Tip | Açıklama |
|---|---|---|
| id | uuid PK | |
| store_id | uuid FK -> stores.id | |
| terminal_code | varchar(50) unique | Banka nezdindeki terminal kodu |
| bank_provider_code | varchar(50) | Hangi banka adaptörü kullanılacak |
| is_active | boolean | |

### 3.4 `payment_transactions` (Ödeme İşlemleri) — **Çekirdek Tablo**
| Kolon | Tip | Açıklama |
|---|---|---|
| id | uuid PK | |
| merchant_id | uuid FK | |
| terminal_id | uuid FK | |
| order_reference | varchar(100) | Merchant'ın sipariş no'su |
| amount | numeric(18,2) | Tutar |
| currency | char(3) | TRY, USD, EUR vb. |
| installment_count | smallint | Taksit sayısı (1 = peşin) |
| transaction_type | varchar(20) | Sale / PreAuth / Capture / Refund / Void |
| status | varchar(20) | Pending / Approved / Declined / Reversed / Refunded |
| masked_card_number | varchar(25) | ör. 4021 22** **** 1234 |
| card_holder_name | varchar(150) | |
| bank_auth_code | varchar(50) | Banka onay kodu |
| bank_provider_code | varchar(50) | İşlemin gönderildiği banka |
| idempotency_key | varchar(100) unique | Tekrar eden istek koruması |
| commission_amount | numeric(18,2) | Hesaplanan komisyon |
| net_amount | numeric(18,2) | Komisyon sonrası net tutar |
| requested_at | timestamptz | |
| completed_at | timestamptz nullable | |

> **Not**: `masked_card_number` dışında kart bilgisi (CVV, tam PAN) **hiçbir zaman** veritabanına yazılmaz.

### 3.5 `transaction_status_history`
| Kolon | Tip | Açıklama |
|---|---|---|
| id | uuid PK | |
| transaction_id | uuid FK | |
| old_status | varchar(20) | |
| new_status | varchar(20) | |
| changed_at | timestamptz | |
| changed_by | varchar(100) | Sistem/kullanıcı |

### 3.6 `refund_transactions`
| Kolon | Tip | Açıklama |
|---|---|---|
| id | uuid PK | |
| original_transaction_id | uuid FK -> payment_transactions.id | |
| refund_amount | numeric(18,2) | |
| reason | varchar(500) | |
| status | varchar(20) | |

### 3.7 `commission_rules`
| Kolon | Tip | Açıklama |
|---|---|---|
| id | uuid PK | |
| merchant_id | uuid FK | |
| installment_count | smallint | Hangi taksit için geçerli |
| rate | numeric(5,2) | Komisyon oranı % |
| valid_from / valid_to | date | |

### 3.8 `users`, `roles`, `user_roles` (ASP.NET Identity tabanlı)
Standart Identity şeması genişletilerek `merchant_id` (nullable, sistem admini için null) eklenir.

### 3.9 `webhook_subscriptions`
| Kolon | Tip | Açıklama |
|---|---|---|
| id | uuid PK | |
| merchant_id | uuid FK | |
| event_type | varchar(50) | PaymentCompleted, RefundCompleted vb. |
| target_url | varchar(500) | |
| secret | varchar(200) | HMAC imzalama için (şifreli saklanır) |
| is_active | boolean | |

### 3.10 `audit_logs` (Append-Only)
| Kolon | Tip | Açıklama |
|---|---|---|
| id | uuid PK | |
| entity_name | varchar(100) | |
| entity_id | uuid | |
| action | varchar(20) | Create/Update/Delete/StatusChange |
| performed_by | varchar(100) | |
| performed_at | timestamptz | |
| payload_snapshot | jsonb | Değişiklik anındaki veri |

## 4. EF Core Mapping Yaklaşımı

- `SanalPosDbContext : DbContext`
- Fluent API ile `IEntityTypeConfiguration<T>` sınıfları kullanılır (her entity için ayrı configuration dosyası: `PaymentTransactionConfiguration.cs` vb.)
- Migration'lar `dotnet ef migrations add <İsim> --project src/SanalPOS.Infrastructure --startup-project src/SanalPOS.API`
- Value Object'ler (`Money`, `MaskedCardNumber`) `OwnsOne` ile mapping yapılır

```csharp
public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions", "sanalpos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.OwnsOne(x => x.MaskedCard, card =>
        {
            card.Property(c => c.Value).HasColumnName("masked_card_number").HasMaxLength(25);
        });
    }
}
```

## 5. NHibernate Mapping Yaklaşımı

NHibernate provider seçildiğinde aynı tablolar `hbm.xml` yerine **Fluent NHibernate** veya `ClassMap<T>` (Fluent NHibernate) ile eşlenir; iki ORM'in de aynı fiziksel şemaya yazması esastır (tek bir kaynak şema — migration'lar EF Core veya Flyway ile yönetilir, NHibernate sadece okuma/yazma yapar).

```csharp
public class PaymentTransactionMap : ClassMap<PaymentTransaction>
{
    public PaymentTransactionMap()
    {
        Table("sanalpos.payment_transactions");
        Id(x => x.Id).GeneratedBy.Assigned();
        Map(x => x.Amount).Column("amount").CustomSqlType("numeric(18,2)");
        Map(x => x.IdempotencyKey).Column("idempotency_key").Unique();
        Component(x => x.MaskedCard, c =>
        {
            c.Map(m => m.Value).Column("masked_card_number");
        });
    }
}
```

> **Önemli**: Her iki ORM implementasyonu da `IPaymentTransactionRepository` arayüzünü uygular; Application katmanı hangi ORM'in aktif olduğunu bilmez (bkz. [01-mimari.md](./01-mimari.md) §2).

## 6. İndeksleme Stratejisi

| Tablo | İndeks | Amaç |
|---|---|---|
| payment_transactions | (merchant_id, requested_at desc) | Merchant bazlı işlem listesi |
| payment_transactions | (idempotency_key) unique | Tekrar eden istek koruması |
| payment_transactions | (status) | Durum bazlı filtreleme/raporlama |
| audit_logs | (entity_name, entity_id) | Denetim sorguları |
| transaction_status_history | (transaction_id) | İşlem geçmişi sorgusu |

## 7. Partisyonlama ve Arşivleme (İleri Faz)

`payment_transactions` ve `audit_logs` tabloları için aylık **range partitioning** (PostgreSQL native partitioning) önerilir; 2 yıldan eski veriler arşiv şemasına taşınır.

## 8. Veri Saklama ve KVKK Uyumu

- Kart sahibi kişisel verileri minimum tutulur (sadece maskeli PAN + kart sahibi adı)
- Silme talebi (KVKK "unutulma hakkı") için `is_deleted` + anonimizasyon prosedürü (finansal kayıtlar yasal saklama süresi boyunca anonimize edilerek tutulur, tamamen silinmez)

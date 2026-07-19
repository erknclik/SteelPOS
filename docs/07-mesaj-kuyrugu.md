# SanalPOS - Mesaj Kuyruğu Stratejisi (RabbitMQ & Kafka)

## 1. Genel Yaklaşım

Sistem **hem RabbitMQ hem Kafka'yı** destekler. Hangi altyapının aktif olacağı `appsettings.json` üzerindeki `Messaging:Provider` (`RabbitMq` veya `Kafka`) alanı ile belirlenir. Application katmanı somut transport'u bilmez; sadece `IEventPublisher` / `IEventConsumer` arayüzlerini kullanır. Bu soyutlama **MassTransit** üzerinden sağlanır (MassTransit hem RabbitMQ hem Kafka transport'unu destekler).

```json
{
  "Messaging": {
    "Provider": "RabbitMq",
    "RabbitMq": {
      "Host": "localhost",
      "VirtualHost": "/",
      "Username": "guest",
      "Password": "guest"
    },
    "Kafka": {
      "BootstrapServers": "localhost:9092",
      "ConsumerGroupId": "sanalpos-consumers"
    }
  }
}
```

## 2. Ne Zaman Hangisi Kullanılmalı?

| Senaryo | Önerilen | Neden |
|---|---|---|
| Bildirim gönderimi (e-posta/SMS) | RabbitMQ | Düşük gecikme, basit iş kuyruğu |
| Webhook tetikleme | RabbitMQ | Retry/DLQ desteği kolay |
| Audit/event stream (analitik, veri gölü besleme) | Kafka | Yüksek hacim, sıralı, replay edilebilir log |
| Mikroservisler arası domain event yayını (ileri faz) | Kafka | Çoklu tüketici, uzun süreli saklama |
| Zamanlanmış/tekil iş kuyruğu (mutabakat batch tetikleme) | RabbitMQ | Basit kuyruk semantiği |

Karar kullanıcıya/işletmeye bırakılır; her iki implementasyon da aynı event sözleşmelerini (contract) kullanır, böylece geçiş şeffaftır.

## 3. Provider Seçim Mekanizması (DI)

```csharp
var messagingProvider = builder.Configuration["Messaging:Provider"] ?? "RabbitMq";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumers(typeof(PaymentCompletedConsumer).Assembly);

    if (messagingProvider == "Kafka")
    {
        x.AddRider(rider =>
        {
            rider.AddProducer<PaymentCompletedEvent>("payment-completed");
            rider.AddConsumer<PaymentCompletedKafkaConsumer>();
            rider.UsingKafka((context, k) =>
            {
                k.Host(builder.Configuration["Messaging:Kafka:BootstrapServers"]);
                k.TopicEndpoint<PaymentCompletedEvent>("payment-completed",
                    builder.Configuration["Messaging:Kafka:ConsumerGroupId"], e =>
                {
                    e.ConfigureConsumer<PaymentCompletedKafkaConsumer>(context);
                });
            });
        });
    }
    else
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["Messaging:RabbitMq:Host"], "/", h =>
            {
                h.Username(builder.Configuration["Messaging:RabbitMq:Username"]);
                h.Password(builder.Configuration["Messaging:RabbitMq:Password"]);
            });
            cfg.ConfigureEndpoints(context);
        });
    }
});
```

## 4. Event Sözleşmeleri (Contracts)

Ortak bir `SanalPOS.Contracts` projesinde tanımlanır (hem publisher hem consumer bu paketi referans alır):

```csharp
public record PaymentCompletedEvent(
    Guid TransactionId,
    Guid MerchantId,
    decimal Amount,
    string Currency,
    DateTime CompletedAtUtc,
    string CorrelationId);

public record PaymentFailedEvent(
    Guid TransactionId,
    Guid MerchantId,
    string ReasonCode,
    string ReasonMessage,
    DateTime FailedAtUtc,
    string CorrelationId);

public record RefundCompletedEvent(
    Guid RefundTransactionId,
    Guid OriginalTransactionId,
    Guid MerchantId,
    decimal RefundAmount,
    string Currency,
    DateTime CompletedAtUtc,
    string CorrelationId);
```

## 5. Ana Event Listesi

| Event | Tetiklendiği An | Tüketiciler |
|---|---|---|
| `PaymentCompletedEvent` | Ödeme onaylandığında | Bildirim servisi, Webhook dispatcher, Muhasebe entegrasyonu, Audit |
| `PaymentFailedEvent` | Ödeme reddedildiğinde | Bildirim servisi, İzleme/alerting |
| `RefundCompletedEvent` | İade tamamlandığında | Bildirim servisi, Webhook dispatcher, Muhasebe |
| `MerchantSuspendedEvent` | Merchant askıya alındığında | Bildirim servisi, Terminal deaktivasyon işlemi |
| `DailyReconciliationRequestedEvent` | Zamanlanmış görev tetiklediğinde | Reconciliation modülü |

## 6. Publish Akışı (Application Katmanı)

```csharp
public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentResultDto>
{
    private readonly IPublishEndpoint _publishEndpoint; // MassTransit soyutlaması

    public async Task<PaymentResultDto> Handle(CreatePaymentCommand request, CancellationToken ct)
    {
        // ... ödeme işlemi tamamlandı
        await _publishEndpoint.Publish(new PaymentCompletedEvent(
            transaction.Id, transaction.MerchantId, transaction.Amount,
            transaction.Currency, DateTime.UtcNow, _correlationIdAccessor.CorrelationId), ct);

        return result;
    }
}
```

> Application katmanı `IPublishEndpoint` (MassTransit'in transport'tan bağımsız arayüzü) kullanır; RabbitMQ mı Kafka mı olduğunu bilmez.

## 7. Consumer Örneği

```csharp
public class PaymentCompletedNotificationConsumer : IConsumer<PaymentCompletedEvent>
{
    public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var e = context.Message;
        await _notificationService.SendPaymentReceiptAsync(e.MerchantId, e.TransactionId, e.Amount);
    }
}
```

## 8. Hata Yönetimi ve Dayanıklılık (Resilience)

- **RabbitMQ**: MassTransit'in yerleşik retry (`UseMessageRetry`) ve **Dead Letter Queue (DLQ)** mekanizması kullanılır (`_error`, `_skipped` kuyrukları otomatik oluşur)
- **Kafka**: Consumer hata durumunda mesaj yeniden işlenir (retry topic pattern: `payment-completed-retry-1`, `-retry-2`, son çare `payment-completed-dlq`)
- Her iki durumda da **en az bir kez teslim (at-least-once delivery)** garantisi esas alınır; consumer'lar **idempotent** yazılır (aynı event iki kez işlense bile yan etkisi tek olmalı — ör. `TransactionId` bazlı kontrol)

```csharp
x.UsingRabbitMq((context, cfg) =>
{
    cfg.UseMessageRetry(r => r.Exponential(5,
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
    cfg.ConfigureEndpoints(context);
});
```

## 9. Outbox Deseni (Transactional Messaging)

Ödeme kaydının veritabanına yazılması ile event'in kuyruğa publish edilmesi **aynı transaction** içinde tutarlı olmalıdır. Bunun için **MassTransit Entity Framework Core Outbox** (veya NHibernate için custom outbox implementasyonu) kullanılır:

```csharp
services.AddDbContext<SanalPosDbContext>();
services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<SanalPosDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });
});
```

NHibernate provider aktifken outbox tablosu (`outbox_messages`) manuel bir `IOutboxRepository` ile yönetilir ve arka planda bir `OutboxProcessor` (Hangfire/BackgroundService) mesajları kuyruğa aktarır.

## 10. İzleme

- RabbitMQ: **RabbitMQ Management UI** (kuyruk derinliği, tüketim hızı)
- Kafka: **Kafka UI / Conduktor / Confluent Control Center**, consumer lag izleme
- Her iki transport için de MassTransit'in yerleşik **health check** entegrasyonu `/health` endpoint'ine eklenir

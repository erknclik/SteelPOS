# SanalPOS - Mimari Doküman

## 1. Mimari Yaklaşım: Clean Architecture (+ CQRS)

SanalPOS, **Clean Architecture** (Onion Architecture) prensipleriyle katmanlandırılır. Bağımlılıklar her zaman dıştan içe doğru akar; iç katmanlar dış katmanları bilmez.

```
┌─────────────────────────────────────────────────────────┐
│  SanalPOS.API (Presentation)                             │
│  Controllers, Middleware, Filters, SignalR Hubs           │
├─────────────────────────────────────────────────────────┤
│  SanalPOS.Infrastructure                                  │
│  EF Core / NHibernate, Redis, RabbitMQ/Kafka,             │
│  Serilog/NLog, Banka Adaptörleri, Dış Servis İstemcileri  │
├─────────────────────────────────────────────────────────┤
│  SanalPOS.Application                                      │
│  Use Case'ler (Command/Query Handlers - CQRS),             │
│  FluentValidation Validators, DTO/Mapping, Interfaces      │
├─────────────────────────────────────────────────────────┤
│  SanalPOS.Domain (Core)                                    │
│  Entities, Value Objects, Domain Events, Enums,            │
│  Domain Servisleri, İş Kuralları                           │
└─────────────────────────────────────────────────────────┘
```

### 1.1 Domain Katmanı (Çekirdek)
- Hiçbir dış bağımlılığı yoktur (framework-agnostic)
- Entity'ler, Value Object'ler (ör. `Money`, `CardNumberMasked`, `IBAN`)
- Domain Event'ler (ör. `PaymentCompletedDomainEvent`)
- Repository **arayüzleri** burada tanımlanır, implementasyonu Infrastructure'da yapılır

### 1.2 Application Katmanı
- **CQRS** deseni: `MediatR` kullanılarak Command/Query ayrımı
- Her Command/Query için bir Handler
- FluentValidation ile Command/Query validasyonu (MediatR pipeline behavior olarak)
- `ICacheService`, `IMessagePublisher`, `ILoggerAdapter`, `IPaymentGatewayAdapter` gibi soyut arayüzler burada tanımlanır
- AutoMapper veya Mapster ile DTO dönüşümleri

### 1.3 Infrastructure Katmanı
- **Persistence**: EF Core `DbContext` ve NHibernate `SessionFactory` implementasyonları; `IUnitOfWork` ve `IRepository<T>` somutlaştırılır
- **Caching**: Redis `IDistributedCache` / `StackExchange.Redis` implementasyonu
- **Messaging**: RabbitMQ (`MassTransit` veya `EasyNetQ`) ve Kafka (`Confluent.Kafka`) implementasyonları
- **Logging**: Serilog ve NLog sink konfigürasyonları
- **External Services**: Banka/ödeme kuruluşu adaptörleri (HTTP client wrapper'ları, Polly retry/circuit breaker)

### 1.4 API (Presentation) Katmanı
- ASP.NET Core Web API Controller'ları (ince controller, iş mantığı içermez)
- Middleware'ler: Global Exception Handling, Request Logging, Correlation-Id, Rate Limiting
- Authentication/Authorization (JWT Bearer)
- API Versiyonlama (`Asp.Versioning`)
- Swagger/OpenAPI dokümantasyonu

## 2. Provider Pattern (Değiştirilebilir Altyapı)

Proje gereksinimi gereği üç alanda birden fazla teknoloji desteklenir ve **appsettings.json / environment variable** üzerinden seçim yapılır:

| Alan | Seçenekler | Konfigürasyon Anahtarı |
|---|---|---|
| ORM | `EfCore`, `NHibernate` | `Persistence:Provider` |
| Log | `Serilog`, `NLog` | `Logging:Provider` |
| Mesaj Kuyruğu | `RabbitMq`, `Kafka` | `Messaging:Provider` |

Bu seçim, `Program.cs` içinde bir **Factory/Strategy** deseniyle `IServiceCollection` üzerine ilgili implementasyonun kayıt edilmesiyle (Dependency Injection) sağlanır. Detaylar için bkz. [14-konfigurasyon-yonetimi.md](./14-konfigurasyon-yonetimi.md)

```csharp
// Program.cs - basitleştirilmiş örnek
var persistenceProvider = builder.Configuration["Persistence:Provider"];
switch (persistenceProvider)
{
    case "NHibernate":
        builder.Services.AddNHibernatePersistence(builder.Configuration);
        break;
    case "EfCore":
    default:
        builder.Services.AddEfCorePersistence(builder.Configuration);
        break;
}
```

Her iki ORM implementasyonu da aynı `IRepository<T>` ve `IUnitOfWork` arayüzlerini uygular; Application katmanı hangi ORM'in kullanıldığını bilmez.

## 3. CQRS + MediatR Akışı

```
Controller -> MediatR.Send(Command/Query)
           -> ValidationBehavior (FluentValidation)
           -> LoggingBehavior (Serilog/NLog)
           -> CachingBehavior (Query ise, Redis)
           -> Handler (iş mantığı)
           -> Repository (EF Core/NHibernate)
           -> Domain Event Dispatch (varsa)
           -> MessagePublisher (RabbitMQ/Kafka, varsa)
           -> Response DTO
```

## 4. Event-Driven Yaklaşım

Kritik iş olayları (ödeme tamamlandı, iade yapıldı, provizyon iptal edildi vb.) **domain event** olarak üretilir ve:
1. Aynı process içinde MediatR `INotification` ile senkron handler'lara iletilir (ör. audit log yazımı)
2. Asenkron olarak mesaj kuyruğuna (RabbitMQ/Kafka) publish edilir (ör. bildirim servisi, muhasebe entegrasyonu, webhook tetikleme)

Bkz. [07-mesaj-kuyrugu.md](./07-mesaj-kuyrugu.md)

## 5. Mikroservis mi, Modüler Monolit mi?

**İlk faz: Modüler Monolit.** Sistem, aşağıdaki mantıksal modüllere ayrılır fakat tek bir deployment biriminde (opsiyonel olarak modül bazlı ayrı deploy edilebilecek şekilde) çalışır:

- **Payments** (ödeme işlemleri çekirdeği)
- **Merchants** (üye işyeri yönetimi)
- **Identity** (kullanıcı/rol/yetkilendirme)
- **Reporting** (raporlama/mutabakat)
- **Notifications** (bildirim)

İleri fazda, yük/ölçeklenme ihtiyacına göre `Payments` ve `Notifications` modülleri bağımsız mikroservislere ayrılabilir; modüler monolit yapı bu geçişi kolaylaştırmak için tasarlanır (her modül kendi Application/Domain alt katmanına sahiptir).

## 6. Çapraz Kesen Konular (Cross-Cutting Concerns)

- **Idempotency**: Ödeme işlemlerinde `Idempotency-Key` header'ı ile tekrar eden isteklerin önlenmesi (Redis üzerinde kısa süreli kayıt)
- **Correlation-Id**: Her istekte üretilen/aktarılan correlation id, loglara ve mesaj kuyruğu event'lerine eklenir
- **Resilience**: Polly ile retry, circuit breaker, timeout politikaları (özellikle banka adaptör çağrılarında)
- **Rate Limiting**: ASP.NET Core `RateLimiter` middleware ile merchant bazlı limitleme
- **Audit Trail**: Tüm finansal komutlar için değişmez (append-only) audit tablosu

## 7. Tasarım Prensipleri

- SOLID prensipleri
- Dependency Inversion: Application katmanı somut teknolojilere değil arayüzlere bağımlı
- Fail-fast validasyon (FluentValidation, pipeline'ın en başında)
- Anlamlı domain modelleme (anemic model'den kaçınma, Value Object kullanımı)
- Test edilebilirlik önceliği (bkz. [12-test-stratejisi.md](./12-test-stratejisi.md))

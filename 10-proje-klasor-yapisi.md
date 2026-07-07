# SanalPOS - Klasör / Solution Yapısı

## 1. Genel Depo (Repository) Yapısı

Monorepo yaklaşımı önerilir (backend + frontend + docs + infra tek repoda):

```
sanalpos/
├── docs/                              # Bu dokümantasyon seti
│   ├── 00-proje-genel-bakis.md
│   ├── 01-mimari.md
│   ├── ... (diğer md dosyaları)
├── src/
│   ├── Backend/
│   │   ├── SanalPOS.sln
│   │   ├── SanalPOS.Domain/
│   │   ├── SanalPOS.Application/
│   │   ├── SanalPOS.Infrastructure/
│   │   ├── SanalPOS.Infrastructure.EfCore/
│   │   ├── SanalPOS.Infrastructure.NHibernate/
│   │   ├── SanalPOS.Infrastructure.Redis/
│   │   ├── SanalPOS.Infrastructure.Messaging.RabbitMq/
│   │   ├── SanalPOS.Infrastructure.Messaging.Kafka/
│   │   ├── SanalPOS.Infrastructure.Logging.Serilog/
│   │   ├── SanalPOS.Infrastructure.Logging.NLog/
│   │   ├── SanalPOS.Contracts/            # Event/DTO sözleşmeleri (backend<->mq paylaşımlı)
│   │   ├── SanalPOS.API/
│   │   └── SanalPOS.BackgroundJobs/       # Hangfire/BackgroundService tabanlı zamanlanmış işler
│   └── Frontend/
│       └── sanalpos-web/                  # React + Vite uygulaması
├── tests/
│   ├── SanalPOS.Domain.UnitTests/
│   ├── SanalPOS.Application.UnitTests/
│   ├── SanalPOS.Infrastructure.IntegrationTests/
│   ├── SanalPOS.API.IntegrationTests/
│   └── SanalPOS.Architecture.Tests/       # NetArchTest ile katman bağımlılık kuralları
├── infra/
│   ├── docker-compose.yml
│   ├── docker-compose.override.yml
│   ├── k8s/                               # (ileri faz) Helm/manifest dosyaları
│   └── scripts/ (db-init.sql, seed-data.sql)
├── .github/
│   └── workflows/
│       ├── backend-ci.yml
│       ├── frontend-ci.yml
│       └── deploy.yml
├── .editorconfig
├── .gitignore
├── Directory.Build.props
├── Directory.Packages.props               # Central Package Management
└── README.md
```

## 2. Backend Katman Detayları

### `SanalPOS.Domain`
```
SanalPOS.Domain/
  Entities/ (PaymentTransaction.cs, Merchant.cs, Store.cs, Terminal.cs, ...)
  ValueObjects/ (Money.cs, MaskedCardNumber.cs, Iban.cs)
  Enums/ (TransactionStatus.cs, TransactionType.cs)
  Events/ (PaymentCompletedDomainEvent.cs)
  Exceptions/ (DomainException.cs)
  Interfaces/ (IRepository.cs, IUnitOfWork.cs, IPaymentTransactionRepository.cs)
  Common/ (BaseEntity.cs, IAuditableEntity.cs)
```

### `SanalPOS.Application`
```
SanalPOS.Application/
  Common/
    Behaviors/ (ValidationBehavior.cs, LoggingBehavior.cs, CachingBehavior.cs)
    Interfaces/ (ICacheService.cs, ICurrentUserService.cs, IPaymentGatewayAdapter.cs)
    Mappings/ (MappingProfile.cs)
    Exceptions/ (SanalPosValidationException.cs, NotFoundException.cs)
  Payments/
    Commands/CreatePayment/, PreAuth/, Capture/, Void/, Refund/
    Queries/GetTransactionById/, GetTransactionList/
    Dtos/
  Merchants/
    Commands/, Queries/, Dtos/
  Reporting/
    Queries/
  DependencyInjection.cs
```

### `SanalPOS.Infrastructure` (Ortak/Shared)
```
SanalPOS.Infrastructure/
  BankAdapters/
    IBankProviderAdapter.cs
    MockBankAdapter.cs
    IsBankasiAdapter.cs (örnek)
  Security/ (TokenService.cs, PasswordHasher.cs)
  Persistence.Common/ (IRepositoryBase implementasyon yardımcıları)
  DependencyInjection.cs   # Provider seçim switch mantığı burada toplanır
```

### `SanalPOS.Infrastructure.EfCore`
```
SanalPOS.Infrastructure.EfCore/
  SanalPosDbContext.cs
  Configurations/ (PaymentTransactionConfiguration.cs, ...)
  Repositories/ (EfPaymentTransactionRepository.cs, ...)
  Migrations/
  UnitOfWork.cs
  DependencyInjection.cs (AddEfCorePersistence)
```

### `SanalPOS.Infrastructure.NHibernate`
```
SanalPOS.Infrastructure.NHibernate/
  NHibernateSessionFactoryProvider.cs
  Mappings/ (PaymentTransactionMap.cs, ...)
  Repositories/ (NHibernatePaymentTransactionRepository.cs, ...)
  UnitOfWork.cs
  DependencyInjection.cs (AddNHibernatePersistence)
```

### `SanalPOS.Infrastructure.Redis`
```
SanalPOS.Infrastructure.Redis/
  RedisCacheService.cs
  RedisDistributedLockService.cs
  DependencyInjection.cs (AddRedisCache)
```

### `SanalPOS.Infrastructure.Messaging.RabbitMq` / `.Kafka`
```
SanalPOS.Infrastructure.Messaging.RabbitMq/
  DependencyInjection.cs (AddRabbitMqMessaging)
  Consumers/ (varsa transport'a özel consumer'lar)

SanalPOS.Infrastructure.Messaging.Kafka/
  DependencyInjection.cs (AddKafkaMessaging)
```

### `SanalPOS.Infrastructure.Logging.Serilog` / `.NLog`
```
SanalPOS.Infrastructure.Logging.Serilog/
  SerilogConfigurator.cs
SanalPOS.Infrastructure.Logging.NLog/
  NLogConfigurator.cs
  nlog.config
```

### `SanalPOS.API`
```
SanalPOS.API/
  Controllers/ (PaymentsController.cs, MerchantsController.cs, AuthController.cs, ReportingController.cs, WebhooksController.cs)
  Middleware/ (ExceptionHandlingMiddleware.cs, CorrelationIdMiddleware.cs)
  Filters/
  Program.cs
  appsettings.json
  appsettings.Development.json
  appsettings.Production.json
```

## 3. Frontend Yapısı

Bkz. [09-frontend-react.md](./09-frontend-react.md) §2

## 4. Proje Referans Kuralları (Katman Bağımlılığı)

```
SanalPOS.API              -> Application, Infrastructure(.*)
SanalPOS.Infrastructure.*  -> Application, Domain
SanalPOS.Application       -> Domain
SanalPOS.Domain            -> (hiçbir şeye bağımlı değil)
SanalPOS.Contracts         -> (bağımsız, sadece POCO/record'lar)
```

Bu kural **`SanalPOS.Architecture.Tests`** projesinde `NetArchTest.Rules` ile otomatik doğrulanır; örneğin Domain katmanının Infrastructure'a referans vermediği CI'da test edilir.

## 5. Adlandırma Konvansiyonları

- Namespace = Proje adı ile başlar: `SanalPOS.Application.Payments.Commands.CreatePayment`
- Interface'ler `I` prefix'i ile (`IPaymentTransactionRepository`)
- Command/Query isimleri fiil + nesne şeklinde (`CreatePaymentCommand`, `GetTransactionByIdQuery`)
- DTO'lar `Dto` son eki ile (`PaymentResultDto`)

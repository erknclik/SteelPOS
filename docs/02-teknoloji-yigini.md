# SanalPOS - Teknoloji Yığını

## 1. Backend

| Kütüphane / Araç | Versiyon (öneri) | Amaç |
|---|---|---|
| .NET SDK | .NET 8 (LTS) | Çalışma zamanı |
| ASP.NET Core Web API | 8.x | REST API |
| MediatR | 12.x | CQRS, in-process mesajlaşma |
| FluentValidation | 11.x | Validasyon |
| FluentValidation.AspNetCore | 11.x | MVC entegrasyonu |
| AutoMapper veya Mapster | AutoMapper 13.x / Mapster 7.x | Nesne eşleme (DTO <-> Entity) |
| Entity Framework Core | 8.x | ORM (birincil provider) |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.x | PostgreSQL EF Core sağlayıcısı |
| NHibernate | 5.5.x | ORM (alternatif provider) |
| NHibernate.Extensions.DependencyInjection | güncel | DI entegrasyonu |
| Serilog.AspNetCore | 8.x | Loglama (birincil sağlayıcı seçeneği) |
| Serilog.Sinks.Console / File / Seq / Elasticsearch | güncel | Log çıktı hedefleri |
| NLog.Web.AspNetCore | 5.x | Loglama (alternatif sağlayıcı seçeneği) |
| StackExchange.Redis | 2.x | Redis istemcisi |
| Microsoft.Extensions.Caching.StackExchangeRedis | 8.x | Distributed cache |
| MassTransit | 8.x | RabbitMQ & Kafka için mesajlaşma soyutlaması |
| MassTransit.RabbitMQ | 8.x | RabbitMQ transport |
| MassTransit.Confluent.Kafka veya Confluent.Kafka | 8.x / 2.x | Kafka transport |
| Polly | 8.x | Retry, circuit breaker, resilience |
| Asp.Versioning.Mvc | 8.x | API versiyonlama |
| Swashbuckle.AspNetCore | 6.x | Swagger/OpenAPI |
| FluentAssertions | 6.x | Test assertion |
| xUnit | 2.x | Unit test framework |
| Testcontainers | 3.x | Integration test (PostgreSQL, Redis, RabbitMQ konteynerleri) |
| Bogus | 35.x | Test verisi üretimi (fake data) |
| Microsoft.AspNetCore.Identity | 8.x | Kimlik/rol yönetimi |
| System.IdentityModel.Tokens.Jwt | 7.x | JWT üretimi/doğrulaması |
| BCrypt.Net-Next veya Identity PasswordHasher | güncel | Parola hashleme |
| Hangfire (opsiyonel) | 1.8.x | Zamanlanmış işler (mutabakat, batch işlemler) |
| HealthChecks (AspNetCore.HealthChecks.*) | 8.x | PostgreSQL/Redis/RabbitMQ/Kafka sağlık kontrolü |

## 2. Veritabanı

- **PostgreSQL 16** — birincil ilişkisel veritabanı
- **pgAdmin** — yerel geliştirme için yönetim arayüzü
- **Redis 7** — cache, session, idempotency key, rate limit sayaçları
- **Flyway veya EF Core Migrations** — şema versiyonlama (EF Core provider seçiliyse `dotnet ef migrations`; NHibernate seçiliyse Flyway/DbUp önerilir çünkü NHibernate'in yerleşik migration aracı yoktur)

## 3. Mesajlaşma

- **RabbitMQ 3.13** — düşük gecikmeli, iş kuyruğu tarzı senaryolar (bildirim, webhook tetikleme)
- **Apache Kafka 3.7** (Confluent platform veya vanilla) — yüksek hacimli event streaming, audit log akışı, analytics besleme
- **MassTransit** her iki transport için de ortak bir soyutlama katmanı sağlar; `IPublishEndpoint` / `IBus` arayüzü Application katmanında kullanılır, transport seçimi Infrastructure'da yapılır

## 4. Frontend

| Kütüphane | Versiyon (öneri) | Amaç |
|---|---|---|
| React | 18.x | UI kütüphanesi |
| TypeScript | 5.x | Tip güvenliği |
| Vite | 5.x | Build tool / dev server |
| React Router | 6.x | Sayfa yönlendirme |
| TanStack Query (React Query) | 5.x | Server state yönetimi, cache |
| Zustand veya Redux Toolkit | güncel | Client state yönetimi |
| Axios | 1.x | HTTP istemcisi |
| React Hook Form | 7.x | Form yönetimi |
| Zod | 3.x | Frontend validasyon şeması (backend FluentValidation kurallarıyla paralel) |
| TailwindCSS | 3.x | Stil |
| shadcn/ui | güncel | Erişilebilir, özelleştirilebilir bileşen kütüphanesi |
| Recharts | 2.x | Raporlama grafikleri |
| i18next | 23.x | Çoklu dil desteği |
| Vitest + Testing Library | güncel | Frontend testleri |
| MSW (Mock Service Worker) | 2.x | API mock'lama (test ortamı) |

## 5. DevOps & Altyapı

| Araç | Amaç |
|---|---|
| Docker / Docker Compose | Yerel geliştirme ortamı (PostgreSQL, Redis, RabbitMQ, Kafka+Zookeeper/KRaft konteynerleri) |
| GitHub Actions | CI/CD pipeline |
| Nginx | Reverse proxy / statik dosya sunumu (React build) |
| Seq veya Elasticsearch+Kibana (ELK) | Log görüntüleme/arama |
| Prometheus + Grafana | Metrik toplama ve izleme |
| SonarQube / SonarCloud (opsiyonel) | Kod kalitesi analizi |
| HashiCorp Vault veya Azure Key Vault (opsiyonel) | Secret yönetimi |

## 6. Sürüm Uyumluluk Notu

Bu doküman öneri niteliğindedir; gerçek implementasyon sırasında `dotnet list package --outdated` ile güncel stabil sürümler kontrol edilmeli ve NuGet/npm paket sürümleri projeye özel `Directory.Packages.props` (Central Package Management) ile sabitlenmelidir.

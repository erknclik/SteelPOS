# SanalPOS - Dokümantasyon Seti

Bu klasör, SanalPOS projesinin uçtan uca teknik dokümantasyonunu içerir. Aşağıdaki sırayla okunması önerilir:

| # | Doküman | Kısa Açıklama |
|---|---|---|
| 00 | [Proje Genel Bakış](./00-proje-genel-bakis.md) | Amaç, kapsam, özellikler, sözlük |
| 01 | [Mimari](./01-mimari.md) | Clean Architecture, CQRS, provider pattern, event-driven yaklaşım |
| 02 | [Teknoloji Yığını](./02-teknoloji-yigini.md) | Kullanılan tüm kütüphane ve araçlar |
| 03 | [Veritabanı Tasarımı](./03-veritabani-tasarimi.md) | PostgreSQL şeması, EF Core & NHibernate mapping |
| 04 | [Validasyon](./04-validasyon.md) | FluentValidation kuralları ve pipeline entegrasyonu |
| 05 | [Loglama](./05-loglama.md) | Serilog & NLog, provider seçim mekanizması |
| 06 | [Cache (Redis)](./06-cache-redis.md) | Cache stratejisi, idempotency, distributed lock |
| 07 | [Mesaj Kuyruğu](./07-mesaj-kuyrugu.md) | RabbitMQ & Kafka, event sözleşmeleri, outbox deseni |
| 08 | [API Tasarımı](./08-api-tasarimi.md) | Endpoint listesi, örnek istek/yanıtlar |
| 09 | [Frontend (React)](./09-frontend-react.md) | UI mimarisi, sayfa/komponent yapısı |
| 10 | [Klasör/Solution Yapısı](./10-proje-klasor-yapisi.md) | Proje dizin yapısı, katman bağımlılık kuralları |
| 11 | [Güvenlik](./11-guvenlik.md) | PCI-DSS yaklaşımı, KVKK, şifreleme, audit |
| 12 | [Test Stratejisi](./12-test-stratejisi.md) | Unit, integration, e2e, mimari testler |
| 13 | [DevOps & Dağıtım](./13-devops-dagitim.md) | Docker, CI/CD, ortamlar, izleme |
| 14 | [Konfigürasyon Yönetimi](./14-konfigurasyon-yonetimi.md) | Provider seçim mekanizması (ORM/Log/MQ) |

## Projenin Ayırt Edici Noktası: Provider Pattern

SanalPOS, üç kritik altyapı bileşeninde **kullanıcı/işletme tercihine göre değiştirilebilir** bir tasarım sunar:

- **ORM**: EF Core ↔ NHibernate (`Persistence:Provider`)
- **Loglama**: Serilog ↔ NLog (`Logging:Provider`)
- **Mesaj Kuyruğu**: RabbitMQ ↔ Kafka (`Messaging:Provider`)

Bu seçimler `appsettings.json` üzerinden yönetilir ve Application katmanı hangi somut teknolojinin çalıştığını bilmez (bkz. [14-konfigurasyon-yonetimi.md](./14-konfigurasyon-yonetimi.md)).

## Sonraki Adımlar

Bu dokümanlar tasarım/plan aşamasıdır. Bir sonraki adım olarak istersen:
1. Solution/proje iskeletini (boş .csproj'lar, klasör yapısı) oluşturabiliriz
2. Domain katmanındaki ilk entity'leri (Merchant, PaymentTransaction) kod olarak yazabiliriz
3. Docker Compose ile yerel ortamı ayağa kaldırıp ilk migration'ı çalıştırabiliriz

Hangisiyle devam etmek istersin?

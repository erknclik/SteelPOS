# SanalPOS - Proje Genel Bakış

## 1. Proje Amacı

SanalPOS, işletmelerin online/fiziksel ödeme işlemlerini (sanal pos, kart tahsilatı, iade, taksitlendirme, komisyon hesaplama vb.) yönetebileceği; banka/ödeme kuruluşu entegrasyonlarına açık, ölçeklenebilir ve teknoloji tercihlerinde esneklik sunan bir ödeme altyapısı platformudur.

Sistem, kurumsal kullanım senaryolarını destekleyecek şekilde tasarlanır: çoklu banka entegrasyonu, çoklu ORM desteği, çoklu loglama sağlayıcısı ve çoklu mesajlaşma altyapısı gibi konularda **konfigürasyon üzerinden seçilebilen** (pluggable/provider pattern) bir mimari izlenir.

## 2. Temel Özellikler

- **Sanal POS İşlemleri**: Satış, iade, iptal, ön provizyon (pre-auth), provizyon kapama, taksitli işlem
- **Banka/Ödeme Kuruluşu Entegrasyonları**: Adapter pattern ile birden fazla banka API'sine bağlanabilme (ör. İş Bankası, Garanti, Akbank, Param, iyzico vb. simülasyon/mock altyapısı ile başlanır)
- **Üye İşyeri (Merchant) Yönetimi**: Çoklu işyeri, çoklu mağaza, komisyon oranları, günlük/aylık limitler
- **Kullanıcı ve Yetkilendirme**: Rol tabanlı erişim (RBAC), JWT tabanlı kimlik doğrulama
- **Raporlama**: Günlük mutabakat (reconciliation), ekstre, işlem geçmişi
- **Bildirim**: E-posta/SMS/Webhook bildirimleri (mesaj kuyruğu üzerinden asenkron)
- **Denetim (Audit)**: Tüm finansal işlemler için değiştirilemez audit log
- **Çoklu Ortam Desteği**: Kullanıcının/işletmenin tercihine göre veri erişim katmanı (EF Core / NHibernate), loglama sağlayıcısı (Serilog / NLog) ve mesaj kuyruğu (RabbitMQ / Kafka) seçilebilir

## 3. Hedef Kitle / Paydaşlar

- İşletme sahipleri / üye işyerleri
- Finans/Muhasebe ekipleri (mutabakat, raporlama)
- Operasyon ekibi (işlem takibi, iade/iptal onayları)
- Geliştirici ekip (entegrasyon, bakım)
- Denetim/uyumluluk ekibi (PCI-DSS, KVKK)

## 4. Teknoloji Yığını (Özet)

| Katman | Teknoloji | Not |
|---|---|---|
| Backend | .NET 8 (LTS) | ASP.NET Core Web API |
| ORM | EF Core + NHibernate | Provider pattern ile ikisi de desteklenir |
| Veritabanı | PostgreSQL | Ana ilişkisel veritabanı |
| Validasyon | FluentValidation | DTO/Command validasyonları |
| Loglama | Serilog + NLog | Provider pattern, appsettings üzerinden seçim |
| Cache | Redis | Dağıtık cache, session, rate-limit |
| Mesaj Kuyruğu | RabbitMQ + Kafka | Provider pattern, appsettings üzerinden seçim |
| Frontend | React (TypeScript) | SPA, Vite tabanlı |
| Kimlik Doğrulama | JWT + Refresh Token | ASP.NET Identity tabanlı |
| Konteynerleştirme | Docker / Docker Compose | Yerel geliştirme ve dağıtım |
| CI/CD | GitHub Actions | Build, test, publish, deploy |

Detaylar için bkz. [02-teknoloji-yigini.md](./02-teknoloji-yigini.md)

## 5. Doküman Haritası

| # | Doküman | İçerik |
|---|---|---|
| 00 | Proje Genel Bakış | Bu doküman |
| 01 | Mimari | Clean Architecture, katmanlar, tasarım prensipleri |
| 02 | Teknoloji Yığını | Kütüphane/versiyon detayları |
| 03 | Veritabanı Tasarımı | Şema, entity'ler, EF Core & NHibernate mapping |
| 04 | Validasyon | FluentValidation kuralları ve yapı |
| 05 | Loglama | Serilog & NLog yapılandırması, provider seçimi |
| 06 | Cache (Redis) | Cache stratejisi, key tasarımı |
| 07 | Mesaj Kuyruğu | RabbitMQ & Kafka, event tasarımı, provider seçimi |
| 08 | API Tasarımı | Endpoint listesi, sözleşmeler (contracts) |
| 09 | Frontend (React) | UI mimarisi, sayfa/komponent yapısı |
| 10 | Klasör/Solution Yapısı | Proje dizin yapısı |
| 11 | Güvenlik | PCI-DSS, KVKK, şifreleme, secrets yönetimi |
| 12 | Test Stratejisi | Unit, integration, e2e testler |
| 13 | DevOps & Dağıtım | Docker, CI/CD, ortamlar |
| 14 | Konfigürasyon Yönetimi | appsettings yapısı, provider switch mekanizması |

## 6. Kapsam Dışı (İlk Faz İçin)

- Gerçek banka canlı entegrasyonu (ilk fazda mock/sandbox banka adaptörü kullanılacak)
- Mobil native uygulama (ilk fazda sadece responsive web/React)
- Çoklu para birimi tam desteği (ilk fazda TRY öncelikli, altyapı çoklu para birimine hazır bırakılır)

## 7. Sözlük

| Terim | Açıklama |
|---|---|
| Sanal POS | Fiziksel POS cihazı olmadan internet üzerinden kart tahsilatı yapılmasını sağlayan sistem |
| Provizyon (Pre-Auth) | Kart üzerinde tutarın bloke edilmesi, henüz tahsil edilmemesi |
| Mutabakat (Reconciliation) | Banka ile sistem kayıtlarının karşılaştırılması süreci |
| Merchant | Üye işyeri |
| Provider Pattern | Aynı arayüzü (interface) uygulayan birden fazla implementasyon arasında konfigürasyonla geçiş yapabilme deseni |

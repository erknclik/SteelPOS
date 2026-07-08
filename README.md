# SanalPOS

İşletmelerin sanal POS işlemlerini (satış, iade, iptal, ön provizyon, taksit, komisyon) yönetebileceği; ORM, loglama ve mesaj kuyruğu altyapılarının **konfigürasyonla değiştirilebildiği** (provider pattern) ödeme altyapısı platformu.

## Ayırt Edici Özellik: Provider Pattern

| Alan | Seçenekler | Konfigürasyon Anahtarı | Varsayılan |
|---|---|---|---|
| ORM | EF Core, NHibernate | `Persistence:Provider` | `EfCore` |
| Loglama | Serilog, NLog | `Logging:Provider` | `Serilog` |
| Mesaj Kuyruğu | RabbitMQ, Kafka | `Messaging:Provider` | `RabbitMq` |

Application katmanı hangi somut teknolojinin çalıştığını bilmez; seçim `appsettings.json` veya ortam değişkenleri (`Persistence__Provider=NHibernate`) üzerinden yapılır.

## Depo Yapısı

```
docs/                    # Teknik dokümantasyon seti (00-14)
src/Backend/             # .NET 8 solution (Clean Architecture + CQRS)
src/Frontend/sanalpos-web/  # React 18 + TypeScript + Vite SPA
tests/                   # Unit / Integration / Architecture testleri
infra/                   # docker-compose, Dockerfile'lar, DB init scriptleri
.github/workflows/       # CI/CD pipeline'ları
```

## Hızlı Başlangıç (Docker Compose)

```bash
cd infra
docker compose up -d          # PostgreSQL, Redis, RabbitMQ, Kafka, Seq, API, Web
```

- API: http://localhost:5000/swagger
- Web: http://localhost:3000
- Seq (log): http://localhost:5341
- RabbitMQ Management: http://localhost:15672 (guest/guest)

Development ortamında açılışta şema oluşturulur ve demo veri (admin kullanıcısı `admin` / `Admin123!*`, demo merchant + terminal) seed edilir.

## Yerel Geliştirme (Docker'sız)

```bash
# Backend
cd src/Backend
dotnet build SanalPOS.sln
dotnet run --project SanalPOS.API        # http://localhost:5000

# Frontend
cd src/Frontend/sanalpos-web
npm install
npm run dev                              # http://localhost:5173
```

PostgreSQL/Redis/RabbitMQ bağlantı bilgileri `src/Backend/SanalPOS.API/appsettings.Development.json` içindedir.

## Testler

```bash
cd src/Backend
dotnet test SanalPOS.sln                 # Unit + Architecture + API smoke testleri

cd src/Frontend/sanalpos-web
npm run test -- --run
```

## Dokümantasyon

Tasarım kararlarının tamamı [docs/](./docs/README.md) altındadır; okuma sırası ve içerik haritası için [docs/README.md](./docs/README.md) dosyasına bakın.

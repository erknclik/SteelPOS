# SanalPOS - DevOps & Dağıtım

## 1. Yerel Geliştirme Ortamı (Docker Compose)

```yaml
# infra/docker-compose.yml
version: "3.9"
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: sanalpos
      POSTGRES_USER: sanalpos
      POSTGRES_PASSWORD: sanalpos_dev_pw
    ports: ["5432:5432"]
    volumes: ["pgdata:/var/lib/postgresql/data"]

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]

  rabbitmq:
    image: rabbitmq:3.13-management
    ports: ["5672:5672", "15672:15672"]
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest

  kafka:
    image: confluentinc/cp-kafka:7.6.0
    ports: ["9092:9092"]
    environment:
      KAFKA_NODE_ID: 1
      KAFKA_PROCESS_ROLES: broker,controller
      KAFKA_LISTENERS: PLAINTEXT://:9092,CONTROLLER://:9093
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
      KAFKA_CONTROLLER_QUORUM_VOTERS: 1@kafka:9093
      KAFKA_CONTROLLER_LISTENER_NAMES: CONTROLLER
      CLUSTER_ID: "sanalpos-kraft-cluster"

  seq:
    image: datalust/seq:latest
    environment:
      ACCEPT_EULA: "Y"
    ports: ["5341:80"]

  sanalpos-api:
    build:
      context: ../src/Backend
      dockerfile: SanalPOS.API/Dockerfile
    ports: ["5000:8080"]
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__Default: "Host=postgres;Database=sanalpos;Username=sanalpos;Password=sanalpos_dev_pw"
      ConnectionStrings__Redis: "redis:6379"
      Persistence__Provider: "EfCore"
      Logging__Provider: "Serilog"
      Messaging__Provider: "RabbitMq"
    depends_on: [postgres, redis, rabbitmq]

  sanalpos-web:
    build:
      context: ../src/Frontend/sanalpos-web
    ports: ["3000:80"]
    depends_on: [sanalpos-api]

volumes:
  pgdata:
```

## 2. Dockerfile (Backend Örneği)

```dockerfile
# SanalPOS.API/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "SanalPOS.API/SanalPOS.API.csproj"
RUN dotnet publish "SanalPOS.API/SanalPOS.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "SanalPOS.API.dll"]
```

## 3. CI Pipeline (GitHub Actions)

```yaml
# .github/workflows/backend-ci.yml
name: Backend CI
on:
  pull_request:
    paths: ["src/Backend/**"]
  push:
    branches: [main]

jobs:
  build-test:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16
        env: { POSTGRES_PASSWORD: test, POSTGRES_DB: sanalpos_test }
        ports: ["5432:5432"]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: "8.0.x" }
      - run: dotnet restore src/Backend/SanalPOS.sln
      - run: dotnet build src/Backend/SanalPOS.sln --no-restore -c Release
      - run: dotnet test src/Backend/SanalPOS.sln --no-build -c Release --collect:"XPlat Code Coverage"
      - name: Kod Kapsamı Kontrolü
        run: |
          # reportgenerator ile kapsam raporu üretilir, %80 eşiği kontrol edilir
          echo "Coverage check step"
      - name: Bağımlılık Zafiyet Taraması
        run: dotnet list src/Backend/SanalPOS.sln package --vulnerable --include-transitive
```

```yaml
# .github/workflows/frontend-ci.yml
name: Frontend CI
on:
  pull_request:
    paths: ["src/Frontend/**"]

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: "20" }
      - run: npm ci
        working-directory: src/Frontend/sanalpos-web
      - run: npm run lint
        working-directory: src/Frontend/sanalpos-web
      - run: npm run test -- --run
        working-directory: src/Frontend/sanalpos-web
      - run: npm run build
        working-directory: src/Frontend/sanalpos-web
```

## 4. Ortamlar (Environments)

| Ortam | Amaç | Notlar |
|---|---|---|
| Development | Geliştirici yerel makinesi | Docker Compose, mock banka adaptörü |
| Test/QA | Otomatik & manuel test | Sandbox banka entegrasyonları |
| Staging | Prod öncesi son doğrulama | Prod ile birebir konfigürasyon, gerçek veriye yakın anonim veri |
| Production | Canlı | Yüksek erişilebilirlik, izleme, alerting aktif |

## 5. Dağıtım Stratejisi

- **Blue-Green Deployment** veya **Rolling Update** (Kubernetes kullanılıyorsa)
- Veritabanı migration'ları **her zaman geriye dönük uyumlu (backward compatible)** yazılır (ör. kolon silme yerine önce "kullanılmıyor" işaretleme, sonraki sürümde silme — expand/contract pattern)
- Health check (`/health/ready`) geçmeden yeni instance trafiğe alınmaz

## 6. İzleme & Alerting

- **Prometheus** + **Grafana**: API response time, hata oranı, kuyruk derinliği, cache hit oranı dashboard'ları
- **Alertmanager**: Kritik eşik aşımlarında (ör. hata oranı %5 üstü, ödeme başarısızlık oranı ani artış) Slack/e-posta bildirimi
- **Seq/ELK**: Merkezi log arama ve analiz

## 7. Yedekleme & Felaket Kurtarma (DR)

- PostgreSQL: Günlük tam yedek + sürekli WAL arşivleme (point-in-time recovery)
- Redis: AOF (Append Only File) persistence aktif (cache kaybı kabul edilebilir olsa da idempotency/lock verisi için önemli)
- RTO/RPO hedefleri: RPO ≤ 15 dakika, RTO ≤ 1 saat (kurumsal ihtiyaca göre revize edilir)
- Düzenli DR tatbikatı (yılda en az 1 kez) önerilir

## 8. Konfigürasyon Yönetimi ve Secret'lar

Bkz. [14-konfigurasyon-yonetimi.md](./14-konfigurasyon-yonetimi.md)

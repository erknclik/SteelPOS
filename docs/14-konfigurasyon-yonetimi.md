# SanalPOS - Konfigürasyon Yönetimi

## 1. Amaç

Bu doküman, projenin en ayırt edici gereksinimini detaylandırır: **ORM (EF Core/NHibernate), Log sağlayıcısı (Serilog/NLog) ve Mesaj kuyruğu (RabbitMQ/Kafka) seçimlerinin kullanıcı/işletme tercihine göre konfigürasyon üzerinden değiştirilebilmesi.**

## 2. appsettings.json - Tam Örnek

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=sanalpos;Username=sanalpos;Password=***",
    "Redis": "localhost:6379,abortConnect=false"
  },
  "Persistence": {
    "Provider": "EfCore",
    "EnableSensitiveDataLogging": false
  },
  "Logging": {
    "Provider": "Serilog",
    "MinimumLevel": "Information",
    "Serilog": {
      "SeqUrl": "http://localhost:5341"
    },
    "NLog": {
      "ConfigFile": "nlog.config"
    }
  },
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
  },
  "Cache": {
    "DefaultExpiryMinutes": 15
  },
  "Jwt": {
    "Issuer": "SanalPOS",
    "Audience": "SanalPOS.Clients",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7,
    "SigningKeySecretName": "Jwt:SigningKey"
  },
  "RateLimiting": {
    "PermitLimit": 100,
    "WindowSeconds": 60
  },
  "AllowedHosts": "*"
}
```

## 3. Program.cs - Merkezi Kayıt Akışı (Composition Root)

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1) Loglama (en başta yapılandırılır, diğer her şey loglanabilsin)
ConfigureLogging(builder);

// 2) Application katmanı (MediatR, FluentValidation, AutoMapper)
builder.Services.AddApplicationServices();

// 3) Persistence (provider'a göre EF Core veya NHibernate)
builder.Services.AddPersistence(builder.Configuration);

// 4) Cache (Redis)
builder.Services.AddRedisCache(builder.Configuration);

// 5) Messaging (provider'a göre RabbitMQ veya Kafka)
builder.Services.AddMessaging(builder.Configuration);

// 6) Auth, Swagger, Versioning, Rate Limiting, Health Checks vb.
builder.Services.AddAuthenticationAndAuthorization(builder.Configuration);
builder.Services.AddApiInfrastructure(builder.Configuration);

var app = builder.Build();
// ... middleware pipeline
app.Run();

// --- Yardımcı metotlar (extension olarak da yazılabilir) ---
void ConfigureLogging(WebApplicationBuilder b)
{
    var provider = b.Configuration["Logging:Provider"] ?? "Serilog";
    if (provider.Equals("NLog", StringComparison.OrdinalIgnoreCase))
    {
        b.Logging.ClearProviders();
        b.Host.UseNLog();
    }
    else
    {
        b.Host.UseSerilog((ctx, services, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());
    }
}
```

## 4. Extension Metotları (Her Katmanda `DependencyInjection.cs`)

```csharp
// SanalPOS.Infrastructure/DependencyInjection.cs
public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Persistence:Provider"] ?? "EfCore";

        return provider switch
        {
            "NHibernate" => services.AddNHibernatePersistence(configuration),
            "EfCore" => services.AddEfCorePersistence(configuration),
            _ => throw new InvalidOperationException(
                $"Desteklenmeyen Persistence:Provider değeri: '{provider}'. " +
                $"Geçerli değerler: 'EfCore', 'NHibernate'.")
        };
    }

    public static IServiceCollection AddMessaging(
        this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Messaging:Provider"] ?? "RabbitMq";

        return provider switch
        {
            "Kafka" => services.AddKafkaMessaging(configuration),
            "RabbitMq" => services.AddRabbitMqMessaging(configuration),
            _ => throw new InvalidOperationException(
                $"Desteklenmeyen Messaging:Provider değeri: '{provider}'. " +
                $"Geçerli değerler: 'RabbitMq', 'Kafka'.")
        };
    }
}
```

## 5. Ortam Bazlı Konfigürasyon Dosyaları

| Dosya | Amaç |
|---|---|
| `appsettings.json` | Ortak/varsayılan ayarlar |
| `appsettings.Development.json` | Yerel geliştirme (mock banka, verbose log) |
| `appsettings.Staging.json` | Staging ortamı |
| `appsettings.Production.json` | Prod ortamı (Information seviyesi log, gerçek/sandbox banka) |
| Environment Variables | Secret'lar ve ortam bazlı override (`Persistence__Provider=NHibernate` gibi `__` ile nested key) |

## 6. Secret Yönetimi

- **Geliştirme**: `dotnet user-secrets set "ConnectionStrings:Default" "..."`
- **Prod**: Azure Key Vault / HashiCorp Vault entegrasyonu, `builder.Configuration.AddAzureKeyVault(...)`
- Secret'lar **asla** appsettings.json içine yazılmaz veya git'e commit edilmez; `.gitignore` içinde `appsettings.*.local.json` gibi dosyalar hariç tutulur

## 7. Konfigürasyon Doğrulama (Fail-Fast)

Uygulama açılışında geçersiz bir `Provider` değeri girildiğinde (ör. `Persistence:Provider = "MongoDb"`) uygulama **hemen** (`InvalidOperationException` ile) başlangıçta hata vermeli, sessizce varsayılana düşmemelidir. Bu, yanlış konfigürasyonun prod ortamında fark edilmeden çalışmasını engeller.

Ek olarak, `IValidateOptions<T>` ile güçlü tipli options sınıfları (`PersistenceOptions`, `LoggingOptions`, `MessagingOptions`) tanımlanıp `builder.Services.AddOptionsWithValidateOnStart<...>()` kullanılması önerilir.

## 8. Konfigürasyon Seçim Özeti Tablosu

| Alan | Konfigürasyon Anahtarı | Değerler | Varsayılan |
|---|---|---|---|
| ORM | `Persistence:Provider` | `EfCore`, `NHibernate` | `EfCore` |
| Log | `Logging:Provider` | `Serilog`, `NLog` | `Serilog` |
| Mesaj Kuyruğu | `Messaging:Provider` | `RabbitMq`, `Kafka` | `RabbitMq` |

Bu tablo, sistemin "kullanıcı tercihine göre" çalışan üç ana esneklik noktasını özetler ve tüm ilgili dokümanlarda (03, 05, 07) referans gösterilir.

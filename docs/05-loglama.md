# SanalPOS - Loglama Stratejisi (Serilog & NLog)

## 1. Genel Yaklaşım

Sistem, **hem Serilog hem NLog'u destekleyecek** şekilde tasarlanır. Hangi sağlayıcının aktif olacağı `appsettings.json` içindeki `Logging:Provider` alanı ile belirlenir (`Serilog` veya `NLog`). Application katmanı, somut log kütüphanesini bilmez; sadece `Microsoft.Extensions.Logging.ILogger<T>` arayüzünü kullanır — her iki sağlayıcı da bu arayüzün arkasına provider olarak takılır.

```json
{
  "Logging": {
    "Provider": "Serilog",
    "MinimumLevel": "Information"
  }
}
```

## 2. Neden `ILogger<T>` Soyutlaması?

.NET'in yerleşik `ILogger<T>` arayüzü kullanılarak Application/Domain katmanları hiçbir logging kütüphanesine doğrudan bağımlı olmaz. Serilog ve NLog, ikisi de bu arayüzün arkasına "provider" olarak eklenir (`UseSerilog()` veya `NLog.Web.AspNetCore` `AddNLog()`). Böylece kod tabanında `Log.Information(...)` gibi statik/kütüphaneye özel çağrılar **yasaktır**; her yerde `ILogger<T>` enjekte edilir.

## 3. Program.cs - Provider Seçim Mekanizması

```csharp
var loggingProvider = builder.Configuration["Logging:Provider"] ?? "Serilog";

if (loggingProvider == "NLog")
{
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();
}
else
{
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId()
            .WriteTo.Console(new CompactJsonFormatter())
            .WriteTo.File(
                path: "logs/sanalpos-.log",
                rollingInterval: RollingInterval.Day,
                formatter: new CompactJsonFormatter())
            .WriteTo.Seq(context.Configuration["Serilog:SeqUrl"] ?? "http://localhost:5341");
    });
}
```

## 4. Serilog Konfigürasyonu (appsettings.json)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    },
    "SeqUrl": "http://localhost:5341",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/sanalpos-.log", "rollingInterval": "Day" } }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

Sink seçenekleri: Console, File, Seq, Elasticsearch (`Serilog.Sinks.Elasticsearch`), opsiyonel olarak `Serilog.Sinks.RabbitMQ` ile kritik hata loglarının ayrıca kuyruğa da yazılması.

## 5. NLog Konfigürasyonu (nlog.config)

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      autoReload="true" throwConfigExceptions="true">
  <extensions>
    <add assembly="NLog.Web.AspNetCore"/>
  </extensions>
  <targets>
    <target xsi:type="File" name="allfile"
            fileName="logs/nlog-all-${shortdate}.log"
            layout="${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}|${aspnet-traceidentifier}" />
    <target xsi:type="Console" name="console"
            layout="${longdate}|${level:uppercase=true}|${logger}|${message}" />
    <target xsi:type="Seq" name="seq" serverUrl="http://localhost:5341" />
  </targets>
  <rules>
    <logger name="Microsoft.*" maxlevel="Info" final="true" />
    <logger name="*" minlevel="Info" writeTo="allfile,console,seq" />
  </rules>
</nlog>
```

## 6. Yapısal (Structured) Loglama Kuralları

- Log mesajları her zaman **structured/template** formatında yazılır (string interpolation yasak):
  ```csharp
  // Doğru
  _logger.LogInformation("Ödeme oluşturuldu. TransactionId: {TransactionId}, Amount: {Amount}", tx.Id, tx.Amount);

  // Yanlış
  _logger.LogInformation($"Ödeme oluşturuldu. TransactionId: {tx.Id}, Amount: {tx.Amount}");
  ```
- **Hassas veri asla loglanmaz**: Tam kart numarası, CVV, şifre, token değerleri log'a yazılmaz. Sadece maskelenmiş kart numarası (`4021 22** **** 1234`) loglanabilir.
- Her log kaydında `CorrelationId`, `MerchantId`, `UserId` (varsa) enrichment ile otomatik eklenir.

## 7. Log Seviyeleri Kullanım Rehberi

| Seviye | Kullanım Senaryosu |
|---|---|
| Trace | Çok detaylı debug bilgisi (sadece geliştirme ortamında) |
| Debug | Geliştirme sırasında adım adım takip |
| Information | Normal iş akışı olayları (ödeme oluşturuldu, kullanıcı giriş yaptı) |
| Warning | Beklenmeyen ama sistemi durdurmayan durumlar (retry denemesi, cache miss) |
| Error | İşlem başarısız oldu, exception yakalandı |
| Critical/Fatal | Sistem geneli çalışamaz durum (DB bağlantısı tamamen koptu) |

## 8. Middleware ile Request/Response Loglama

`RequestLoggingMiddleware`, her HTTP isteği için giriş/çıkış, süre (elapsed ms), status code, correlation id bilgisini loglar. Serilog kullanılıyorsa `UseSerilogRequestLogging()` middleware'i tercih edilir; NLog kullanılıyorsa özel bir middleware yazılır.

## 9. Merkezi Log Toplama (Observability)

- Geliştirme/test ortamı: **Seq** (hafif, hızlı kurulum)
- Prod ortamı önerisi: **Elasticsearch + Kibana (ELK)** veya **Grafana Loki**
- Her iki log sağlayıcısı da (Serilog/NLog) aynı merkezi hedefe (Seq/ELK) yazabilecek şekilde yapılandırılır; böylece sağlayıcı değişse bile izlenebilirlik kesintiye uğramaz.

## 10. Test Ortamında Loglama

Unit testlerde `NullLogger<T>` veya `ILogger<T>` mock'u (`Moq`/`NSubstitute`) kullanılır; gerçek Serilog/NLog sink'leri test sürecine dahil edilmez.

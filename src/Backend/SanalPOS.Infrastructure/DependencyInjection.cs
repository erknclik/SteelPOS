using MassTransit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Infrastructure.BankAdapters;
using SanalPOS.Infrastructure.Consumers;
using SanalPOS.Infrastructure.EfCore;
using SanalPOS.Infrastructure.Iso8583;
using SanalPOS.Infrastructure.Messaging.Kafka;
using SanalPOS.Infrastructure.Messaging.RabbitMq;
using SanalPOS.Infrastructure.NHibernate;
using SanalPOS.Infrastructure.Security;
using SanalPOS.Infrastructure.Services;

namespace SanalPOS.Infrastructure;

/// <summary>
/// Provider seçim switch mantığı burada toplanır (bkz. docs/14-konfigurasyon-yonetimi.md).
/// Geçersiz provider değeri açılışta InvalidOperationException fırlatır (fail-fast);
/// uygulama sessizce varsayılana düşmez.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient("webhooks", client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddDataProtection();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        // Banka adaptörleri: factory, kayıtlı tüm adaptörleri ProviderCode'a göre çözer.
        // ISO 8583 konuşan bankalar konfigürasyondan gelir (Iso8583:Banks); mock ilk faz içindir.
        services.AddSingleton<IBankProviderAdapter, MockBankAdapter>();

        // STAN üreteci: tek instance'da InMemory yeterli; çok instance'lı üretimde Redis
        // (banka başına merkezî INCR sayacı) seçilmelidir. Geçersiz değer açılışta patlar.
        var stanProvider = configuration["Iso8583:StanSequence"] ?? "InMemory";
        switch (stanProvider)
        {
            case "Redis":
                services.AddSingleton<Iso8583.Network.IStanSequenceFactory, RedisStanSequenceFactory>();
                break;
            case "InMemory":
                break; // AddIso8583BankAdapters içindeki TryAdd varsayılanı kullanılır.
            default:
                throw new InvalidOperationException(
                    $"Desteklenmeyen Iso8583:StanSequence değeri: '{stanProvider}'. Geçerli değerler: 'InMemory', 'Redis'.");
        }

        services.AddIso8583BankAdapters(configuration);
        services.AddSingleton<IBankAdapterFactory, BankAdapterFactory>();

        // 3D Secure MPI: provider switch (diğer provider'larla aynı fail-fast yaklaşım).
        var threeDsProvider = configuration["ThreeDSecure:Provider"] ?? ThreeDSecure.SimulatedThreeDSecureProvider.ProviderName;
        switch (threeDsProvider)
        {
            case ThreeDSecure.SimulatedThreeDSecureProvider.ProviderName:
                services.AddSingleton(new ThreeDSecure.SimulatedThreeDSecureOptions
                {
                    AcsUrl = configuration["ThreeDSecure:Simulated:AcsUrl"] ?? "/api/v1/acs-simulator"
                });
                services.AddSingleton<IThreeDSecureProvider, ThreeDSecure.SimulatedThreeDSecureProvider>();
                break;
            default:
                throw new InvalidOperationException(
                    $"Desteklenmeyen ThreeDSecure:Provider değeri: '{threeDsProvider}'. Geçerli değerler: 'Simulated'.");
        }

        services.AddScoped<Webhooks.IWebhookDispatcher, Webhooks.WebhookDispatcher>();

        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Persistence:Provider"] ?? "EfCore";

        return provider switch
        {
            "NHibernate" => services.AddNHibernatePersistence(configuration),
            "EfCore" => services.AddEfCorePersistence(configuration),
            _ => throw new InvalidOperationException(
                $"Desteklenmeyen Persistence:Provider değeri: '{provider}'. Geçerli değerler: 'EfCore', 'NHibernate'.")
        };
    }

    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Messaging:Provider"] ?? "RabbitMq";

        return provider switch
        {
            "Kafka" => services.AddKafkaMessaging(configuration, typeof(PaymentCompletedConsumer)),
            "RabbitMq" => services.AddRabbitMqMessaging(configuration, typeof(PaymentCompletedConsumer)),
            // Broker gerektirmeyen in-memory transport: integration test ve yerel demo amaçlıdır.
            "InMemory" => services.AddInMemoryMessaging(),
            _ => throw new InvalidOperationException(
                $"Desteklenmeyen Messaging:Provider değeri: '{provider}'. Geçerli değerler: 'RabbitMq', 'Kafka', 'InMemory'.")
        };
    }

    private static IServiceCollection AddInMemoryMessaging(this IServiceCollection services)
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumers(typeof(PaymentCompletedConsumer).Assembly);
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });
        services.AddScoped<IEventPublisher, InMemoryEventPublisher>();
        return services;
    }
}

internal sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly MassTransit.IPublishEndpoint _publishEndpoint;

    public InMemoryEventPublisher(MassTransit.IPublishEndpoint publishEndpoint) => _publishEndpoint = publishEndpoint;

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class =>
        _publishEndpoint.Publish(@event, ct);
}

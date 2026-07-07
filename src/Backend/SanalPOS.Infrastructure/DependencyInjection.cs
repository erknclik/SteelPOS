using MassTransit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Infrastructure.BankAdapters;
using SanalPOS.Infrastructure.Consumers;
using SanalPOS.Infrastructure.EfCore;
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

        // Banka adaptörleri: yeni banka eklendiğinde buraya kaydedilir; factory koda göre çözer.
        services.AddSingleton<IBankProviderAdapter, MockBankAdapter>();
        services.AddSingleton<IBankAdapterFactory, BankAdapterFactory>();

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

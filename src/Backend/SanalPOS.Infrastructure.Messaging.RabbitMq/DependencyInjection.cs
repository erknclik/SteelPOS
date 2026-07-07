using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SanalPOS.Application.Common.Interfaces;

namespace SanalPOS.Infrastructure.Messaging.RabbitMq;

public static class DependencyInjection
{
    /// <summary>
    /// MassTransit'i RabbitMQ transport'u ile kaydeder. Consumer'lar verilen assembly'lerden taranır
    /// (SanalPOS.Infrastructure içindeki ortak consumer sınıfları her iki transport'ta da kullanılır).
    /// </summary>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services, IConfiguration configuration, params Type[] consumerAssemblyMarkers)
    {
        services.AddMassTransit(x =>
        {
            foreach (var marker in consumerAssemblyMarkers)
                x.AddConsumers(marker.Assembly);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(
                    configuration["Messaging:RabbitMq:Host"] ?? "localhost",
                    configuration["Messaging:RabbitMq:VirtualHost"] ?? "/",
                    h =>
                    {
                        h.Username(configuration["Messaging:RabbitMq:Username"] ?? "guest");
                        h.Password(configuration["Messaging:RabbitMq:Password"] ?? "guest");
                    });

                // At-least-once teslim + üstel retry; başarısız mesajlar _error kuyruğuna düşer (DLQ).
                cfg.UseMessageRetry(r => r.Exponential(
                    5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();
        return services;
    }
}

/// <summary>IPublishEndpoint (MassTransit) üzerinden yayınlar; Application katmanı transport'u bilmez.</summary>
public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public RabbitMqEventPublisher(IPublishEndpoint publishEndpoint) => _publishEndpoint = publishEndpoint;

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class =>
        _publishEndpoint.Publish(@event, ct);
}

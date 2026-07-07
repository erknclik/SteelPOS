using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Contracts;

namespace SanalPOS.Infrastructure.Messaging.Kafka;

public static class DependencyInjection
{
    /// <summary>
    /// MassTransit'i Kafka rider'ı ile kaydeder. Kafka rider bir bus transport'u gerektirdiği için
    /// in-memory bus + Kafka rider kombinasyonu kullanılır; event'ler topic'lere produce edilir.
    /// </summary>
    public static IServiceCollection AddKafkaMessaging(
        this IServiceCollection services, IConfiguration configuration, params Type[] consumerAssemblyMarkers)
    {
        var bootstrapServers = configuration["Messaging:Kafka:BootstrapServers"] ?? "localhost:9092";
        var consumerGroupId = configuration["Messaging:Kafka:ConsumerGroupId"] ?? "sanalpos-consumers";

        services.AddMassTransit(x =>
        {
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));

            x.AddRider(rider =>
            {
                rider.AddProducer<PaymentCompletedEvent>("payment-completed");
                rider.AddProducer<PaymentFailedEvent>("payment-failed");
                rider.AddProducer<RefundCompletedEvent>("refund-completed");
                rider.AddProducer<MerchantSuspendedEvent>("merchant-suspended");
                rider.AddProducer<DailyReconciliationRequestedEvent>("daily-reconciliation-requested");
                rider.AddProducer<WebhookTestRequestedEvent>("webhook-test-requested");

                foreach (var marker in consumerAssemblyMarkers)
                    rider.AddConsumers(marker.Assembly);

                rider.UsingKafka((context, k) =>
                {
                    k.Host(bootstrapServers);

                    AddTopicEndpoint<PaymentCompletedEvent>(k, context, "payment-completed", consumerGroupId);
                    AddTopicEndpoint<PaymentFailedEvent>(k, context, "payment-failed", consumerGroupId);
                    AddTopicEndpoint<RefundCompletedEvent>(k, context, "refund-completed", consumerGroupId);
                    AddTopicEndpoint<MerchantSuspendedEvent>(k, context, "merchant-suspended", consumerGroupId);
                    AddTopicEndpoint<WebhookTestRequestedEvent>(k, context, "webhook-test-requested", consumerGroupId);
                });
            });
        });

        services.AddScoped<IEventPublisher, KafkaEventPublisher>();
        return services;
    }

    private static void AddTopicEndpoint<TEvent>(
        IKafkaFactoryConfigurator k, IRiderRegistrationContext context, string topic, string groupId)
        where TEvent : class
    {
        k.TopicEndpoint<TEvent>(topic, groupId, e =>
        {
            e.CreateIfMissing(t => t.NumPartitions = 1);
            e.ConfigureConsumers(context);
        });
    }
}

/// <summary>Event tipine karşılık gelen ITopicProducer üzerinden ilgili topic'e yayınlar.</summary>
public class KafkaEventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public KafkaEventPublisher(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        var producer = _serviceProvider.GetRequiredService<ITopicProducer<TEvent>>();
        return producer.Produce(@event, ct);
    }
}

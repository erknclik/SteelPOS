namespace SanalPOS.Application.Common.Interfaces;

/// <summary>
/// Mesaj kuyruğuna event yayınlama soyutlaması. RabbitMQ ve Kafka implementasyonları
/// kendi Infrastructure.Messaging.* projelerindedir; Application transport'u bilmez.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;
}

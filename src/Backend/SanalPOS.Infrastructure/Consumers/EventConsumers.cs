using MassTransit;
using Microsoft.Extensions.Logging;
using SanalPOS.Contracts;
using SanalPOS.Infrastructure.Webhooks;

namespace SanalPOS.Infrastructure.Consumers;

// Transport bağımsız MassTransit consumer'ları: RabbitMQ endpoint'i veya Kafka topic'i
// üzerinden aynı sınıflar tüketilir. Consumer'lar idempotent yazılır (at-least-once teslim).

public class PaymentCompletedConsumer : IConsumer<PaymentCompletedEvent>
{
    private readonly IWebhookDispatcher _webhookDispatcher;
    private readonly ILogger<PaymentCompletedConsumer> _logger;

    public PaymentCompletedConsumer(IWebhookDispatcher webhookDispatcher, ILogger<PaymentCompletedConsumer> logger)
    {
        _webhookDispatcher = webhookDispatcher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var e = context.Message;
        _logger.LogInformation(
            "PaymentCompletedEvent tüketildi. TransactionId: {TransactionId}, CorrelationId: {CorrelationId}",
            e.TransactionId, e.CorrelationId);

        // Bildirim (e-posta/SMS) entegrasyonu ileri fazda; şimdilik webhook dispatch edilir.
        await _webhookDispatcher.DispatchAsync(e.MerchantId, "PaymentCompleted", e, context.CancellationToken);
    }
}

public class PaymentFailedConsumer : IConsumer<PaymentFailedEvent>
{
    private readonly IWebhookDispatcher _webhookDispatcher;
    private readonly ILogger<PaymentFailedConsumer> _logger;

    public PaymentFailedConsumer(IWebhookDispatcher webhookDispatcher, ILogger<PaymentFailedConsumer> logger)
    {
        _webhookDispatcher = webhookDispatcher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        var e = context.Message;
        _logger.LogWarning(
            "PaymentFailedEvent tüketildi. TransactionId: {TransactionId}, ReasonCode: {ReasonCode}",
            e.TransactionId, e.ReasonCode);

        await _webhookDispatcher.DispatchAsync(e.MerchantId, "PaymentFailed", e, context.CancellationToken);
    }
}

public class RefundCompletedConsumer : IConsumer<RefundCompletedEvent>
{
    private readonly IWebhookDispatcher _webhookDispatcher;
    private readonly ILogger<RefundCompletedConsumer> _logger;

    public RefundCompletedConsumer(IWebhookDispatcher webhookDispatcher, ILogger<RefundCompletedConsumer> logger)
    {
        _webhookDispatcher = webhookDispatcher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RefundCompletedEvent> context)
    {
        var e = context.Message;
        _logger.LogInformation(
            "RefundCompletedEvent tüketildi. RefundTransactionId: {RefundTransactionId}", e.RefundTransactionId);

        // Merchant id orijinal işlemden değil event'ten gelmediği için webhook'u
        // refund payload'ı ile orijinal işlem sahibine göndermek üzere ileri fazda zenginleştirilebilir.
        await Task.CompletedTask;
    }
}

/// <summary>
/// Zamanlanmış mutabakat tetiklemesini (BackgroundJobs -> DailyReconciliationRequestedEvent)
/// tüketir ve mutabakat komutunu çalıştırır. Idempotent: aynı gün ikinci kez koşarsa
/// aynı toplamlar tekrar gönderilir (banka tarafı batch'i günle eşleştirir).
/// </summary>
public class DailyReconciliationRequestedConsumer : IConsumer<DailyReconciliationRequestedEvent>
{
    private readonly MediatR.ISender _sender;
    private readonly ILogger<DailyReconciliationRequestedConsumer> _logger;

    public DailyReconciliationRequestedConsumer(MediatR.ISender sender, ILogger<DailyReconciliationRequestedConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DailyReconciliationRequestedEvent> context)
    {
        var e = context.Message;
        _logger.LogInformation("Mutabakat tetiklendi. Gün: {Day}, CorrelationId: {CorrelationId}", e.Day, e.CorrelationId);

        var results = await _sender.Send(
            new Application.Reconciliation.RunReconciliationCommand(e.Day), context.CancellationToken);

        foreach (var result in results.Where(r => !r.IsBalanced))
        {
            _logger.LogWarning(
                "Mutabakat farkı: Provider {Provider} ({Currency}), Gün {Day}, Kod {Code} - {Message}",
                result.ProviderCode, result.Currency, result.Day, result.ReasonCode, result.ReasonMessage);
        }
    }
}

public class WebhookTestRequestedConsumer : IConsumer<WebhookTestRequestedEvent>
{
    private readonly IWebhookDispatcher _webhookDispatcher;

    public WebhookTestRequestedConsumer(IWebhookDispatcher webhookDispatcher) =>
        _webhookDispatcher = webhookDispatcher;

    public Task Consume(ConsumeContext<WebhookTestRequestedEvent> context)
    {
        var e = context.Message;
        return _webhookDispatcher.DispatchAsync(
            e.MerchantId, "Test", new { e.SubscriptionId, message = "SanalPOS webhook test event" },
            context.CancellationToken);
    }
}

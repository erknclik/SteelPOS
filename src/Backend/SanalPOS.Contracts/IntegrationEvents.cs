namespace SanalPOS.Contracts;

// Mesaj kuyruğu (RabbitMQ/Kafka) üzerinden yayınlanan event sözleşmeleri.
// Hem publisher hem consumer bu projeyi referans alır; breaking change yapılamaz
// (bkz. docs/07-mesaj-kuyrugu.md §4 ve docs/12-test-stratejisi.md §5).

public record PaymentCompletedEvent(
    Guid TransactionId,
    Guid MerchantId,
    decimal Amount,
    string Currency,
    DateTime CompletedAtUtc,
    string CorrelationId);

public record PaymentFailedEvent(
    Guid TransactionId,
    Guid MerchantId,
    string ReasonCode,
    string ReasonMessage,
    DateTime FailedAtUtc,
    string CorrelationId);

public record RefundCompletedEvent(
    Guid RefundTransactionId,
    Guid OriginalTransactionId,
    Guid MerchantId,
    decimal RefundAmount,
    string Currency,
    DateTime CompletedAtUtc,
    string CorrelationId);

public record MerchantSuspendedEvent(
    Guid MerchantId,
    DateTime SuspendedAtUtc,
    string CorrelationId);

public record DailyReconciliationRequestedEvent(
    DateOnly Day,
    DateTime RequestedAtUtc,
    string CorrelationId);

public record WebhookTestRequestedEvent(
    Guid SubscriptionId,
    Guid MerchantId,
    DateTime RequestedAtUtc,
    string CorrelationId);

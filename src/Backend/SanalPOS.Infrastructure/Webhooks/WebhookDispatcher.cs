using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Infrastructure.Webhooks;

public interface IWebhookDispatcher
{
    Task DispatchAsync(Guid merchantId, string eventType, object payload, CancellationToken ct = default);
}

/// <summary>
/// Merchant'ın aktif webhook aboneliklerine HMAC-SHA256 imzalı POST atar
/// (imza X-SanalPOS-Signature header'ında taşınır; bkz. docs/08-api-tasarimi.md §6).
/// </summary>
public class WebhookDispatcher : IWebhookDispatcher
{
    public const string SignatureHeader = "X-SanalPOS-Signature";

    private readonly IWebhookSubscriptionRepository _subscriptionRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        IWebhookSubscriptionRepository subscriptionRepository,
        ISecretProtector secretProtector,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatcher> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _secretProtector = secretProtector;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task DispatchAsync(Guid merchantId, string eventType, object payload, CancellationToken ct = default)
    {
        var subscriptions = await _subscriptionRepository.GetActiveByEventTypeAsync(merchantId, eventType, ct);
        if (subscriptions.Count == 0)
            return;

        var body = JsonSerializer.Serialize(new { eventType, occurredAt = DateTime.UtcNow, data = payload });
        var client = _httpClientFactory.CreateClient("webhooks");

        foreach (var subscription in subscriptions)
        {
            try
            {
                var secret = _secretProtector.Unprotect(subscription.Secret);
                var signature = ComputeSignature(secret, body);

                using var request = new HttpRequestMessage(HttpMethod.Post, subscription.TargetUrl);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                request.Headers.Add(SignatureHeader, signature);

                var response = await client.SendAsync(request, ct);
                _logger.LogInformation(
                    "Webhook gönderildi. SubscriptionId: {SubscriptionId}, EventType: {EventType}, StatusCode: {StatusCode}",
                    subscription.Id, eventType, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                // Tek abonelik hatası diğer gönderimleri engellemez; retry MQ consumer retry'ı ile sağlanır.
                _logger.LogError(ex, "Webhook gönderimi başarısız. SubscriptionId: {SubscriptionId}", subscription.Id);
            }
        }
    }

    private static string ComputeSignature(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }
}

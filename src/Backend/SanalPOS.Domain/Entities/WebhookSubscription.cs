using SanalPOS.Domain.Common;
using SanalPOS.Domain.Exceptions;

namespace SanalPOS.Domain.Entities;

public class WebhookSubscription : BaseEntity, IAuditableEntity
{
    public Guid MerchantId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string TargetUrl { get; private set; } = string.Empty;

    /// <summary>HMAC imzalama secret'ı; uygulama seviyesinde şifrelenmiş olarak saklanır.</summary>
    public string Secret { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    protected WebhookSubscription()
    {
    }

    public WebhookSubscription(Guid merchantId, string eventType, string targetUrl, string secret)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new DomainException("Webhook hedef adresi geçerli bir HTTPS URL olmalıdır.");
        if (string.IsNullOrWhiteSpace(eventType))
            throw new DomainException("Event tipi boş olamaz.");
        if (string.IsNullOrWhiteSpace(secret))
            throw new DomainException("Webhook secret'ı boş olamaz.");

        MerchantId = merchantId;
        EventType = eventType.Trim();
        TargetUrl = targetUrl;
        Secret = secret;
    }

    public void Deactivate() => IsActive = false;
}

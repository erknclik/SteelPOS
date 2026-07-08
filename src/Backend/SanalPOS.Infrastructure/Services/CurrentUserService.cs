using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SanalPOS.Application.Common.Interfaces;

namespace SanalPOS.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Principal?.FindFirstValue("sub"), out var id)
            ? id
            : null;

    public string UserName =>
        Principal?.FindFirstValue(ClaimTypes.Name)
        ?? Principal?.FindFirstValue("unique_name")
        ?? "system";

    public Guid? MerchantId =>
        Guid.TryParse(Principal?.FindFirstValue("merchant_id"), out var id) ? id : null;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}

/// <summary>
/// Correlation id, CorrelationIdMiddleware tarafından HttpContext.Items'a yazılır;
/// HTTP bağlamı yoksa (background job) her scope için yeni bir id üretilir.
/// </summary>
public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Lazy<string> _fallback = new(() => Guid.NewGuid().ToString());

    public CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public string CorrelationId =>
        _httpContextAccessor.HttpContext?.Items[ItemKey] as string ?? _fallback.Value;
}

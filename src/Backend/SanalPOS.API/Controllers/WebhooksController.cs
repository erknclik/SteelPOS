using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Webhooks;
using SanalPOS.Domain.Entities;

namespace SanalPOS.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks")]
[Authorize(Roles = $"{Role.SystemAdmin},{Role.MerchantAdmin}")]
public class WebhooksController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;

    public WebhooksController(ISender sender, ICurrentUserService currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    public sealed record CreateWebhookRequest(Guid? MerchantId, string EventType, string TargetUrl, string Secret);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WebhookSubscriptionDto>>> GetList(
        [FromQuery] Guid? merchantId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetWebhookSubscriptionsQuery(ResolveMerchant(merchantId)), ct));

    [HttpPost]
    public async Task<ActionResult<WebhookSubscriptionDto>> Create(CreateWebhookRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new CreateWebhookSubscriptionCommand(
            ResolveMerchant(request.MerchantId), request.EventType, request.TargetUrl, request.Secret), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _sender.Send(new DeleteWebhookSubscriptionCommand(id), ct);
        return NoContent();
    }

    /// <summary>Aboneliğe test event'i gönderir (mesaj kuyruğu üzerinden asenkron).</summary>
    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct)
    {
        await _sender.Send(new TestWebhookCommand(id), ct);
        return Accepted();
    }

    private Guid ResolveMerchant(Guid? requested)
    {
        if (_currentUser.IsInRole(Role.SystemAdmin))
            return requested ?? throw new ForbiddenException("SystemAdmin için merchantId parametresi zorunludur.");
        return _currentUser.MerchantId ?? throw new ForbiddenException("Kullanıcı bir merchant'a bağlı değil.");
    }
}

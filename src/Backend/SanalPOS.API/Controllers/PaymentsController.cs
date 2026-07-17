using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SanalPOS.Application.Common.Models;
using SanalPOS.Application.Payments.Commands.CapturePayment;
using SanalPOS.Application.Payments.Commands.CreatePayment;
using SanalPOS.Application.Payments.Commands.RefundPayment;
using SanalPOS.Application.Payments.Commands.ThreeDSecure;
using SanalPOS.Application.Payments.Commands.VoidPayment;
using SanalPOS.Application.Payments.Dtos;
using SanalPOS.Application.Payments.Queries;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;

namespace SanalPOS.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
[Authorize(Roles = $"{Role.SystemAdmin},{Role.MerchantAdmin},{Role.Operator}")]
public class PaymentsController : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly ISender _sender;

    public PaymentsController(ISender sender) => _sender = sender;

    public sealed record CreatePaymentRequest(
        Guid MerchantId,
        Guid TerminalId,
        string OrderReference,
        decimal Amount,
        string Currency,
        short InstallmentCount,
        string CardNumber,
        string CardHolderName,
        int ExpireMonth,
        int ExpireYear,
        string Cvv);

    public sealed record RefundRequest(decimal Amount, string? Reason);

    /// <summary>Yeni satış işlemi oluşturur. Idempotency-Key header'ı zorunludur.</summary>
    [HttpPost]
    public Task<ActionResult<PaymentResultDto>> CreateSale(CreatePaymentRequest request, CancellationToken ct) =>
        CreateInternal(request, TransactionType.Sale, ct);

    /// <summary>Ön provizyon (blokaj) oluşturur. Idempotency-Key header'ı zorunludur.</summary>
    [HttpPost("pre-auth")]
    public Task<ActionResult<PaymentResultDto>> CreatePreAuth(CreatePaymentRequest request, CancellationToken ct) =>
        CreateInternal(request, TransactionType.PreAuth, ct);

    private async Task<ActionResult<PaymentResultDto>> CreateInternal(
        CreatePaymentRequest request, TransactionType type, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue(IdempotencyKeyHeader, out var idempotencyKey)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { title = $"'{IdempotencyKeyHeader}' header'ı zorunludur.", status = 400 });
        }

        var result = await _sender.Send(new CreatePaymentCommand(
            request.MerchantId, request.TerminalId, request.OrderReference,
            request.Amount, request.Currency, request.InstallmentCount,
            request.CardNumber, request.CardHolderName, request.ExpireMonth, request.ExpireYear,
            request.Cvv, idempotencyKey!, type), ct);

        return CreatedAtAction(nameof(GetById), new { id = result.TransactionId, version = "1" }, result);
    }

    public sealed record ThreeDSPaymentRequest(
        Guid MerchantId,
        Guid TerminalId,
        string OrderReference,
        decimal Amount,
        string Currency,
        short InstallmentCount,
        string CardNumber,
        string CardHolderName,
        int ExpireMonth,
        int ExpireYear,
        string Cvv,
        string CallbackUrl);

    /// <summary>
    /// 3D Secure satış akışını başlatır. Kart 3DS'e kayıtlıysa yanıtta requiresRedirect=true
    /// ve ACS yönlendirme bilgileri (acsUrl, md, paReq) döner; istemci kart hamilini ACS'e
    /// form-post ile yönlendirir. Kayıtlı değilse işlem doğrudan sonuçlanır.
    /// Idempotency-Key header'ı zorunludur.
    /// </summary>
    [HttpPost("3ds")]
    public async Task<ActionResult<ThreeDSInitiationResultDto>> Initiate3DS(ThreeDSPaymentRequest request, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue(IdempotencyKeyHeader, out var idempotencyKey)
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { title = $"'{IdempotencyKeyHeader}' header'ı zorunludur.", status = 400 });
        }

        var result = await _sender.Send(new Initiate3DSPaymentCommand(
            request.MerchantId, request.TerminalId, request.OrderReference,
            request.Amount, request.Currency, request.InstallmentCount,
            request.CardNumber, request.CardHolderName, request.ExpireMonth, request.ExpireYear,
            request.Cvv, idempotencyKey!, request.CallbackUrl), ct);

        return Ok(result);
    }

    /// <summary>
    /// ACS dönüş (callback) endpoint'i: kart hamilinin tarayıcısı ACS doğrulaması sonrası
    /// buraya form-post edilir. MD tek kullanımlık oturum belirtecidir; JWT beklenmez.
    /// </summary>
    [HttpPost("3ds/complete")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<ActionResult<PaymentResultDto>> Complete3DS(
        [FromForm(Name = "MD")] string md,
        [FromForm(Name = "PaRes")] string paRes,
        CancellationToken ct) =>
        Ok(await _sender.Send(new Complete3DSPaymentCommand(md, paRes), ct));

    /// <summary>Provizyonu kapatır (tahsilata çevirir).</summary>
    [HttpPost("{id:guid}/capture")]
    public async Task<ActionResult<PaymentResultDto>> Capture(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new CapturePaymentCommand(id), ct));

    /// <summary>Aynı gün içinde işlemi iptal eder.</summary>
    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<PaymentResultDto>> Void(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new VoidPaymentCommand(id), ct));

    /// <summary>Kısmi/tam iade oluşturur.</summary>
    [HttpPost("{id:guid}/refund")]
    [Authorize(Roles = $"{Role.SystemAdmin},{Role.MerchantAdmin}")]
    public async Task<ActionResult<RefundResultDto>> Refund(Guid id, RefundRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new RefundPaymentCommand(id, request.Amount, request.Reason), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTransactionByIdQuery(id), ct));

    /// <summary>Filtrelenebilir işlem listesi. Toplam kayıt sayısı X-Total-Count header'ında döner.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetList(
        [FromQuery] Guid? merchantId,
        [FromQuery] TransactionStatus? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? terminalId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        PagedResult<TransactionDto> result = await _sender.Send(
            new GetTransactionListQuery(merchantId, status, from, to, terminalId, page, pageSize), ct);

        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result.Items);
    }

    [HttpGet("{id:guid}/status-history")]
    public async Task<ActionResult<IReadOnlyList<StatusHistoryDto>>> GetStatusHistory(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTransactionStatusHistoryQuery(id), ct));
}

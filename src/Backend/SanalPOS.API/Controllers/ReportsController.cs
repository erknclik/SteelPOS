using System.Text;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Common.Models;
using SanalPOS.Application.Payments.Dtos;
using SanalPOS.Application.Payments.Queries;
using SanalPOS.Application.Reporting.Queries;
using SanalPOS.Domain.Entities;

namespace SanalPOS.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[Authorize(Roles = $"{Role.SystemAdmin},{Role.MerchantAdmin}")]
public class ReportsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(ISender sender, ICurrentUserService currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>Günlük işlem özeti (toplam tutar, adet, komisyon).</summary>
    [HttpGet("daily-summary")]
    public async Task<ActionResult<DailySummaryDto>> GetDailySummary(
        [FromQuery] DateTime? day, [FromQuery] Guid? merchantId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetDailySummaryQuery(ResolveMerchant(merchantId), day), ct));

    /// <summary>Banka mutabakat raporu: günün onaylı/iadeli işlemleri.</summary>
    [HttpGet("reconciliation")]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetReconciliation(
        [FromQuery] DateTime? day, [FromQuery] Guid? merchantId, CancellationToken ct = default)
    {
        var dayStart = (day ?? DateTime.UtcNow).Date;
        PagedResult<TransactionDto> result = await _sender.Send(new GetTransactionListQuery(
            ResolveMerchant(merchantId), null, dayStart, dayStart.AddDays(1), null, 1, 100), ct);
        return Ok(result.Items);
    }

    /// <summary>CSV export (?format=csv). İleri fazda Excel desteği eklenebilir.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? merchantId, [FromQuery] string format = "csv", CancellationToken ct = default)
    {
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { title = "Sadece 'csv' formatı desteklenmektedir.", status = 400 });

        PagedResult<TransactionDto> result = await _sender.Send(new GetTransactionListQuery(
            ResolveMerchant(merchantId), null, from, to, null, 1, 100), ct);

        var sb = new StringBuilder();
        sb.AppendLine("Id;OrderReference;Amount;Currency;Installments;Type;Status;MaskedCard;Commission;Net;RequestedAt;CompletedAt");
        foreach (var tx in result.Items)
        {
            sb.AppendLine(string.Join(';',
                tx.Id, tx.OrderReference, tx.Amount, tx.Currency, tx.InstallmentCount, tx.TransactionType,
                tx.Status, tx.MaskedCardNumber, tx.CommissionAmount, tx.NetAmount,
                tx.RequestedAt.ToString("O"), tx.CompletedAt?.ToString("O")));
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"sanalpos-export-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>MerchantAdmin sadece kendi merchant'ının raporunu görür.</summary>
    private Guid? ResolveMerchant(Guid? requested) =>
        _currentUser.IsInRole(Role.SystemAdmin) ? requested : _currentUser.MerchantId;
}

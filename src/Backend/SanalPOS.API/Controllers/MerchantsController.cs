using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Common.Models;
using SanalPOS.Application.Merchants.Commands;
using SanalPOS.Application.Merchants.Dtos;
using SanalPOS.Application.Merchants.Queries;
using SanalPOS.Domain.Entities;

namespace SanalPOS.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/merchants")]
[Authorize]
public class MerchantsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;

    public MerchantsController(ISender sender, ICurrentUserService currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    public sealed record CreateMerchantRequest(string Name, string TaxNumber, string Iban, decimal DefaultCommissionRate);
    public sealed record UpdateMerchantRequest(string Name, string TaxNumber, string Iban, decimal DefaultCommissionRate);
    public sealed record CreateStoreRequest(string Name, string? Address);
    public sealed record CreateTerminalRequest(Guid StoreId, string TerminalCode, string BankProviderCode);
    public sealed record AddCommissionRuleRequest(short InstallmentCount, decimal Rate, DateOnly ValidFrom, DateOnly? ValidTo);

    [HttpPost]
    [Authorize(Roles = Role.SystemAdmin)]
    public async Task<ActionResult<MerchantDto>> Create(CreateMerchantRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CreateMerchantCommand(
            request.Name, request.TaxNumber, request.Iban, request.DefaultCommissionRate), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1" }, result);
    }

    [HttpGet]
    [Authorize(Roles = Role.SystemAdmin)]
    public async Task<ActionResult<IReadOnlyList<MerchantDto>>> GetList(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        PagedResult<MerchantDto> result = await _sender.Send(new GetMerchantListQuery(page, pageSize), ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result.Items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MerchantDto>> GetById(Guid id, CancellationToken ct)
    {
        EnsureCanAccessMerchant(id);
        return Ok(await _sender.Send(new GetMerchantByIdQuery(id), ct));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Role.SystemAdmin)]
    public async Task<ActionResult<MerchantDto>> Update(Guid id, UpdateMerchantRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new UpdateMerchantCommand(
            id, request.Name, request.TaxNumber, request.Iban, request.DefaultCommissionRate), ct));

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = Role.SystemAdmin)]
    public async Task<ActionResult<MerchantDto>> Suspend(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new SuspendMerchantCommand(id), ct));

    [HttpGet("{id:guid}/commission-rules")]
    [Authorize(Roles = Role.SystemAdmin)]
    public async Task<ActionResult<IReadOnlyList<CommissionRuleDto>>> GetCommissionRules(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new GetCommissionRulesQuery(id), ct));

    [HttpPost("{id:guid}/commission-rules")]
    [Authorize(Roles = Role.SystemAdmin)]
    public async Task<ActionResult<CommissionRuleDto>> AddCommissionRule(
        Guid id, AddCommissionRuleRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new AddCommissionRuleCommand(
            id, request.InstallmentCount, request.Rate, request.ValidFrom, request.ValidTo), ct));

    [HttpGet("{id:guid}/stores")]
    [Authorize(Roles = $"{Role.SystemAdmin},{Role.MerchantAdmin}")]
    public async Task<ActionResult<IReadOnlyList<StoreDto>>> GetStores(Guid id, CancellationToken ct)
    {
        EnsureCanAccessMerchant(id);
        return Ok(await _sender.Send(new GetStoresQuery(id), ct));
    }

    [HttpPost("{id:guid}/stores")]
    [Authorize(Roles = $"{Role.SystemAdmin},{Role.MerchantAdmin}")]
    public async Task<ActionResult<StoreDto>> CreateStore(Guid id, CreateStoreRequest request, CancellationToken ct)
    {
        EnsureCanAccessMerchant(id);
        return Ok(await _sender.Send(new CreateStoreCommand(id, request.Name, request.Address), ct));
    }

    [HttpGet("{id:guid}/terminals")]
    [Authorize(Roles = $"{Role.SystemAdmin},{Role.MerchantAdmin}")]
    public async Task<ActionResult<IReadOnlyList<TerminalDto>>> GetTerminals(Guid id, CancellationToken ct)
    {
        EnsureCanAccessMerchant(id);
        return Ok(await _sender.Send(new GetTerminalsQuery(id), ct));
    }

    [HttpPost("{id:guid}/terminals")]
    [Authorize(Roles = $"{Role.SystemAdmin},{Role.MerchantAdmin}")]
    public async Task<ActionResult<TerminalDto>> CreateTerminal(Guid id, CreateTerminalRequest request, CancellationToken ct)
    {
        EnsureCanAccessMerchant(id);
        return Ok(await _sender.Send(new CreateTerminalCommand(
            request.StoreId, request.TerminalCode, request.BankProviderCode), ct));
    }

    /// <summary>MerchantAdmin/Operator sadece kendi merchant'ına erişebilir; SystemAdmin hepsine.</summary>
    private void EnsureCanAccessMerchant(Guid merchantId)
    {
        if (_currentUser.IsInRole(Role.SystemAdmin))
            return;
        if (_currentUser.MerchantId != merchantId)
            throw new ForbiddenException("Bu üye işyerine erişim yetkiniz yok.");
    }
}

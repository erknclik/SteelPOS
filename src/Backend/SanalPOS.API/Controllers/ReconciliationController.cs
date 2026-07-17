using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SanalPOS.Application.Reconciliation;
using SanalPOS.Domain.Entities;

namespace SanalPOS.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reconciliation")]
[Authorize(Roles = Role.SystemAdmin)]
public class ReconciliationController : ControllerBase
{
    private readonly ISender _sender;

    public ReconciliationController(ISender sender) => _sender = sender;

    public sealed record RunReconciliationRequest(DateOnly? Day, string? ProviderCode);

    /// <summary>
    /// Gün sonu mutabakatını manuel tetikler (normalde BackgroundJobs her gün UTC 03:00'te
    /// bir önceki gün için tetikler). Gün verilmezse dün (UTC) mutabakatlanır;
    /// providerCode verilirse yalnızca o banka koşulur.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<IReadOnlyList<ReconciliationResultDto>>> Run(
        RunReconciliationRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new RunReconciliationCommand(request.Day, request.ProviderCode), ct));
}

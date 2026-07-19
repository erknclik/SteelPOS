using FluentValidation;
using MediatR;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Reconciliation;

/// <summary>Son mutabakat koşumları, yeni -> eski (geçmiş ekranı ve dashboard kartı için).</summary>
public sealed record GetReconciliationHistoryQuery(int Count = 20) : IRequest<IReadOnlyList<ReconciliationRunDto>>;

public sealed record ReconciliationRunDto(
    Guid Id,
    string Day,
    string ProviderCode,
    string Currency,
    int SaleCount,
    decimal SaleAmount,
    int RefundCount,
    decimal RefundAmount,
    int VoidCount,
    decimal VoidAmount,
    bool IsBalanced,
    string? ReasonCode,
    string? ReasonMessage,
    DateTime ExecutedAt)
{
    public static ReconciliationRunDto FromEntity(ReconciliationRun run) => new(
        run.Id, run.Day.ToString("yyyy-MM-dd"), run.ProviderCode, run.Currency,
        run.SaleCount, run.SaleAmount, run.RefundCount, run.RefundAmount,
        run.VoidCount, run.VoidAmount, run.IsBalanced, run.ReasonCode, run.ReasonMessage,
        run.ExecutedAt);
}

public sealed class GetReconciliationHistoryQueryValidator : AbstractValidator<GetReconciliationHistoryQuery>
{
    public GetReconciliationHistoryQueryValidator()
    {
        RuleFor(x => x.Count).InclusiveBetween(1, 100);
    }
}

public class GetReconciliationHistoryQueryHandler
    : IRequestHandler<GetReconciliationHistoryQuery, IReadOnlyList<ReconciliationRunDto>>
{
    private readonly IReconciliationRunRepository _runRepository;

    public GetReconciliationHistoryQueryHandler(IReconciliationRunRepository runRepository) =>
        _runRepository = runRepository;

    public async Task<IReadOnlyList<ReconciliationRunDto>> Handle(GetReconciliationHistoryQuery request, CancellationToken ct)
    {
        var runs = await _runRepository.GetRecentAsync(request.Count, ct);
        return runs.Select(ReconciliationRunDto.FromEntity).ToList();
    }
}

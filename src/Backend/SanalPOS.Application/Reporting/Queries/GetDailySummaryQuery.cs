using MediatR;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Reporting.Queries;

public sealed record DailySummaryDto(
    DateTime Day,
    int TotalCount,
    int ApprovedCount,
    int DeclinedCount,
    decimal TotalAmount,
    decimal TotalCommission,
    decimal TotalNet,
    decimal TotalRefunded);

/// <summary>Günlük işlem özeti (toplam tutar, adet, komisyon). Mutabakat raporunun temelidir.</summary>
public sealed record GetDailySummaryQuery(Guid? MerchantId, DateTime? DayUtc) : IRequest<DailySummaryDto>;

public class GetDailySummaryQueryHandler : IRequestHandler<GetDailySummaryQuery, DailySummaryDto>
{
    private readonly IPaymentTransactionRepository _repository;

    public GetDailySummaryQueryHandler(IPaymentTransactionRepository repository) => _repository = repository;

    public async Task<DailySummaryDto> Handle(GetDailySummaryQuery request, CancellationToken ct)
    {
        var day = (request.DayUtc ?? DateTime.UtcNow).Date;
        var summary = await _repository.GetDailySummaryAsync(request.MerchantId, day, ct);

        return new DailySummaryDto(
            summary.Day, summary.TotalCount, summary.ApprovedCount, summary.DeclinedCount,
            summary.TotalAmount, summary.TotalCommission, summary.TotalNet, summary.TotalRefunded);
    }
}

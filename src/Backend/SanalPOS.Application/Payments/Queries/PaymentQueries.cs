using FluentValidation;
using MediatR;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Models;
using SanalPOS.Application.Payments.Dtos;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Payments.Queries;

// ---- Tekil işlem detayı ----

public sealed record GetTransactionByIdQuery(Guid TransactionId) : IRequest<TransactionDto>;

public class GetTransactionByIdQueryHandler : IRequestHandler<GetTransactionByIdQuery, TransactionDto>
{
    private readonly IPaymentTransactionRepository _repository;

    public GetTransactionByIdQueryHandler(IPaymentTransactionRepository repository) => _repository = repository;

    public async Task<TransactionDto> Handle(GetTransactionByIdQuery request, CancellationToken ct)
    {
        var tx = await _repository.GetByIdAsync(request.TransactionId, ct)
                 ?? throw new NotFoundException(nameof(PaymentTransaction), request.TransactionId);
        return TransactionDto.FromEntity(tx);
    }
}

// ---- Filtrelenebilir işlem listesi ----

public sealed record GetTransactionListQuery(
    Guid? MerchantId,
    TransactionStatus? Status,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? TerminalId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<TransactionDto>>;

public class GetTransactionListQueryValidator : AbstractValidator<GetTransactionListQuery>
{
    public GetTransactionListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class GetTransactionListQueryHandler : IRequestHandler<GetTransactionListQuery, PagedResult<TransactionDto>>
{
    private readonly IPaymentTransactionRepository _repository;

    public GetTransactionListQueryHandler(IPaymentTransactionRepository repository) => _repository = repository;

    public async Task<PagedResult<TransactionDto>> Handle(GetTransactionListQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(
            request.MerchantId, request.Status, request.FromUtc, request.ToUtc,
            request.TerminalId, request.Page, request.PageSize, ct);

        return new PagedResult<TransactionDto>(
            items.Select(TransactionDto.FromEntity).ToList(), totalCount, request.Page, request.PageSize);
    }
}

// ---- İşlem durum geçmişi ----

public sealed record GetTransactionStatusHistoryQuery(Guid TransactionId) : IRequest<IReadOnlyList<StatusHistoryDto>>;

public class GetTransactionStatusHistoryQueryHandler : IRequestHandler<GetTransactionStatusHistoryQuery, IReadOnlyList<StatusHistoryDto>>
{
    private readonly IPaymentTransactionRepository _repository;

    public GetTransactionStatusHistoryQueryHandler(IPaymentTransactionRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<StatusHistoryDto>> Handle(GetTransactionStatusHistoryQuery request, CancellationToken ct)
    {
        var tx = await _repository.GetWithHistoryAsync(request.TransactionId, ct)
                 ?? throw new NotFoundException(nameof(PaymentTransaction), request.TransactionId);

        return tx.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => new StatusHistoryDto(h.OldStatus.ToString(), h.NewStatus.ToString(), h.ChangedAt, h.ChangedBy))
            .ToList();
    }
}

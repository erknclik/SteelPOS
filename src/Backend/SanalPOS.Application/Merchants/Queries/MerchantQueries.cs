using MediatR;
using SanalPOS.Application.Common.Behaviors;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Common.Models;
using SanalPOS.Application.Merchants.Dtos;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Merchants.Queries;

// ---- Merchant detayı (cache'li) ----

public sealed record GetMerchantByIdQuery(Guid MerchantId) : IRequest<MerchantDto>, ICacheableQuery
{
    public string CacheKey => CacheKeys.Merchant(MerchantId);
    public TimeSpan Expiry => TimeSpan.FromMinutes(15);
}

public class GetMerchantByIdQueryHandler : IRequestHandler<GetMerchantByIdQuery, MerchantDto>
{
    private readonly IMerchantRepository _repository;

    public GetMerchantByIdQueryHandler(IMerchantRepository repository) => _repository = repository;

    public async Task<MerchantDto> Handle(GetMerchantByIdQuery request, CancellationToken ct)
    {
        var merchant = await _repository.GetByIdAsync(request.MerchantId, ct)
                       ?? throw new NotFoundException(nameof(Merchant), request.MerchantId);
        return MerchantDto.FromEntity(merchant);
    }
}

// ---- Merchant listesi ----

public sealed record GetMerchantListQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<MerchantDto>>;

public class GetMerchantListQueryHandler : IRequestHandler<GetMerchantListQuery, PagedResult<MerchantDto>>
{
    private readonly IMerchantRepository _repository;

    public GetMerchantListQueryHandler(IMerchantRepository repository) => _repository = repository;

    public async Task<PagedResult<MerchantDto>> Handle(GetMerchantListQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(request.Page, request.PageSize, ct);
        return new PagedResult<MerchantDto>(
            items.Select(MerchantDto.FromEntity).ToList(), totalCount, request.Page, request.PageSize);
    }
}

// ---- Mağazalar ----

public sealed record GetStoresQuery(Guid MerchantId) : IRequest<IReadOnlyList<StoreDto>>;

public class GetStoresQueryHandler : IRequestHandler<GetStoresQuery, IReadOnlyList<StoreDto>>
{
    private readonly IStoreRepository _repository;

    public GetStoresQueryHandler(IStoreRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<StoreDto>> Handle(GetStoresQuery request, CancellationToken ct)
    {
        var stores = await _repository.GetByMerchantAsync(request.MerchantId, ct);
        return stores.Select(StoreDto.FromEntity).ToList();
    }
}

// ---- Terminaller ----

public sealed record GetTerminalsQuery(Guid MerchantId) : IRequest<IReadOnlyList<TerminalDto>>;

public class GetTerminalsQueryHandler : IRequestHandler<GetTerminalsQuery, IReadOnlyList<TerminalDto>>
{
    private readonly ITerminalRepository _repository;

    public GetTerminalsQueryHandler(ITerminalRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<TerminalDto>> Handle(GetTerminalsQuery request, CancellationToken ct)
    {
        var terminals = await _repository.GetByMerchantAsync(request.MerchantId, ct);
        return terminals.Select(TerminalDto.FromEntity).ToList();
    }
}

// ---- Komisyon kuralları (cache'li) ----

public sealed record GetCommissionRulesQuery(Guid MerchantId) : IRequest<IReadOnlyList<CommissionRuleDto>>, ICacheableQuery
{
    public string CacheKey => CacheKeys.CommissionRules(MerchantId);
    public TimeSpan Expiry => TimeSpan.FromMinutes(30);
}

public class GetCommissionRulesQueryHandler : IRequestHandler<GetCommissionRulesQuery, IReadOnlyList<CommissionRuleDto>>
{
    private readonly ICommissionRuleRepository _repository;

    public GetCommissionRulesQueryHandler(ICommissionRuleRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<CommissionRuleDto>> Handle(GetCommissionRulesQuery request, CancellationToken ct)
    {
        var rules = await _repository.GetByMerchantAsync(request.MerchantId, ct);
        return rules.Select(CommissionRuleDto.FromEntity).ToList();
    }
}

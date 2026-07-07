using FluentValidation;
using MediatR;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Merchants.Dtos;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Interfaces;
using SanalPOS.Domain.ValueObjects;

namespace SanalPOS.Application.Merchants.Commands;

// ---- Merchant oluşturma ----

public sealed record CreateMerchantCommand(string Name, string TaxNumber, string Iban, decimal DefaultCommissionRate)
    : IRequest<MerchantDto>;

public class CreateMerchantCommandValidator : AbstractValidator<CreateMerchantCommand>
{
    public CreateMerchantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TaxNumber).NotEmpty().MaximumLength(20).Matches(@"^\d+$").WithMessage("Vergi numarası sadece rakam içermelidir.");
        RuleFor(x => x.Iban).NotEmpty().MaximumLength(34);
        RuleFor(x => x.DefaultCommissionRate).InclusiveBetween(0, 100);
    }
}

public class CreateMerchantCommandHandler : IRequestHandler<CreateMerchantCommand, MerchantDto>
{
    private readonly IMerchantRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateMerchantCommandHandler(IMerchantRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MerchantDto> Handle(CreateMerchantCommand request, CancellationToken ct)
    {
        var merchant = new Merchant(request.Name, request.TaxNumber, new Iban(request.Iban), request.DefaultCommissionRate);
        await _repository.AddAsync(merchant, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MerchantDto.FromEntity(merchant);
    }
}

// ---- Merchant güncelleme ----

public sealed record UpdateMerchantCommand(Guid MerchantId, string Name, string TaxNumber, string Iban, decimal DefaultCommissionRate)
    : IRequest<MerchantDto>;

public class UpdateMerchantCommandValidator : AbstractValidator<UpdateMerchantCommand>
{
    public UpdateMerchantCommandValidator()
    {
        RuleFor(x => x.MerchantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TaxNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Iban).NotEmpty().MaximumLength(34);
        RuleFor(x => x.DefaultCommissionRate).InclusiveBetween(0, 100);
    }
}

public class UpdateMerchantCommandHandler : IRequestHandler<UpdateMerchantCommand, MerchantDto>
{
    private readonly IMerchantRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public UpdateMerchantCommandHandler(IMerchantRepository repository, IUnitOfWork unitOfWork, ICacheService cache)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<MerchantDto> Handle(UpdateMerchantCommand request, CancellationToken ct)
    {
        var merchant = await _repository.GetByIdAsync(request.MerchantId, ct)
                       ?? throw new NotFoundException(nameof(Merchant), request.MerchantId);

        merchant.Update(request.Name, request.TaxNumber, new Iban(request.Iban), request.DefaultCommissionRate);
        _repository.Update(merchant);
        await _unitOfWork.SaveChangesAsync(ct);

        // Aktif invalidation: merchant detay cache'i temizlenir (bkz. docs/06-cache-redis.md §5).
        await _cache.RemoveAsync(CacheKeys.Merchant(merchant.Id), ct);

        return MerchantDto.FromEntity(merchant);
    }
}

// ---- Merchant askıya alma ----

public sealed record SuspendMerchantCommand(Guid MerchantId) : IRequest<MerchantDto>;

public class SuspendMerchantCommandHandler : IRequestHandler<SuspendMerchantCommand, MerchantDto>
{
    private readonly IMerchantRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public SuspendMerchantCommandHandler(IMerchantRepository repository, IUnitOfWork unitOfWork, ICacheService cache)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<MerchantDto> Handle(SuspendMerchantCommand request, CancellationToken ct)
    {
        var merchant = await _repository.GetByIdAsync(request.MerchantId, ct)
                       ?? throw new NotFoundException(nameof(Merchant), request.MerchantId);

        merchant.Suspend();
        _repository.Update(merchant);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.Merchant(merchant.Id), ct);

        return MerchantDto.FromEntity(merchant);
    }
}

// ---- Mağaza oluşturma ----

public sealed record CreateStoreCommand(Guid MerchantId, string Name, string? Address) : IRequest<StoreDto>;

public class CreateStoreCommandValidator : AbstractValidator<CreateStoreCommand>
{
    public CreateStoreCommandValidator()
    {
        RuleFor(x => x.MerchantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class CreateStoreCommandHandler : IRequestHandler<CreateStoreCommand, StoreDto>
{
    private readonly IMerchantRepository _merchantRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateStoreCommandHandler(IMerchantRepository merchantRepository, IStoreRepository storeRepository, IUnitOfWork unitOfWork)
    {
        _merchantRepository = merchantRepository;
        _storeRepository = storeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<StoreDto> Handle(CreateStoreCommand request, CancellationToken ct)
    {
        _ = await _merchantRepository.GetByIdAsync(request.MerchantId, ct)
            ?? throw new NotFoundException(nameof(Merchant), request.MerchantId);

        var store = new Store(request.MerchantId, request.Name, request.Address);
        await _storeRepository.AddAsync(store, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return StoreDto.FromEntity(store);
    }
}

// ---- Terminal oluşturma ----

public sealed record CreateTerminalCommand(Guid StoreId, string TerminalCode, string BankProviderCode) : IRequest<TerminalDto>;

public class CreateTerminalCommandValidator : AbstractValidator<CreateTerminalCommand>
{
    public CreateTerminalCommandValidator()
    {
        RuleFor(x => x.StoreId).NotEmpty();
        RuleFor(x => x.TerminalCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.BankProviderCode).NotEmpty().MaximumLength(50);
    }
}

public class CreateTerminalCommandHandler : IRequestHandler<CreateTerminalCommand, TerminalDto>
{
    private readonly IStoreRepository _storeRepository;
    private readonly ITerminalRepository _terminalRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTerminalCommandHandler(IStoreRepository storeRepository, ITerminalRepository terminalRepository, IUnitOfWork unitOfWork)
    {
        _storeRepository = storeRepository;
        _terminalRepository = terminalRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TerminalDto> Handle(CreateTerminalCommand request, CancellationToken ct)
    {
        _ = await _storeRepository.GetByIdAsync(request.StoreId, ct)
            ?? throw new NotFoundException(nameof(Store), request.StoreId);

        var existing = await _terminalRepository.GetByCodeAsync(request.TerminalCode, ct);
        if (existing is not null)
            throw new ConflictException($"'{request.TerminalCode}' kodlu terminal zaten mevcut.");

        var terminal = new Terminal(request.StoreId, request.TerminalCode, request.BankProviderCode);
        await _terminalRepository.AddAsync(terminal, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return TerminalDto.FromEntity(terminal);
    }
}

// ---- Komisyon kuralı ekleme ----

public sealed record AddCommissionRuleCommand(Guid MerchantId, short InstallmentCount, decimal Rate, DateOnly ValidFrom, DateOnly? ValidTo)
    : IRequest<CommissionRuleDto>;

public class AddCommissionRuleCommandValidator : AbstractValidator<AddCommissionRuleCommand>
{
    public AddCommissionRuleCommandValidator()
    {
        RuleFor(x => x.MerchantId).NotEmpty();
        RuleFor(x => x.InstallmentCount).InclusiveBetween((short)1, (short)12);
        RuleFor(x => x.Rate).InclusiveBetween(0, 100);
    }
}

public class AddCommissionRuleCommandHandler : IRequestHandler<AddCommissionRuleCommand, CommissionRuleDto>
{
    private readonly IMerchantRepository _merchantRepository;
    private readonly ICommissionRuleRepository _ruleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public AddCommissionRuleCommandHandler(
        IMerchantRepository merchantRepository,
        ICommissionRuleRepository ruleRepository,
        IUnitOfWork unitOfWork,
        ICacheService cache)
    {
        _merchantRepository = merchantRepository;
        _ruleRepository = ruleRepository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<CommissionRuleDto> Handle(AddCommissionRuleCommand request, CancellationToken ct)
    {
        _ = await _merchantRepository.GetByIdAsync(request.MerchantId, ct)
            ?? throw new NotFoundException(nameof(Merchant), request.MerchantId);

        var rule = new CommissionRule(request.MerchantId, request.InstallmentCount, request.Rate, request.ValidFrom, request.ValidTo);
        await _ruleRepository.AddAsync(rule, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.CommissionRules(request.MerchantId), ct);

        return CommissionRuleDto.FromEntity(rule);
    }
}

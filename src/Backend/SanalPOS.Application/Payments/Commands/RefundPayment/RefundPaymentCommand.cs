using FluentValidation;
using MediatR;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Payments.Dtos;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Exceptions;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Payments.Commands.RefundPayment;

/// <summary>Kısmi veya tam iade oluşturur.</summary>
public sealed record RefundPaymentCommand(Guid TransactionId, decimal Amount, string? Reason) : IRequest<RefundResultDto>;

public class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.TransactionId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("İade tutarı sıfırdan büyük olmalıdır.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, RefundResultDto>
{
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IRefundTransactionRepository _refundRepository;
    private readonly IBankAdapterFactory _bankAdapterFactory;
    private readonly IDistributedLockService _lockService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public RefundPaymentCommandHandler(
        IPaymentTransactionRepository transactionRepository,
        IRefundTransactionRepository refundRepository,
        IBankAdapterFactory bankAdapterFactory,
        IDistributedLockService lockService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _transactionRepository = transactionRepository;
        _refundRepository = refundRepository;
        _bankAdapterFactory = bankAdapterFactory;
        _lockService = lockService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<RefundResultDto> Handle(RefundPaymentCommand request, CancellationToken ct)
    {
        // Aynı işleme eşzamanlı iade denemelerini distributed lock ile engelle.
        await using var lockHandle = await _lockService.AcquireAsync(
            CacheKeys.TransactionLock(request.TransactionId), TimeSpan.FromSeconds(30), ct);
        if (lockHandle is null)
            throw new ConflictException("İşlem üzerinde devam eden başka bir operasyon var, lütfen tekrar deneyin.");

        var transaction = await _transactionRepository.GetWithHistoryAsync(request.TransactionId, ct)
                          ?? throw new NotFoundException(nameof(PaymentTransaction), request.TransactionId);

        var adapter = _bankAdapterFactory.Resolve(transaction.BankProviderCode);
        var reference = new BankTransactionReference(
            transaction.BankAuthCode ?? string.Empty, transaction.BankRrn, transaction.BankStan,
            transaction.Amount.Amount, transaction.Amount.Currency);
        var bankResult = await adapter.RefundAsync(reference, request.Amount, ct);
        if (!bankResult.IsSuccessful)
            throw new DomainException(bankResult.ReasonMessage ?? "Banka iadeyi reddetti.");

        var refund = new RefundTransaction(transaction.Id, request.Amount, request.Reason);
        refund.Complete();
        transaction.ApplyRefund(refund, _currentUser.UserName);

        await _refundRepository.AddAsync(refund, ct);
        _transactionRepository.Update(transaction);
        await _unitOfWork.SaveChangesAsync(ct);

        return new RefundResultDto(
            refund.Id, transaction.Id, refund.RefundAmount, refund.Status.ToString(), transaction.Status.ToString());
    }
}

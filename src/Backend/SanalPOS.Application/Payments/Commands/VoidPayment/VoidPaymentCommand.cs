using FluentValidation;
using MediatR;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Payments.Dtos;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Exceptions;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Payments.Commands.VoidPayment;

/// <summary>Aynı gün içindeki işlemi iptal eder (void).</summary>
public sealed record VoidPaymentCommand(Guid TransactionId) : IRequest<PaymentResultDto>;

public class VoidPaymentCommandValidator : AbstractValidator<VoidPaymentCommand>
{
    public VoidPaymentCommandValidator()
    {
        RuleFor(x => x.TransactionId).NotEmpty();
    }
}

public class VoidPaymentCommandHandler : IRequestHandler<VoidPaymentCommand, PaymentResultDto>
{
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IBankAdapterFactory _bankAdapterFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public VoidPaymentCommandHandler(
        IPaymentTransactionRepository transactionRepository,
        IBankAdapterFactory bankAdapterFactory,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _transactionRepository = transactionRepository;
        _bankAdapterFactory = bankAdapterFactory;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<PaymentResultDto> Handle(VoidPaymentCommand request, CancellationToken ct)
    {
        var transaction = await _transactionRepository.GetWithHistoryAsync(request.TransactionId, ct)
                          ?? throw new NotFoundException(nameof(PaymentTransaction), request.TransactionId);

        var adapter = _bankAdapterFactory.Resolve(transaction.BankProviderCode);
        var reference = new BankTransactionReference(
            transaction.BankAuthCode ?? string.Empty, transaction.BankRrn, transaction.BankStan,
            transaction.Amount.Amount, transaction.Amount.Currency);
        var result = await adapter.VoidAsync(reference, ct);
        if (!result.IsSuccessful)
            throw new DomainException(result.ReasonMessage ?? "Banka iptali reddetti.");

        transaction.Void(_currentUser.UserName);
        _transactionRepository.Update(transaction);
        await _unitOfWork.SaveChangesAsync(ct);

        return new PaymentResultDto(
            transaction.Id, transaction.Status.ToString(), transaction.BankAuthCode,
            transaction.CommissionAmount, transaction.NetAmount, transaction.CompletedAt);
    }
}

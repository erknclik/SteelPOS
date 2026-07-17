using FluentValidation;
using MediatR;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Payments.Dtos;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Exceptions;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Payments.Commands.CapturePayment;

/// <summary>Ön provizyonu tahsilata çevirir (provizyon kapama).</summary>
public sealed record CapturePaymentCommand(Guid TransactionId) : IRequest<PaymentResultDto>;

public class CapturePaymentCommandValidator : AbstractValidator<CapturePaymentCommand>
{
    public CapturePaymentCommandValidator()
    {
        RuleFor(x => x.TransactionId).NotEmpty();
    }
}

public class CapturePaymentCommandHandler : IRequestHandler<CapturePaymentCommand, PaymentResultDto>
{
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IBankAdapterFactory _bankAdapterFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CapturePaymentCommandHandler(
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

    public async Task<PaymentResultDto> Handle(CapturePaymentCommand request, CancellationToken ct)
    {
        var transaction = await _transactionRepository.GetWithHistoryAsync(request.TransactionId, ct)
                          ?? throw new NotFoundException(nameof(PaymentTransaction), request.TransactionId);

        var adapter = _bankAdapterFactory.Resolve(transaction.BankProviderCode);
        var reference = new BankTransactionReference(
            transaction.BankAuthCode ?? string.Empty, transaction.BankRrn, transaction.BankStan,
            transaction.Amount.Amount, transaction.Amount.Currency);
        var result = await adapter.CaptureAsync(reference, transaction.Amount.Amount, ct);
        if (!result.IsSuccessful)
            throw new DomainException(result.ReasonMessage ?? "Banka provizyon kapamayı reddetti.");

        transaction.Capture(_currentUser.UserName);
        _transactionRepository.Update(transaction);
        await _unitOfWork.SaveChangesAsync(ct);

        return new PaymentResultDto(
            transaction.Id, transaction.Status.ToString(), transaction.BankAuthCode,
            transaction.CommissionAmount, transaction.NetAmount, transaction.CompletedAt);
    }
}

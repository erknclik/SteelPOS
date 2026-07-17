using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Payments.Dtos;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Payments.Commands.ThreeDSecure;

/// <summary>
/// ACS dönüşünü (MD + PaRes) işler: 3DS oturumundaki şifreli kart bağlamı tek kullanımlık
/// okunur ve hemen silinir, PaRes MPI'da doğrulanır; başarılıysa otorizasyon ECI/CAVV
/// kanıtıyla bankaya gönderilir, değilse işlem reddedilir.
/// </summary>
public sealed record Complete3DSPaymentCommand(string Md, string PaRes) : IRequest<PaymentResultDto>;

public sealed class Complete3DSPaymentCommandValidator : AbstractValidator<Complete3DSPaymentCommand>
{
    public Complete3DSPaymentCommandValidator()
    {
        RuleFor(x => x.Md).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PaRes).NotEmpty().MaximumLength(20_000);
    }
}

public class Complete3DSPaymentCommandHandler : IRequestHandler<Complete3DSPaymentCommand, PaymentResultDto>
{
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IMerchantRepository _merchantRepository;
    private readonly IThreeDSecureProvider _threeDSecureProvider;
    private readonly IBankAdapterFactory _bankAdapterFactory;
    private readonly ICacheService _cache;
    private readonly ISecretProtector _secretProtector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<Complete3DSPaymentCommandHandler> _logger;

    public Complete3DSPaymentCommandHandler(
        IPaymentTransactionRepository transactionRepository,
        IMerchantRepository merchantRepository,
        IThreeDSecureProvider threeDSecureProvider,
        IBankAdapterFactory bankAdapterFactory,
        ICacheService cache,
        ISecretProtector secretProtector,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILogger<Complete3DSPaymentCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _merchantRepository = merchantRepository;
        _threeDSecureProvider = threeDSecureProvider;
        _bankAdapterFactory = bankAdapterFactory;
        _cache = cache;
        _secretProtector = secretProtector;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PaymentResultDto> Handle(Complete3DSPaymentCommand request, CancellationToken ct)
    {
        var sessionKey = CacheKeys.ThreeDSecureSession(request.Md);
        var protectedContext = await _cache.GetAsync<string>(sessionKey, ct)
                               ?? throw new NotFoundException("3DS oturumu", request.Md);

        // Tek kullanımlık: aynı MD ile ikinci callback işlenmez (replay koruması).
        await _cache.RemoveAsync(sessionKey, ct);

        var context = JsonSerializer.Deserialize<ThreeDSCardContext>(_secretProtector.Unprotect(protectedContext))
                      ?? throw new ConflictException("3DS oturum verisi çözümlenemedi.");

        var transaction = await _transactionRepository.GetByIdAsync(context.TransactionId, ct)
                          ?? throw new NotFoundException(nameof(PaymentTransaction), context.TransactionId);

        if (transaction.Status != TransactionStatus.Pending3DS)
            throw new ConflictException($"İşlem 3DS sonucu bekleyen durumda değil ('{transaction.Status}').");

        var performedBy = _currentUser.UserName;
        var verification = await _threeDSecureProvider.VerifyAsync(request.Md, request.PaRes, ct);

        if (!verification.IsAuthenticated)
        {
            _logger.LogWarning("3DS doğrulaması başarısız. TransactionId: {TransactionId}, Sebep: {Reason}",
                transaction.Id, verification.FailureReason);

            transaction.Decline("3DS-FAIL", verification.FailureReason ?? "3D Secure doğrulaması başarısız.", performedBy);
            await _unitOfWork.SaveChangesAsync(ct);
            return ToResult(transaction);
        }

        var merchant = await _merchantRepository.GetWithCommissionRulesAsync(context.MerchantId, ct)
                       ?? throw new NotFoundException(nameof(Merchant), context.MerchantId);

        var adapter = _bankAdapterFactory.Resolve(transaction.BankProviderCode);
        var chargeRequest = new ChargeRequest(
            context.CardNumber, context.CardHolderName, context.ExpireMonth, context.ExpireYear,
            context.Cvv, context.Amount, context.Currency, context.InstallmentCount, context.OrderReference,
            new ThreeDSecureData(verification.Eci!, verification.Cavv!, verification.Xid));

        var chargeResult = context.TransactionType == TransactionType.PreAuth
            ? await adapter.PreAuthAsync(chargeRequest, ct)
            : await adapter.ChargeAsync(chargeRequest, ct);

        if (chargeResult.IsApproved)
        {
            transaction.Approve(
                chargeResult.AuthCode!,
                merchant.ResolveCommissionRate(context.InstallmentCount, DateTime.UtcNow),
                performedBy,
                chargeResult.Rrn,
                chargeResult.Stan);
        }
        else
        {
            transaction.Decline(
                chargeResult.ReasonCode ?? "UNKNOWN",
                chargeResult.ReasonMessage ?? "Banka işlemi reddetti.",
                performedBy);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "3DS ödeme sonuçlandı. TransactionId: {TransactionId}, Status: {Status}",
            transaction.Id, transaction.Status);

        return ToResult(transaction);
    }

    private static PaymentResultDto ToResult(PaymentTransaction tx) => new(
        tx.Id, tx.Status.ToString(), tx.BankAuthCode, tx.CommissionAmount, tx.NetAmount, tx.CompletedAt);
}

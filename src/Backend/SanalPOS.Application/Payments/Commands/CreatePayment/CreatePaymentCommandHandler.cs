using MediatR;
using Microsoft.Extensions.Logging;
using SanalPOS.Application.Common.Exceptions;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Payments.Dtos;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Interfaces;
using SanalPOS.Domain.ValueObjects;

namespace SanalPOS.Application.Payments.Commands.CreatePayment;

public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentResultDto>
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IMerchantRepository _merchantRepository;
    private readonly ITerminalRepository _terminalRepository;
    private readonly IBankAdapterFactory _bankAdapterFactory;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CreatePaymentCommandHandler> _logger;

    public CreatePaymentCommandHandler(
        IPaymentTransactionRepository transactionRepository,
        IMerchantRepository merchantRepository,
        ITerminalRepository terminalRepository,
        IBankAdapterFactory bankAdapterFactory,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILogger<CreatePaymentCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _merchantRepository = merchantRepository;
        _terminalRepository = terminalRepository;
        _bankAdapterFactory = bankAdapterFactory;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PaymentResultDto> Handle(CreatePaymentCommand request, CancellationToken ct)
    {
        // 1) Idempotency: aynı key ile gelen tekrar istek, var olan işlemi döndürür.
        var transactionId = Guid.NewGuid();
        var acquired = await _cache.SetIfNotExistsAsync(
            CacheKeys.Idempotency(request.IdempotencyKey), transactionId.ToString(), IdempotencyTtl, ct);

        if (!acquired)
        {
            var existing = await _transactionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
            if (existing is not null)
            {
                _logger.LogWarning("Tekrar eden idempotency key: {IdempotencyKey}, mevcut işlem döndürüldü. TransactionId: {TransactionId}",
                    request.IdempotencyKey, existing.Id);
                return ToResult(existing);
            }

            // Key rezerve edilmiş ama işlem henüz DB'ye yazılmamış: eşzamanlı çift istek.
            throw new ConflictException("Aynı Idempotency-Key ile bir işlem şu anda işleniyor.");
        }

        var merchant = await _merchantRepository.GetWithCommissionRulesAsync(request.MerchantId, ct)
                       ?? throw new NotFoundException(nameof(Merchant), request.MerchantId);

        var terminal = await _terminalRepository.GetByIdAsync(request.TerminalId, ct)
                       ?? throw new NotFoundException(nameof(Terminal), request.TerminalId);
        if (!terminal.IsActive)
            throw new ConflictException("Terminal aktif değil.");

        // 2) İşlemi Pending olarak oluştur (tam PAN değil, maskeli hali saklanır).
        var transaction = new PaymentTransaction(
            merchant.Id,
            terminal.Id,
            request.OrderReference,
            new Money(request.Amount, request.Currency),
            request.InstallmentCount,
            request.TransactionType,
            MaskedCardNumber.FromPan(request.CardNumber),
            request.CardHolderName,
            terminal.BankProviderCode,
            request.IdempotencyKey);

        // 3) Banka adaptörüne gönder (adapter pattern; ilk fazda mock).
        var adapter = _bankAdapterFactory.Resolve(terminal.BankProviderCode);
        var chargeRequest = new ChargeRequest(
            request.CardNumber, request.CardHolderName, request.ExpireMonth, request.ExpireYear,
            request.Cvv, request.Amount, request.Currency, request.InstallmentCount, request.OrderReference);

        var chargeResult = request.TransactionType == TransactionType.PreAuth
            ? await adapter.PreAuthAsync(chargeRequest, ct)
            : await adapter.ChargeAsync(chargeRequest, ct);

        // 4) Sonuca göre durum geçişi (domain event'ler entity içinde üretilir).
        var performedBy = _currentUser.UserName;
        if (chargeResult.IsApproved)
        {
            var commissionRate = merchant.ResolveCommissionRate(request.InstallmentCount, DateTime.UtcNow);
            transaction.Approve(chargeResult.AuthCode!, commissionRate, performedBy);
        }
        else
        {
            transaction.Decline(
                chargeResult.ReasonCode ?? "UNKNOWN",
                chargeResult.ReasonMessage ?? "Banka işlemi reddetti.",
                performedBy);
        }

        await _transactionRepository.AddAsync(transaction, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ödeme işlemi sonuçlandı. TransactionId: {TransactionId}, Status: {Status}, MaskedCard: {MaskedCard}",
            transaction.Id, transaction.Status, transaction.MaskedCard.Value);

        return ToResult(transaction);
    }

    private static PaymentResultDto ToResult(PaymentTransaction tx) => new(
        tx.Id, tx.Status.ToString(), tx.BankAuthCode, tx.CommissionAmount, tx.NetAmount, tx.CompletedAt);
}

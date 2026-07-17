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
using SanalPOS.Domain.ValueObjects;

namespace SanalPOS.Application.Payments.Commands.ThreeDSecure;

/// <summary>
/// 3D Secure ödeme akışını başlatır. Kart 3DS'e kayıtlıysa işlem Pending3DS durumuna
/// alınır ve kart hamilinin yönlendirileceği ACS bilgileri döner; kayıtlı değilse
/// işlem doğrudan (3DS'siz) otorize edilir.
///
/// PCI notu: kart verisi ACS dönüşüne kadar gereklidir; bu süre boyunca yalnızca
/// ISecretProtector ile şifrelenmiş halde, kısa TTL'li cache'te (MD anahtarıyla) tutulur,
/// callback'te tek kullanımlık okunup silinir. Veritabanına asla yazılmaz.
/// </summary>
public sealed record Initiate3DSPaymentCommand(
    Guid MerchantId,
    Guid TerminalId,
    string OrderReference,
    decimal Amount,
    string Currency,
    short InstallmentCount,
    string CardNumber,
    string CardHolderName,
    int ExpireMonth,
    int ExpireYear,
    string Cvv,
    string IdempotencyKey,
    string CallbackUrl,
    TransactionType TransactionType = TransactionType.Sale) : IRequest<ThreeDSInitiationResultDto>;

public sealed class Initiate3DSPaymentCommandValidator : AbstractValidator<Initiate3DSPaymentCommand>
{
    private static readonly string[] SupportedCurrencies = ["TRY", "USD", "EUR"];

    public Initiate3DSPaymentCommandValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Currency).NotEmpty().Length(3)
            .Must(c => SupportedCurrencies.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Geçersiz para birimi kodu.");
        RuleFor(x => x.CardNumber).NotEmpty().CreditCard().WithMessage("Geçersiz kart numarası.");
        RuleFor(x => x.CardHolderName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ExpireMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.ExpireYear).GreaterThanOrEqualTo(_ => DateTime.UtcNow.Year)
            .WithMessage("Kartın son kullanma yılı geçmiş olamaz.");
        RuleFor(x => x.Cvv).NotEmpty().Matches(@"^\d{3,4}$").WithMessage("CVV 3 veya 4 haneli olmalıdır.");
        RuleFor(x => x.InstallmentCount).InclusiveBetween((short)1, (short)12);
        RuleFor(x => x.OrderReference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CallbackUrl).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TransactionType)
            .Must(t => t is TransactionType.Sale or TransactionType.PreAuth)
            .WithMessage("3DS akışı sadece Sale veya PreAuth başlatabilir.");
    }
}

public class Initiate3DSPaymentCommandHandler : IRequestHandler<Initiate3DSPaymentCommand, ThreeDSInitiationResultDto>
{
    /// <summary>Kart hamilinin ACS'te doğrulamayı tamamlaması için tanınan süre.</summary>
    public static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IMerchantRepository _merchantRepository;
    private readonly ITerminalRepository _terminalRepository;
    private readonly IThreeDSecureProvider _threeDSecureProvider;
    private readonly IBankAdapterFactory _bankAdapterFactory;
    private readonly ICacheService _cache;
    private readonly ISecretProtector _secretProtector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<Initiate3DSPaymentCommandHandler> _logger;

    public Initiate3DSPaymentCommandHandler(
        IPaymentTransactionRepository transactionRepository,
        IMerchantRepository merchantRepository,
        ITerminalRepository terminalRepository,
        IThreeDSecureProvider threeDSecureProvider,
        IBankAdapterFactory bankAdapterFactory,
        ICacheService cache,
        ISecretProtector secretProtector,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILogger<Initiate3DSPaymentCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _merchantRepository = merchantRepository;
        _terminalRepository = terminalRepository;
        _threeDSecureProvider = threeDSecureProvider;
        _bankAdapterFactory = bankAdapterFactory;
        _cache = cache;
        _secretProtector = secretProtector;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ThreeDSInitiationResultDto> Handle(Initiate3DSPaymentCommand request, CancellationToken ct)
    {
        var acquired = await _cache.SetIfNotExistsAsync(
            CacheKeys.Idempotency(request.IdempotencyKey), Guid.NewGuid().ToString(), IdempotencyTtl, ct);

        if (!acquired)
        {
            // 3DS başlatma tekrarlanabilir bir okuma değildir (ACS oturumu tek kullanımlık):
            // aynı key ile ikinci istek her durumda çakışma kabul edilir.
            throw new ConflictException("Aynı Idempotency-Key ile bir 3DS işlemi zaten başlatıldı.");
        }

        var merchant = await _merchantRepository.GetWithCommissionRulesAsync(request.MerchantId, ct)
                       ?? throw new NotFoundException(nameof(Merchant), request.MerchantId);

        var terminal = await _terminalRepository.GetByIdAsync(request.TerminalId, ct)
                       ?? throw new NotFoundException(nameof(Terminal), request.TerminalId);
        if (!terminal.IsActive)
            throw new ConflictException("Terminal aktif değil.");

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

        var enrollment = await _threeDSecureProvider.InitiateAsync(new ThreeDSEnrollmentRequest(
            transaction.Id, request.CardNumber, request.ExpireMonth, request.ExpireYear,
            request.Amount, request.Currency, request.OrderReference, request.CallbackUrl), ct);

        var performedBy = _currentUser.UserName;

        if (!enrollment.IsEnrolled)
        {
            // Kart 3DS'e kayıtlı değil: doğrudan otorizasyona düş (fallback; bkz. docs/16 §4).
            _logger.LogInformation("Kart 3DS'e kayıtlı değil, doğrudan otorizasyon. TransactionId: {TransactionId}",
                transaction.Id);

            var adapter = _bankAdapterFactory.Resolve(terminal.BankProviderCode);
            var chargeRequest = new ChargeRequest(
                request.CardNumber, request.CardHolderName, request.ExpireMonth, request.ExpireYear,
                request.Cvv, request.Amount, request.Currency, request.InstallmentCount, request.OrderReference);

            var chargeResult = request.TransactionType == TransactionType.PreAuth
                ? await adapter.PreAuthAsync(chargeRequest, ct)
                : await adapter.ChargeAsync(chargeRequest, ct);

            if (chargeResult.IsApproved)
                transaction.Approve(chargeResult.AuthCode!, merchant.ResolveCommissionRate(request.InstallmentCount, DateTime.UtcNow), performedBy);
            else
                transaction.Decline(chargeResult.ReasonCode ?? "UNKNOWN", chargeResult.ReasonMessage ?? "Banka işlemi reddetti.", performedBy);

            await _transactionRepository.AddAsync(transaction, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ThreeDSInitiationResultDto.CompletedWithoutRedirect(new PaymentResultDto(
                transaction.Id, transaction.Status.ToString(), transaction.BankAuthCode,
                transaction.CommissionAmount, transaction.NetAmount, transaction.CompletedAt));
        }

        // Kart kayıtlı: işlem Pending3DS'e alınır, kart bağlamı şifreli ve tek kullanımlık saklanır.
        transaction.StartThreeDSecure(performedBy);
        await _transactionRepository.AddAsync(transaction, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var context = new ThreeDSCardContext(
            transaction.Id, request.MerchantId, request.CardNumber, request.CardHolderName,
            request.ExpireMonth, request.ExpireYear, request.Cvv,
            request.Amount, request.Currency, request.InstallmentCount,
            request.OrderReference, request.TransactionType);

        var protectedContext = _secretProtector.Protect(JsonSerializer.Serialize(context));
        await _cache.SetAsync(CacheKeys.ThreeDSecureSession(enrollment.Md!), protectedContext, SessionTtl, ct);

        _logger.LogInformation(
            "3DS akışı başlatıldı. TransactionId: {TransactionId}, MaskedCard: {MaskedCard}",
            transaction.Id, transaction.MaskedCard.Value);

        return ThreeDSInitiationResultDto.Redirect(transaction.Id, enrollment.Md!, enrollment.AcsUrl!, enrollment.PaReq);
    }
}

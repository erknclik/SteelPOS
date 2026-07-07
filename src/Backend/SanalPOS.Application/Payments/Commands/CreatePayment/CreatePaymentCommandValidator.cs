using FluentValidation;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Payments.Commands.CreatePayment;

public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    private static readonly string[] SupportedCurrencies = ["TRY", "USD", "EUR"];

    private readonly IMerchantRepository _merchantRepository;

    public CreatePaymentCommandValidator(IMerchantRepository merchantRepository)
    {
        _merchantRepository = merchantRepository;

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Tutar sıfırdan büyük olmalıdır.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Tutar izin verilen üst limiti aşıyor.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .Must(BeAValidCurrency).WithMessage("Geçersiz para birimi kodu.");

        RuleFor(x => x.CardNumber)
            .NotEmpty()
            .CreditCard().WithMessage("Geçersiz kart numarası.");

        RuleFor(x => x.CardHolderName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.ExpireMonth).InclusiveBetween(1, 12);

        RuleFor(x => x.ExpireYear)
            .GreaterThanOrEqualTo(_ => DateTime.UtcNow.Year)
            .WithMessage("Kartın son kullanma yılı geçmiş olamaz.");

        RuleFor(x => x)
            .Must(x => new DateTime(x.ExpireYear, 1, 1).AddMonths(x.ExpireMonth) > DateTime.UtcNow)
            .WithMessage("Kartın son kullanma tarihi geçmiş.")
            .When(x => x.ExpireMonth is >= 1 and <= 12 && x.ExpireYear >= DateTime.UtcNow.Year);

        RuleFor(x => x.Cvv)
            .NotEmpty()
            .Matches(@"^\d{3,4}$").WithMessage("CVV 3 veya 4 haneli olmalıdır.");

        RuleFor(x => x.InstallmentCount)
            .InclusiveBetween((short)1, (short)12);

        RuleFor(x => x.OrderReference)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.TransactionType)
            .Must(t => t is TransactionType.Sale or TransactionType.PreAuth)
            .WithMessage("Yeni işlem sadece Sale veya PreAuth tipinde olabilir.");

        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .MustAsync((id, ct) => _merchantRepository.ExistsAndActiveAsync(id, ct))
            .WithMessage("Üye işyeri bulunamadı veya pasif durumda.");

        RuleFor(x => x.TerminalId).NotEmpty();
    }

    private static bool BeAValidCurrency(string currency) =>
        SupportedCurrencies.Contains(currency, StringComparer.OrdinalIgnoreCase);
}

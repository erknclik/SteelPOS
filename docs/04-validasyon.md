# SanalPOS - Validasyon Stratejisi (FluentValidation)

## 1. Genel Yaklaşım

Tüm giriş verileri (Command/Query/DTO) **FluentValidation** ile doğrulanır. Validasyon mantığı Controller'da değil, **Application katmanında** tanımlanır ve MediatR pipeline'ına bir `Behavior` olarak eklenir; böylece her Command/Query otomatik olarak validasyondan geçer.

## 2. Klasör Yapısı

```
SanalPOS.Application/
  Payments/
    Commands/
      CreatePayment/
        CreatePaymentCommand.cs
        CreatePaymentCommandHandler.cs
        CreatePaymentCommandValidator.cs
```

Kural: Her Command/Query'nin yanında, aynı klasörde `<İsim>Validator.cs` dosyası bulunur.

## 3. MediatR Pipeline Behavior

```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
            throw new SanalPosValidationException(failures);

        return await next();
    }
}
```

Kayıt (`Program.cs` / `DependencyInjection.cs`):
```csharp
services.AddValidatorsFromAssembly(typeof(CreatePaymentCommandValidator).Assembly);
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

## 4. Örnek Validator: Ödeme Oluşturma

```csharp
public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
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

        RuleFor(x => x.ExpireMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.ExpireYear).GreaterThanOrEqualTo(DateTime.UtcNow.Year);

        RuleFor(x => x.Cvv)
            .NotEmpty()
            .Matches(@"^\d{3,4}$").WithMessage("CVV 3 veya 4 haneli olmalıdır.");

        RuleFor(x => x.InstallmentCount)
            .InclusiveBetween(1, 12);

        RuleFor(x => x.MerchantId)
            .NotEmpty();

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(100);
    }

    private bool BeAValidCurrency(string currency) =>
        new[] { "TRY", "USD", "EUR" }.Contains(currency);
}
```

## 5. Asenkron / Veritabanı Erişimi Gereken Kurallar

Merchant'ın var olup olmadığı, limit aşımı gibi kontroller için `MustAsync` kullanılır ve gerekli repository/servis Validator'a constructor injection ile verilir:

```csharp
RuleFor(x => x.MerchantId)
    .MustAsync(async (id, ct) => await _merchantRepository.ExistsAsync(id, ct))
    .WithMessage("Üye işyeri bulunamadı veya pasif durumda.");
```

> **Not**: Application katmanındaki validator'lar ORM'den bağımsızdır; `IMerchantRepository` arayüzü üzerinden çalışırlar, bu sayede EF Core/NHibernate seçiminden etkilenmezler.

## 6. Hata Formatı (API Response)

Validasyon hataları global exception middleware tarafından yakalanır ve **RFC 7807 Problem Details** formatında döner:

```json
{
  "type": "https://sanalpos.com/errors/validation",
  "title": "Validasyon hatası",
  "status": 400,
  "errors": {
    "Amount": ["Tutar sıfırdan büyük olmalıdır."],
    "Cvv": ["CVV 3 veya 4 haneli olmalıdır."]
  },
  "traceId": "00-4bf9...-01"
}
```

## 7. Frontend Tarafında Doğrulama

React tarafında **Zod** şemaları, backend FluentValidation kurallarıyla birebir eşleştirilir (aynı kural setleri iki tarafta da yaşar; kritik iş kuralları asla sadece frontend'de doğrulanmaz — backend her zaman son otorite kabul edilir).

## 8. Test Stratejisi

Her validator için ayrı unit test sınıfı yazılır (`CreatePaymentCommandValidatorTests`), FluentValidation'ın kendi test extension'ı (`TestValidate`) kullanılır:

```csharp
var result = validator.TestValidate(command);
result.ShouldHaveValidationErrorFor(x => x.Amount);
```

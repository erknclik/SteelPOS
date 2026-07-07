using FluentValidation.TestHelper;
using NSubstitute;
using SanalPOS.Application.Payments.Commands.CreatePayment;
using SanalPOS.Domain.Interfaces;
using Xunit;

namespace SanalPOS.Application.UnitTests.Payments;

public class CreatePaymentCommandValidatorTests
{
    private readonly CreatePaymentCommandValidator _validator;

    public CreatePaymentCommandValidatorTests()
    {
        var merchantRepository = Substitute.For<IMerchantRepository>();
        merchantRepository.ExistsAndActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _validator = new CreatePaymentCommandValidator(merchantRepository);
    }

    private static CreatePaymentCommand ValidCommand(
        decimal amount = 100m, string currency = "TRY", string cvv = "123",
        short installments = 1, string cardNumber = "4111111111111111") => new(
        Guid.NewGuid(), Guid.NewGuid(), "SIP-001", amount, currency, installments,
        cardNumber, "AHMET YILMAZ", 12, DateTime.UtcNow.Year + 2, cvv, "idem-key-1");

    [Fact]
    public async Task ValidCommand_ShouldPass()
    {
        var result = await _validator.TestValidateAsync(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1_000_001)]
    public async Task InvalidAmount_ShouldFail(decimal amount)
    {
        var result = await _validator.TestValidateAsync(ValidCommand(amount: amount));
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public async Task UnsupportedCurrency_ShouldFail()
    {
        var result = await _validator.TestValidateAsync(ValidCommand(currency: "GBP"));
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("abc")]
    public async Task InvalidCvv_ShouldFail(string cvv)
    {
        var result = await _validator.TestValidateAsync(ValidCommand(cvv: cvv));
        result.ShouldHaveValidationErrorFor(x => x.Cvv);
    }

    [Fact]
    public async Task InvalidCardNumber_ShouldFail()
    {
        var result = await _validator.TestValidateAsync(ValidCommand(cardNumber: "1234567890123456"));
        result.ShouldHaveValidationErrorFor(x => x.CardNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task InvalidInstallmentCount_ShouldFail(short installments)
    {
        var result = await _validator.TestValidateAsync(ValidCommand(installments: installments));
        result.ShouldHaveValidationErrorFor(x => x.InstallmentCount);
    }

    [Fact]
    public async Task InactiveMerchant_ShouldFail()
    {
        var repository = Substitute.For<IMerchantRepository>();
        repository.ExistsAndActiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        var validator = new CreatePaymentCommandValidator(repository);

        var result = await validator.TestValidateAsync(ValidCommand());
        result.ShouldHaveValidationErrorFor(x => x.MerchantId);
    }
}

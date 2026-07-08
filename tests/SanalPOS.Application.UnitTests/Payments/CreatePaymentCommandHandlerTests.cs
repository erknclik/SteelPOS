using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Application.Payments.Commands.CreatePayment;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Interfaces;
using SanalPOS.Domain.ValueObjects;
using Xunit;

namespace SanalPOS.Application.UnitTests.Payments;

public class CreatePaymentCommandHandlerTests
{
    private readonly IPaymentTransactionRepository _transactionRepository = Substitute.For<IPaymentTransactionRepository>();
    private readonly IMerchantRepository _merchantRepository = Substitute.For<IMerchantRepository>();
    private readonly ITerminalRepository _terminalRepository = Substitute.For<ITerminalRepository>();
    private readonly IBankAdapterFactory _bankAdapterFactory = Substitute.For<IBankAdapterFactory>();
    private readonly IBankProviderAdapter _bankAdapter = Substitute.For<IBankProviderAdapter>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly Merchant _merchant;
    private readonly Terminal _terminal;
    private readonly CreatePaymentCommandHandler _handler;

    public CreatePaymentCommandHandlerTests()
    {
        _merchant = new Merchant("Demo", "1234567890", new Iban("TR330006100519786457841326"), 2.5m);
        var store = new Store(_merchant.Id, "Merkez", null);
        _terminal = new Terminal(store.Id, "TERM-01", "MOCKBANK");

        _cache.SetIfNotExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _merchantRepository.GetWithCommissionRulesAsync(_merchant.Id, Arg.Any<CancellationToken>()).Returns(_merchant);
        _terminalRepository.GetByIdAsync(_terminal.Id, Arg.Any<CancellationToken>()).Returns(_terminal);
        _bankAdapterFactory.Resolve("MOCKBANK").Returns(_bankAdapter);
        _currentUser.UserName.Returns("test-user");

        _handler = new CreatePaymentCommandHandler(
            _transactionRepository, _merchantRepository, _terminalRepository, _bankAdapterFactory,
            _cache, _unitOfWork, _currentUser, NullLogger<CreatePaymentCommandHandler>.Instance);
    }

    private CreatePaymentCommand ValidCommand() => new(
        _merchant.Id, _terminal.Id, "SIP-001", 1250.50m, "TRY", 1,
        "4111111111111111", "AHMET YILMAZ", 12, DateTime.UtcNow.Year + 2, "123",
        Guid.NewGuid().ToString());

    [Fact]
    public async Task Handle_BankApproves_ShouldReturnApprovedWithCommission()
    {
        _bankAdapter.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChargeResult(true, "482910", null, null));

        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        result.Status.Should().Be(TransactionStatus.Approved.ToString());
        result.BankAuthCode.Should().Be("482910");
        result.CommissionAmount.Should().Be(31.26m);
        result.NetAmount.Should().Be(1219.24m);
        await _transactionRepository.Received(1).AddAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BankDeclines_ShouldReturnDeclined()
    {
        _bankAdapter.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChargeResult(false, null, "05", "Yetersiz bakiye"));

        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

        result.Status.Should().Be(TransactionStatus.Declined.ToString());
        result.BankAuthCode.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DuplicateIdempotencyKey_ShouldReturnExistingTransaction()
    {
        var command = ValidCommand();
        var existing = new PaymentTransaction(
            _merchant.Id, _terminal.Id, "SIP-001", new Money(1250.50m, "TRY"), 1,
            TransactionType.Sale, MaskedCardNumber.FromPan("4111111111111111"),
            "AHMET YILMAZ", "MOCKBANK", command.IdempotencyKey);
        existing.Approve("482910", 2.5m, "test-user");

        _cache.SetIfNotExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _transactionRepository.GetByIdempotencyKeyAsync(command.IdempotencyKey, Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.TransactionId.Should().Be(existing.Id);
        await _bankAdapter.DidNotReceive().ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>());
        await _transactionRepository.DidNotReceive().AddAsync(Arg.Any<PaymentTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PreAuth_ShouldCallPreAuthOnAdapter()
    {
        _bankAdapter.PreAuthAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ChargeResult(true, "111111", null, null));

        var command = ValidCommand() with { TransactionType = TransactionType.PreAuth };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(TransactionStatus.Approved.ToString());
        await _bankAdapter.Received(1).PreAuthAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>());
        await _bankAdapter.DidNotReceive().ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>());
    }
}

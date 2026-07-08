using FluentAssertions;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Events;
using SanalPOS.Domain.Exceptions;
using SanalPOS.Domain.ValueObjects;
using Xunit;

namespace SanalPOS.Domain.UnitTests.Entities;

public class PaymentTransactionTests
{
    private static PaymentTransaction CreatePendingTransaction(decimal amount = 100m) => new(
        Guid.NewGuid(), Guid.NewGuid(), "SIP-001",
        new Money(amount, "TRY"), 1, TransactionType.Sale,
        MaskedCardNumber.FromPan("4021220000001234"), "AHMET YILMAZ",
        "MOCKBANK", Guid.NewGuid().ToString());

    [Fact]
    public void Approve_ShouldComputeCommissionAndRaiseEvent()
    {
        var tx = CreatePendingTransaction(1250.50m);

        tx.Approve("482910", 2.5m, "test");

        tx.Status.Should().Be(TransactionStatus.Approved);
        tx.CommissionAmount.Should().Be(31.26m);
        tx.NetAmount.Should().Be(1219.24m);
        tx.BankAuthCode.Should().Be("482910");
        tx.CompletedAt.Should().NotBeNull();
        tx.DomainEvents.Should().ContainSingle(e => e is PaymentCompletedDomainEvent);
        tx.StatusHistory.Should().ContainSingle(h =>
            h.OldStatus == TransactionStatus.Pending && h.NewStatus == TransactionStatus.Approved);
    }

    [Fact]
    public void Approve_AlreadyApproved_ShouldThrow()
    {
        var tx = CreatePendingTransaction();
        tx.Approve("123456", 2m, "test");

        var act = () => tx.Approve("123456", 2m, "test");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Decline_ShouldRaisePaymentFailedEvent()
    {
        var tx = CreatePendingTransaction();

        tx.Decline("05", "Yetersiz bakiye", "test");

        tx.Status.Should().Be(TransactionStatus.Declined);
        tx.DomainEvents.Should().ContainSingle(e => e is PaymentFailedDomainEvent);
    }

    [Fact]
    public void Void_SameDayApproved_ShouldReverse()
    {
        var tx = CreatePendingTransaction();
        tx.Approve("123456", 2m, "test");

        tx.Void("test");

        tx.Status.Should().Be(TransactionStatus.Reversed);
    }

    [Fact]
    public void ApplyRefund_PartialThenFull_ShouldTransitionStatuses()
    {
        var tx = CreatePendingTransaction(100m);
        tx.Approve("123456", 2m, "test");

        tx.ApplyRefund(new RefundTransaction(tx.Id, 40m, null), "test");
        tx.Status.Should().Be(TransactionStatus.PartiallyRefunded);

        tx.ApplyRefund(new RefundTransaction(tx.Id, 60m, null), "test");
        tx.Status.Should().Be(TransactionStatus.Refunded);
    }

    [Fact]
    public void ApplyRefund_ExceedingAmount_ShouldThrow()
    {
        var tx = CreatePendingTransaction(100m);
        tx.Approve("123456", 2m, "test");

        var act = () => tx.ApplyRefund(new RefundTransaction(tx.Id, 150m, null), "test");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Capture_OnSaleTransaction_ShouldThrow()
    {
        var tx = CreatePendingTransaction();
        tx.Approve("123456", 2m, "test");

        var act = () => tx.Capture("test");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Constructor_InvalidInstallment_ShouldThrow()
    {
        var act = () => new PaymentTransaction(
            Guid.NewGuid(), Guid.NewGuid(), "SIP-001",
            new Money(100, "TRY"), 13, TransactionType.Sale,
            MaskedCardNumber.FromPan("4021220000001234"), "TEST",
            "MOCKBANK", "key");
        act.Should().Throw<DomainException>();
    }
}

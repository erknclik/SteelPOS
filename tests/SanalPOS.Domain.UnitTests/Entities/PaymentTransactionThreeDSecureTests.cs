using FluentAssertions;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Events;
using SanalPOS.Domain.Exceptions;
using SanalPOS.Domain.ValueObjects;
using Xunit;

namespace SanalPOS.Domain.UnitTests.Entities;

public class PaymentTransactionThreeDSecureTests
{
    private static PaymentTransaction CreatePendingTransaction() => new(
        Guid.NewGuid(), Guid.NewGuid(), "SIP-3DS-001",
        new Money(100m, "TRY"), 1, TransactionType.Sale,
        MaskedCardNumber.FromPan("4021220000001234"), "AHMET YILMAZ",
        "MOCKBANK", Guid.NewGuid().ToString());

    [Fact]
    public void StartThreeDSecure_FromPending_ShouldTransitionAndRecordHistory()
    {
        var tx = CreatePendingTransaction();

        tx.StartThreeDSecure("test");

        tx.Status.Should().Be(TransactionStatus.Pending3DS);
        tx.StatusHistory.Should().ContainSingle(h =>
            h.OldStatus == TransactionStatus.Pending && h.NewStatus == TransactionStatus.Pending3DS);
    }

    [Fact]
    public void StartThreeDSecure_WhenAlreadyPending3DS_ShouldThrow()
    {
        var tx = CreatePendingTransaction();
        tx.StartThreeDSecure("test");

        var act = () => tx.StartThreeDSecure("test");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Approve_FromPending3DS_ShouldSucceed()
    {
        var tx = CreatePendingTransaction();
        tx.StartThreeDSecure("test");

        tx.Approve("482910", 2.5m, "test");

        tx.Status.Should().Be(TransactionStatus.Approved);
        tx.DomainEvents.Should().ContainSingle(e => e is PaymentCompletedDomainEvent);
        tx.StatusHistory.Should().ContainSingle(h =>
            h.OldStatus == TransactionStatus.Pending3DS && h.NewStatus == TransactionStatus.Approved);
    }

    [Fact]
    public void Decline_FromPending3DS_ShouldSucceed()
    {
        var tx = CreatePendingTransaction();
        tx.StartThreeDSecure("test");

        tx.Decline("3DS-FAIL", "Kart hamili doğrulaması başarısız.", "test");

        tx.Status.Should().Be(TransactionStatus.Declined);
        tx.DomainEvents.Should().ContainSingle(e => e is PaymentFailedDomainEvent);
    }

    [Fact]
    public void StartThreeDSecure_FromApproved_ShouldThrow()
    {
        var tx = CreatePendingTransaction();
        tx.Approve("482910", 2.5m, "test");

        var act = () => tx.StartThreeDSecure("test");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Void_FromPending3DS_ShouldThrow()
    {
        var tx = CreatePendingTransaction();
        tx.StartThreeDSecure("test");

        var act = () => tx.Void("test");

        act.Should().Throw<DomainException>();
    }
}

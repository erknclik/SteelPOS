using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using SanalPOS.Application.Common.Events;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Contracts;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Events;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Payments.EventHandlers;

/// <summary>
/// Domain event'leri iki işe çevirir: (1) append-only audit kaydı, (2) mesaj kuyruğuna
/// integration event publish (bildirim/webhook/muhasebe tüketicileri için).
/// </summary>
public class PaymentCompletedDomainEventHandler : INotificationHandler<DomainEventNotification<PaymentCompletedDomainEvent>>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ICorrelationIdAccessor _correlationId;
    private readonly ILogger<PaymentCompletedDomainEventHandler> _logger;

    public PaymentCompletedDomainEventHandler(
        IAuditLogRepository auditLogRepository,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ICorrelationIdAccessor correlationId,
        ILogger<PaymentCompletedDomainEventHandler> logger)
    {
        _auditLogRepository = auditLogRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _correlationId = correlationId;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<PaymentCompletedDomainEvent> notification, CancellationToken ct)
    {
        var tx = notification.DomainEvent.Transaction;

        await _auditLogRepository.AddAsync(AuditLogFactory.ForTransaction(tx, AuditAction.StatusChange, _currentUser.UserName), ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(new PaymentCompletedEvent(
            tx.Id, tx.MerchantId, tx.Amount.Amount, tx.Amount.Currency,
            tx.CompletedAt ?? DateTime.UtcNow, _correlationId.CorrelationId), ct);

        _logger.LogInformation("PaymentCompletedEvent yayınlandı. TransactionId: {TransactionId}", tx.Id);
    }
}

public class PaymentFailedDomainEventHandler : INotificationHandler<DomainEventNotification<PaymentFailedDomainEvent>>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ICorrelationIdAccessor _correlationId;

    public PaymentFailedDomainEventHandler(
        IAuditLogRepository auditLogRepository,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ICorrelationIdAccessor correlationId)
    {
        _auditLogRepository = auditLogRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _correlationId = correlationId;
    }

    public async Task Handle(DomainEventNotification<PaymentFailedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;

        await _auditLogRepository.AddAsync(AuditLogFactory.ForTransaction(e.Transaction, AuditAction.StatusChange, _currentUser.UserName), ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(new PaymentFailedEvent(
            e.Transaction.Id, e.Transaction.MerchantId, e.ReasonCode, e.ReasonMessage,
            e.OccurredAtUtc, _correlationId.CorrelationId), ct);
    }
}

public class RefundCompletedDomainEventHandler : INotificationHandler<DomainEventNotification<RefundCompletedDomainEvent>>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ICorrelationIdAccessor _correlationId;

    public RefundCompletedDomainEventHandler(
        IAuditLogRepository auditLogRepository,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ICorrelationIdAccessor correlationId)
    {
        _auditLogRepository = auditLogRepository;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _correlationId = correlationId;
    }

    public async Task Handle(DomainEventNotification<RefundCompletedDomainEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;

        await _auditLogRepository.AddAsync(new AuditLog(
            nameof(RefundTransaction), e.Refund.Id, AuditAction.Create, _currentUser.UserName,
            JsonSerializer.Serialize(new
            {
                e.Refund.OriginalTransactionId,
                e.Refund.RefundAmount,
                Status = e.Refund.Status.ToString()
            })), ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(new RefundCompletedEvent(
            e.Refund.Id, e.Original.Id, e.Refund.RefundAmount,
            e.OccurredAtUtc, _correlationId.CorrelationId), ct);
    }
}

internal static class AuditLogFactory
{
    /// <summary>Denetim snapshot'ı — sadece maskeli kart bilgisi yazılır, hassas veri içermez.</summary>
    public static AuditLog ForTransaction(PaymentTransaction tx, AuditAction action, string performedBy) => new(
        nameof(PaymentTransaction), tx.Id, action, performedBy,
        JsonSerializer.Serialize(new
        {
            tx.MerchantId,
            tx.TerminalId,
            tx.OrderReference,
            Amount = tx.Amount.Amount,
            tx.Amount.Currency,
            Status = tx.Status.ToString(),
            Type = tx.TransactionType.ToString(),
            MaskedCard = tx.MaskedCard.Value,
            tx.BankAuthCode,
            tx.CommissionAmount,
            tx.NetAmount
        }));
}

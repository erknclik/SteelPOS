using System.Text.Json;
using MediatR;
using SanalPOS.Application.Common.Events;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Contracts;
using SanalPOS.Domain.Entities;
using SanalPOS.Domain.Enums;
using SanalPOS.Domain.Events;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Application.Merchants.EventHandlers;

public class MerchantSuspendedDomainEventHandler : INotificationHandler<DomainEventNotification<MerchantSuspendedDomainEvent>>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ICorrelationIdAccessor _correlationId;

    public MerchantSuspendedDomainEventHandler(
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

    public async Task Handle(DomainEventNotification<MerchantSuspendedDomainEvent> notification, CancellationToken ct)
    {
        var merchant = notification.DomainEvent.Merchant;

        await _auditLogRepository.AddAsync(new AuditLog(
            nameof(Merchant), merchant.Id, AuditAction.StatusChange, _currentUser.UserName,
            JsonSerializer.Serialize(new { merchant.Name, Status = merchant.Status.ToString() })), ct);
        await _unitOfWork.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(new MerchantSuspendedEvent(
            merchant.Id, notification.DomainEvent.OccurredAtUtc, _correlationId.CorrelationId), ct);
    }
}

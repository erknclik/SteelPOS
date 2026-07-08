using MediatR;
using SanalPOS.Domain.Common;

namespace SanalPOS.Application.Common.Events;

/// <summary>
/// Domain event'leri (framework bağımsız IDomainEvent) MediatR pipeline'ına taşıyan sarmalayıcı.
/// UnitOfWork implementasyonları SaveChanges sonrasında bu notification'ı publish eder.
/// </summary>
public sealed class DomainEventNotification<TDomainEvent> : INotification
    where TDomainEvent : IDomainEvent
{
    public TDomainEvent DomainEvent { get; }

    public DomainEventNotification(TDomainEvent domainEvent) => DomainEvent = domainEvent;
}

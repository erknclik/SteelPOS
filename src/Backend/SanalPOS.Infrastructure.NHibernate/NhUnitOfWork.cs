using MediatR;
using NHibernate;
using NHibernate.Engine;
using SanalPOS.Application.Common.Events;
using SanalPOS.Domain.Common;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Infrastructure.NHibernate;

/// <summary>
/// NHibernate tabanlı UnitOfWork. Flush + commit sonrasında domain event'leri dispatch eder;
/// EF Core UnitOfWork ile aynı davranış sözleşmesine sahiptir.
/// </summary>
public class NhUnitOfWork : IUnitOfWork
{
    private readonly ISession _session;
    private readonly IPublisher _publisher;

    public NhUnitOfWork(ISession session, IPublisher publisher)
    {
        _session = session;
        _publisher = publisher;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var domainEvents = CollectDomainEvents();

        var transaction = _session.GetCurrentTransaction();
        if (transaction is { IsActive: true })
        {
            await _session.FlushAsync(ct);
            await transaction.CommitAsync(ct);
        }
        else
        {
            using var tx = _session.BeginTransaction();
            await _session.FlushAsync(ct);
            await tx.CommitAsync(ct);
        }

        foreach (var domainEvent in domainEvents)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = Activator.CreateInstance(notificationType, domainEvent)!;
            await _publisher.Publish(notification, ct);
        }

        return domainEvents.Count;
    }

    private List<IDomainEvent> CollectDomainEvents()
    {
        var persistenceContext = _session.GetSessionImplementation().PersistenceContext;
        var entities = persistenceContext.EntityEntries.Keys
            .OfType<BaseEntity>()
            .Where(e => e.DomainEvents.Count != 0)
            .ToList();

        var events = entities.SelectMany(e => e.DomainEvents).ToList();
        entities.ForEach(e => e.ClearDomainEvents());
        return events;
    }
}

using MediatR;
using SanalPOS.Application.Common.Events;
using SanalPOS.Domain.Common;
using SanalPOS.Domain.Interfaces;

namespace SanalPOS.Infrastructure.EfCore;

/// <summary>
/// SaveChanges sonrasında biriken domain event'leri MediatR üzerinden dispatch eder.
/// Event handler'lar audit yazımı ve integration event publish işlerini üstlenir.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly SanalPosDbContext _context;
    private readonly IPublisher _publisher;

    public UnitOfWork(SanalPosDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var domainEvents = CollectDomainEvents();
        var result = await _context.SaveChangesAsync(ct);

        foreach (var domainEvent in domainEvents)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = Activator.CreateInstance(notificationType, domainEvent)!;
            await _publisher.Publish(notification, ct);
        }

        return result;
    }

    private List<IDomainEvent> CollectDomainEvents()
    {
        var entities = _context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        var events = entities.SelectMany(e => e.DomainEvents).ToList();
        entities.ForEach(e => e.ClearDomainEvents());
        return events;
    }
}

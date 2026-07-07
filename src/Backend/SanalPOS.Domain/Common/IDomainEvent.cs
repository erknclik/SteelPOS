namespace SanalPOS.Domain.Common;

/// <summary>
/// Domain event işaretleyici arayüzü. Domain katmanı framework bağımsız kalsın diye
/// MediatR.INotification yerine bu marker kullanılır; Application katmanında
/// DomainEventNotification&lt;T&gt; ile sarmalanarak dispatch edilir.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}

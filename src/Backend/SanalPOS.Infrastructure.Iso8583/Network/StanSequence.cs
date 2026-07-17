using System.Collections.Concurrent;

namespace SanalPOS.Infrastructure.Iso8583.Network;

/// <summary>
/// System Trace Audit Number (DE11) üreteci. STAN, kanal üzerindeki istek/yanıt
/// eşleştirmesinin anahtarıdır; banka başına tekil olmalıdır. Üretim (çok instance)
/// için Redis tabanlı implementasyon kullanılır (Iso8583:StanSequence konfigürasyonu).
/// </summary>
public interface IStanSequence
{
    /// <summary>Sıradaki STAN'i 6 haneli, sola sıfır dolgulu döndürür (000001-999999, döngüsel).</summary>
    ValueTask<string> NextAsync(CancellationToken ct = default);
}

/// <summary>Banka (ProviderCode) başına bir STAN sayacı üretir.</summary>
public interface IStanSequenceFactory
{
    IStanSequence Create(string providerCode);
}

/// <summary>
/// Süreç içi döngüsel sayaç. Başlangıç değeri saatten türetilir ki uygulama yeniden
/// başladığında kısa süre önce kullanılmış STAN'lerle çakışma olasılığı düşsün.
/// Tek instance/dev-test içindir; çok instance'lı üretimde Redis tabanlı sayaç kullanılmalıdır.
/// </summary>
public sealed class InMemoryStanSequence : IStanSequence
{
    private int _current;

    public InMemoryStanSequence()
    {
        var now = DateTime.UtcNow;
        _current = (int)(now.TimeOfDay.TotalSeconds * 10) % 999_999;
    }

    public ValueTask<string> NextAsync(CancellationToken ct = default)
    {
        int next;
        int original;
        do
        {
            original = _current;
            next = original >= 999_999 ? 1 : original + 1;
        } while (Interlocked.CompareExchange(ref _current, next, original) != original);

        return ValueTask.FromResult(next.ToString("D6"));
    }
}

public sealed class InMemoryStanSequenceFactory : IStanSequenceFactory
{
    private readonly ConcurrentDictionary<string, InMemoryStanSequence> _sequences = new(StringComparer.OrdinalIgnoreCase);

    public IStanSequence Create(string providerCode) =>
        _sequences.GetOrAdd(providerCode, _ => new InMemoryStanSequence());
}

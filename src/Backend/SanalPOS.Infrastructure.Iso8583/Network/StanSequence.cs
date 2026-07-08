namespace SanalPOS.Infrastructure.Iso8583.Network;

/// <summary>
/// System Trace Audit Number (DE11) üreteci. STAN, kanal üzerindeki istek/yanıt
/// eşleştirmesinin anahtarıdır; banka başına tekil olmalıdır.
/// </summary>
public interface IStanSequence
{
    /// <summary>Sıradaki STAN'i 6 haneli, sola sıfır dolgulu döndürür (000001-999999, döngüsel).</summary>
    string Next();
}

/// <summary>
/// Süreç içi döngüsel sayaç. Başlangıç değeri saatten türetilir ki uygulama yeniden
/// başladığında kısa süre önce kullanılmış STAN'lerle çakışma olasılığı düşsün.
/// (Not: çok instance'lı üretim senaryosunda banka başına merkezî — örn. Redis — sayaç kullanılmalıdır.)
/// </summary>
public sealed class InMemoryStanSequence : IStanSequence
{
    private int _current;

    public InMemoryStanSequence()
    {
        var now = DateTime.UtcNow;
        _current = (int)(now.TimeOfDay.TotalSeconds * 10) % 999_999;
    }

    public string Next()
    {
        int next;
        int original;
        do
        {
            original = _current;
            next = original >= 999_999 ? 1 : original + 1;
        } while (Interlocked.CompareExchange(ref _current, next, original) != original);

        return next.ToString("D6");
    }
}

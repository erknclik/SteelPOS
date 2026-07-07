using SanalPOS.Domain.Exceptions;

namespace SanalPOS.Domain.ValueObjects;

/// <summary>
/// Maskeli kart numarası. Tam PAN hiçbir zaman saklanmaz; sadece ilk 6 + son 4 hane tutulur.
/// </summary>
public sealed class MaskedCardNumber : IEquatable<MaskedCardNumber>
{
    public string Value { get; private set; } = string.Empty;

    private MaskedCardNumber()
    {
        // ORM'ler için
    }

    private MaskedCardNumber(string value) => Value = value;

    /// <summary>Tam PAN'dan maskeli değer üretir; PAN bu metottan sonra saklanmaz.</summary>
    public static MaskedCardNumber FromPan(string pan)
    {
        var digits = new string((pan ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length is < 12 or > 19)
            throw new DomainException("Geçersiz kart numarası uzunluğu.");

        var masked = digits[..6] + new string('*', digits.Length - 10) + digits[^4..];
        return new MaskedCardNumber(Format(masked));
    }

    /// <summary>Zaten maskelenmiş bir değeri (ör. veritabanından) sarmalar.</summary>
    public static MaskedCardNumber FromMasked(string masked) => new(masked);

    private static string Format(string raw) =>
        string.Join(' ', Enumerable.Range(0, (raw.Length + 3) / 4)
            .Select(i => raw.Substring(i * 4, Math.Min(4, raw.Length - i * 4))));

    public bool Equals(MaskedCardNumber? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as MaskedCardNumber);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
}

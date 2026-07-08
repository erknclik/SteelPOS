using SanalPOS.Domain.Exceptions;

namespace SanalPOS.Domain.ValueObjects;

public sealed class Iban : IEquatable<Iban>
{
    public string Value { get; private set; } = string.Empty;

    private Iban()
    {
        // ORM'ler için
    }

    public Iban(string value)
    {
        var normalized = (value ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        if (normalized.Length is < 15 or > 34 || !normalized[..2].All(char.IsLetter))
            throw new DomainException("Geçersiz IBAN formatı.");
        Value = normalized;
    }

    public bool Equals(Iban? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as Iban);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
}

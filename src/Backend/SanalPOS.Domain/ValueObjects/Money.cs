using SanalPOS.Domain.Exceptions;

namespace SanalPOS.Domain.ValueObjects;

/// <summary>Tutar + ISO 4217 para birimi value object'i.</summary>
public sealed class Money : IEquatable<Money>
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "TRY";

    private Money()
    {
        // ORM'ler (EF Core OwnsOne / NHibernate Component) için
    }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new DomainException("Tutar negatif olamaz.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("Para birimi ISO 4217 formatında 3 karakter olmalıdır.");

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Try(decimal amount) => new(amount, "TRY");

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        if (other.Amount > Amount)
            throw new DomainException("Sonuç tutarı negatif olamaz.");
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Percentage(decimal rate) => new(Amount * rate / 100m, Currency);

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException($"Para birimleri uyuşmuyor: {Currency} / {other.Currency}");
    }

    public bool Equals(Money? other) =>
        other is not null && Amount == other.Amount && Currency == other.Currency;

    public override bool Equals(object? obj) => Equals(obj as Money);
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);
    public override string ToString() => $"{Amount:0.00} {Currency}";
}

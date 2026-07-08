namespace SanalPOS.Infrastructure.Iso8583.Messages;

/// <summary>
/// Kodlamadan bağımsız ISO 8583 mesajı: MTI + data element sözlüğü.
/// Değerler her zaman string taşınır; binary alanlar hex string'tir.
/// Tel formatına çeviri Iso8583Serializer + Iso8583Spec (dialekt) ile yapılır.
/// </summary>
public sealed class Iso8583Message
{
    private readonly SortedDictionary<int, string> _fields = new();

    public Iso8583Message(string mti)
    {
        if (mti.Length != 4 || !mti.All(char.IsAsciiDigit))
            throw new ArgumentException($"MTI 4 rakamdan oluşmalı: '{mti}'.", nameof(mti));

        Mti = mti;
    }

    public string Mti { get; }

    /// <summary>DE numarasına göre değer atar/okur. Değer null atanırsa alan silinir.</summary>
    public string? this[int number]
    {
        get => _fields.TryGetValue(number, out var value) ? value : null;
        set
        {
            ValidateNumber(number);
            if (value is null)
                _fields.Remove(number);
            else
                _fields[number] = value;
        }
    }

    public bool Has(int number) => _fields.ContainsKey(number);

    /// <summary>Alanı okur; yoksa Iso8583Exception fırlatır (zorunlu yanıt alanları için).</summary>
    public string GetRequired(int number) =>
        _fields.TryGetValue(number, out var value)
            ? value
            : throw new Spec.Iso8583Exception($"Mesajda (MTI {Mti}) zorunlu DE{number} alanı yok.");

    public IReadOnlyDictionary<int, string> Fields => _fields;

    /// <summary>Aynı sınıf içinde istek MTI'sinden yanıt MTI'sini türetir (0200 -> 0210, 0400 -> 0410).</summary>
    public static string ResponseMtiOf(string requestMti)
    {
        var digits = requestMti.ToCharArray();
        digits[2] = (char)(digits[2] + 1);
        return new string(digits);
    }

    private static void ValidateNumber(int number)
    {
        if (number is < 2 or > 128)
            throw new ArgumentOutOfRangeException(nameof(number), number, "Data element numarası 2-128 aralığında olmalı.");
    }
}

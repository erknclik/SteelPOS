namespace SanalPOS.Infrastructure.Iso8583.Spec;

/// <summary>MTI'nin tel üzerindeki kodlaması.</summary>
public enum MtiFormat
{
    /// <summary>4 ASCII karakter.</summary>
    Ascii,

    /// <summary>2 byte packed BCD.</summary>
    Bcd
}

/// <summary>Bitmap'in tel üzerindeki kodlaması.</summary>
public enum BitmapFormat
{
    /// <summary>8 (veya 16) ham byte.</summary>
    Binary,

    /// <summary>16 (veya 32) hex ASCII karakter.</summary>
    HexAscii
}

/// <summary>Değişken alanların uzunluk başlığı kodlaması.</summary>
public enum LengthHeaderEncoding
{
    /// <summary>LLVAR=2, LLLVAR=3 ASCII rakam.</summary>
    Ascii,

    /// <summary>LLVAR=1, LLLVAR=2 byte packed BCD.</summary>
    Bcd
}

/// <summary>
/// Bir bankanın ISO 8583 "dialekt"i: MTI/bitmap/uzunluk başlığı kodlamaları ve alan tablosu.
/// Her banka aynı standardı farklı yorumlar; yeni banka eklemek = yeni Iso8583Spec tanımlamak.
/// </summary>
public sealed class Iso8583Spec
{
    private readonly IReadOnlyDictionary<int, FieldSpec> _fields;

    public Iso8583Spec(
        string name,
        MtiFormat mtiFormat,
        BitmapFormat bitmapFormat,
        LengthHeaderEncoding lengthHeaderEncoding,
        IEnumerable<FieldSpec> fields)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dialekt adı boş olamaz.", nameof(name));

        Name = name;
        MtiFormat = mtiFormat;
        BitmapFormat = bitmapFormat;
        LengthHeaderEncoding = lengthHeaderEncoding;

        var table = new Dictionary<int, FieldSpec>();
        foreach (var field in fields)
        {
            if (!table.TryAdd(field.Validate().Number, field))
                throw new ArgumentException($"DE{field.Number} dialektte birden fazla kez tanımlanmış.", nameof(fields));
        }

        _fields = table;
    }

    public string Name { get; }
    public MtiFormat MtiFormat { get; }
    public BitmapFormat BitmapFormat { get; }
    public LengthHeaderEncoding LengthHeaderEncoding { get; }

    public FieldSpec GetField(int number) =>
        _fields.TryGetValue(number, out var spec)
            ? spec
            : throw new Iso8583Exception($"'{Name}' dialektinde DE{number} tanımlı değil.");

    public bool HasField(int number) => _fields.ContainsKey(number);

    /// <summary>Mevcut dialekti temel alıp alanları değiştirilmiş yeni bir dialekt üretir.</summary>
    public Iso8583Spec WithOverrides(string name, params FieldSpec[] overrides)
    {
        var merged = _fields.Values.ToDictionary(f => f.Number);
        foreach (var field in overrides)
            merged[field.Number] = field.Validate();

        return new Iso8583Spec(name, MtiFormat, BitmapFormat, LengthHeaderEncoding, merged.Values);
    }
}

/// <summary>ISO 8583 encode/decode ve kanal hatalarının ortak tipi.</summary>
public class Iso8583Exception : Exception
{
    public Iso8583Exception(string message) : base(message)
    {
    }

    public Iso8583Exception(string message, Exception innerException) : base(message, innerException)
    {
    }
}

namespace SanalPOS.Infrastructure.Iso8583.Spec;

/// <summary>Alan uzunluk türü: sabit veya değişken (LLVAR/LLLVAR ISO 8583 gösterimi).</summary>
public enum Iso8583LengthKind
{
    /// <summary>Sabit uzunluk; değer spec'teki uzunluğa pad edilir.</summary>
    Fixed,

    /// <summary>2 haneli uzunluk başlığı (max 99).</summary>
    LLVar,

    /// <summary>3 haneli uzunluk başlığı (max 999).</summary>
    LLLVar
}

/// <summary>Alan içeriği; encode öncesi validasyon ve pad kurallarını belirler.</summary>
public enum Iso8583Content
{
    /// <summary>Sadece rakam (n). Sabit uzunlukta sola '0' ile pad edilir.</summary>
    Numeric,

    /// <summary>Harf/rakam/özel karakter (an, ans). Sabit uzunlukta sağa boşluk ile pad edilir.</summary>
    AlphaNumeric,

    /// <summary>Track-2 verisi (z): rakamlar ve 'D'/'=' ayracı.</summary>
    Track2,

    /// <summary>Ham byte içerik (b); değer hex string olarak taşınır.</summary>
    Binary
}

/// <summary>Alan gövdesinin tel üzerindeki kodlaması.</summary>
public enum Iso8583BodyEncoding
{
    /// <summary>Karakter başına 1 byte ASCII.</summary>
    Ascii,

    /// <summary>Packed BCD: 2 rakam = 1 byte; tek haneli uzunluklar sola '0' nibble ile pad edilir.</summary>
    Bcd,

    /// <summary>Ham byte; mesaj modelindeki değer hex string'tir.</summary>
    Binary
}

/// <summary>
/// Tek bir data element'in (DE) dialekt tanımı. Uzunluk, değişken alanlarda karakter
/// (binary alanlarda byte) cinsinden maksimum, sabit alanlarda kesin uzunluktur.
/// </summary>
public sealed record FieldSpec(
    int Number,
    string Name,
    Iso8583Content Content,
    Iso8583LengthKind LengthKind,
    int Length,
    Iso8583BodyEncoding Encoding,
    bool Sensitive = false)
{
    public FieldSpec Validate()
    {
        if (Number is < 2 or > 128)
            throw new ArgumentOutOfRangeException(nameof(Number), Number, "Data element numarası 2-128 aralığında olmalı (bit 1 bitmap'e ayrılmıştır).");
        if (Length <= 0)
            throw new ArgumentOutOfRangeException(nameof(Length), Length, $"DE{Number} için uzunluk pozitif olmalı.");
        if (LengthKind == Iso8583LengthKind.LLVar && Length > 99)
            throw new ArgumentOutOfRangeException(nameof(Length), Length, $"DE{Number}: LLVAR alan en fazla 99 olabilir.");
        if (LengthKind == Iso8583LengthKind.LLLVar && Length > 999)
            throw new ArgumentOutOfRangeException(nameof(Length), Length, $"DE{Number}: LLLVAR alan en fazla 999 olabilir.");
        if (Content == Iso8583Content.Binary && Encoding != Iso8583BodyEncoding.Binary)
            throw new ArgumentException($"DE{Number}: Binary içerik yalnızca Binary encoding ile kullanılabilir.");
        return this;
    }
}

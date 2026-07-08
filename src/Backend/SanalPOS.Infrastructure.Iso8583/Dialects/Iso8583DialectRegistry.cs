using SanalPOS.Infrastructure.Iso8583.Spec;

namespace SanalPOS.Infrastructure.Iso8583.Dialects;

/// <summary>
/// Dialekt adı -> Iso8583Spec çözümlemesi. Varsayılan olarak standart ISO 8583:1987
/// ASCII ve BCD dialektleri kayıtlıdır; banka özel dialektleri Register ile eklenir
/// (örn. mevcut bir dialektin WithOverrides çıktısı).
/// </summary>
public interface IIso8583DialectRegistry
{
    Iso8583Spec Get(string name);
    void Register(Iso8583Spec spec);
}

public sealed class Iso8583DialectRegistry : IIso8583DialectRegistry
{
    private readonly Dictionary<string, Iso8583Spec> _specs = new(StringComparer.OrdinalIgnoreCase);

    public Iso8583DialectRegistry()
    {
        Register(Iso8583Dialects.Iso87Ascii);
        Register(Iso8583Dialects.Iso87Bcd);
    }

    public Iso8583Spec Get(string name) =>
        _specs.TryGetValue(name, out var spec)
            ? spec
            : throw new InvalidOperationException(
                $"'{name}' adında ISO 8583 dialekti kayıtlı değil. Kayıtlı dialektler: {string.Join(", ", _specs.Keys)}");

    public void Register(Iso8583Spec spec)
    {
        _specs[spec.Name] = spec;
    }
}

/// <summary>Hazır gelen standart dialektler.</summary>
public static class Iso8583Dialects
{
    public const string Iso87AsciiName = "Iso87Ascii";
    public const string Iso87BcdName = "Iso87Bcd";

    /// <summary>ISO 8583:1987, tamamı ASCII (MTI, bitmap hex-ASCII, alanlar ASCII). Test/simülatör dostu.</summary>
    public static Iso8583Spec Iso87Ascii { get; } = new(
        Iso87AsciiName,
        MtiFormat.Ascii,
        BitmapFormat.HexAscii,
        LengthHeaderEncoding.Ascii,
        StandardFields(Iso8583BodyEncoding.Ascii));

    /// <summary>ISO 8583:1987, sayısal alanlar packed BCD, bitmap binary. Türk bankalarında yaygın form.</summary>
    public static Iso8583Spec Iso87Bcd { get; } = new(
        Iso87BcdName,
        MtiFormat.Bcd,
        BitmapFormat.Binary,
        LengthHeaderEncoding.Bcd,
        StandardFields(Iso8583BodyEncoding.Bcd));

    /// <summary>
    /// Standart ISO 8583:1987 alan tablosunun bu projede kullanılan alt kümesi.
    /// numericEncoding: sayısal/track alanların gövde kodlaması (ASCII veya BCD);
    /// alfanümerik alanlar her iki dialektte de ASCII kalır.
    /// </summary>
    private static IEnumerable<FieldSpec> StandardFields(Iso8583BodyEncoding numericEncoding)
    {
        FieldSpec N(int no, string name, Iso8583LengthKind kind, int len, bool sensitive = false) =>
            new(no, name, Iso8583Content.Numeric, kind, len, numericEncoding, sensitive);

        FieldSpec An(int no, string name, Iso8583LengthKind kind, int len, bool sensitive = false) =>
            new(no, name, Iso8583Content.AlphaNumeric, kind, len, Iso8583BodyEncoding.Ascii, sensitive);

        return new[]
        {
            N(2, "Primary Account Number", Iso8583LengthKind.LLVar, 19, sensitive: true),
            N(3, "Processing Code", Iso8583LengthKind.Fixed, 6),
            N(4, "Amount, Transaction", Iso8583LengthKind.Fixed, 12),
            N(7, "Transmission Date and Time", Iso8583LengthKind.Fixed, 10),
            N(11, "System Trace Audit Number", Iso8583LengthKind.Fixed, 6),
            N(12, "Time, Local Transaction", Iso8583LengthKind.Fixed, 6),
            N(13, "Date, Local Transaction", Iso8583LengthKind.Fixed, 4),
            N(14, "Date, Expiration", Iso8583LengthKind.Fixed, 4, sensitive: true),
            N(18, "Merchant Category Code", Iso8583LengthKind.Fixed, 4),
            N(22, "POS Entry Mode", Iso8583LengthKind.Fixed, 3),
            N(25, "POS Condition Code", Iso8583LengthKind.Fixed, 2),
            new FieldSpec(35, "Track 2 Data", Iso8583Content.Track2, Iso8583LengthKind.LLVar, 37, numericEncoding, Sensitive: true),
            An(37, "Retrieval Reference Number", Iso8583LengthKind.Fixed, 12),
            An(38, "Authorization Id Response", Iso8583LengthKind.Fixed, 6),
            An(39, "Response Code", Iso8583LengthKind.Fixed, 2),
            An(41, "Card Acceptor Terminal Id", Iso8583LengthKind.Fixed, 8),
            An(42, "Card Acceptor Id Code", Iso8583LengthKind.Fixed, 15),
            An(43, "Card Acceptor Name/Location", Iso8583LengthKind.Fixed, 40),
            An(48, "Additional Data - Private", Iso8583LengthKind.LLLVar, 999, sensitive: true),
            N(49, "Currency Code, Transaction", Iso8583LengthKind.Fixed, 3),
            new FieldSpec(52, "PIN Data", Iso8583Content.Binary, Iso8583LengthKind.Fixed, 8, Iso8583BodyEncoding.Binary, Sensitive: true),
            new FieldSpec(55, "ICC Data (EMV)", Iso8583Content.Binary, Iso8583LengthKind.LLLVar, 255, Iso8583BodyEncoding.Binary, Sensitive: true),
            An(62, "Private Data 1", Iso8583LengthKind.LLLVar, 999),
            An(63, "Private Data 2", Iso8583LengthKind.LLLVar, 999),
            N(67, "Extended Payment Code (Taksit)", Iso8583LengthKind.Fixed, 2),
            N(70, "Network Management Information Code", Iso8583LengthKind.Fixed, 3),
            N(90, "Original Data Elements", Iso8583LengthKind.Fixed, 42),
        };
    }
}

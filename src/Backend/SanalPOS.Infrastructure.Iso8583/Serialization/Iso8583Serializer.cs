using System.Text;
using SanalPOS.Infrastructure.Iso8583.Messages;
using SanalPOS.Infrastructure.Iso8583.Spec;

namespace SanalPOS.Infrastructure.Iso8583.Serialization;

/// <summary>
/// Iso8583Message &lt;-&gt; byte[] dönüşümü. Dialekt (Iso8583Spec) MTI/bitmap/uzunluk başlığı
/// kodlamalarını ve alan tablosunu belirler; serializer dialekte göre davranır.
/// </summary>
public static class Iso8583Serializer
{
    public static byte[] Serialize(Iso8583Message message, Iso8583Spec spec)
    {
        var buffer = new MemoryStream(256);

        WriteMti(buffer, message.Mti, spec);
        WriteBitmap(buffer, message, spec);

        foreach (var (number, value) in message.Fields)
        {
            var field = spec.GetField(number);
            WriteField(buffer, field, value);
        }

        return buffer.ToArray();
    }

    public static Iso8583Message Deserialize(ReadOnlySpan<byte> data, Iso8583Spec spec)
    {
        var offset = 0;

        var mti = ReadMti(data, ref offset, spec);
        var presentFields = ReadBitmap(data, ref offset, spec);

        var message = new Iso8583Message(mti);
        foreach (var number in presentFields)
        {
            var field = spec.GetField(number);
            message[number] = ReadField(data, ref offset, field, spec);
        }

        if (offset != data.Length)
            throw new Iso8583Exception($"Mesaj sonunda {data.Length - offset} beklenmeyen byte kaldı (MTI {mti}, dialekt '{spec.Name}').");

        return message;
    }

    // ---- MTI ----

    private static void WriteMti(MemoryStream buffer, string mti, Iso8583Spec spec)
    {
        var bytes = spec.MtiFormat == MtiFormat.Bcd ? Bcd.Encode(mti) : Encoding.ASCII.GetBytes(mti);
        buffer.Write(bytes);
    }

    private static string ReadMti(ReadOnlySpan<byte> data, ref int offset, Iso8583Spec spec)
    {
        if (spec.MtiFormat == MtiFormat.Bcd)
            return Bcd.Decode(Take(data, ref offset, 2, "MTI"), 4);

        return Encoding.ASCII.GetString(Take(data, ref offset, 4, "MTI"));
    }

    // ---- Bitmap ----

    private static void WriteBitmap(MemoryStream buffer, Iso8583Message message, Iso8583Spec spec)
    {
        var useSecondary = message.Fields.Keys.Any(n => n > 64);
        var bitmap = new byte[useSecondary ? 16 : 8];

        if (useSecondary)
            SetBit(bitmap, 1); // Bit 1 = ikincil bitmap mevcut.

        foreach (var number in message.Fields.Keys)
            SetBit(bitmap, number);

        if (spec.BitmapFormat == BitmapFormat.HexAscii)
            buffer.Write(Encoding.ASCII.GetBytes(Convert.ToHexString(bitmap)));
        else
            buffer.Write(bitmap);
    }

    private static List<int> ReadBitmap(ReadOnlySpan<byte> data, ref int offset, Iso8583Spec spec)
    {
        var primary = ReadBitmapBlock(data, ref offset, spec);
        var hasSecondary = (primary[0] & 0x80) != 0;

        Span<byte> bitmap = stackalloc byte[16];
        primary.CopyTo(bitmap);

        var bitCount = 64;
        if (hasSecondary)
        {
            ReadBitmapBlock(data, ref offset, spec).CopyTo(bitmap[8..]);
            bitCount = 128;
        }

        var fields = new List<int>();
        for (var number = 2; number <= bitCount; number++)
        {
            if ((bitmap[(number - 1) / 8] & (0x80 >> ((number - 1) % 8))) != 0)
                fields.Add(number);
        }

        return fields;
    }

    private static byte[] ReadBitmapBlock(ReadOnlySpan<byte> data, ref int offset, Iso8583Spec spec)
    {
        if (spec.BitmapFormat == BitmapFormat.HexAscii)
        {
            var hex = Encoding.ASCII.GetString(Take(data, ref offset, 16, "bitmap"));
            try
            {
                return Convert.FromHexString(hex);
            }
            catch (FormatException ex)
            {
                throw new Iso8583Exception($"Bitmap hex-ASCII çözümlenemedi: '{hex}'.", ex);
            }
        }

        return Take(data, ref offset, 8, "bitmap").ToArray();
    }

    private static void SetBit(byte[] bitmap, int bit) =>
        bitmap[(bit - 1) / 8] |= (byte)(0x80 >> ((bit - 1) % 8));

    // ---- Alanlar ----

    private static void WriteField(MemoryStream buffer, FieldSpec field, string value)
    {
        value = Normalize(field, value);
        ValidateContent(field, value);

        var charLength = field.Content == Iso8583Content.Binary ? value.Length / 2 : value.Length;

        if (field.LengthKind == Iso8583LengthKind.Fixed)
        {
            if (charLength != field.Length)
                throw new Iso8583Exception($"DE{field.Number} ({field.Name}) sabit {field.Length} uzunluğunda olmalı, {charLength} verildi.");
        }
        else
        {
            if (charLength > field.Length)
                throw new Iso8583Exception($"DE{field.Number} ({field.Name}) en fazla {field.Length} olabilir, {charLength} verildi.");
            buffer.Write(EncodeLengthHeader(field, charLength));
        }

        buffer.Write(EncodeBody(field, value));
    }

    private static string ReadField(ReadOnlySpan<byte> data, ref int offset, FieldSpec field, Iso8583Spec spec)
    {
        var charLength = field.LengthKind == Iso8583LengthKind.Fixed
            ? field.Length
            : DecodeLengthHeader(data, ref offset, field, spec);

        if (charLength > field.Length)
            throw new Iso8583Exception($"DE{field.Number} ({field.Name}) uzunluk başlığı {charLength}, izin verilen maksimum {field.Length}.");

        return DecodeBody(data, ref offset, field, charLength);
    }

    private static byte[] EncodeLengthHeader(FieldSpec field, int length)
    {
        var digits = field.LengthKind == Iso8583LengthKind.LLVar ? 2 : 3;
        var text = length.ToString().PadLeft(digits, '0');

        // Uzunluk başlığı kodlaması dialekt geneli yerine alan gövdesiyle uyumlu tutulur:
        // ASCII gövdeli alan ASCII başlık, BCD/Binary gövdeli alan BCD başlık kullanır.
        return field.Encoding == Iso8583BodyEncoding.Ascii
            ? Encoding.ASCII.GetBytes(text)
            : Bcd.Encode(text.PadLeft(digits == 2 ? 2 : 4, '0'));
    }

    private static int DecodeLengthHeader(ReadOnlySpan<byte> data, ref int offset, FieldSpec field, Iso8583Spec spec)
    {
        var digits = field.LengthKind == Iso8583LengthKind.LLVar ? 2 : 3;

        string text;
        if (field.Encoding == Iso8583BodyEncoding.Ascii)
        {
            text = Encoding.ASCII.GetString(Take(data, ref offset, digits, $"DE{field.Number} uzunluk başlığı"));
        }
        else
        {
            var bytes = digits == 2 ? 1 : 2;
            text = Bcd.Decode(Take(data, ref offset, bytes, $"DE{field.Number} uzunluk başlığı"), bytes * 2);
        }

        if (!int.TryParse(text, out var length))
            throw new Iso8583Exception($"DE{field.Number} uzunluk başlığı sayısal değil: '{text}'.");

        return length;
    }

    private static byte[] EncodeBody(FieldSpec field, string value) => field.Encoding switch
    {
        Iso8583BodyEncoding.Ascii => Encoding.ASCII.GetBytes(value),
        Iso8583BodyEncoding.Bcd => Bcd.Encode(value),
        Iso8583BodyEncoding.Binary => FromHex(field, value),
        _ => throw new Iso8583Exception($"DE{field.Number}: bilinmeyen encoding {field.Encoding}.")
    };

    private static string DecodeBody(ReadOnlySpan<byte> data, ref int offset, FieldSpec field, int charLength)
    {
        switch (field.Encoding)
        {
            case Iso8583BodyEncoding.Ascii:
                return Encoding.ASCII.GetString(Take(data, ref offset, charLength, $"DE{field.Number}"));

            case Iso8583BodyEncoding.Bcd:
            {
                var byteCount = (charLength + 1) / 2;
                return Bcd.Decode(Take(data, ref offset, byteCount, $"DE{field.Number}"), charLength);
            }

            case Iso8583BodyEncoding.Binary:
                return Convert.ToHexString(Take(data, ref offset, charLength, $"DE{field.Number}"));

            default:
                throw new Iso8583Exception($"DE{field.Number}: bilinmeyen encoding {field.Encoding}.");
        }
    }

    private static string Normalize(FieldSpec field, string value)
    {
        if (field.LengthKind != Iso8583LengthKind.Fixed)
            return value;

        // Sabit alanlarda ISO 8583 pad kuralı: sayısal sola '0', alfanümerik sağa boşluk.
        return field.Content switch
        {
            Iso8583Content.Numeric => value.PadLeft(field.Length, '0'),
            Iso8583Content.AlphaNumeric => value.PadRight(field.Length, ' '),
            _ => value
        };
    }

    private static void ValidateContent(FieldSpec field, string value)
    {
        var valid = field.Content switch
        {
            Iso8583Content.Numeric => value.All(char.IsAsciiDigit),
            Iso8583Content.Track2 => value.All(c => char.IsAsciiDigit(c) || c is 'D' or '='),
            Iso8583Content.Binary => value.Length % 2 == 0 && value.All(Uri.IsHexDigit),
            _ => value.All(c => c >= 0x20 && c <= 0x7E)
        };

        if (!valid)
            throw new Iso8583Exception($"DE{field.Number} ({field.Name}) içeriği {field.Content} kuralına uymuyor.");
    }

    private static byte[] FromHex(FieldSpec field, string value)
    {
        try
        {
            return Convert.FromHexString(value);
        }
        catch (FormatException ex)
        {
            throw new Iso8583Exception($"DE{field.Number} ({field.Name}) geçerli hex string değil.", ex);
        }
    }

    private static ReadOnlySpan<byte> Take(ReadOnlySpan<byte> data, ref int offset, int count, string what)
    {
        if (offset + count > data.Length)
            throw new Iso8583Exception($"Mesaj beklenenden kısa: {what} için {count} byte gerekiyordu, {data.Length - offset} kaldı.");

        var slice = data.Slice(offset, count);
        offset += count;
        return slice;
    }
}

/// <summary>Packed BCD yardımcıları. Track-2 ayracı '=' 0xD nibble olarak kodlanır.</summary>
internal static class Bcd
{
    public static byte[] Encode(string digits)
    {
        if (digits.Length % 2 != 0)
            digits = "0" + digits;

        var bytes = new byte[digits.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)((NibbleOf(digits[i * 2]) << 4) | NibbleOf(digits[i * 2 + 1]));

        return bytes;
    }

    public static string Decode(ReadOnlySpan<byte> data, int digitCount)
    {
        var chars = new char[data.Length * 2];
        for (var i = 0; i < data.Length; i++)
        {
            chars[i * 2] = CharOf((byte)(data[i] >> 4));
            chars[i * 2 + 1] = CharOf((byte)(data[i] & 0x0F));
        }

        // Tek haneli uzunluklarda soldaki pad nibble'ı atılır.
        return new string(chars, chars.Length - digitCount, digitCount);
    }

    private static int NibbleOf(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        '=' or 'D' => 0xD,
        _ => throw new Iso8583Exception($"BCD kodlanamayan karakter: '{c}'.")
    };

    private static char CharOf(byte nibble) => nibble switch
    {
        <= 9 => (char)('0' + nibble),
        0xD => 'D',
        _ => throw new Iso8583Exception($"BCD çözülemeyen nibble: 0x{nibble:X}.")
    };
}

using System.Text;
using SanalPOS.Infrastructure.Iso8583.Messages;
using SanalPOS.Infrastructure.Iso8583.Spec;

namespace SanalPOS.Infrastructure.Iso8583.Diagnostics;

/// <summary>
/// Log için güvenli mesaj gösterimi. PCI DSS gereği tam PAN/track/CVV asla loglanmaz:
/// PAN ilk 6 + son 4 açık kalır, diğer hassas alanlar tamamen maskelenir.
/// </summary>
public static class Iso8583MessageMasker
{
    public static string Describe(Iso8583Message message, Iso8583Spec spec)
    {
        var sb = new StringBuilder(128);
        sb.Append("MTI=").Append(message.Mti);

        foreach (var (number, value) in message.Fields)
        {
            sb.Append(", DE").Append(number).Append('=');
            var sensitive = !spec.HasField(number) || spec.GetField(number).Sensitive;
            sb.Append(sensitive ? Mask(number, value) : value);
        }

        return sb.ToString();
    }

    private static string Mask(int number, string value)
    {
        // DE2 (PAN): BIN (ilk 6) + son 4 tanılama için açık bırakılır.
        if (number == 2 && value.Length >= 13)
            return value[..6] + new string('*', value.Length - 10) + value[^4..];

        // DE35 (track2): PAN'in BIN kısmı dışında tamamı maskelenir.
        if (number == 35 && value.Length >= 6)
            return value[..6] + new string('*', value.Length - 6);

        return new string('*', value.Length);
    }
}

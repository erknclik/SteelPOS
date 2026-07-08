namespace SanalPOS.Infrastructure.Iso8583.Adapters;

/// <summary>ISO 8583 DE39 yanıt kodları ve kullanıcıya dönecek Türkçe karşılıkları.</summary>
public static class Iso8583ResponseCodes
{
    public const string Approved = "00";

    private static readonly IReadOnlyDictionary<string, string> Messages = new Dictionary<string, string>
    {
        ["00"] = "İşlem onaylandı.",
        ["01"] = "Bankanızı arayınız.",
        ["02"] = "Bankanızı arayınız (özel koşul).",
        ["03"] = "Geçersiz üye işyeri.",
        ["04"] = "Karta el koyunuz.",
        ["05"] = "İşlem onaylanmadı.",
        ["12"] = "Geçersiz işlem.",
        ["13"] = "Geçersiz tutar.",
        ["14"] = "Geçersiz kart numarası.",
        ["15"] = "Kart hamili bankası bulunamadı.",
        ["19"] = "İşlemi tekrar deneyiniz.",
        ["25"] = "Kayıt bulunamadı.",
        ["30"] = "Mesaj format hatası.",
        ["33"] = "Kartın süresi dolmuş.",
        ["34"] = "Sahtekârlık şüphesi.",
        ["36"] = "Kısıtlanmış kart.",
        ["38"] = "PIN deneme sayısı aşıldı.",
        ["41"] = "Kayıp kart.",
        ["43"] = "Çalıntı kart.",
        ["51"] = "Yetersiz bakiye / limit.",
        ["54"] = "Kartın son kullanma tarihi geçmiş.",
        ["57"] = "Kart sahibine kapalı işlem.",
        ["58"] = "Terminale kapalı işlem.",
        ["61"] = "Para çekme limiti aşıldı.",
        ["62"] = "Kısıtlanmış kart.",
        ["63"] = "Güvenlik ihlali.",
        ["65"] = "İşlem adedi limiti aşıldı.",
        ["75"] = "PIN deneme sayısı aşıldı.",
        ["76"] = "Anahtar senkronizasyon hatası.",
        ["77"] = "Red, tekrar iletim hatalı.",
        ["82"] = "CVV doğrulaması başarısız.",
        ["89"] = "Geçersiz terminal.",
        ["91"] = "Kart hamili bankası hizmet dışı.",
        ["92"] = "Yönlendirme yapılamadı.",
        ["94"] = "Mükerrer işlem.",
        ["96"] = "Banka sistem hatası.",
        ["99"] = "Genel hata."
    };

    public static bool IsApproved(string? code) => code == Approved;

    public static string MessageOf(string? code) =>
        code is not null && Messages.TryGetValue(code, out var message)
            ? message
            : $"Banka işlemi reddetti (kod: {code ?? "yok"}).";
}

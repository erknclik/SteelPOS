# ISO 8583 Banka Entegrasyonu

Bu doküman, SanalPOS'un bankalarla **ISO 8583:1987** protokolü üzerinden konuşmasını sağlayan
`SanalPOS.Infrastructure.Iso8583` projesini anlatır. Amaç: **hangi banka gelirse gelsin**,
kod değişikliği yapmadan (ya da en fazla bir dialekt tanımı ekleyerek) o bankanın mesaj
formatına uyum sağlamak.

## 1. Genel Mimari

```
CreatePaymentCommandHandler
        │  (IBankAdapterFactory.Resolve(terminal.BankProviderCode))
        ▼
IBankProviderAdapter  ◄────────────  MockBankAdapter (ilk faz / test)
        ▲
        │
Iso8583BankAdapter (banka başına 1 instance, konfigürasyondan)
        │  Iso8583Message (MTI + DE sözlüğü — kodlamadan bağımsız)
        ▼
Iso8583Serializer  ◄────  Iso8583Spec (dialekt: alan tablosu + kodlamalar)
        │  byte[]
        ▼
TcpIso8583Channel (framing, TPDU, TLS, STAN eşleştirme, reconnect)
        │  TCP / TLS
        ▼
      BANKA
```

Katmanların sorumlulukları birbirinden tamamen ayrıdır:

| Katman | Sorumluluk | Banka farkı nerede? |
|---|---|---|
| `Iso8583Message` | MTI + data element (DE) sözlüğü; kodlama bilmez | — |
| `Iso8583Spec` (dialekt) | Alan tablosu, MTI/bitmap/uzunluk kodlamaları | **Burada** (konfigürasyon/kayıt) |
| `Iso8583Serializer` | Mesaj ↔ byte[] dönüşümü, bitmap, LLVAR/LLLVAR, BCD/ASCII | — |
| `TcpIso8583Channel` | Kalıcı bağlantı, çerçeveleme, istek/yanıt eşleştirme | Kanal ayarları (konfigürasyon) |
| `Iso8583BankAdapter` | İş akışı → MTI/PC eşlemesi, DE39 → sonuç, timeout reversal | Terminal/üye işyeri kimlikleri (konfigürasyon) |

## 2. Dialekt (Iso8583Spec) Sistemi

Her banka ISO 8583'ü farklı yorumlar: kimi tamamen ASCII konuşur, kimi sayısal alanları
packed BCD kodlar, kimi alan uzunluklarını değiştirir. Bu farklar **dialekt** olarak modellenir.

Hazır dialektler (`Iso8583Dialects`):

- **`Iso87Ascii`** — MTI 4 ASCII karakter, bitmap hex-ASCII, alanlar ASCII. Simülatör/test dostu.
- **`Iso87Bcd`** — MTI 2 byte BCD, bitmap binary, sayısal alanlar packed BCD. Türk bankalarında yaygın form.

### Banka özel dialekt ekleme

Var olan bir dialekti temel alıp yalnızca farklı alanları override edersiniz:

```csharp
// Örn. X Bankası CVV2'yi DE48'de LLVAR bekliyor olsun:
var xBank = Iso8583Dialects.Iso87Bcd.WithOverrides(
    "XBank",
    new FieldSpec(48, "CVV2", Iso8583Content.AlphaNumeric, Iso8583LengthKind.LLVar, 10,
        Iso8583BodyEncoding.Ascii, Sensitive: true));

// Uygulama açılışında kayıt:
dialectRegistry.Register(xBank);   // IIso8583DialectRegistry (DI'dan)
```

Konfigürasyonda `"Dialect": "XBank"` yazmak yeterlidir; adaptör ve kanal kodu değişmez.

## 3. Mesaj Eşlemesi (Iso8583BankAdapter)

| SanalPOS işlemi | MTI | Processing Code (DE3) | Not |
|---|---|---|---|
| Satış (Charge) | 0200 | 000000 | |
| Ön otorizasyon (PreAuth) | 0100 | 000000 | |
| Kapama (Capture) | 0220 | 000000 | DE38 = orijinal otorizasyon kodu |
| İptal (Void) | 0400 | 020000 | DE38 |
| İade (Refund) | 0200 | 200000 | DE38 |

Otomatik doldurulan ortak alanlar: DE7 (iletim zamanı, UTC), DE11 (STAN), DE12/13 (yerel saat/tarih),
DE18 (MCC), DE22=010, DE25=59 (e-ticaret), DE37 (RRN), DE41/42 (terminal/üye işyeri), DE43.
Satışta ek olarak: DE2 (PAN), DE4 (tutar, kuruş, 12 hane), DE14 (son kullanma YYMM), DE48 (CVV),
DE49 (ISO 4217 sayısal kur kodu), DE62 (sipariş referansı), DE67 (taksit sayısı, >1 ise).

**Yanıt değerlendirme:** DE39 = `"00"` → onay (DE38 otorizasyon kodu döner); diğer tüm kodlar
`Iso8583ResponseCodes` tablosundan Türkçe mesaja çevrilir ve işlem reddedilir.

**Timeout reversal:** Finansal isteğe (0100/0200) yanıt gelmezse işlem banka tarafında
onaylanmış olabilir. Adaptör otomatik olarak 0400 reversal gönderir (DE90 orijinal mesaj kimliği ile)
ve işlemi `TIMEOUT` koduyla reddeder. Reversal da başarısızsa gün sonu mutabakatı düzeltir.

## 4. TCP Kanal (TcpIso8583Channel)

- **Kalıcı bağlantı**: banka başına tek soket; kopunca bir sonraki istekte otomatik yeniden bağlanır.
- **Çerçeveleme**: `TwoByteBinary` (2 byte big-endian uzunluk, en yaygın) veya `FourAsciiDigits`.
- **TPDU**: bazı bankaların beklediği sabit başlık `TpduHeaderHex` ile eklenir/soyulur.
- **TLS**: `UseTls: true` ile SslStream (sertifika doğrulaması standart; üretimde zorunlu tutun).
- **Eşleştirme**: yanıtlar sıra garantisi olmadan gelebilir; STAN (DE11) + yanıt MTI anahtarıyla
  bekleyen isteğe eşlenir. Eşleşmeyen yanıt loglanıp atılır.
- **STAN**: `InMemoryStanSequence` süreç içi döngüsel sayaçtır (000001–999999). Çok instance'lı
  üretimde banka başına merkezî (örn. Redis tabanlı) `IStanSequence` implementasyonu takılmalıdır.

## 5. Konfigürasyon

Yeni banka eklemek = `appsettings.json`'a kayıt eklemek. Her kayıt için bir `Iso8583BankAdapter`
oluşturulur ve `BankAdapterFactory`'ye `ProviderCode` ile kaydedilir; `Terminal.BankProviderCode`
bu koda işaret eder. Hatalı/eksik konfigürasyon açılışta patlar (fail-fast).

```json
"Iso8583": {
  "Banks": [
    {
      "Enabled": true,
      "ProviderCode": "ISBANK",
      "Dialect": "Iso87Bcd",
      "TerminalId": "TERM0001",
      "MerchantId": "000000000000001",
      "MerchantNameLocation": "SanalPOS Merchant Istanbul TR",
      "MerchantCategoryCode": "5999",
      "Channel": {
        "Host": "pos.isbank.example",
        "Port": 8583,
        "UseTls": true,
        "FrameLengthFormat": "TwoByteBinary",
        "TpduHeaderHex": "6000030000",
        "ConnectTimeout": "00:00:10",
        "ResponseTimeout": "00:00:30"
      }
    }
  ]
}
```

`Enabled: false` ile kayıt repoda durur ama adaptör oluşturulmaz (örnek/sandbox tanımları için).

## 6. Güvenlik (PCI DSS)

- Tam PAN/CVV yalnızca banka çağrısı süresince bellekte yaşar; **hiçbir katman saklamaz**.
- Kanal, mesajları yalnızca `Debug` seviyesinde ve **maskeli** loglar (`Iso8583MessageMasker`):
  DE2 → ilk 6 + son 4, DE35 → ilk 6, DE14/48/52/55 → tamamen maskeli.
  Dialektte tanımsız alanlar da varsayılan olarak maskelenir.
- Üretimde `UseTls: true` zorunlu tutulmalı; sertifika doğrulaması kapatılamaz.

## 7. Yeni Banka Ekleme Kontrol Listesi

1. Bankanın ISO 8583 spesifikasyonunu edinin (alan tablosu, kodlamalar, TPDU, çerçeve formatı).
2. Hazır dialektlerden biri uyuyorsa doğrudan kullanın; uymuyorsa `WithOverrides` ile banka
   dialekti tanımlayıp `IIso8583DialectRegistry.Register` ile kaydedin.
3. `Iso8583:Banks` altına konfigürasyon kaydını ekleyin (terminal/üye işyeri numaraları bankadan alınır).
4. Terminal kaydının `BankProviderCode` değerini yeni `ProviderCode` ile eşleştirin.
5. Banka test ortamına karşı sertifikasyon senaryolarını koşun (onay, red, iptal, iade, timeout/reversal).

## 8. Test

`tests/SanalPOS.Iso8583.UnitTests` kapsamı:

- Serializer: ASCII/BCD roundtrip, ikincil bitmap, LLVAR/LLLVAR, pad kuralları, hatalı girdi/kısa mesaj.
- Adaptör: MTI/PC eşlemesi, alan doldurma, DE39 → Türkçe mesaj, timeout → otomatik reversal.
- Kanal: loopback TCP üzerinde sahte banka ile gerçek framing, eşzamanlı isteklerin STAN ile
  doğru eşleşmesi, timeout ve yeniden bağlanma senaryoları.
- Maskeleme, STAN üreteci (thread-safety), dialekt registry.

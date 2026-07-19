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

| SanalPOS işlemi | MTI | Processing Code (DE3) | Orijinal işlem referansı |
|---|---|---|---|
| Satış (Charge) | 0200 | 000000 | — |
| Ön otorizasyon (PreAuth) | 0100 | 000000 | — |
| Kapama (Capture) | 0220 | 000000 | DE37 = RRN, DE38 = otorizasyon kodu |
| İptal (Void) | 0400 | 020000 | DE37/DE38 + DE90 (orijinal STAN) + DE4 orijinal tutar |
| İade (Refund) | 0200 | 200000 | DE37/DE38 |

Onay yanıtındaki **RRN (DE37)** ve **STAN (DE11)** `ChargeResult` ile üst katmana döner ve
`PaymentTransaction.BankRrn/BankStan` alanlarında persist edilir; sonraki operasyonlar bu
referansları `BankTransactionReference` ile adaptöre geri taşır (bankalar iptal/iadeyi
yalnızca otorizasyon koduyla değil RRN/STAN ile eşleştirir).

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
- **STAN**: `Iso8583:StanSequence` konfigürasyonu ile seçilir (fail-fast):
  - `InMemory` (varsayılan): süreç içi döngüsel sayaç (000001–999999); tek instance/dev-test için.
  - `Redis`: banka başına merkezî INCR sayacı (`sanalpos:iso8583:stan:{provider}`); çok
    instance'lı üretimde STAN çakışmasını önler, sayaç yeniden başlatmalarda kalıcıdır.

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

## 7a. Gün Sonu Mutabakatı (Batch Close, 0500)

Her gün UTC 03:00'te `SanalPOS.BackgroundJobs` bir `DailyReconciliationRequestedEvent`
yayınlar; API tarafındaki consumer `RunReconciliationCommand`'ı çalıştırır
(manuel tetikleme: `POST /api/v1/reconciliation/run`, SystemAdmin).

Akış: günün toplamları banka sağlayıcısı + para birimi bazında hesaplanır ve her bankaya
**0500 (PC 920000)** batch-close mesajı ile gönderilir:

| Alan | İçerik |
|---|---|
| DE76 / DE88 | Satış adedi / tutarı (debits) — 0200 satış + 0220 kapama |
| DE74 / DE86 | İade adedi / tutarı (credits) |
| DE77 / DE89 | İptal adedi / tutarı (debit reversals) |

Banka toplamları kendi defteriyle karşılaştırır: eşleşirse DE39=00 (dengede),
eşleşmezse **DE39=95** (out-of-balance) döner ve fark operasyon ekibince incelenir.

Her koşum sonucu **`reconciliation_runs` tablosuna kalıcı yazılır** (banka + para birimi
başına bir satır); `GET /api/v1/reconciliation/history` son koşumları döner. Frontend'te
mutabakat sayfası geçmiş tablosunu, dashboard ise son koşumların dengede olup olmadığını
gösteren bir kart sunar — dengesiz kayıtlar operasyonun inceleme kuyruğudur.
Sayım kuralı: void edilen işlem **hem satış hem iptal** kalemidir (banka semantiği,
net = debits − reversals); ön otorizasyon (0100) tahsilat olmadığı için sayılmaz,
kapama (0220) sayılır. Timeout sonrası otomatik reversal'a düşen işlemler hiçbir
tarafta yer almaz (onaylanmadılar) — mutabakat bu senaryonun nihai güvencesidir.

## 8. Banka Simülatörü (SanalPOS.BankSimulator)

Gerçek banka bağlantısı olmadan uçtan uca akışı koşmak için ISO 8583 konuşan sahte banka
host'u. Konsol uygulaması olarak (`dotnet run --project src/Backend/SanalPOS.BankSimulator -- 8583`)
veya Docker Compose'daki `banksim` servisi olarak çalışır; entegrasyon testleri aynı motoru
in-process kullanır.

Deterministik senaryolar (test kartıyla tetiklenir, hepsi Luhn-geçerli olmalıdır):

| Tetik | Sonuç |
|---|---|
| PAN sonu `0002` (örn. 4000000000000002) | DE39=51, yetersiz bakiye |
| PAN sonu `0004` (örn. 4000000800000004) | Yanıt yok → adaptör timeout + otomatik reversal |
| PAN sonu `0041` | DE39=41, kayıp kart |
| CVV (DE48) `999` | DE39=82, CVV hatası |
| Diğer tüm kartlar | DE39=00 onay + DE38 otorizasyon kodu |

0800 (echo/sign-on) ve 0400 (reversal) her zaman onaylanır. Simülatör onayladığı finansal
işlemleri kendi defterine (ledger) yazar; 0500 batch-close geldiğinde gönderilen toplamları
defteriyle karşılaştırıp 00 (dengede) veya 95 (out-of-balance) döner — böylece mutabakat
akışı uçtan uca gerçekçi test edilir.

Docker Compose ortamında `sanalpos-api`, `BANKSIM` sağlayıcı koduyla önceden
yapılandırılmıştır: `BankProviderCode = "BANKSIM"` olan bir terminal oluşturmak,
ödemeleri gerçek TCP/ISO 8583 hattı üzerinden simülatöre yönlendirmek için yeterlidir.

## 9. Test

`tests/SanalPOS.Iso8583.UnitTests` kapsamı:

- Serializer: ASCII/BCD roundtrip, ikincil bitmap, LLVAR/LLLVAR, pad kuralları, hatalı girdi/kısa mesaj.
- Adaptör: MTI/PC eşlemesi, alan doldurma, DE39 → Türkçe mesaj, timeout → otomatik reversal.
- Kanal: loopback TCP üzerinde sahte banka ile gerçek framing, eşzamanlı isteklerin STAN ile
  doğru eşleşmesi, timeout ve yeniden bağlanma senaryoları.
- Maskeleme, STAN üreteci (thread-safety), dialekt registry.

`tests/SanalPOS.API.IntegrationTests/Iso8583EndToEndTests.cs` ise tam zinciri doğrular:
HTTP API → CQRS handler → `Iso8583BankAdapter` → `TcpIso8583Channel` → **gerçek TCP** →
`BankSimulatorEngine`. Onay, red (51/82) ve banka sessiz kaldığında timeout + otomatik
reversal senaryoları uçtan uca koşulur.

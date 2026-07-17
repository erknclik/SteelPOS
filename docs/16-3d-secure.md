# 3D Secure Akışı

Bu doküman, SanalPOS'un 3D Secure (kart hamili doğrulama) desteğini anlatır. MPI
(Merchant Plug-In) soyutlaması provider pattern ile tasarlanmıştır: ilk fazda simüle
MPI çalışır, üretimde gerçek MPI/banka 3D gateway adaptörü aynı arayüzü uygulayarak takılır.

## 1. Akış

```
İstemci                    SanalPOS API                  MPI (IThreeDSecureProvider)     ACS (banka)
   │  POST /payments/3ds        │                                │                          │
   ├──────────────────────────► │  InitiateAsync (kayıt sorgusu) │                          │
   │                            ├──────────────────────────────► │                          │
   │                            │  Enrolled: Md, AcsUrl, PaReq   │                          │
   │  requiresRedirect=true     │  işlem -> Pending3DS           │                          │
   │  (md, acsUrl, paReq)       │  kart bağlamı şifreli cache'e  │                          │
   │ ◄──────────────────────────┤                                │                          │
   │  form-post (MD,PaReq,TermUrl)                               │                          │
   ├───────────────────────────────────────────────────────────────────────────────────────►│
   │                     kart hamili OTP girer; ACS TermUrl'e MD+PaRes form-post eder       │
   │ ◄─────────────────────────────────────────────────────────────────────────────────────┤
   │  POST /payments/3ds/complete (MD, PaRes)                    │                          │
   ├──────────────────────────► │  VerifyAsync(md, paRes)        │                          │
   │                            ├──────────────────────────────► │                          │
   │                            │  ECI/CAVV                      │                          │
   │                            │  otorizasyon (ISO 8583, DE47=ECI/CAVV) -> banka           │
   │  PaymentResultDto          │  işlem -> Approved/Declined    │                          │
   │ ◄──────────────────────────┤                                │                          │
```

- **Durum makinesi:** `Pending → Pending3DS → Approved/Declined`. `Pending3DS` durumundaki
  işlem void/refund edilemez.
- **Kart 3DS'e kayıtlı değilse** (`IsEnrolled=false`): işlem yönlendirme olmadan doğrudan
  otorize edilir (`requiresRedirect=false`, sonuç `payment` alanında döner).
- **ECI/CAVV:** başarılı doğrulamanın kanıtı `ChargeRequest.ThreeDSecure` ile banka
  adaptörüne taşınır; ISO 8583 adaptörü bunu DE47'de gönderir (dialekt ile override edilebilir).

## 2. PCI DSS: Kart Verisinin Yaşam Döngüsü

Initiate ile complete arasında kart verisi gereklidir (otorizasyon callback'te yapılır).
Bu pencere boyunca kart bağlamı:

- `ISecretProtector` (DataProtection) ile **şifrelenmiş** halde,
- **MD anahtarıyla** cache'te (`sanalpos:3ds-session:{md}`), **10 dakika TTL** ile tutulur,
- callback'te **tek kullanımlık** okunur ve **hemen silinir** (replay koruması: aynı MD
  ile ikinci callback 404 alır),
- veritabanına asla yazılmaz, loglanmaz.

## 3. API

| Endpoint | Auth | Açıklama |
|---|---|---|
| `POST /api/v1/payments/3ds` | JWT + Idempotency-Key | 3DS satış başlatır; `requiresRedirect` ve ACS bilgileri döner |
| `POST /api/v1/payments/3ds/complete` | Anonim (form-post) | ACS dönüşü; MD tek kullanımlık oturum belirtecidir |
| `POST /api/v1/acs-simulator` | Anonim (yalnızca Simulated) | Dev ACS sayfası; PaRes'i TermUrl'e otomatik form-post eder |

`3ds/complete` endpoint'i ACS'in tarayıcı üzerinden yaptığı `application/x-www-form-urlencoded`
POST'u kabul eder; kimlik MD üzerinden kurulur (JWT beklenmez).

**SPA akışı:** `3ds/complete?returnUrl=...` verilirse sonuç JSON yerine 302 redirect ile
`returnUrl?transactionId=...&status=Approved|Declined|SessionExpired` olarak taşınır; böylece
tarayıcı React'taki `/payments/3ds/result` sayfasına düşer. Open-redirect'e karşı yalnızca
relatif yollar ve `ThreeDSecure:AllowedReturnUrls` önekleri kabul edilir. Frontend'te
"3D Secure ile öde" işaretliyken form, initiate yanıtındaki ACS'e otomatik form-post yapar
(`AcsRedirect` bileşeni); kart bilgisi yönlendirmeden önce state'ten temizlenir.

## 4. Simüle MPI Senaryoları

`ThreeDSecure:Provider = "Simulated"` (varsayılan) iken test kartları:

| Kart | Davranış |
|---|---|
| `4000000600000006` | 3DS'e kayıtlı değil → doğrudan otorizasyon fallback'i |
| `4000000700000005` | ACS doğrulaması başarısız (PaRes=N) → `3DS-FAIL` red |
| Diğerleri (örn. `4111111111111111`) | Kayıtlı → ACS onaylar → ECI 05 + CAVV ile otorizasyon |

Banka simülatörü senaryolarıyla (docs/15 §8) birleştirilebilir: örn. 3DS başarılı ama
banka `...0002` kartını yetersiz bakiye ile reddeder.

## 5. Konfigürasyon

```json
"ThreeDSecure": {
  "Provider": "Simulated",           // fail-fast switch; üretimde gerçek MPI adaptörü
  "Simulated": {
    "AcsUrl": "/api/v1/acs-simulator" // dev ACS sayfası (Simulated dışı provider'da 404)
  }
}
```

Gerçek MPI eklemek için `IThreeDSecureProvider` uygulanır ve switch'e yeni case eklenir;
Application katmanı ve API değişmez.

## 6. Test

- `PaymentTransactionThreeDSecureTests`: durum geçişleri (Pending3DS'e giriş/çıkış, yasak geçişler).
- `ThreeDSecureEndToEndTests`: initiate → ACS sayfası (HTML parse) → complete → ISO 8583
  hattı üzerinden banka simülatörüne otorizasyon. Onay, ACS reddi, kayıtsız kart fallback'i,
  MD replay koruması ve bilinmeyen MD senaryoları.
- `Iso8583BankAdapterTests`: ECI/CAVV → DE47 eşlemesi.

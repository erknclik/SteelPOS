# SanalPOS - API Tasarımı

## 1. Genel Kurallar

- REST tabanlı, JSON gövde
- Base path: `/api/v{version}/...` (ör. `/api/v1/payments`)
- Kimlik doğrulama: `Authorization: Bearer <JWT>`
- Tüm isteklerde `X-Correlation-Id` header'ı opsiyonel gönderilebilir; gönderilmezse sunucu üretir ve response'da geri döner
- Ödeme oluşturma gibi kritik uçlarda `Idempotency-Key` header'ı **zorunludur**
- Hata formatı: RFC 7807 Problem Details
- Sayfalama: `?page=1&pageSize=20` query parametreleri, response'da `X-Total-Count` header'ı
- Tarih formatı: ISO 8601 UTC (`2026-07-07T12:00:00Z`)

## 2. Kimlik Doğrulama & Yetkilendirme

| Endpoint | Metod | Açıklama |
|---|---|---|
| `/api/v1/auth/login` | POST | Kullanıcı adı/şifre ile giriş, JWT + refresh token döner |
| `/api/v1/auth/refresh` | POST | Refresh token ile yeni access token alma |
| `/api/v1/auth/logout` | POST | Refresh token'ı iptal eder, access token'ı blacklist'e alır |
| `/api/v1/auth/change-password` | POST | Şifre değiştirme |

Roller: `SystemAdmin`, `MerchantAdmin`, `Operator`, `ReadOnly`

## 3. Ödeme İşlemleri (Payments)

| Endpoint | Metod | Açıklama | Yetki |
|---|---|---|---|
| `/api/v1/payments` | POST | Yeni satış işlemi oluşturur (Idempotency-Key zorunlu) | Operator+ |
| `/api/v1/payments/pre-auth` | POST | Ön provizyon (blokaj) oluşturur | Operator+ |
| `/api/v1/payments/{id}/capture` | POST | Provizyonu kapatır (tahsilata çevirir) | Operator+ |
| `/api/v1/payments/{id}/void` | POST | Aynı gün içinde işlemi iptal eder | Operator+ |
| `/api/v1/payments/{id}/refund` | POST | Kısmi/tam iade oluşturur | MerchantAdmin+ |
| `/api/v1/payments/{id}` | GET | Tekil işlem detayını getirir | Operator+ |
| `/api/v1/payments` | GET | Filtrelenebilir işlem listesi (tarih, durum, terminal) | Operator+ |
| `/api/v1/payments/{id}/status-history` | GET | İşlemin durum geçmişi | Operator+ |

### Örnek İstek: Ödeme Oluşturma
```http
POST /api/v1/payments HTTP/1.1
Authorization: Bearer <token>
Idempotency-Key: 3f1c9e2a-...
Content-Type: application/json

{
  "merchantId": "b2a1...",
  "terminalId": "9c3d...",
  "orderReference": "SIP-2026-000123",
  "amount": 1250.50,
  "currency": "TRY",
  "installmentCount": 3,
  "cardNumber": "4021220000001234",
  "cardHolderName": "AHMET YILMAZ",
  "expireMonth": 12,
  "expireYear": 2028,
  "cvv": "123"
}
```

### Örnek Yanıt
```json
{
  "transactionId": "d4e5f6...",
  "status": "Approved",
  "bankAuthCode": "482910",
  "commissionAmount": 31.26,
  "netAmount": 1219.24,
  "completedAt": "2026-07-07T12:03:11Z"
}
```

> **Güvenlik Notu**: `cardNumber` ve `cvv` alanları sadece TLS üzerinden, isteğin sadece HTTPS ile kabul edildiği ortamda gönderilir; sunucu tarafında loglanmaz, sadece banka adaptörüne iletilir ve response'da/veritabanında maskeli hali tutulur (bkz. [11-guvenlik.md](./11-guvenlik.md)).

## 4. Üye İşyeri (Merchants)

| Endpoint | Metod | Açıklama | Yetki |
|---|---|---|---|
| `/api/v1/merchants` | POST | Yeni merchant oluşturur | SystemAdmin |
| `/api/v1/merchants` | GET | Merchant listesi | SystemAdmin |
| `/api/v1/merchants/{id}` | GET | Merchant detayı | SystemAdmin, MerchantAdmin (kendi) |
| `/api/v1/merchants/{id}` | PUT | Merchant günceller | SystemAdmin |
| `/api/v1/merchants/{id}/suspend` | POST | Merchant'ı askıya alır | SystemAdmin |
| `/api/v1/merchants/{id}/commission-rules` | GET/POST | Komisyon kuralları | SystemAdmin |
| `/api/v1/merchants/{id}/stores` | GET/POST | Mağaza yönetimi | MerchantAdmin+ |
| `/api/v1/merchants/{id}/terminals` | GET/POST | Terminal yönetimi | MerchantAdmin+ |

## 5. Raporlama (Reporting)

| Endpoint | Metod | Açıklama |
|---|---|---|
| `/api/v1/reports/daily-summary` | GET | Günlük işlem özeti (toplam tutar, adet, komisyon) |
| `/api/v1/reports/reconciliation` | GET | Banka mutabakat raporu |
| `/api/v1/reports/export` | GET | CSV/Excel export (`?format=csv`) |

## 6. Webhook Yönetimi

| Endpoint | Metod | Açıklama |
|---|---|---|
| `/api/v1/webhooks` | GET/POST | Webhook aboneliği listeleme/oluşturma |
| `/api/v1/webhooks/{id}` | DELETE | Abonelik silme |
| `/api/v1/webhooks/{id}/test` | POST | Test event'i gönderir |

Webhook gövdeleri `X-SanalPOS-Signature` header'ında HMAC-SHA256 imzası ile gönderilir; alıcı taraf bu imzayı kendi secret'ı ile doğrular.

## 7. Sistem/Sağlık

| Endpoint | Metod | Açıklama |
|---|---|---|
| `/health` | GET | Genel sağlık kontrolü (DB, Redis, MQ) |
| `/health/ready` | GET | Readiness probe (Kubernetes) |
| `/health/live` | GET | Liveness probe |
| `/api/v1/version` | GET | Uygulama versiyon bilgisi |

## 8. HTTP Durum Kodu Standardı

| Kod | Anlamı |
|---|---|
| 200 | Başarılı |
| 201 | Oluşturuldu (Location header ile) |
| 202 | Kabul edildi, asenkron işleniyor |
| 400 | Validasyon hatası |
| 401 | Kimlik doğrulama gerekli/geçersiz |
| 403 | Yetkisiz erişim |
| 404 | Bulunamadı |
| 409 | Çakışma (ör. aynı idempotency key ile farklı gövde) |
| 422 | İş kuralı ihlali (ör. yetersiz limit) |
| 429 | Rate limit aşıldı |
| 500 | Beklenmeyen sunucu hatası |
| 503 | Servis geçici olarak kullanılamıyor (banka adaptörü down) |

## 9. API Versiyonlama Politikası

- URL tabanlı versiyonlama (`/api/v1/...`, `/api/v2/...`)
- Eski versiyon en az 6 ay boyunca desteklenmeye devam eder (deprecation header: `Sunset`)
- Kırıcı olmayan değişiklikler (yeni alan ekleme) versiyon artırmadan yapılabilir

## 10. Swagger/OpenAPI

`/swagger/index.html` üzerinden interaktif dokümantasyon sunulur; JWT ile "Authorize" butonu üzerinden test edilebilir. OpenAPI şeması `/swagger/v1/swagger.json` üzerinden dışa aktarılabilir (Postman/Insomnia koleksiyonuna dönüştürmek için).

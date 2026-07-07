# SanalPOS - Güvenlik Standartları

SanalPOS bir ödeme/finans sistemi olduğu için güvenlik, **isteğe bağlı bir özellik değil, tasarımın merkezi bileşenidir**. Bu doküman genel güvenlik prensiplerini ve uyulması gereken standartları anlatır; spesifik banka/PSP sertifikasyon süreçleri (PCI-DSS denetimi vb.) ilgili uzman/denetim firması ile yürütülmelidir.

## 1. PCI-DSS Kapsamı ve Yaklaşım

- Sistem, tam kart numarasını (PAN) **hiçbir zaman kalıcı olarak saklamaz**.
- İdeal mimaride kart verisi doğrudan banka/PSP'nin **tokenization** servisine veya **hosted field/iframe** çözümüne yönlendirilir; SanalPOS backend'i sadece maskeli PAN ve dönen token/auth code ile çalışır.
- Eğer PAN geçici olarak backend üzerinden geçmesi gerekiyorsa (örn. mock adaptör senaryosu):
  - Bellekte tutulma süresi minimize edilir, işlem tamamlanır tamamlanmaz referans temizlenir (`Array.Clear` / güvenli string temizleme)
  - Hiçbir loglama, cache, veya mesaj kuyruğu event'ine tam PAN yazılmaz
  - Disk'e swap edilebilecek büyük obje havuzlarında tutulmaz

## 2. Kimlik Doğrulama & Yetkilendirme

- **JWT (access token, 15 dk ömür) + Refresh Token (rotating, 7 gün ömür)**
- Refresh token'lar veritabanında **hash'lenerek** saklanır (düz metin tutulmaz), her kullanımda rotate edilir (tekrar kullanım tespiti = tüm oturumların iptali)
- Parola politikası: minimum 10 karakter, karmaşıklık kuralı, `HaveIBeenPwned` API entegrasyonu ile bilinen sızıntı kontrolü (opsiyonel)
- Parola hash: ASP.NET Core Identity varsayılan `PasswordHasher` (PBKDF2, yüksek iterasyon) veya `BCrypt`
- **RBAC** (Role-Based Access Control): `SystemAdmin`, `MerchantAdmin`, `Operator`, `ReadOnly`
- Kritik işlemler (iade, merchant askıya alma) için **ikinci onay (four-eyes principle)** ileri fazda değerlendirilebilir
- Başarısız giriş denemelerinde **hesap kilitleme** (5 deneme / 15 dk) ve Redis tabanlı rate limiting

## 3. Transport & Şifreleme

- Tüm trafik **TLS 1.2+** zorunlu (HTTP -> HTTPS otomatik yönlendirme, HSTS header)
- Veritabanı bağlantıları `sslmode=require` ile şifreli
- Redis bağlantısı prod ortamında TLS + AUTH şifreli
- Hassas alanlar (webhook secret, banka API anahtarları) veritabanında **AES-256** ile şifreli saklanır (uygulama seviyesinde encryption, .NET `IDataProtector` API'si kullanılabilir)
- Secret yönetimi: Geliştirmede `dotnet user-secrets`, prod ortamında **Azure Key Vault / HashiCorp Vault / AWS Secrets Manager**; secret'lar **asla** appsettings.json'a düz metin yazılmaz veya git'e commit edilmez

## 4. Girdi Doğrulama & Enjeksiyon Koruması

- Tüm girdiler FluentValidation ile doğrulanır (bkz. [04-validasyon.md](./04-validasyon.md))
- EF Core / NHibernate parametreli sorgular kullanır; **hiçbir yerde raw SQL string concatenation yapılmaz**
- HTML/JS enjeksiyonuna karşı çıktı encode edilir (React varsayılan olarak XSS'e karşı korumalıdır; `dangerouslySetInnerHTML` kullanımı yasaktır)
- Dosya yükleme özelliği varsa (ileri faz), dosya tipi/boyutu/virüs taraması (ClamAV) zorunlu

## 5. Idempotency ve Çift İşlem Koruması

- Her ödeme isteği `Idempotency-Key` header'ı taşımak zorundadır (bkz. [06-cache-redis.md](./06-cache-redis.md) §6)
- Aynı key ile farklı bir istek gövdesi gelirse `409 Conflict` döner

## 6. Rate Limiting & DDoS Koruması

- ASP.NET Core `RateLimiter` middleware, merchant/IP bazlı sliding window limit
- Reverse proxy (Nginx/Cloudflare) seviyesinde temel DDoS koruması
- Anormal işlem paternleri (kısa sürede çok sayıda farklı kart denemesi) için **fraud detection** kuralları (ileri faz — basit kural motoru: aynı IP'den dakikada N'den fazla farklı kart denemesi -> otomatik blok)

## 7. Audit ve İzlenebilirlik

- Her finansal işlem ve yetkilendirme değişikliği `audit_logs` tablosuna **immutable (append-only)** olarak yazılır (bkz. [03-veritabani-tasarimi.md](./03-veritabani-tasarimi.md) §3.10)
- Audit kayıtları normal kullanıcı rolleri tarafından değiştirilemez/silinemez; sadece `SystemAdmin` salt-okunur görüntüleyebilir
- Correlation-Id tüm loglar, event'ler ve audit kayıtları arasında uçtan uca izlenebilirlik sağlar

## 8. KVKK / GDPR Uyumu

- Kişisel veri minimizasyonu: Sadece iş amacı için gerekli veri toplanır
- Açık rıza metinleri ve aydınlatma metni frontend'de kayıt/işlem öncesi gösterilir
- Veri saklama süresi: Finansal kayıtlar yasal saklama süresi (Türkiye'de genellikle 10 yıl, Vergi Usul Kanunu'na göre) boyunca tutulur; süre sonunda anonimize edilir
- "Unutulma hakkı" talebinde kişisel tanımlayıcı alanlar (ad soyad, iletişim) anonimize edilir, finansal tutarlar denetim amacıyla korunur

## 9. Güvenlik Testleri

- Statik kod analizi: SonarQube/SonarCloud, `dotnet format` + analyzer kuralları
- Bağımlılık tarama: `dotnet list package --vulnerable`, `npm audit`, Dependabot/Renovate otomatik güncelleme PR'ları
- Periyodik penetrasyon testi (yılda en az 1 kez, harici bağımsız firma ile) — canlıya çıkmadan önce zorunlu
- OWASP Top 10 kontrol listesi CI pipeline'ında otomatikleştirilmiş taramalarla (ör. OWASP ZAP baseline scan) desteklenir

## 10. Güvenli Geliştirme Yaşam Döngüsü (SDLC)

- Her PR için zorunlu code review (en az 1 onay)
- Secret tarama pre-commit hook (`gitleaks` veya `truffleHog`)
- CI pipeline'ında güvenlik testleri geçmeden merge engellenir (branch protection rules)

## 11. Sorumlu Açıklama (Responsible Disclosure)

Güvenlik açığı bildirimleri için `security@sanalpos.com` gibi bir iletişim kanalı ve temel bir "security.txt" / bug bounty politikası tanımlanması önerilir.

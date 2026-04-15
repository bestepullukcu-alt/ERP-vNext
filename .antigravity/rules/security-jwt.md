---
description: "SEC-001 — Diten ERP vNext JWT Standartları, Kimlik Doğrulama ve Yetkilendirme Kuralları"
---

# Güvenlik — JWT Kuralları (Diten ERP vNext)

Bu doküman, sistem genelindeki tüm mikroservislerin (Auth, MDM vb.) kimlik doğrulama (Authentication) ve yetkilendirme (Authorization) mekanizmalarını nasıl kurgulayacağını belirler.

## 🛡️ Kimlik Doğrulama Standartı (Authentication)

Diten ERP vNext, merkezi olmayan (Decentralized) bir doğrulama yapısı kullanır.

- **Bağımsız Doğrulama:** Her servis, gelen isteği kendi içinde `JwtBearer` middleware'i ile doğrulamalıdır. Gateway'in doğrulamış olmasına güvenilerek servis içi güvenlik bypass edilemez.
- **Konfigürasyon:** Authority, Audience ve Secret gibi değerler asla kod içinde hardcoded (sabit) tutulamaz. Bunlar `appsettings.json` veya `Environment Variables` üzerinden (Placeholder kullanımı ile) yönetilmelidir.
- **JWT Şeması:** Her zaman standart `Bearer {token}` şeması kullanılmalıdır.



---

## 🚦 Yetkilendirme Kuralları (Authorization)

Sistemde "Varsayılan Olarak Yasak" (Default Deny) prensibi geçerlidir.

- **Güvenli Endpointler:** Tüm `POST`, `PUT`, `PATCH` ve `DELETE` endpoint'leri varsayılan olarak `[Authorize]` attribute'u ile korunmalıdır. Bir endpoint'in anonim erişime açılması için (Örn: `/health`) açıkça talep veya özel mimari izin gereklidir.
- **Permission-Based Access:** Sadece giriş yapmış olmak yetmez; her işlem kullanıcının sahip olduğu `Permission` (Yetki Anahtarı) ile denetlenmelidir (Örn: `[HasPermission("Modules.SampleModule.Delete")]`).
- **Tenant İzolasyonu:** JWT içindeki `TenantId` claim'i, istek başlığındaki `X-Tenant-Id` ile eşleşmelidir. Bu, `debugger` ve `security-agent` tarafından denetlenir.

---

## 🚫 Güvenlik Yasakları (Critical Bans)

1. **Loglama Yasağı:** Token içeriği (Secret), JWT stringi veya veritabanı Connection String'leri asla log dosyalarına yazdırılamaz.
2. **Hardcoded Secrets:** Geliştirme (Dev) ortamında dahi olsa, `signingKey` gibi hassas veriler kodun içine gömülemez.
3. **Zayıf Algoritma:** Sadece güvenli ve güncel algoritmalar (Örn: `HMAC SHA256`) kullanılmalıdır.

---

## 🔗 Servisler Arası Güvenlik (Downstream)

Gateway'den servise akan isteklerde Token'ın düşmemesi (Token Passthrough) sağlanmalıdır. Ocelot veya HttpClient çağrılarında Bearer Token bir sonraki katmana güvenli bir şekilde aktarılmalıdır.



---

## ✅ Kontrol Listesi
- [ ] Servis `AddJwtBearer` konfigürasyonuna sahip mi?
- [ ] Değiştirme (Write) işlemlerinde `[Authorize]` mevcut mu?
- [ ] Hassas veriler loglardan arındırıldı mı?
- [ ] Konfigürasyonlar `Environment` üzerinden mi okunuyor?
- [ ] `X-Tenant-Id` ile JWT içindeki `TenantId` uyumlu mu?

---
Diten ERP vNext Security & JWT Standard - SEC-001
---
description: Diten ERP vNext API tasarımı, isimlendirme standartları ve HTTP hata yönetimi kuralları.
---

# API Konvansiyonları (Diten ERP vNext)

Bu doküman, tüm mikroservisler (MDM, Auth vb.) ve Gateway katmanı için geçerli olan ortak API tasarım anayasasıdır.

## 🛣️ Routing (Yönlendirme) Standartları

### 1. Mikroservis İçi (Downstream)
- **Format:** `/api/v1/[resource]`
- **İsimlendirme:** Kebab-case ve Çoğul (Plural) isimler kullanılmalıdır.
- *Doğru:* `/api/v1/legal-entities`, `/api/v1/countries`
- *Yanlış:* `/api/GetCountries`, `/api/v1/Country`

### 2. Gateway Üzerinden (Upstream)
- Frontend her zaman Gateway portu (`5000`) üzerinden konuşur.
- **Format:** `/:service-name/api/v1/:resource`
- *Örnek:* `http://localhost:5000/mdm/api/v1/countries`

---

## 🚦 HTTP Status Codes (Durum Kodları)

| Kod | Durum | Diten Uygulama Kuralı |
| :--- | :--- | :--- |
| **200** | OK | Başarılı okuma, güncelleme veya silme işlemleri. |
| **201** | Created | Başarılı yeni kayıt oluşturma (Header'da `Location` dönülmeli). |
| **204** | No Content | Başarılı işlem sonrası dönülecek veri yoksa. |
| **400** | Bad Request | **Kritik:** Validation hataları veya eksik/geçersiz `X-Tenant-Id` header'ı. |
| **401** | Unauthorized | Geçersiz veya süresi dolmuş JWT (Bearer Token). |
| **403** | Forbidden | Token geçerli ama kullanıcının bu işlem için yetkisi (Permission) yok. |
| **404** | Not Found | Kayıt yok. **Önemli:** Başka bir tenant'a ait ID istendiğinde 403 yerine güvenlik için 404 dönülmelidir (Obscurity). |

---

## 🛡️ Hata ve Yanıt Standardı (Error Handling)

### 1. ProblemDetails Standardı
Tüm hata yanıtları RFC 7807 (ProblemDetails) formatında dönülmelidir.
- **Title:** Hatanın kısa özeti (L10n Key olabilir).
- **Status:** HTTP Status Code.
- **Detail:** Teknik olmayan, açıklayıcı mesaj.
- **Extensions:** Varsa `traceId` veya `validationErrors` listesi.

### 2. Multi-Tenant Güvenliği
- Hiçbir API yanıtı (Error dahil) teknik stack trace veya hassas sistem bilgisi içermemelidir.
- Kiracı bazlı izolasyon sızıntısı (Cross-tenant leak) riskine karşı, veritabanı sorgu sonucu `null` dönerse doğrudan 404 fırlatılmalıdır.

---

## 📦 Request / Response Standartları

- **JSON Naming:** Her zaman `camelCase` (Örn: `taxNumber`).
- **GUID Zorunluluğu:** ID alanları ve `TenantId` her zaman GUID formatında string/object olmalıdır.
- **Null Değerler:** JSON yanıtlarında `null` dönen alanlar (eğer opsiyonel ise) payload'ı küçültmek için yanıttan çıkarılabilir (Ignore Null Values).
- **Boş Listeler:** Veri yoksa `null` yerine boş array `[]` dönülmelidir.

---

## ✅ Kontrol Listesi
- [ ] Endpoint `/api/v1/` ile başlıyor mu?
- [ ] Kaynak isimleri çoğul mu?
- [ ] `X-Tenant-Id` kontrolü yapıldı mı?
- [ ] Hata durumunda `ProblemDetails` dönülüyor mu?
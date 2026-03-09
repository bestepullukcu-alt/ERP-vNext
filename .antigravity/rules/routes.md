---
description: "NET-001 — Diten ERP vNext Gateway Routing, Path Naming ve Header Standartları"
---

# Route Naming Standard (Diten ERP vNext)

Bu doküman, tüm mikroservislerin Gateway (Ocelot) arkasındaki adresleme mantığını ve HTTP header kullanım standartlarını belirler.

## 🎯 Amaç
- Servisler arası iletişimde tek tip adresleme sağlamak.
- Case-sensitivity (Büyük/Küçük harf) kaynaklı 404 hatalarını engellemek.
- Multi-tenancy ve Auth bilgilerini standartlaştırmak.

---

## 🛣️ Upstream (Gateway - Port 5000) Standartları

Frontend veya dış servisler her zaman Gateway üzerinden konuşur. Tüm Upstream yolları **küçük harf (lowercase)** olmalıdır.

- **Genel Format:** `/services/<module>/{everything}`
- **<module>:** Servisin küçük harfle yazılmış kısa adı (örn: `mdm`, `auth`, `finance`).

**Örnekler:**
- **MDM Servisi:** `http://localhost:5000/services/mdm/api/v1/legal-entities`
- **Auth Servisi:** `http://localhost:5000/services/auth/api/v1/login`

---

## 🏁 Downstream (Internal - Port 5050/5056) Standartları

Gateway'in arkasındaki servislerin kendi içindeki adresleme yapısıdır.

- **API Prefix:** Her servis kendi endpoint'lerini `/api/v1/...` ile başlatmalıdır.
- **Health Check:** Sistem sağlığı takibi için her serviste `/health` endpoint'i bulunmalıdır. (Bu endpoint `X-Tenant-Id` zorunluluğu barındırmaz).



---

## 🛡️ Header Standartları

Tüm isteklerde aşağıdaki header'ların varlığı ve formatı denetlenmelidir:

1. **Multi-Tenant Header:**
   - `X-Tenant-Id`: Her zaman bir **GUID** olmalıdır.
   - Örn: `00000000-0000-0000-0000-000000000001`
2. **Auth Header:**
   - `Authorization`: `Bearer <JWT_TOKEN>` formatında olmalıdır.
3. **Correlation Header:**
   - `X-Correlation-Id`: İsteklerin servisler arası takibi için (Observability) zorunludur.

---

## 📍 Location Header Standardı (Proxy Awareness)

Bir servis `201 Created` döndüğünde, yanıtın `Location` header'ı kullanıcının erişebileceği **Gateway adresini** göstermelidir, servisin internal (5050) portunu değil.

- **Kural:** Her mikroservis kendi `appsettings.json` dosyasında bir `PublicBaseUrl` tanımına sahip olmalıdır.
- **Örnek (MDM):**
  `PublicBaseUrl = http://localhost:5000/services/mdm`
- **Sonuç:** Servis içinden `CreatedAtAction` çağrıldığında dönen URL şu şekilde olmalıdır:
  `http://localhost:5000/services/mdm/api/v1/legal-entities/{id}`

---

## 🚨 Önemli Notlar
- Gateway konfigürasyonunda (Ocelot) `ReRoute` tanımları yapılırken `UpstreamPathTemplate` alanı her zaman `/services/` ön ekiyle başlamalıdır.
- Servisler arası doğrudan (Internal) iletişimde dahi `X-Tenant-Id` header'ı asla düşürülmemeli, bir sonraki servise aktarılmalıdır.

---

## ✅ Kontrol Listesi
- [ ] Upstream path tamamen lowercase mi?
- [ ] Path `/services/` ile başlıyor mu?
- [ ] `X-Tenant-Id` header'ı GUID olarak tanımlandı mı?
- [ ] `Location` header gateway URL'ini gösteriyor mu?
- [ ] `/health` endpoint'i tanımlandı mı?

---
Diten ERP vNext Networking & Routing Standard - NET-001
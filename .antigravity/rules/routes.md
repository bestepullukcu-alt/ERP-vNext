---
description: "NET-001 — Diten ERP vNext Gateway Routing, Path Naming ve Header Standartları"
---

# Route Naming Standard (Diten ERP vNext)

Bu doküman, tüm mikroservislerin Gateway (Ocelot) arkasındaki adresleme mantığını ve HTTP header kullanım standartlarını belirler.

Port seçimi `AGENTS.md` port şeması ve ilgili domain config'e göre yapılır. Hardcoded MDM `5050` varsayımı yeni modüller için geçerli değildir. Örnekler: Auth `5056`, Platform `5057`, DevEnablement `5058`.

## 🎯 Amaç
- Servisler arası iletişimde tek tip adresleme sağlamak.
- Case-sensitivity (Büyük/Küçük harf) kaynaklı 404 hatalarını engellemek.
- Multi-tenancy ve Auth bilgilerini standartlaştırmak.

---

## 🛣️ Ocelot Gateway Rota Stratejisi

Servisler için iki tür rota mevcuttur ve ikisi **aynı anda** `ocelot.json` içinde yaşayabilir:

### Strateji A — Explicit Modül Rotaları (Öncelikli)
Bilinen her modül için **explicit** (açık) Upstream/Downstream çifti eklenir. Bu rotalar Ocelot'ta **önce** eşleşir.

```json
{
  "DownstreamPathTemplate": "/api/v1/{resource}",
  "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": "{servicePort}" }],
  "UpstreamPathTemplate": "/api/{resource}",
  "UpstreamHttpMethod": ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"]
}
```

**Örnek (resource modülü):**
```json
{
  "DownstreamPathTemplate": "/api/countries",
  "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": "{servicePort}" }],
  "UpstreamPathTemplate": "/api/countries",
  "UpstreamHttpMethod": ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"]
},
{
  "DownstreamPathTemplate": "/api/countries/{everything}",
  "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": "{servicePort}" }],
  "UpstreamPathTemplate": "/api/countries/{everything}",
  "UpstreamHttpMethod": ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"]
}
```

> **Not:** Mevcut projedeki controller route'ları genelde `/api/{resource}` formatındadır (v1 ön eki olmadan). Yeni modüller bu formatla eklenir.
>
> **CORS Notu (KRİTİK):** Frontend, `Authorization` ve `X-Tenant-Id` gibi custom header'lar gönderdiğinde browser otomatik olarak **preflight `OPTIONS`** isteği atar. Bu yüzden `UpstreamHttpMethod` listesinde **`OPTIONS` mutlaka bulunmalıdır**, aksi halde DataTables gibi bileşenler “Ajax error (tn/7)” gösterebilir.

### Strateji B — Catch-All Rota (Fallback)
Explicit tanımlanmamış istekler için fallback. `ocelot.json`'da **en sona** konumlandırılmalıdır.

```json
{
  "DownstreamPathTemplate": "/{everything}",
  "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": "{servicePort}" }],
  "UpstreamPathTemplate": "/services/mdm/{everything}",
  "UpstreamHttpMethod": ["GET", "POST", "DELETE", "PUT", "PATCH", "OPTIONS"]
}
```

---

## 🔄 Frontend (JS) ↔ Gateway URL Formatı

Frontend DataTable sayfalarında iki API profili vardır:

### Profil 1 — `proxy-profile` (Platform/admin MVC default)
Browser JS aynı origin'deki frontend proxy'yi çağırır. MVC controller, HttpOnly `access_token` cookie'sini server-side okuyup Gateway'e `Authorization: Bearer` olarak aktarır.

```javascript
// ✅ Doğru (browser network: frontend 5001)
const endpoint = '/Platform/ModuleCatalog/api';
fetch(`${endpoint}?page=1&pageSize=10`, { headers: getAuthHeaders() });
```

Bu profilde browser JS içinde `document.cookie`, `access_token`, `Authorization: Bearer` veya `window.API.platform + '/api/...'` kullanılmaz.

### Profil 2 — `direct-gateway-profile`
Sadece browser-safe token/header stratejisi olan tenant/public shell ekranlarında `window.API.{service}` kullanılır:

```javascript
// ✅ Doğru
ajax: { url: apiUrl + '/api/countries' }

// ❌ Yanlış
ajax: { url: apiUrl + '/services/mdm/api/v1/countries' }
```

Her iki profilde de browser JS mikroservis portlarına (`5056`, `5057`, `5058`) doğrudan gitmez.

---

## 🛡️ Header Standartları

Tüm isteklerde aşağıdaki header'ların varlığı ve formatı denetlenmelidir:

1. **Multi-Tenant Header:**
   - `X-Tenant-Id`: Her zaman bir **GUID** olmalıdır.
   - Örn: `00000000-0000-0000-0000-000000000001`
2. **Auth Header:**
   - Gateway'e giden servis çağrısında `Authorization`: `Bearer <JWT_TOKEN>` formatında olmalıdır.
   - HttpOnly cookie kullanan MVC shell modüllerinde bu header browser JS tarafından değil, frontend proxy tarafından server-side eklenir.
3. **Correlation Header:**
   - `X-Correlation-Id`: İsteklerin servisler arası takibi için (Observability) zorunludur.

---

## 📍 Location Header Standardı (Proxy Awareness)

Bir servis `201 Created` döndüğünde, yanıtın `Location` header'ı kullanıcının erişebileceği **Gateway adresini** göstermelidir.

- **Kural:** Her mikroservis kendi `appsettings.json` dosyasında bir `PublicBaseUrl` tanımına sahip olmalıdır.
- **Örnek:** `PublicBaseUrl = http://localhost:5000`

---

## 🚨 Ocelot Rota Ekleme Kuralları

Yeni modül eklendiğinde `integration-agent` şu adımları takip eder:
1. `ocelot.json`'a **explicit** iki rota ekle: `/{resource}` ve `/{resource}/{everything}`
2. `UpstreamHttpMethod`'da `PATCH` ve **`OPTIONS`** dahil tüm metodları listele (CORS preflight için `OPTIONS` zorunludur)
3. Explicit rotalar, catch-all rotadan (`/services/mdm/{everything}`) **ÖNCE** gelecek şekilde sırala
4. Portu domain'e göre seç: Auth → `5056`, Platform → `5057`, DevEnablement → `5058` (`ports.md` / `AGENTS.md` referans alınır)

---

## ✅ Kontrol Listesi
- [ ] Explicit Upstream/Downstream çifti eklendi mi? (her modül için 2 rota)
- [ ] `PATCH` HTTP metodu dahil mi?
- [ ] `OPTIONS` HTTP metodu dahil mi? (CORS preflight)
- [ ] Explicit rotalar catch-all'dan önce mi?
- [ ] `X-Tenant-Id` header'ı GUID olarak tanımlandı mı?
- [ ] Frontend JS doğru API profilini kullanıyor mu? Platform/admin için `/{AreaName}/{ModuleName}/api` same-origin proxy; direct-gateway için `window.API.{service} + '/api/{resource}'`.
- [ ] `/health` endpoint'i tanımlandı mı?

---
Diten ERP vNext Networking & Routing Standard - NET-001

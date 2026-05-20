---
description: "FRONT-PLATFORM-SEARCH — Platform Admin Ctrl+K Search Registry and Localization Standard"
---

# Platform Global Search Registry (Ctrl+K)

Bu kural yalnızca **Platform/Admin shell** modülleri içindir. Account, tenant, public veya legacy shell modülleri bu standarda dahil değildir; onlar için ayrı bir search standardı onaylanmadan bu dosya genişletilmez.

## 1. Scope

Bu kural aşağıdaki durumlarda zorunludur:

- Module pack `shell: platform-admin` diyorsa.
- View/controller yüzeyi `Views/Platform/{ModuleName}/` veya `/Platform/...` route'u üretiyorsa.
- Modül `_LayoutPlatformAdmin.cshtml` içinde menü veya navigasyon yüzeyi alıyorsa.

Şunlar Ctrl+K registry'ye eklenmez:

- Account, tenant, public veya legacy shell sayfaları.
- Backend-only altyapılar: lookups, quotas, event bus, background jobs, secrets, internal provisioning işleri.
- Dynamic `{id}` / GUID isteyen detail/edit sayfaları.
- Internal API endpoint'leri.
- Audit, docs, module-pack veya engineering artifact linkleri.
- Hidden/internal admin endpoint'leri.

## 2. Registry Contract

Platform Ctrl+K registry'nin hedef modeli iki dilli statik JSON'dur:

- `frontend/Diten.Web/wwwroot/assets/json/platform-search.en.json`
- `frontend/Diten.Web/wwwroot/assets/json/platform-search.tr.json`

Her iki dosya aynı teknik route setini taşır. `url` ve `icon` çevrilmez; `name`, `group` ve `keywords` ilgili dile göre yazılır.

Zorunlu item alanları:

```json
{
  "name": "Module Catalog",
  "icon": "bx-grid-alt",
  "url": "/Platform/ModuleCatalog",
  "group": "Catalog",
  "keywords": ["modules", "catalog", "pages", "assignments"]
}
```

Zorunlu üst seviye shape:

```json
{
  "navigation": {
    "Catalog": []
  },
  "suggestions": {
    "Catalog": []
  }
}
```

## 3. URL and Content Rules

- `url` değeri her zaman `/Platform/` ile başlar.
- `.html`, demo template, docs veya audit linki kullanılamaz.
- Stable list/index route'ları eklenir.
- Stable create route'u varsa ve kullanıcıya açık ise eklenebilir.
- Dynamic detail/edit route'ları (`/{id}`, `?id=`, GUID gerektiren linkler) eklenmez.
- `keywords` route arama terimlerini ve kullanıcı dilindeki doğal terimleri kapsamalıdır.
- Türkçe registry'de gerekirse İngilizce teknik terimler ek keyword olarak korunabilir; örn. `["kiracı", "tenant", "müşteri"]`.

## 4. L10n Standard

Ctrl+K modal UI metinleri hardcoded İngilizce bırakılamaz. Platform shell için modal metinleri `window.L10n` üzerinden çözülür:

- Placeholder: `Search [CTRL + K]` / `Ara [CTRL + K]`
- No results: `No results found` / `Sonuç bulunamadı`
- Cancel/escape metinleri ve görünen yardımcı metinler

Platform search sonuçları `en` ve `tr` dillerinde eksiksiz olmalıdır. Platform için aktif localization hedefi `en/tr`'dir; tenant/account search standardı onaylanana kadar 7 dil zorunluluğu bu registry'ye uygulanmaz.

## 5. Workflow Gate

Yeni Platform/Admin UI modülü teslim edilirken:

- İki search JSON dosyasına da aynı route seti eklenir.
- İngilizce ve Türkçe `name`, `group`, `keywords` alanları doldurulur.
- `suggestions` sadece en önemli kısa yolları taşır; her navigation item suggestion olmak zorunda değildir.
- Runtime smoke testte Ctrl+K iki dilde açılır, yeni modül adı/grup/keyword araması sonuç döndürür ve sonuç doğru route'a gider.

Eğer uygulama kodu henüz iki dilli `platform-search.{culture}.json` dosyalarını yüklemiyorsa, yeni Platform modülü tesliminde bu durum blocker olarak raporlanır. Onaylı ayrı bir frontend migration olmadan sadece legacy `platform-search.json` güncellenerek "Ctrl+K localization tamam" denilemez.

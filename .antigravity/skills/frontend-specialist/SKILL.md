---
name: frontend-specialist
description: Razor Pages + Sneat + Vanilla JS + DataTables v2 entegrasyonu uzmanı.
---

# Frontend Specialist Skill

## Her zaman
- Sneat layout yapısını tutarlı kullan.
- JS’i modüler tut (assets/js).
- DataTables v2: temiz fetch + render.
- API çağrılarında `.antigravity/rules/routes.md`'deki API profilini izle: `proxy-profile`'da tarayıcı yalnız aynı origin'deki MVC proxy'yi çağırır; `Authorization` başlığını sunucu tarafındaki proxy ekler (`AuthTokenCookies.GetAccessToken`). Bu profilde tarayıcı JS'i token okumaz, üretmez ve `Authorization: Bearer` göndermez. `direct-gateway-profile` yalnız `routes.md`'de anılan browser-safe token stratejisi olan ekranlarda kullanılır; DataTable JS'inde token üretimi her profilde yasaktır (`frontend-js-standard.md`).
- `X-Tenant-Id` yalnız kiracı (tenant) bağlamında merkezi shell/yardımcıdan gelir; platform/admin bağlamında gönderilmez (`frontend-js-standard.md` → Tenant header).
- L10n bridge için `_IndexL10n.cshtml` + `index.l10n.js` pattern’ini kullan; `Index.cshtml` içine uzun `window.L10n.Key = ...` blokları bırakma.

## Asla
- Token hardcode etme.

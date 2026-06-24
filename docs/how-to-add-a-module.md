# Yeni Modül Nasıl Eklenir (Developer Rehberi)

> Bir modülün **account (tenant) sol menüsünde görünmesi + uçtan uca çalışması** için izlenecek adımlar.
> Son güncelleme: 2026-06-17.

---

## ⚡ ÖNCE BUNU OKU — Menü neden görünmüyor?

**Account sol menüsü ELLE yazılmış (static Razor markup), OTOMATİK DEĞİL.**

- Module Catalog'a kayıt eklemek, `IsNavigationVisible=true` yapmak, `ModulePageDescriptor` eklemek → **menüde GÖSTERMEZ.** Bu kayıtları menüye basan bir frontend kodu yok (tasarlandı ama yapılmadı — aşağıya bak).
- Bir menü linkinin görünmesi için **İKİ şart birden** gerekir:
  1. `_LayoutTenantShell.cshtml`'de o modül için **elle bir `<li>` bloğu** olmalı, ve
  2. Giriş yapan kullanıcının **o izni** olmalı (izin seed edilmiş + tenant'a entitlement verilmiş + köprü izni role yazmış).

> **"Dev modülü (GoldenReference) görünüyor ama benimki görünmüyor"** → çünkü dev örnekleri `_LayoutTenantShell.cshtml`'de **izin guard'ı OLMADAN** hardcoded. Gerçek modül `@if (Perms.Has("..."))` guard'lı `<li>` ekler; izin verilmemişse link gizli kalır (markup olsa bile).

### Blueprint'teki "nav menü modülü" nedir?
`ModulePageDescriptor` (PageCode, RoutePath, RequiredPermission, IsNavigationVisible, SortOrder taşır) — **otomatik/dinamik menü için TASARLANMIŞ** bir kayıt. Niyet: shell aktif + nav-visible descriptor'ları çekip kullanıcının izinlerine göre menüyü kursun. **Bugün bu YAPILMAMIŞ** (entity + API + DB var, ama menüyü kuran frontend loader yok). O yüzden menü elle yazılıyor. *(İleride bu loader yapılırsa Adım 9 — elle `<li>` — ortadan kalkar; catalog'a ekleyince otomatik gelir.)*

---

## ✅ Checklist (sırayla)

### Kimlik
1. **Canonical module ID al.** `MOD-xxxx` satırını `execution/registries/module-id-registry.md`'ye ekle, sonra fail-closed kontrol:
   `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-XXXX --name "Canonical Name"`
   Non-zero çıkış SENİ BLOKLAR. ID/isim otoritesi: `docs/System Capability & Implementation Blueprint - master 7.xlsx :: Blueprint_Data`. **ID uydurma.**
2. **Module pack hazırla (onaylı).** Alanlar, izin key'leri, gateway route planı, tenant-assigned mı platform-global mı, `form_field_count` + `golden_reference` (slim ≤8 / compact >8), UI shell hedefi. **Status `approved`/`ready-for-dev` olmadan kod yazma.** → `execution/domains/{domain}/module-packs/{ID}.md`

### Backend
3. **İzinleri seed et (ELLE).** `services/Diten.AuthService/.../Seed/DataSeeder.cs` `SeedPermissionsAsync`'e ekle: `new("yourmodule","resource","read")` + create/update/delete. Baseline rol grant'ları: `DefaultRolePermissionTemplate.cs`.
   ⚠️ İzin modül string'i (lowercase) catalog `ModuleCode` ile (normalize sonrası) **EŞLEŞMELİ**, yoksa entitlement→izin köprüsü **sessizce hiçbir şey vermez**. Pack-driven otomatik üretim YOK — bu manuel.
4. **Backend servisi.** Domain entity + repository (tenant izolasyon + soft-delete) + CQRS (Command/Query/Handler/Validator — ayrı dosyalar) + API controller (`Response<T>` envelope, `[Authorize(Policy=...)]`). Bkz. `.antigravity/workflows/add-endpoint-cqrs.md`.
5. **Gateway route ekle.** `gateway/Diten.ApiGateway/ocelot.json`'a **2 explicit entry**: `/api/{resource}` + `/api/{resource}/{everything}` (tüm HTTP metotları), doğru porta, **catch-all'dan ÖNCE**. Portlar: Platform **5057**, Auth **5056**, MDM **5004**, DevEnablement **5058**. Yanlış/eksik route → entitlement kontrolünden ÖNCE **404** ("bozuk" gibi görünür, "yetkisiz" değil). `ocelot.json` protected path. Örnek: mevcut `golden-reference-slim` çifti.

### Platform
6. **Module catalog item seed et.** `ModuleCatalogItem` (ModuleCode **UPPERCASE**, ModuleName, DisplayName, Status, `IsTenantAssignable`, SortOrder) → platform admin tenant'a atayabilsin. API: `POST /api/platform/module-catalog` (`ModuleCatalogController.cs`). (App startup'ta seed edilmiyor → API/migration ile.)
7. *(Opsiyonel)* Subscription plan ile geliyorsa, ModuleCode'u planın `IncludedModuleKeys` listesine ekle → plan otomatik entitlement versin.
8. **Tenant entitlement provisione et.** `POST /api/platform/tenants/{tenantId}/commercial/module-entitlements` (`TenantModuleEntitlementsController.cs`). Bu `TenantEntitlementAddedV1/EnabledV1` event'i yayınlar → AuthService'teki `EntitlementSyncConsumer` `GrantModuleAsync` çağırır → izinler **Admin (hepsi)** + **Viewer (read-only)** rollerine yazılır.
   ⚠️ **Bu adım olmadan kimsede izin yok → menü guard'ı her zaman false → link gizli.**

### Frontend / Menü
9. 🔴 **Menü linkini ekle.** `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` içinde `<ul class="menu-inner">` altına elle bir `<li class="menu-item">` bloğu, `@if (Perms.Has("yourmodule.resource.read"))` ile sar. **MENÜYÜ GÖSTEREN ADIM BUDUR.** Örnek: Access Governance bloğu (≈satır 211-261).
10. *(Varsa)* Modülü Management-Governance domain shell registry'sine ekle: `frontend/Diten.Web/Config/ManagementGovernanceRegistry.cs` (`Module(...)` factory entry). Bu, sol-nav'dan AYRI bir manuel liste — elle senkron tut.
11. **Razor UI'yi kur.** `Views/{Area}/{Module}/` (Index + create/edit/details, golden_reference slim|compact). Route attribute menü href'iyle eşleşmeli. Platform/admin modülde Ctrl+K search kaydı (`platform-global-search-registry.md`).
12. **Lokalizasyon ekle.** Dil başına `.resx` (tenant **7 dil**, platform **2 dil**). ⚠️ `.resx` dosya adı, Razor localization marker class adıyla **TAM eşleşmeli** (örn. `MyModuleIndex.en.resx`). Ortak string'ler `SharedResource`'tan; modül string'leri modül `.resx`'inde.
13. **Test + verify.** xUnit (CRUD + tenant-izolasyon + soft-delete) + `/tenant-audit` (izin sızıntısı) + smoke (sayfa yükleniyor, DataTable render, l10n çözülüyor, menü linki görünüyor). ⚠️ `.cshtml` değişikliği **`dotnet build` ister** (runtime-compile kapalı; JS/CSS sadece hard-refresh ister).

---

## ⚠️ En sık hatalar (bunları atlama)
1. **Catalog'a/ModulePageDescriptor'a eklemek menüyü GÖSTERMEZ.** Elle `<li>` şart (Adım 9). Dinamik menü loader yok.
2. **İzin seed ettim ama görünmüyor** → entitlement vermedin (Adım 8). İzin TANIMI var ama hiçbir role atanmamış → `Perms.Has()` hep false.
3. **ModuleCode ≠ izin modül string'i** → entitlement atandı ama **sıfır izin** akıyor (köprü sessizce no-op). (`ModulePermissionResolver` override map'i boş.)
4. **Gateway route yanlış/eksik/catch-all'dan sonra/yanlış port** → **404** (entitlement kontrolünden önce).
5. **Registry drift** → `ManagementGovernanceRegistry.cs` + `_LayoutTenantShell.cshtml` + Platform catalog üç ayrı manuel liste, senkron kontrolü yok. Catalog'da "assignable" ama menüde yok = hayalet modül.
6. **`.cshtml` değiştirdim görünmüyor** → `dotnet build` etmedin (runtime-compile kapalı).
7. **`.resx` adı marker class'la eşleşmiyor** → key'ler çözülmez, sayfa ham key gösterir.

---

## TL;DR
**Modül çalışsın + menüde görünsün için:** canonical ID → module pack → **izin seed (Adım 3)** → backend → **gateway route (Adım 5)** → catalog item → **tenant entitlement (Adım 8 = izin role yazılır)** → **menü `<li>` ekle (Adım 9 = link görünür)** → UI → l10n → test + **`dotnet build`**.
İki kritik unutulan: **Adım 8** (izin role gelmiyor) ve **Adım 9** (menü elle eklenir, otomatik değil).

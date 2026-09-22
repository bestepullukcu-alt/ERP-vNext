# WORK PACKAGE — WP-ST-DETAIL-2 · Detay sayfası: golden restyle + salt-okunur KAPSAM + sürüm-geçmişi paneli + "Kampanyada kullan" (frontend + Web proxy)

> **CT (SoR).** MOD-0167-FU04, Faz 3 Detay (frontend yarısı). Branch `feature/crm-scmm-studio` (`2f3c20a0` üstü — WP-ST-DETAIL-1 §37 sonrası). DETAIL-1'in sürüm-geçmişi endpoint'ini tüketir. **Frontend (Details.cshtml + details.js + strategy-create.css? + _IndexL10n + 7 resx) + Web proxy (StrategyTemplatesController GET).** CrmService/domain DEĞİŞMEZ (DETAIL-1 bitti).

## Kanıt
- **Details.cshtml (303 satır):** `row g-4` > sol `col-12 col-lg-8` (Identity/Segment/Frequency/Product/Content section'ları, `card backbone-preview-section mb-4` + `h6 text-uppercase text-heading fw-semibold` — **dt-card-icon YOK**) + sağ `col-12 col-lg-4` (261; mevcut metadata kartı: EffectiveFrom/To/UpdatedAt + ApplyIsMicroTargetHelp). **KAPSAM/scope section YOK.** Header aksiyonları Edit/Activate/NewVersion/Archive (data-action). Scripts: details.js.
- **Detay VM scope alanları MEVCUT** (StrategyTemplateViewModels.cs:28-40): `ScopeType`/`EffectiveScopeType`/`CountryScope`/`LegalEntityId`/`BusinessUnitId` → read-only KAPSAM bunlardan render edilir.
- **details.js:** `endpoint='/CRM/StrategyTemplates/api'`; `envelope`/`post` helper; `[data-action]` (activate/archive/new-version). Sürüm fetch YOK.
- **Web proxy deseni:** `api/templates/{templateId:guid}` (196) + `/bindings` (201) `ProxyGetAsync(..., ReadPermission, ct, ReadFallback)`. `/versions` aynı desende eklenir (DETAIL-1 endpoint'i; gateway `{everything}` kapsıyor).
- **Golden ikon standardı (EDIT-X):** Identity bx-purchase-tag-alt / KAPSAM bx-map / Segment bx-group / Frekans bx-time-five / Ürün bx-package / İçerik bx-book-content. Sürüm geçmişi → bx-history (iconify'da doğrula).
- **Kampanyada kullan:** Campaign roadmap adım 4 (henüz akışta yok). Mockup'ta link; hedef yok → **disabled placeholder** (Campaign gelince bağlanır).

## NE (frontend + Web proxy; CrmService DEĞİŞMEZ)
1. **Web proxy:** `[HttpGet("api/templates/{templateId:guid}/versions")]` → `ProxyGetAsync($"/api/crm/strategy-templates/{templateId}/versions", ReadPermission, ct, ReadFallback)` (mevcut bindings deseni).
2. **Details.cshtml — golden restyle:** her sol section h6'sına başa `<i class="bx bx-<icon> dt-card-icon" aria-hidden="true"></i>` + h6 `d-flex align-items-center gap-2` (EDIT-X ikonları). Sağ metadata kartı başlığı da (varsa h6) golden'a hizala (ikon bx-info-circle).
3. **Salt-okunur KAPSAM section (yeni):** sol sütuna, Kimlik'ten sonra, `card backbone-preview-section mb-4` + golden h6 (bx-map + `ScopeSection` "KAPSAM — Nerede") + `dl.row` salt-okunur: Kapsam seviyesi (ScopeType/EffectiveScopeType friendly), Ülke (CountryScope), Legal Entity (LegalEntityId → varsa çözümlenmiş ad, yoksa ham/—), İş birimi (BusinessUnitId). Değer yoksa "—". (VM alanlarından; yeni endpoint gerekmez.)
4. **Sürüm geçmişi paneli (sağ sütun):** yeni `card` (golden h6 bx-history + `VersionHistory` "Sürüm geçmişi") + `<div id="stVersionHistory">` (details.js doldurur). Boşken/yüklenirken kısa metin.
5. **"Kampanyada kullan" (sağ sütun):** `<button class="btn btn-outline-primary w-100 disabled" disabled aria-disabled="true">@Localizer["UseInCampaign"] ↗</button>` + küçük not `UseInCampaignSoon` ("Kampanya entegrasyonu ile gelecek"). (Campaign adım 4'te aktifleştirilir — F-ST-USE-IN-CAMPAIGN.)
6. **details.js — sürüm geçmişi fetch+render:** sayfa yüklenince (Detay), `#stVersionHistory` varsa `fetch(`${endpoint}/templates/${id}/versions`)` (envelope → `.versions`); her sürüm satırı: **vN** + durum rozeti (published/active yeşil, draft gri, archived gri) + tarih (ActivatedAt||CreatedAt) + **IsCurrent** işareti ("geçerli"). newest-first (endpoint zaten DESC). Hata→kısa hata metni (uydurma yok). id sayfadan (data-id veya URL). data-action akışı DEĞİŞMEZ.
7. **resx (7 dil) + L köprüsü:** `ScopeSection` (varsa reuse), `VersionHistory`, `UseInCampaign`, `UseInCampaignSoon`, `CurrentVersion` ("geçerli"), durum etiketleri (reuse ContentStatus* veya yeni Vault). details.js'in okuduğu anahtarları `_IndexL10n` whitelist + index.l10n.js köprüsüne ekle. PascalCase köprü.

## KORU / YAPMA
- **CrmService/domain/contract/gateway/ocelot DEĞİŞMEZ** (DETAIL-1 bitti; yalnız Web proxy GET + frontend). Mevcut Detay section verileri (Identity/Segment/Frequency/Product/Content) + header aksiyonları (Edit/Activate/NewVersion/Archive, data-action) + metadata kartı DEĞİŞMEZ (yalnız h6'lara ikon + yeni KAPSAM/sürüm paneli eklenir). "Kampanyada kullan" **disabled placeholder** (gerçek nav YOK — Campaign yok). Uydurma sürüm/scope yok (VM/endpoint'ten). Liste/Edit/Create/form.js DOKUNMA. Tema/L10n köprüsü.
- **DUR:** `/versions` proxy 4xx/5xx dönüyorsa panel boş+hata metni (uydurma yok) — DUR gerekmez; ama scope alanları VM'de beklenenden farklıysa/KAPSAM render veri kaybediyorsa → DUR+raporla.

## Acceptance
- **E2:** Diten.Web.Tests 201/0. git diff: Details.cshtml + details.js + StrategyTemplatesController.cs (Web) + _IndexL10n + index.l10n.js + 7 resx (+css opsiyonel). **CrmService/domain/gateway/liste/Edit/Create/form.js diff YOK.**
- **E4 (FLEET RESTART):** Detay'da tüm section başlıkları golden ikon (dt-card-icon) + KAPSAM salt-okunur bölümü; sağda **Sürüm geçmişi** (vN + durum + tarih + "geçerli", newest-first — canlıda en az 1 sürüm) + **"Kampanyada kullan"** disabled placeholder; mevcut aksiyonlar (Edit/Activate/NewVersion/Archive) çalışır. **Razor+js+resx → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-ST-DETAIL-2 · Detay sayfası golden restyle + salt-okunur KAPSAM + sürüm-geçmişi paneli + Kampanyada kullan (MOD-0167-FU04 Faz3; frontend + Web proxy)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 2f3c20a0 üstü · Worktree: ana checkout

DETAIL-1 sürüm-geçmişi endpoint'i (GET /api/crm/strategy-templates/{id}/versions) hazır. CrmService/domain/gateway DEĞİŞMEZ (yalnız Web proxy GET + frontend).

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-DETAIL-2-detail-page-frontend.md · frontend/Diten.Web/Views/CRM/StrategyTemplates/Details.cshtml (sol col-lg-8 section'lar 55-259; sağ col-lg-4 metadata 261+; header aksiyon data-action) · frontend/Diten.Web/wwwroot/assets/js/CRM/StrategyTemplates/details.js (envelope/post/data-action) · frontend/Diten.Web/Controllers/CRM/StrategyTemplatesController.cs (api/templates/{id} :196 + /bindings :201 ProxyGetAsync deseni) · frontend/Diten.Web/Models/CRM/StrategyTemplateViewModels.cs (Detail scope 28-40) · _IndexL10n.cshtml + wwwroot/assets/js/CRM/StrategyTemplates/index.l10n.js · 7 resx · iconify-icons.css (ikon doğrula). EDIT-X ikonları: Identity bx-purchase-tag-alt/KAPSAM bx-map/Segment bx-group/Frekans bx-time-five/Ürün bx-package/İçerik bx-book-content/Sürüm bx-history.

NE (frontend + Web proxy; CrmService DEĞİŞMEZ):
 1) Web: [HttpGet("api/templates/{templateId:guid}/versions")] → ProxyGetAsync($"/api/crm/strategy-templates/{templateId}/versions", ReadPermission, ct, ReadFallback).
 2) Details.cshtml: her sol section h6'sına <i class="bx bx-<icon> dt-card-icon" aria-hidden="true"></i> + h6 d-flex align-items-center gap-2 (EDIT-X ikonları; iconify'da doğrula).
 3) Yeni salt-okunur KAPSAM section (Kimlik'ten sonra, sol): card backbone-preview-section mb-4 + golden h6 (bx-map + ScopeSection) + dl.row: Kapsam seviyesi (ScopeType/EffectiveScopeType), Ülke (CountryScope), Legal Entity (LegalEntityId), İş birimi (BusinessUnitId); yoksa "—".
 4) Sağ sütun: yeni card (golden h6 bx-history + VersionHistory) + <div id="stVersionHistory"> (details.js doldurur, yüklenirken kısa metin).
 5) Sağ sütun: <button class="btn btn-outline-primary w-100 disabled" disabled aria-disabled="true">UseInCampaign ↗</button> + küçük not UseInCampaignSoon (placeholder; Campaign yok).
 6) details.js: Detay'da #stVersionHistory varsa fetch(`${endpoint}/templates/${id}/versions`) (envelope→.versions); satır = vN + durum rozeti (published/active yeşil, draft/archived gri) + tarih (ActivatedAt||CreatedAt) + IsCurrent "geçerli"; newest-first (endpoint DESC); hata→kısa metin. id sayfadan. data-action akışı DEĞİŞMEZ.
 7) resx 7 dil + L köprüsü: ScopeSection(reuse)/VersionHistory/UseInCampaign/UseInCampaignSoon/CurrentVersion + durum etiketleri; details.js'in okuduğu anahtarlar _IndexL10n + index.l10n.js'e; PascalCase köprü.
KORU/YAPMA: CrmService/domain/contract/gateway/ocelot DEĞİŞMEZ; mevcut section verileri + header aksiyonları (Edit/Activate/NewVersion/Archive) + metadata kartı DEĞİŞMEZ (yalnız h6 ikon + KAPSAM/sürüm paneli eklenir); Kampanyada kullan disabled placeholder (gerçek nav yok); uydurma yok; Liste/Edit/Create/form.js DOKUNMA; tema/L10n köprüsü.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0; git diff Details.cshtml+details.js+Controller+_IndexL10n+index.l10n.js+7 resx(+css); CrmService/domain/gateway/liste/Edit/Create/form.js diff yok. Ayrı commit ("feat(strategy): WP-ST-DETAIL-2 — Detay golden restyle + salt-okunur KAPSAM + sürüm-geçmişi paneli + Kampanyada kullan placeholder (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: scope alanları VM'de beklenenden farklıysa/KAPSAM render veri kaybediyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: 12e27183 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-stdetail2-verify @12e27183
```
- ✅ **Kapsam (11 dosya, +156/−7):** Web StrategyTemplatesController (proxy GET) + Details.cshtml + details.js + _IndexL10n + 7 resx. **CrmService/MdmService/domain/gateway/ocelot/Index/Create/Edit/_Form/_SidePanel/form.js TEMİZ** ✓ (Web controller ≠ CrmService controller).
- ✅ **Golden restyle:** 7 section h6'ya dt-card-icon (Identity bx-purchase-tag-alt/Segment bx-group/Frekans bx-time-five/Ürün bx-package/İçerik bx-book-content/Classification+Lifecycle bx-info-circle/KAPSAM bx-map). Salt-okunur KAPSAM section (79-93) VM alanlarından (EffectiveScopeType||ScopeType, CountryScope, LegalEntityId, BusinessUnitId; yoksa "—" — uydurma yok).
- ✅ **Sürüm geçmişi + Kampanyada kullan:** Web proxy `api/templates/{id}/versions` (209-212, ReadPermission+ReadFallback); details.js `#stVersionHistory` fetch → envelope `.versions` → vN+durum rozeti+tarih(ActivatedAt||CreatedAt)+IsCurrent "geçerli", newest-first, boş/hata kısa metin (Bootstrap utility, ekstra css yok). "Kampanyada kullan" btn-outline-primary **disabled** placeholder + UseInCampaignSoon (F-ST-USE-IN-CAMPAIGN, Campaign adım 4).
- ✅ **KORU=0:** mevcut section verileri + header aksiyonları (Edit/Activate/NewVersion/Archive data-action) + metadata kartı korundu; CrmService/gateway dokunulmadı; index.l10n.js generic (whitelist türetir) dokunulmadı.
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **201/0**.
- ⏳ E4: Detay golden + KAPSAM + sürüm geçmişi paneli. **FLEET RESTART.**

**WP-ST-DETAIL-2 KOMPLE (E2). FAZ 3 DETAY KAPANDI (DETAIL-1 backend + DETAIL-2 frontend). Roadmap adım 2 bitti. Sonraki: adım 3 — content chain (SCMM/MOD-0162 sayfaları İçerik ızgarasını besler: incele/test/düzelt).**

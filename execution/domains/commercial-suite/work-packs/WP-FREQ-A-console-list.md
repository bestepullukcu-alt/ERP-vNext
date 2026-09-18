# WORK PACKAGE — WP-FREQ-A · Visit Frequency Policy konsol iskeleti (tab + Golden Compact liste) (frontend+backend)

> **CT (SoR).** MOD-0165-FU03 VisitFrequencyPolicy. Branch `feature/scmm-content-studio` (`63374a8c` üstü). Owner: frekans politikaları için tab'lı konsol (Liste + Çözümleme), Golden Compact DataTable, "Yeni Politika" offcanvas editör (create+edit), row-action detay. **FU03 backend HAZIR** (contract/list/resolve/CRUD/activate/archive), **frontend YOK** — sıfırdan. Bu WP = **iskelet: nav + controller proxy + Index(2 tab) + Golden Compact liste + soft-delete backend**. Editör (FREQ-B) + detay/çözümleme (FREQ-C) ayrı.

## Referans desenler (proje standardı — birebir izle)
- **Golden Compact DataTable + offcanvas iskelet:** `Views/DevEnablement/GoldenReferenceSlim/` (Index.cshtml: ①_Filter ②BulkActionBar ③`_DataTable` [`<table id="dt-… data-dt-standard="v2">`] ④offcanvas partial'lar ⑤_IndexL10n) + `GoldenReferenceSlimController.cs`.
- **Tab console (Liste + Çözümleme):** `Views/CRM/EligibilityPolicies/Index.cshtml` (`nav nav-pills` + `data-bs-toggle="tab"` → tab-policies + tab-evaluate) + `EligibilityPoliciesController.cs` (Index + `ProxyGetAsync` deseni).
- **Backend:** `VisitFrequencyPoliciesController` (`api/crm/visit-frequency-policies` + `/contract` + `/resolve` + `{id}` + create/update/activate/archive). RBAC `crm.visit-frequency-policy.{read,manage,resolve}` (fallback crm.territory.read/model.manage).

## Kapsam
**1) Backend (soft-delete ekle — additive):**
- `EntityBase.IsDeleted` destekliyorsa (territory_nodes'ta var): VisitFrequencyPolicy'ye **soft-delete** komutu + endpoint `POST api/crm/visit-frequency-policies/{id}/delete` (IsDeleted=true, DeletedAt/By); list read'i `IsDeleted=false` filtreler. **Archive (status) KORUNUR — ayrı**: Arşivle=status archived (history görünür), Sil=soft-delete (listeden kalkar). EntityBase IsDeleted YOKSA → DUR+raporla (Archive'a düş).
**2) Frontend controller** (`VisitFrequencyPoliciesController`, EligibilityPolicies proxy deseni): `Index()` + proxy'ler → list / contract / resolve / create / update / activate / archive / delete. RBAC gate.
**3) Index.cshtml — tab console:** `nav nav-pills`: **[Liste]** (aktif) + **[Çözümleme]** (permission-gated, FREQ-C placeholder). tab-content: liste tab = _Filter + _DataTable + BulkActionBar; çözümleme tab = boş placeholder (FREQ-C). Offcanvas partial'lar için **boş placeholder** (FREQ-B/C dolduracak): `_CreateEditOffcanvas` (Yeni Politika/Düzenle) + `_DetailsQuickView` (detay).
**4) _DataTable (Golden Compact v2):** `<table id="dt-visitfrequencypolicies" data-dt-standard="v2">` — kolonlar: Code · Name · Hedef (TargetType humanize + ref) · Frekans (FrequencyType + RequiredVisitCount/PeriodType, ör "Ayda 4") · Priority · Status (bg-label badge) · Actions. Satır aksiyonları: **Detay** (row→_DetailsQuickView) · **Düzenle** (→_CreateEditOffcanvas) · **Aktifleştir/Arşivle** (status'e göre) · **Sil** (soft-delete). "Yeni Politika" butonu → _CreateEditOffcanvas.
**5) _Filter + datatable.js** (Golden Compact standardı: `dt-inline-filter-host` vb — GoldenReferenceSlim deseni). Server-side/client-side liste (backend list endpoint).
**6) Nav girişi** (CRM nav'a VisitFrequencyPolicies) + L10n (7 dil).

## KORU / YAPMA
- Backend contract/resolve/CRUD/activate/archive davranışı DEĞİŞMEZ (yalnız additive soft-delete). Golden Compact v2 DataTable standardı (dt-inline-filter-host class, data-dt-standard, updateVisualState desenleri — memory'deki gotcha'lar) birebir. Offcanvas editör/detay içeriği bu WP'de YOK (boş placeholder). Çözümleme paneli FREQ-C. Vocabulary hardcode YOK (contract'tan). Başka modül. Segment dosyalarına dokunma.

## Acceptance
- **E2:** Diten.Web.Tests + CrmService.Application.Tests baseline-diff sıfır-yeni-fail. /CRM/VisitFrequencyPolicies açılır: 2 tab (Liste aktif + Çözümleme placeholder); Golden Compact DataTable (kolonlar + satır aksiyonları + Yeni Politika); soft-delete endpoint (IsDeleted, list filtreler, Archive ayrı korunur); nav girişi; RBAC gate. Offcanvas'lar boş placeholder. git diff: yeni VisitFrequencyPolicies view'ları + controller + backend soft-delete + nav + L10n.
- **E4:** liste dolu gelir; Yeni Politika/Detay offcanvas açılır (boş, FREQ-B/C dolduracak); Sil soft-delete.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-FREQ-A · Visit Frequency Policy konsol iskeleti (MOD-0165-FU03, frontend+backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (63374a8c üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-A-console-list.md · Views/DevEnablement/GoldenReferenceSlim/ (Index+_DataTable+_Filter+_DetailsQuickView+_CreateEditOffcanvas+_IndexL10n) + GoldenReferenceSlimController.cs · Views/CRM/EligibilityPolicies/Index.cshtml (tab console) + EligibilityPoliciesController.cs (proxy) · services/.../Api/Controllers/CRM/VisitFrequencyPoliciesController.cs (backend endpoint'ler) + Domain/Entities/VisitFrequencyPolicy.cs + EntityBase.cs (IsDeleted var mı) · CRM nav kaydı (mevcut CRM console nav deseni).

NE:
 Backend (additive soft-delete): EntityBase.IsDeleted varsa VisitFrequencyPolicy soft-delete komut+handler + endpoint POST api/crm/visit-frequency-policies/{id}/delete (IsDeleted=true+DeletedAt/By); list read IsDeleted=false filtrele. Archive (status) KORUNUR/ayrı. IsDeleted YOKSA DUR+raporla.
 Frontend controller (VisitFrequencyPoliciesController, EligibilityPolicies proxy deseni): Index() + proxy list/contract/resolve/create/update/activate/archive/delete + RBAC gate.
 Index.cshtml tab console: nav-pills [Liste aktif]+[Çözümleme placeholder gated]; liste tab=_Filter+_DataTable+BulkActionBar; çözümleme tab=boş placeholder; offcanvas boş placeholder _CreateEditOffcanvas + _DetailsQuickView (FREQ-B/C dolduracak).
 _DataTable Golden Compact v2 (id="dt-visitfrequencypolicies" data-dt-standard="v2"): kolon Code/Name/Hedef(TargetType humanize+ref)/Frekans(type+count/period "Ayda 4")/Priority/Status(bg-label)/Actions; satır aksiyon Detay/Düzenle/Aktifleştir-Arşivle/Sil; "Yeni Politika"→_CreateEditOffcanvas. _Filter + datatable.js (Golden Compact: dt-inline-filter-host).
 Nav girişi + L10n 7 dil.
KORU/YAPMA: backend contract/resolve/CRUD/activate/archive DEĞİŞMEZ (yalnız additive soft-delete); Golden Compact v2 standardı (dt-inline-filter-host/data-dt-standard/updateVisualState gotcha'ları) birebir; offcanvas editör/detay/çözümleme İÇERİĞİ bu WP'de YOK (placeholder); vocabulary hardcode yok (contract); Segment dosyaları/başka modül DOKUNMA.
DOĞRULA (E2): Diten.Web.Tests + CrmService.Application.Tests baseline-diff sıfır-yeni-fail; console 2 tab + Golden Compact DataTable + satır aksiyon + Yeni Politika + soft-delete(IsDeleted list filtreler, Archive ayrı) + nav + RBAC; offcanvas placeholder; git diff yeni VisitFrequencyPolicies + controller + backend soft-delete + nav + L10n. Ayrı commit. §22 TÜRKÇE. K13.
Durma: EntityBase IsDeleted yoksa (soft-delete kurulamıyor); Golden Compact v2 standardı kurulamıyorsa; backend list/proxy bağlanamıyorsa; nav deseni belirsizse; kapsam VisitFrequencyPolicies dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → Diten.Web.Tests + CrmService.Application.Tests baseline-diff; console 2 tab; Golden Compact v2 DataTable (kolon+aksiyon+Yeni); soft-delete additive (IsDeleted list filtreler, Archive/status davranışı korundu); nav+RBAC; offcanvas placeholder (içerik yok); backend contract/resolve/CRUD değişmedi.

## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: a14dd389 · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-freqa-verify @a14dd389
```
- ✅ Scope: yeni VisitFrequencyPolicies frontend (Controller + Index + 5 view + 2 js + 7 resx) + backend additive soft-delete (Delete command/handler + entity DeletedBy) + CrmManifestProvider nav + 1 test. Segment/başka modül dokunulmadı.
- ✅ Backend additive: contract/resolve/CRUD/archive DEĞİŞMEDİ (yalnız Delete command/handler + DeletedBy). Soft-delete DISTINCT from Archive (Arşivle=status archived listede kalır; Sil=IsDeleted listeden+resolve-setinden düşer; hard delete yok). list/get/resolve zaten IsDeleted=false filtreliyor.
- ✅ Console: 2 tab (Liste aktif + Çözümleme resolve-gated placeholder); Golden Compact v2 DataTable (id=dt-visitfrequencypolicies, data-dt-standard=v2; dt-inline-filter-host + updateVisualState gotcha'ları korundu); kolon Code/Name/Hedef/Frekans/Priority/Status/Actions; satır aksiyon Detay/Düzenle/Arşivle/Sil + Yeni Politika; contract-driven filtre. _CreateEditOffcanvas + _DetailsQuickView boş placeholder (FREQ-B/C).
- ✅ Nav: CrmManifestProvider VISIT_FREQUENCY_POLICIES (sortOrder 180) + Nav.Page.* 7 dil; NavManifestL10nGuard yeşil.
- ✅ Build+test (CT izole, Release, GERÇEK): CrmService.Application.Tests **1749/0/5** (+4 soft-delete) + Diten.Web.Tests **137/0**.
- ℹ️ Kapsam kararları (kabul): activate satır-aksiyonu yok (FU03 backend'inde activate endpoint yok — KORU'ya uygun; status→active editörde FREQ-B); bulk yok (backend bulk-delete yok + EligibilityPolicies referansı da içermiyor); nav RequiredPermission=crm.territory.read fallback (canonical seed'lenene dek). verify_datatable_page.py fail = EligibilityPolicies ile aynı proxy/no-bulk baseline ailesi (WP kabul kapısı değil).
- ⏳ E4: /CRM/VisitFrequencyPolicies liste dolu; Yeni/Detay offcanvas placeholder; Sil soft-delete.

**FREQ-A iskelet KOMPLE. Sıra: FREQ-B (Create/Edit offcanvas editör — güncel mockup C:\tmp\mockup-freq.html).**

---
id: MOD-0164-FU03
name: Consent & Preference Admin UI
parent: MOD-0164
parent_name: Consent & Preference Management
domain: commercial-suite
service: frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
runtime_code_allowed: true
runtime_code_scope: "UI ONLY — frontend/Diten.Web Consent & Preference Admin UI, UI tests ve evidence. Diten.CrmService backend runtime, Gateway config, Auth seed/grant, registry ve Mongo değişikliği YASAK."
owner: module-pack-author
branch: feature/crm/mod-0164-fu03-consent-preference-admin-ui
started: 2026-08-03
target: 2026-08-03
form_field_count: 15
dependencies:
  - MOD-0164-FU01 (Consent & Preference Management boundary PASS)
  - MOD-0164-FU02 (Consent & Preference runtime / evaluation provider PASS — 65/65 authed smoke)
  - MOD-0048 (Consent vocabulary Runtime=SoT reconciliation PASS; alignment sets Submitted/Pending — publish blocker değil)
  - MOD-0165-FU04 (Campaign / Targeting runtime PASS — komşu tüketici)
  - MOD-0150 (Contact Availability PASS — subject panel gelecekteki entegrasyon)
  - MOD-0285 (existing tenant navigation pattern; no Platform runtime change)
  - DEV-0000 (Golden Reference Slim — archive confirmation, toast, offcanvas/canvas)
  - DEV-0001 (Golden Reference Compact — primary Consent/Preference surface)
---

# MOD-0164-FU03 — Consent & Preference Admin UI

> **READY-FOR-DEV UI AUTHORIZATION (2026-08-03).** Kullanıcı bu pack'in hazırlanmasını ve `ready-for-dev`
> olmasını açıkça istedi (governance gate BLOCKED döndükten sonra). Bu pack yalnız FU02'de PASS olan
> Consent / Preference / Evaluate API'lerini CRM tenant shell içinde kullanılabilir kılan frontend yüzeyini
> yetkilendirir. Backend business logic, evaluate provider, Gateway route, RBAC seed/grant, registry, migration
> ve Mongo yazımı **açılmaz**.
>
> **DCP-002 kimlik kapısı:** MOD-0164 registry'de canonical ("Consent & Preference Management", Blueprint W-2,
> DCP-002 gate OK 2026-07-14, Consent'i sahiplenir). FU03 onun bir **follow-up child**'ıdır (DCP-002: mevcut
> Blueprint MOD parent'ının FU/child'ı geçerlidir). Yerel `verify_module_id.py` bu ortamda çalıştırılamadı
> (python yok); kimlik registry + Blueprint kaydıyla doğrulandı. Implementation closeout'ta script erişilebilirse
> `--check-id MOD-0164-FU03 --name "Consent & Preference Admin UI" --parent MOD-0164` yeniden koşulmalıdır.
>
> **Neden gerekli:** FU02 backend/API runtime PASS olmasına rağmen FU03 yeni ve geniş kapsamlı bir UI feature'ıdır.
> `AGENTS.md` uyarınca `approved`/`ready-for-dev` module pack olmadan `@orchestrator` implementasyona başlayamaz;
> Commercial Suite domain config tenant-shell navigation'ı korur. Bu pack §6 ve §9'da yalnız Consent & Preferences
> menü girdisi için dar, test edilebilir istisna verir.

---

## 1. Module Summary

Amaç, CRM kullanıcılarının mevcut Gateway arkasındaki FU02 Consent/Preference runtime'ını şu yüzeylerle
kullanabilmesidir:

- `CRM Admin → Consent & Preferences` permission-controlled navigation ve deep link.
- Consent list, detail, create, edit ve archive.
- Preference list, detail, create, edit ve archive.
- Evaluate Test Panel: write-free consent/preference eligibility sorgusu + provenance görünümü.
- ReasonCodes / diagnostics / matched-id provenance görünümü.
- Reusable `ConsentPreferenceSubjectPanel` (Contact / AccountContactLink context) — parent UI yoksa follow-up.
- Contract-driven capability gating, kontrollü loading/empty/error durumları.
- Golden Compact/Slim pattern, DataTable v2 ve yedi dil localization parity.

Hedef kullanıcı tenant içindeki yetkili CRM yöneticisidir. FU03 hiçbir yeni business aggregate veya API açmaz;
FU02 contract'ının frontend consumer'ıdır. FU02 vocabulary **in-domain** doğrulanır; MOD-0048 consent alignment
setleri Submitted/Pending olsa bile bu UI için blocker değildir — UI runtime canonical vocabulary'i kullanır.

## 2. Ownership and Boundaries

### In-scope

1. CRM Admin → Consent & Preferences navigation/menu entry.
2. Consent List ve filtreleri.
3. Consent Detail: identity, consent question, legal/status, evidence pointer, external references, audit.
4. Consent Create/Edit full-page Compact akışı.
5. Consent archive action; hard delete yok.
6. Preference List ve filtreleri.
7. Preference Detail: identity, preference context, effective window, external references, audit.
8. Preference Create/Edit full-page Compact akışı.
9. Preference archive action; hard delete yok.
10. Evaluate Test Panel (write-free) ve allowed/blocked/unknown gösterimi.
11. ReasonCodes, SelectionReason, matched-id provenance ve diagnostics gösterimi.
12. `ConsentPreferenceSubjectPanel` reusable component (parent UI yoksa integration follow-up).
13. `GET /api/crm/consents/contract` temelli capability checks.
14. Gateway-only proxy/client entegrasyonu, UI testleri, build, smoke ve evidence.

### Out-of-scope / kesinlikle yetkisiz

- Consent veya Preference backend runtime/business-logic değişikliği.
- Evaluate provider logic değişikliği.
- Campaign runtime veya Campaign UI.
- Segment engine, visit/route planning, due/overdue, MOD-0155.
- Frequency, Knowledge, Brand/Product, Digital Detailing veya Recommendation runtime.
- Workflow/approval, import/export engine veya patient data.
- Hard delete veya HTTP `DELETE`.
- Direct port `5061` business call.
- Migration, Mongo hand-edit, RBAC seed/grant, MOD-0048 publish veya registry write.
- `gateway/Diten.ApiGateway/**` değişikliği.
- Contact/AccountContactLink runtime mutasyonu veya Contact üzerine flat ConsentStatus alanı.
- API'de olmayan master/display resolution veya fake preview/filter.

## 3. Owned Objects

FU03 runtime entity sahiplenmez. `entity_base: EntityBase`, FU02'nin tenant-owned ConsentRecord ve
PreferenceRecord aggregate'lerinden miras alınan kontratı belgeler; frontend yeni entity/schema/index oluşturmaz.

- MVC surface: `ConsentPreferencesController` (`frontend/Diten.Web`), route `/CRM/ConsentPreferences`.
- Frontend models: contract, consent, preference, evaluate, external-reference ve gateway-envelope view modelleri.
- Views: consent list/create/edit/details; preference list/create/edit/details; consent & preference form;
  filters; DataTable partial'ları; evaluate panel; subject panel; provenance panel; L10n bridge.
- Scripts: consent/preference list/detail/form, evaluate panel ve localization bridge davranışları.
- UI permission consumers (canonical, seed yok):
  - `crm.consent.read`, `crm.consent.manage`, `crm.consent.evaluate`
  - `crm.preference.read`, `crm.preference.manage`
- API owner: **FU02 / Diten.CrmService**; FU03 yalnız tüketir.

## 4. Entity Fields

Bu UI-only pack schema üretmez. Aşağıdaki alanlar FU02 response/request sözleşmesini tüketir.

### Consent authoring ve detail

| Field | UI | Kural |
|---|---|---|
| ConsentId | list/detail (read-only) | Backend üretir; request'te yok. |
| SubjectType | Create identity + list/detail/filter | Required; contract vocabulary; **edit'te immutable/read-only**. |
| SubjectId | Create identity + list/detail/filter | Required GUID; **edit'te immutable/read-only**. |
| Channel | Create identity + list/detail/filter | Required; contract vocabulary; **edit'te immutable/read-only**. `all` sentinel YASAK (consent). |
| Purpose | Create identity + list/detail/filter | Required; contract vocabulary; **edit'te immutable/read-only**. |
| ScopeType | Create identity + detail | Optional; contract vocabulary; **edit'te immutable/read-only**; ScopeId varsa required. |
| ScopeId | Create identity + detail | Optional GUID; **edit'te immutable/read-only**; ScopeType olmadan gönderilemez. |
| LegalBasis | Create/Edit | Required; contract vocabulary (runtime canonical 7). |
| ConsentStatus | Create/Edit/filter | Required; contract vocabulary; archive yalnız endpoint üzerinden. |
| Source | Create/Edit | Contract vocabulary (runtime canonical 8). |
| EffectiveFrom | Create/Edit | Required date/time. |
| EffectiveTo | Create/Edit | Optional; varsa `EffectiveTo >= EffectiveFrom`. |
| Reason | Create/Edit/detail | Optional; contract shape'i korunur. |
| WithdrawalReason | Conditional | `ConsentStatus=withdrawn` ise backend gerektiriyorsa required. |
| EvidenceRef / EvidenceType / EvidenceId | Create/Edit/detail | API'de ne varsa; yalnız pointer/provenance — dosya/içerik/URL render edilmez. |
| ExternalReferences[] | Optional collection | SourceSystem, ExternalId, ExternalCode, ExternalName, ImportedAt, IsPrimary; API dışı alan eklenmez. |
| IsArchived / ArchivedAt / ArchivedBy / CreatedAt / CreatedBy / UpdatedAt / UpdatedBy | Read-only | Lifecycle ve audit gösterimi. |

### Preference authoring ve detail

| Field | UI | Kural |
|---|---|---|
| PreferenceId | list/detail (read-only) | Backend üretir; request'te yok. |
| SubjectType | Create identity + list/detail/filter | Required; **edit'te immutable/read-only**. |
| SubjectId | Create identity + list/detail/filter | Required GUID; **edit'te immutable/read-only**. |
| Channel | Create identity + list/detail/filter | Contract vocabulary; **edit'te immutable/read-only**. **`all` sentinel yalnız Preference channel için geçerli.** |
| PreferenceType | Create identity + list/detail/filter | Required; contract vocabulary; **edit'te immutable/read-only**. |
| PreferenceValue | Create/Edit/detail | API contract'a göre required/optional; UI contract'ı takip eder. |
| ScopeType / ScopeId | Create identity + detail | Optional; **edit'te immutable/read-only**; ScopeId ScopeType olmadan gönderilemez. |
| Priority | Create/Edit/detail | Required ve `>= 1`. |
| IsRestrictive | Create/Edit/detail | Restrictive preference allowed consent'i blocked yapabilir; UI bunu açıkça belirtir. |
| EffectiveFrom | Create/Edit | Required date/time. |
| EffectiveTo | Create/Edit | Optional; varsa `EffectiveTo >= EffectiveFrom`. |
| ExternalReferences[] | Optional collection | API request contractı kadar. |
| IsArchived / ArchivedAt / ArchivedBy / CreatedAt / CreatedBy / UpdatedAt / UpdatedBy | Read-only | Lifecycle ve audit gösterimi. |

## 5. Repo Scope

Yalnız aşağıdaki alanlarda değişiklik yapılabilir:

- `execution/domains/commercial-suite/module-packs/MOD-0164-FU03-consent-preference-admin-ui.md`
- `frontend/Diten.Web/Controllers/CRM/ConsentPreferencesController.cs`
- `frontend/Diten.Web/Models/CRM/ConsentPreferenceViewModels.cs`
- `frontend/Diten.Web/Views/CRM/ConsentPreferences/**`
- `frontend/Diten.Web/Resources/Views/CRM/ConsentPreferences/**`
- `frontend/Diten.Web/wwwroot/assets/js/CRM/ConsentPreferences/**`
- `frontend/Diten.Web/tests/**` — yalnız MOD-0164-FU03 test dosyaları veya doğrudan ilgili test registration.
- `frontend/Diten.Web/Resources/SharedResource.{en,fr,es,zh,ar,ru,tr}.resx` — yalnız Consent & Preferences
  menü/shared archive/validation key'leri; var olan key'ler tekrar eklenmez.
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — yalnız §6'daki dar navigation istisnası.
- `docs/audits/mod-0164-fu03-consent-preference-admin-ui-implementation-2026-08-03.md` — implementation evidence.

Var olan ortak frontend helper'ları tüketilebilir; değiştirilmeleri bu pack tarafından yetkilendirilmez. Ortak
helper değişikliği zorunlu görünürse orchestrator durur ve ayrı authorization ister.

## 6. Protected Paths

### Dar navigation istisnası

Domain config'e göre `_LayoutTenantShell.cshtml` protected alandır. Module Pack daha spesifik otorite olarak yalnız
şu değişikliğe izin verir:

- Dosya: `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml`.
- Section: mevcut `Commercial Suite` menü grubunda Accounts / Contacts / Territory / Campaigns komşuluğu.
- İzinli değişiklik: `/CRM/ConsentPreferences` deep linkine giden tek `<li>` ve gerekiyorsa Commercial Suite
  header'ının yalnız consent read permission'ı varken bir kez görünmesini sağlayan mevcut boolean koşulunun minimal
  genişletilmesi.
- Zorunlu guard: `Perms.Has("crm.consent.read")`; RBAC fallback nedeniyle canonical key claim'de yoksa mevcut
  frontend resolver davranışı raporlanır, seed/grant veya genişleten yeni resolver yazılmaz.
- Label: yedi dilli `ConsentPreferencesMenu` shared key'i; hardcoded visible text yok.
- Aktif route: `currentPath.StartsWith("/CRM/ConsentPreferences", StringComparison.OrdinalIgnoreCase)`.
- Yasak: layout yapısı, diğer menü öğeleri, DynamicModuleMenu ViewComponent, token/cookie akışı, navigation API,
  CSS/JS bundle, impersonation veya shell behavior değişikliği.

MOD-0285 dynamic navigation loader incelendi. Descriptor publish/self-registration Platform/backend değişikliği
gerektirdiği ve FU03 UI-only olduğu için bu pack'te kullanılmaz. İleride MOD-0285 data-driven migration yapılırsa
hardcoded Consent & Preferences `<li>` kaldırılması ayrı follow-up'tır; çift menü kabul edilmez.

### Diğer protected alanlar

- `.antigravity/**`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, `_LayoutPlatformAdmin.cshtml`.
- `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**`.
- `services/Diten.CrmService/**` ve diğer tüm `services/**`.
- `gateway/Diten.ApiGateway/**` ve özellikle `ocelot.json`.
- `execution/registries/**`, Auth seed/grant dosyaları, migrations ve Mongo data.
- CRM Accounts, Contacts, Territory, Campaigns dosyaları; yalnız okunabilir referanstır.

## 7. Dependencies

- FU02 evidence: runtime, contract, evaluate provider ve 65/65 authed smoke PASS kanıtı.
- `/api/crm/consents` ve `/api/crm/preferences` Gateway route çiftleri (ocelot'ta mevcut, `{everything}` dahil);
  methods `GET/POST/PUT/OPTIONS`, `DELETE` yok.
- `GET /api/crm/consents/contract`: feature flags, consent + evaluation vocabulary, reason codes, permissions ve
  limitations.
- FU02 in-domain vocabulary (runtime canonical) — MOD-0048 publish beklenmez.
- GoldenReferenceCompact: Consent/Preference list ve full-page Create/Edit/Details.
- GoldenReferenceSlim: archive confirmation ve toast davranışı; evaluate panel için hafif canvas.
- `_LayoutTenantShell`, `IPermissionSnapshot`, `PermissionClaims.HasPermission`, controller `RequirePage` pattern'i.
- Global `window.showConfirm` ve `window.showToast` helper'ları.

## 8. Runtime Constraints

- Frontend browser veya MVC proxy tüm business çağrılarını Gateway `5000` üzerinden yapar.
- Direct `http://localhost:5061`, `https://localhost:5061` veya herhangi bir `:5061` business URL yasaktır.
- Same-origin MVC proxy tercih edilir; HttpOnly access token server-side Gateway requestine aktarılır.
- Payload içinde `TenantId` alanı oluşturulmaz/gönderilmez. Mevcut auth mekanizmasının tenant header/claim akışı korunur.
- Consent/Preference lifecycle archive endpointleriyle yürür; `DELETE` kullanılmaz.
- Contract okunamazsa action'lar varsayılan olarak kapalıdır ve kontrollü error state gösterilir (fail closed).
- Contract flag'i yok/false ise ilgili action hide veya disable edilir; yasak capability türetilmez.
- Backend'in desteklemediği filtre client-side fake filter olarak uygulanmaz; disabled/omitted ve evidence'da limitation.
- **Consent list API bugün yalnız SubjectType, SubjectId, Channel, Purpose, ConsentStatus ve IncludeArchived
  filtrelerini destekler.** Search text, ScopeType, ScopeId, LegalBasis, Source ve effective date-range
  backend-supported **değildir**.
- **Preference list API bugün yalnız SubjectType, SubjectId, Channel, PreferenceType ve IncludeArchived destekler.**
  Search text, ScopeType, ScopeId ve IsRestrictive server filter **değildir**.
- `IncludeArchived` backend default'u `true`'dur; UI "Include archived" kapalıyken `IncludeArchived=false` gönderir.
- Evaluate write-free'dir; UI hiçbir kayıt üretmez ve sonucu kaydedilmiş consent gibi göstermez.
- Unknown/future response alanları (bkz. §16 guard listesi) ignore edilir; yeni feature açılmaz.

## 9. Layout & Shell Contract

- `shell: tenant`.
- Bütün Consent/Preference Razor page'lerinde açıkça `Layout = "_LayoutTenantShell";` yazılır.
- View root: `frontend/Diten.Web/Views/CRM/ConsentPreferences/`.
- MVC route: `/CRM/ConsentPreferences`; consent detail `/CRM/ConsentPreferences/Consents/{consentId}`,
  preference detail `/CRM/ConsentPreferences/Preferences/{preferenceId}`, evaluate `/CRM/ConsentPreferences/Evaluate`
  veya controller convention ile eşdeğer stabil route.
- Consent ve Preference iki sekme/alt-sayfa olarak aynı Consent & Preferences yüzeyinde sunulur; evaluate ayrı tab.
- Breadcrumb, varsa mevcut tenant-shell pattern'iyle `CRM Admin → Consent & Preferences → …` gösterir.
- Yeni shell, table, modal, toast veya breadcrumb pattern'i icat edilmez.
- §6 dar navigation istisnası dışında shared layout değiştirilmez.

## 10. Backend File Convention

FU03 backend feature üretmez. Golden Compact backend klasör convention'ı incelenmiştir fakat FU02 runtime zaten
`services/Diten.CrmService/.../Features/ConsentPreference/` altında PASS'tir. Orchestrator:

- yeni command/query/handler/validator/repository/controller oluşturmaz,
- FU02 sınıflarını taşımaz veya yeniden adlandırmaz,
- request/response sözleşmesini frontend modellerinde birebir tüketir,
- backend ihtiyacı tespit ederse UI'da fake davranış yazmak yerine limitation raporlar.

Bu bölüm standarda göre zorunludur; `Backend File Convention: N/A — existing FU02 API is protected` kararı
intentional'dır.

## 11. Frontend File Contract

Primary surface GoldenReferenceCompact ile:

```text
frontend/Diten.Web/
├── Controllers/CRM/ConsentPreferencesController.cs
├── Models/CRM/ConsentPreferenceViewModels.cs
├── Views/CRM/ConsentPreferences/
│   ├── Index.cshtml                 (Consent & Preferences shell; Consents/Preferences/Evaluate tabs)
│   ├── Consents/
│   │   ├── Create.cshtml
│   │   ├── Edit.cshtml
│   │   ├── Details.cshtml
│   │   ├── _Form.cshtml
│   │   ├── _Filter.cshtml
│   │   └── _DataTable.cshtml
│   ├── Preferences/
│   │   ├── Create.cshtml
│   │   ├── Edit.cshtml
│   │   ├── Details.cshtml
│   │   ├── _Form.cshtml
│   │   ├── _Filter.cshtml
│   │   └── _DataTable.cshtml
│   ├── _EvaluatePanel.cshtml
│   ├── _SubjectPanel.cshtml          (ConsentPreferenceSubjectPanel reusable)
│   ├── _Provenance.cshtml            (reasonCodes / matched-ids / diagnostics)
│   ├── _IndexL10n.cshtml
│   └── ConsentPreferencesIndex.cs
├── wwwroot/assets/js/CRM/ConsentPreferences/
│   ├── index.js
│   ├── index.l10n.js
│   ├── consent-form.js
│   ├── preference-form.js
│   └── evaluate.js
└── Resources/Views/CRM/ConsentPreferences/
    └── ConsentPreferencesIndex.{en,fr,es,zh,ar,ru,tr}.resx
```

- Consent ve Preference Create/Edit/Details full-page Compact'tır; bunlar için `_CreateEditOffcanvas.cshtml` ve
  `_DetailsQuickView.cshtml` **YASAK** (compact kuralı).
- Consent ve Preference tablolarında `data-dt-standard="v2"`, skeleton, `_Filter`, toolbar, pagination, sort,
  save-view marker ve loading/empty/error state zorunludur.
- Archive confirmation `window.showConfirm`; toast `window.showToast`. Ham `alert`, `confirm` veya doğrudan
  `Swal.fire` kullanımı yasaktır.
- ReasonCodes/provenance compact badge/list + expandable diagnostics; excluded/archived rows saklanmaz.
- Evaluate panel hafif bir tab/panel'dir; write-free olduğu ekranda açıkça yazılır.

## 12. Validation Rules

| Alan / davranış | Required | UI kuralı |
|---|---|---|
| SubjectType (consent & preference) | Yes | Blank engellenir; contract vocabulary; edit'te read-only. |
| SubjectId | Yes | Geçerli GUID; edit'te read-only. |
| Channel (consent) | Yes | Contract vocabulary; `all` YASAK; edit'te read-only. |
| Channel (preference) | Contract'a göre | Contract vocabulary; `all` sentinel izinli; edit'te read-only. |
| Purpose (consent) | Yes | Contract vocabulary; edit'te read-only. |
| ScopeType / ScopeId | No | ScopeId varsa ScopeType required; edit'te read-only. |
| LegalBasis (consent) | Yes | Contract vocabulary; dışı değer gönderilmez → backend 400 reasonCodes gösterilir. |
| ConsentStatus | Yes | Contract vocabulary; archive değeri edit ile lifecycle bypass etmez. |
| Source (consent) | Contract'a göre | Contract vocabulary. |
| WithdrawalReason | Conditional | `ConsentStatus=withdrawn` ise backend gerektiriyorsa required. |
| PreferenceType | Yes | Contract vocabulary; edit'te read-only. |
| PreferenceValue | Contract'a göre | API contract required ise uygulanır. |
| Priority (preference) | Yes | Numeric `>= 1`. |
| EffectiveFrom | Yes | Geçerli tarih/zaman. |
| EffectiveTo | No | Varsa `EffectiveTo >= EffectiveFrom`. |
| EvidenceRef/Type/Id | No | Format-level; yalnız pointer; dosya/içerik render edilmez. |
| ExternalReferences[] | No | API request contractı kadar; ekstra alan eklenmez. |
| TenantId | Forbidden | View model/form/JSON payload içinde bulunmaz. |

Client validation erken geri bildirimdir; backend validation ve reasonCodes korunur/gösterilir.

## 13. Failure Path to Verify

1. Contract load 401/403/5xx/timeout → controlled error, capability actionları fail-closed.
2. Consent/Preference list loading/empty/error → Golden state; fake rows yok.
3. Required field veya ters tarih aralığı → submit yok, localized validation.
4. Invalid channel/purpose/legalBasis/consentStatus → backend 400; reasonCodes visible; "unknown allowed" gösterilmez.
5. `EffectiveTo < EffectiveFrom` → 400 handled.
6. withdrawn without reason → backend 400 (contract gerektiriyorsa) reasonCodes ile gösterilir.
7. Archived consent/preference edit → action disabled; yarış 409 ayrıca görünür.
8. Archive tekrarında already-archived/409 → düzgün işlenir, sahte başarı yok.
9. ScopeId without ScopeType → 400 visible.
10. Duplicate external mapping 409 → visible; silent merge yok.
11. Evaluate invalid channel → 400 (malformed question); "unknown" gibi gösterilmez.
12. Evaluate no matching consent → `unknown` + `consent_unknown`; **allowed gibi gösterilmez**.
13. Restrictive preference → evaluate `blocked` + preference reason; matched consent gizlenmeden gösterilir.
14. Unauthorized route/action → mevcut 401/403 UX; permission bypass/fallback genişletmesi yok.
15. Unknown backend response field → sessiz ignore; visit/route/frequency/recommendation feature açılmaz.

## 14. Authorization Convention

- Actor: authenticated tenant user; controller `[Authorize]` ve mevcut page/action guard pattern'i.
- Menu/list/detail (consent): canonical `crm.consent.read`.
- Consent create/edit/archive: `crm.consent.manage`.
- Evaluate panel: `crm.consent.evaluate`.
- Preference list/detail: `crm.preference.read`.
- Preference create/edit/archive: `crm.preference.manage`.
- **FU02 fallback (kanıtlanmış):** canonical `crm.consent.*` / `crm.preference.*` seed **edilmemiştir**; FU02
  endpoint'leri `crm.territory.read` (reads/evaluate) ve `crm.territory.model.manage` (writes) fallback'i ile çalışır
  (MOD-0165 FU03/FU05 ile aynı). Frontend yeni fallback icat etmez; mevcut `IPermissionSnapshot`,
  `PermissionClaims.HasPermission` ve `RequirePage` davranışını kullanır.
- Menü guard'ı canonical `crm.consent.read` kalır. Claim bulunmadığı için görünürlük engellenirse bu
  **PARTIAL/follow-up** olarak raporlanır; seed/grant veya hardcoded allow yapılmaz.
- Permission key'ler lowercase-dotted ve PKS-001 uyumludur.

## 15. Gateway / API Routing Decision

Karar: **Yeni Gateway değişikliği gereksiz.** FU02 rotaları `ocelot.json` içinde mevcuttur
(`/api/crm/consents` + `/api/crm/consents/{everything}`, `/api/crm/preferences` + `/api/crm/preferences/{everything}`);
dosya protected kalır.

UI yalnız Gateway üzerinden şunları tüketebilir:

```text
GET    /api/crm/consents
GET    /api/crm/consents/{consentId}
POST   /api/crm/consents
PUT    /api/crm/consents/{consentId}
POST   /api/crm/consents/{consentId}/archive
GET    /api/crm/consents/evaluate
GET    /api/crm/consents/contract
GET    /api/crm/preferences
GET    /api/crm/preferences/{preferenceId}
POST   /api/crm/preferences
PUT    /api/crm/preferences/{preferenceId}
POST   /api/crm/preferences/{preferenceId}/archive
```

- Gateway base port `5000`; direct `5061` yok.
- HTTP `DELETE` yok.
- TenantId payload yok.
- Path param'lar `{consentId}` / `{preferenceId}` (guid) — actual FU02 route isimleri; `…RecordId` değil.
- Backend error/reasonCodes UI toast/detail panelinde görünür.
- Existing authorization cookie/token propagation pattern'i korunur.

## 16. Acceptance Criteria

- [ ] `/CRM/ConsentPreferences` route'u `_LayoutTenantShell` ile render olur ve deep link çalışır.
- [ ] `_LayoutTenantShell.cshtml` değişikliği yalnız §6 dar istisnasındadır; menü canonical `crm.consent.read` guard'lıdır.
- [ ] Consent list DataTable v2/Golden Compact-Slim state contractına uyar; kolonlar §H listesine uyar
  (ConsentId, SubjectType, SubjectId, Channel, Purpose, ScopeType, ScopeId, LegalBasis, Status, Source,
  EffectiveFrom, EffectiveTo, IsArchived, UpdatedAt, Actions).
- [ ] Consent backend-supported filtreler (SubjectType, SubjectId, Channel, Purpose, ConsentStatus, IncludeArchived)
  server query kullanır; Search/ScopeType/ScopeId/LegalBasis/Source/date-range fake filter değildir ve evidence'da belgelenir.
- [ ] Consent detail identity, consent question, legal/status, evidence pointer, external references ve audit bölümlerini gösterir.
- [ ] Consent hiçbir yerde "genel izin bayrağı" gibi gösterilmez; daima subject×channel×purpose×scope×time bağlamında.
- [ ] Consent Create/Edit Compact full-page ve ortak `_Form`; required/date validations vardır; immutable soru boyutları edit'te read-only.
- [ ] Consent archive POST archive endpointini ve confirmation/toast pattern'ini kullanır; DELETE yok; archived kayıt read-only.
- [ ] Consent channel option listesinde `all` **yoktur**.
- [ ] Preference list DataTable v2; kolonlar §L listesine uyar (PreferenceId, SubjectType, SubjectId, Channel,
  PreferenceType, PreferenceValue, ScopeType, ScopeId, Priority, IsRestrictive, EffectiveFrom, EffectiveTo, IsArchived, UpdatedAt, Actions).
- [ ] Preference backend-supported filtreler (SubjectType, SubjectId, Channel, PreferenceType, IncludeArchived) server query kullanır;
  Search/ScopeType/ScopeId/IsRestrictive fake değildir ve limitation belgelenir.
- [ ] Preference channel option listesinde `all` sentinel **vardır**; Priority `>= 1` validation.
- [ ] Preference detail "preference can restrict consent but cannot grant consent" açıklamasını gösterir; archived read-only.
- [ ] Preference create/edit Compact; immutable soru boyutları edit'te read-only; archive POST archive kullanır, DELETE yok.
- [ ] Evaluate panel açılır; write-free/read-only açıklaması görünür; subject/channel/purpose ister.
- [ ] Evaluate `GET /api/crm/consents/evaluate` çağırır; allowed/blocked/unknown badge render eder; **unknown allowed gibi gösterilmez**.
- [ ] Evaluate sonucu EligibilityStatus, Decision, ReasonCodes, EvaluatedAt, MatchedConsentId, MatchedPreferenceIds,
  EvaluatorVersion, SelectionReason/diagnostics gösterir; matched ids yalnız provenance.
- [ ] `ConsentPreferenceSubjectPanel` SubjectType+SubjectId ile filtered consent/preference gösterir; parent UI yoksa integration follow-up raporlanır.
- [ ] Contact üzerine flat ConsentStatus alanı eklenmez; Consent/Preference ayrı aggregate kalır.
- [ ] ReasonCodes/provenance her ekranda görünür (badge + expandable diagnostics + toast summary).
- [ ] Contract flags actionları fail-closed enable/disable/hide eder.
- [ ] Permission-controlled list/action/menu visibility mevcut resolver'a bağlıdır; seed/grant yoktur.
- [ ] Tüm yeni visible text en/fr/es/zh/ar/ru/tr RESX/L10n parity taşır.
- [ ] Frontend kodunda direct `5061`, Consent/Preference `DELETE`, TenantId payload veya §16 guard-listesi alanı yoktur.
- [ ] Diten.Web build, ilgili UI tests, DataTable verifier, RESX parity ve mümkünse authenticated tenant smoke PASS'tir.
- [ ] Evidence raporu belirtilen 22 bölümü ve desteklenmeyen filtre/permission fallback/smoke sınırlamalarını içerir.
- [ ] Backend, Gateway, registry, seed/grant, Mongo, MOD-0048 publish ve MOD-0155 değişmemiştir.

### Response shape / UI data guard

UI şu alanları beklemez, göstermez ve bunlardan feature üretmez:

```text
visitPlanId
routePlanId
routeId
dueStatus
overdue
lastVisitDate
requiredVisitCount
periodType
frequencyPolicyId
campaignTargetId
segmentMembership
recommendationId
nextBestAction
workflowApprovalId
contentRenderUrl
filePayload
consentRecordPayloadAsTargetData
preferenceRecordPayloadAsTargetData
```

Bu alanlar future/unknown olarak sakince ignore edilir.

## 17. Test Expectations

Minimum otomatik/statik doğrulama:

1. Consent & Preferences route render; contract Gateway client üzerinden load; nav entry consent-read guard'lı.
2. Consent list load/loading/empty/error ve filtre render.
3. Consent create/edit açılışı, required/date validation, TenantId'siz POST/PUT; immutable soru boyutları edit'te read-only.
4. Consent archive POST kullanımı; consent için `DELETE` yokluğu; archived read-only.
5. Consent detail evidence/provenance pointer'ları master fetch olmadan render.
6. Consent channel options içinde `all` yok.
7. Preference list load; create/edit açılışı; Priority `>= 1` validation; immutable fields read-only.
8. Preference channel options içinde `all` var.
9. Preference archive POST kullanımı; `DELETE` yokluğu; preference "cannot grant consent" copy'si.
10. Evaluate panel açılır; subject/channel/purpose ister; Gateway'e `GET evaluate` çağırır.
11. Evaluate allowed/blocked/unknown badge; unknown "not allowed" copy; MatchedConsentId/MatchedPreferenceIds provenance; ReasonCodes render.
12. SubjectPanel SubjectType/SubjectId ile render; Contact flat ConsentStatus alanı eklenmemiş.
13. Yedi locale dosyasında aynı key seti.
14. Frontend source'ta direct `5061`, forbidden `DELETE`, TenantId ve §16 guard-listesi alanı taraması temiz.
15. `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` PASS.
16. `python3 .antigravity/scripts/verify_datatable_page.py . --area CRM --module ConsentPreferences --reference compact` PASS (python varsa).
17. Mevcut frontend testleri etkilenmez.

Authenticated smoke hedef tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`. Login → menu/list → consent create →
detail → evaluate same subject (allowed) → restrictive preference create → evaluate (blocked) → archive → archived
read-only → evaluate no-consent (unknown) → no DELETE → Gateway-only network → locale smoke. Test data uygun değilse
ilgili positive smoke açıkça deferred yazılır; fake veri/Mongo hand-edit yapılmaz.

## 18. Ready-for-dev Checklist

- [x] AGENTS.md, Commercial Suite domain config, master plan, registry ve delivery board okundu.
- [x] DCP-002 identity: MOD-0164 registry canonical + Blueprint W-2; FU03 child follow-up. (Yerel python gate yok — closeout'ta koşulacak.)
- [x] FU02 evidence ve gerçek FU02 controller/route/contract/vocabulary/fallback sözleşmesi okundu.
- [x] Golden Reference Compact pack ve canlı frontend kodu okundu (Create/Edit/Details/_Form/_Filter/_DataTable/_IndexL10n).
- [x] Golden Reference Slim archive/modal/toast kodu referans olarak.
- [x] Kardeş MOD-0165-FU05 Campaign Admin UI pack'i navigation/protected-path deseni için okundu.
- [x] Frontmatter tüm zorunlu alanları içeriyor.
- [x] Consent form field count 15 (>8); `golden_reference: compact`.
- [x] Layout & Shell Contract açıkça `_LayoutTenantShell` yazıyor.
- [x] Backend File Convention UI-only/N-A olarak açık; FU02 runtime protected.
- [x] Frontend File Contract Compact dosya setini listeliyor; offcanvas/quickview yasak.
- [x] Field-level ve conditional validation kuralları yazıldı.
- [x] Failure Path duplicate, missing, unauthorized, archived/concurrency, invalid-vocab, evaluate-unknown içeriyor.
- [x] Authorization: canonical keys + actor + FU02 territory fallback sınırı açık.
- [x] Gateway routing kararı `değişiklik gereksiz`; endpoint allowlist ve gerçek route'lar açık.
- [x] Protected navigation istisnası dosya/section/guard/değişmez alanlar düzeyinde kesin.
- [x] Backend-supported list filtreleri ve desteklenmeyen filtre limitation'ları açık.
- [x] Acceptance criteria test edilebilir; response-shape guard ve exclusions yazıldı.
- [x] Kullanıcı bu hazırlık görevinde `ready-for-dev` hedefini açıkça yetkilendirdi.

## 19. Implementation Notes

- FU02 vocabulary **in-domain** doğrulanır (`ConsentPreferenceContract.cs`); MOD-0048 consent alignment setleri
  Submitted/Pending olsa da bu UI için blocker **değildir**. UI runtime canonical vocab'ı kullanır:
  - LegalBasis (7): `explicit-consent, contract, legal-obligation, legitimate-interest, public-interest, vital-interest, other`
  - Source (8): `subject-declared, field-capture, portal, consent-center, legacy-import, contract-document, manual, other`
  - Consent Channel (9): `visit, email, sms, phone, whatsapp, portal, digital-detailing, training, other` (`all` yok)
  - Purpose (9): `campaign, medical-visit, product-information, training, marketing, service, compliance, research, other`
  - ConsentStatus (6): `granted, denied, withdrawn, restricted, unknown, expired`
  - PreferenceType (8): `preferred-channel, do-not-contact, do-not-visit, preferred-visit-window, language-preference, content-preference, frequency-cap, topic-interest`
  - Preference Channel: consent channel + `all` sentinel.
  UI mümkünse bu değerleri `GET /consents/contract` vocabulary bloğundan okur (hardcoded liste yerine); contract
  okunamazsa yukarıdaki canonical değerler fallback referanstır.
- FU02 canonical permissions seed edilmemiştir; backend territory fallback kullanır (`crm.territory.read` /
  `crm.territory.model.manage`). UI seed/grant yapmaz; menü/aksiyon görünürlüğü canonical `crm.consent.*` beklerken
  claim yoksa PARTIAL/follow-up olarak raporlanır.
- Evaluate write-free'dir; sonucu kaydedilmiş consent gibi göstermek yasaktır.
- `IncludeArchived` backend default `true`; UI "Include archived" toggle'ı bunu açıkça yönetir.
- Çalışma ağacı çok sayıda mevcut değişiklik içeriyor. Orchestrator yalnız §5 paths üzerinde çalışmalı, mevcut
  kullanıcı değişikliklerini korumalı ve layout değişikliğini minimal tutmalıdır.
- Implementation başladığında pack status `in-progress`, test sonrası `review`, kabulden sonra `done` yapılır;
  `execution/registries/module-implementation-status.md` güncellemesi implementation closeout kapsamındadır. Bu
  hazırlık task'ı registry write yapmaz.

## 20. Follow-up Items

- **MOD-0164-FU-RBAC:** canonical `crm.consent.*` / `crm.preference.*` keys seed/grant ve backend territory
  fallback kaldırma; FU03 kapsamı dışı.
- **Contact / AccountContactLink entegrasyonu:** `ConsentPreferenceSubjectPanel` bu pack'te oluşturulur; Contact
  detail / AccountContactLink parent UI yoksa gömme (embed) ayrı follow-up olarak raporlanır.
- **MOD-0285 navigation migration:** Consent & Preferences descriptor data-driven olduğunda hardcoded menu entry'nin
  kaldırılması ve no-double-menu smoke; FU03 kapsamı dışı.
- Backend destekli ek Consent/Preference filtreleri (Search, ScopeType/ScopeId, LegalBasis, Source, IsRestrictive,
  date-range) istenirse ayrı runtime FU authorization.
- **MOD-0048 — Approve & Publish Submitted Consent Alignment Versions:** governance/authoring-UI tutarlılığı için
  ayrı checker task; FU03 UI için blocker değil.
- MOD-0155 beklemede kalır; bu pack visit/route/frequency/due-overdue kapsamını açmaz.

### Orchestrator handoff

```text
@orchestrator execution/domains/commercial-suite/module-packs/MOD-0164-FU03-consent-preference-admin-ui.md

MOD-0164-FU03 — Consent & Preference Admin UI Implementation
```

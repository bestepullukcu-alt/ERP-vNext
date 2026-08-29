---
id: MOD-0162-FU02
name: Knowledge / Content Taxonomy Runtime + UI
parent: MOD-0162
parent_name: Knowledge Base
siblings: MOD-0162-FU01, MOD-0162-FU01A, MOD-0162-FU01B, MOD-0162-FU01C
domain: commercial-suite
service: Diten.CrmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: done
runtime_code_allowed: true
runtime_code_scope: "KnowledgeContent + Subject/Topic + AudienceProfile runtime (CRUD-minus-delete + archive + effective dating + contract + campaign content read provider) in Diten.CrmService AND the CRM Admin → Knowledge UI in frontend/Diten.Web. Concept-graph (FU01C), KnowledgePath (FU01A), EngagementJourney (FU01B) runtime, digital detailing, recommendation engine, file storage, Campaign/Consent/Brand-Product mutation, Gateway config, RBAC seed/grant, MOD-0048 publish, registry write ve Mongo hand-edit YASAKTIR."
owner: module-pack-author
branch: feature/crm/mod-0162-fu02-knowledge-content-runtime-ui
started: 2026-08-09
target: TBD (boundary onayı + Gateway authorization sonrası)
form_field_count: 18
dependencies:
  - MOD-0162-FU01 (Knowledge Content & Subject Taxonomy Foundation — SoT boundary; §4–§9 sözleşmesi implement edilir)
  - MOD-0162-FU01A (KnowledgePath / Content Sequence boundary — bu FU'da IMPLEMENT EDİLMEZ; ayrı FU)
  - MOD-0162-FU01B (EngagementJourney boundary — bu FU'da IMPLEMENT EDİLMEZ; ayrı FU)
  - MOD-0162-FU01C (Subject Concept Graph boundary — bu FU'da IMPLEMENT EDİLMEZ; yalnız ConceptNodeId format-level referans)
  - MOD-0290 / MDM (Brand/Product Source of Truth — BrandId/ProductId optional reference, tüketici; master kopyalanmaz)
  - MOD-0165-FU04 (Campaign runtime — content linkage read provider ile tüketir; Campaign DEĞİŞMEZ)
  - MOD-0048 (reference data — knowledge-content-type / -status / -source / audience-profile-type vokabüleri; publish ayrı operatör)
  - MOD-0028 / MOD-0029 (Document Management — FileRef referansı; dosya deposu burada açılmaz)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te yok, en sona bırakıldı)
  - DEV-0001 (Golden Reference Compact — KnowledgeContent primary surface)
  - DEV-0000 (Golden Reference Slim — archive confirmation, toast, taxonomy alt-form canvas)
---

# MOD-0162-FU02 — Knowledge / Content Taxonomy Runtime + UI

> **✅ DONE (2026-08-10) — authenticated Gateway smoke ALL PASS (22/0); pack `review → done`.**
> Closeout smoke (`scripts/smoke-mod0162-fu02-knowledge-content-authenticated.ps1`, tenant 97c59330…) passed every step:
> login → contract 200 (7 flags true / 9 forbidden absent) → Subject/Topic/AudienceProfile/KnowledgeContent create 201 →
> TenantId-injected ignored → ContentVersion=1.0 → archive 200 → archived-update 409 → DELETE/PATCH 404 → no Campaign
> mutation → cleanup archive-only. Runtime + UI + smoke verified end-to-end. Remaining follow-ups (out of FU02 scope):
> FU02-RBAC, MOD-0048 reference-set publish, F-L10N pro translation, FU01A/01B/01C runtime.
>
> **🚧 REVIEW (2026-08-09) — backend + frontend implemented & build-green; authenticated smoke was pending.**
> **Backend:** KnowledgeContent + Subject + Topic + AudienceProfile aggregates, in-domain vocabulary, archive lifecycle,
> effective dating, contract endpoint, `IKnowledgeContentLinkageReader` read provider, 5 controllers — `Diten.CrmService`
> **build-green**; **23 backend tests PASS** (full suite 642/0). **Frontend:** CRM Admin → Knowledge (Content Compact
> list/create/edit/details, Taxonomy admin, nav `<li>`, proxy-only controller) — `Diten.Web` **build-green** (CoreCompile
> 0 errors); DataTable verifier PASS **except the 6 bulk-delete checks, which are N/A for this archive-only module** (§18);
> **7-language RESX parity = 113 keys** + SharedResource `KnowledgeMenu` ×7. **Remaining:** authenticated Gateway smoke
> (needs live fleet + operator login; script ready), RBAC seed/grant (FU02-RBAC), MOD-0048 reference-set publish, FU01A/01B/
> 01C runtime. Evidence:
> [implementation audit](../../../../docs/audits/mod-0162-fu02-knowledge-content-runtime-ui-implementation-2026-08-09.md).
> No Campaign/Consent/Brand-Product/MOD-0155 change; no RBAC seed/grant; no MOD-0048 publish; no Mongo hand-edit;
> `ocelot.json` unchanged.
>
> **✅ READY-FOR-DEV (2026-08-09) — her iki blocker kapandı; pack `@orchestrator` implementasyonuna AÇIK.**
> **F-BND** resolved (MOD-0162-FU01 approved) + **F-GW** resolved (Gateway `/api/crm/knowledge` + `/api/crm/knowledge/{everything}`
> route'ları eklendi → downstream `Diten.CrmService:5061`, `GET/POST/PUT/OPTIONS`, DELETE/PATCH yok;
> [F-GW audit](../../../../docs/audits/mod-0162-fu02-f-gw-knowledge-gateway-route-authorization-2026-08-09.md)).
> `status: draft → ready-for-dev`.
>
> **RUNTIME + UI IMPLEMENTATION AUTHORIZATION (2026-08-09) — `runtime_code_allowed: true`, `status: ready-for-dev`.**
> Bu pack, MOD-0162-FU01 boundary'sinde **yetkilendirilen ama implement edilmeyen** `KnowledgeContent` +
> `Subject`/`Topic` + `AudienceProfile` sözleşmesini `Diten.CrmService` içinde runtime'a ve CRM tenant shell içinde
> UI'ya dönüştürme yetkisini tanımlar. FU01 §19 Next Prompt #2 ve F4 bu FU'yu adıyla rezerve etmiştir.
>
> **Neden şimdi:** Consent (MOD-0164), Campaign/Targeting (MOD-0165) ve Brand/Product (MOD-0290) temel runtime/UI
> işleri büyük ölçüde tamamlandı. MOD-0155 Visit/Route Planning'e geçmeden önce **gerçek upstream boşluk**,
> "gidince ne anlatılacak?" sorusunun runtime karşılığıdır: Knowledge içeriği bugün yalnız Campaign runtime'ında
> ID/format-level referans olarak taşınıyor; onu yönetecek bir aggregate/endpoint/UI **yok**. RBAC alignment
> kullanıcı direktifiyle **en sona** bırakılmıştır.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-09):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0162-FU02 --name "Knowledge / Content Taxonomy Runtime + UI" --parent MOD-0162`
> → `OK  MOD-0162-FU02: proven against Blueprint/registry.` (exit 0). MOD-0162-FU01 kimlik notu (domain-nötr model;
> EA yatay-capability göçü — FU01 §18/F1) bu pack'i de kapsar.
>
> **Ready-for-dev geçiş kaydı (iki blocker da kapandı):**
> 1. **✅ F-BND RESOLVED (2026-08-09):** MOD-0162-FU01 (FU02'nin tek zorunlu SoT boundary'si) `approved`'a çekildi
>    ([boundary approval review](../../../../docs/audits/mod-0162-boundary-approval-review-fu01-fu01a-fu01b-fu01c-2026-08-09.md)).
>    FU01A/FU01C de `approved`; FU01B `draft` kaldı ama **FU02 EngagementJourney runtime açmadığı için blocker değil**.
> 2. **✅ F-GW RESOLVED (2026-08-09):** Gateway `/api/crm/knowledge` + `/api/crm/knowledge/{everything}` route'ları
>    `ocelot.json`'a eklendi (downstream `5061`, `GET/POST/PUT/OPTIONS`, DELETE/PATCH yok; mevcut CRM/MDM/legal-entities
>    route'ları korundu; toplam 114 → 116). Bkz. §11.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 1. Module Summary

Amaç: tenant CRM yöneticilerinin **anlatılacak içeriği** (`KnowledgeContent`) ve onu sınıflandıran
**Subject / Topic / AudienceProfile** taksonomisini uçtan uca yönetebilmesidir. FU02:

- `KnowledgeContent` merkezli içerik kataloğunu (CRUD-minus-delete + archive + effective dating) açar.
- `Subject` (en üst anlatım alanı), `Topic` (hiyerarşik alt konu, parent-child) ve `AudienceProfile` (generic
  hedef profil) taksonomisini açar.
- `BrandId`/`ProductId`/`ConceptNodeId`/`CampaignId` gibi alanları **opsiyonel format-level referans** olarak
  taşır; master runtime resolve etmez, kopyalamaz.
- Campaign runtime'ının **değişmeden** tüketebileceği bir **read provider / content linkage contract** yayınlar.
- `GET /api/crm/knowledge/contract` ile capability flag / vokabüler / permission / limitation yayınlar.
- CRM Admin → Knowledge navigation, DataTable v2 liste, full-page Compact create/edit, Slim archive/detail canvas
  ve 7 dil RESX parity ile UI'yı açar.

Hedef kullanıcı, tenant içindeki yetkili CRM içerik yöneticisidir. FU02 **yeni servis yaratmaz**; aggregate'ler
mevcut `Diten.CrmService` içinde açılır. Digital detailing, recommendation engine, visit/route planning, concept-graph
runtime, path/journey runtime ve workflow approval **açılmaz**.

## 2. Ownership and Boundaries

Ownership kararları (FU01 §2 ile birebir):

```text
Knowledge / Content runtime  → MOD-0162'nin Source of Truth'udur (bu FU sahiplenir).
Brand/Product Source of Truth → MOD-0290 / MDM'dir (bu FU yalnız optional reference tüketir).
Campaign, Knowledge içeriğini KOPYALAMAZ; yalnız referans tutar (read provider ile tüketir).
MOD-0155 Knowledge içeriği ÜRETMEZ; ileride tüketir.
```

### In-scope (yetkilendirilen)

1. `KnowledgeContent` aggregate + CRUD-minus-delete + archive lifecycle + effective dating.
2. `Subject` aggregate (unique `SubjectCode`) + CRUD-minus-delete + archive.
3. `Topic` aggregate (stabil `TopicCode`, `ParentTopicId` hiyerarşi, cross-subject/cycle yasağı) + CRUD-minus-delete + archive.
4. `AudienceProfile` aggregate (generic `ProfileCode`) + CRUD-minus-delete + archive.
5. `BrandId`/`ProductId`/`ConceptNodeId`/`CampaignId`/`SegmentId` **optional reference** alanları (format-level; MOD-0290 SoR tüketici).
6. Campaign content linkage için **read provider** (Campaign runtime **değiştirilmeden** query/contract seam).
7. `GET /api/crm/knowledge/contract` capability endpoint.
8. Backend tests + authenticated Gateway smoke script + evidence report.
9. CRM Admin → Knowledge navigation/menu entry (dar `_LayoutTenantShell` istisnası, §13).
10. Knowledge Content List / Detail / Create / Edit / Archive UI.
11. Subject / Topic / AudienceProfile List / Create / Edit / Detail / Archive UI (taxonomy admin).
12. Brand/Product/Concept ID reference display; master fetch **varsa** yalnız Gateway `/api/mdm/*` üzerinden; çözülemezse **raw ID**.
13. Contract-driven, Gateway-only, Golden Compact/Slim UI + DataTable v2 + 7 dil RESX + UI tests + smoke.

### Out-of-scope / kesinlikle yetkisiz

```text
Campaign runtime change            Campaign UI change
Consent runtime/UI change          Brand/Product runtime change
Brand/Product ownership change     Frequency runtime
Visit planning                     Route planning
MOD-0155 (herhangi bir yüzeyi)     Recommendation engine
Digital detailing runtime          Workflow / approval (MOD-0023)
Concept graph runtime (FU01C)      KnowledgePath runtime (FU01A)
EngagementJourney runtime (FU01B)  MOD-0048 publish
RBAC seed / grant                  Registry write
Mongo hand-edit                    Import/export engine
Patient data                       File / binary storage
Hard delete                        HTTP DELETE
```

## 3. Owned Objects

- **Entities (yeni, `Diten.CrmService`):** `KnowledgeContent`, `Subject`, `Topic`, `AudienceProfile`.
- **Backend Features:** `Features/Knowledge/Content/`, `Features/Knowledge/Subject/`, `Features/Knowledge/Topic/`,
  `Features/Knowledge/AudienceProfile/`, `Features/Knowledge/Contract/` (Golden Compact klasör convention'ı, §10).
- **Read provider:** `IKnowledgeContentLinkageReader` (Campaign consumer seam; §12).
- **Controllers (`Diten.CrmService.Api/Controllers/CRM/`):** `KnowledgeContentsController`, `KnowledgeSubjectsController`,
  `KnowledgeTopicsController`, `KnowledgeAudienceProfilesController`, `KnowledgeContractController` (ya da tek
  `KnowledgeController` grubu — Campaign controller precedent'ine göre orchestrator kararı, route sözleşmesi §10 sabit).
- **MVC surface (`frontend/Diten.Web`):** `KnowledgeController` (route `/CRM/Knowledge`), Knowledge view seti (§11).
- **API endpoints:** §10.
- **Canonical permissions (yalnız öneri; seed/grant YOK — §15):** `crm.knowledge.read`, `crm.knowledge.manage`,
  `crm.knowledge.subject.read`, `crm.knowledge.subject.manage`.
- **Contract flags:** §16.

## 4. Entity Fields — genel kurallar

- Dört aggregate de **tenant-scoped**; `TenantId` **JWT claim'inden** çözülür, DTO/request payload'da **asla** bulunmaz.
- `entity_base: EntityBase` (CrmService tenant-owned; soft-delete/audit `EntityBase`'den gelir).
- **Hard delete yok**; yaşam döngüsü yalnız `archive`.
- İki `DateTimeOffset` alanı (`EffectiveFrom`/`EffectiveTo`) **birlikte index'lenmez/sort edilmez** (CRM parallel-array
  tuzağı — [crm-datetimeoffset-array-pitfalls]). Effective window sorguları buna göre tasarlanır.
- Yeni aggregate'ler **`RegisterClassMaps`'e eklenmelidir** (aksi hâlde Guid FK'lar binary yazılır, filtreler sessizce
  boş döner — MOD-0151 FU05 dersi).

> **Naming divergence (kayda geçen karar):** FU01 §5/§9 içerik sürüm alanını `Version` olarak adlandırır. Platform
> kuralı ([entity-base-template.md] + module-pack-standard §14) `Version` adını **teknik concurrency** için rezerve
> eder; iş sürümü `Version` **olamaz**. Bu FU alanı **`ContentVersion`** olarak isimlendirir. Bu, boundary metninden
> bilinçli ve gerekli bir sapmadır; anlam (aynı `ContentCode` altında çok sürüm) korunur.

## 8. Knowledge Content Model (`KnowledgeContent`)

| Field | Type | Required | Kural |
|---|---|---|---|
| `ContentId` | Guid | Sistem | Aggregate kimliği |
| `ContentCode` | string | Evet | Sürümler arası **ortak, stabil**; tenant içinde aktif unique (§9) |
| `ContentTitle` | string | Evet | Trim, max 300 (FU01 `Title`) |
| `ContentType` | string | Evet | MOD-0048 `knowledge-content-type`; set yoksa **fail-closed 400** (§16, F-RD) |
| `ContentStatus` | string | Evet | MOD-0048 `knowledge-content-status`: `draft·review·approved·published·inactive·archived` |
| `SubjectId` | Guid | Evet | Archived subject'e **yeni** içerik bağlanamaz → 409/400 |
| `TopicId` | Guid | Hayır | Verilirse subject ile tutarlı olmalı; archived topic'e yeni bağlanamaz |
| `AudienceProfileId` | Guid | Hayır | Yoksa içerik **genel**; uydurma profil atanmaz |
| `ConceptNodeId` | Guid | Hayır | **Format-level referans** (FU01C runtime YOK); resolve edilmez |
| `BrandId` | Guid | Hayır | **Optional MDM referansı**; master kopyalanmaz/resolve zorunlu değil |
| `ProductId` | Guid | Hayır | **Optional MDM referansı**; master kopyalanmaz |
| `CampaignId` / `SegmentId` | Guid | Hayır | Opsiyonel metadata; Campaign runtime bu FU'da değişmez |
| `LanguageCode` | string | Evet | Aynı `ContentCode` altında çok dilli sürümler mümkün |
| `Summary` | string | Hayır | Kısa özet (FU01 `Description`) |
| `ContentBodyRef` | string | Koşullu | Yapılandırılmış gövde işaretçisi (§8.1); pointer, dosya değil |
| `ContentAssetRef` / `FileRef` | string | Koşullu | **MOD-0028/0029 doküman referansı** (documentId+versionId); depo açılmaz |
| `Url` | string | Koşullu | Dış kaynak bağlantısı |
| `ContentVersion` | string | Evet | İş sürümü (naming divergence — §4) |
| `EffectiveFrom` | DateTimeOffset | Evet | |
| `EffectiveTo` | DateTimeOffset | Hayır | `EffectiveTo < EffectiveFrom` → **400** |
| `Source` | string | Evet | MOD-0048 `knowledge-content-source`: `manual·campaign·legacy-import·training·external·other` |
| `Tags[]` | string[] | Hayır | Serbest etiket; taksonominin yerine geçmez |
| `ExternalReferences[]` | object[] | Hayır | `SourceSystem·ExternalId·ExternalCode·ExternalName·ImportedAt·IsPrimary` |
| `IsArchived` / `ArchivedAt` | bool/DTO | Sistem | Archive lifecycle |
| `CreatedAt·CreatedBy·UpdatedAt·UpdatedBy` | audit | Sistem | `EntityBase` |

### 8.1 Payload / dosya boundary (kritik)

`ContentBodyRef` / `ContentAssetRef` / `FileRef` / `Url`'den **en az biri** zorunludur. FU02 **dosya yükleme, dosya
depolama, önizleme üretimi, içerik render'ı YAPMAZ.** Binary depo **açılmaz**; repoda canlı olan Document Management
(MOD-0028/0029) referansla tüketilir. Aynı dosyanın ikinci kopyası `KnowledgeContent` içinde tutulmaz.

## 9. Subject / Topic / AudienceProfile Model

### 9.1 `Subject`

| Field | Type | Required | Kural |
|---|---|---|---|
| `SubjectId` | Guid | Sistem | |
| `SubjectCode` | string | Evet | Tenant içinde **unique**, stabil; rename `DisplayName`/`Alias` ile |
| `SubjectName` (`DisplayName`) | string | Evet | |
| `Description` | string | Hayır | |
| `Status` | string | Evet | `draft·active·inactive·archived` |
| `SortOrder` | int | Evet | |
| `EffectiveFrom·EffectiveTo` | DateTimeOffset | Evet/Hayır | `EffectiveTo < EffectiveFrom` → 400 |
| `Alias[]` | string[] | Hayır | Eski ad/kod (arama + geçmiş referans) |
| `ExternalReferences[]` | object[] | Hayır | |
| `IsArchived` + audit | — | Sistem | Hard delete yok |

### 9.2 `Topic` (hiyerarşik)

| Field | Type | Required | Kural |
|---|---|---|---|
| `TopicId` | Guid | Sistem | |
| `SubjectId` | Guid | Evet | Topic yalnız **kendi subject'i** içinde yaşar |
| `TopicCode` | string | Evet | Subject içinde stabil |
| `ParentTopicId` | Guid | Hayır | Parent-child; **cross-subject parent → 400**; **cycle → 400**; self-parent → 400 |
| `TopicName` (`DisplayName`) | string | Evet | Rename kodu bozmaz |
| `Description` | string | Hayır | |
| `Status` | string | Evet | `draft·active·inactive·archived` |
| `SortOrder` | int | Evet | |
| `EffectiveFrom·EffectiveTo` | DateTimeOffset | Evet/Hayır | |
| `Alias[]` / `ExternalReferences[]` | — | Hayır | |
| `IsArchived` + audit | — | Sistem | Archived topic'e yeni içerik bağlanamaz; mevcut bağlı kalır, history korunur |

> Maksimum hiyerarşi derinliği implementasyonda sabitlenir (öneri **5**). `Indication`/`Need`/`Benefit` gibi
> **kavramlar Topic ağacına gömülmez** — onlar FU01C concept-graph'ındadır (bu FU'da runtime yok).

### 9.3 `AudienceProfile` (generic)

| Field | Type | Required | Kural |
|---|---|---|---|
| `AudienceProfileId` | Guid | Sistem | |
| `ProfileCode` | string | Evet | Tenant içinde stabil/unique |
| `ProfileName` (`DisplayName`) | string | Evet | |
| `Description` | string | Hayır | |
| `Status` | string | Evet | MOD-0048 `audience-profile-type` bağlamı + `draft·active·inactive·archived` |
| `SortOrder` | int | Evet | |
| `EffectiveFrom·EffectiveTo` | DateTimeOffset | Evet/Hayır | |
| `Alias[]` / `ExternalReferences[]` | — | Hayır | |
| `IsArchived` + audit | — | Sistem | Archived profil yeni içeriğe bağlanamaz |

Profil **generic**tir: `DoctorProfile` ayrı nesne **değildir** (pharma doktor profili = generic `AudienceProfile`'ın
bir kaydı). Profil ↔ contact/segment/pozisyon eşleştirme kuralı bu FU'da **yazılmaz** (tüketici tarafı).

## 10. API Contract

Route sözleşmesi (Gateway `5000` üzerinden; downstream `Diten.CrmService` port `5061`):

```text
GET    /api/crm/knowledge/contents
POST   /api/crm/knowledge/contents
GET    /api/crm/knowledge/contents/{contentId}
PUT    /api/crm/knowledge/contents/{contentId}
POST   /api/crm/knowledge/contents/{contentId}/archive

GET    /api/crm/knowledge/subjects
POST   /api/crm/knowledge/subjects
GET    /api/crm/knowledge/subjects/{subjectId}
PUT    /api/crm/knowledge/subjects/{subjectId}
POST   /api/crm/knowledge/subjects/{subjectId}/archive

GET    /api/crm/knowledge/topics                 (filter: subjectId)
POST   /api/crm/knowledge/topics
GET    /api/crm/knowledge/topics/{topicId}
PUT    /api/crm/knowledge/topics/{topicId}
POST   /api/crm/knowledge/topics/{topicId}/archive

GET    /api/crm/knowledge/audience-profiles
POST   /api/crm/knowledge/audience-profiles
GET    /api/crm/knowledge/audience-profiles/{audienceProfileId}
PUT    /api/crm/knowledge/audience-profiles/{audienceProfileId}
POST   /api/crm/knowledge/audience-profiles/{audienceProfileId}/archive

GET    /api/crm/knowledge/contract
```

Kurallar:
- **DELETE yok** (hiçbir kaynakta). Lifecycle yalnız `archive` POST.
- **TenantId payload yok**; JWT claim'inden çözülür.
- Direct `5061` frontend call yok; same-origin MVC proxy / Gateway.
- Response / `reasonCode` / `correlationId` zarfı (`Response<T>`) korunur.
- Archived kayıt **okunabilir**; archived kayıt **update → 409**; archive **idempotent** (already-archived düzgün cevap).
- Contract endpoint capability flags + vocabulary + permissions + limitations yayınlar.

### 10.1 Backend File Convention (Golden Compact birebir)

Her Feature (Content / Subject / Topic / AudienceProfile) için:

```text
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/{Feature}/
├── Commands/
│   ├── Create{Feature}Command.cs        (sealed record, IRequest<Response<Guid>>)
│   ├── Update{Feature}Command.cs        (sealed record, IRequest<Response<NoContent>>)
│   └── Archive{Feature}Command.cs       (soft lifecycle — Delete/BulkDelete YOK)
├── Queries/
│   ├── Get{Feature}ListQuery.cs
│   └── Get{Feature}ByIdQuery.cs
├── Handlers/
│   ├── CommandHandlers/                 (Create/Update/Archive{Feature}Handler — suffix YOK)
│   └── QueryHandlers/                   (Get{Feature}List/ByIdHandler)
├── Validators/
│   ├── Create{Feature}Validator.cs      (suffix YOK)
│   └── Update{Feature}Validator.cs
└── {Feature}Models.cs                   (TEK dosyada DTO/ViewModel)
```

**Not:** Golden Reference'ın `Delete`/`BulkDelete` command'ları bu modülde **kasıtlı yoktur** (hard delete yasağı);
yerine `Archive{Feature}Command` gelir. Bu, Golden'dan bilinçli ve gerekçeli sapmadır (§18'de belgelenecek).
Handler/Validator isimlerinde `Command`/`Query` suffix **yok**. Contract endpoint için `Features/Knowledge/Contract/`
altında `GetKnowledgeContractQuery` + handler (Territory `GetTerritoryContractQuery` precedent'i).

## 11. UI Scope

Primary surface: **CRM Admin → Knowledge**. Golden reference: **compact** (`KnowledgeContent` formu 18 alan > 8).

UI pages:

```text
Knowledge Content List           (DataTable v2)
Knowledge Content Create         (full-page Compact + _Form)
Knowledge Content Edit           (full-page Compact + _Form)
Knowledge Content Detail         (ayrı Details sayfası)
Knowledge Content Archive        (Golden Slim confirmation modal → archive POST)
Subject List / Create / Edit / Detail / Archive
Topic List / Create / Edit / Detail / Archive            (subject-scoped, hiyerarşi görünümü)
AudienceProfile List / Create / Edit / Detail / Archive
```

Kurallar:
- **DataTable v2** (`data-dt-standard="v2"` + skeleton + filter + toolbar + pagination + sort + loading/empty/error).
- Full-page Compact Create/Edit; `KnowledgeContent` için `_CreateEditOffcanvas` **yasak**.
- Taxonomy alt-formları (Subject/Topic/AudienceProfile, ≤8 alan) için Golden **Slim** canvas/offcanvas kullanılabilir.
- Archive modal Golden Slim `window.showConfirm`; toast Golden Slim `window.showToast`. Ham `alert/confirm/Swal.fire` yasak.
- **Hardcoded görünür metin yok**; 7 dil RESX (`en·fr·es·zh·ar·ru·tr`) + `window.L10n` bridge parity.
- Brand/Product/Concept selector gerekiyorsa **yalnız Gateway** `/api/mdm/brands`, `/api/mdm/products` üzerinden okunur.
- **Fake Brand/Product name üretilmez**; çözümlenemezse **raw ID** gösterilir.
- Contract flag'i yok/false ise ilgili action hide/disable; contract okunamazsa action'lar **fail-closed** kapalı.

### 11.1 Frontend File Contract (Compact — `KnowledgeContent` örneği)

```text
frontend/Diten.Web/
├── Controllers/CRM/KnowledgeController.cs
├── Models/CRM/KnowledgeViewModels.cs
├── Views/CRM/Knowledge/
│   ├── Index.cshtml            (Layout = "_LayoutTenantShell"; content list)
│   ├── Create.cshtml  Edit.cshtml  Details.cshtml  _Form.cshtml
│   ├── _Filter.cshtml  _DataTable.cshtml  _IndexL10n.cshtml
│   ├── Subjects/  Topics/  AudienceProfiles/   (list + Slim canvas alt yüzeyleri)
│   └── KnowledgeIndex.cs
├── wwwroot/assets/js/CRM/Knowledge/  (index.js, index.l10n.js, form.js, details.js)
└── Resources/Views/CRM/Knowledge/    (KnowledgeIndex.{en,fr,es,zh,ar,ru,tr}.resx)
```

## 12. Campaign Content Linkage — Read Provider

Campaign runtime (MOD-0165-FU04) **değiştirilmeden**, FU02 bir **read provider / read contract** yayınlar; Campaign
ileride bunu tüketebilir. Bu FU Campaign'e satır/alan **yazmaz**, Campaign kodunu **değiştirmez**.

```text
IKnowledgeContentLinkageReader (öneri):
GET /api/crm/knowledge/contents?subjectId=…&topicId=…&audienceProfileId=…&language=…
    &campaignId=…&brandId=…&productId=…&effectiveAt=…&status=published
→ yalnız published + effective satırlar (+ ContentVersion, ContentCode, FileRef/Url)
```

Katalog **liste döndürür, karar vermez**: sıralama/skor/"en iyi içerik"/ziyaret planı **üretmez** (MOD-0155 / recommendation
kapsamı değildir). Campaign runtime mutation bu FU'da **yasaktır**.

## 5. Repo Scope

Yalnız aşağıdaki alanlarda değişiklik yapılabilir:

- `execution/domains/commercial-suite/module-packs/MOD-0162-FU02-knowledge-content-runtime-ui.md`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/**` (yeni)
- `services/Diten.CrmService/src/Diten.CrmService.Domain/**` — yalnız yeni Knowledge aggregate'leri
- `services/Diten.CrmService/src/Diten.CrmService.Infrastructure/**` — yeni repository'ler + `RegisterClassMaps` kaydı
- `services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/Knowledge*Controller.cs` (yeni)
- `services/Diten.CrmService/**/tests/**` — yalnız Knowledge test dosyaları
- `frontend/Diten.Web/Controllers/CRM/KnowledgeController.cs` (yeni)
- `frontend/Diten.Web/Models/CRM/KnowledgeViewModels.cs` (yeni)
- `frontend/Diten.Web/Views/CRM/Knowledge/**` (yeni)
- `frontend/Diten.Web/Resources/Views/CRM/Knowledge/**` (yeni)
- `frontend/Diten.Web/wwwroot/assets/js/CRM/Knowledge/**` (yeni)
- `frontend/Diten.Web/Resources/SharedResource.{en,fr,es,zh,ar,ru,tr}.resx` — yalnız Knowledge menü/shared key'leri
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` — yalnız §13 dar navigation istisnası
- `docs/audits/mod-0162-fu02-knowledge-content-runtime-ui-*.md` — implementation evidence

Ortak helper'lar tüketilebilir ama değiştirilemez; zorunlu görünürse orchestrator **durur** ve ayrı authorization ister.

## 6. Protected Paths

- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`, `_LayoutPlatformAdmin.cshtml`
- `_LayoutTenantShell.cshtml` — §13 dar istisnası **dışında** değiştirilmez
- `frontend/Diten.Web/Controllers/Archive/**`, `Views/Archive/**`
- `services/Diten.CrmService/**/Features/Campaign/**`, `Consent*`, `ConsentPreference/**` (yalnız okunur; mutation yasak)
- Diğer tüm `services/**` (`Diten.MdmService` Brand/Product dâhil — yalnız Gateway'den tüketilir)
- `gateway/Diten.ApiGateway/**` ve özellikle `ocelot.json` (integration-agent — §11 kararı)
- `execution/registries/**`, Auth seed/grant, migrations, Mongo data

## 7. Dependencies

- **MOD-0162-FU01** (SoT boundary — §4–§9 sözleşmesi; **approved olması ready-for-dev ön koşuludur**).
- MOD-0162-FU01A/B/C (kardeş boundary'ler — bu FU'da runtime **açılmaz**; ConceptNodeId yalnız format-level).
- MOD-0165-FU04 (Campaign runtime — read provider tüketicisi; değişmez).
- MOD-0290 / MDM (Brand/Product SoR — Gateway `/api/mdm/*` reference).
- MOD-0048 (knowledge reference set'leri — publish ayrı operatör aksiyonu, F-RD).
- MOD-0028/0029 (Document Management — FileRef).
- MOD-0018 (RBAC — yalnız tüketim; seed/grant en sona, F-RBAC).
- DEV-0001 (Compact) + DEV-0000 (Slim) Golden Reference'ları.

## Runtime Constraints

- Persistence: MongoDB, multi-tenant logical isolation, `TenantId` **zorunlu** (JWT), cross-tenant **404**.
- Tüm frontend/business çağrıları Gateway `5000`; direct `:5061` yasak.
- Payload'da `TenantId` yok; archive-only lifecycle, `DELETE` yok.
- Contract okunamazsa fail-closed; unknown/future response alanları sessizce ignore.
- Backend'in desteklemediği filtre client-side fake filter olarak uygulanmaz; disabled/omitted + evidence limitation.

## 13. Protected Navigation Authorization (dar `_LayoutTenantShell` istisnası)

Domain config `_LayoutTenantShell.cshtml`'i protected sayar. Module Pack daha spesifik otorite olarak yalnız şuna izin verir:

- Dosya: `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml`.
- Section: mevcut Commercial Suite menü grubunda Accounts/Contacts/Territory/Campaigns komşuluğu.
- İzinli değişiklik: `/CRM/Knowledge` deep linkine giden tek `<li>` (+ gerekiyorsa grup header boolean'ının minimal genişletilmesi).
- Guard: `Perms.Has("crm.knowledge.read")`. RBAC fallback nedeniyle canonical key claim'de yoksa mevcut resolver
  davranışı **raporlanır**; seed/grant veya genişleten yeni resolver **yazılmaz** → **PARTIAL/follow-up** (§15).
- Label: 7 dilli `KnowledgeMenu` shared key; **hardcoded görünür metin yok**.
- Aktif route: `currentPath.StartsWith("/CRM/Knowledge", StringComparison.OrdinalIgnoreCase)`.
- Yasak: layout yapısı, diğer menü öğeleri, DynamicModuleMenu, token/cookie akışı, navigation API, CSS/JS bundle.

MOD-0285 dynamic navigation loader data-driven olduğunda hardcoded Knowledge `<li>`'nin kaldırılması ayrı follow-up'tır;
çift menü kabul edilmez.

## 14. Golden UI Decision

- `golden_reference: compact`. `KnowledgeContent` create/edit formu 18 kullanıcı alanı (> 8) → **Compact** (ayrı
  Create/Edit/Details sayfaları + `_Form`).
- Taxonomy alt-nesneleri (Subject/Topic/AudienceProfile ≤ 8 alan) Golden **Slim** canvas/offcanvas ile aynı modül
  yüzeyinde yönetilir; ayrı Compact sayfa zorunlu değildir.
- Archive confirmation ve toast Golden **Slim** pattern'i (`window.showConfirm` / `window.showToast`).

## Layout & Shell Contract

- `shell: tenant`. Bütün Knowledge Razor page'lerinde açıkça `Layout = "_LayoutTenantShell";` yazılır.
- View root: `frontend/Diten.Web/Views/CRM/Knowledge/`. MVC route: `/CRM/Knowledge`.
- Breadcrumb: `CRM Admin → Knowledge → Detail/Create/Edit`. Yeni shell/table/modal/toast pattern'i icat edilmez.

## Validation Rules

| Alan | Required | Kural | DB-level | Pre-check |
|---|---|---|---|---|
| `ContentCode` | Evet | Trim, max 100, stabil | Aktif unique (tenant) | `ExistsByContentCodeAsync` |
| `ContentTitle` | Evet | Trim, max 300 | — | — |
| `ContentType` | Evet | MOD-0048 set üyesi; set yoksa 400 | — | reference set fail-closed |
| `ContentStatus` | Evet | MOD-0048 set üyesi; unknown → 400 | — | — |
| `SubjectId` | Evet | Var + **archived değil** | — | `SubjectExists && !Archived` |
| `TopicId` | Hayır | Verilirse subject ile tutarlı + archived değil | — | topic∈subject check |
| `AudienceProfileId` | Hayır | Verilirse archived değil | — | — |
| `BrandId`/`ProductId`/`ConceptNodeId` | Hayır | Boş veya geçerli GUID (**format-level**; master resolve zorunlu değil) | — | — |
| `LanguageCode` | Evet | ISO benzeri kod | — | — |
| body/asset/url | Koşullu | `ContentBodyRef`/`ContentAssetRef`/`FileRef`/`Url`'den ≥1 | — | — |
| `ContentVersion` | Evet | Trim; `(ContentCode,Language)` örtüşen effective published → 409 | — | overlap check |
| `EffectiveFrom` | Evet | Geçerli tarih | — | — |
| `EffectiveTo` | Hayır | `>= EffectiveFrom` | — | — |
| `Source` | Evet | MOD-0048 set üyesi | — | — |
| `SubjectCode`/`TopicCode`/`ProfileCode` | Evet | Trim, stabil, unique (scope'unda) | Unique index | `Exists…Async` |
| `ParentTopicId` | Hayır | Same-subject; **cycle/self yasak** → 400 | — | cycle detection |
| `TenantId` | **Forbidden** | Payload'da bulunmaz | — | — |

## Failure Path to Verify

- **Duplicate `ContentCode`** → **409** + field-level error + kayıt oluşmaz + reload temiz state.
- **Unknown `ContentStatus`/`ContentType`/`Source`** (set üyesi değil) → **400** + validator mesajı.
- **Reference set yayınlanmamış** → **fail-closed 400** (hardcoded fallback yok).
- **Archived subject/topic'e yeni içerik** → **409/400**; mevcut bağlı içerik korunur.
- **Archived content update** → **409** ("data changed / archived", sessiz overwrite yok).
- **`EffectiveTo < EffectiveFrom`** → **400**.
- **Örtüşen effective published `(ContentCode,Language)`** → **409** (sessiz seçim yok).
- **Cross-subject / cycle `ParentTopicId`** → **400**.
- **Cross-tenant erişim** → **404**.
- **DELETE denemesi** → **404/405** (route yok).
- **Unauthorized actor** → **403** + UI action disabled (RBAC fallback davranışı raporlanır).

## Authorization Convention

- Policy: `[Authorize]` (shell: tenant). Actor: authenticated tenant user.
- Permission format: PKS-001 lowercase-dotted `{module}.{resource}.{action}`.
- Canonical (öneri): `crm.knowledge.read` (menu/list/detail) · `crm.knowledge.manage` (content create/edit/archive) ·
  `crm.knowledge.subject.read` · `crm.knowledge.subject.manage` (subject/topic/audience-profile taxonomy).
- **Seed/grant YOK** (RBAC en sona — §15, F-RBAC). Katalog hazır değilse MOD-0151 FU08 precedent'i: anahtar tanımlanır
  ama `All` listesine eklenmez + geçici read/manage fallback + `-RBAC` follow-up; UI mevcut resolver'dan daha geniş
  yetki türetmez.

## 15. Permission / Visibility

RBAC alignment kullanıcı direktifiyle **en sona** bırakıldığından bu FU'da seed/grant yapılmaz.

- Permission yoksa menü/list/action görünürlüğü **PARTIAL** kalabilir (PASS blocker değildir).
- **Hardcoded allow yok.** Mevcut fallback varsa **raporlanır**, genişletilmez.
- Canonical `crm.knowledge.*` katalog + grant ayrı task: **F-RBAC**.

## 16. Contract Flags

`GET /api/crm/knowledge/contract` şunları yayınlayabilir:

```json
{
  "supportsKnowledgeContentManagement": true,
  "supportsSubjectTaxonomyManagement": true,
  "supportsConceptGraphReference": true,
  "supportsBrandProductReference": true,
  "supportsArchiveLifecycle": true,
  "supportsEffectiveDating": true,
  "supportsContractDrivenUi": true
}
```

`supportsConceptGraphReference` = yalnız **format-level ConceptNodeId referansı** (FU01C runtime değil).
`supportsBrandProductReference` = yalnız optional MDM reference (master ownership değil).

**Response'ta HİÇ bulunmayacak yasak flag'ler:**

```text
supportsVisitPlanning · supportsRoutePlanning · supportsRecommendationEngine ·
supportsDigitalDetailingRuntime · supportsWorkflowApproval · supportsCampaignRuntimeMutation ·
supportsBrandProductMasterOwnership · supportsFileStorage · supportsHardDelete
```

## 17. Tests / Smoke Acceptance

### Backend tests (minimum)

1. KnowledgeContent create valid.
2. Duplicate `ContentCode` (tenant içinde) → 409.
3. Unknown `ContentStatus` → 400.
4. `BrandId`/`ProductId` format/reference validation (boş veya geçerli GUID; resolve zorunlu değil).
5. Content archive soft lifecycle.
6. Archived content update → 409.
7. Content DELETE unsupported (route yok → 404/405).
8. Subject/Topic/AudienceProfile create valid.
9. Subject/Topic/AudienceProfile archive soft lifecycle.
10. Tenant isolation (cross-tenant → 404).
11. Contract flags true (§16 yedi flag).
12. Forbidden flags absent (§16 dokuz yasak flag).
13. No Campaign mutation (read provider Campaign'e yazmaz).
14. No Brand/Product mutation.
15. `dotnet build services/Diten.CrmService` PASS.

### UI tests

1. Knowledge route render (`_LayoutTenantShell`).
2. Menu permission guarded (`crm.knowledge.read`).
3. Contract loads.
4. Content list loads (loading/empty/error state).
5. Content create/edit validation (required/date; TenantId'siz POST/PUT).
6. Content archive **POST**, not DELETE.
7. Subject/Topic/AudienceProfile list loads.
8. Brand/Product selector **Gateway-only** (`/api/mdm/*`); çözülemezse raw ID.
9. No direct `5061`.
10. No `TenantId` payload.
11. No `DELETE`.
12. 7 language RESX parity.
13. `dotnet build frontend/Diten.Web` + `verify_datatable_page.py --area CRM --module Knowledge --reference compact` PASS.

### Smoke (authenticated Gateway; hedef tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93`, `X-Tenant-Id` header)

1. Login. 2. Contract 200. 3. Create Subject (+ Topic + AudienceProfile). 4. Create KnowledgeContent. 5. Read detail.
6. Archive. 7. Archived update → 409. 8. DELETE → 404/405. 9. No Campaign mutation. 10. No Brand/Product mutation.
11. Gateway-only network. 12. Cleanup **archive only** (hard delete yok). RBAC grant yoksa positive smoke deferred
yazılır (fake veri / Mongo hand-edit yapılmaz).

## 18. Explicit Exclusions

Concept graph runtime (FU01C) · KnowledgePath runtime (FU01A) · EngagementJourney runtime (FU01B) · digital detailing ·
recommendation engine · visit planning · route planning · MOD-0155 (herhangi bir yüzeyi) · frequency runtime ·
Campaign runtime/UI mutation · Consent runtime/UI · Brand/Product runtime & ownership · workflow/approval (MOD-0023) ·
e-signature · file/binary storage · içerik render/preview · arama indeksi · import/export engine · patient data ·
Account/Contact/Territory mutation · hard delete · HTTP DELETE · Mongo hand-edit · RBAC seed/grant · MOD-0048 publish ·
registry write · `TenantId` payload · direct `:5061` business call · fake Brand/Product name resolution.

> Golden Reference'ın `Delete`/`BulkDelete` command'ları ve DataTable bulk-delete kolonu bu modülde **kasıtlı yoktur**
> (hard delete yasağı). Bu, Golden'dan gerekçeli tek yapısal sapmadır.

## Acceptance Criteria

- [ ] `/CRM/Knowledge` route'u `_LayoutTenantShell` ile render olur; Knowledge menüsü `crm.knowledge.read` guard'lıdır (§13).
- [ ] `KnowledgeContent` CRUD-minus-delete + archive Gateway üzerinden çalışır; DELETE yok.
- [ ] `Subject`/`Topic`/`AudienceProfile` CRUD-minus-delete + archive çalışır; hiyerarşi cross-subject/cycle 400 verir.
- [ ] Reference set yayınlanmadan create/update **fail-closed 400** (hardcoded enum yok).
- [ ] Archived kayıt okunur; archived update 409; archive idempotent.
- [ ] `EffectiveTo < EffectiveFrom` 400; örtüşen effective published `(ContentCode,Language)` 409.
- [ ] `BrandId`/`ProductId`/`ConceptNodeId` **format-level** taşınır; master fetch **yalnız** `/api/mdm/*` Gateway; çözülemezse raw ID.
- [ ] Campaign content linkage **read provider** olarak yayınlanır; Campaign runtime **değişmez**.
- [ ] Contract yedi flag'i döner; dokuz yasak flag response'ta **yok**.
- [ ] Content list DataTable v2 / Golden Compact-Slim state contractına uyar; Create/Edit full-page Compact + `_Form`.
- [ ] Tüm yeni görünür metin 7 dil RESX/L10n parity; hardcoded metin yok.
- [ ] Frontend'de direct `5061`, `DELETE`, `TenantId` payload veya yasak response alanı **yok**.
- [ ] CrmService + Diten.Web build, backend/UI tests, DataTable verifier, RESX parity PASS; mümkünse authenticated smoke PASS.
- [ ] Campaign, Consent, Brand/Product, Gateway config, registry, seed/grant, Mongo **değişmemiştir**; MOD-0155 açılmamıştır.

## Ready-for-dev Checklist

- [x] AGENTS.md, Commercial Suite domain config, module-pack-standard, registry, FU01–FU01C boundary'leri okundu.
- [x] DCP-002 identity preflight PASS (2026-08-09).
- [x] Golden Reference Compact + Slim pack ve canlı kod referans alındı; CrmService Feature pattern doğrulandı.
- [x] Frontmatter tüm zorunlu alanları içeriyor; `golden_reference: compact`, `form_field_count: 18 (>8)`.
- [x] Backend File Convention (Golden Compact + archive-yerine-delete sapması gerekçeli) yazıldı.
- [x] Frontend File Contract Compact + Slim seti listelendi; Layout açıkça `_LayoutTenantShell`.
- [x] Validation Rules her field; Failure Path ≥ 4 senaryo (duplicate/missing/unauthorized/concurrency-archived).
- [x] Authorization + permission listesi + fallback sınırı yazıldı; seed/grant yapılmadı.
- [x] Contract flags (7 pozitif / 9 yasak) yazıldı.
- [x] **MOD-0162-FU01 `approved`** (SoT boundary onayı) — **✅ 2026-08-09** (F-BND resolved; FU01A/01C de approved, FU01B held-non-blocking).
- [x] **Gateway `/api/crm/knowledge*` route authorization** — **✅ 2026-08-09** (F-GW resolved; 2 route bloğu eklendi, §11).

## 11 (Gateway / API Routing Decision)

Karar: **✅ Gateway route EKLENDİ (F-GW resolved, 2026-08-09).** Route authorization tamamlandı; `ocelot.json`'a
Campaigns precedent'i birebir iki blok eklendi (toplam route 114 → 116):

```text
/api/crm/knowledge                 ↔  downstream /api/crm/knowledge              (localhost:5061, GET/POST/PUT/OPTIONS)
/api/crm/knowledge/{everything}    ↔  downstream /api/crm/knowledge/{everything} (localhost:5061, GET/POST/PUT/OPTIONS)
```

- **DELETE / PATCH eklenmedi.** Downstream `Diten.CrmService:5061`.
- Mevcut `/api/crm/{campaigns,consents,preferences,...}`, `/api/mdm/brands`, `/api/legal-entities` route'ları **korundu** (doğrulandı).
- Brand/Product için `/api/mdm/*` route'u değişmedi; Knowledge için `/api/mdm/*` **kullanılmaz**.
- Runtime henüz yok olduğundan canlıda `/api/crm/knowledge` 404/502 dönebilir; bu route authorization için FAIL değildir
  (implementation FU02 kapsamında). Gateway restart gerekebilir. Bkz.
  [F-GW audit](../../../../docs/audits/mod-0162-fu02-f-gw-knowledge-gateway-route-authorization-2026-08-09.md).

## Implementation Notes

- Sıralama önerisi: (1) MOD-0048 knowledge reference set'leri authoring + publish (F-RD), (2) Gateway route (F-GW),
  (3) backend aggregate'ler + contract, (4) UI, (5) authenticated smoke, (6) RBAC (F-RBAC).
- Yeni aggregate'ler `RegisterClassMaps`'e eklenir; `EffectiveFrom`/`EffectiveTo` **birlikte index/sort edilmez**;
  DateTimeOffset instant-vs-date karşılaştırmalarında `.Date` tuzağına dikkat ([crm-datetimeoffset-array-pitfalls]).
- Çoklu-doc atomik yazımda `SupportsTransactionsAsync` guard + compensation (standalone dev Mongo — [crm-standalone-mongo-transaction-fallback]).
- Contract endpoint için `Features/Knowledge/Contract/GetKnowledgeContractQuery` (Territory contract precedent'i).
- Campaign controller (`services/Diten.CrmService/.../Api/Controllers/CRM/CampaignsController.cs`) controller/route
  yapısı için birebir örnektir.
- Implementation başladığında status `in-progress` → test sonrası `review` → kabulde `done`; kod ilk kez kazanacağı için
  `execution/registries/module-implementation-status.md` MOD-0162 satırı **implementation closeout**'ta güncellenir
  (bu hazırlık task'ı registry write **yapmaz**).

## 19. Created / Updated Files

- **Created:** `execution/domains/commercial-suite/module-packs/MOD-0162-FU02-knowledge-content-runtime-ui.md` (bu dosya).
- **Created:** `docs/audits/mod-0162-fu02-knowledge-content-runtime-ui-module-pack-authorization-2026-08-09.md` (authorization evidence).
- **Updated:** yok (registry/status/Mongo/kod değişmedi — authorization task'ı).

## 20. Follow-up Items

| # | Follow-up | Owner | Neden |
|---|---|---|---|
| F-BND | ✅ **RESOLVED 2026-08-09** — MOD-0162-FU01 `approved` (FU02'nin tek zorunlu SoT'u); FU01A/01C de `approved`; FU01B held (MOD-0166 adlandırma, FU02 için non-blocking) | Reviewer / EA | SoT sözleşmesi onaylandı |
| F-GW | ✅ **RESOLVED 2026-08-09** — `/api/crm/knowledge` + `/api/crm/knowledge/{everything}` eklendi (5061, GET/POST/PUT/OPTIONS, DELETE/PATCH yok) | integration-agent | Route authorization tamamlandı; §11 |
| F-RD | **MOD-0048 knowledge reference set publish** (`knowledge-content-type`/`-status`/`-source`/`audience-profile-type`) | MOD-0048 operator | Hardcoded enum yasağı; runtime prereq |
| F-RBAC | **MOD-0162-FU02-RBAC — `crm.knowledge.*` katalog + grant** | MOD-0018 / commercial-suite | RBAC en sona (§15) |
| F-DOC | **Content ↔ Document Management linkage sözleşmesi** (`FileRef` = documentId+versionId) | MOD-0028/0029 | Çift kopya yasağı sözleşmeye bağlanmalı |
| F-A/B/C | **KnowledgePath / EngagementJourney / Concept Graph runtime implementation** | commercial-suite | Ayrı FU'lar; bu FU'da açılmaz |
| F-WF | **Content approval workflow (MOD-0023)** — `review`/`approved` bugün yalnız metadata | commercial-suite | En sona |
| F-155 | **MOD-0155 Visit/Route Planning** — Knowledge runtime/UI PASS sonrası | commercial-suite | Beklemede |

### Next Recommended Prompt (✅ F-BND + F-GW karşılandı — implementasyona hazır)

```text
@orchestrator execution/domains/commercial-suite/module-packs/MOD-0162-FU02-knowledge-content-runtime-ui.md

MOD-0162-FU02 — Knowledge / Content Taxonomy Runtime + UI Implementation
```

> **Not:** RBAC alignment en sona bırakılacak. MOD-0155 hâlâ beklemede kalacak.
> Target Customer → Lead → Opportunity hattı Knowledge runtime/UI sonrası değerlendirilecek.

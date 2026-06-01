# Module Pack Standard (ERP-vNext)

Bu standart, `execution/domains/{domain}/module-packs/{ID}-{slug}.md` dosyalarinin minimum formatini tanimlar.

> **Otorite zinciri:** Module Pack > Domain Config > AGENTS.md > `.antigravity/`
>
> **Tek gercek standart:** Yapi/naming/folder kararlari Golden Reference uzerinden tanimlanir.
> - Slim referans: `services/Diten.DevEnablementService/.../Features/GoldenReferenceSlim/` + `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`
> - Compact referans: `.../Features/GoldenReferenceCompact/` + `.../Views/DevEnablement/GoldenReferenceCompact/`
> - Pack-of-record: [DEV-0000-golden-reference-slim.md](../../execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md) ve [DEV-0001-golden-reference-compact.md](../../execution/domains/developer-enablement/module-packs/DEV-0001-golden-reference-compact.md)

---

## 1. File Naming

Yeni dosyalar icin zorunlu format:

```text
{ID}-{slug}.md
```

Kurallar:
- Yeni ERP product module ID: `MOD-NNNN`
- Follow-up ID: `MOD-NNNN-FUxx`
- Delivery Capability Pack ID: `DCP-NNN` (module pack degil, kendi portfolio klasorunde tutulur)
- Developer Enablement golden reference ID: `DEV-NNNN`
- `slug`: kucuk harf + tire ayirici (`product-management`)
- Tarihsel domain-prefixed veya legacy ID'ler registry-controlled identity olarak korunur; toplu rename yapilmaz.

Ornekler:
- `MOD-0018-rbac-abac-authorization.md`
- `MOD-0018-FU12-tenant-authorization-context-foundation.md`
- `DEV-0000-golden-reference-slim.md`

---

## 2. YAML Frontmatter (Required)

Her module pack dosyasi asagidaki frontmatter ile baslamalidir:

```yaml
---
id: PSS-009
name: Platform Administrators Management
domain: platform-shared-services
service: Diten.Platform
shell: platform-admin
golden_reference: slim
entity_base: GlobalEntity
status: draft
owner: ali.tufanoglu
branch: feature/pss/pss-009-platform-administrators
started: 2026-05-12
target: 2026-05-25
form_field_count: 7
---
```

### Alan kurallari

| Alan | Tip | Zorunluluk | Aciklama |
|---|---|---|---|
| `id` | string | Zorunlu | Registry-controlled ID (`MOD-NNNN`, `MOD-NNNN-FUxx`, `DEV-NNNN`, veya korunmus legacy ID) |
| `name` | string | Zorunlu | Insan-okunur modul adi |
| `domain` | string | Zorunlu | Domain klasor adi ile birebir ayni |
| `service` | string | Zorunlu | Backend servis projesinin adi (`Diten.Platform`, `Diten.MdmService`, `Diten.DevEnablementService`, `Diten.AuthService`) |
| `shell` | enum | Zorunlu | `platform-admin` \| `tenant` \| `none` — frontend layout zorunlulugunu turetir |
| `golden_reference` | enum | DataTable modullerinde zorunlu | `slim` (≤8 form alani) \| `compact` (>8 form alani) \| `none` (DataTable disi modul) |
| `entity_base` | enum | Zorunlu | `EntityBase` \| `BaseEntity` \| `GlobalEntity` — somut sinif adi (servis bazli) |
| `status` | enum | Zorunlu | `draft` \| `approved` \| `ready-for-dev` \| `in-progress` \| `review` \| `done` \| `blocked` |
| `owner` | string | Zorunlu | Sorumlu kisi veya ekip |
| `branch` | string | Zorunlu | `feature/{domain-short}/{id-lower}-{slug}` |
| `started` / `target` | date | Zorunlu | `YYYY-MM-DD` |
| `form_field_count` | int | DataTable modullerinde zorunlu | Create/edit formundaki kullanici alanlari sayisi |

### `shell` × Layout Turetme

| `shell` degeri | Razor layout | View klasoru | Reference live module |
|---|---|---|---|
| `platform-admin` | `_LayoutPlatformAdmin` | `Views/Platform/{Module}/` | `Views/Platform/Tenants/` |
| `tenant` | `_LayoutTenantShell` | `Views/{Area}/{Module}/` | `Views/DevEnablement/GoldenReferenceSlim/` |
| `none` | — | (backend-only) | — |

Razor sayfasinda layout **ACIKCA** yazilir; `_ViewStart.cshtml` varsayilani degistirilmez:

```cshtml
@{
    ViewData["Title"] = ...;
    Layout = "_LayoutPlatformAdmin";   // shell: platform-admin
}
```

### `entity_base` Sinif Adi (Servis Bazli)

| Servis | `entity_base` degeri | Aciklama |
|---|---|---|
| `Diten.MdmService` | `EntityBase` | Tenant-owned, kavramsal EntityBase |
| `Diten.DevEnablementService` | `EntityBase` | Tenant-owned |
| `Diten.AuthService` | `EntityBase` | Tenant-owned |
| `Diten.Platform` (tenant-aware kayit) | `BaseEntity` | Platform service icinde EntityBase yerine BaseEntity adi kullanilir |
| `Diten.Platform` (cross-tenant katalog) | `GlobalEntity` | Platform seviyesi system-of-record (Tenant, SubscriptionPlan, ModuleCatalogItem, vb.) |

`GlobalEntity` istisnasi icin module pack gerekce yazmali: `Runtime Constraints` veya `Entity Schema Rules` bolumunde "kayit tenant-owned degildir, neden global oldugu" aciklanir; DTO/request payload icinde `TenantId` bulunmaz.

---

## 3. Golden Reference Kontrati (Zorunlu)

Module pack `golden_reference: slim` veya `compact` belirledikten sonra, modulun tum dosya yapisi, naming convention'i, partial seti, handler folder hiyerarsisi **bu referans modulun gercek kodunu birebir taklit eder**. Sapma teknik borctur ve kabul edilmez.

### Kaynaklar (zorunlu okuma)

- `golden_reference: slim`:
  - Backend: `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`
  - Frontend: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/`
  - Pack: `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`
- `golden_reference: compact`:
  - Backend: `services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/`
  - Frontend: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/`
  - Pack: `execution/domains/developer-enablement/module-packs/DEV-0001-golden-reference-compact.md`

### Sayim Kurali (Slim vs Compact)

Form alan sayimi sadece kullanicinin create/edit formunda doldurdugu modul alanlaridir.

**Sayilmayanlar:** `Id`, `TenantId`, `IsDeleted`, `CreatedAt`, `UpdatedAt`, `DeletedAt`, audit alanlari, DataTable checkbox/action kolonlari.

| Form alan sayisi | Karar |
|---|---|
| `8 ve alti` | `golden_reference: slim` → Index icinde create/edit offcanvas + QuickView offcanvas |
| `8'den fazla` | `golden_reference: compact` → ayri `Create/Edit/Details` sayfalari |

---

## 4. Backend File Convention (Golden Reference Birebir)

```text
services/{Service}/src/{Service}.Application/Features/{Module}/
├── Commands/
│   ├── Create{Module}Command.cs            (sealed record, IRequest<Response<Guid>>)
│   ├── Update{Module}Command.cs            (sealed record, IRequest<Response<NoContent>>)
│   ├── Delete{Module}Command.cs
│   └── BulkDelete{Module}Command.cs
├── Queries/
│   ├── Get{Module}ListQuery.cs             (sealed record, IRequest<Response<...>>)
│   └── Get{Module}ByIdQuery.cs
├── Handlers/
│   ├── CommandHandlers/                    ← AYRI klasor (zorunlu)
│   │   ├── Create{Module}Handler.cs        (sealed class, Command suffix YOK)
│   │   ├── Update{Module}Handler.cs
│   │   ├── Delete{Module}Handler.cs
│   │   └── BulkDelete{Module}Handler.cs
│   └── QueryHandlers/                      ← AYRI klasor (zorunlu)
│       ├── Get{Module}ListHandler.cs
│       └── Get{Module}ByIdHandler.cs
├── Validators/
│   ├── Create{Module}Validator.cs          (Command suffix YOK)
│   └── Update{Module}Validator.cs
└── {Module}Models.cs                       ← TEK dosyada tum DTO/ViewModel'ler
```

### Naming Kurallari

| Tip | Format | Ornek |
|---|---|---|
| Command (record) | `{Verb}{Module}Command` | `CreateGoldenReferenceSlimCommand` |
| Query (record) | `Get{Module}{Qualifier}Query` | `GetGoldenReferenceSlimByIdQuery` |
| Handler (class) | `{Verb}{Module}Handler` | `CreateGoldenReferenceSlimHandler` |
| Validator (class) | `{Verb}{Module}Validator` | `CreateGoldenReferenceSlimValidator` |

**Kritik:** Handler ve Validator isimlerinde **Command / Query / Request suffix YOK**. Sadece `{Verb}{Module}Handler` / `{Verb}{Module}Validator`.

### Yasaklar

- Tek dosyada birden fazla `public class` veya `public record`
- Handler isminde `*CommandHandler.cs` / `*QueryHandler.cs` / `*RequestHandler.cs` suffix
- `Handlers/CommandHandlers/` ve `Handlers/QueryHandlers/` ayrimi yapmamak
- `Application/Features/{Module}/Requests/Commands/` gibi ekstra alt klasor

---

## 5. Frontend File Contract

### Slim (`golden_reference: slim`)

```text
Views/{Area}/{Module}/
├── Index.cshtml                            (Layout AÇIKÇA yazılır)
├── _Filter.cshtml                          (inline collapsible filter)
├── _DataTable.cshtml                       (data-dt-standard="v2" + skeleton loader)
├── _IndexL10n.cshtml                       (JSON payload bridge)
├── _CreateEditOffcanvas.cshtml             (Slim-ozel)
├── _DetailsQuickView.cshtml                (Slim-ozel, QuickView offcanvas)
└── {Module}Index.cs                        (marker class)

wwwroot/assets/js/{Area}/{Module}/
├── index.js
└── index.l10n.js

Resources/Views/{Area}/{Module}/
└── {Module}Index.{lang}.resx               (en+tr zorunlu, Platform; 7 dil Tenant)
```

### Compact (`golden_reference: compact`)

Slim'e ek olarak:
```text
Views/{Area}/{Module}/
├── Create.cshtml                           (Compact-ozel, sayfa kabuk + _Form)
├── Edit.cshtml                             (Compact-ozel, sayfa kabuk + _Form)
├── Details.cshtml                          (Compact-ozel, ayri detay sayfasi)
└── _Form.cshtml                            (Create/Edit ortak form partial)
```

**Compact'ta YASAK:** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index icinde create/edit offcanvas.

### Index.cshtml Kontrati (Slim ornegi)

1. **Layout ACIKCA yazilir** (Razor block icinde, frontmatter `shell` alanindan turetilir)
2. **Partial path'leri absolute**: `~/Views/{Area}/{Module}/_Filter.cshtml`
3. **BulkActionBar ViewModel pattern**: `DataTableBulkActionBarViewModel` ile shared `_BulkActionBar.cshtml`
4. **Bolum sirasi:** ① Filter → ② BulkActionBar → ③ DataTable → ④ Offcanvas panels
5. **Contract marker yorumlari:** her partial cagrisindan once verifier'in aradigi marker yorum satiri

---

## 6. Required Sections (Pack Govdesi)

Frontmatter altinda asagidaki bolumler zorunludur:

| # | Bolum | Aciklama |
|---|---|---|
| 1 | `Module Summary` | Modulun amaci, hedef kullanici, kapasite ozeti |
| 2 | `Ownership and Boundaries` | In-scope / out-of-scope, sahip oldugu objeler |
| 3 | `Owned Objects` | Entity, repository, command, query, DTO, API endpoint, frontend route, permission listesi |
| 4 | `Entity Fields` | Schema, tip, zorunluluk, validation kurallari, MongoDB index ihtiyaci |
| 5 | `Repo Scope` | Dokunulacak somut klasor/dosya yollari |
| 6 | `Protected Paths` | Dokunulmayacak yollar (en az `.antigravity/**` ve domain-disi servisler) |
| 7 | `Dependencies` | Bagimli oldugu mevcut servis/modul/altyapi |
| 8 | `Runtime Constraints` | Gateway portu, route format, soft delete, GlobalEntity gerekcesi |
| 9 | `Layout & Shell Contract` | `shell` degeri + Razor `Layout = "..."` zorunlulugu + view klasoru |
| 10 | `Backend File Convention` | Golden Reference birebir folder/naming (Bolum 4 referansi) |
| 11 | `Frontend File Contract` | Slim/Compact dosya seti (Bolum 5 referansi) |
| 12 | `Validation Rules` | Field-level FluentValidation kurallari (required, format, business) |
| 13 | `Failure Path to Verify` | Duplicate, missing, unauthorized, concurrency senaryolari + beklenen davranis |
| 14 | `Authorization Convention` | Permission format (Platform.* vs Modules.*) + actor type + policy |
| 15 | `Gateway / API Routing Decision` | Yeni Ocelot route gerekli mi, integration-agent task'i mi |
| 16 | `Acceptance Criteria` | Test edilebilir, davranis odakli madde listesi |
| 17 | `Test Expectations` | Unit, integration, frontend smoke, build, verifier, RESX beklentileri |
| 18 | `Ready-for-dev Checklist` | Status `ready-for-dev`'e gecmeden once onaylanacak madde listesi |
| 19 | `Implementation Notes` | Master-plan saplamalari, kararlar, gelecek baglantilari |
| 20 | `Follow-up Items` | Sonraki sprint/wave'e birakilan isler |

---

## 7. Acceptance Criteria Kurallari

- Belirsiz ifadeler (`iyilestirildi`, `duzgun calisiyor`) kullanilmaz
- Somut endpoint, UI davranisi, localization ve quality gate adimlari yazilir
- DataTable modulunde `verify_datatable_page.py` ve `quality-gate-datatable` atiflari zorunludur
- Layout zorunlulugu test edilebilir madde olarak yer alir (ornek: "Tum `Views/Platform/Administrators/*.cshtml` dosyalarinda `Layout = \"_LayoutPlatformAdmin\"` ACIKCA yazili")
- Platform/Admin modul lookup kullaniyorsa AC icinde endpoint/proxy path, `LookupOptionDto` response shape, unauthorized davranis ve hardcoded fallback olmamasi test edilebilir yazilir. Yeni Platform lookup key gerekiyorsa bu key PSS lookup scope'u olarak acik onaylanir.

---

## 8. Validation Rules Bolumu Sablonu

Her field icin tablo:

```text
| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| Email | Evet | Email regex, max 256, lowercase normalize | Unique index | ExistsByEmailAsync |
| DisplayName | Evet | Trim, max 200 | — | — |
```

Iliskili kontroller acik yazilir (ornek: "PartnerAdmin → PartnerId NotEmpty").

---

## 9. Failure Path to Verify Bolumu Sablonu

Her hata senaryosu icin:

```text
- **Duplicate Email**
  - Expected: 409 + UI field-level error + kayit olusmaz + reload sonrasi temiz state
- **Missing DisplayName**
  - Expected: 400 + validator mesaji + save engellenir
- **Concurrency Conflict**
  - Expected: 409 + UI "data changed, reload required" + sessiz overwrite YOK
- **Unauthorized Actor**
  - Expected: 403 + UI action disabled veya permission-denied state
```

---

## 10. Authorization Convention Bolumu Sablonu

```text
Policy:     [Authorize(Policy = "PlatformActor")]   // shell: platform-admin
            [Authorize]                              // shell: tenant
Permission: [HasPermission("{Prefix}.{Resource}.{Action}")]
  - Platform service controller'lari:  Platform.{Resource}.{Action}
  - Tenant service controller'lari:    Modules.{ModuleName}.{Action}
Actions:    Read, Create, Update, Delete, BulkDelete (+ modul-spesifik aksiyonlar)
Actor type: platform_admin (otomatik tum permission'lara pass) | partner_admin | tenant_user
```

Modul-spesifik permission listesi (ornek):
```text
Platform.Administrators.Read
Platform.Administrators.Create
Platform.Administrators.Update
Platform.Administrators.Suspend
Platform.Administrators.AssignRoles
```

---

## 11. Gateway / API Routing Decision Bolumu Sablonu

```text
Karar: Gateway degisikligi {gerekli | gereksiz}.

- Frontend Gateway 5000 uzerinden cagirir; servis portuna dogrudan gitmez.
- Mevcut `ocelot.json`'da `/api/{kebab-resource}` catch-all var mi? {evet | hayir}
- Gerekli ise: explicit Upstream/Downstream cifti + OPTIONS metodu dahil integration-agent task'i olarak ayri yurutulur.
- `gateway/Diten.ApiGateway/**/ocelot.json` protected path; bu pack dogrudan yazmaz.
```

---

## 12. Ready-for-dev Checklist Bolumu Sablonu

```text
- [ ] Golden Reference (slim veya compact) referans olarak okundu
- [ ] Frontmatter tum zorunlu alanlar dolu (service, shell, golden_reference, entity_base)
- [ ] Layout & Shell Contract bolumunde Razor Layout acikca yazili
- [ ] Backend File Convention bolumunde folder/naming Golden Reference ile birebir
- [ ] Frontend File Contract bolumunde Slim/Compact dosya listesi tam
- [ ] Validation Rules her field icin yazili
- [ ] Failure Path to Verify en az 4 senaryo (duplicate, missing, unauthorized, concurrency)
- [ ] Authorization Convention permission listesi + policy + actor type
- [ ] Gateway routing karari acik (gerekli/gereksiz + integration-agent task'i)
- [ ] Platform/Admin modulde Lookup & Reference Data Decision yazili (mevcut `/api/lookups/{key}` kullanimi, yeni Platform lookup key ihtiyaci veya MDM/reference boundary gerekcesi)
- [ ] Acceptance criteria test edilebilir maddeler
- [ ] Test expectations build/verifier/RESX/smoke kapsiyor
```

---

## 13. Repo Scope Rules

`Repo Scope` bolumu:
- Somut klasor/dosya yollari icermeli
- Dokunulacak yerleri net listelemeli
- Gerekirse API gateway dosyasini module-level kisitla belirtmeli

`Protected Paths` bolumu:
- Dokunulmayacak alanlari acik yazmali
- En az `.antigravity/**` ve domain-disi servis yollarini icermeli
- `gateway/Diten.ApiGateway/**/ocelot.json` integration-agent owned (gerekirse pack'ten cikarilan ayri task)
- Platform/Admin lookup kararlarinda ERP Account, General Reference, Financial Reference, Territory Reference ve tenant-side ERP reference module path'leri protected/out-of-scope olarak acik yazilir.

---

## 13.1 Platform Lookup & Reference Data Decision

Platform/Admin module pack'leri, select/dropdown/filter/default degeri iceren her yerde lookup kararini yazmalidir:

- Mevcut Platform system lookup ise endpoint listelenir: `/api/lookups/{key}`.
- Yeni Platform-owned lookup key gerekiyorsa Repo Scope, Acceptance Criteria ve Test Expectations icinde explicit yazilir.
- Ihtiyac MDM/reference kapsamina giriyorsa PSS lookup'a eklenmez; MDM module pack'e yonlendirilir.
- Frontend consumer varsa same-origin proxy/Gateway path'i yazilir; browser JS servis portu `5057` kullanmaz.
- Hardcoded fallback lookup listesi kabul edilmez.

Referans kural: [platform-lookups-reference-data.md](platform-lookups-reference-data.md).

---

## 14. Entity Schema Rules

`Entity Fields` bolumu her alan icin tip, zorunluluk ve kural belirtmelidir.

> [!IMPORTANT]
> **Naming Rule:** Is mantigina ait versiyon alanlari (semantic version vb.) kesinlikle `Version` olarak adlandirilamaz. `Version` ismi teknik altyapi (concurrency) icin rezerve edilmistir. Bkz: [entity-base-template.md](entity-base-template.md).

Frontmatter `entity_base` alani dolduruldugunda:
- `GlobalEntity` ise: kaydin tenant-owned olmadigi gerekceli yazilir, DTO/request payload'da `TenantId` bulunmaz, global unique index gerekce gosterir
- `EntityBase` / `BaseEntity` ise: tenant-owned kayit, `TenantId` server-side cozulur, DTO'da yer almaz

---

## 15. Test Expectations Kurallari

Minimum beklenti:
- Tenant isolation kontrolu (tenant-owned modullerde) veya Platform-scope erisim kontrolu (`GlobalEntity` modullerinde)
- Soft delete davranisi
- Platform/Admin lookup kullanan modulde lookup endpoint smoke, `LookupOptionDto` shape validation, unauthorized kontrolu ve hardcoded fallback yoklugu
- Browser smoke test sonucu
- Build PASS (en az ilgili servis + frontend + gateway)
- DataTable modulunde verifier PASS
- RESX parite PASS

Module ozelligine gore unit/integration test kapsami acik yazilmalidir.

---

## 16. Lifecycle Rules

Status akisi:

```text
draft -> approved/ready-for-dev -> in-progress -> review -> done
```

Alternatif durum:
- `blocked` (engellenen is)

Kurallar:
- `draft` yalnizca kullanici incelemesi icindir; orchestrator bu status ile kod yazamaz.
- Kod uretimi icin status `approved` veya `ready-for-dev` olmalidir.
- `done` olduktan sonra module pack silinmez; kalici audit belgesi olarak korunur.
- Yeni degisiklikte ayni dosyada `Implementation Notes` guncellenir.

---

## 17. Authority Rule

Yetki hiyerarsisi:

```text
Module Pack > Domain Config > AGENTS.md > .antigravity/
```

Ayni konuda celiski varsa module pack kazanir.

---

## 18. Quick Template

```md
---
id: PSS-XXX
name: ...
domain: platform-shared-services
service: Diten.Platform
shell: platform-admin
golden_reference: slim
entity_base: GlobalEntity
status: draft
owner: ali.tufanoglu
branch: feature/pss/pss-xxx-...
started: YYYY-MM-DD
target: YYYY-MM-DD
form_field_count: 7
---

# PSS-XXX — {Module Name}

## Module Summary
...

## Ownership and Boundaries
- In-scope: ...
- Out-of-scope: ...

## Owned Objects
- Entity: ...
- Commands: ...
- Queries: ...
- DTOs: ...
- API endpoints: ...
- Permissions: ...

## Entity Fields
| Field | Type | Rules |
|---|---|---|
| ... | ... | ... |

## Repo Scope
- ...

## Protected Paths
- `.antigravity/**`
- ...

## Dependencies
- ...

## Runtime Constraints
- ...

## Layout & Shell Contract
- `shell: platform-admin` → tum .cshtml dosyalarinda `Layout = "_LayoutPlatformAdmin"` ACIKCA
- View klasoru: `Views/Platform/{Module}/`
- Frontend route: `/Platform/{Module}`

## Backend File Convention
Golden Reference Slim birebir:
- Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/, {Module}Models.cs
- Naming: Command/Query record, Handler class (suffix YOK)

## Frontend File Contract
Slim partial seti:
- Index.cshtml, _Filter.cshtml, _DataTable.cshtml, _IndexL10n.cshtml
- _CreateEditOffcanvas.cshtml, _DetailsQuickView.cshtml
- {Module}Index.cs + index.js + index.l10n.js

## Validation Rules
| Field | Required | Rule |
|---|---|---|
| ... | ... | ... |

## Failure Path to Verify
- Duplicate ...
- Missing ...
- Concurrency ...
- Unauthorized ...

## Authorization Convention
- Policy: `[Authorize(Policy = "PlatformActor")]`
- Permission format: `Platform.{Resource}.{Action}`
- Permissions: ...

## Gateway / API Routing Decision
- Karar: gerekli/gereksiz
- ...

## Acceptance Criteria
- [ ] ...

## Test Expectations
- ...

## Ready-for-dev Checklist
- [ ] Golden Reference referans alindi
- [ ] ...

## Implementation Notes
...

## Follow-up Items
...
```

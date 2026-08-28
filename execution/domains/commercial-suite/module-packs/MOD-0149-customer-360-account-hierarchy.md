---
id: MOD-0149
name: Customer 360 / Account Hierarchy
domain: commercial-suite
service: Diten.CrmService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
owner: module-pack-author
branch: feature/crm/mod-0149-customer-360-account-hierarchy
started: 2026-07-14
target: 2026-08-29
form_field_count: 17
runtime_code_allowed: true
runtime_code_scope: account-foundation-only (Account/WorkPlace master + hierarchy + 360 + attribute/external-ref surface; NO Contact/Consent/Territory/Visit/Lead/Opportunity/Campaign/Segment)
ready_for_dev_by: user (ready-for-dev status gate 2026-07-14)
dependencies:
  - MOD-0018
  - MOD-0048
  - MOD-0285
  - MOD-0021
  - commercial-suite-domain-foundation
---

# MOD-0149 — Customer 360 / Account Hierarchy

> **READY-FOR-DEV** (status gate 2026-07-14, kullanıcı açık onayı). Golden Reference **compact** (DEV-0001) şablon olarak
> alındı — sapma teknik borçtur. Otorite: Module Pack > Domain Config ([../domain-config.md](../domain-config.md)) >
> AGENTS.md > .antigravity/rules. `runtime_code_scope: account-foundation-only` — implementation yalnız Account foundation
> yüzeyini kapsar; diğer CRM modülleri (Contact/Consent/Territory/Visit/Lead/Opportunity/Campaign/Segment) kapsam dışıdır.

> ### Ready-for-dev Prerequisite Closeout (2026-07-14)
> Dört ready-for-dev ön koşulu da kapandı:
> 1. **`crm` branch code** — AGENTS.md §9'a eklendi; `feature/crm/mod-0149-customer-360-account-hierarchy` geçerli.
> 2. **Permission validator nested-key** — `IsCanonicalPermissionKey` ≥3 segment kabul ediyor (doğrulandı); `crm.account.hierarchy.manage` / `crm.account.attribute.manage` / `crm.account.overview.read` geçerli; `crm.account.360.read` kullanılmaz.
> 3. **MOD-0048 reference readiness** — `account-type`/`account-status` required set kararları + 5 önerilen set + PSS-012/Business Reference Data SoR kabul; authoring template + operatör checklist hazır; **actual publish operatör aksiyonuna devredildi** (implementation validation'ı için Step 1–4 publish şart). Bkz. [readiness](../../../docs/audits/mod-0149-crm-reference-data-readiness.md) + [authoring-closeout](../../../docs/audits/mod-0149-crm-reference-data-authoring-closeout.md).
> 4. **Diten.CrmService scaffold** — MOD-0149-PREREQ ready-for-dev; 5-katman scaffold (port 5061) build 0/0, smoke 5/5, `/health` 200, Account business surface yok. Bkz. [scaffold audit](../../../docs/audits/diten-crm-service-scaffold-implementation-2026-07-14.md).
>
> **Implementation başlamadan operatörün MOD-0048'de `account-type` + `account-status` set'lerini publish etmesi gerekir** (create/update validation aksi halde çalışmaz).

## 1. Module Summary

Customer 360 / Account Hierarchy, Commercial Suite'in **generic CRM foundation**'ının ilk modülüdür. Account/Customer,
WorkPlace (kurum hesabı), account hierarchy (parent-child), account profile/address/geo reference, account status ve
account 360 read-model yüzeyini sahiplenir. Bu modül olmadan Contact (MOD-0150), Territory (MOD-0151), Lead (MOD-0152),
Opportunity (MOD-0153), Field Sales/Visit (MOD-0155), Segment (MOD-0167) ve Campaign (MOD-0165) sağlıklı çalışamaz.

**Stratejik konum:** Yeni CRM yalnız eski pharma ziyaret sistemi değildir — **core generic CRM + pharma field-force
extension**. MOD-0149 generic foundation'dır; pharma-specific Field Sales / MicroTarget / Visit Planning MOD-0155'e aittir
ve **MOD-0149 içine gömülmez**. Hedef kullanıcı: tenant CRM kullanıcıları (admin/manager/sales/marketing).

## 2. Business Context

- Account, tüm downstream CRM ilişkilerinin bağlandığı **müşteri-truth kökü**dür; duplicate account = bozuk CRM.
- WorkPlace zenginliği (hastane/eczane/klinik profil alanları) legacy DitenCRM'den **business-rule olarak** alınır;
  kod taşınmaz (bkz. §25).
- Account hierarchy, kurumsal müşteri ağaçları (holding → şirket → şube / hastane → poliklinik) için gereklidir.
- Custom/dynamic property (legacy Property/PropertyList), controlled attribute yaklaşımıyla **tasarlanır**; eski dinamik
  alan motoru kopyalanmaz. Generic custom-field engine başka bir MOD'a aitse MOD-0149 yalnız **tüketici** olur (open q.).

## 3. Ownership / SoR Boundary

**Sahiplenir (owns):** Account/Customer · WorkPlace/kurum hesabı · Account hierarchy (parent-child) · Account profile ·
Account address reference (değer, coğrafya SoR değil) · Account geo/location (lat/lon değeri) · Account type/category/status
seçimi (değer; lookup SoR MOD-0048) · Account'un CRM tarafındaki dynamic/custom attribute **yüzeyi** · Account 360
read-model / detail surface · Account/WorkPlace identity/duplicate rule · Account external/legacy reference.

**Sahiplenmez (consume-only):** Contact/kişi/doktor/eczacı affiliation → **MOD-0150** · Consent/preference → **MOD-0164** ·
Territory/Zone/MicroZone assignment → **MOD-0151** · Visit/MicroTarget/Activity/Route → **MOD-0155** · Lead → **MOD-0152** ·
Opportunity → **MOD-0153** · Campaign → **MOD-0165** · Segment/TargetCustomer → **MOD-0167** · Country/City/District ve
generic lookup → **MOD-0048** · Employee/Sales Rep/MR master → **HR/Org (MOD-0288)** · Business Unit master → **Platform/Org** ·
Brand/Product/SKU → **MDM/Product** · Auth/Role/Permission engine → **MOD-0018** · Navigation engine → **MOD-0285**.

Detay: [../crm-sor-boundary.md](../crm-sor-boundary.md).

### 3.1 Zone / MicroZone / Territory Ownership (kesin karar — implementation'da yanlış anlaşılmasın)

> **"MOD-0149 provides location foundation; MOD-0151 owns coverage assignment."**

- **MOD-0149 Account master içinde `ZoneId` veya `MicroZoneId` owned/persist field OLARAK TUTULMAZ.** Bu bir mimari kuraldır.
- MOD-0149 yalnız account/workplace master datasını, **address** ve **geo (lat/lon)** alanlarını tutar → *location foundation*.
- Country/City/District **reference data** MOD-0048'den tüketilir (canonical CRM'de tutulmaz).
- **Territory/Zone/MicroZone tanımı ve account assignment MOD-0151'e aittir** (*coverage assignment*).
- MOD-0151 hazır olduğunda Account 360 ekranı Territory/Zone/MicroZone bilgisini **MOD-0151'den read-only projection**
  olarak gösterebilir (persistence field değil; `CoverageSummary` gibi salt-okunur DTO).
- MOD-0155 Field Sales / Visit Planning, route planning için territory/micro-zone bilgisini **MOD-0151 üzerinden tüketir**.
- MOD-0149 içinde **fake zone data veya placeholder assignment üretilmez.**
- MOD-0151 yokken Account 360'da Coverage/Territory alanı "Not assigned" / "Not available yet" olabilir veya gizlenir;
  ama owned `ZoneId`/`MicroZoneId` **eklenmez**.
- Account model'ine `ZoneId`/`MicroZoneId` persist etme girişimi = **architecture / pack violation** (§19).

## 4. Owned Objects

- **Entity / aggregate (adaylar):** `Account` (kök) · `AccountAddress` (veya inline address reference) ·
  `AccountHierarchyLink` (veya `Account.ParentAccountId` alanı) · `AccountAttributeValue` (account-level controlled
  attribute surface — **generic custom-field engine DEĞİL**, §10.2) · **`AccountExternalReference`** (ayrı model:
  `SourceSystem + ExternalId`, §10.1) · `AccountStatusHistory` (opsiyonel lifecycle note).
  > Karar: MVP'de hierarchy `Account.ParentAccountId` self-reference + ayrı `AccountHierarchyLink` yerine tek alan ile
  > başlar; `AccountAttributeValue` ve `AccountExternalReference` **ayrı collection**. `Account360ReadModel` **entity değil**,
  > query DTO'dur (`GetAccountOverviewQuery`). `Account` master'ında **`ZoneId`/`MicroZoneId` YOK** (§3.1); Coverage
  > read-only projection MOD-0151'den gelir.
- **Commands:** `CreateAccountCommand` · `UpdateAccountCommand` · `DeleteAccountCommand` · `BulkDeleteAccountCommand` ·
  `LinkParentAccountCommand` · `UnlinkParentAccountCommand` · `UpsertAccountAttributeCommand` · `ImportAccountsCommand`.
- **Queries:** `GetAccountListQuery` · `GetAccountByIdQuery` · `GetAccountOverviewQuery` (360) ·
  `GetAccountHierarchyQuery` · `ExportAccountsQuery`.
- **DTOs:** `AccountModels.cs` (list/detail/overview/hierarchy/attribute record'ları — TEK dosya).
- **API endpoints (Gateway plan, §17):** `/api/crm/accounts` (+ `{id}`, `{id}/overview`, `{id}/hierarchy`,
  `import`, `export`).
- **Permissions (§16):** `crm.account.{read,create,update,delete,import,export}` · `crm.account.hierarchy.manage` ·
  `crm.account.attribute.manage` · `crm.account.overview.read`.

## 5. Out-of-Scope

Bkz. §3 "Sahiplenmez". Ek olarak: generic custom-field engine implementasyonu, HCP (doktor/eczacı) kimlik SoR'u,
territory/micro-zone scoped görünürlük enforcement'ı (MOD-0151/MOD-0155 + MOD-0018-FU15), account-based Visit/Route
üretimi, Consent yakalama. Bunlar diğer MOD'lara aittir; MOD-0149 yalnız gerekli reference'ları tüketir.

## 6. Dependencies

| Bağımlılık | Tür | Not |
|---|---|---|
| commercial-suite domain foundation | Governance | README/domain-config/build-lane/RBAC/SoR/legacy-preservation hazır (closeout PASS) |
| **MOD-0018 / Diten.AuthService** | Runtime (consume) | `[HasPermission("crm.account.*")]`; yeni RBAC kurulmaz |
| **MOD-0048 Reference Data** | Runtime (consume) | Minimum lookup setleri (§6.1). CRM içinde canonical tutulmaz. **Readiness dependency.** |
| **MOD-0021 Audit Trail** | Runtime (consume) | Audit event publish/append contract tüketicisi (§23) |
| **MOD-0285 Navigation** | Follow-up | Accounts menü `<li>` (`crm.account.read` guard) — how-to-add-a-module Adım 9; bu pack menü yazmaz |
| **MOD-0018-FU15 Real DataScopeResolver** | Future | Territory/account scoped visibility için gerekli; **MVP'yi bloklamaz** (tenant scope yeterli) |
| Response<T> envelope + CustomBaseController | Convention | Zorunlu |
| DataTable v2 verifier + RESX checker | Convention | Compact standardı |

### 6.1 MOD-0048 Reference Data Readiness (explicit)

MOD-0149'un MOD-0048'den **tükettiği** minimum lookup setleri (canonical key adayları):

`account-type` · `account-category` · `workplace-type` · `workplace-category` · `country` · `city` · `district` ·
`address-type` · `account-status` · `status-reason`

- Bu lookup'lar **CRM içinde canonical olarak oluşturulmaz**; MOD-0149 yalnız **tüketir**.
- MOD-0048 hazır değilse create/update **validation eksik kalır** (type/category/status/country doğrulanamaz).
- Initial implementation'da MOD-0048 readiness yoksa **geçici hardcoded fallback yapılmaz**.
- Eksik lookup **blocker veya explicit dependency** olarak raporlanır (§28).

## 7. Repo Scope (implementation'da, pack onayı sonrası)

- `execution/domains/commercial-suite/module-packs/MOD-0149-customer-360-account-hierarchy.md` (bu dosya)
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Account/**` *(service scaffold EA-TBD, §Blockers)*
- `services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/AccountController.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Domain/**`, `.../Persistence/**`, `.../Infrastructure/**` (5-katman)
- `frontend/Diten.Web/Controllers/CRM/AccountsController.cs`
- `frontend/Diten.Web/Views/CRM/Accounts/**`
- `frontend/Diten.Web/wwwroot/assets/js/CRM/Accounts/**`
- `frontend/Diten.Web/Resources/Views/CRM/Accounts/**` (7 dil `.resx`)
- `gateway/Diten.ApiGateway/**` yalnız route referansı; doğrudan `ocelot.json` yazımı **integration-agent**'a ait.

### 7.1 Service Path Kararı (FINAL — pack review için blocker değil)

- MOD-0149 ve Commercial Suite CRM core runtime için **hedef servis `services/Diten.CrmService/**`**'dır; bu, CRM core
  modüllerinin **başlangıç runtime boundary**'sidir. Frontmatter `service: Diten.CrmService` korunur.
- **Bu task/pack service scaffold OLUŞTURMAZ.** `Diten.CrmService` scaffold'ı **ayrı bir approved/ready-for-dev
  implementation task'ında** (repo standardı gerektiriyorsa bir capability/module pack üzerinden) yapılır → **follow-up**.
- CPQ / O2C / Service gibi adjacent modüllerin **aynı serviste mi ayrı serviste mi** olacağı ileride **EA kararı**dır;
  **MOD-0149'u bloklamaz**.
- Sınıflandırma: **pack approval için blocker değil**; **ready-for-dev + implementation başlangıcı için service scaffold task'ı gerekir** (§28).

## 8. Protected Paths

- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN)
- `frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` (yalnız implementation follow-up Adım-9 `<li>` — bu pack değiştirmez)
- `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` (yalnız integration-agent)
- Diğer domain servisleri: `services/Diten.AuthService/**`, `services/Diten.Platform/**`, `services/Diten.MdmService/**`, `services/Diten.HcmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**`
- `AGENTS.md` (bu pack kapsamında değişmez)

## 9. Runtime Constraints

- **Shell:** `tenant` → `_LayoutTenantShell` (§13).
- **Persistence:** MongoDB, `TenantId` **zorunlu**, server-side çözülür; DTO/form payload'da `TenantId` **yok**.
- **Soft delete:** `IsDeleted`/`DeletedAt` zorunlu; silinen account list/get-by-id'de dönmez.
- **Cross-tenant:** başka tenant'ın account ID'sine erişim **404** (bilgi sızıntısı yok).
- **Entity base:** `EntityBase` (tenant-owned CRM kaydı; `Diten.CrmService` tenant service). GlobalEntity **değildir**.
- **Frontend Gateway:** browser JS servis portuna gitmez; Gateway (5000) üzerinden `/api/crm/...`.
- **Mimari:** 5 katman (Api/Application/Domain/Persistence/Infrastructure) + CQRS (MediatR) + 4 pipeline behavior.
- **Compact:** ayrı Create/Edit/Details sayfaları; offcanvas create/edit **yasak**.

## 10. Entity / Field Model

`Account` (EntityBase; kullanıcı-editable create/edit alanları — 17):

| # | Field | Type | Required | Rule / Source |
|---|---|---|---|---|
| 1 | AccountName | string | Evet | Trim, max 200 |
| 2 | AccountCode | string | **Hayır (girişte); persistence sonrası zorunlu** | Kullanıcı girerse trim/normalize + max 64 + allowed-char + tenant-scoped unique; **boşsa sistem auto-generate** (§10.1a). Kayıt sonrası her zaman dolu. |
| 3 | AccountType | string (lookup) | Evet | MOD-0048 `account type` |
| 4 | AccountCategory | string? (lookup) | Hayır | MOD-0048 `account category` |
| 5 | ParentAccountId | Guid? (ref) | Hayır | Tenant-scoped Account; self-ref; döngü yasak (§18) |
| 6 | Status | string (lookup/enum) | Evet | MOD-0048 `status reason` veya sabit lifecycle |
| 7 | CountryRef | string? (lookup) | Hayır | MOD-0048 `country` |
| 8 | CityRef | string? (lookup) | Hayır | MOD-0048 `city` |
| 9 | DistrictRef | string? (lookup) | Hayır | MOD-0048 `district` |
| 10 | AddressLine | string? | Hayır | Max 500 |
| 11 | Latitude | double? | Hayır | -90..90 |
| 12 | Longitude | double? | Hayır | -180..180 |
| 13 | ResponsiblePersonName | string? | Hayır | Max 200 |
| 14 | ResponsiblePersonPhone | string? | Hayır | Phone format, max 32 |
| 15 | ResponsiblePersonEmail | string? | Hayır | Email format, max 256, lowercase normalize |
| 16 | ExternalReference (MVP quick-entry) | string? | Hayır | **MVP tek-alan hızlı giriş**; default `SourceSystem` ile `AccountExternalReference`'a yazılır (§10.1b). AccountCode yerine kullanılmaz; runtime OldSystem dependency YOK. |
| 17 | Notes | string? | Hayır | Max 2000 |

> **YOK (persist edilmez):** `ZoneId` / `MicroZoneId` — Account master'ında owned field değildir (§3.1). Coverage/Territory
> bilgisi ileride MOD-0151'den **read-only projection** olarak gelir (`CoverageSummary` DTO), persistence field değil.

**Sayılmayan (audit/altyapı):** `Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`.
**Ayrı yüzey (form_field_count'a dahil DEĞİL):** dynamic/custom attribute'lar `AccountAttributeValue` collection'ında
(`crm.account.attribute.manage`, §10.2); legacy/import dış kodlar `AccountExternalReference` collection'ında (§10.1b).
Base form 17 alan olarak kalır.

**MongoDB index ihtiyacı:** `{TenantId, AccountCode}` unique (soft-delete aware); `{TenantId, AccountName}` arama;
`{TenantId, ParentAccountId}` hierarchy; `AccountExternalReference`: `{TenantId, SourceSystem, ExternalId}` unique (partial, boş değilse).

> **Naming kuralı:** İş alanı olarak `Version` adı kullanılmaz (concurrency için rezerve).

### 10.1 AccountCode & AccountExternalReference kararları

**10.1a — AccountCode (optional manual + auto-generation fallback):**
- Create formunda **optional user input**. Kullanıcı girerse: trim/normalize + max-length + allowed-char + `{TenantId, AccountCode}` unique.
- Kullanıcı boş bırakırsa: sistem **tenant içinde benzersiz AccountCode üretir**. Persistence sonrası AccountCode **her zaman dolu**.
- AccountCode = CRM'in **kendi human-readable/internal identifier**'ı. Legacy/import/dış sistem kodları AccountCode'a **zorla yazılmaz** (→ `AccountExternalReference`).
- **Unique index:** `{TenantId, AccountCode}`. Farklı tenant'larda aynı AccountCode bulunabilir (uniqueness TenantId ile birlikte).
- **Auto-generation formatı (FINAL):** **`ACC-{YYYY}-{sequence}`** — sequence 6 haneli zero-pad. Örnek: `ACC-2026-000001`, `ACC-2026-000002`.
- **Sequence scope (FINAL):** **tenant + year scoped**. Her tenant kendi sequence'ına sahiptir; **yıl değişince** aynı tenant için sequence `000001`'den başlayabilir.
- **Collision:** auto-generation'da çakışma olursa **controlled retry**; retry sayısı tükenirse **controlled domain/application error** dönülür (sessiz fallback YOK) + log/audit beklenir (§19, §23).
- Kayıt sonrası AccountCode **boş kalamaz**. Create form helper text önerisi: **"Boş bırakırsanız sistem otomatik üretir."**

**10.1b — AccountExternalReference (SourceSystem + ExternalId modeli):**
- **Ayrı model/collection** (Account master string alanı değil). Aynı `ExternalId` **farklı SourceSystem**'lerden gelebilir → tek string unique yetmez.
- **Alanlar:** `AccountId` · `SourceSystem` · `ExternalId` · `SourceEntity` (örn. `WorkPlace`/`Client`/`Account`) · opsiyonel `DisplayName`/`Notes` · `ImportedAt`/`CreatedAt` audit.
- **Unique rule:** `{TenantId, SourceSystem, ExternalId}` unique.
- Legacy `OldSystemId` varsa `SourceSystem = OldCRM`/`OldSystem` gibi **normalize** edilmiş değerle tutulur; runtime OldSystem dependency **kurulmaz**.
- Import sırasında dış sistem kodları buraya yazılır; AccountCode CRM'in kendi identifier'ı olarak korunur.
- **MVP form kararı:** base create formunda **tek ExternalReference quick-entry alanı** kalabilir → default `SourceSystem` ile bir `AccountExternalReference` satırına yazılır. Tam çoklu-kaynak yönetimi Details ekranında/import ile yapılır.

### 10.2 Dynamic / Custom Attribute sınırı (net)

- MOD-0149 **generic custom-field engine'e dönüşmez.** `AccountAttributeValue` yalnız **account-level controlled attribute value surface**'idir.
- Legacy DitenCRM `Property`/`PropertyList` yalnız **iş kuralı hafızası** olarak referans alınır; eski motor **kopyalanmaz**.
- Platform-wide/commercial-wide custom-field engine ileride ayrı bir MOD olarak tanımlanırsa MOD-0149 onu **tüketir**.
- Dynamic attribute yönetimi `crm.account.attribute.manage` ile korunur; değerler **form_field_count'a dahil değildir**.
- **Open question:** Attribute **definition**'larının SoR'u EA tarafından netleştirilmeli. *"Generic custom-field engine SoR'u EA
  tarafından netleştirilmeli. MOD-0149 yalnız account-level attribute value surface sağlayabilir; platform-wide engine sahiplenmez."*

## 11. Form Field Count + Golden Reference Decision

- **form_field_count: 17** (create/edit formundaki kullanıcı alanları). 8'den fazla → **`golden_reference: compact`**.
- MVP'de küçültme yapılmadı; geo (lat/lon) ve responsible-person alanları workplace profil zenginliği için MVP'de tutuldu.
  İleride bir phase-2 sadeleştirmesi istenirse pack güncellenir, ama 8 altına inmesi beklenmez → compact kararı sabittir.
- Dynamic attribute yüzeyi (§10.2) ve çoklu `AccountExternalReference` yönetimi (§10.1b) ayrı surface olduğundan sayıma dahil değildir.
- AccountCode form alanı sayılır (optional giriş olsa da bir form alanıdır); ExternalReference MVP quick-entry tek alanı sayılır. Toplam **17** değişmez.

## 12. Layout & Shell Contract

- `shell: tenant`.
- Razor layout: **her** `.cshtml` sayfası `Layout = "_LayoutTenantShell";` **açıkça** yazar (`_ViewStart` varsayılanı değiştirilmez).
- View klasörü: `frontend/Diten.Web/Views/CRM/Accounts/`.
- Frontend route: `/CRM/Accounts` (veya `/Accounts` — controller area `CRM`).
- `_LayoutTenantShell.cshtml` bu pack'te **değişmez**; menü `<li>` implementation follow-up (Adım 9, `crm.account.read` guard).

> **Branch kararı (öneri statüsü):** `branch: feature/crm/mod-0149-customer-360-account-hierarchy`. `crm` branch short
> code AGENTS.md §9'a eklenene kadar bu ad **öneri statüsündedir**. Implementation'a alınmadan önce **ya AGENTS.md §9'a
> `crm` eklenmesi** (tercih edilen karar) **ya da** mevcut kısa kodlardan biriyle geçici branch convention seçilmelidir.
> Bu pack/task AGENTS.md'yi **değiştirmez** (protected).

## 13. Backend File Convention (Golden Reference Compact birebir)

```text
services/Diten.CrmService/src/Diten.CrmService.Application/Features/Account/
├── Commands/
│   ├── CreateAccountCommand.cs            (sealed record, IRequest<Response<Guid>>)
│   ├── UpdateAccountCommand.cs            (sealed record, IRequest<Response<NoContent>>)
│   ├── DeleteAccountCommand.cs
│   ├── BulkDeleteAccountCommand.cs
│   ├── LinkParentAccountCommand.cs
│   ├── UnlinkParentAccountCommand.cs
│   ├── UpsertAccountAttributeCommand.cs
│   └── ImportAccountsCommand.cs
├── Queries/
│   ├── GetAccountListQuery.cs
│   ├── GetAccountByIdQuery.cs
│   ├── GetAccountOverviewQuery.cs
│   ├── GetAccountHierarchyQuery.cs
│   └── ExportAccountsQuery.cs
├── Handlers/
│   ├── CommandHandlers/                   ← AYRI klasör (zorunlu)
│   │   ├── CreateAccountHandler.cs        (sealed class, suffix YOK)
│   │   ├── UpdateAccountHandler.cs
│   │   ├── DeleteAccountHandler.cs
│   │   ├── BulkDeleteAccountHandler.cs
│   │   ├── LinkParentAccountHandler.cs
│   │   ├── UnlinkParentAccountHandler.cs
│   │   ├── UpsertAccountAttributeHandler.cs
│   │   └── ImportAccountsHandler.cs
│   └── QueryHandlers/                     ← AYRI klasör (zorunlu)
│       ├── GetAccountListHandler.cs
│       ├── GetAccountByIdHandler.cs
│       ├── GetAccountOverviewHandler.cs
│       ├── GetAccountHierarchyHandler.cs
│       └── ExportAccountsHandler.cs
├── Validators/
│   ├── CreateAccountValidator.cs          (suffix YOK)
│   └── UpdateAccountValidator.cs
└── AccountModels.cs                       ← TEK dosyada tüm DTO/ViewModel'ler
```

- Command = record `{Verb}AccountCommand`; Query = record `Get Account…Query`; Handler = class `{Verb}AccountHandler`
  (**Command/Query/Request suffix YOK**); Validator = class `{Verb}AccountValidator`.
- Controller ince: yalnız MediatR'a gönderir, `Response<T>` + `CustomBaseController`.

## 14. Frontend File Contract (Compact)

```text
frontend/Diten.Web/Views/CRM/Accounts/
├── Index.cshtml            (Layout = "_LayoutTenantShell" AÇIKÇA)
├── Create.cshtml           (sayfa kabuk + _Form)
├── Edit.cshtml             (sayfa kabuk + _Form)
├── Details.cshtml          (Account 360 / overview + hierarchy + attribute sekmeleri)
├── _Form.cshtml            (Create/Edit ortak)
├── _Filter.cshtml          (inline collapsible filter)
├── _DataTable.cshtml       (data-dt-standard="v2" + skeleton loader)
├── _IndexL10n.cshtml       (JSON L10n bridge)
└── AccountIndex.cs         (marker class)

frontend/Diten.Web/wwwroot/assets/js/CRM/Accounts/
├── index.js
└── index.l10n.js

frontend/Diten.Web/Resources/Views/CRM/Accounts/
└── AccountIndex.{lang}.resx   (7 dil: en, fr, es, zh, ar, ru, tr)
```

- **Compact'ta YASAK:** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index içinde create/edit offcanvas.
- DataTable: `data-dt-standard="v2"`, skeleton loader, inline filter, `stateSave:false`, SavedViews/personalization standardı.
- Uzun `window.L10n` blokları Index'e gömülmez; `_IndexL10n.cshtml` + `index.l10n.js` bridge kullanılır.

## 15. Runtime Constraints (özet — bkz. §9)

Tenant isolation zorunlu · soft delete · cross-tenant 404 · Gateway üzerinden çağrı · CQRS + envelope · compact full-page.

## 16. Permission / Authorization Convention

- **Policy:** `[Authorize]` (tenant actor). **Permission:** `[HasPermission("crm.account.{action}")]` — PKS-001
  lowercase-dotted, ≥3 segment, kebab multiword. Enforcement backend'de; frontend `Perms.Has` yalnız UX.
- Yeni RBAC kurulmaz; mevcut MOD-0018 modeli tüketilir. `crm.*` namespace PKS-001 §4'te önceden ayrılmış.

| Permission | Action | Risk | Data Scope | Audit |
|---|---|---|---|---|
| `crm.account.read` | list/detail | Düşük | Tenant (MVP); territory later (FU15) | Hayır |
| `crm.account.create` | create | Orta | Tenant | Evet |
| `crm.account.update` | update | Orta | Tenant | Evet |
| `crm.account.delete` | soft delete | Yüksek | Tenant | Evet |
| `crm.account.import` | bulk import | Yüksek | Tenant | Evet |
| `crm.account.export` | export | Orta (PII) | Tenant | Evet |
| `crm.account.hierarchy.manage` | link/unlink parent | Orta | Tenant | Evet |
| `crm.account.attribute.manage` | dynamic attribute upsert | Orta | Tenant | Evet |
| `crm.account.overview.read` | 360 read model | Düşük | Tenant | Hayır |

> **PKS-001 permission key kararları (net):**
> - `crm.account.360.read` **KULLANILMAYACAK** — segment grammar `^[a-z][a-z0-9-]*$` segmentin harfle başlamasını ister; "360" rakamla başlar (geçersiz).
> - `crm.account.overview.read` **KULLANILACAK** (360 read model için canonical karşılık).
> - `crm.account.hierarchy.manage` ve `crm.account.attribute.manage` **4-segmentli nested** permission key'lerdir. PKS-001 ≥3 segmenti kabul eder.
> - **FINAL KARAR: nested permission key standardı korunur.** Bu iki key kullanılmaya devam eder; **geçici 3-segment alternatiflere dönülmez.**
> - **DOĞRULANDI (2026-07-14):** `Diten.Platform.../ModulePages/ModulePageDescriptorNormalizer.IsCanonicalPermissionKey` bugün **`parts.Length >= 3`** kabul ediyor (segment grammar `[a-z][a-z0-9-]*`). Yani `crm.account.hierarchy.manage` / `crm.account.attribute.manage` (4-segment) ve `crm.account.overview.read` **zaten geçerli**; `crm.account.360.read` (rakamla başlayan segment) doğru şekilde reddedilir. **AG-STEP-004B ≥3 genişletmesi bu validator'da uygulanmış durumda** → nested-key **artık ready-for-dev blocker değil**; yalnız permission seed sırasında bu key'lerin catalog→auth sync geçtiği **doğrulanır** (implementation-task).
> - Ad-hoc rename yapılmaz. `crm.account.360.read` kullanılmaz; `crm.account.overview.read` korunur.

**Role/profile mapping (mevcut MOD-0018 modeline; yeni global role yok):**

| Role | Permissions | Data Scope |
|---|---|---|
| CRM Admin | tüm `crm.account.*` | Tenant |
| CRM Manager | read/create/update/export + hierarchy.manage + attribute.manage | Tenant |
| Sales Manager | read/export | Tenant (ekip, FU15) |
| Sales Representative / MR | read | Tenant → territory scoped later (FU15) |
| Marketing Manager | read | Tenant |
| Read-only CRM Viewer | read | Tenant |

## 17. Gateway / API Routing Decision

- **Karar:** Gateway değişikliği **gerekli** (yeni CRM downstream). **Bu pack ocelot.json yazmaz** — integration-agent task'ı.
- Route planı (integration-agent, explicit Upstream/Downstream + OPTIONS):
  `/api/crm/accounts` · `/api/crm/accounts/{id}` · `/api/crm/accounts/{id}/overview` · `/api/crm/accounts/{id}/hierarchy` ·
  `/api/crm/accounts/import` · `/api/crm/accounts/export`.
- **Route convention notu:** Golden Reference `/api/{kebab-resource}` kullanır, `v1` segmenti yoktur. Task'ta önerilen
  `/api/v1/crm/accounts` repo'da gözlenen convention'a uymuyor → **`/api/crm/accounts`** olarak normalize edildi.
  Nihai kararı integration-agent `routes.md` ile doğrular.
- Frontend Gateway (5000) üzerinden çağırır; `Diten.CrmService` portuna doğrudan gitmez.

## 18. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| AccountName | Evet | Trim, max 200 | — | Validator |
| AccountCode | **Hayır (girişte)** | User enters value → trim/normalize + max 64 + allowed-char + unique check; User leaves empty → **auto-generate** + unique check; persistence sonrası **zorunlu dolu** | **Unique {TenantId, AccountCode}** (soft-delete aware) | `ExistsByCodeAsync` / generator |
| AccountType | Evet | MOD-0048 `account type` geçerli değer | — | Lookup validation |
| AccountCategory | Hayır | MOD-0048 `account category` | — | Lookup validation |
| ParentAccountId | Hayır | Tenant-scoped Account var; **döngü yok**; self ≠ parent | — | `GetByIdAsync` + cycle check |
| Status | Evet | MOD-0048 `status reason` / sabit lifecycle | — | Lookup validation |
| Country/City/District Ref | Hayır | MOD-0048 ilgili key | — | Lookup validation |
| AddressLine | Hayır | Max 500 | — | Validator |
| Latitude | Hayır | -90..90 | — | Validator |
| Longitude | Hayır | -180..180 | — | Validator |
| ResponsiblePersonName | Hayır | Max 200 | — | Validator |
| ResponsiblePersonPhone | Hayır | Phone format, max 32 | — | Validator |
| ResponsiblePersonEmail | Hayır | Email regex, max 256, lowercase | — | Validator |
| ExternalReference (MVP quick-entry) | Hayır | Tek-alan hızlı giriş; default `SourceSystem` ile `AccountExternalReference` satırına yazılır (§10.1b). AccountCode yerine geçmez. | (asıl unique `AccountExternalReference`'ta) | — |
| AccountExternalReference (ayrı model) | — | `SourceSystem` + `ExternalId` (+ SourceEntity/DisplayName/audit); **unique {TenantId, SourceSystem, ExternalId}**; aynı ExternalId farklı SourceSystem = **allowed** | Partial unique index | `ExistsBySourceExternalAsync` |
| Notes | Hayır | Max 2000 | — | Validator |

**Karar kayıtları:** AccountCode = **optional giriş + auto-generate fallback**, tenant-scoped unique, persistence sonrası
zorunlu (§10.1a); external kimlik = **`AccountExternalReference` (SourceSystem+ExternalId) modeli**, `{TenantId, SourceSystem,
ExternalId}` unique (§10.1b); AccountCategory = **optional**; Address = **optional**; `ZoneId`/`MicroZoneId` = **persist edilmez** (§3.1).
**Duplicate account/workplace rule:** birincil = AccountCode unique + AccountExternalReference (SourceSystem+ExternalId) unique;
ikincil = `normalize(AccountName) + AccountType + normalize(AddressLine)` "olası duplicate" uyarısı (bloklamaz). Tümü **tenant
scoped**. Magic number (ClientTypeId 92/99 gibi) **kullanılmaz**.

## 19. Failure Paths

| Senaryo | Beklenen |
|---|---|
| Missing required field (AccountName/Type/Status) | 400 + validator mesajı + kayıt olmaz |
| Manual AccountCode duplicate (tenant) | **409** + field-level error + kayıt olmaz + reload temiz state |
| AccountCode boş bırakılmış | sistem **auto-generate** eder (tenant-unique); kayıt sonrası dolu |
| Auto-generated AccountCode collision | **controlled retry**; retry başarılı → kayıt |
| Auto-generation retry exhausted | **controlled domain/application error** (500 değil, kontrollü) + log/audit; kayıt olmaz |
| ExternalReference duplicate (aynı SourceSystem + ExternalId) | **409** + field-level error |
| Aynı ExternalId, farklı SourceSystem | **allowed** (unique {SourceSystem, ExternalId}) |
| Possible duplicate (ad+tip+adres) | 200 ama UI "olası duplicate" uyarısı; kullanıcı onayıyla devam |
| Unauthorized user | 403 (backend enforce); UI'da aksiyon disabled / menüde link yok |
| Cross-tenant account access (id başka tenant) | **404** (sızıntı yok) |
| Invalid reference data value (type/status/country…) | 400 + validator; MOD-0048 geçersiz değer |
| Parent account not found | 400/404 + validator; link olmaz |
| Parent account cross-tenant | **404** (parent başka tenant) |
| Circular hierarchy attempt (A→B→A) | 400 + cycle validator; link olmaz |
| Soft-deleted account access | list/get-by-id dönmez (404); güncelleme reddedilir |
| Import duplicate row | satır reddedilir + import raporunda hatalı satır listelenir; geçerli satırlar işlenir |
| Zone/MicroZone not available (MOD-0151 yok) | Account create **bloklanmaz**; Coverage section "not assigned"/gizli |
| Invalid MOD-0048 lookup value (type/category/status/country…) | **400** + validator |
| `ZoneId`/`MicroZoneId` persist girişimi (Account owned model) | **architecture / pack violation** — reddedilir (§3.1) |
| Concurrency conflict (aynı account eş-zamanlı update) | 409 + "data changed, reload" + sessiz overwrite YOK |

## 20. Golden Flow

1. Kullanıcı `/CRM/Accounts` Create sayfasına girer (`crm.account.create`).
2. **AccountCode alanını isterse doldurur, isterse boş bırakır** (helper: "Boş bırakırsanız sistem otomatik üretir").
3. **Boş bırakırsa** sistem tenant içinde **unique AccountCode üretir** (collision → controlled retry).
4. **Manuel girerse** trim/normalize + tenant-scoped **unique validation** yapılır.
5. Diğer zorunlu alanlar (AccountName/Type/Status) ve MOD-0048 lookup değerleri doğrulanır; olası-duplicate uyarısı çalışır.
6. Account tenant-scoped kaydedilir (`TenantId` server-side); **kayıt sonrası AccountCode her zaman dolu**.
7. External legacy ID varsa **`AccountExternalReference` (SourceSystem+ExternalId)** olarak saklanır; **AccountCode yerine kullanılmaz**.
8. **Zone/MicroZone bilgisi create flow'da zorunlu değildir**; MOD-0149 owned zone alanı yoktur.
9. Details / 360 sayfası açılır (`crm.account.overview.read`): profil + hierarchy + attribute sekmeleri; audit MOD-0021'e publish edilir.
10. İleride **MOD-0151 hazır olduğunda** Account 360'da **read-only Coverage/Territory** bölümü (MOD-0151 projection) görünebilir.
11. Başka tenant'a ait account ID ile erişim **404**; yetkisiz kullanıcı **403** / menüde link yok (backend enforce).

## 21. Acceptance Criteria

- [ ] `Diten.CrmService` altında `Features/Account/` Golden Reference Compact folder/naming ile birebir (Commands, Queries, Handlers/CommandHandlers, Handlers/QueryHandlers, Validators, `AccountModels.cs`).
- [ ] Handler/Validator isimlerinde `Command`/`Query`/`Request` suffix YOK.
- [ ] Tüm `Views/CRM/Accounts/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"` AÇIKÇA yazılı.
- [ ] Compact sayfa seti tam: `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml`; offcanvas create/edit YOK.
- [ ] `_DataTable.cshtml` `data-dt-standard="v2"` marker + skeleton içerir; `verify_datatable_page.py --area CRM --module Accounts --reference compact` PASS.
- [ ] `crm.account.*` permission'ları `[HasPermission]` ile enforce; yetkisiz → 403.
- [ ] `TenantId` DTO/payload'da yok, server-side; cross-tenant account ID → 404.
- [ ] AccountCode **boş bırakıldığında** sistem tenant içinde **unique AccountCode üretir**.
- [ ] AccountCode **manuel girildiğinde** tenant-scoped **unique kontrolü** çalışır; duplicate → 409.
- [ ] AccountCode payload'da **boş gelebilir** ama **kayıt sonrası boş kalamaz**.
- [ ] External kimlik `AccountExternalReference` (SourceSystem+ExternalId) modeliyle tutulur; `{TenantId, SourceSystem, ExternalId}` unique; aynı ExternalId farklı SourceSystem allowed.
- [ ] Account master'ında `ZoneId`/`MicroZoneId` **persist edilmez**; Coverage/Territory (varsa) MOD-0151 read-only projection'dır.
- [ ] Invalid MOD-0048 reference value ile create/update **400** döner.
- [ ] CRM içinde country/city/district/account-type **canonical seed edilmez**.
- [ ] ParentAccount döngü denemesi → 400; cross-tenant parent → 404.
- [ ] Soft delete: silinen account list/get-by-id'de dönmez.
- [ ] Account create/update/delete/import/export/hierarchy-link/attribute-update audit event'leri MOD-0021'e publish edilir.
- [ ] 7 dil `.resx` paritesi (en/fr/es/zh/ar/ru/tr); RESX checker PASS; L10n bridge Index'e gömülü değil.
- [ ] `dotnet build` (CRM service + frontend + gateway) PASS.

## 22. Test Expectations

- **Build:** `dotnet build services/Diten.CrmService/...Api.csproj -c Debug` · `frontend/Diten.Web/Diten.Web.csproj` · `gateway/Diten.ApiGateway/Diten.ApiGateway.csproj`.
- **xUnit:** create/update/delete/read · duplicate (AccountCode/ExternalReference) · tenant isolation · cross-tenant 404 · soft-delete davranışı · reference-data invalid value · hierarchy circular prevention · parent cross-tenant.
- **Authorization tests:** permission yok → 403; her `crm.account.*` için guard.
- **Frontend verifier:** `verify_datatable_page.py ... --reference compact` PASS.
- **RESX checker:** 7 dil parite PASS.
- **Browser smoke:** Accounts menü görünürlüğü (permission'lı) · liste yüklenir · Create sayfası açılır · Details/360 açılır · delete/bulk-delete.

## 23. Audit Expectations

Auditlenecek aksiyonlar (owner **MOD-0021**; MOD-0149 yalnız publish/append contract tüketicisi):
`account.create` · `account.update` · `account.delete` · `account.restore` (varsa) · `account.import` · `account.export` ·
`account.hierarchy.link` · `account.hierarchy.unlink` · `account.attribute.update` · `account.duplicate.rejected`
(duplicate reddi/attempt, gerekiyorsa). Her event: actor (current user), TenantId, data scope, PII risk (export/responsible
person alanları için yüksek), retention MOD-0021'e devreder. MOD-0149 kendi audit store'unu kurmaz.

## 24. Localization Expectations

- Tenant modülü → **7 dil**: `en, fr, es, zh, ar, ru, tr`.
- `.resx` + `_IndexL10n.cshtml` + `index.l10n.js` bridge standardı; PascalCase loader korunur (camelCase→PascalCase).
- Index içine uzun `window.L10n` blokları gömülmez.
- RESX parite zorunlu; eksik anahtar CI/checker'da fail.

## 25. Legacy Value Preservation

Yöntem: **rule capture / reference schema** (kod değil kural). Kaynak: [../legacy-value-preservation.md](../legacy-value-preservation.md).

| Legacy Asset | Kaynak | Preservation |
|---|---|---|
| WorkPlace modeli (hastane/eczane/klinik profil, geo, category/definition, responsible person, bed number/person quantity) | DitenCRM WorkPlace | Reference schema — zengin profil alanları Account/attribute yüzeyine eşlenir; kod taşınmaz |
| Client ↔ WorkPlace ilişkisi | DitenCRM | Client tarafı **MOD-0150**; Account/WorkPlace tarafı 360 Details'te gösterilir |
| Property / PropertyList (dynamic field) | DitenCRM | Controlled account-level attribute value surface (`AccountAttributeValue`, §10.2); eski motor kopyalanmaz; generic engine başka MOD'a aitse tüketici ol |
| WorkPlace/Client legacy dış kodlar | DitenCRM/OldSystem | `AccountExternalReference` (SourceSystem+ExternalId, §10.1b); SourceSystem normalize (`OldCRM`/`OldSystem`); runtime dependency yok |
| Duplicate workplace rule | DitenCRM | Tenant-scoped: AccountCode unique + AccountExternalReference (SourceSystem+ExternalId) unique + ad/tip/adres olası-duplicate (§18) |
| Parent-child account | DitenCRM/legacy | `ParentAccountId` + cycle guard |

## 26. Do-Not-Migrate List

- Eski DitenCRM kodu / controller / view / repository yapısı
- OldSystem runtime bağımlılığı, localhost self-call, hardcoded IP/port
- Magic number'lar (ör. `ClientTypeId 92/99`)
- Country/City/Zone/Brand referanslarını CRM içinde canonical tutma (→ MOD-0048/MDM)
- Frontend-only validation yaklaşımı (backend validation zorunlu)
- Authorization'sız endpoint modeli
- `OldSystemId`/legacy id: yalnız `ExternalReference` **veri alanı** olarak migration compat; **runtime dependency kurulmaz**.

## 27. Ready-for-dev Checklist (final review formatı)

Durum etiketleri: **`content-ready`** = pack içinde karar verilmiş/yazılmış · **`pre-implementation-required`** = kod öncesi
kullanıcı/EA/ön koşul ister · **`implementation-task`** = ilgili implementation task'ında yapılır.

### A. Pack Content Readiness — `content-ready`

- [x] Golden Reference Compact (DEV-0001) kararı ve şablon referansı — `content-ready`
- [x] Frontmatter zorunlu alanlar dolu (service, shell, golden_reference, entity_base, form_field_count) — `content-ready`
- [x] Ownership / SoR boundary (§3) + Layout & Shell Contract (§12) — `content-ready`
- [x] Zone/MicroZone persist edilmeme kararı (§3.1) — `content-ready`
- [x] AccountCode optional manual + auto-generation fallback + **FINAL format `ACC-{YYYY}-{sequence}`** (§10.1a) — `content-ready`
- [x] `AccountExternalReference` SourceSystem + ExternalId modeli (§10.1b) — `content-ready`
- [x] MOD-0048 minimum lookup listesi (§6.1) — `content-ready`
- [x] Permission listesi + nested-key FINAL kararı (§16) — `content-ready`
- [x] Validation Rules (§18) + Failure Paths (§19) + Golden Flow (§20) — `content-ready`
- [x] Acceptance Criteria (§21) + Test Expectations (§22) — `content-ready`
- [x] Audit Expectations (§23) + Localization Expectations (§24) — `content-ready`
- [x] Coverage/Territory read-only projection'ın MOD-0151'den geleceği (§3.1) — `content-ready`

### B. Implementation Prerequisites — `pre-implementation-required` / `implementation-task`

- [ ] `Diten.CrmService` scaffold / service path onayı — `pre-implementation-required` (§7.1)
- [ ] `crm` branch short code AGENTS.md §9 follow-up — `pre-implementation-required` (§12)
- [ ] MOD-0048 lookup readiness/seed doğrulaması — `pre-implementation-required` (§6.1)
- [ ] AccountCode sequence/generator implementation stratejisi (tenant+year sequence store) — `implementation-task` (format §10.1a'da net)
- [ ] Permission validator nested-key (≥3 segment) desteği — `pre-implementation-required` (§16)
- [ ] MOD-0018 `crm.account.*` permission seed planı — `implementation-task`
- [ ] MOD-0285 / static tenant shell menü `<li>` (Adım 9) — `implementation-task`
- [ ] Gateway `/api/crm/accounts*` route — `implementation-task` (integration-agent)

## 28. Blockers / Open Questions (yeniden sınıflandırma)

| Item | Blocks Pack Approval? | Blocks Ready-for-dev? | Blocks Implementation? | Notes |
|---|---|---|---|---|
| Diten.CrmService service scaffold / path | Hayır | **Evet** | **Evet** | Hedef `services/Diten.CrmService/**` (FINAL); scaffold ayrı task (§7.1) |
| `crm` branch short code (AGENTS.md §9) | Hayır | **Evet** | **Evet** | Branch açmadan önce §9 update (tercih) veya geçici convention (§12) |
| MOD-0048 lookup readiness | Hayır | **Evet** | **Evet** | §6.1; hazır değilse fake üretilmez, blocker/readiness follow-up |
| AccountCode generator format | Hayır | Hayır | Hayır | **Format bu task'ta netleşti** (`ACC-{YYYY}-{sequence}`, tenant+year, §10.1a); yalnız sequence-store impl kalır |
| Permission nested-key validator | Hayır | **Hayır (doğrulandı)** | Hayır | `IsCanonicalPermissionKey` bugün **≥3 segment kabul ediyor** (§16); nested key'ler zaten geçerli. Kalan: seed-time catalog→auth sync doğrulaması (implementation-task) |
| MOD-0018 permission seed | Hayır | Hayır | Hayır (implementation-task) | `crm.account.*` seed impl task'ında |
| MOD-0018-FU15 data scope | Hayır | Hayır | **Hayır (MVP)** | MVP tenant scope; **MOD-0151/MOD-0155 için Evet** |
| MOD-0151 Territory/Zone/MicroZone | Hayır (MOD-0149) | Hayır | Hayır | Coverage read-only projection kaynağı; **coverage projection için Evet** |
| MOD-0155 Field Sales route planning | Hayır (MOD-0149) | Hayır | Hayır | territory/micro-zone'u MOD-0151 üzerinden tüketir |
| HCP identity SoR | Hayır | Hayır | Hayır | MOD-0150/MOD-0155 için kritik; MOD-0149 account/workplace foundation |
| Generic custom-field engine SoR | Hayır | Hayır | Hayır | EA karar; MOD-0149 yalnız account-level value surface (§10.2) |
| Static tenant shell menü | Hayır | Hayır | Hayır (implementation-task) | Accounts `<li>` elle (Adım 9) |
| `AccountExternalReference` import mapping | Hayır | Hayır | Hayır | import task'ında SourceSystem/SourceEntity eşleme |

**Özet (prerequisite closeout 2026-07-14):** Hiçbir item **pack approval**'ı bloklamaz. Ready-for-dev için kalan **3** açık
ön koşul: **(1) Diten.CrmService scaffold task'ı · (2) `crm` branch code (AGENTS.md §9) · (3) MOD-0048 lookup set/value
readiness**. **(4) Permission validator nested-key = DOĞRULANDI/KAPANDI** (`IsCanonicalPermissionKey` zaten ≥3 segment).
AccountCode format ve tüm içerik kararları kapandı. MOD-0048 **engine**'i `ready-for-dev`/~90% hazır; kalan yalnız CRM
reference **set/value authoring** (kod değil, MOD-0048 governance UI üzerinden data readiness).

## 29. Follow-up Items

- Implementation task'ında `execution/registries/module-implementation-status.md` MOD-0149 satırı güncellenir (Başlanmadı → in-progress → …).
- integration-agent: `/api/crm/accounts*` ocelot route çifti (OPTIONS dahil).
- MOD-0150 (Contact) pack'i ile Account↔Contact ilişki yüzeyi hizalanır.
- MOD-0155 legacy preservation design pack'i ile WorkPlace profil alanları paylaşımı doğrulanır (SoR sızıntısı olmadan).
- `commercial.*` namespace kararı MOD-0149'u etkilemez (`crm.*` yeterli).

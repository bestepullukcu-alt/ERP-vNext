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
> 3. **MOD-0048 reference readiness** — `account-type`/`account-status` required set kararları + 5 önerilen set + PSS-012/Business Reference Data SoR kabul; authoring template + operatör checklist hazır; **actual publish operatör aksiyonuna devredildi** (implementation validation'ı için Step 1–4 publish şart). Bkz. [readiness](../../../docs/records/audits/2026-08/mod-0149-crm-reference-data-readiness.md) + [authoring-closeout](../../../docs/records/audits/2026-08/mod-0149-crm-reference-data-authoring-closeout.md).
> 4. **Diten.CrmService scaffold** — MOD-0149-PREREQ ready-for-dev; 5-katman scaffold (port 5065) build 0/0, smoke 5/5, `/health` 200, Account business surface yok. Bkz. [scaffold audit](../../../docs/records/audits/2026-07/diten-crm-service-scaffold-implementation-2026-07-14.md).
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
| `crm.account.lookup-validation` | Treasury AR müşteri doğrulama ve seçici araması (§30; U-1, U-3, ertelenen U-2) — `crm.account.read` yerine geçmez, onu kapsamaz | Düşük (yalnız doğrulama alanları) | Tenant | Hayır (salt okuma, §30.1) |
| `crm.account-ship-to.read` | Sevkiyat alıcısı adresi okuma (§32; U-4) — `crm.account.read` ve `lookup-validation` yerine geçmez, onları kapsamaz | Düşük (ad, kod, durum, adres; kişi alanı yok) | Tenant | Hayır (salt okuma, §32.1) |

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
| CRM Admin | tüm `crm.account.*` (`crm.account.lookup-validation` dahil) + `crm.account-ship-to.read` (önek `crm.account.` değil; eşleşme tam eşitlik — açıkça yazılır, K-19) | Tenant |
| CRM Manager | read/create/update/export + hierarchy.manage + attribute.manage | Tenant |
| Sales Manager | read/export | Tenant (ekip, FU15) |
| Sales Representative / MR | read | Tenant → territory scoped later (FU15) |
| Marketing Manager | read | Tenant |
| Read-only CRM Viewer | read | Tenant |
| AR kullanıcısı (MOD-0120; atama MOD-0018-FU27) | yalnız `lookup-validation` — `read` **almaz** (K-1) | Tenant |
| Sevkiyat kullanıcısı (aday tüketiciler, istek: MOD-0178, MOD-0183 / MOD-0186; atama tüketici + MOD-0018) | `lookup-validation` (U-3 arama) + `account-ship-to.read` (U-4) — `read` **almaz** (K-13) | Tenant |

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

## 30. Treasury AR tüketimi (MOD-0120) — ek (2026-10-30)

> **ONAY BEKLİYOR — kod izni vermez.** Bu bölüm yalnız eklemedir; §1–§29'da yalnız §16 izin tablosu ve rol eşlemesi
> K-1 anahtarıyla genişledi (2026-11-02); mevcut AC'ler (§21), kapsam guard'ı ve `status` değişmedi.
> **Revizyon 2026-11-04:** bağımsız inceleme `docs/records/reviews/MOD-0149/2026-11-04-6df3626c.md` (KOŞULLU) bulguları
> F-1…F-9 işlendi; F-2 için sahip kararı K-8 ile seçici arama ucu U-3 eklendi (§30.2, §30.11).
> Kod yazılmadan önce bu pack sürümünün CT-APR-02 bağımsız incelemesi (`execution/approvals/reviews.tsv`)
> ve CT-APR-01 onay satırı (`execution/approvals/approvals.tsv`) gerekir. Kaynak: commercial-suite sahibi yanıtları
> Y-4/4b ve Y-4/4c (2026-10-27, `docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md:35`);
> MOD-0120 O-3 (KARAR D29/1, seçenek (a)) ve O-4.

### 30.1 Kapsam

- MOD-0120 Accounts Receivable müşteri kimliğini (`CustomerId` = `Account.Id`) bu modülden **yalnız sözleşmeyle** doğrular;
  AR hesap kaydını okumaz, yazmaz, `accounts` / `account_external_references` koleksiyonlarına doğrudan erişmez.
- Bu ek üç **salt-okuma** ucu (U-1, U-3; ertelenen U-2) ve sözleşme belgesini ekler. Account modeline alan, yeni koleksiyon, yeni yazma komutu ya da
  yeni denetim olayı eklenmez (§23 değişmez; okuma AUD-001 dışında).
- Kapsam dışı: AR'nin kendi port / sınıflandırıcı kodu (MOD-0120), Auth katalog kaydı ve rol ataması (MOD-0018),
  `ocelot.json` (integration-agent), müşteri seçici UI (MOD-0120 same-origin proxy). Seçicinin veri kaynağı bu ekin
  U-3 ucudur (K-8); seçimden sonra kimlik yazmadan önce U-1 ile doğrulanır.
- Bu ekin tüketicisi yalnız MOD-0120'dir. MOD-0177, MOD-0183 ve MOD-0186 sözleşme §5a'da **aday (istek)** olarak durur;
  kendi paketleri bu sözleşmeye atıfla hizalanınca tüketici olurlar (sahip SCE). Adaylar bu ekin kabulünü etkilemez.

### 30.2 Uçlar ve sözleşme

Sözleşme: [CRM-ACCOUNT-LOOKUP 1.0.0](../../../../docs/analysis/contracts/drafts/crm-account-lookup-validation-v1.0-proposal.md)
(taslak; çelişkide bu pack kazanır).

> **U-2 ertelendi (K-7, MOD-0120 sahibi 2026-10-30):** bu sürümde tüketicisi yok; MOD-0120 O-10 (içe aktarma / eski
> kayıt taşıma) ile etkinleşir. Tanımı burada ve sözleşmede kalır; bu sürümün işi yalnız DL-1 (U-1 ve U-3).

| Uç | Dilim | Yanıt | Dosyalar (Golden Compact düzeni, §13) |
|---|---|---|---|
| U-1 `GET /api/crm/accounts/{accountId:guid}/lookup-validation` | DL-1 | 200 `accountId`, `code`, `name`, `status`, `isBillable`, `contractVersion` · 404 (yok / silinmiş / başka kiracı — aynı gövde) · 503 | `Queries/GetAccountLookupValidationQuery.cs` · `Handlers/QueryHandlers/GetAccountLookupValidationHandler.cs` |
| U-3 `GET /api/crm/accounts/lookup?search=&page=&pageSize=&billableOnly=` | DL-1 | 200 sayfalı liste (öğe alanları U-1 ile aynı: `accountId`, `code`, `name`, `status`, `isBillable`; sayfa üstünde `page`, `pageSize`, `total`, `contractVersion`) · 400 (arama / sayfa sınırı) · 503 | `Queries/SearchAccountLookupQuery.cs` · `Handlers/QueryHandlers/SearchAccountLookupHandler.cs` |
| U-2 `GET /api/crm/accounts/external-references/resolve?sourceSystem=&externalId=` | DL-2 (ertelendi) | 200 (U-1 alanları + `sourceSystem`, `externalId`) · 400 (boş girdi) · 404 (eşleşme yok / hesap geçersiz) · 409 (çoklu eşleşme, savunma) · 503 | `Queries/GetAccountByExternalReferenceQuery.cs` · `Handlers/QueryHandlers/GetAccountByExternalReferenceHandler.cs` |

- Denetleyici: üç uç `Controllers/CRM/AccountController.cs`'e eklenir (§7'deki `Controllers/AccountController.cs` yolu
  bugün `Controllers/CRM/` altındadır). `lookup` ve `external-references/resolve` sabit yolları `{id:guid}` kısıtlı
  rotalarla çakışmaz (emsal aynı denetleyicide `filter-options`, `related`).
- DTO: `AccountLookupValidationDto` `AccountModels.cs` içinde (tek dosya kuralı).
- (U-2, DL-2) Depo: `IAccountExternalReferenceRepository`'ye çözümleme sorgusu eklenir (kiracı + `IsDeleted = false` +
  tam eşitlik; en çok 2 satır okur, 2 satır = 409).
- `isBillable = (Status == "active")`, sıradan karşılaştırma; faturalanabilirlik kuralı yalnız CRM'dedir.
- Kiracı reddi (K-11) **yalnız U-1 ve U-3'e bağlı bir uç filtresinde** yapılır (iki eyleme konan eylem filtresi):
  token'da `tenant_id` yoksa 400 döner, işleyici çağrılmaz. Ortak kiracı ara katmanı
  (`Infrastructure/Middleware/TenantResolutionMiddleware.cs`) **değişmez**; öteki CRM uçlarının davranışı aynı kalır.
- U-3 (K-8): `search` `Trim()` sonrası 2–100 karakter, aksi 400; düz metindir (regex değil) — mevcut sayfalı arama
  deposu `IAccountRepository.ListAsync` (`AccountRepository.cs:78-145`: kiracı + `IsDeleted = false`, ad ya da kodda
  "içerir", `TurkishInsensitivePattern` ile kaçışlı ve Türkçe i harflerine duyarsız) kullanılır; indeks
  `ix_accounts_tenant_name` var. `page` ≥ 1, `pageSize` 1–50 (varsayılan 20), aksi 400. `billableOnly=true` yalnız
  `active` döner. Sıralama ad artan, eşit adlarda `Id` artan: ikinci anahtar zorunludur, yoksa sayfa sınırında bir
  kayıt iki sayfada görünür ya da hiç görünmez.
  - **Yalnız U-3 yolunda.** Mevcut hesap listesinin sıralaması (`BuildSort`, `AccountRepository.cs:150-162`;
    `GetAccountListHandler.cs:75`) **değişmez**: kod alanı, azalan yön ve indeks garantisi olduğu gibi kalır, mevcut
    Account testleri tabandaki sonuçla aynıdır (AC-AR-10). U-3 sabit sıralamayı (ad artan, `_id` artan) depoya ayrı
    bir seçenek ya da ayrı bir yöntemle verir; liste sayfası bu seçeneği kullanmaz.
  - **Ek indeks DL-1 işidir:** `{TenantId: 1, AccountName: 1, _id: 1}` (ad `ix_accounts_tenant_name_id`), mevcut
    `ix_accounts_tenant_name` değişmez. Mevcut indeks ad + `_id` sıralamasını karşılamaz; bellek içi sıralama sınırı
    (kod yorumu `AccountRepository.cs:147-149`) bu yüzden ek indeksle önlenir.
  Eşleşme yoksa 200 boş liste (404 yok). U-3 sonucu uygunluk kararı
  değildir: tüketici seçilen kimliği yazmadan önce U-1 ile doğrular.
- (U-2, DL-2) `sourceSystem` / `externalId`: yazma yoluyla aynı normalizasyon — yalnız `Trim()`, büyük/küçük harf korunur
  (`CreateAccountHandler.cs:62-73`); çözümleme `"default"` kaynağını varsaymaz.
- `account-status` kümesi sözleşmede yayımlanır: `active` (faturalanabilir), `inactive`, `draft`, `suspended` (Y-4/4c) ve
  `archived` (karar K-2, Beste 2026-10-30; faturalanamaz). AR tarafında sabit durum tablosu olmaz.

### 30.3 İzin anahtarı

- **Karar K-1 (Beste, 2026-10-30): `crm.account.lookup-validation`.** Üç uç (U-1, U-3, U-2) yalnız bu anahtarla korunur
  (`[HasPermission("crm.account.lookup-validation")]`); `crm.account.read` bu uçlara **yetmez**, AR kullanıcısı
  `crm.account.read` **almaz** (MOD-0120 O-3 (b) reddedildi).
- PKS-001 uyumu: 3 segment; `lookup-validation` Tier-3 eylem sözlüğünde var; emsal `auth.users.lookup-validation`.
- Anahtar §16 izin tablosuna ve rol eşlemesine eklendi (2026-11-02, bağımsız inceleme öncesi revizyon).
- Ayrı işler: Auth katalog kaydı (MOD-0018), AR rollerine atama (MOD-0018-FU27 ya da sahibin seçtiği yer).

### 30.4 Gateway

Mevcut `/api/crm/accounts/{everything}` rotası (`gateway/Diten.ApiGateway/ocelot.json:2970-2989`, GET dahil, port 5061)
üç ucu da taşır (U-1, U-3 — DL-1; U-2 — DL-2); yeni rota gerekmez, `ocelot.json` değişmez. Teyit integration-agent'ındır (yalnız not; §17 değişmez).

### 30.5 Kabul ölçütleri — DL-1 (U-1 ve U-3)

- [ ] **AC-AR-1** (U-1) Kendi kiracısındaki `active` hesap için 200, `isBillable = true`, alanlar sözleşme §2.2 ile birebir; gövdede adres / sorumlu kişi / dış referans / öznitelik **yok**.
- [ ] **AC-AR-2** (U-1) `inactive`, `draft`, `suspended`, `archived` ve kümede olmayan bir kod için 200, `isBillable = false`.
- [ ] **AC-AR-3** (U-1) Başka kiracının hesabı, silinmiş hesap ve olmayan kimlik → 404; üç gövde bayt bayt aynı.
- [ ] **AC-AR-7a** (U-1, U-3) Depo okunamazsa (Mongo istisnası / zaman aşımı) 503; 500, 200, 404 ya da boş liste dönmez.
- [ ] **AC-AR-8a** (U-1, U-3) `crm.account.lookup-validation` olmadan 403; yalnız `crm.account.read` sahibi kullanıcı 403 alır; anahtar sahibi `crm.account.read` olmadan 200 alır.
- [ ] **AC-AR-9a** (U-1, U-3) Üretici uyumu gerçek işleyicilerle ölçülür: aynı veriyle U-1 ve U-3 her `account-status` değeri ve küme dışı bir kod için aynı `isBillable`'ı verir ve bu değer her zaman `status == "active"` ile aynıdır (çelişkili satır üretilmez); `contractVersion` `crm-account-lookup/1.0`; alanlar sözleşme §2.2 / §2a.2 ile birebir. Sözleşme §9 fikstürleri tüketicinin testidir (MOD-0120 AC-3, üretim sınıflandırıcısına karşı); CRM'de sınıflandırıcı kopyası yazılmaz.
- [ ] **AC-AR-10** `dotnet build` (CrmService) PASS; mevcut Account testleri tabandaki sonuçla aynı.
- [ ] **AC-AR-11** (U-3) Yalnız çağıranın kiracısındaki silinmemiş hesaplar döner; öğe alanları birebir `accountId`, `code`, `name`, `status`, `isBillable` (sözleşme §2a); adres / sorumlu kişi / dış referans / öznitelik **yok**; `isBillable` U-1 kuralıyla aynı.
- [ ] **AC-AR-12** (U-3) `search` `Trim()` sonrası 2 karakterden kısa ya da 100'den uzun → 400; `page < 1`, `pageSize < 1` ya da `> 50` → 400; hiçbirinde liste dönmez.
- [ ] **AC-AR-13** (U-3) Arama düz metindir: `.*` ya da `a.b(` her hesabı eşleştirmez ve hata vermez; ad ya da kodda "içerir"; `HAMİDİYE` kaydı `hamidiye` aramasıyla bulunur.
- [ ] **AC-AR-14** (U-3) `billableOnly=true` yalnız `active` hesapları döner; sıralama ad artan, eşit adda `Id` artan; aynı adlı üç hesap `pageSize=1` ile üç sayfada her biri tam bir kez görünür; `total` filtreli sayıdır; eşleşme yoksa 200 boş liste.
- [ ] **AC-AR-15** (U-1, U-3) Kiracı: `X-Tenant-Id` başlığı token'daki `tenant_id`'den farklıysa 400; kiracısız çağrı (başlık yok) 400; token'da `tenant_id` yoksa `X-Tenant-Id` başlığı olsa bile 400 (K-11); üç durumda da veri dönmez ve depo sorgulanmaz (sözleşme §1). Red uca özel filtrededir: filtre yalnız U-1 ve U-3 eylemlerinde bulunur (öznitelik / yansıma testi) ve ortak kiracı ara katmanı bu dilimde değişmez. Öteki CRM uçlarının `tenant_id`'siz token karşısındaki davranışı bu ekin konusu değildir (BL-324); testler o uçların veri döndürmesini **beklemez ve sabitlemez**.

### 30.6 Kabul ölçütleri — DL-2 (U-2 ertelendi)

- [ ] **AC-AR-4** (U-2) Tam eşleşmede 200 ve U-1 ile aynı doğrulama alanları; eşleşme yok → 404; harf farkı (`oldcrm` / `OldCRM`) eşleşmez → 404; başka kiracıdaki eşleşme → 404.
- [ ] **AC-AR-5** (U-2) İki etkin aday satırda 409; hiçbir hesap döndürülmez.
- [ ] **AC-AR-6** (U-2) Boş / yalnız boşluk `sourceSystem` ya da `externalId` → 400; `Trim()` sonrası 256 karakterden uzun `sourceSystem` ya da `externalId` → 400 (sözleşme §3.1).
- [ ] **AC-AR-7b** (U-2) Depo okunamazsa 503; 500, 200 ya da 404 dönmez.
- [ ] **AC-AR-8b** (U-2) AC-AR-8a'nın üç izin koşulu U-2 için de geçerlidir.
- [ ] **AC-AR-9b** (U-2) Üretici uyumu gerçek U-2 işleyicisiyle ölçülür: 200 gövdesi U-1 ile aynı alanları ve aynı `isBillable` kuralını taşır; eşleşme yokluğu 404, çoklu aday 409 (sözleşme §3, §7). F-R404 / F-R409 tüketici testidir (MOD-0120 O-10).

### 30.7 Sabotaj beklentileri — DL-1

Her guard en az bir sabotajla kırmızıya döner (CT kendi sabotajını da koşar; sabotaj kanıtı olmayan guard sayılmaz):

| Sabotaj | Kırmızıya dönmesi gereken |
|---|---|
| U-1 depo çağrısından kiracı filtresini kaldır | AC-AR-3 (başka kiracı 200 döner) |
| `IsDeleted` filtresini kaldır | AC-AR-3 (silinmiş hesap 200 döner) |
| Başka kiracının hesabı için ayrı ileti ya da 403 döndür | AC-AR-3 (gövdeler aynı değil) |
| `isBillable`'ı `Status != "inactive"` yap | AC-AR-2 (`draft` / `suspended` / `archived` faturalanabilir olur) |
| U-1 ya da U-3'te `[HasPermission]`'ı `crm.account.read` yap | AC-AR-8a |
| Uca `crm.account.read` + `crm.account.lookup-validation` ikisini birden iste | AC-AR-8a (anahtar sahibi `read` olmadan 403 alır) |
| Mongo istisnasını yakalama / 404'e ya da boş listeye çevir | AC-AR-7a |
| U-1 gövdesine ya da U-3 öğesine `AddressLine` ekle | AC-AR-1, AC-AR-11 |
| U-3'te `isBillable`'ı U-1'den ayrı bir kuralla hesapla (ör. `Status != "archived"`) | AC-AR-9a (iki uç farklı `isBillable` verir) |
| `contractVersion`'ı `crm-account-lookup/2.0` yap | AC-AR-9a |
| U-3 aramasında kiracı ya da `IsDeleted` filtresini kaldır | AC-AR-11 |
| U-3'te en kısa arama sınırını kaldır | AC-AR-12 (tek harf aramada liste döner) |
| U-3'te terimi kaçışsız regex olarak kullan | AC-AR-13 (`.*` her hesabı döner) |
| U-3'te `billableOnly`'yi yok say | AC-AR-14 |
| U-3'te 100 karakter üst sınırını kaldır | AC-AR-12 (101 karakterlik aramada liste döner) |
| U-3'te `pageSize` üst sınırını kaldır | AC-AR-12 (`pageSize=51` liste döner) |
| U-3'te `page < 1` kontrolünü kaldır | AC-AR-12 (`page=0` liste döner) |
| U-3'te ikinci sıralama anahtarını (`Id`) kaldır | AC-AR-14 (sıralama tanımı testi `_id`'yi bulamaz) |
| U-3 sıralamasını liste sayfasının `BuildSort`'una da uygula | AC-AR-10 (liste sayfası sırası testi) |
| U-3'te sıralamayı azalan yap | AC-AR-14 |
| U-3'te `total`'ı filtresiz sayıdan ver | AC-AR-14 |
| İşleyicide kiracı yoksa kiracı filtresiz sorguya devam et | AC-AR-15 (kiracısız çağrı veri döner) |
| Token'da `tenant_id` yokken `X-Tenant-Id` başlığını kabul et | AC-AR-15 (başlıktaki kiracının verisi döner) |
| K-11 reddini ortak `TenantResolutionMiddleware`'e taşı | AC-AR-15 (filtre U-1 / U-3 eylemlerinde bulunmaz; öznitelik testi kırmızı) |
| K-11 filtresini denetleyici ya da genel (global) düzeye koy | AC-AR-15 (filtre U-1 / U-3 dışındaki eylemlerde de bulunur) |

### 30.8 Sabotaj beklentileri — DL-2

| Sabotaj | Kırmızıya dönmesi gereken |
|---|---|
| Çözümlemede `ToLowerInvariant()` ekle | AC-AR-4 (harf farkı eşleşir) |
| Çoklu adayda ilk satırı döndür | AC-AR-5 |
| Boş girdi kontrolünü kaldır (boş `sourceSystem` ile aramaya git) | AC-AR-6 |
| Mongo istisnasını 404'e çevir | AC-AR-7b |
| U-2'de `[HasPermission]`'ı `crm.account.read` yap | AC-AR-8b |
| U-2 200 gövdesinde `isBillable`'ı hesaplamadan `true` yaz | AC-AR-9b |
| 256 karakter sınırını kaldır | AC-AR-6 |

### 30.9 Test beklentileri — DL-1

- **xUnit (Application):** U-1 işleyicisi için kiracı, silme, durum tablosu ve depo hatası (sahte depo istisna atar);
  U-3 işleyicisi için kiracı, silme, arama ve sayfa sınırları, düz metin arama (`.*`, `a.b(`), Türkçe i, `billableOnly`,
  sayfalama ve depo hatası vakaları.
- **xUnit (Api / yetki):** U-1 ve U-3 için `[HasPermission]` anahtar ataması; `crm.account.read` yalnız → 403.
- **Kiracı (AC-AR-15):** U-1 ve U-3 için başlık ↔ token uyuşmazlığı (400), kiracısız çağrı (400, veri yok) ve
  `tenant_id`'siz token + `X-Tenant-Id` başlığı (400, veri yok); K-11 filtresinin yalnız U-1 ve U-3 eylemlerinde
  bulunduğu (öznitelik / yansıma testi; ortak ara katman değişmez). Öteki uçların veri döndürmesi beklenmez ve
  ölçülmez — o uçların kiracı davranışı BL-324'ün işidir.
- **Depo testleri (gerçek mongod):** kiracı / `IsDeleted` filtresi, kaçışlı arama, filtreli `total` ve sıralama depoda
  olduğundan, sahte depolu işleyici testi bu sabotajları göremez. AC-AR-3 (U-1 depo okuması), AC-AR-11, AC-AR-13 ve
  AC-AR-14 test sahipli mongod ile koşar (`tests/Diten.CrmService.Application.Tests/CentralAudit/DisposableCrmMongod.cs`
  kalıbı; geliştiricinin 27017'sine bağlanılmaz; CI'da `DITEN_TEST_MONGOD`). İkinci anahtar ayrıca doğrudan sınanır:
  U-3'ün sıralama tanımı BSON'a çevrilir ve `{AccountName: 1, _id: 1}` olduğu doğrulanır (eşit adlı kayıtların
  sayfalaması tek başına sabotajı yakalamayabilir). `ix_accounts_tenant_name_id` indeksinin açıldığı da bu testlerde
  ölçülür. Liste sayfasının sıralama tanımı değişmediği ayrıca sınanır (AC-AR-10).
- **Üretici uyumu (AC-AR-9a):** U-1 ve U-3 işleyicileri aynı veriyle çağrılır; her `account-status` değeri ve küme dışı
  bir kod için iki uç aynı `isBillable`'ı verir; `contractVersion` ve alan listesi sözleşmeyle karşılaştırılır. Sözleşme
  §9 fikstürleri tüketicinin (MOD-0120) testidir; CRM'de sınıflandırıcı kopyası yazılmaz (Çekirdek 10).
- **Korelasyon (K-10):** uygulayıcı, CrmService Api'de gelen `X-Correlation-Id`'yi günlük kapsamına koyan paylaşılan bir
  ara katman olup olmadığını ölçer; varsa U-1 ve U-3 için bağlar, yoksa raporuna yazar. Yeni mekanizma yazılmaz;
  sözleşme 1.0 korelasyonu garanti etmez.
- **Build:** `dotnet build services/Diten.CrmService/...Api.csproj -c Debug`.
- Canlı veri ölçümü (indeksin canlı veritabanındaki varlığı) bu ekin parçası değildir; ayrı operatör adımıdır.

### 30.10 Test beklentileri — DL-2

- **xUnit (Application):** U-2 işleyicisi için kiracı, silme, normalizasyon (harf korunur), çoklu eşleşme, boş girdi ve
  depo hatası vakaları.
- **xUnit (Api / yetki):** U-2 için `[HasPermission]` anahtar ataması; `crm.account.read` yalnız → 403.
- **Üretici uyumu (AC-AR-9b):** U-2 200 gövdesi U-1 ile aynı alanlar ve `isBillable`; F-R404 / F-R409 tüketicinin
  (MOD-0120 O-10) testidir.

### 30.11 Kararlar ve açık sorular

| # | Karar | Durum |
|---|---|---|
| K-1 | İzin anahtarı | **KAPANDI** (Beste, 2026-10-30): `crm.account.lookup-validation` (§30.3) |
| K-2 | `archived` sözleşmede yayımlanır mı | **KAPANDI** (Beste, 2026-10-30): evet, `isBillable = false` |
| K-3 | U-2 409 (çoklu eşleşme) karşısında tüketicinin davranışı | **ÜRETİCİ KURALI** (Beste, 2026-10-30): tüketici tahmin etmez, hesap seçmez, işlemi durdurur. Hata kalıcıdır (tekrar denemek çözmez; çözüm CRM'de mükerrer dış referansın temizlenmesidir) → öneri 422 ayrı kod (ör. `RECEIVABLE_CUSTOMER_AMBIGUOUS`), 503 değil. Kod adı MOD-0120 sahibinindir. |
| K-4 | Gateway joker rotası | **KAPANDI** (Beste, 2026-10-30): yeterli; integration-agent teyidi yalnız not (§30.4) |
| K-5 | `runtime_code_scope` künyesi | **KAPANDI** (Beste, 2026-10-30): değişmez — "external-ref surface" U-2'yi, "Account/WorkPlace master + 360" okuma yüzeyi U-1'i ve U-3'ü (K-8) kapsar; yeni nesne / alan / komut yok |
| K-6 | Teslim dilimleri tablosu | **KAPANDI** (Beste, 2026-10-30): yalnız bu ek için tablo (§31); §1–§29'un mevcut, teslim edilmiş işi geriye dönük dilimlere bölünmez |
| K-8 | MOD-0120 müşteri seçicisinin veri kaynağı (inceleme F-2) | **KAPANDI** (Beste, 2026-11-04, seçenek 1): dar arama ucu U-3 `GET /api/crm/accounts/lookup`, aynı `crm.account.lookup-validation` anahtarıyla; yalnız `accountId`, `code`, `name`, `status`, `isBillable` döner; DL-1'e girer. AR kullanıcısı `crm.account.read` almaz (K-1 korunur). MOD-0120:1146-1147 ve :1349-1350 U-3'e hizalanır (Finans'ın işi). |
| K-7 | U-2'nin (dış referans çözümleme) MOD-0120'deki kullanım yeri | **KAPANDI** (MOD-0120 sahibi, 2026-10-30): bu sürümde U-2'yi çağıran akış yok — `CustomerId` müşteri seçiciden `Account.Id` olarak gelir ve U-1 ile doğrulanır; U-2'yi kullanacak içe aktarma ve eski kayıt taşıma MOD-0120 O-10 ile bu sürümün dışında, banka eşleştirme pakette yok. **DL-2 O-10'a kadar ertelendi;** DL-1 ve sözleşme belgesi bu sürümün önkoşulu. `sourceSystem` listesi O-10'da belirlenir; büyük/küçük harf kuralı sözleşmede (§3.1) bağlayıcı. K-3'teki tüketici hata kodu O-10 kapsamında MOD-0120'ye girer. |
| K-10 | Gelen `X-Correlation-Id`'nin CRM günlük kapsamına bağlanması (sözleşme §7) | **AÇIK** (sahip commercial-suite / K2): sözleşme 1.0 garanti etmez; DL-1 uygulayıcısı paylaşılan ara katmanı ölçer, varsa bağlar (§30.9); yoksa K2 sonraki ek ya da platform işi olarak karar verir. Dondurmayı engellemez. |
| K-11 | `tenant_id` taşımayan token'la U-1 / U-3 çağrısı (inceleme 2026-11-05-999c7e29 F-3) | **KAPANDI** (Beste, 2026-11-05): reddedilir — token'da `tenant_id` yoksa `X-Tenant-Id` başlığı olsa bile 400, veri yok (AC-AR-15). Gateway'in token'sız kiracı yolları (istemci başlığı, alt alan adı, Development yedeği) bu iki uca kiracı seçtiremez. |

Kayıt: K-1…K-6, K-8 ve K-11 `docs/records/decisions/2026-11/mod0149-ar-lookup-owner-decisions-01.md` (durum: taslak — sahip okuması CT-APR-01 onayıyla
birlikte kayda geçer); K-7 = D112/2 (`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md`, MOD-0120 sahibi).

## 31. Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | Treasury AR müşteri doğrulama ve seçici arama uçları (U-1 lookup-validation ve U-3 lookup) | Kapsam, Uçlar ve sözleşme, İzin anahtarı, Permission / Authorization Convention, Gateway, Kabul ölçütleri — DL-1 (U-1 ve U-3), Sabotaj beklentileri — DL-1, Test beklentileri — DL-1 |
| DL-2 | Dış referans çözümleme ucu (U-2 external-references/resolve) — ERTELENDİ: MOD-0120 O-10 ile (K-7) | Uçlar ve sözleşme, İzin anahtarı, Gateway, Kabul ölçütleri — DL-2 (U-2 ertelendi), Sabotaj beklentileri — DL-2, Test beklentileri — DL-2 |
| DL-3 | Sevkiyat adresi okuma ucu (U-4 ship-to, yalnız ana adres) — §32 | Sevkiyat adresi — kapsam, Sevkiyat adresi — uç ve sözleşme, Sevkiyat adresi — izin anahtarı, Permission / Authorization Convention, Sevkiyat adresi — gateway, Kabul ölçütleri — DL-3 (U-4), Sabotaj beklentileri — DL-3, Test beklentileri — DL-3 |

## 32. Sevkiyat adresi okuma (U-4) — ek (2026-11-13)

> **ONAY BEKLİYOR — kod izni vermez.** Bu bölüm yalnız eklemedir; §1–§31'de yalnız §16 izin tablosu ve rol eşlemesi
> K-13 anahtarıyla genişler, §31'e DL-3 satırı eklenir. Mevcut AC'ler (§21, §30.5), `status` ve `runtime_code_scope`
> değişmez — **tek istisna AC-AR-15'in filtre kümesidir:** DL-3 ile K-11 filtresi U-4'e de konur, AC-AR-15'teki
> "yalnız U-1 ve U-3" kümesi {U-1, U-3, U-4} olur (§32.2; inceleme 2026-11-13-0a9fd4ef F-1). Kod yazılmadan önce bu pack sürümünün CT-APR-02 bağımsız incelemesi (`execution/approvals/reviews.tsv`) ve
> CT-APR-01 onay satırı (`execution/approvals/approvals.tsv`) gerekir. Kaynak: WMS (MOD-0178 çıkış sevkiyatı formu v2)
> isteği 2026-11-13; sözleşme K-9 (MVP-6 adres yüzeyi); commercial-suite sahibi kararları K-12…K-18 (Beste, 2026-11-13).

### 32.1 Sevkiyat adresi — kapsam

- Sevkiyat tüketicileri — hepsi **aday (istek)**: MOD-0178 (WMS ekibinin "çıkış sevkiyatı formu v2" isteği,
  2026-11-13; MOD-0178 paketi bugün `customerRef` / adres okuması içermez — paket sözleşmeye hizalanınca tüketici olur,
  sahip SCE), MOD-0183 / MOD-0186 (MVP-6) — alıcıyı CRM hesabından seçer ve
  hesabın kimliğini (`customerRef` = `Account.Id`) ve adres kopyasını tutar. Adresi **yalnız sözleşmeyle** okurlar:
  `crm.account.read` almazlar, tam hesap kaydını (`GET /api/crm/accounts/{id}`) okumazlar.
- Bu ek tek bir **salt-okuma** ucu (U-4) ve bir izin anahtarı ekler. Account modeline alan, yeni koleksiyon, yeni yazma
  komutu, yeni indeks ya da denetim olayı eklemez (§23 değişmez).
- **DL-3 yalnız ana adresi** taşır (`Account.CountryRef`, `CityRef`, `DistrictRef`, `AddressLine`). Çoklu teslimat
  adresi (K-18) ve ana adrese posta kodu (K-16) **sonraki dilimdir (DL-4, ayrı ek, §32.10)**; sözleşme biçimi bugünden
  liste olduğu için DL-4 U-4'ün biçimini değiştirmez.
- Kapsam dışı: tüketicilerin kendi seçici / adaptör kodu (MOD-0178, MOD-0183, MOD-0186), Auth rol ataması (MOD-0018),
  `ocelot.json` (integration-agent). Seçicide arama mevcut U-3'tür (§30.2); U-3 değişmez.
- `runtime_code_scope` künyesi değişmez: U-4 "Account/WorkPlace master + 360" okuma yüzeyidir (K-5 emsali).

### 32.2 Sevkiyat adresi — uç ve sözleşme

Sözleşme: [CRM-ACCOUNT-LOOKUP 1.1.0 taslağı](../../../../docs/analysis/contracts/drafts/crm-account-lookup-v1.1-ship-to-proposal.md)
(1.0.0'a ekleme; çelişkide bu pack kazanır).

| Uç | Dilim | Yanıt | Dosyalar (Golden Compact düzeni, §13) |
|---|---|---|---|
| U-4 `GET /api/crm/accounts/{accountId:guid}/ship-to` | DL-3 | 200 `accountId`, `code`, `name`, `status`, `defaultAddressId`, `addresses[]`, `contractVersion` · 400 (kiracı, K-11) · 403 · 404 (yok / silinmiş / başka kiracı — aynı gövde) · 503 | `Queries/GetAccountShipToQuery.cs` · `Handlers/QueryHandlers/GetAccountShipToHandler.cs` |

- Denetleyici: `Controllers/CRM/AccountController.cs`; `[HasPermission("crm.account-ship-to.read")]` ve U-1/U-3'teki
  `[RequireTokenTenant]` filtresi (K-11) **yalnız bu eyleme** eklenir. Ortak kiracı ara katmanı
  (`Infrastructure/Middleware/TenantResolutionMiddleware.cs`) değişmez.
- **AC-AR-15 istisnası (tek):** filtrenin bulunduğu eylem kümesi {U-1, U-3} → {U-1, U-3, U-4} olur. Bu dilimde
  güncellenenler: DL-1 yansıma testi (`AccountLookupApiTests.cs`, `AC_AR_15_K11_filter_is_on_u1_and_u3_only_and_never_global`)
  beklenen kümesi ve adı; `Api/Filters/RequireTokenTenantAttribute.cs` açıklaması. Filtre yine denetleyici ya da genel
  düzeyde **değildir**; AC-AR-15'in öteki koşulları (400 durumları, ortak ara katman değişmez, öteki uçlar sabitlenmez)
  aynı kalır.
- DTO: `AccountShipToDto` ve `AccountShipToAddressDto` `AccountModels.cs` içinde (tek dosya kuralı); U-4 sabitleri
  (`contractVersion = "crm-account-lookup/1.1"`, `"primary"`) `AccountLookupContract.cs`'te. U-1 / U-3 gövdeleri ve
  `contractVersion` değerleri (`crm-account-lookup/1.0`) **değişmez**.
- Depo: mevcut `IAccountRepository.GetByIdAsync` (kiracı + `IsDeleted = false`, `AccountRepository.cs` `ActiveTenant`).
  Yeni sorgu ya da indeks yok.
- **`addresses` kuralı (DL-3):**
  - Dört ana adres alanı `Trim()` edilir; boş kalan null olur.
  - En az biri doluysa tek öğe: `addressId = "primary"`, `kind = "primary"`, `label = null`, `isDefault = true`,
    `postalCode = null`, `defaultAddressId = "primary"`.
  - Dördü de boşsa `addresses = []`, `defaultAddressId = null`, yine 200.
  - `addressLine` kesilmez.
- **Durum 404'e katılmaz:** `active` dışındaki hesap 200 döner, `status` saklandığı gibi (MOD-0048 `account-status` kodu).
  Seçim ve uyarı kuralı tüketicidedir (K-14: WMS yalnız `active` seçtirir, kayıt ve "sevke hazır"da yeniden okur).
- Gövde **taşımaz:** sorumlu kişi (ad / telefon / e-posta), notlar, üst hesap, dış referanslar, öznitelikler, logo, konum,
  `isBillable`, görünen ad (ülke / şehir / ilçe adı — K-15, tüketici Reference Data'dan çözer).
- 503: depo okunamazsa (Mongo istisnası / zaman aşımı) 503; 500, 200 ya da 404 dönmez (U-1 eşlemesiyle aynı yol).

### 32.3 Sevkiyat adresi — izin anahtarı

- **Karar K-13 (Beste, 2026-11-13): `crm.account-ship-to.read`.** PKS-001: 3 segment, kaynak `account-ship-to`
  (`^[a-z][a-z0-9-]*$`), Tier-1 eylem `read`.
- U-4'e `crm.account.read` **yetmez** (tam kayıt anahtarı); `crm.account.lookup-validation` **yetmez** (Hazine AR
  rollerine adres açılmaz, K-1 korunur). Anahtar U-1 / U-3'e **yetmez**; U-3 kendi anahtarıyla kalır.
- Sevkiyat rolleri iki anahtarı birlikte alır: `crm.account.lookup-validation` (U-3 arama) + `crm.account-ship-to.read`
  (U-4). Atama tüketicinin Auth ile işidir.
- Katalog: CRM manifest'ine `ApiPermissions` satırı (`CrmManifestProvider.cs`, scope `Tenant`) — DL-1-FU01 emsali
  (#396). Bu satır Platform dosyasındadır: DL-3'ün ayrı küçük işi (DL-3b), aynı kabulle.
- Veri kapsamı: kiracı geneli (§30.3 ile aynı not).

### 32.4 Sevkiyat adresi — gateway

Mevcut `/api/crm/accounts/{everything}` rotası (GET dahil, port 5061) U-4'ü taşır; yeni rota yok, `ocelot.json`
değişmez (teyit integration-agent'ın, yalnız not; §17 ve §30.4 değişmez).

### 32.5 Kabul ölçütleri — DL-3 (U-4)

- [ ] **AC-ST-1** Kendi kiracısındaki hesap, dört ana adres alanı dolu, **ek teslimat adresi ve `Account.PostalCode` verisi yokken** (DL-4 öncesi) → 200; `addresses` tek öğe (`addressId = "primary"`, `kind = "primary"`, `label = null`, `isDefault = true`, `postalCode = null`), alanlar kırpılmış; `defaultAddressId = "primary"`; `contractVersion = "crm-account-lookup/1.1"`; `code`, `name`, `status` hesapla aynı.
- [ ] **AC-ST-2** Dört ana adres alanı boş ya da yalnız boşluk → 200, `addresses = []`, `defaultAddressId = null`. Yalnız biri dolu → tek `primary` öğe, öteki alanlar null.
- [ ] **AC-ST-3** Başka kiracının hesabı, silinmiş hesap ve olmayan kimlik → 404; üç gövde bayt bayt aynı; depo kiracı + `IsDeleted` filtresiyle okunur.
- [ ] **AC-ST-4** `inactive`, `draft`, `suspended`, `archived` ve kümede olmayan bir kod → 200; `status` saklandığı gibi; adres yine döner.
- [ ] **AC-ST-5** Gövde alan listesi tam eşleşme: üst düzeyde yalnız `accountId`, `code`, `name`, `status`, `defaultAddressId`, `addresses`, `contractVersion`; öğede yalnız `addressId`, `kind`, `label`, `isDefault`, `countryRef`, `cityRef`, `districtRef`, `postalCode`, `addressLine`. Sorumlu kişi, telefon, e-posta, not, dış referans, öznitelik, logo, konum, `isBillable` alanları dolu bir hesapla ölçülür ve hiçbiri gövdede yoktur.
- [ ] **AC-ST-6** İzin: anahtarsız 403; yalnız `crm.account.read` → 403; yalnız `crm.account.lookup-validation` → 403; yalnız `crm.account-ship-to.read` → 200. `crm.account-ship-to.read` U-1 ve U-3'e yetmez (403).
- [ ] **AC-ST-7** Kiracı (K-11): başlık ↔ token uyuşmazlığı 400; kiracısız çağrı 400; `tenant_id`'siz token + `X-Tenant-Id` 400; üçünde veri dönmez, depo sorgulanmaz. `RequireTokenTenant` filtresi U-4 eyleminde bulunur (öznitelik / yansıma); ortak kiracı ara katmanı değişmez.
- [ ] **AC-ST-8** Depo okunamazsa 503; 500, 200 ya da 404 dönmez.
- [ ] **AC-ST-9** Değişmezlik: U-1 ve U-3 gövdeleri, `contractVersion` değerleri (`crm-account-lookup/1.0`) ve izinleri değişmez — mevcut `AccountLookup` testleri tabandaki sonuçla aynı, **tek istisna** AC-AR-15 filtre kümesi testinin beklenen kümesi {U-1, U-3, U-4} (§32.2); Account listesi / detay testleri tabanla aynı; `dotnet build` (CrmService) PASS.
- [ ] **AC-ST-10** (DL-3b) CRM manifest'inde `crm.account-ship-to.read` tam bir kez, scope `Tenant`, boş olmayan görünen adla; hiçbir sayfa ya da eyleme bağlı değil; kayıt işleyicisi anahtarı Auth kataloğuna CRM modülüyle eşitler (DL-1-FU01 testleri emsali). Testler CRM sayfa listesini **sabitlemez** (alt küme kontrolü).

### 32.6 Sabotaj beklentileri — DL-3

| Sabotaj | Kırmızıya dönmesi gereken |
|---|---|
| U-4 depo okumasından kiracı filtresini kaldır | AC-ST-3 |
| `IsDeleted` filtresini kaldır | AC-ST-3 |
| Başka kiracının hesabı için ayrı ileti ya da 403 döndür | AC-ST-3 |
| `active` dışı hesap için 404 döndür | AC-ST-4 |
| Gövdeye `ResponsiblePersonPhone` (ya da herhangi bir kişi alanı: `ResponsiblePersonName` / `ResponsiblePersonEmail`) ekle | AC-ST-5 |
| Gövdeye `isBillable` ekle | AC-ST-5 |
| Adres alanlarında `Trim()`'i kaldır | AC-ST-1 |
| Dört alan boşken yine `primary` öğe üret | AC-ST-2 |
| `defaultAddressId`'yi liste boşken `"primary"` bırak | AC-ST-2 |
| `[HasPermission]`'ı `crm.account.read` yap | AC-ST-6 |
| `[HasPermission]`'ı `crm.account.lookup-validation` yap | AC-ST-6 |
| U-3'e `crm.account-ship-to.read`'i alternatif anahtar olarak ekle | AC-ST-6 |
| U-4'ten `RequireTokenTenant`'ı kaldır | AC-ST-7 |
| Token'da `tenant_id` yokken `X-Tenant-Id`'yi kabul et | AC-ST-7 |
| Mongo istisnasını 404'e ya da boş listeye çevir | AC-ST-8 |
| U-1'in `contractVersion`'ını `crm-account-lookup/1.1` yap | AC-ST-9 |
| Manifest anahtarının scope'unu `tenant` (küçük harf) yap | AC-ST-10 |

### 32.7 Test beklentileri — DL-3

- **xUnit (Application):** U-4 işleyicisi — dolu / kısmi / boş adres, kırpma, durum tablosu (beş durum + küme dışı kod),
  kişi alanları dolu hesapta gövde alan listesi (yansıma ya da JSON anahtar kümesi tam eşleşme), depo hatası (sahte depo
  istisna atar → 503).
- **Depo (gerçek mongod):** kiracı ve `IsDeleted` filtresi depodadır → AC-ST-3 test sahipli mongod ile koşar
  (`DisposableCrmMongod` kalıbı; 27017'ye bağlanılmaz; CI'da `DITEN_TEST_MONGOD`).
- **Api / yetki:** `[HasPermission]` anahtar ataması ve izin durumları (AC-ST-6): U-4'te anahtarsız, yalnız
  `crm.account.read`, yalnız `crm.account.lookup-validation` → 403, yalnız `crm.account-ship-to.read` → 200; ayrıca
  **yalnız `crm.account-ship-to.read` taşıyan token ile U-1 ve U-3 → 403** (Api testi; "U-3'e alternatif anahtar"
  sabotajını yakalar). `RequireTokenTenant` yalnız U-1, U-3 ve U-4 eylemlerinde (yansıma; `EndpointDataSource` +
  `MvcOptions.Filters` — DL-1 testinin beklenen kümesi {U-1, U-3, U-4} olur, §32.2 istisnası); kiracı üç durumu (AC-ST-7).
- **Değişmezlik (AC-ST-9):** mevcut `AccountLookup*Tests` ve Account testleri tabanla karşılaştırılır.
- **DL-3b (Platform):** `CrmManifestAccountLookupPermissionTests` emsali; sayfa listesi alt küme kontrolü.
- **Gelecek dilime karşı (DL-4):** testler ek teslimat adresi verisi olmayan hesapla ölçer; "liste en çok bir öğe" ya da
  "`postalCode` her zaman null" **genel kural olarak sabitlenmez** — ölçüt "ek adres verisi yokken" diye yazılır.
- **Build:** `dotnet build services/Diten.CrmService/...Api.csproj -c Debug`.

### 32.8 Sevkiyat adresi — kararlar ve açık sorular

| # | Karar | Durum |
|---|---|---|
| K-12 | Tasarım | **KAPANDI** (Beste, 2026-11-13): CRM-ACCOUNT-LOOKUP 1.1 içinde ayrı uç U-4; U-1/U-3'e adres eklenmez |
| K-13 | İzin anahtarı | **KAPANDI** (Beste, 2026-11-13): `crm.account-ship-to.read` (§32.3) |
| K-14 | Aktif olmayan hesaba sevkiyat | **TÜKETİCİ KARARI** (WMS, 2026-11-13): yalnız `active` seçilir; sonradan pasifleşende sevkiyat sürer, kayıt ve "sevke hazır"da yeniden okuma + uyarı. CRM'den yalnız `status` |
| K-15 | Görünen ad | **ÖNERİ**: tüketici Reference Data'dan (`country`, `city`, `district`) çözer; U-4 kod döner |
| K-16 | Ana adrese posta kodu | **KAPANDI** (Beste, 2026-11-13): eklenecek — DL-4 (§32.10) |
| K-17 | Ülke / şehir / ilçe kodlarının yazmada doğrulanmaması | **AÇIK → DL-4**: yeni ve değişen adreslerde Reference Data doğrulaması DL-4'te; U-4 saklananı döner |
| K-18 | Çoklu teslimat adresi | **KAPANDI** (Beste, 2026-11-13): yapılacak — DL-4; ekran mockup'sız, hesap detayının mevcut sekme yapısında standart liste + form |
| K-9 | MVP-6 adres yüzeyi (sözleşme 1.0 §11) | DL-3 kabulü + 1.1 donmasıyla kapanır; MVP-6'nın U-4'e geçişi kendi işi |
| K-19 | Yeni anahtarı alan CRM rolleri (inceleme F-2) | **KAPANDI** (CT önerisi, sahip kaydında): CRM Admin alır; öteki CRM rolleri (Manager, Sales, Marketing, Viewer) almaz (§16) |

Kayıt: K-12…K-19 ve WMS isteği / K-14 `docs/records/decisions/2026-11/mod0149-ship-to-owner-decisions-01.md` (durum: taslak —
sahip okuması CT-APR-01 onayıyla birlikte kayda geçer).

### 32.9 Teslim sırası

1. Bu ekin CT-APR-02 incelemesi + CT-APR-01 onayı.
2. DL-3 (U-4) + DL-3b (manifest anahtarı) — CRM senkron işi (S0) kabul edildikten sonra, test dalı tabanında.
3. Sözleşme 1.1 donması (DL-3 CT K13 kabulünde).
4. DL-4 (§32.10) ayrı ek + inceleme + kod.

### 32.10 Sonraki dilim — DL-4 teslimat adresleri (bu ekte AC yok)

Ayrı ek olarak yazılır ve ayrıca onaylanır. Kapsam taslağı (bağlayıcı değil):

- Hesaba ek teslimat adresleri: ad, ülke / şehir / ilçe kodu, posta kodu, adres satırı, varsayılan, pasif; silme yalnız
  soft delete. Tek varsayılan kuralı. Hesabın ana adresine `PostalCode` (K-16).
- Yazmada `country`, `city`, `district` Reference Data doğrulaması (K-17), yeni ve değişen adreslerde.
- Hesap detayında yeni "Teslimat adresleri" sekmesi (Work Center sekme kartı, standart liste + form; mockup yok);
  hesap formunda posta kodu; kiracı modülü 7 dil.
- U-4: liste ek adreslerle büyür (`kind = "delivery"`, `addressId` UUID); biçim ve sürüm değişmez.
- Yazma komutları merkezi denetim işaretini taşır (`ICentralAuditDeclaration`); yazma izni ayrı karar (öneri
  `crm.account.update`).

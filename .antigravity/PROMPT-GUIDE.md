# Prompt Guide

> ⚠️ **Bu dosya prompt kataloğudur — kullanım rehberi değildir.**
> Yeni başlayanlar için akış anlatımı ve "hangi agent ne zaman" rehberi `docs/agent-usage-guide.md`'dedir. Bu dosya yalnız kopyalanabilir prompt örnekleri ve anti-pattern'leri tutar.

Bu dosya, ERP-vNext icin guncel prompt katalogudur. Amac, agent secimini, module pack akisini, Slim/Compact kararini ve dogrulama beklentilerini tek bir yerde netlestirmektir.

> Otorite sirasi: Module Pack > Domain Config > AGENTS.md > `.antigravity/` standartlari.

---

## Temel Ilkeler

- Yeni modul veya buyuk feature kodu module pack olmadan yazilmaz.
- Module pack hazirlama isi `module-pack-author` veya `/prepare-module-pack` ile baslar.
- `@orchestrator` module pack yazmaz; yalnizca `approved` veya `ready-for-dev` durumundaki module pack uzerinden gelistirme yonetir.
- `draft` durumundaki module pack kullanici incelemesi icindir; bu durumda kod yazilmaz.
- DataTable modullerinde golden referans secimi create/edit form alan sayisina gore yapilir.
- `8 ve alti` kullanici alani: `GoldenReferenceSlim`, create/edit offcanvas.
- `8'den fazla` kullanici alani: `GoldenReferenceCompact`, full `Create/Edit/Details` sayfalari.
- Frontend istekleri servis portlarina dogrudan gitmez; her zaman Gateway `5000` uzerinden gider.
- Prompt; kapsam, degistirilmeyecekler, kabul kriterleri ve dogrulama beklentisi icermelidir.
- Eski `Products`, `SampleModule`, `Diten.MdmService` ve hardcoded `5050` ornekleri aktif referans degildir.

---

## Ne Zaman Hangi Giris Noktasi

| Ihtiyac | Onerilen giris noktasi | Not |
|---|---|---|
| Yeni module pack hazirlama | `module-pack-author` veya `/prepare-module-pack` | Kod yazmaz, status `draft` birakir |
| Onayli module pack ile yeni modul gelistirme | `@orchestrator` + `/add-module` | Pack `approved` veya `ready-for-dev` olmali |
| Cok modullu / cross-cutting yetenek hazirlama | `/prepare-capability-pack` | Kod yazmaz; Delivery Capability Pack'i `draft` birakir (CAP-001). Tek modul yeterliyse `/prepare-module-pack`'e doner |
| Mevcut CRUD/DataTable duzeltmesi | `@orchestrator` | UI, JS, backend, gateway ve test etkisi birlikte yonetilir |
| Sadece frontend partial/DataTable isi | `frontend-ui-ux` | Dar kapsamliysa dogrudan agent kullanilabilir |
| Sadece backend CQRS endpoint | `backend-architect` veya `/add-endpoint-cqrs` | Command/query/handler ayrimi korunur |
| Sadece gateway route | `integration-agent` | Ocelot etkisi dar ise dogrudan agent uygundur |
| Sadece l10n | `l10n-agent` | `.resx`, `_IndexL10n.cshtml`, `index.l10n.js` kontrol edilir |
| Test ve kalite kapisi | `testing-agent` | Build, verifier, RESX ve smoke test beklentisi yazilir |
| Son kullanici dokumani | `user-manual-generator` | Kod etkisi yoksa dogrudan agent uygundur |
| Genel review / inceleme (edit'e izin verilebilir) | `@orchestrator` veya ilgili uzman agent | Duzenlemeye izin veren genel review normal goreve-ozel rotayi izleyebilir; kod yazma istenmiyorsa acikca belirtilir |
| Audit-only / no-change review (kod yazma/edit yok) | `/read-only-audit` -> `read-only-auditor` | worktree-read-only veya strict repository-read-only; repoyu degistirmez, baseline'dan sapmaz |

---

## Prompt Yazma Kurallari

Her iyi prompt asagidakileri net yazar:

1. Giris noktasi:
   - `@[.antigravity/agents/module-pack-author.md]`
   - `@[.antigravity/agents/orchestrator.md]`
   - veya dogrudan uzman agent
2. Gorev tipi:
   - module pack, yeni modul, endpoint, fix, migration, audit, test, dokumantasyon
3. Kapsam:
   - hangi domain, module pack, servis, frontend area veya gateway yolu degisebilir
4. Degistirilmeyecekler:
   - protected path, baska domain, archive, layout, gateway gibi sinirlar
5. Kabul kriterleri:
   - test edilebilir, davranis odakli maddeler
6. Dogrulama:
   - build, verifier, RESX, xUnit, browser smoke veya audit beklentisi
7. Ilgili standartlar:
   - workflow, rule, golden reference, module pack

---

## Iki Asamali Yeni Modul Akisi

### 1. Module pack hazirlat

```text
@[.antigravity/agents/module-pack-author.md]

Legal Entity icin module pack hazirla.
Kod yazma.

Beklenti:
- AGENTS.md, domain-config.md, execution/portfolio/master-development-plan.md ve execution/registries/module-id-registry.md dosyalarını oku.
- Domain'i belirle.
- Owned objects, repo scope, protected paths, acceptance criteria ve test expectations yaz.
- Create/edit form kullanici alan sayisini cikar.
- Golden referans kararini yaz: GoldenReferenceSlim veya GoldenReferenceCompact.
- Module pack status degerini draft birak.
```

Alternatif workflow kullanimi:

```text
/prepare-module-pack Legal Entity
Kod yazma. Module pack'i draft olarak hazirla.
```

### 2. Module pack incelet

```text
Bu module pack'i kontrol et.

Kontrol edilecekler:
- Domain secimi dogru mu?
- Repo scope yeterli ve sinirli mi?
- Protected paths eksiksiz mi?
- Acceptance criteria test edilebilir mi?
- Test expectations build, verifier, RESX ve smoke beklentilerini kapsiyor mu?
- form_field_count ve golden_reference karari dogru mu?

Kod yazma, sadece eksik veya riskleri raporla.
```

### 3. Kullanici onayindan sonra gelistirme baslat

```text
@[.antigravity/agents/orchestrator.md]

Legal Entity module pack approved/ready-for-dev durumunda.
Bu module pack'e gore gelistirmeyi baslat.

Zorunlu okuma sirasi:
1) AGENTS.md
2) execution/domains/{domain}/module-packs/{ID}.md
3) execution/delivery/platform-delivery-board.md
4) gerekiyorsa execution/portfolio/master-development-plan.md
5) .antigravity/workflows/add-module.md

Kabul kriteri:
- Module pack'teki acceptance criteria tamamlanacak.
- Golden referans karari birebir uygulanacak.
- Build, verifier ve l10n kontrolleri raporlanacak.
```

### 4. Draft pack ile kod yazma denemesinde beklenen davranis

```text
@[.antigravity/agents/orchestrator.md]

Bu draft module pack icin gelistirme baslatma.
Once pack'in eksiklerini raporla ve kullanici onayi gerektigini belirt.
Kod yazma.
```

---

## Slim / Compact Karar Promptlari

Alan sayimi sadece create/edit formundaki kullanici alanlari icindir.

Sayilanlar:
- Kullanici tarafindan girilen create/edit form alanlari.

Sayilmayanlar:
- `Id`
- `TenantId`
- `IsDeleted`
- `CreatedAt`, `UpdatedAt`, `DeletedAt`
- audit alanlari
- DataTable checkbox/action kolonlari

### Slim DataTable promptu

```text
Legal Entity create/edit formunda 7 kullanici alani var.
GoldenReferenceSlim standardini kullan.

Frontend beklentisi:
- Index.cshtml
- _Filter.cshtml
- _DataTable.cshtml
- _IndexL10n.cshtml
- _CreateEditOffcanvas.cshtml
- LegalEntityIndex.cs
- index.l10n.js
- index.js

Create/Edit offcanvas olacak.
Full Create/Edit/Details sayfalari ekleme.
```

### Compact DataTable promptu

```text
Customer Account create/edit formunda 12 kullanici alani var.
GoldenReferenceCompact standardini kullan.

Frontend beklentisi:
- Index.cshtml
- _Filter.cshtml
- _DataTable.cshtml
- _IndexL10n.cshtml
- Create.cshtml
- Edit.cshtml
- Details.cshtml
- _Form.cshtml
- CustomerAccountIndex.cs
- index.l10n.js
- index.js

Index icinde create/edit offcanvas kullanma.
```

---

## Agent Bazli Prompt Ornekleri

### module-pack-author

```text
@[.antigravity/agents/module-pack-author.md]

Vendor Profile icin module pack olustur veya mevcutsa guncelle.
Kod, backend, frontend veya gateway dosyasi degistirme.

Beklenti:
- Domain'i AGENTS.md ve domain-config.md uzerinden belirle.
- Alan sayisini cikar.
- `form_field_count` ve `golden_reference` alanlarini yaz.
- Repo scope, protected paths, acceptance criteria ve test expectations ekle.
- Status `draft` olsun.
```

### orchestrator

```text
@[.antigravity/agents/orchestrator.md]

execution/domains/{domain}/module-packs/{ID}.md dosyasindaki approved module pack'e gore gelistirme yap.

Zorunlu:
- Module pack yoksa dur ve kullaniciya once `/prepare-module-pack` kullanmasini soyle.
- Module pack draft ise dur ve onay gerektigini soyle.
- Module pack approved/ready-for-dev ise gelistirmeyi baslat.
- Backend, frontend, gateway, l10n ve test islerini ayni module pack'e gore koordine et.
```

### backend-architect

```text
@[.antigravity/agents/backend-architect.md]

Legal Entity backend CQRS yapisini module pack'e gore uygula.

Zorunlu klasor ayrimi (Golden Reference birebir):
- Commands/
- Queries/
- Handlers/CommandHandlers/
- Handlers/QueryHandlers/
- Validators/
- {Module}Models.cs              (TEK dosyada tum DTO/ViewModel'ler)

Naming (Golden Reference):
- Command: {Verb}{Module}Command (sealed record)
- Query:   Get{Module}{Qualifier}Query (sealed record)
- Handler: {Verb}{Module}Handler (sealed class, Command/Query suffix YOK)
- Validator: {Verb}{Module}Validator (Command suffix YOK)

Kurallar:
- Her command, query ve handler ayri dosyada olacak.
- Bir dosyada birden fazla public class/record YASAK.
- Controller ince kalacak ve MediatR'a gonderecek.
- Response<T> envelope ve CustomBaseController kullanilacak.
- TenantId server-side cozulur; DTO veya form payload icinde olmaz.
- Soft delete zorunludur.
```

### frontend-ui-ux

```text
@[.antigravity/agents/frontend-ui-ux.md]

Legal Entity frontend DataTable yapisini module pack'teki golden_reference kararina gore uygula.

Ortak partial yapisi:
- Index.cshtml
- _Filter.cshtml
- _DataTable.cshtml
- _IndexL10n.cshtml
- {ModuleName}Index.cs
- index.l10n.js
- index.js

Slim ise:
- _CreateEditOffcanvas.cshtml ekle.
- Create/Edit offcanvas kullan.

Compact ise:
- Create.cshtml, Edit.cshtml, Details.cshtml, _Form.cshtml ekle.
- Index icinde create/edit offcanvas kullanma.
```

### integration-agent

```text
@[.antigravity/agents/integration-agent.md]

Legal Entity icin gateway route ekle.

Kurallar:
- Frontend sadece Gateway 5000 uzerinden cagiracak.
- Servis portunu AGENTS.md ve domain-config.md uzerinden belirle.
- Hardcoded eski 5050 referansi kullanma.
- Ocelot route degisikligini module pack repo scope disina tasirma.
```

### l10n-agent

```text
@[.antigravity/agents/l10n-agent.md]

Legal Entity localization yapisini tamamla.

Beklenti:
- Çoklu dil (Platform: 2 dil, Tenant: 7 dil) `.resx`.
- View resource ve shared resource ayrimini koru.
- Index icinde uzun `window.L10n.Key = ...` bloklari yazma.
- `_IndexL10n.cshtml` JSON payload uretsin.
- `index.l10n.js` bridge standardini uygula.
```

### testing-agent

```text
@[.antigravity/agents/testing-agent.md]

Legal Entity module pack icin test ve kalite kontrollerini calistir.

Beklenti:
- Backend build
- Frontend build
- Gateway build
- RESX checker
- DataTable verifier
- 5001 frontend ve 5000 gateway uzerinden smoke test

Sonucta gecen/kalan kontrolleri kisa raporla.
```

### user-manual-generator

```text
@[.antigravity/agents/user-manual-generator.md]

Legal Entity modulu icin son kullanici kilavuzu hazirla.

Beklenti:
- Listeleme, filtreleme, kolon gorunurlugu ve Save View davranisini acikla.
- Slim ise create/edit offcanvas akisini anlat.
- Compact ise Create, Edit ve Details sayfalarini anlat.
- Teknik implementasyon detayi yazma.
```

---

## Workflow Prompt Ornekleri

### prepare-module-pack

```text
/prepare-module-pack Legal Entity

Kod yazma.
Module pack'i draft olarak hazirla.
Alan sayisini ve GoldenReferenceSlim/GoldenReferenceCompact kararini yaz.
```

### add-module

```text
/add-module execution/domains/{domain}/module-packs/{ID}.md

Bu workflow module pack hazirlama workflow'u degildir.
Sadece approved/ready-for-dev module pack uzerinden gelistirme yap.
```

### quality-gate-datatable

```text
/quality-gate-datatable Legal Entity --reference slim

Kontrol:
- DataTable v2 marker
- inline filter
- skeleton loader
- Save View
- _CreateEditOffcanvas.cshtml
```

```text
/quality-gate-datatable Customer Account --reference compact

Kontrol:
- DataTable v2 marker
- inline filter
- skeleton loader
- Create.cshtml
- Edit.cshtml
- Details.cshtml
- _Form.cshtml
- Index icinde create/edit offcanvas olmamasi
```

### add-endpoint-cqrs

```text
/add-endpoint-cqrs LegalEntity

Yeni endpoint: POST /api/legal-entities/bulk-activate
Validation:
- id listesi bos olamaz
- her id tenant scope icinde olmali

Beklenti:
- Command, validator ve handler ayri dosyalarda olsun.
- Controller sadece MediatR'a gondersin.
- Response<T> envelope kullanilsin.
```

### add-mongo-collection

```text
/add-mongo-collection LegalEntities

Beklenti:
- TenantId zorunlu.
- IsDeleted ve DeletedAt soft delete alanlari zorunlu.
- Tenant scoped unique index ihtiyacini module pack'e gore degerlendir.
```

### migrate-datatable-v2

```text
/migrate-datatable-v2 Vendor Profile --reference slim

Beklenti:
- DataTable v2 marker kullan.
- Inline filter standardina gec.
- `_DataTable.cshtml` ve `_Filter.cshtml` partial yapisini uygula.
- stateSave:false ve personalizationClient Save View akisini koru.
```

---

## DataTable Frontend Standart Promptu

```text
Bu DataTable modulunde frontend partial standardini uygula.

Ortak zorunlu dosyalar:
- Index.cshtml
- _Filter.cshtml
- _DataTable.cshtml
- _IndexL10n.cshtml
- {ModuleName}Index.cs
- index.l10n.js
- index.js

_Filter.cshtml:
- inline collapsible filter kullanir.
- offcanvas filter kullanmaz.

_DataTable.cshtml:
- `data-dt-standard="v2"` marker icerir.
- skeleton loader icerir.
- checkbox ve action kolonlarini icerir.

_IndexL10n.cshtml:
- JSON payload uretir.
- Index icinde uzun `window.L10n.Key = ...` bloklari yazilmaz.
```

---

## Backend CQRS Standart Promptu

```text
Backend CQRS implementasyonunda Golden Reference birebir yapi:

Folder:
- Commands/
- Queries/
- Handlers/CommandHandlers/
- Handlers/QueryHandlers/
- Validators/
- {Module}Models.cs              (TEK dosyada tum DTO/ViewModel)

Naming (Golden Reference):
- Command:   {Verb}{Module}Command         (sealed record)
- Query:     Get{Module}{Qualifier}Query   (sealed record)
- Handler:   {Verb}{Module}Handler         (sealed class, Command/Query/Request SUFFIX YOK)
- Validator: {Verb}{Module}Validator       (Command SUFFIX YOK)

Kurallar:
- Her command, query, handler ve validator ayri dosyada.
- Bir dosyada birden fazla public class/record YASAK.
- Controller ince kalir.
- TenantId server-side cozulur.
- Soft delete uygulanir.
- Repository karari golden reference ve mevcut servis standardina gore teklesir.
- Referans kod: services/Diten.DevEnablementService/.../Features/GoldenReferenceSlim/
```

---

## Dogrulama Komutlari

### Golden Reference verifier

```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceSlim --reference slim
```

```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area DevEnablement --module GoldenReferenceCompact --reference compact
```

### Module verifier

```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area {AreaName} --module {ModuleName} --reference slim
```

```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area {AreaName} --module {ModuleName} --reference compact
```

### Build

```bash
dotnet build services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Diten.DevEnablementService.Api.csproj -c Debug
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug
```

### RESX

```bash
python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py .
```

---

## Anti-Pattern'ler

### Module pack olmadan yeni modul baslatmak

Yanlis:

```text
@[.antigravity/agents/orchestrator.md]

Legal Entity modulunu sifirdan gelistir.
```

Dogru:

```text
@[.antigravity/agents/module-pack-author.md]

Legal Entity icin once module pack hazirla. Kod yazma.
```

### Draft module pack ile kod yazdirmak

Yanlis:

```text
@[.antigravity/agents/orchestrator.md]

Draft Legal Entity pack'e gore gelistirmeyi baslat.
```

Dogru:

```text
Bu draft module pack'i incele.
Eksik acceptance criteria, repo scope, protected path veya test expectation var mi raporla.
Kod yazma.
```

### Orchestrator'a module pack yazdirmak

Yanlis:

```text
@[.antigravity/agents/orchestrator.md]

Legal Entity module pack'i olustur ve gelistirmeye basla.
```

Dogru:

```text
@[.antigravity/agents/module-pack-author.md]

Legal Entity module pack'i draft olarak hazirla. Kod yazma.
```

### Golden referansi belirsiz birakmak

Yanlis:

```text
Legal Entity'i golden reference gibi yap.
```

Dogru:

```text
Legal Entity create/edit formunda 7 kullanici alani var.
GoldenReferenceSlim kullan.
Create/Edit offcanvas olacak.
```

### Field count kuralini yanlis saymak

Yanlis:

```text
Id, TenantId, CreatedAt ve action kolonlari dahil 11 alan var; Compact kullan.
```

Dogru:

```text
Create/edit formunda kullanicinin girdigi 7 alan var.
Id, TenantId, audit alanlari ve action kolonlari sayilmaz.
GoldenReferenceSlim kullan.
```

### Frontend'den servis portuna dogrudan gitmek

Yanlis:

```text
Frontend isteklerini 5058 DevEnablement servisine gonder.
```

Dogru:

```text
Frontend istekleri Gateway 5000 uzerinden gidecek.
Servis portu sadece gateway route icinde kullanilir.
```

### DataTable v2 marker atlamak

Yanlis:

```html
<table class="datatables-legal-entities table border-top">
```

Dogru:

```html
<table id="dt-legal-entities" data-dt-standard="v2" class="datatables-legal-entities table border-top">
```

### stateSave'i acik birakmak

Yanlis:

```js
dt = new DataTable(dtTableEl, window.DtDefaults.create({
    ajax: { url: listUrl }
}));
```

Dogru:

```js
dt = new DataTable(dtTableEl, window.DtDefaults.create({
    stateSave: false,
    ajax: { url: listUrl }
}));
```

### L10n bridge'i Index icine gommek

Yanlis:

```cshtml
<script>
window.L10n.Save = '@Localizer["Save"]';
window.L10n.Cancel = '@Localizer["Cancel"]';
</script>
```

Dogru:

```text
_IndexL10n.cshtml JSON payload uretir.
index.l10n.js bu payload'i JS tarafina tasir.
```

---

## Legal Entity Ornek Akis

### 1. Hazirlik

```text
@[.antigravity/agents/module-pack-author.md]

Legal Entity icin module pack hazirla.
Kod yazma.
Domain'i belirle, alan sayisini cikar, Slim/Compact kararini yaz ve status draft birak.
```

### 2. Inceleme

```text
Legal Entity module pack'i incele.
Eksik acceptance criteria, repo scope, protected path, test expectation veya golden_reference karari var mi raporla.
Kod yazma.
```

### 3. Onay sonrasi gelistirme

```text
@[.antigravity/agents/orchestrator.md]

Legal Entity module pack approved/ready-for-dev durumunda.
Bu pack'e gore gelistirmeyi baslat.
Backend, frontend, gateway, l10n ve test islerini koordine et.
```

### 4. Dogrulama

```text
Legal Entity icin build, RESX checker ve DataTable verifier calistir.
Slim ise `--reference slim`, Compact ise `--reference compact` kullan.
Sonuclari kisa raporla.
```

---

## Son Kontrol Listesi

- Module pack yoksa yeni modul kodu yazilmiyor mu?
- Draft module pack kullanici onayi icin bekliyor mu?
- Orchestrator yalnizca approved/ready-for-dev pack ile gelistiriyor mu?
- `form_field_count` ve `golden_reference` yazili mi?
- Slim icin `_CreateEditOffcanvas.cshtml` var mi?
- Compact icin `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml` var mi?
- `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml` standardi korunuyor mu?
- Backend CQRS command/query/handler ayrimi korunuyor mu?
- Frontend Gateway 5000 disina cikmiyor mu?
- Verifier `--reference slim|compact` ile calisiyor mu?


---

## Module ID Canonicalization Gate (DCP-002)

The Blueprint (`docs/System Capability & Implementation Blueprint - master 5.xlsx` :: `Blueprint_Data`) is the canonical authority for every `MOD-xxxx` ID and canonical name. Before creating or reserving any `MOD-xxxx` (new module, FU/child, or reservation):

1. **Blueprint lookup** — the ID + canonical name must exist in `Blueprint_Data`, or the ID must be an FU/child of an existing Blueprint MOD parent.
2. **Registry collision** — it must not already map to a different capability in `execution/registries/module-id-registry.md`.
3. **Canonical-name validation** — the pack `name` must match the Blueprint canonical name (or an approved alias).
4. **Parent/FU/child decision** — decide explicitly whether the work is a new module or an FU/child of an existing module before minting an ID.
5. **Repo-only reservation** — a capability absent from the Blueprint requires an explicit Enterprise Architect reservation recorded in the registry; no placeholder or next-free ID may be invented.
6. **Preflight (fail-closed)** — run `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-XXXX --name "Canonical Name" [--parent MOD-YYYY] [--repo-only]`. A non-zero exit BLOCKS pack creation.

Authority and policy: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`. Legacy (`PSS-*`, `NEW-*`) and repo-only IDs are valid only as deprecated aliases pending Enterprise Architect reservation.

### CAND-CAP candidate namespace (DCP-002)

When the Blueprint has no capability and no existing MOD/FU fits, use a temporary candidate identity `CAND-CAP-####` — a governance/documentation identity ONLY, never written into runtime literals. Validate with the fail-closed candidate gate:

`python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-#### --name "Capability Name"`

Lifecycle: `legacy ID → deprecated alias to CAND-CAP-#### → later deprecated alias to the EA-assigned canonical MOD-xxxx`. New-module identity rule: **Blueprint lookup → existing MOD or FU when available → otherwise CAND-CAP only → never invent a MOD / PSS / NEW identity.**

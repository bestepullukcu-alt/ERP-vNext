---
id: MOD-0193
name: BOM & Routings
domain: supply-chain-execution
service: Diten.ManufacturingService
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
status: ready-for-dev
owner: supply-chain-execution (MVP-3 lane) / control-tower
branch: claude/bold-bell-52tgsz
started: 2026-10-06
target: 2026-10-20
form_field_count: 12
approved_by: "product owner via CT (session 2026-10-06: 'MVP-3 BOM & Routings … tamamla'; service decision 'Ayrı servis')"
approved_on: 2026-10-06
---

# MOD-0193 — BOM & Routings

> **Status guard.** `ready-for-dev` — DCP-009 `approved` (CT 2026-09-11) + bu pack `ready-for-dev` (ürün sahibi, 2026-10-06: "MVP-3 … tamamla", servis kararı "Ayrı servis") → **kod yetkili yalnız MOD-0193 için** (CAP-001 §7). DCP-009'un diğer üyeleri için `runtime_code_allowed` değişmez.
> **Lane guard.** Bu pack MVP-3'tür. MVP-1 (0173–0177), MVP-2 (0140–0145), MVP-4 (0188–0192), MVP-5 (0178–0182), MVP-6 (0183–0187, 0147/0148) dosyalarına **dokunmaz** (ürün sahibi, 2026-10-06: "sadece mvp3 yapılacak diğer mvp'lere dokunma").
> **Contract authority.** API yüzeyi `docs/analysis/contracts/bom.openapi.yaml`'dan türetilir (x-owner MOD-0193, FROZEN v1). Üç frozen operasyonun (`getCurrentBom`, `getBomVersion`, `explodeBom`) şekli değişmez; yönetim uçları **additive** (v1.1.0) eklenir. Uydurma yasak (K12).
> **Identity guard.** Yeni `MOD-xxxx` basılmaz. `verify_module_id.py . --check-id MOD-0193 --name "BOM & Routings"` → **exit 0** (2026-10-06, kanıt §19).

## 1. Module Summary

Ürün reçetesi (Bill of Materials) + üretim rotası (Routing) SoR'u. Bir **ana ürün (MOD-0290 item)** için hangi bileşenin
(hammadde/API/excipient/ara ürün — hepsi MOD-0290 item) **ne kadar** kullanıldığını ve hangi operasyon adımlarıyla
üretildiğini, **sürümlü ve değişiklik kontrollü** tutar. Pharma/GxP + Manufacturing (DEC-INV-11): reçete bir GxP
kaydıdır — her yürürlüğe alma bir değişiklik kontrolü referansı taşır ve geçmişi silinemez.

Hedef kullanıcı: üretim / formülasyon mühendisi (taslak yazar), kalite / değişiklik kontrol sorumlusu (yürürlüğe alır),
MVP-4 MRP (MOD-0189) ve MOD-0194 İş Emirleri (okur, patlatır). Blueprint W-4 (Manufacturing Execution), Min
Integration Contract = BOM (frozen). Rapor §14.7 / §23: **kimlik MOD-0290'da, miktar/formülasyon burada.**

## 2. Ownership and Boundaries

- **In-scope:** BOM sürümü (başlık + bileşen satırları + rota adımları) · yaşam döngüsü Draft → Effective → Superseded ·
  item başına tek yürürlükteki sürüm · tarih bazlı geçerlilik (`asOfDate`) · tek seviye patlatma (MRP ihtiyacı) ·
  döngü (cycle) koruması · BOM sürüm geçmişi (değiştirilemez, AUD-001 yol c).
- **Out-of-scope:** ürün/item kimliği, UoM tanımı, `materialType` (MOD-0290/MDM — consume edilir, kopyası açılmaz) ·
  stok/balance/ledger (MOD-0173 — BOM **stok tutmaz, stoğa yazmaz**) · iş emri / tüketim hareketi `GOODS_ISSUE_PROD`
  (MOD-0194) · değişiklik kontrolü iş akışının kendisi (MOD-0209 — BOM yalnız referansını taşır) · iş merkezi (work
  center) master'ı (MOD-0192/0194 — BOM yalnız kodunu taşır) · çok seviyeli patlatma ve MRP ağı (MOD-0189).
- **Owned contract:** `BOM` (`bom.openapi.yaml`, x-owner MOD-0193). v1.0.0 FROZEN yüzey korunur; v1.1.0 additive.

## 3. Owned Objects

- **Entity:** `BomVersion` (EntityBase) + gömülü `BomComponentLine[]`, `RoutingStep[]` · `BomHistoryEntry` (yalnız ekleme;
  yol c izi).
- **Commands:** `CreateBomDraftCommand`, `UpdateBomDraftCommand`, `ReleaseBomVersionCommand`, `DeleteBomDraftCommand`.
- **Queries:** `GetBomListQuery`, `GetBomVersionQuery`, `GetCurrentBomQuery`, `ExplodeBomQuery`, `GetBomHistoryQuery`.
- **API (frozen v1.0.0):** `GET /api/bom/{itemId}/current?asOfDate=` · `GET /api/bom/version/{bomVersionId}` · `POST /api/bom/explode`.
- **API (additive v1.1.0):** `GET /api/bom/versions` (sayfalı liste) · `POST /api/bom/versions` (taslak) ·
  `PUT /api/bom/version/{bomVersionId}` (taslak düzenle) · `POST /api/bom/version/{bomVersionId}/release` ·
  `DELETE /api/bom/version/{bomVersionId}` (yalnız taslak, soft) · `GET /api/bom/version/{bomVersionId}/history`.
- **Frontend route:** `/Manufacturing/Boms` (tenant shell).
- **Permissions:** `manufacturing.bom.read|create|update|release|delete`.

## 4. Entity Fields

`BomVersion`:

| Field | Type | Rules | Index |
|---|---|---|---|
| Id (= `bomVersionId`) | Guid | server-assigned | PK |
| ItemId | Guid | required; ana ürün, MOD-0290 item (fail-closed `UNKNOWN_ITEM`) | (Tenant, LE, ItemId, RevisionNo) **unique** |
| RevisionNo (= contract `version`) | int | server-assigned: item başına max+1 (silinmişler dahil, tekrar kullanılmaz) | ↑ |
| Description | string? | ≤ 200 | — |
| Status | enum Draft/Effective/Superseded | server; contract enum | (Tenant, LE, ItemId, Status) |
| EffectiveFrom / EffectiveTo | DateTimeOffset? | server; release anında `now`; önceki sürümün `EffectiveTo` = yenisinin `EffectiveFrom` | — |
| ChangeControlRef | string? | release'de **zorunlu** (MOD-0209 referansı), ≤ 64 | — |
| Components[] | BomComponentLine | ≥ 1 | — |
| Components[].ComponentItemId | Guid | required; MOD-0290 item; ana ürünle aynı olamaz; aynı satırda tekrar edemez (aynı position) | — |
| Components[].Quantity | Decimal string | `^\d+(\.\d+)?$`, > 0, ≤ 18 ondalık hane; **float YASAK**; 1 birim ana ürün başına | — |
| Components[].UomId | string | required, ≤ 32 (MOD-0290 UoM kodu; bu dilimde serbest metin — ASSUMPTION-BOM-03) | — |
| Components[].Position | int | 1..9999, sürüm içinde unique | — |
| Components[].Alternates | string[]? | ≤ 10, her biri MOD-0290 item id | — |
| Routing | { RoutingId, Steps[] }? | opsiyonel; `RoutingId` server-assigned | — |
| Routing.Steps[].StepNo | int | 1..9999, unique | — |
| Routing.Steps[].Operation | string | required, ≤ 120 | — |
| Routing.Steps[].WorkCenter | string? | ≤ 32 (iş merkezi kodu, master değil) | — |
| CreatedBy / UpdatedBy / ReleasedBy | Guid | server (`sub`) | — |
| TenantId, LegalEntityId | Guid | **server-resolved, payload'da YOK**; her sorguda filtre | compound |
| Version (concurrency), IsDeleted, DeletedAt, CreatedAt, UpdatedAt | EntityBase | soft-delete zorunlu | — |

`BomHistoryEntry` (yalnız ekleme): `Id, TenantId, LegalEntityId, BomVersionId, ItemId, RevisionNo, Operation
(Created/Updated/Released/Superseded/Deleted), FromStatus, ToStatus, ChangedFields[], ChangeControlRef, ActorId,
ActorDisplayName, CorrelationId, OccurredAtUtc, Outcome`. Index: (Tenant, LE, BomVersionId, OccurredAtUtc).

> `form_field_count: 12` > 8 → **compact**. Sayım (kullanıcının doldurduğu modül alanları): başlık **itemId,
> description** (2) + bileşen satırı **componentItemId, quantity, uomId, position, alternates** (5) + rota adımı
> **stepNo, operation, workCenter** (3) + release formu **changeControlRef, onay kutusu** (2) = **12**. Liste filtresi
> ve `asOfDate` sorgusu sayılmaz.

## 5. Repo Scope

- `services/Diten.ManufacturingService/**` (yeni servis — 5 katman + tests)
- `frontend/Diten.Web/Controllers/ManufacturingBomsController.cs`, `Models/Manufacturing/Boms/**`,
  `Views/Manufacturing/Boms/**`, `wwwroot/assets/js/Manufacturing/Boms/**`, `Resources/Views/Manufacturing/Boms/**`
- `frontend/Diten.Web.Tests/**` içinde yalnız BOM'a ait yeni test dosyaları
- Paylaşılan dosyalarda **yalnız kendi satırı (ekleme):** `gateway/Diten.ApiGateway/ocelot.json` (`/api/bom` çifti →
  5067), `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs` (`KnownDownstreamPorts` += 5067),
  `frontend/Diten.Web/Resources/SharedResource.{7 dil}.resx` (`Nav.Module.BOMROUTINGS`, `Nav.Page.BOMS`),
  `tests/architecture/**` (yeni servisin denetim defteri + kabul edilen iz + kiracı sitesi), `AGENTS.md` §3 port satırı.
- Governance: bu pack, `module-id-registry.md` MOD-0193 satırı, DCP-009 §MVP-3 notu, domain-config servis notu,
  `bom.openapi.yaml` (additive v1.1.0).

## 6. Protected Paths

- `.antigravity/**`
- **Diğer MVP'ler:** `services/Diten.SupplyChainService/**` (MVP-6, yalnız PR #134'te), `services/Diten.ProcurementService/**`
  (MVP-2), MVP-6 module pack'leri, `docs/roadmap/plans/mvp6-*`, `feature/mvp6-logistics` dalı.
- Diğer domain servisleri (`services/Diten.MdmService/**`, `Diten.Platform/**`, `Diten.AuthService/**`, …).
- `docs/analysis/contracts/product-master-bundle.openapi.yaml`, `inventory-bundle.openapi.yaml` (CONSUMED — redefine YASAK).
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`.

## 7. Dependencies (SOP §16.1 dependency gate)

| bağımlılık | rol | durum (ölçüldü 2026-10-06) | karar |
|---|---|---|---|
| MOD-0290 Product/Item/SKU Master | item kimliği (ana ürün + bileşen) | `canonical`; PRODUCT-MASTER-BUNDLE FROZEN; MdmService çalışıyor ama `/api/product-master/validate` canlı değil | **SATISFIED (contract)** — `IProductReferenceValidator` seam'i; varsayılan `Permissive`, `ProductMaster:Mode=Http` ile `POST /validate` (frozen) |
| MOD-0209 Change Control | yürürlüğe almada değişiklik referansı | registry'de **yok**, kod **yok** | **WAIVED (W-0193-01)** — `IChangeControlGate` seam'i: biçim doğrulaması (zorunlu, ≤ 64); gerçek 0209 gelince seam değişir, BOM kodu değişmez |
| MOD-0003 Data Contract Registry | contract sürüm kaydı | `planned / missing` | **WAIVED (W-0193-02)** — contract dosyası repo'da pinli, her yanıt `contractVersion: v1` taşır |
| MOD-0040 Canonical ID & Correlation (Blueprint) | korelasyon kimliği | standart; ayrı runtime yok | **SATISFIED (convention)** — `X-Correlation-Id` (yoksa sunucu üretir), her hata gövdesinde `correlationId` |
| MOD-0173 Inventory (MVP-1) | — | — | **Bağımlılık yok**: BOM stoğa yazmaz, okumaz ("after MVP-1" yalnız takvim sırası; G1 = contract freeze, karşılandı) |

Waiver standardı (SOP §16.2): sahibi = MVP-3 lane CT · gerekçe = backbone modül repo'da yok, contract-first ilerleme ·
kapanış tetiği = MOD-0209 / MOD-0003 module pack'i `ready-for-dev` · risk = değişiklik referansı doğrulanmadan saklanır
(yalnız biçim) · geri dönüş = seam implementasyonunu değiştirmek.

## 8. Runtime Constraints

- Servis **`Diten.ManufacturingService`**, port **5067** (5066 MVP-6 SupplyChain'de; 5067 hiçbir servis config'inde yok —
  ölçüldü). Manufacturing Execution (0193–0197) için ayrı bounded context (ürün sahibi kararı 2026-10-06). Frontend
  yalnız Gateway (5000) üzerinden.
- Tenant + Legal-Entity: JWT `tenant_id` / `legal_entity_id` (header eşleşmezse 400; eksikse 400); her sorguda
  `TenantId` **ve** `LegalEntityId` **ve** `IsDeleted=false`. Cross-tenant/LE → 404 `UNKNOWN_BOM`.
- Mongo: GUID V3 subtype-4 (`GuidRepresentation.Standard`); **replica set zorunlu** (release + geçmiş tek işlem).
- **Yanıt biçimi istisnası (AGENTS §6, gerekçeli):** bu modül `Response<T>` zarfı **kullanmaz**. Sebep: sahibi olduğu
  `bom.openapi.yaml` FROZEN v1 ve tüketicisi MVP-4 MRP — başarı gövdesi doğrudan `BomView`, hata gövdesi
  `{ error: { code, message, correlationId }, contractVersion }`. Zarf eklemek frozen şekli bozar. Additive uçlar da aynı
  biçimi kullanır (tek servis içinde iki biçim yok).
- Decimal = string; float YASAK. Patlatma çarpımı `decimal` ile, ölçek = max(girdi ölçekleri), değer kaybı varsa tam ölçek.
- Effective/Superseded sürüm **değiştirilemez ve silinemez**; düzeltme yeni taslak + yeni release ile.

## 9. Layout & Shell Contract

`shell: tenant` → `Views/Manufacturing/Boms/*.cshtml` içinde `Layout = "_LayoutTenantShell"` **açıkça**. Route `/Manufacturing/Boms`.
Yetkisiz kullanıcı → yalnız `_AccessDenied` (UAS-001), yönlendirme yok.

## 10. Backend File Convention

Procurement/DevEnablement kalıbı: `Application/Features/Boms/{Commands,Queries,Handlers/CommandHandlers,Handlers/QueryHandlers,Validators}/`
+ `BomModels.cs`; Handler/Validator adları suffix'siz (`ReleaseBomVersionHandler`). Domain `Entities/`, `Repositories/`;
Persistence Mongo repository + index initializer; Infrastructure tenant middleware + `HasPermission`; Api controller +
`ModuleRegistration`.

## 11. Frontend File Contract

Compact: `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`), `_IndexL10n.cshtml`,
`Create.cshtml`, `Edit.cshtml`, `Details.cshtml` (bileşenler + rota + **sürüm geçmişi** + release), `_Form.cshtml`
(başlık + bileşen grid + rota grid), `BomsIndex.cs`, `index.js`, `index.l10n.js`, `form.js`, `details.js`.
L10n: tenant modülü → **7 dil**.

## 12. Validation Rules

| Field | Rule | Hata |
|---|---|---|
| itemId | uuid, ≠ Guid.Empty, MOD-0290'da var | 400 `INVALID_REQUEST` / 422 `UNKNOWN_ITEM` |
| components | 1..500 satır | 400 |
| componentItemId | uuid, ≠ itemId, MOD-0290'da var | 400 / 422 `UNKNOWN_ITEM` / 422 `SELF_REFERENCE` |
| quantity | `^\d+(\.\d+)?$`, > 0 | 400 |
| uomId | dolu, ≤ 32 | 400 |
| position | 1..9999, sürüm içinde unique | 400 |
| alternates | ≤ 10, uuid, ≠ itemId | 400 |
| routing.steps.stepNo | 1..9999 unique | 400 |
| routing.steps.operation | dolu, ≤ 120 | 400 |
| description | ≤ 200 | 400 |
| changeControlRef (release) | dolu, ≤ 64, `IChangeControlGate` kabul | 400 / 422 `CHANGE_CONTROL_REJECTED` |
| expectedVersion (update/release/delete) | concurrency token = `Version` | 409 `CONCURRENCY_CONFLICT` |
| explode.quantity | `^\d+(\.\d+)?$`, > 0 | 400 |

## 13. Failure Path to Verify

- **Bilinmeyen item / bileşen** → 422 `UNKNOWN_ITEM`, kayıt yok, geçmiş yok.
- **Kendine referans** (bileşen = ana ürün) → 422 `SELF_REFERENCE`.
- **Döngü** (A→B yürürlükte, B için A içeren sürüm release) → 409 `BOM_CYCLE`, durum değişmez.
- **Effective/Superseded sürümü düzenleme/silme** → 409 `BOM_NOT_DRAFT`.
- **Eşzamanlılık** (eski `expectedVersion`) → 409 `CONCURRENCY_CONFLICT`.
- **İki kullanıcı aynı anda release** → tam biri başarılı; item başına tek `Effective` (unique kısmi index).
- **Geçmiş yazılamazsa** (işlem hatası) → komut başarısız 503 `PERSISTENCE_UNAVAILABLE`, BOM da yazılmaz (K2 fail-closed).
- **Yetkisiz aktör** → 403 (UI: iskelet yok, `_AccessDenied`).
- **Cross-tenant / cross-LE** okuma/yazma → 404 `UNKNOWN_BOM`; header ≠ JWT → 400.
- **Yürürlükte BOM yok** (`current`, `explode`) → 404 `UNKNOWN_BOM`.

## 14. Authorization Convention

`[Authorize]` + `[HasPermission("manufacturing.bom.{action}")]`, actions `read` (liste, sürüm, current, explode, geçmiş),
`create`, `update`, `release`, `delete`. Actor: tenant user. Her uygulanan anahtar manifestte bildirilir (sayfa
`RequiredPermission` ya da action `PermissionKey`) — bildirilmeyen anahtar Auth'a ulaşmaz (MVP-6 recipe 6.4 dersi).

## 15. Gateway / API Routing Decision

Yeni contract ailesi → `ocelot.json` çifti `/api/bom` + `/api/bom/{everything}` → `localhost:5067`, tüm metodlar.
Paylaşılan dosyaya yalnız bu iki rota eklenir (integration-agent rolü bu lane CT'si). `KnownDownstreamPorts` += 5067.

## 16. Acceptance Criteria

1. `POST /api/bom/versions` geçerli gövdeyle 201 + `BomView` (`status: Draft`, `version` = item başına sıradaki no).
2. `PUT /api/bom/version/{id}` taslakta 200; yürürlükteki sürümde 409 `BOM_NOT_DRAFT`.
3. `POST …/release` (`changeControlRef` dolu) 200: sürüm `Effective`, `effectiveFrom` = sunucu zamanı; aynı item'ın önceki
   `Effective` sürümü **aynı işlemde** `Superseded` + `effectiveTo` = yeni `effectiveFrom`.
4. `GET /api/bom/{itemId}/current` yürürlükteki sürümü döner; `asOfDate` verilince o günün sonundaki sürümü döner;
   yoksa 404 `UNKNOWN_BOM` (frozen Error şekli).
5. `GET /api/bom/version/{id}` frozen `BomView` alanlarını birebir döner (`bomVersionId, itemId, version, status,
   effectiveFrom, effectiveTo, components[{componentItemId, quantity, uomId, position, alternates}], routing{routingId,
   steps[{stepNo, operation, workCenter}]}, contractVersion: "v1"`).
6. `POST /api/bom/explode` `{itemId, quantity:"100.000"}` → `requirements[{componentItemId, requiredQuantity, uomId}]`,
   `requiredQuantity` = quantity × bileşen miktarı (decimal, string), aynı bileşen+uom birleştirilir, `bomVersionId`, `contractVersion`.
7. Döngü / kendine referans / bilinmeyen item yolları §13'teki kodlarla reddedilir ve hiçbir kayıt yazmaz.
8. Her yazma komutu aynı işlemde bir `BomHistoryEntry` yazar; `GET …/history` bunu kiracı kullanıcısına (izin `read`)
   döner; Details ekranı geçmişi gösterir (AUD-001 yol c koşul 3).
9. Başka kiracı / LE'nin BOM'u hiçbir uçta görünmez (404), listede çıkmaz.
10. Web: `/Manufacturing/Boms` liste (server-side, filtre item/status), Create/Edit (bileşen + rota grid), Details
    (release, sil, geçmiş); `Layout = "_LayoutTenantShell"`; 7 dil `.resx` eşit anahtar; yetkisiz → `_AccessDenied`.
11. Gateway: `/api/bom/**` → 5067; `OcelotConfigurationTests` 5067'yi tanır.
12. Mimari testler (denetim defteri, kiracı-çelişki sitesi, JWT clock skew, Mongo test DB deseni) yeşil.

## 17. Test Expectations

- `services/Diten.ManufacturingService/tests/Diten.ManufacturingService.Tests`: domain (patlatma aritmetiği, döngü,
  geçerlilik), handler (fake repository / validator), **Mongo entegrasyon** (gerçek replica set: release atomikliği,
  supersede, unique Effective, concurrency, kiracı + LE izolasyonu, geçmiş aynı işlemde), HTTP (WebApplicationFactory:
  frozen şekil, hata zarfı, 401/403/404, header çelişkisi).
- Sabotaj kanıtı: izolasyon filtresi ve geçmiş yazımı kaldırılınca ilgili test kırmızı.
- `dotnet build` 0 hata; `tests/architecture` yeşil; `gateway/Diten.ApiGateway.Tests` 5067 ihlali yok;
  `frontend/Diten.Web.Tests` (NavManifest L10n guard dahil) yeşil; 7 resx anahtar paritesi.

## 18. Ready-for-dev Checklist

- [x] Golden Reference compact okundu (Procurement Requisitions/GRN satırlı form)
- [x] Frontmatter tam (service, shell, golden_reference, data_mode, entity_base)
- [x] Layout & Shell açık (`_LayoutTenantShell`)
- [x] Backend File Convention tanımlı
- [x] Frontend File Contract compact listesi tam
- [x] Validation Rules her alan için
- [x] Failure Path ≥ 4 (duplicate/cycle, missing, unauthorized, concurrency)
- [x] Authorization Convention (anahtarlar + manifest bildirimi)
- [x] Gateway kararı (yeni çift, 5067)
- [x] Lookup kararı: platform lookup yok; item seçimi MOD-0290 kimliği (uuid) ile — MDM picker FU (F-0193-02)
- [x] AC test edilebilir
- [x] Test expectations build/test/arch/resx kapsıyor
- [x] Audited Events: her yazma komutu tam bir kez (§21)
- [x] DCP-002 exit 0 (§19)
- [x] Dependency gate: SATISFIED / WAIVED kayıtlı (§7)

## 19. Implementation Notes

- **DCP-002 kanıtı (2026-10-06):** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0193 --name "BOM & Routings"` → `OK  MOD-0193: proven against Blueprint/registry.` exit 0.
- **ASSUMPTION-BOM-01 (yürürlük anı):** release anında yürürlüğe girer (`effectiveFrom = now`); ileri tarihli yürürlük
  yok (F-0193-01). Geriye tarihleme GxP gereği yasak.
- **ASSUMPTION-BOM-02 (patlatma derinliği):** `explode` tek seviye (frozen örnekle birebir: 100 × 2.000 = 200.000);
  çok seviyeli ağ MRP'nindir (MOD-0189). Döngü koruması ise çok seviyeli yapılır (yürürlükteki sürümler üzerinden DFS).
- **ASSUMPTION-BOM-03 (UoM):** bileşen `uomId` MOD-0290 UoM koduna karşı doğrulanmaz (frozen PRODUCT-MASTER `validate`
  UoM taşımıyor); serbest kod ≤ 32. F-0193-03.
- **ASSUMPTION-BOM-04 (taban miktar):** bileşen miktarı 1 birim ana ürün içindir (frozen şemada `baseQuantity` yok).
- **ASSUMPTION-BOM-05 (asOfDate):** `asOfDate` = o UTC gününün sonu (23:59:59.9999999Z); verilmezse şimdi.
- **Servis ayrımı:** DCP-009 tek servis (`Diten.SupplyChainService`) öngörüyordu; o servis yalnız MVP-6 dalında (PR #134,
  birleşmemiş) var. MVP-6'ya dokunmamak için ürün sahibi 2026-10-06'da ayrı servis seçti → DCP-009 §MVP-3 notu.

## 20. Follow-up Items

- F-0193-01 ileri tarihli yürürlük (planlı reçete değişimi) — `effectiveFrom` girdisi + "Scheduled" yorumu (contract additive).
- F-0193-02 item seçici: MOD-0290 ürün arama (MDM lookup) — bugün uuid girişi.
- F-0193-03 UoM doğrulaması PRODUCT-MASTER `getSkuUom` / UoM master ile.
- F-0193-04 MOD-0209 gerçek değişiklik kontrolü entegrasyonu (W-0193-01 kapanışı); e-imza (21 CFR Part 11) release'de.
- F-0193-05 Merkezi denetim günlüğüne iletim — K4 ortak iletici geldiğinde (BL-501) yol b eklenir; yol c kalır.
- F-0193-06 Routing'in iş merkezi master'ına (MOD-0192/0194) bağlanması; süre/kapasite alanları.
- F-0193-07 Gerçek `ProductMaster:Mode=Http` entegrasyonu MOD-0290 `validate` canlı olunca (bugün permissive).

## 21. Audited Events (Denetlenen Olaylar) — AUD-001

Sınıf: **GxP kaydı (K2)** — reçete/formülasyon. Kural §4.3: kayıt yazılamazsa işlem yok. Yol **c — eşdeğer iz**
`bom-surum-gecmisi`: `BomHistoryEntry`, BOM yazımıyla **aynı Mongo işleminde** `IBomHistoryJournal.CommitAsync` ile
yazılır; depo arayüzünde güncelleme/silme yok; kaydın kendi ekranında (Details → Geçmiş, izin `manufacturing.bom.read`)
okunur. Merkezi günlük ("bu kiracıda dün kim ne yaptı") bu izle cevaplanmaz — F-0193-05.

| komut | olay | yol |
|---|---|---|
| CreateBomDraftCommand | `Created` | c (`IBomHistoryJournal.CommitAsync`) |
| UpdateBomDraftCommand | `Updated` (değişen alan adları) | c |
| ReleaseBomVersionCommand | `Released` + önceki sürüm için `Superseded` | c |
| DeleteBomDraftCommand | `Deleted` | c |

Sorgular (liste, sürüm, current, explode, geçmiş) yazmaz; `explode` POST olsa da kalıcı kayıt değiştirmez (sorgu türü).

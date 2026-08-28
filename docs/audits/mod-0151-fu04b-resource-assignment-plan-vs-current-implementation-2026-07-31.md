# MOD-0151 FU04B — Resource Assignment Plan vs Current Visibility · Implementation

Tarih: 2026-07-31
Target tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
Module: `MOD-0151 — Territory Management`
Runtime scope: `FU04B-resource-assignment-plan-vs-current-visibility`

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Root `AGENTS.md`, Commercial Suite domain config, MOD-0151 module pack §7.5a / §17 / §18 / §19 / §22.4 okundu | ✔ |
| FU04B pack authorization | **PASS** — `docs/audits/mod-0151-fu04b-resource-assignment-plan-vs-current-pack-authorization-2026-07-31.md` |
| FU04A implementation kaynağı | `docs/audits/mod-0151-fu04a-...-implementation-2026-07-30.md` (PARTIAL; ana davranışlar mevcut) |
| Mevcut kod okundu | `TerritoryResourceAssignment`, `ActivateTerritoryModelHandler`, `GetCurrentTerritoryResourceResponsibilitiesHandler`, replacement/transfer provenance alanları, `TerritoryActivationUnitOfWork`, `TerritoryResourceAssignmentRepository`, `TerritoryContractDto`, `TerritoryModelsController`, `TerritoryResourcesController`, Details UI + JS + RESX, FU04/FU04A testleri |
| Fleet durumu | Gateway 5000 / Auth 5056 / Platform 5057 / CrmService 5061 **200**; Web 5001 ayakta |
| Başlangıç git durumu | Çalışma ağacı önceden kirliydi (CRM'in tamamı untracked); yalnız FU04B kapsamına dokunuldu |

---

## 2. Implementation Summary

FU04B, FU04A'nın ürettiği veriyi **okuyan** additive bir katmandır. Tek yazma yolu, mevcut activation lifecycle'ının
içine yerleştirilen immutable plan baseline snapshot'ıdır; onun dışında hiçbir mutasyon yüzeyi eklenmemiştir.

| Katman | Ne eklendi |
|---|---|
| Domain | `TerritoryResourceAssignmentPlanSnapshot` + `…SnapshotLine` (immutable aggregate); `ITerritoryResourceAssignmentPlanSnapshotRepository` (**update/delete/insert üyesi yok**); `ITerritoryActivationUnitOfWork.CommitAsync` snapshot parametresi aldı |
| Application | `PlanVsCurrent` feature (contracts + diff engine + 3 query handler); `TerritoryCurrentResponsibilityPolicy` (tek "current" tanımı); activation handler'ına baseline capture |
| Persistence | `TerritoryResourceAssignmentPlanSnapshotRepository` (read-only); UoW'ya aynı işlem sınırında snapshot insert; class map + 2 index |
| API | 3 read-only endpoint (2 model-scoped, 1 resource-scoped) |
| Contract | 2 yeni flag |
| Frontend | Details sayfasına **Plan vs Current** read-only tab + partial + `plan-vs-current.js` + proxy action + 7 dil RESX (41 anahtar) |
| Gateway | `/api/crm/resources/{everything}` → 5061 rotası (kullanıcı onaylı; §13'te ayrıntı) |
| Tests | `TerritoryPlanVsCurrentFu04BTests` — 29 test |

---

## 3. Snapshot Model

`TerritoryResourceAssignmentPlanSnapshot` (koleksiyon `territory_resource_assignment_plan_snapshots`):

**Header:** `Id` (PlanSnapshotId) · `TenantId` · `TerritoryModelId` · `CapturedAt` · `CapturedBy` ·
`ActivationCorrelationId` · `SnapshotVersion` · EntityBase audit alanları.

**Line:** `TerritoryNodeId` · `TerritoryNodeCode` · `TerritoryNodeName` · `BusinessScopes` · `PositionCode` ·
`PositionTitle` · `PositionType` · `ResourceId` / `ResourceType` · `ResourceDisplayName` · `PlannedEffectiveFrom` ·
`PlannedEffectiveTo` · `IsPrimary` · `SourceAssignmentId`.

**Uygulanan kurallar:**

- Snapshot **yalnız** model activation sırasında yazılır. Ayrı "snapshot al" endpoint'i veya buton **yok**.
- Activation fail-closed olursa snapshot **yazılmaz** — capture, tüm guard'lardan (reference readiness, node varlığı,
  overlap, position policy, conflict) **sonra**, proposed→active flip'inden **önce** yapılır ve aynı UoW'a verilir.
- **Immutable:** repository arayüzünde update/delete/insert üyesi yoktur; testle doğrulandı.
- Re-activation yeni `SnapshotVersion` üretir (`previous + 1`), eskisi **silinmez**.
- Snapshot display copy'dir; SoR `TerritoryResourceAssignment` ve MOD-0288'de kalır. `SourceAssignmentId` canlı
  zincire tek bağlantıdır.
- `LegacyRoleCode` snapshot'a **yazılmaz** — canonical `PositionRef` alanları kullanılır (negatif testle doğrulandı).

**Mongo notları (bilinen tuzaklardan kaçınıldı):**

- Class map eklendi (`TerritoryModelId` → stringGuid; line'da `TerritoryNodeId`/`SourceAssignmentId`). Eksik olsaydı
  Guid'ler binary yazılıp filtreler string serialize edecek ve sorgu **sessizce boş** dönecekti (FU05'te yaşanan hata).
- İki index de **hiçbir DateTimeOffset alanı içermiyor** (`CapturedAt`, `PlannedEffectiveFrom` BSON array olarak
  saklanıyor; bir compound key en fazla bir array alanı taşıyabilir → "cannot index parallel arrays").

---

## 4. Activation Integration

Sıra (`ActivateTerritoryModelHandler`):

1. Draft/inactive model activation başlar.
2. Mevcut FU02B/FU04A guard'ları çalışır: status, expiry, tarih penceresi, lifecycle reference publish, contract
   readiness, en az bir node, overlapping active model, **proposed position policy**, **conflict report**.
3. Herhangi biri başarısızsa `Reject(...)` → controlled 400/409, **snapshot yazılmaz**.
4. Geçilirse proposed kayıtlardan immutable baseline kurulur (`BuildPlanSnapshotAsync`).
5. Model/node/assignment statüleri flip edilir.
6. `_unitOfWork.CommitAsync(model, nodes, assignments, planSnapshot, ct)` — snapshot **aynı transaction/compensation
   sınırında** yazılır.

**FU04A davranışı korundu:** proposed→active geçişi, correlation/reason taşınması, conflict fail-closed ve current
responsibility semantiği değişmedi; hiçbir guard zayıflatılmadı. Snapshot yazımı bir **lifecycle yan etkisidir**,
kullanıcı aksiyonu değildir.

**Transaction:** replica set/mongos'ta native transaction; standalone Mongo'da compensation. Compensation yolunda
snapshot **en son** insert edilir — hata halinde catch bloğu önceki durumu geri yükler ve baseline hiç oluşmaz, yani
immutability bu yolda da bir "compensating delete" gerektirmez.

---

## 5. Plan vs Current Behavior

```text
Plan    = activation anındaki immutable snapshot
Current = effectiveAt tarihindeki FU04A current responsibility sonucu
Diff    = read-time hesaplanan fark (cache yok)
```

**İkinci bir "current" tanımı üretilmedi.** Predicate tek bir yere çıkarıldı —
`TerritoryCurrentResponsibilityPolicy.IsCurrent(assignment, effectiveAt)`:
active status · `ValidFrom <= effectiveAt` · (`ValidTo` null veya `>= effectiveAt`) · not soft-deleted.
FU04A'nın current responsibility handler'ı da aynı semantiği kullanır; davranışı değişmedi.

Model-status kapısı **çağırana** aittir: FU04A active model ister; FU04B bunu yalnız arşiv/inactive
**historical** görünümü için gevşetir ve sonucu `isHistorical=true` ile açıkça işaretler (D-FU04B-6).

Draft/proposed kayıtlar hiçbir `effectiveAt` için current sayılmaz (test edildi).

**State makinesi:**

| State | Koşul | Davranış |
|---|---|---|
| `not-yet-activated` | Model draft, baseline yok | Satır üretilmez; UI "planning preview only" uyarısı |
| `not-captured` | Model aktive edilmiş ama baseline yok (FU04B öncesi aktivasyon) | **404 değil**, 200 + state; UI açık bilgi mesajı |
| `available` | Baseline var | Karşılaştırma üretilir |

---

## 6. Diff Types

10 tip uygulandı. **Öncelik sırası** (pack §22.4, deterministik):

`Replaced` > `TransferredOut` > `TransferredIn` > `AddedAfterActivation` > `EndedAfterActivation` >
`MissingCurrent` > `DateChanged` > `ScopeChanged` > `PositionChanged` > `Unchanged`

Kazanan verdict satırın `diffType`'ı olur; kalan farklar `secondaryDifferences` içinde listelenir (kazanan tekrar
edilmez). Testle doğrulandı: aynı anda date+scope+position farkı olan satır `DateChanged` döner, diğer ikisi
secondary olarak raporlanır.

| Diff type | Uygulanan kural |
|---|---|
| `Unchanged` | Aynı resource/position/node/scope/tarih |
| `Replaced` | Zincirde replacement kenarı var, terminal current ve node değişmemiş |
| `TransferredOut` | Zincirde transfer kenarı var ve terminal başka node'da → planlı slot satırı |
| `TransferredIn` | Aynı zincirin hedef node satırı; `transferFromAssignmentId` ile geriye bağlı |
| `AddedAfterActivation` | Current, hiçbir baseline zincirinin ulaşmadığı kayıt |
| `EndedAfterActivation` | Baseline var, terminal ended/expired, yerine geçen yok |
| `MissingCurrent` | `SourceAssignmentId` canlı zincirde çözülemiyor → **bütünlük sinyali, hata değil** |
| `DateChanged` | `PlannedEffectiveFrom/To` ↔ `ValidFrom/ValidTo` (**`.Date` karşılaştırması** — DateTimeOffset instant-vs-date tuzağı) |
| `ScopeChanged` | Business scope kümesi veya `IsPrimary` farklı |
| `PositionChanged` | Normalize `PositionCode` farklı |

**Slot eşleştirme anahtarı:** `TerritoryNodeId` + normalize `PositionCode` + `BusinessScopes`, zincir takibi için
`SourceAssignmentId`. Zincir yürüyüşü cycle-guard'lıdır. **Projection cache eklenmedi** (D-FU04B-7).

---

## 7. API Summary

Üçü de **read-only**, `crm.territory.model.read` (FU04A fallback deseni) ile korunuyor:

```text
GET /api/crm/territory-models/{modelId}/resource-assignment-plan-snapshot
GET /api/crm/territory-models/{modelId}/resource-assignment-plan-vs-current
GET /api/crm/resources/{resourceId}/resource-assignment-plan-vs-current
```

**Filtreler** (model-scoped): `effectiveAt` · `territoryNodeId` · `businessUnit` · `positionCode` · `resourceId` ·
`diffType`. Resource-scoped'ta `resourceId` route'tan gelir.

**Davranış:** snapshot yoksa 404 değil, açık state; draft modelde `not-yet-activated`; archived modelde read-only
historical; hiçbiri mutation yapmaz; payload'da `TenantId` yok (claim'den okunur); cross-tenant id → 404.

---

## 8. UI Summary

- Territory Model **Details** sayfası iki sekmeye ayrıldı: **Territory Hierarchy** (mevcut viewer, değişmedi) ve
  **Plan vs Current** (yeni, read-only). **Yeni bağımsız sayfa / menü / page descriptor açılmadı.**
- Tab içeriği: açıklayıcı + read-only uyarısı · state notice'ları · baseline meta (captured at/by, version,
  correlation id, effectiveAt) · diff type sayaç rozetleri · inline filtre (change type, node, BU, position,
  effectiveAt) · DataTable v2 grid.
- Grid kolonları: Change Type · Territory Node (transfer varsa `KESAN → SULEYMANPASA` ok gösterimi) ·
  Business Unit · Position · Planned Resource · Current Resource · Effective Date · Reason.
- Provenance responsive child row'da: planned/current pencereleri, position, `replacedAssignmentId`,
  `replacementAssignmentId`, `transferFromAssignmentId`, `transferToAssignmentId`, `changedAt`, `changedBy`,
  `correlationId`, secondary differences, legacy role (varsa, "display only" etiketiyle).
- **Hiçbir aksiyon butonu yok** — create/end/replace/transfer/apply/workflow/evidence/import-export sıfır.
- Tablo ilk sekme aktivasyonunda lazy kurulur (gizli pane'de DataTable kolon genişliği sıfır ölçer).
- `effectiveAt` grid filtresi değil **server-side** filtredir (hangi kaydın current olduğunu değiştirir) — değişince
  yeniden fetch edilir.
- Filtre id'leri (`filterPvc*`, `btnPvcFilterApply/Reset`) hierarchy filtresiyle **çakışmayacak** şekilde ayrı tutuldu.
- 7 dil RESX parity: **228/228 × 7**, XML geçerli (41 yeni anahtar).

---

## 9. Position-Based Validation

- Snapshot, current eşleştirme, diff, filtreler ve UI kolonlarının tamamı `PositionCode` (normalize) ·
  `PositionTitle` · `PositionRef` · `PositionType` üzerinden çalışır.
- `RoleCode` / `LegacyRoleCode` **snapshot key değil, diff key değil, current filter kaynağı değil**.
- `territory-resource-role` FU04B read modelinin kaynağı değildir.
- Legacy değer yalnız `legacyRoleCode` DTO alanında, UI'da "display only" etiketiyle görünür.
- **Negatif testler:** ① snapshot line tipinde "Role" içeren property yok; ② deprecated flat `PositionCode` alanı
  canonical'dan farklı olsa bile diff `Unchanged` kalır ve legacy değer yalnız gösterim alanına düşer.

---

## 10. Contract Summary

Eklenen (`true`):

- `supportsResourceAssignmentPlanSnapshot`
- `supportsResourceAssignmentPlanVsCurrent`

Bozulmayanlar (contract testleriyle doğrulandı): `supportsResourceAssignments`,
`supportsResourceAssignmentLifecycle`, `supportsResourceReplacement`, `supportsResourceTransfer`,
`supportsCurrentResponsibility`, `supportsPositionBasedResourceAssignment`, `supportsAccountAssignmentApply`,
`supportsWorkflowActivation = false`, `supportsApprovalTrace = false`, `evidencePack = false`, `importExport = false`.

---

## 11. Tests

| Paket | Sonuç |
|---|---|
| CRM Application Tests | **372 passed · 0 failed · 5 skipped · toplam 377** (FU04A sonrası taban 348 → +29 FU04B testi) |
| CrmService API build | **PASS** — 0 error, 0 warning |
| Diten.Web build | **PASS** — 0 error, 14 mevcut nullable warning (FU04A ile aynı sayı) |
| `plan-vs-current.js` syntax (`node --check`) | **PASS** |
| RESX parity | **228/228 × 7 dil**, XML geçerli |
| TerritoryManagement DataTable verifier | **73 PASS / 18 FAIL** — FU04A tabanıyla **birebir aynı**; fail'lerin tamamı `index.js` bulk-action şablon borcu, FU04B yüzeyine ait değil ve bu scope'ta değiştirilmedi |

**FU04B test kapsamı (29):** activation snapshot capture · fail-closed'da snapshot yazılmaması · commit hatasında
snapshot kalmaması · re-activation versiyonlama · `SourceAssignmentId` kaydı · position alanları · legacy role
negatif testleri · repository'de update/delete üyesi olmaması · 10 diff type'ın her biri · precedence + secondary
differences · position normalize eşleşmesi · proposed'ın current sayılmaması · 5 filtre · `effectiveAt` etkisi ·
draft/not-captured/archived state'leri · cross-tenant 404 · resource-level view · resourceId zorunluluğu ·
"okuma hiçbir şeyi mutate etmez" · summary sayaçları.

---

## 12. Live Smoke

**ÇALIŞTIRILMADI — kullanıcı kararıyla atlandı.**

Gateway-only authenticated smoke, hedef tenant için oturum/token gerektiriyor; bu koşuda kimlik bilgisi mevcut
değildi. Kullanıcıya üç seçenek sunuldu (tarayıcıda doğrulama / token verme / smoke'u atlayıp PARTIAL kapatma) ve
**"smoke'u atla, PARTIAL kapat"** seçildi. Smoke ayrı bir closeout task'ı olarak açılmalıdır.

Kimlik gerektirmeyen, bu koşuda **fiilen doğrulanan** canlı kontroller:

| Kontrol | Sonuç |
|---|---|
| Gateway `/health`, Auth, Platform, CrmService `/health` | 200 |
| `GET /api/crm/territory-models/{id}/resource-assignment-plan-snapshot` (Gateway) | **401** (route var, fail-closed) |
| `GET /api/crm/territory-models/{id}/resource-assignment-plan-vs-current` (Gateway) | **401** |
| `GET /api/crm/resources/{id}/resource-assignment-plan-vs-current` (Gateway) | rota eklenmeden **404** → eklendikten sonra **401** |
| `GET /api/crm/resources/{id}/territory-responsibilities` (FU04A, Gateway) | rota eklendikten sonra **401** (önce 404'tü) |
| Web `/CRM/TerritoryManagement/Models/{id}/PlanVsCurrent/Json` | **302 → /account/login** (proxy action canlı, auth zorunlu) |
| Çalışan CrmService/Web süreçleri yeni kodu aldı mı | Evet — yeni route'lar canlı süreçte yanıt veriyor |

**Kapatılamayan smoke adımları:** contract flag'lerinin canlı gövdeden okunması · draft plan → activate → snapshot
oluşumu · Unchanged → replacement (`Replaced`) → transfer (`TransferredOut`/`TransferredIn`) zinciri ·
resource-level view · UI tab'ının canlı render'ı · Account/Contact/FU05 değişmediğinin canlı teyidi.

---

## 13. Guard Checks

| Guard | Sonuç |
|---|---|
| Resource assignment create/update/end/replace/transfer davranışı değişti mi? | **No** — bu handler'lara dokunulmadı |
| AccountTerritoryAssignment apply değişti mi? | **No** |
| Account assignment history değişti mi? | **No** |
| Account master mutate edildi mi? | **No** |
| Contact mutate edildi mi? | **No** |
| Workflow / approval / submit / MOD-0023 eklendi mi? | **No** — `supportsWorkflowActivation` hâlâ `false` |
| Evidence pack / import-export / visit-route eklendi mi? | **No** |
| Brand Scope / Product-Brand master eklendi mi? | **No** |
| Hard delete (`DeleteOne`/`DeleteMany`/`Drop`) | **No** — FU04B kodunda hiç yok |
| Mongo hand-edit | **No** |
| RBAC seed/grant değişti mi? | **No** — yeni permission anahtarı bile eklenmedi |
| MOD-0048 publish değişti mi? | **No** |
| Direct 5061 business API çağrısı (frontend) | **No** — tek eşleşmeler dosya başlığı yorumları |
| Payload'da `TenantId` | **No** — claim'den okunuyor |
| Yeni bağımsız ana menü sayfası / page descriptor | **No** — yalnız Details içinde tab |
| Diff projection cache | **No** — read-time hesap |
| `RoleCode` diff/snapshot/current key oldu mu? | **No** — yalnız display-only alan, negatif testli |
| Snapshot mutable mı? | **No** — repository'de update/delete/insert üyesi yok |
| Activation fail olunca snapshot yazıldı mı? | **No** — iki ayrı testle doğrulandı |
| **Gateway route değişti mi?** | **EVET — bilinçli ve kullanıcı onaylı** ↓ |

### Gateway route sapması (açık kayıt)

`gateway/Diten.ApiGateway/ocelot.json` **AGENTS.md §4 protected path**'tir (yalnız `integration-agent` modifiye eder).
Tespit: `/api/crm/resources/**` için **hiç rota yoktu** — bu, FU04B'nin üçüncü endpoint'ini **ve** FU04A'nın hâlihazırda
var olan `territory-responsibilities` endpoint'ini Gateway üzerinden erişilemez bırakıyordu (ikisi de 404).

Kullanıcıya iki seçenek sunuldu (dokunma + follow-up olarak raporla / rotayı ekle) ve **"rotayı sen ekle"** seçildi.
Eklenen tek rota:

```json
{ "UpstreamPathTemplate": "/api/crm/resources/{everything}", "UpstreamHttpMethod": ["GET", "OPTIONS"],
  "DownstreamPathTemplate": "/api/crm/resources/{everything}", "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5061 }] }
```

Yalnız `GET`/`OPTIONS`; JSON geçerliliği doğrulandı (101 rota); doğrulama sonrası her iki endpoint 404 → **401**
(fail-closed) döndü. Bu, `integration-agent`'a geriye dönük bildirilmesi gereken bir sapmadır.

---

## 14. Created / Updated Files

**Backend — yeni:**

- `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/TerritoryResourceAssignmentPlanSnapshot.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/ITerritoryResourceAssignmentPlanSnapshotRepository.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/ResourceAssignments/TerritoryCurrentResponsibilityPolicy.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/PlanVsCurrent/TerritoryPlanVsCurrentContracts.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/PlanVsCurrent/TerritoryPlanVsCurrentEngine.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/PlanVsCurrent/Handlers/TerritoryPlanVsCurrentQueryHandlers.cs`
- `services/Diten.CrmService/src/Diten.CrmService.Persistence/Repositories/TerritoryResourceAssignmentPlanSnapshotRepository.cs`

**Backend — güncellenen:**

- `.../Domain/Repositories/ITerritoryActivationUnitOfWork.cs` (snapshot parametresi)
- `.../Application/Features/Territory/Models/Handlers/TerritoryLifecycleHandlers.cs` (baseline capture)
- `.../Application/Features/Territory/Contract/TerritoryContractDto.cs` (2 flag)
- `.../Persistence/Repositories/TerritoryActivationUnitOfWork.cs` (aynı sınırda snapshot write)
- `.../Persistence/DependencyInjection.cs` (repo kaydı, 2 class map, 2 index)
- `.../Api/Controllers/CRM/TerritoryModelsController.cs` (2 endpoint)
- `.../Api/Controllers/CRM/TerritoryResourcesController.cs` (1 endpoint)

**Frontend — yeni:**

- `frontend/Diten.Web/Views/CRM/TerritoryManagement/_PlanVsCurrent.cshtml`
- `frontend/Diten.Web/wwwroot/assets/js/CRM/TerritoryManagement/plan-vs-current.js`

**Frontend — güncellenen:**

- `frontend/Diten.Web/Views/CRM/TerritoryManagement/Details.cshtml` (tab yapısı + l10n data bloğu + script)
- `frontend/Diten.Web/Controllers/CRM/TerritoryManagementController.cs` (`PlanVsCurrentJson` proxy)
- `frontend/Diten.Web/Models/CRM/TerritoryViewModels.cs` (3 view model)
- `frontend/Diten.Web/Resources/Views/CRM/TerritoryManagement/TerritoryManagementResources.{en,tr,fr,es,ru,zh,ar}.resx` (41 anahtar × 7)

**Gateway:**

- `gateway/Diten.ApiGateway/ocelot.json` (1 rota — §13)

**Tests / evidence:**

- `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Territory/TerritoryPlanVsCurrentFu04BTests.cs` (yeni)
- `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Territory/FakeTerritoryInfrastructure.cs` (fake UoW yeni imza + `FakeTerritoryPlanSnapshotRepo`)
- `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Territory/TerritoryLifecycleTests.cs` · `TerritoryResourceAssignmentFu04ATests.cs` (ctor argümanı)
- `docs/audits/mod-0151-fu04b-resource-assignment-plan-vs-current-implementation-2026-07-31.md`

---

## 15. Final Verdict

### **PARTIAL — FU04B kod/test/UI PASS; canlı smoke kullanıcı kararıyla atlandı**

PASS tarafı:

- Snapshot activation sırasında oluşuyor, fail-closed'da oluşmuyor, commit hatasında kalmıyor.
- Snapshot immutable (repository'de update/delete üyesi yok, re-activation versiyonluyor).
- Üç read-only endpoint çalışıyor, fail-closed, 404 yerine açık state döndürüyor.
- 10 diff type deterministik; precedence ve secondary differences uygulandı ve test edildi.
- UI tab/section var, aksiyon butonu yok, yeni menü sayfası açılmadı.
- Position-based davranış korunuyor; RoleCode hiçbir yerde key değil (negatif testli).
- 7 dil RESX parity (228/228), contract flag'leri tam, testler 372/377 PASS, build'ler 0 error.
- Verifier FU04A tabanına göre regresyonsuz (73/18).
- Mutasyon, workflow, evidence, import-export, Account/Contact/FU05 guard'larının tamamı korundu.

PARTIAL gerekçeleri:

1. **Gateway-only canlı smoke çalıştırılmadı** (kullanıcı kararı). Diff type'ların canlı zinciri (Unchanged →
   Replaced → TransferredOut/In), UI tab'ının canlı render'ı ve resource-level view canlıda doğrulanmadı; kapsam
   yalnız otomatik testlerle PASS.
2. **`changedBy` alanı boş dönüyor.** FU04A assignment üzerinde aktör kimliği persist etmiyor (Application katmanına
   kullanıcı kimliği taşınmıyor; lifecycle audit'i de `"authenticated-user"` sabitini kullanıyor). Gerçek aktör
   MOD-0021 audit event'inde. UI bunu "atamada kayıtlı değil" olarak açıkça gösteriyor. Düzeltmek FU04A yazma
   davranışını değiştirmeyi gerektirirdi — FU04B'de **yasak**.
3. **`CapturedBy` aynı nedenle sabit** (`authenticated-user`).
4. **Protected path sapması:** `ocelot.json`'a rota eklendi (kullanıcı onaylı, §13). `integration-agent`'a
   bildirilmelidir.
5. FU04A'dan devralınan borçlar açık: Position Directory runtime authority değil; yerel Mongo standalone'da native
   transaction yok (compensation çalışıyor); modül geneli 18 DataTable şablon borcu.

---

## 16. Next Recommended Prompt

```text
MOD-0151 FU05 Live Smoke Closeout
```

Öncesinde/paralelinde açılması önerilenler:

- `MOD-0151 FU04B Live Smoke Closeout` — atlanan canlı zincir (§12).
- `integration-agent` bildirimi — `/api/crm/resources/**` rotasının geriye dönük onayı (§13).

---

## 17. UI Placement / Golden Compact Personalization Addendum — 2026-07-31

Kullanıcı onayıyla FU04B karşılaştırma yüzeyi, yeni bir route veya menü oluşturmadan Territory Model Details'tan
mevcut Resource Assignments sayfasına taşındı.

- Territory Model Details artık yalnız tenant hierarchy viewer'ını render eder; FU04B tab/payload/script içermez.
- Resource Assignments sayfası Assignment Preview ile aynı compact `nav-pills` kart standardında
  `Resource Assignments` + `Plan vs Current` sekmelerini birlikte taşır.
- Plan vs Current DataTable v2 toolbar'ına Export/Action, Column Visibility, inline Filter ve gerçek Save View
  eklendi.
- Save View yalnız shared `personalizationClient` kullanır (`CRM` / `TerritoryResourcePlanVsCurrent`) ve
  `filters + effectiveAt + search + colVis + columnOrder + order` state'ini saklar; localStorage/raw personalization
  fetch yoktur.
- Reset fabrika state'ini bütün olarak geri uygular; search dirty-state baseline'a dönünce Save View yeniden gizlenir.
- Backend query, mutation, Gateway route, RBAC ve yedi-dil RESX anahtar seti değişmedi.

Doğrulama:

- `node --check .../plan-vs-current.js` → PASS.
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug --no-restore -o .tmp-build-mod0151-ui` → PASS,
  0 warning / 0 error (ayrı output, çalışan 5001 apphost kilidinden bağımsız).
- Authenticated browser smoke, güncel build için geçici 5091 instance:
  - model `3618056f-99e5-4950-afdd-58357b790fcf`: hierarchy table var; Plan vs Current tab/payload yok.
  - model `868791c6-3e68-4614-a80d-0743dc7c6f88`: iki compact pill tab render; Plan vs Current toolbar/filter
    render; collapse açılıyor; dirty search Save View'i gösteriyor; baseline'a dönüş gizliyor; raw l10n key yok;
    Plan vs Current akışında yeni console error yok.
- Module-wide DataTable verifier sonucu `73 PASS / 18 FAIL`; 18 fail Territory Management Index'in mevcut
  Compact CRUD/bulk/quick-view şablon borçlarıdır ve bu FU04B tab refactor'ının hedef dosyaları değildir.

### UI refinement

- Snapshot metadata satırı (`captured at/by`, version, correlation ve effective date) yüzeyden kaldırıldı.
- Planned, Current ve Changed özetleri Assignment Preview ile aynı avatar/icon KPI kart düzenine çevrildi.
- Plan vs Current filtre host'u ortak Golden `dt-inline-filter-host` kontratına bağlandı; chip ölçüleri, Select2
  özetleri ve responsive toolbar davranışı artık Slim/Compact referans stillerini kullanır.
- Plan vs Current script'i DOM seviyesinde idempotent hale getirildi; hot reload/yeniden değerlendirme sırasında
  aynı tab event'leri tekrar bağlanmaz ve mevcut `dt-planvscurrent` instance'ı ikinci kez initialise edilmez.
- Responsive renderer ilk çizimde dış `table` değişkeni yerine DataTables'ın verdiği API instance'ını kullanır;
  constructor yarıda kalıp processing göstergesini açık bırakamaz. Geçerli payload sonrasındaki istemci hataları da
  artık yanlış `not-captured` business-state uyarısına çevrilmez.
- Provenance/evidence alanları otomatik responsive child-row'dan çıkarıldı. Golden Slim action kolonundaki Quick
  View gözü, aynı `backbone-preview-offcanvas` düzeninde salt-okunur Plan/Current ve Details bölümlerini açar.

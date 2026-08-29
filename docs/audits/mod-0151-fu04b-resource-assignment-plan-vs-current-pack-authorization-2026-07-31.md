# MOD-0151 FU04B — Resource Assignment Plan vs Current Visibility · Pack Authorization

Tarih: 2026-07-31
Module: `MOD-0151 — Territory Management`
Target file: `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md`
Tür: **governance / pack scope authorization** — kod yazılmadı, runtime değiştirilmedi, smoke çalıştırılmadı.

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| Root `AGENTS.md`, Commercial Suite domain config, `crm-sor-boundary.md` okundu | ✔ |
| MOD-0151 module pack (frontmatter + §7 + §17 + §18 + §19 + §22) okundu | ✔ |
| FU04A pack authorization | **PASS** — `docs/audits/mod-0151-fu04a-pack-runtime-scope-authorization-2026-07-30.md` |
| FU04A implementation | **PARTIAL** — `docs/audits/mod-0151-fu04a-resource-assignment-lifecycle-replacement-operational-visibility-implementation-2026-07-30.md`; ana davranışlar mevcut (position-based lifecycle, proposed→active transition, active create/end, replacement, transfer, current responsibility, history query) |
| Pack status | `ready-for-dev`, `runtime_code_allowed: true` |
| FU04B'nin pack'te önceden tanımlı olup olmadığı | **Tanımlı değildi** — §22 FU breakdown tablosunda FU04A'dan doğrudan FU05'e geçiliyordu; bu task o boşluğu kapatır |
| Otorite sırası | Blueprint Excel (`Blueprint_Data`) > Module Pack > Domain Config > `AGENTS.md` > `.antigravity/rules/` |
| Protected path / RBAC seed / gateway route / `.antigravity` değişikliği | **Yok** |

**Not:** FU04A PARTIAL'dır (Position Directory runtime authority değil, standalone Mongo'da native transaction yok,
FU04A dışı DataTable borçları, conflict/override canlıda tekrarlanmadı). FU04B bu PARTIAL kalemleri **çözmez ve
büyütmez**; onların üstüne **additive read-only** bir katman olarak yetkilendirilmiştir. FU04A'nın PARTIAL olması
FU04B pack authorization'ını bloklamaz, çünkü FU04B'nin bağımlı olduğu davranışların (proposed/active ayrımı,
replacement/transfer provenance, current responsibility query) tamamı çalışır durumdadır.

---

## 2. Business need summary

Draft modelde resource assignment planlanır:

```text
Edirne / Keşan Zone + Alpha Business Unit + Medical Representative Position → Ayşe Hanım
```

Model active olduktan sonra operasyonel değişiklik yapılır:

```text
Ayşe Hanım → Tekirdağ / Süleymanpaşa Zone'a transfer
Keşan Zone → Mehmet Bey Medical Representative olur
```

**Problem:** Activation sırasında `proposed` kayıtlar `active` yapıldığı için **plan bilgisi üzerine yazılır**.
Geriye yalnız history satırları kalır; kullanıcı "başta plan neydi" sorusunu tek ekranda soramaz.

**Kullanıcının görmek istedikleri:** Başta plan neydi · Şu an current ne · Ne değişti · Kim ne zaman değiştirdi ·
Hangi reason ile · Ayşe planned iken nerede current oldu · Mehmet current olarak sonradan mı eklendi.

**Beklenen FU04B çıktısı:**

```text
Keşan Zone       | Planned: Ayşe | Current: Mehmet | ChangeType: Replaced       | reason + changedAt/By
Süleymanpaşa Zone| Planned: —    | Current: Ayşe   | ChangeType: TransferredIn  | transfer link + reason
(Ayşe/Keşan satırı)                                | ChangeType: TransferredOut | transferToAssignmentId ile bağlı
```

FU04B, FU04A üzerine **additive visibility / read-model follow-up**'ıdır. **Workflow approval değildir; resource
assignment mutasyonu değildir.**

---

## 3. Pack frontmatter changes

**Değişiklik:** `runtime_code_scope` alanına additive olarak `FU04B-resource-assignment-plan-vs-current-visibility`
eklendi; **mevcut hiçbir scope silinmedi veya değiştirilmedi.**

Öncesi (8 scope):

```text
FU01-territory-model-node-backend-only; FU02-territory-hierarchy-ui-viewer;
FU02A-country-business-unit-scope-selector-hardening; FU02B-lifecycle-computed-expiry-draft-soft-delete;
FU03-assignment-rules-and-preview; FU04-resource-assignments;
FU04A-resource-assignment-lifecycle-replacement-operational-visibility; FU05-account-assignment-apply-history
```

Sonrası (9 scope):

```text
FU01-territory-model-node-backend-only; FU02-territory-hierarchy-ui-viewer;
FU02A-country-business-unit-scope-selector-hardening; FU02B-lifecycle-computed-expiry-draft-soft-delete;
FU03-assignment-rules-and-preview; FU04-resource-assignments;
FU04A-resource-assignment-lifecycle-replacement-operational-visibility;
FU04B-resource-assignment-plan-vs-current-visibility; FU05-account-assignment-apply-history
```

**Diğer frontmatter alanları değişmedi** (`status`, `runtime_code_allowed`, `dependencies`, `wave`,
`blueprint_bundle`, `sor`, vb.).

### Pack gövdesinde yapılan additive değişiklikler

| # | Yer | Değişiklik |
|---|---|---|
| 1 | Frontmatter satır 13 | `runtime_code_scope` += FU04B |
| 2 | Header READY-FOR-DEV notu (satır 45–59) | Başlık "FU04B scope update 2026-07-31"; yetkili FU listesine FU04B eklendi; FU04B'nin ne açıp ne açmadığı 4 satırda özetlendi |
| 3 | **§7.5a** (yeni) | `TerritoryResourceAssignmentPlanSnapshot` immutable aggregate'i — header + line alanları + 5 kural |
| 4 | §17 Permission Proposal | **FU04B permission kararı** — yeni anahtar önerilmez; `crm.territory.resource.read` (fallback `crm.territory.model.read`); hiçbir `*.manage` talebi yok |
| 5 | §18 UI Surfaces | Satır 11: Plan vs Current **read-only tab/section** (Territory Model Details içinde); yeni bağımsız sayfa/menü olmadığı ve global Resource Change Monitor'ün future follow-up olduğu açıkça yazıldı |
| 6 | §19 API / CQRS Proposal | 3 read-only endpoint satırı eklendi (permission + filtre + notCaptured davranışı ile) |
| 7 | **§22.4** (yeni bölüm) | FU04B'nin tam scope tanımı: senaryo, allowed scope (7 madde), diff type semantiği tablosu, D-FU04B-1…7 policy kararları, position-based zorunluluk, explicit exclusions, acceptance criteria, test expectations, FU boundary |
| 8 | §22 FU breakdown tablosu | FU04A ile FU05 arasına **FU04B** satırı (Scope / Depends On / Out-of-Scope) |

---

## 4. FU04B authorized scope

### 4.1 Plan baseline capture

Yeni **immutable** aggregate `TerritoryResourceAssignmentPlanSnapshot` (pack §7.5a) yetkilendirildi.

**Header:** `PlanSnapshotId` · `TenantId` · `TerritoryModelId` · `CapturedAt` · `CapturedBy` ·
`ActivationCorrelationId` · `SnapshotVersion` · audit metadata.

**Line:** `TerritoryNodeId` · `TerritoryNodeCode` · `TerritoryNodeName` · `BusinessScopes` · `PositionCode` ·
`PositionTitle` · `PositionType` · `ResourceId` / `PersonRef` · `ResourceDisplayName` · `PlannedEffectiveFrom` ·
`PlannedEffectiveTo` · `IsPrimary` · `SourceAssignmentId`.

Bu, **FU04B'nin tek yazma yetkisidir**; ayrı bir "snapshot al" endpoint'i veya kullanıcı aksiyonu açılmamıştır.

### 4.2 Plan vs Current comparison

```text
Plan    = activation anındaki proposed resource assignment snapshot
Current = active modelde effectiveAt tarihindeki current responsibility
Diff    = read-time hesaplanan fark
```

### 4.3 Diff type hesaplama (10 tip, normatif semantik + öncelik sırası)

`Unchanged` · `Replaced` · `TransferredOut` · `TransferredIn` · `AddedAfterActivation` · `EndedAfterActivation` ·
`MissingCurrent` · `DateChanged` · `ScopeChanged` · `PositionChanged`

Pack §22.4'te her tip için koşul yazıldı. Bir satır birden fazla koşulu sağlıyorsa öncelik:
`Replaced` > `TransferredOut/In` > `AddedAfterActivation`/`EndedAfterActivation` > `MissingCurrent` >
`DateChanged` > `ScopeChanged` > `PositionChanged` > `Unchanged`; ikincil farklar satır detayında listelenir.
`MissingCurrent` bir hata değil, **veri bütünlüğü sinyali** olarak tanımlandı.

### 4.4 Read/query endpoints (hepsi read-only)

```text
GET /api/crm/territory-models/{modelId}/resource-assignment-plan-snapshot
GET /api/crm/territory-models/{modelId}/resource-assignment-plan-vs-current
GET /api/crm/resources/{resourceId}/resource-assignment-plan-vs-current
```

**Filtreler:** `effectiveAt` · `territoryNodeId` · `businessUnit` scope · `positionCode` · `resourceId` ·
`diffType`/`changeType`.
**Permission:** `crm.territory.resource.read` (katalog hazır değilse FU04A ile aynı `crm.territory.model.read`
fallback'i). Snapshot yoksa kontrollü boş / `notCaptured` response — 404 değil.

### 4.5 UI visibility

Territory Model **Details** ekranında **read-only tab/section** (pack §18 satır 11). Kolonlar: Planned Resource ·
Current Resource · Change Type · Position · Business Unit · Territory Node · Effective Date · Reason ·
Replacement/Transfer link'leri. Hiçbir aksiyon butonu yok. **Yeni ana menü sayfası açılmadı**; global Resource
Change Monitor **future follow-up** olarak kayda geçti.

### 4.6 Audit / provenance visibility

Mevcut FU04A alanlarının **okunup gösterilmesi** yetkilendirildi: `replacementReason` · `transferReason` ·
`replacedAssignmentId` · `transferFromAssignmentId` · `transferToAssignmentId` · `changedAt` · `changedBy` ·
`correlationId`. FU04B bu alanları **üretmez veya değiştirmez**.

### 4.7 Contract / test / evidence

Contract flag hizalaması (`supportsResourceAssignmentPlanBaseline`, `supportsResourcePlanVsCurrent`), backend/frontend
testleri, 7 dil RESX parity, Compact DataTable v2 verifier, Gateway-only authenticated smoke ve implementation
evidence report yetkilendirildi.

---

## 5. FU04B explicit exclusions

Pack §22.4'e yazıldığı hâliyle, FU04B kapsamında **kesinlikle yasak**:

- Resource assignment **create / update / end / replace / transfer davranışını değiştirmek** (yeni endpoint, değişen
  validation, değişen conflict/override politikası dahil)
- `AccountTerritoryAssignment` apply · account assignment history değiştirmek
- Account master mutasyonu · Contact mutasyonu
- Workflow approval · submit/approve/reject · MOD-0023 entegrasyonu
- Evidence pack (FU07) · import/export (FU08) · visit/route planning implementation (FU09 / MOD-0155)
- Brand Scope · Product/Brand master
- Hard delete · Mongo hand-edit
- RBAC seed/grant (ayrıca yetkilendirilmedikçe) · MOD-0048 publish (ayrıca yetkilendirilmedikçe)
- `crm.territory.delete` · `crm.micro-zone.manage` · request payload'ında `TenantId` · direct port 5061 çağrısı
- **Yeni bağımsız ana menü sayfası**
- Diff projection cache / materialized read model (D-FU04B-7 gereği FU04B'de zorunlu değil ve yetkilendirilmedi)

---

## 6. Plan baseline policy

| # | Karar | İçerik |
|---|---|---|
| **D-FU04B-1** | **Yakalama anı** | `TerritoryModel` activation sırasında, proposed resource assignment'lar `active` yapılmadan **hemen önce**, **aynı lifecycle işlem sınırında**. Activation fail-closed olursa snapshot da yazılmaz (all-or-nothing) |
| **D-FU04B-2** | **Immutability** | **Evet.** Write-once; update/delete yolu yok. Model başına aktivasyon başına bir snapshot; yeniden aktivasyon (inactive → active) yeni `SnapshotVersion` üretir, öncekini silmez |
| **D-FU04B-5** | **Draft modelde görünürlük** | Yalnız **"planning preview"** (proposed listesi, current sütunu boş, açık "not yet activated" uyarısı). Gerçek Plan vs Current **ancak activation snapshot'ından sonra** anlamlıdır |
| **D-FU04B-6** | **Archived modelde görünürlük** | **Evet**, read-only historical comparison; hiçbir aksiyon sunulmaz |

**Ek kurallar (pack §7.5a):** `SourceAssignmentId`, snapshot satırını canlı assignment zincirine (replacement/transfer
provenance dahil) bağlayan tek anahtardır. Snapshot **display kopyasıdır, SoR değildir** — Person/Position master
MOD-0288'e, assignment SoR'u `TerritoryResourceAssignment`'a aittir. `LegacyRoleCode` snapshot'a yazılmaz.

---

## 7. Diff / comparison policy

| # | Karar | İçerik |
|---|---|---|
| **D-FU04B-3** | **Current kaynağı** | **FU04A current responsibility query'si** veya onunla **birebir aynı** deterministic current assignment policy. FU04B paralel/ikinci bir "current" tanımı üretmez — bu, iki farklı doğru cevap riskini yapısal olarak kapatır |
| **D-FU04B-4** | **Runtime mutation** | **Hayır.** Read-only projection/query. Tek istisna D-FU04B-1'deki activation-time baseline yazımıdır ve bu bir kullanıcı aksiyonu değil, lifecycle yan etkisidir |
| **D-FU04B-7** | **Diff saklama** | Plan snapshot **immutable saklanır** · current state **runtime okunur** · diff **read-time hesaplanır**. Projection cache ileride eklenebilir fakat FU04B'de **zorunlu değildir ve yetkilendirilmemiştir** |

**Determinizm şartı (acceptance criteria'ya yazıldı):** aynı girdi (`modelId` + `effectiveAt` + filtre seti) aynı
diff çıktısını üretmelidir.

**Slot eşleştirme anahtarı:** `TerritoryNodeId` + normalize `PositionCode` + `BusinessScopes`, zincir takibi için
`SourceAssignmentId`.

---

## 8. Position-based requirements

FU04B **tamamen Position tabanlıdır** ve bu pack'e normatif olarak kaydedildi:

- Snapshot, current eşleştirmesi, diff hesaplaması, query filtreleri ve UI kolonları yalnız `PositionCode`
  (normalize) · `PositionTitle` · `PositionRef` · `PositionType` alanlarını kullanır.
- **`RoleCode` / `LegacyRoleCode` yeni query, diff veya snapshot kaynağı olamaz** ve snapshot'a yazılmaz.
- Legacy `RoleCode` yalnız migration/backward-compatibility amacıyla, açıkça "legacy" etiketiyle **gösterilebilir**;
  **eşleştirme anahtarı olarak kullanılamaz**.
- Acceptance criteria'ya negatif test şartı eklendi: hiçbir yüzeyin `RoleCode`'u eşleştirme/diff anahtarı olarak
  kullanmadığı test edilir.

Bu, FU04A'nın "canonical kimlik `TerritoryPositionRef` + `PositionCode`, `RoleCode` artık canonical değil" kararının
FU04B'ye taşınmasıdır — pack §7.5 `LegacyRoleCode` ("migration-only, deprecated") tanımıyla tutarlıdır.

---

## 9. UI / read-model notes

- Yüzey **Territory Model Details içinde tab/section**'dır — yeni route, yeni page descriptor, yeni menü `<li>` veya
  MOD-0285 nav kaydı **açılmamıştır**.
- Tab hiçbir create/end/replace/transfer/apply aksiyonu içermez; salt okunur bir karşılaştırma tablosudur.
- Golden Reference **Compact** + DataTable v2 + **Gateway-only** + 7 dil RESX parity kuralları aynen geçerlidir.
- Snapshot yoksa yüzey boş/`notCaptured` durumunu açıkça gösterir (hata değil).
- Draft modelde "planning preview + not yet activated" uyarısı; archived modelde read-only historical comparison.
- **Global Resource Change Monitor** (model-bağımsız, tüm tenant genelinde değişiklik akışı) bilinçli olarak
  **future follow-up** bırakıldı; bu pack'te yetkilendirilmedi.
- FU04B'nin ürettiği plan baseline, ileride **FU06'nın before/after diff'i** için doğal girdi olabilir; bu bağlantı
  FU06 pack authorization'ında ele alınacak, FU04B'de **açılmadı**.

---

## 10. Guard checks

| Guard | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| FU04B scope added? | **Yes** — frontmatter + §7.5a + §17 + §18 + §19 + §22.4 + FU tablosu |
| Existing FU scopes preserved? | **Yes** — FU01, FU02, FU02A, FU02B, FU03, FU04, FU04A, FU05 aynen duruyor; hiçbiri silinmedi/değiştirilmedi |
| Resource assignment mutation opened? | **No** — tek yazma yetkisi activation-time immutable snapshot; mevcut mutation davranışına dokunulmadı |
| Account/Contact mutation opened? | **No** |
| Workflow/evidence/import/export opened? | **No** |
| Visit/route implementation opened? | **No** |
| Brand Scope opened? | **No** |
| Hard delete allowed? | **No** |
| Position-based requirement recorded? | **Yes** — §22.4 "Position-based zorunluluk" + acceptance criteria negatif testi |
| New standalone menu page opened? | **No** — yalnız Model Details içinde read-only tab |
| RBAC seed/grant changed? | **No** — yeni permission anahtarı bile önerilmedi |
| MOD-0048 publish changed? | **No** |
| Gateway route / `ocelot.json` changed? | **No** |
| Protected path / `.antigravity` changed? | **No** |
| Registry updated? | **No** — pack'in yetkisi dışında (mevcut registry drift'i ayrı follow-up) |

---

## 11. Created / updated files

| File | Action | Notes |
|---|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md` | **Updated** | 8 additive değişiklik (§3 tablosu); 1200 → 1396 satır |
| `docs/audits/mod-0151-fu04b-resource-assignment-plan-vs-current-pack-authorization-2026-07-31.md` | **Created** | Bu rapor |

Kod, runtime, reference data, RBAC, Gateway ve Mongo değiştirilmedi. Smoke çalıştırılmadı.

---

## 12. Final verdict

### **PASS**

- FU04B scope **additive** olarak eklendi; mevcut 8 FU scope'unun tamamı korundu.
- **Plan baseline capture** (immutable `TerritoryResourceAssignmentPlanSnapshot`) ve **plan-vs-current comparison**
  (10 diff type + 3 read-only endpoint + filtreler) yetkilendirildi.
- **Position-based requirement** normatif olarak kaydedildi; `RoleCode` eşleştirme/diff kaynağı olmaktan açıkça
  men edildi.
- **Mutasyon kapsamı açılmadı** — tek yazma yetkisi, mevcut activation lifecycle'ı içindeki immutable snapshot
  yazımıdır ve ayrı bir kullanıcı aksiyonu değildir.
- **Yeni ana menü sayfası açılmadı** — yüzey Territory Model Details içinde read-only tab'dır.
- D-FU04B-1…7 policy kararlarının tamamı netleştirildi; acceptance criteria ve test expectations yazıldı.
- **Implementation promptu hazırlanabilir.**

**Bilinçli olarak açık bırakılanlar (FU04B'yi bloklamaz):**

1. Global Resource Change Monitor — future follow-up.
2. Diff projection cache — D-FU04B-7 gereği bu FU'da zorunlu değil, yetkilendirilmedi.
3. `crm.territory.resource.read` katalog/grant hizalaması — FU04A ile aynı `FU04A-RBAC` follow-up'ına bağlıdır;
   FU04B ayrı bir RBAC işi doğurmaz (yeni anahtar önermedi).
4. FU04B baseline ↔ FU06 before/after diff bağlantısı — FU06 pack authorization'ının konusu.

---

## 13. Next recommended prompt

```text
@orchestrator MOD-0151 FU04B — Resource Assignment Plan vs Current Visibility
```

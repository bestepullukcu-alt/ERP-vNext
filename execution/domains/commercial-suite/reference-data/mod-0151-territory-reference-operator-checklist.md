---
module_id: MOD-0151
module_name: Territory Management
domain: commercial-suite
source_pack: MOD-0151 Territory Management
owner: module-pack-author
status: template-only
runtime_code_allowed: false
creates_reference_sets: false
publishes_reference_values: false
audience: MOD-0048 reference-data operator
date: 2026-07-23
---

# MOD-0151 — Territory Reference Set Operator Checklist

> **Bu dosya bir talimat listesidir; hiçbir şey publish etmez.** Operator bu adımları MOD-0048 UI/API üzerinden
> kendisi yürütür. Değer kaynağı:
> [mod-0151-territory-required-reference-authoring-template.json](./mod-0151-territory-required-reference-authoring-template.json) ·
> Açıklamalar: [mod-0151-territory-required-reference-authoring-template.md](./mod-0151-territory-required-reference-authoring-template.md)

---

## 0. Ön kararlar (authoring'e başlamadan)

| # | Karar | Neden kritik |
|---|---|---|
| 0.1 | **Hedef tenant id** belirlendi mi? | Tenant-owned set'ler tenant başına publish edilir |
| 0.2 | **Her set için scope** (`tenant` vs `global`) belirlendi mi? | `global` set'ler `scope_key` **kabul etmez** (`scope_key_not_allowed_for_global`); `tenant` set'ler `scope_key` **zorunlu** kılar (`scope_key_required`). Yanlış scope → tüm consumer çağrıları kırılır |
| 0.3 | v1 önerisi kabul edildi mi: **tüm set'leri tenant-scoped publish et**, `platform-owned` işaretini "tenant bu değerleri serbestçe değiştirmemeli" kuralı olarak uygula? | MOD-0149/0150 precedent'i tenant-scoped'tur; global publish CRM tarafında henüz kanıtlanmadı |
| 0.4 | SoD için **iki farklı kullanıcı** hazır mı (submitter ≠ approver)? | MOD-0149/0150 akışında zorunlu |
| 0.5 | `business-scope-type` gerekli mi? (Tenant `TerritoryBusinessScope` kullanacak mı?) | Kullanılacaksa **required + activation gate** olur |

---

## 1. Authoring adımları (her set için tekrarlanır)

| # | Adım | Beklenen kanıt |
|---|---|---|
| 1 | Set oluştur — payload snake_case: `set_code`, `scope_type`, `status=Active` | 201 · setId |
| 2 | Set için version oluştur | 201 · versionId |
| 3 | Value'ları ekle — snake_case: `code`, `label`, `is_active`, `sort_order`, `metadata` | 200 |
| 4 | Validate | 200 |
| 5 | Submit (**kullanıcı A**) | 200 · GovernanceState=Submitted |
| 6 | **Approve (kullanıcı B ≠ A — SoD)** | 200 · ApprovalState=Approved |
| 7 | Publish | 200 · publishedVersionId dolu |
| 8 | Published-values smoke (§3) | 200 · doğru value sayısı |

> **valueCode = stabil kimlik.** Yayınlanmış bir kodu yeniden adlandırmayın; yeni value ekleyip eskisini
> `isDeprecated=true` yapın.

---

## 2. Publish sırası (bağımlılık nedeniyle önerilen)

1. `territory-coverage-scope` — `territory-resource-role.defaultCoverageScope` bu set'in kodlarına referans verir.
2. `territory-level`
3. `territory-model-status` · `territory-node-status` · `territory-assignment-status` · `territory-assignment-source`
4. `business-scope-type` (kullanılıyorsa)
5. `territory-resource-role`
6. `territory-rule-type` · `territory-conflict-policy`
7. *(opsiyonel)* `planning-period-type` · `territory-change-type` · `product-portfolio` · `brand-group`

---

## 3. Published-values smoke + count doğrulama

**Tenant-scoped set:** `GET /api/v1/reference-data/sets/{setCode}/published-values?scope_key={tenantId}`
**Global set:** `GET /api/v1/reference-data/sets/{setCode}/published-values` *(scope_key **YOK**)*

| # | SetCode | Beklenen value sayısı | Publish edildi? | Smoke 200? | Count doğru? |
|---|---|---|---|---|---|
| 1 | `territory-level` | **6** | ☐ | ☐ | ☐ |
| 2 | `territory-model-status` | **7** | ☐ | ☐ | ☐ |
| 3 | `territory-node-status` | **5** | ☐ | ☐ | ☐ |
| 4 | `territory-assignment-status` | **4** | ☐ | ☐ | ☐ |
| 5 | `territory-assignment-source` | **4** | ☐ | ☐ | ☐ |
| 6 | `territory-resource-role` | **11** | ☐ | ☐ | ☐ |
| 7 | `territory-rule-type` | **9** | ☐ | ☐ | ☐ |
| 8 | `territory-conflict-policy` | **4** | ☐ | ☐ | ☐ |
| 9 | `territory-coverage-scope` | **7** | ☐ | ☐ | ☐ |
| 10 | `business-scope-type` | **7** *(koşullu)* | ☐ | ☐ | ☐ |

**Required toplam: 10 set · 64 value.**

Opsiyonel (bloklamaz): `planning-period-type` **4** · `territory-change-type` **7** ·
`product-portfolio` *(tenant'a özgü)* · `brand-group` *(tenant'a özgü)* · `commercial-role-scope-policy` *(boş)*.

> Consumer okuması `version.Values` üzerinden yapılır (`PublishedSnapshotJson` değil) ve `isDeprecated` client-side
> filtrelenir. Okuma izni: `platform.businessreferencedata.consumer.read`.

---

## 4. Metadata coverage doğrulama (bu adım atlanırsa aktivasyon kırılır)

| # | Kontrol | Set | Beklenen | ☐ |
|---|---|---|---|---|
| 4.1 | `rank` + `sortOrder` **6/6** value'da dolu ve **kesin artan** (10/20/30/40/50/60) | `territory-level` | ✅ | ☐ |
| 4.2 | `requiresTerritoryId` + `requiresBusinessScope` + `allowsTerritoryId` + `allowsBusinessScope` **7/7** value'da dolu | `territory-coverage-scope` | ✅ | ☐ |
| 4.3 | `defaultCoverageScope` + `isSalesRole` + `isManagementRole` + `canBePrimary` **11/11** value'da dolu | `territory-resource-role` | ✅ | ☐ |
| 4.4 | `defaultCoverageScope` değerlerinin **tamamı** `territory-coverage-scope` valueCode'larıyla eşleşiyor | çapraz | ✅ | ☐ |
| 4.5 | `isSalesScopeDefault` + `includeInSalesPerformanceDefault` + `ownerModule` **7/7** value'da dolu | `business-scope-type` | ✅ | ☐ |
| 4.6 | `non-sales-resource-planning` ve `operational-scope` için **her iki bayrak da `false`** (Production Admin kuralı) | `business-scope-type` | ✅ | ☐ |
| 4.7 | `requiresReason` + `canBeOverwrittenByRule` **4/4** value'da dolu; `manual`/`override` → `requiresReason=true`, `canBeOverwrittenByRule=false` | `territory-assignment-source` | ✅ | ☐ |
| 4.8 | `blocksActivation` dolu; `block` ve `manual-review` → `true` | `territory-conflict-policy` | ✅ | ☐ |

---

## 5. Format ve bütünlük kontrolleri

| # | Kontrol | ☐ |
|---|---|---|
| 5.1 | Tüm `setCode` değerleri **lowercase-kebab** | ☐ |
| 5.2 | Tüm `valueCode` değerleri **lowercase-kebab** | ☐ |
| 5.3 | Set içinde **duplicate valueCode yok** | ☐ |
| 5.4 | Tüm value'larda `isDeprecated=false` | ☐ |
| 5.5 | **UPPER_SNAKE alias oluşturulmadı** | ☐ |
| 5.6 | `micro-zone` adında **ayrı set oluşturulmadı** (MicroZone = `territory-level` value'su) | ☐ |
| 5.7 | `territory-model-status` value'ları pack **§22.1 + §13.1** lifecycle birleşimi ile **birebir** (7/7; `inactive` dahil) ve `territory-node-status` (5/5; `archived` dahil) | ☐ |
| 5.8 | Mongo **hand-edit yapılmadı** | ☐ |
| 5.9 | CRM tarafında **local seed / hardcoded fallback oluşturulmadı** | ☐ |

---

## 6. Readiness gate (MOD-0151 FU01 öncesi)

| # | Kapı | ☐ |
|---|---|---|
| 6.1 | §3'teki **10 required set** publish edildi ve smoke 200 döndü | ☐ |
| 6.2 | §4 metadata coverage kontrollerinin **tamamı** PASS | ☐ |
| 6.3 | §5 format kontrollerinin **tamamı** PASS | ☐ |
| 6.4 | Scope kararı (§0.2/0.3) belgelendi; consumer çağrı şekli (`scope_key` var/yok) netleşti | ☐ |
| 6.5 | **Readiness PASS** → MOD-0151 FU01 create smoke açılabilir | ☐ |

> **Required set publish edilmemişken beklenen davranış:** MOD-0151 FU01 create/update **kontrollü 400**,
> FU06 activation **kontrollü 422** döner. Bu bir hata değil, **doğru fail-closed davranıştır**. Crash, sessiz
> varsayılan veya "reference yoksa geç" davranışı görülürse **implementation hatasıdır**.

---

## 7. Yasaklar (operator için)

- ❌ Reference değerlerini CRM servisine seed etmek
- ❌ Kod içine hardcoded fallback listesi koymak
- ❌ MongoDB'yi elle düzenlemek
- ❌ Fake/boş published-values ile ilerlemek
- ❌ Yayınlanmış bir `valueCode`'u yeniden adlandırmak (deprecate + yeni ekle)
- ❌ `territory-model-status` / `territory-node-status` value'su **silmek** veya kod lifecycle'ı dışında value **eklemek** (kod lifecycle'ı ile sözleşme). 2026-07-28 reconciliation'ında eklenen `inactive` + `archived` bu sözleşmenin parçasıdır — bkz. runbook §0
- ❌ `micro-zone` ayrı set'i oluşturmak
- ❌ Publish sonrası set scope'unu (`tenant` ↔ `global`) değiştirmek

---

## 8. Kanıt kaydı (publish sonrası doldurulur)

| Alan | Değer |
|---|---|
| Tarih | — |
| Ortam | — |
| Tenant id | — |
| Scope kararı (tenant / global) | — |
| Submitter (kullanıcı A) | — |
| Approver (kullanıcı B) | — |
| Publish edilen set sayısı | — / 10 required |
| Toplam publish edilen value | — / 64 required |
| Smoke sonucu | — |
| Readiness verdict | PASS / FAIL |

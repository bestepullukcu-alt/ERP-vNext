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
governance: MOD-0048 / PSS-012 (Business Reference Data)
date: 2026-07-23
---

# MOD-0151 — Territory Required Reference Set Authoring Template

> **TEMPLATE-ONLY.** Bu dosya hiçbir reference set **oluşturmaz**, hiçbir value **publish etmez**, hiçbir runtime kod
> içermez. Operator daha sonra bu template'i kullanarak MOD-0048 üzerinden set'leri author/publish eder.
> Makine okunur sürüm: [mod-0151-territory-required-reference-authoring-template.json](./mod-0151-territory-required-reference-authoring-template.json).
> Operator adım listesi: [mod-0151-territory-reference-operator-checklist.md](./mod-0151-territory-reference-operator-checklist.md).

---

## 1. Purpose

MOD-0151 Territory Management pack'inin **§16 Reference Data Proposal** bölümündeki reference set'ler için MOD-0048 /
PSS-012 **authoring template + operator checklist** hazırlamak. Akış MOD-0149 ve MOD-0150 ile birebir aynıdır:

```
pack (§16 öneri) → PREREQ authoring template (bu dosya) → operator authoring/publish → published-values smoke → FU01
```

**CRM local seed yok · hardcoded fallback yok · Mongo hand-edit yok · fake published-values yok.**

---

## 2. Source Authority

| Kaynak | Ne için |
|---|---|
| [MOD-0151 module pack](../module-packs/MOD-0151-territory-management.md) — §4, §8, §9, §10, §16, §20, §23 | Set listesi, metadata ihtiyaçları, activation gate kuralları, kabul edilmiş kararlar |
| [MOD-0151 pack prep audit](../../../../docs/audits/mod-0151-territory-management-pack-prep-2026-07-23.md) | Tasarım gerekçeleri, Blueprint hizalaması |
| [MOD-0150 authoring template](../../../../docs/audits/mod-0150-required-reference-authoring-template.md) + `.json` | **Precedent** — dosya yapısı, operator akışı, SoD kuralı, smoke checklist |
| [MOD-0149 reference readiness](../../../../docs/audits/mod-0149-crm-reference-data-readiness.md) | İlk CRM reference governance akışı |
| [domain-config.md](../domain-config.md) · [crm-sor-boundary.md](../crm-sor-boundary.md) | Reference değerlerinin MOD-0048'e ait olduğu sınır kuralı |
| `AGENTS.md` · `.antigravity/rules/` | Protected path + engineering kuralları |

**Otorite sırası:** Module Pack > bu template > operator tercihi. Pack ile bu template çelişirse **pack kazanır**.

---

## 3. Scope / Out of Scope

**Scope (bu dosya):** required + optional/temporary set listesi · her set için value listesi · metadata şeması ve
önerilen metadata değerleri · runtime gate / activation gate işaretleri · publishing ve validation notları ·
operator checklist.

**Out of scope (kesinlikle yapılmadı):** MOD-0048 runtime kod · reference set publish · reference value seed ·
database write · migration · API/controller · frontend UI · permission seed · MOD-0151 pack güncellemesi ·
registry / module-id-registry update · gateway route · `_LayoutTenantShell` · Product/Brand master tasarımı ·
territory backend / workflow / evidence implementasyonu.

---

## 4. Scope-key kuralı (authoring'den ÖNCE karar verilmeli)

Bu, publish sonrası düzeltilmesi pahalı olan tek karardır:

| Set scope | Publish | Consumer okuması | Hata durumu |
|---|---|---|---|
| `tenant` (tenant-owned) | `scope_type=tenant`, tenant başına | `GET …/published-values?scope_key={tenantId}` | `scope_key` verilmezse **400 `scope_key_required`** |
| `global` (platform-owned) | `scope_type=global`, tek sefer | `GET …/published-values` (**scope_key YOK**) | `scope_key` verilirse **400 `scope_key_not_allowed_for_global`** |

> **Önemli:** MOD-0149 ve MOD-0150 set'lerinin **tamamı tenant-scoped** publish edilmiştir ve CRM consumer'ları
> `scope_key`'i açıkça geçmektedir. Aşağıdaki tabloda `platform-owned` işaretli set'ler *sahiplik* anlamındadır
> (değerler tenant'a göre değişmemelidir). Bu ortamda **global scope publish'i CRM tarafında henüz kanıtlanmamıştır**;
> bu nedenle **v1 önerisi: tüm set'leri tenant-scoped publish et**, `platform-owned` işaretini "tenant bu değerleri
> serbestçe değiştirmemeli" kuralı olarak uygula. Global'e geçiş ayrı bir governance kararıdır
> (bkz. §12 Guardrails ve pack §23).

---

## 5. Required Reference Sets

| SetCode | Required | Owner Type | Runtime Gate | Activation Gate | Notes |
|---|---|---|---|---|---|
| `territory-level` | ✅ | tenant-owned | ✅ | ✅ | **`rank` metadata zorunlu** — hiyerarşi validasyonu buna dayanır |
| `territory-model-status` | ✅ | platform-owned | ✅ | ✅ | Pack **§22.1 + §13.1** lifecycle birleşimi ile birebir eşleşmeli (7 value) |
| `territory-node-status` | ✅ | platform-owned | ✅ | ✅ | FU02B model-driven node lifecycle dahil (5 value) |
| `territory-assignment-status` | ✅ | platform-owned | ✅ | ✅ | Hem account hem resource assignment için |
| `territory-assignment-source` | ✅ | platform-owned | ✅ | ✅ | `requiresReason` + `canBeOverwrittenByRule` metadata |
| `territory-resource-role` | ✅ | tenant-owned | ✅ | ✅ | Rol adları sektöre göre değişir → tenant-owned |
| `territory-rule-type` | ✅ | platform-owned | ✅ | ⚠️ koşullu | Activation yalnız modelde rule varsa bloklar |
| `territory-conflict-policy` | ✅ | platform-owned | ✅ | ⚠️ koşullu | Çözülmemiş conflict varsa bloklar |
| `territory-coverage-scope` | ✅ | platform-owned | ✅ | ✅ | `requiresTerritoryId` metadata **zorunlu** |
| `business-scope-type` | ✅ koşullu | platform-owned | ⚠️ koşullu | ⚠️ koşullu | `TerritoryBusinessScope` kullanılıyorsa **required** |

**Toplam:** 10 set · **64 value**.

> **Aktivasyonu her koşulda bloklayan 6 set:** `territory-level` · `territory-model-status` · `territory-node-status` ·
> `territory-assignment-status` · `territory-assignment-source` · `territory-coverage-scope`.

---

## 6. Required Set Details

### 6.1 `territory-level` (6 value · tenant-owned · **rank metadata zorunlu**)

**Purpose:** MOD-0151 hiyerarşi seviyeleri. Tümü `TerritoryNode` level'ıdır — **MicroZone ayrı aggregate, ayrı
permission veya ayrı reference set DEĞİLDİR** (pack D-hierarchy / §12).

| ValueCode | DisplayName en / tr | rank | sortOrder | isLeafAllowed | canHaveChildren |
|---|---|---|---|---|---|
| `division` | Division / Bölüm | **10** | 10 | false | true |
| `country` | Country / Ülke | **20** | 20 | false | true |
| `region` | Region / Bölge | **30** | 30 | false | true |
| `area` | Area / Alan | **40** | 40 | false | true |
| `zone` | Zone / Bölge (Zone) | **50** | 50 | true | true |
| `microzone` | MicroZone / Mikro Bölge | **60** | 60 | true | false |

**Validation usage (pack §8, §20):**
- **Child rank > parent rank** — `country`(20) altına `zone`(50) **geçerli**.
- **Level atlamak serbest** (`country → zone`). **Geri gitmek yasak** (`zone → region` → 400).
- Her tenant her level'ı kullanmak zorunda değildir; yayınlanan set kullanılabilir seviyelerin **havuzudur**.
- Kod yalnız "rank artmalı" kuralını bilir; **sıra hardcoded değildir**.

**Operator notes:** rank aralıkları 10'ar bırakılmıştır — ileride `sub-region` (35) gibi ara seviye eklemek için.
Yayınlanmış bir value'nun rank'ini **değiştirmek** mevcut hiyerarşileri geçersiz kılabilir → yeni value ekle,
eskisini deprecate et.

---

### 6.2 `territory-model-status` (7 value · platform-owned)

**Purpose:** `TerritoryModel` lifecycle'ı. Pack **§22.1 (FU02B manual lifecycle) VE §13.1 (FU06 approval lifecycle)**
ile birebir eşleşmelidir; eksik/fazla value lifecycle'ı bozar.

| ValueCode | lifecycleOrder | isEditable | requiresApproval | isTerminal | Sahip FU |
|---|---|---|---|---|---|
| `draft` | 10 | ✅ | ❌ | ❌ | FU01/FU02B |
| `review` | 20 | ❌ | ✅ | ❌ | FU06 |
| `approved` | 30 | ❌ | ❌ | ❌ | FU06 |
| `active` | 40 | ❌ | ❌ | ❌ | FU02B |
| **`inactive`** | **45** | ❌ | ❌ | ❌ | **FU02B** |
| `superseded` | 50 | ❌ | ❌ | ✅ | FU06 |
| `archived` | 60 | ❌ | ❌ | ✅ | FU02B |

**Validation usage:** `active` **immutable** (pack §20) — node/rule/assignment mutasyonu 409. `review` içerik kilitli.
`superseded`/`archived` geçmiştir, değiştirilemez. `inactive` FU02B `deactivate` çıktısıdır ve **geri dönülebilir**
(`inactive → active`).

**Operator notes:** Bu set kod lifecycle'ı ile **sözleşmelidir**; value silme veya yeniden adlandırma yapmayın.
`inactive` 2026-07-28 reconciliation'ında eklendi (sortOrder 45, mevcut değerlerin sırası korunarak) — bkz. runbook §0.
**`inactive` ile `superseded` birbirinin yerine kullanılamaz:** `superseded` terminal FU06 ikame durumudur.

---

### 6.3 `territory-node-status` (5 value · platform-owned)

`draft` (editable) · `active` (activeLike) · `inactive` (editable) · `ended` (historical) ·
**`archived`** (historical, sortOrder 50 — 2026-07-28'de eklendi).

**Validation usage:** Soft-delete yalnız `draft` / `inactive` nesnelerde geçerlidir (pack §20). `ended` node'lar
listelerde geçmiş olarak görünür, silinmez. `archived`, model archive edildiğinde FU02B'nin node'lara yansıttığı
read-only arşiv durumudur (pack §22.1) ve **`ended` ile aynı şey değildir** — `ended` FU06/atama katmanının
tarihsel sonlandırma işaretidir.

---

### 6.4 `territory-assignment-status` (4 value · platform-owned)

`proposed` (allowsMutation) · `active` (activeLike) · `ended` (historical) · `rejected` (historical).

**Validation usage:** Atama sonlandırma **hard delete değildir** → `Status=ended` + `ValidTo` (pack §10, §20).
Duplicate active primary kontrolü yalnız `active` kayıtlara bakar.

---

### 6.5 `territory-assignment-source` (4 value · platform-owned)

| ValueCode | requiresReason | canBeOverwrittenByRule |
|---|---|---|
| `rule` | ❌ | ✅ |
| `manual` | ✅ | ❌ |
| `import` | ❌ | ❌ |
| `override` | ✅ | ❌ |

**Validation usage (pack §19, §20):** Rule re-run yalnız `canBeOverwrittenByRule=true` kayıtları yeniden yazabilir —
**manuel ve override atamalar ezilmez**. `requiresReason=true` kaynaklarda `ChangeReason` boşsa **400**.

**Operator notes:** `import` için `requiresReason` tenant politikasına göre `true` yapılabilir (koşullu); template
default'u `false`'tur.

---

### 6.6 `territory-resource-role` (11 value · tenant-owned)

**Purpose:** Resource assignment rolleri. MOD-0151 **employee / person / position master sahibi değildir** — bu set
yalnız rolün territory kapsamındaki anlamını tanımlar.

| Role | defaultCoverageScope | isSalesRole | isManagementRole | canBePrimary | requiresBusinessScope |
|---|---|---|---|---|---|
| `medical-representative` | `exact-territory` | ✅ | ❌ | ✅ | ❌ |
| `area-manager` | `territory-subtree` | ✅ | ✅ | ✅ | ❌ |
| `regional-manager` | `territory-subtree` | ✅ | ✅ | ✅ | ❌ |
| `division-manager` | `territory-subtree` | ✅ | ✅ | ✅ | ❌ |
| `product-manager` | `product-portfolio` | ✅ | ✅ | ✅ | ✅ |
| `business-unit-manager` | `business-unit` | ✅ | ✅ | ✅ | ✅ |
| `hoc` | `all-business-scopes` | ✅ | ✅ | ❌ | ❌ |
| `commercial-manager` | `territory-subtree` | ✅ | ✅ | ✅ | ✅ |
| `admin` | `model-wide` | ❌ | ✅ | ❌ | ❌ |
| `viewer` | `model-wide` | ❌ | ❌ | ❌ | ❌ |
| `operational-resource` | `business-scope` | ❌ | ❌ | ❌ | ✅ |

**Validation usage:**
- `defaultCoverageScope` **yalnız UI/atama varsayılanıdır**; kullanıcı geçerli başka bir kapsam seçebilir.
- `canBePrimary=false` roller (`hoc`, `admin`, `viewer`, `operational-resource`) exclusivity kurallarına takılmaz.
- `requiresBusinessScope=true` roller `BusinessScope` olmadan atanamaz.
- `isSalesRole=false` roller **satış performans roll-up'ına dahil edilmez** (pack §15, D2).
- HOC / Commercial Manager'ın kesin kapsam politikası **policy-driven** bırakılmıştır (pack F7).

**Operator notes:** `defaultCoverageScope` değerleri **`territory-coverage-scope` set'inin valueCode'larıyla
eşleşmelidir** — çapraz referans kırılırsa atama formu varsayılanı çözemez.

---

### 6.7 `territory-rule-type` (9 value · platform-owned)

`geography` · `account-list` · `account-type` · `product-portfolio` · `business-scope` · `channel` · `segment` ·
`manual` · `import`.

Metadata: `requiresCriteria` · `supportsPriority` · `supportsPreview`. `manual` ve `import` kriter gerektirmez;
`manual` preview üretmez.

**Validation usage:** `requiresCriteria=true` tiplerde boş kriterle rule kaydı **400**. Activation gate yalnız
**modelde rule varsa** devreye girer.

---

### 6.8 `territory-conflict-policy` (4 value · platform-owned)

| ValueCode | severity | blocksActivation |
|---|---|---|
| `block` | error | ✅ |
| `warn` | warning | ❌ |
| `priority` | info | ❌ |
| `manual-review` | warning | ✅ (çözülene kadar) |

**Validation usage:** `blocksActivation=true` politikası altında **çözülmemiş conflict varsa aktivasyon 422** (pack §20).

---

### 6.9 `territory-coverage-scope` (7 value · platform-owned · **requiresTerritoryId metadata zorunlu**)

| CoverageScope | requiresTerritoryId | requiresBusinessScope | allowsTerritoryId | allowsBusinessScope |
|---|---|---|---|---|
| `exact-territory` | ✅ | ❌ | ✅ | ✅ |
| `territory-subtree` | ✅ | ❌ | ✅ | ✅ |
| `business-unit` | ❌ | ✅ | ❌ | ✅ |
| `product-portfolio` | ❌ | ✅ | ❌ | ✅ |
| `business-scope` | ❌ | ✅ | ✅ | ✅ |
| `model-wide` | ❌ | ❌ | ❌ | ❌ |
| `all-business-scopes` | ❌ | ❌ | ❌ | ❌ |

**Validation usage (pack §10, §20):** `CoverageScope` ↔ `TerritoryId` tutarlılığı bu metadata'dan **türetilir**.
`requiresTerritoryId=true` iken `TerritoryId` boşsa **400**; `allowsTerritoryId=false` iken doluysa **400**.
Bu metadata olmadan tutarlılık kuralı **hardcode edilmek zorunda kalır — bu yasaktır.**

---

### 6.10 `business-scope-type` (7 value · platform-owned · **koşullu required**)

**Purpose:** `TerritoryBusinessScope` sınıflandırması. **Business Unit bir territory level DEĞİLDİR** — kesişen
boyuttur (pack §9). MOD-0151 business unit / product / brand master **sahibi değildir**.

Detaylı tablo için §8. `isSalesScopeDefault` ve `includeInSalesPerformanceDefault` **zorunlu metadata**'dır —
Production Admin'in satış roll-up'ına girmemesi (D2) bu iki alandan türetilir.

**Operator notes:** Bu set `TerritoryBusinessScope` kullanılmıyorsa publish edilmek zorunda değildir; kullanılıyorsa
**required + activation gate**'tir.

---

## 7. Optional / Temporary Reference Sets

| SetCode | Why optional / temporary | Owner | Notes |
|---|---|---|---|
| `planning-period-type` | Planning-only; runtime validation'ı gate'lemez | tenant-owned | 4 value (annual/quarterly/monthly/custom). Tenant politikası `PlanningPeriodRef`'i zorunlu kılarsa activation gate olur |
| `product-portfolio` | **Temporary** — gerçek Product/Brand master yok (pack D3 / F6) | temporary-tenant-owned | Örnek değerler `alpha`/`beta`/`gamma` **yalnız illüstratiftir**; operator tenant'ın gerçek kodlarını yazar |
| `brand-group` | **Temporary** — aynı gerekçe | temporary-tenant-owned | Değerler tenant'a özgü; template'te **bilinçli olarak boş** bırakıldı |
| `territory-change-type` | Raporlama/trace tutarlılığı için önerilir; FU01 runtime gate'i değil | platform-owned | 7 value (pack §7.6 ChangeType listesi) |
| `commercial-role-scope-policy` | **Future** — HOC / Commercial Manager kapsam politikası dışsallaştırılırsa (pack F7) | tenant-owned | Değerler politika tanımlanana kadar boş |

**Optional-set kuralı (MOD-0150 precedent'i):** yayınlanmamış optional set → ilgili alan **boş bırakılır**,
**local fallback üretilmez**. Yalnız required set'ler create/update'i gate'ler.

**Temporary set emeklilik notu:** Product/Brand master modülü geldiğinde `product-portfolio` ve `brand-group`
deprecate edilir veya external reference'a dönüşür. `TerritoryBusinessScope` **stabil `ScopeCode` üzerinden** bağlandığı
için emeklilik geçmiş atamaları koparmamalıdır (pack D1/D3).

---

## 8. Business Scope Defaults

| ScopeType | isSalesScopeDefault | includeInSalesPerformanceDefault | OwnerModule | Notes |
|---|---|---|---|---|
| `business-unit` | **true** | **true** | MOD-0288 (veya `unitType` gelene kadar MOD-0048 temporary) | **Alpha / Beta / Gamma** buraya düşer — sabit, dönem taşımaz (D1) |
| `product-portfolio` | **true** | **true** | MDM/Product future (veya MOD-0048 temporary) | Temporary until Product/Brand master |
| `brand-group` | **true** | **true** | MDM/Product future (veya MOD-0048 temporary) | Temporary until Product/Brand master |
| `operational-scope` | **false** | **false** | MOD-0288 (veya MOD-0048 temporary) | Factory / affiliated company resource planlaması |
| `non-sales-resource-planning` | **false** | **false** | MOD-0288 (veya MOD-0048 temporary) | **Production Admin** buraya düşer (D2) |
| `channel` | true (koşullu) | true (koşullu) | MOD-0048 veya MOD-0167 (kullanıma göre) | Tenant kullanımına göre değerlendirilir |
| `segment` | true (koşullu) | true (koşullu) | MOD-0167 | Segment tanımı MOD-0167'nin SoR'u |

> **D1 (Alpha/Beta/Gamma):** yıl/çeyrek bazında **yeniden açılmaz**. Dönem değişimi `TerritoryModel` versiyonu /
> `PlanningPeriodRef` / `VersionNumber` ile yönetilir. `ScopeCode` sabit kalır → geçmiş performans karşılaştırılabilir.
>
> **D2 (Production Admin):** gerçek satış/product-portfolio business unit'i **değildir**. Resource assignment ve
> visibility planlamasında **kullanılabilir**; satış performans roll-up'ına **otomatik dahil edilmez**.

---

## 9. Resource Role Defaults

§6.6'daki tablo canonical'dır. Özet kural seti:

| Kural | Sonuç |
|---|---|
| `defaultCoverageScope` ⊆ `territory-coverage-scope` valueCode'ları | Çapraz referans **doğrulanmalı** (11/11 eşleşiyor) |
| `canBePrimary=false` | Exclusivity kurallarına takılmaz |
| `requiresBusinessScope=true` | BusinessScope olmadan atama **400** |
| `isSalesRole=false` | Satış performans roll-up'ına girmez (pack §15) |
| `isManagementRole=true` | Subtree/roll-up görünürlüğü hedeflenir (v1'de CrmService coverage filter ile) |

---

## 10. Coverage Scope Rules

§6.9 tablosu canonical'dır. Uygulama kuralı:

```
requiresTerritoryId = true  → TerritoryId zorunlu   (boşsa 400)
allowsTerritoryId   = false → TerritoryId null olmalı (doluysa 400)
requiresBusinessScope = true → BusinessScope zorunlu (boşsa 400)
allowsBusinessScope  = false → BusinessScope null olmalı (doluysa 400)
```

Bu dört bayrak olmadan MOD-0151 tutarlılık kuralını **kod içine gömmek zorunda kalır** — pack §16 ve §21'e göre
**yasaktır**.

---

## 11. Activation Gate Rules

`POST /api/crm/territory-models/{id}/activate` **fail-closed**'dır. Aktivasyon aşağıdaki durumlarda reddedilir:

| Gate | Koşul | Beklenen yanıt |
|---|---|---|
| Reference readiness (koşulsuz) | `territory-level` · `territory-model-status` · `territory-node-status` · `territory-assignment-status` · `territory-assignment-source` · `territory-coverage-scope` set'lerinden **biri bile** publish edilmemiş | **422** (veya create yolunda **400**) |
| Reference readiness (koşullu) | Modelde rule var ama `territory-rule-type` / `territory-conflict-policy` yok | **422** |
| Reference readiness (koşullu) | Model business scope kullanıyor ama `business-scope-type` yok | **422** |
| Metadata readiness | `territory-level` value'larında `rank` eksik → hiyerarşi doğrulanamaz | **422** |
| Metadata readiness | `territory-coverage-scope` value'larında `requiresTerritoryId` eksik → kapsam doğrulanamaz | **422** |
| Conflict | `blocksActivation=true` politikası altında çözülmemiş conflict | **422** |
| Approval | MOD-0023 Transition Gate `Blocked` / instance yok | **409 / 422** |

> **Hiçbir gate "reference yoksa geç" davranışına düşmez.** Sessiz varsayılan, hardcoded fallback ve fake published-values
> **yasaktır** (§12).

---

## 12. Guardrails

1. **Bu task publish etmez.** Bu dosya ve JSON yalnız template'tir.
2. **Hardcoded fallback yasak.** Yayınlanmamış required set → **kontrollü 400/422**, asla sessiz varsayılan.
3. **CRM local seed yasak.** Değerler yalnız MOD-0048'de canonical'dır.
4. **Mongo hand-edit yasak.** Authoring MOD-0048 governance akışı (validate → submit → SoD approve → publish) üzerinden.
5. **UPPER_SNAKE alias yasak.** Tüm setCode ve valueCode `lowercase-kebab`.
6. **MicroZone ayrı set değildir** — `territory-level` içinde bir value'dur. `micro-zone` adında set **oluşturmayın**.
7. **MOD-0151 master sahibi değildir:** business unit · product/brand · employee/person/position · organization.
8. **`product-portfolio` ve `brand-group` geçicidir** — Product/Brand master gelince emekliye ayrılır.
9. **Platform data-scope'a dokunulmaz** — bu template `EntitlementDataScopeKind` veya MOD-0018 engine'ini değiştirmez.
10. **ValueCode stabil kimliktir.** Yayınlanmış bir valueCode'u **yeniden adlandırmayın** — geçmiş atamalar kopar.
    Yeni value ekleyin, eskisini `isDeprecated=true` yapın.
11. **Scope kararı publish öncesi verilir** (§4) — sonradan değiştirmek tüm consumer çağrılarını kırar.

---

## 13. JSON Template Reference

Makine okunur sürüm: [`mod-0151-territory-required-reference-authoring-template.json`](./mod-0151-territory-required-reference-authoring-template.json)

| Alan | Anlam |
|---|---|
| `sets[]` | **Required** set'ler (10 set / 64 value) |
| `optionalSets[]` | Optional + temporary set'ler (5 set) |
| `sets[].ownerType` | `platform-owned` · `tenant-owned` · `temporary-tenant-owned` · `conditional` |
| `sets[].required` / `runtimeGate` / `activationGate` | boolean |
| `sets[].metadataSchema` | `"tip|required"` / `"tip|optional"` biçiminde alan sözleşmesi |
| `sets[].values[]` | `valueCode` (lowercase-kebab) · `displayName {en,tr}` · `sortOrder` · `isDeprecated` · `metadata` |
| `operatorChecklist[]` / `guardrails[]` | §12 ve checklist dosyasının makine okunur özeti |

JSON doğrulaması (bu template ile birlikte çalıştırıldı): **valid JSON · comment yok · trailing comma yok ·
64 required value · 0 duplicate · 0 non-kebab kod · `territory-resource-role.defaultCoverageScope` değerlerinin
11/11'i `territory-coverage-scope` valueCode'larıyla eşleşiyor.**

---

## 14. Operator Checklist

Tam adım listesi ayrı dosyadadır:
[`mod-0151-territory-reference-operator-checklist.md`](./mod-0151-territory-reference-operator-checklist.md)

Özet: tenant/scope kararı → set create → version → values (+metadata) → validate → submit (kullanıcı A) →
**SoD approve (kullanıcı B ≠ A)** → publish → published-values smoke + count doğrulama → metadata coverage doğrulama →
readiness PASS → ancak o zaman MOD-0151 FU01 create smoke.

---

## 15. Next Steps

1. **MOD-0151 FU00 Pack Approval / Source Reconciliation Closeout** — pack §24 acceptance checklist'ini yürüt;
   bu template'i F1 follow-up'ının karşılığı olarak kapat.
2. **MOD-0048 Territory Reference Set Publish Operator Runbook** — operator'ün gerçek publish'i yapacağı runbook
   (bu template + checklist girdi olarak kullanılır).
3. **MOD-0151 FU01 implementation prompt** — yalnız (a) pack approval ve (b) required set readiness PASS sonrası.

> Bugün MOD-0151 pack'i `content-ready` / `runtime_code_allowed: false`. Bu template o durumu **değiştirmez**.

---
module_id: MOD-0151
module_name: Territory Management
domain: commercial-suite
source_pack: MOD-0151 Territory Management
owner: module-pack-author
audience: MOD-0048 reference-data operator
status: operator-runbook-ready
runtime_code_allowed: false
publishes_reference_values: false
target_tenant_id: 97c59330-dbc4-4665-b29c-0c26dbb5cc93
target_scope_type: tenant
readiness_status: OPERATOR_RUNBOOK_READY / PUBLISHED_VALUES_PENDING
date: 2026-07-23
---

# MOD-0151 — Territory Reference Set Publish Operator Runbook

> **Bu dosya publish YAPMAZ.** Operator'ün gerçek publish işlemini doğru, kanıtlı ve geri alınabilir şekilde
> yürütebilmesi için hazırlanmış bir çalışma talimatıdır. Değer kaynağı:
> [authoring template JSON](./mod-0151-territory-required-reference-authoring-template.json) ·
> Açıklamalar: [authoring template MD](./mod-0151-territory-required-reference-authoring-template.md) ·
> Kısa checklist: [operator checklist](./mod-0151-territory-reference-operator-checklist.md) ·
> Pack: [MOD-0151](../module-packs/MOD-0151-territory-management.md) ·
> Gate: [FU00 closeout](../../../../docs/audits/mod-0151-fu00-pack-approval-closeout-2026-07-23.md)

**Hedef tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
**Hedef scope:** `tenant` (tüm set'ler — §2 gerekçe)
**Kapsam:** 10 required set / **64 value** + 2 önerilen optional set

---

## 0. RE-PUBLISH GEREKSİNİMİ (2026-07-28 — FU02B Lifecycle Status Reconciliation)

> ⚠️ Tenant `97c59330-…` daha önce **62 required value** ile publish edildi (73 toplam). FU02B canlı smoke'u,
> lifecycle sözlüğünde **iki değerin eksik** olduğunu ortaya çıkardı; `deactivate` ve `archive` fail-closed 400
> dönüyor. Kanıt: [`mod-0151-fu02b-authenticated-gateway-live-smoke-closeout-retry-2026-07-25.md`](../../../../docs/audits/mod-0151-fu02b-authenticated-gateway-live-smoke-closeout-retry-2026-07-25.md)

**Yalnız iki set için re-publish gerekir:**

| Set | Eklenen değer | sortOrder | Önce | Sonra |
|---|---|---|---|---|
| `territory-model-status` | `inactive` | **45** (active 40 ile superseded 50 arasına) | 6 value | **7 value** |
| `territory-node-status` | `archived` | **50** (ended 40'tan sonra) | 4 value | **5 value** |

**Kurallar:**

- Mevcut hiçbir value **silinmez, yeniden kodlanmaz veya sortOrder'ı değiştirilmez** — yalnız iki yeni value eklenir.
- Diğer 8 required set ve 2 optional set **dokunulmaz**; onlar için re-publish gerekmez.
- Publish maker-checker akışıyla yapılır (SoD: submit eden approve edemez); `publishoverride` ile SoD bypass **yasaktır**.
- Publish sonrası beklenen: **required 64/64**, **toplam 75/75** (64 + optional 11).
- Re-publish sonrası doğrulama: `smoke-mod-0151-territory-publishedvalues.ps1`, ardından FU02B live smoke RETRY-2.

---

## 0. Doğrulanmış runtime gerçekleri (uydurulmadı — koddan okundu)

| Konu | Doğrulanan gerçek | Kaynak |
|---|---|---|
| Controller route | `api/v1/reference-data` | `BusinessReferenceDataController.cs:25` |
| Gateway | `/api/v1/reference-data` + `/api/v1/reference-data/{everything}` → Platform | `ocelot.json:902-930` |
| Published-values | `GET sets/{setCode}/published-values?scope_key=…` | `BusinessReferenceDataController.cs:337` |
| Scope kuralı | `global` set + scope_key → **`scope_key_not_allowed_for_global`** · `tenant` set + scope_key yok → **`scope_key_required`** | `BusinessReferenceDataConsumerQueryService.cs:538-550` |
| **SoD kod düzeyinde zorunlu** | Submitter kendi versiyonunu approve edemez → **`sod_submitter_cannot_approve`** (audit'e de yazılır) | `BusinessReferenceDataGovernanceService.cs:128-135` |
| **Publish idempotency zorunlu** | `Idempotency-Key` header yoksa → **400 `idempotency_key_required`** | `BusinessReferenceDataController.cs:290-296` |
| Publish override | Normal publish endpoint'inde `override_action: true` → **403 `publish_override_permission_required`**; ayrı endpoint + ayrı izin gerekir | `BusinessReferenceDataController.cs:283-287, 310` |
| Reject gerekçesi | Reject + boş `rejection_reason` → **`rejection_reason_required`** | `BusinessReferenceDataGovernanceService.cs:137-141` |
| **⚠️ Metadata tipi** | Value payload'da metadata alanı **`attributes`**'tır ve tipi **`Dictionary<string,string>`** — yani **tüm metadata string olarak gönderilir** | `BusinessReferenceDataStewardshipRequests.cs:199-200` |
| Consumer okuması | `version.Values` üzerinden; `IsDeprecated` client-side filtrelenir | MOD-0029/MOD-0149 consumer deseni |
| CRM tarafı beklentisi | `GatewayReferenceDataValidator` default path: `/api/v1/reference-data/sets/{setCode}/published-values` | `GatewayReferenceDataValidator.cs:39` |

### ⚠️ Kritik: metadata string olarak yazılır

F1 template'i metadata'yı **tipli** gösterir (`rank: 10`, `isSalesScopeDefault: true`) çünkü sözleşmeyi anlatır.
**API'ye gönderirken tümü string olmalıdır:**

```
"attributes": { "rank": "10", "sortOrder": "10", "canHaveChildren": "true" }
```

Sayısal/boolean metadata'yı JSON number/boolean olarak göndermeye çalışmak **deserialization hatası** üretir.
Tüketici tarafı (MOD-0151 FU01) bu string'leri parse etmekle yükümlüdür — bu **template'in bir hatası değil**,
platform sözleşmesidir. FU01 implementasyonuna bu not iletilmelidir.

---

## 1. Ön koşullar

| # | Ön koşul | Not |
|---|---|---|
| 1 | Gateway (5000) ve Platform (5057) ayakta | Tüm çağrılar **Gateway üzerinden**; servis portuna doğrudan gidilmez |
| 2 | **İki farklı kullanıcı** hazır (Maker ≠ Checker) | SoD kod düzeyinde zorunlu (§0) |
| 3 | Maker izinleri | `Platform.BusinessReferenceData.Create` · `.Version.Create` · `.Version.Update` · `.Version.Validate` · `.Version.Submit` |
| 4 | Checker izinleri | `Platform.BusinessReferenceData.Version.Approve` · `.Version.Publish` |
| 5 | Smoke izni | `Platform.BusinessReferenceData.Consumer.Read` |
| 6 | Tenant doğrulandı | `97c59330-dbc4-4665-b29c-0c26dbb5cc93` |
| 7 | Değerler onaylandı | F1 template JSON'u referans; `product-portfolio` / `brand-group` için tenant gerçeği teyit edilmeli (§4) |

> Permission anahtarları attribute'larda PascalCase görünür; runtime karşılaştırması lowercase'e normalize edilir.
> Yetki eksikse **403** alırsınız — bu bir runbook hatası değil, izin eksikliğidir.

---

## 2. Scope Decision

**Karar: 10 required set'in ve önerilen optional set'lerin TAMAMI `scope_type=tenant` ile ve
`scope_key=97c59330-dbc4-4665-b29c-0c26dbb5cc93` için publish edilecektir.**

F1 template'inde `territory-model-status`, `territory-coverage-scope` vb. **`platform-owned`** olarak işaretlidir.
Bu bir **sahiplik** ifadesidir ("tenant bu değerleri serbestçe değiştirmemeli"), **teknik scope zorunluluğu değildir.**

| Seçenek | Açıklama | v1 kararı |
|---|---|---|
| **Safe v1 — tenant-scoped** | Tüm set'ler `scope_type=tenant`, `scope_key={tenantId}` | ✅ **SEÇİLDİ** |
| Future — global | `platform-owned` set'ler `scope_type=global`, scope_key **verilmez** | ⏸️ Ertelendi (ayrı governance kararı) |

**Gerekçe:**
1. **Precedent:** MOD-0149 ve MOD-0150'nin tüm required set'leri tenant-scoped publish edilmiştir; CRM consumer'ları
   (`GatewayReferenceDataValidator`) `scope_key`'i **açıkça** geçmektedir.
2. **FU01 live create smoke** tenant-specific published-values gerektirir.
3. **`scope_key` trap'i önlenir:** karışık scope'ta consumer'ın bazı set'lerde `scope_key` göndermesi, bazılarında
   göndermemesi gerekir; tek bir `tenant` scope'u bu dallanmayı ortadan kaldırır.
4. Global scope davranışı CRM tüketici yolunda **henüz kanıtlanmamıştır**; belirsizlik varken tenant scope güvenlidir.

> Global'e geçiş ileride yapılırsa: yeni global set publish edilir, tenant set deprecate edilir, **consumer çağrı şekli
> değişir** (scope_key kaldırılır). Bu bir kod değişikliği gerektirir → ayrı task.

---

## 3. Required Publish Plan

Tümü: `scope_type=tenant` · `scope_key=97c59330-dbc4-4665-b29c-0c26dbb5cc93`

| Order | SetCode | Values | Owner Type | Required Metadata | Activation Gate | Blocks Live Smoke? |
|---|---|---|---|---|---|---|
| 1 | `territory-coverage-scope` | **7** | platform-owned | `requiresTerritoryId`, `requiresBusinessScope`, `allowsTerritoryId`, `allowsBusinessScope` | ✅ koşulsuz | ✅ Evet |
| 2 | `territory-level` | **6** | tenant-owned | **`rank`**, `sortOrder` | ✅ koşulsuz | ✅ Evet |
| 3 | `territory-model-status` | **7** | platform-owned | opsiyonel | ✅ koşulsuz | ✅ Evet |
| 4 | `territory-node-status` | **5** | platform-owned | opsiyonel | ✅ koşulsuz | ✅ Evet |
| 5 | `territory-assignment-status` | **4** | platform-owned | opsiyonel | ✅ koşulsuz | ✅ Evet |
| 6 | `territory-assignment-source` | **4** | platform-owned | `requiresReason`, `canBeOverwrittenByRule` | ✅ koşulsuz | ✅ Evet |
| 7 | `business-scope-type` | **7** | platform-owned | `isSalesScopeDefault`, `includeInSalesPerformanceDefault`, `ownerModule` | ⚠️ koşullu (business scope kullanılıyorsa) | ✅ Evet |
| 8 | `territory-resource-role` | **11** | tenant-owned | `defaultCoverageScope`, `isSalesRole`, `isManagementRole`, `canBePrimary` | ✅ koşulsuz | ✅ Evet |
| 9 | `territory-rule-type` | **9** | platform-owned | opsiyonel | ⚠️ koşullu (rule varsa) | ✅ Evet |
| 10 | `territory-conflict-policy` | **4** | platform-owned | `severity`, `blocksActivation` | ⚠️ koşullu (conflict varsa) | ✅ Evet |

**TOPLAM: 10 set · 64 value.**

### Sıra gerekçesi
- **1. `territory-coverage-scope` önce:** `territory-resource-role.defaultCoverageScope` bu set'in valueCode'larına
  referans verir. Ters sırada publish edilirse çapraz referans doğrulaması yapılamaz.
- **2. `territory-level`:** `TerritoryNode` hiyerarşi validasyonu `rank` metadata'sına dayanır.
- **3–6. lifecycle/status/source:** Model, node ve assignment yaşam döngülerinin temeli.
- **7. `business-scope-type`:** Production Admin / non-sales default'ları burada; `territory-resource-role`'ün
  `requiresBusinessScope=true` rolleri buna dayanır.
- **8. `territory-resource-role`:** 1 ve 7'ye bağımlı.
- **9–10. rule/conflict:** Yalnız FU03+ için gerekir; en sona bırakılır.

### FU01 etkisi (set bazında)
| Set | FU01 impact |
|---|---|
| `territory-level`, `territory-node-status` | **Doğrudan** — `TerritoryNode` create/update validation |
| `territory-model-status` | **Doğrudan** — `TerritoryModel` create/update validation |
| `territory-coverage-scope`, `territory-resource-role`, `territory-assignment-*` | **Dolaylı** — FU01 aggregate'lerinde kullanılmaz, ancak contract endpoint readiness raporu bunları listeler; FU04/FU05'te doğrudan olur |
| `business-scope-type` | **Dolaylı** — `TerritoryModel.BusinessScopes[]` doluysa doğrudan |
| `territory-rule-type`, `territory-conflict-policy` | **FU03** — FU01'i bloklamaz ama publish planına dahil edilmesi kolaydır |

---

## 4. Optional Publish Plan

| SetCode | Recommended Action | Reason | Blocks FU01? |
|---|---|---|---|
| `planning-period-type` (4 value) | ✅ **Publish et** | `TerritoryModel.PlanningPeriodRef` kullanılabilir hale gelir; maliyeti düşük, riski yok | ❌ Hayır |
| `territory-change-type` (7 value) | ✅ **Publish et** | FU06 Change Approval Trace ve raporlama tutarlılığı; şimdi publish etmek sonradan geriye dönük veri düzeltmesini önler | ❌ Hayır |
| `product-portfolio` | ⚠️ **Yalnız tenant değerleri onaylandıysa** | Template'teki `alpha`/`beta`/`gamma` **illüstratiftir**. Operator, tenant'ın gerçek portfolyo kodlarını iş sahibiyle teyit etmelidir. Yanlış kod publish etmek `TerritoryBusinessScope.ScopeCode` üzerinden geçmişe bağlanır → temizlemesi pahalıdır | ❌ Hayır |
| `brand-group` | ⛔ **Şimdilik publish etme** | Değerler bilinmiyor. **Boş set publish etmeyin** — publish edilmiş ama değersiz bir set, tüketiciye "set var, değer yok" belirsizliği verir ve `no_published_version` yerine boş liste döndürerek hatayı maskeler. Değerler netleştiğinde publish edilir | ❌ Hayır |
| `commercial-role-scope-policy` | ⛔ **Publish etme (future)** | Politika henüz tanımlanmadı (pack F7). Boş set publish etmenin faydası yok | ❌ Hayır |

> **Boş set publish etme kararı (gerekçe):** MOD-0048 tüketici davranışında yayınlanmış-ama-boş bir set,
> yayınlanmamış bir set'ten **farklı** hata yolu üretir. Belirsiz durumu net bir "yok" olarak bırakmak,
> yanlış bir "var ama boş" durumundan daha güvenlidir.

---

## 5. Maker / Checker (SoD) akışı

**SoD kod düzeyinde zorunludur** — submitter kendi versiyonunu approve edemez (`sod_submitter_cannot_approve`,
ihlal audit'e yazılır). Bu bir tavsiye değil, sistem kuralıdır.

| Rol | Sorumluluk | Adımlar |
|---|---|---|
| **Maker / Author** | Set + version + value hazırlar, doğrular, onaya gönderir | 1–5 |
| **Checker / Approver** | Değer ve metadata'yı **bağımsız** kontrol eder, onaylar, yayınlar | 6–7 |

**Checker'ın onaylamadan önce bakması gerekenler:**
- valueCode'lar F1 template ile birebir mi (lowercase-kebab, doğru sayı)?
- Metadata `attributes` içinde ve **string** olarak mı?
- `territory-level` rank'ları 10→60 kesin artan mı?
- `business-scope-type`'ta `operational-scope` ve `non-sales-resource-planning` **false/false** mı?
- `territory-resource-role.defaultCoverageScope` değerleri `territory-coverage-scope` kodlarında var mı?
- Scope `tenant` ve doğru tenant mı?

---

## 6. Publish prosedürü (set başına tekrarlanır)

Tüm çağrılar **Gateway (5000)** üzerinden, `Authorization: Bearer <token>` ile.

| # | Adım | Çağrı | Beklenen | Kim |
|---|---|---|---|---|
| 1 | Set oluştur | `POST /api/v1/reference-data/sets` → `{ "set_code": "...", "name": "...", "scope_type": "tenant", "description": "...", "status": "Active" }` | 200/201 · `setId` | Maker |
| 2 | Version oluştur | `POST /api/v1/reference-data/sets/{setId}/versions` | 200/201 · `versionId` | Maker |
| 3 | Value'ları yaz | `PUT /api/v1/reference-data/versions/{versionId}/values` → `{ "values": [ { "code": "...", "label": "...", "is_active": true, "sort_order": 10, "attributes": { "rank": "10" } } ] }` | 200 | Maker |
| 4 | Validate | `POST /api/v1/reference-data/versions/{versionId}/validate` | 200 · hata yok | Maker |
| 5 | Submit | `POST /api/v1/reference-data/versions/{versionId}/submit` | 200 · `SubmittedBy` = Maker | Maker |
| 6 | **Approve** | `POST /api/v1/reference-data/versions/{versionId}/approve` → `{ "decision": "approve", "comment": "..." }` | 200 · Approved | **Checker (≠ Maker)** |
| 7 | **Publish** | `POST /api/v1/reference-data/versions/{versionId}/publish` + **`Idempotency-Key: <uuid>`** header → `{ "publish_mode": "immediate" }` | 200 · `publishedVersionId` dolu | Checker |
| 8 | Smoke | `GET /api/v1/reference-data/sets/{setCode}/published-values?scope_key=97c59330-dbc4-4665-b29c-0c26dbb5cc93` | 200 · doğru count | Her ikisi |

**Sık karşılaşılan kontrollü hatalar (bunlar bug değildir):**

| Hata | Anlamı | Çözüm |
|---|---|---|
| `idempotency_key_required` (400) | Publish'te header yok | `Idempotency-Key` ekle |
| `sod_submitter_cannot_approve` | Aynı kullanıcı submit+approve | Farklı kullanıcıyla approve et |
| `rejection_reason_required` | Reject'te gerekçe yok | `rejection_reason` doldur |
| `publish_override_permission_required` (403) | `override_action: true` gönderildi | Normal publish'te override kullanma |
| `scope_key_required` (400) | Tenant set'e scope_key verilmedi | `?scope_key={tenantId}` ekle |
| `scope_key_not_allowed_for_global` (400) | Global set'e scope_key verildi | Bu runbook'ta olmamalı (hepsi tenant) |
| `no_published_version` | Set var, publish edilmiş versiyon yok | 7. adımı tamamla |

**Yasak:** Mongo hand-edit · CRM local seed · hardcoded fallback · `publish-override` ile SoD atlatma ·
sahte/boş published-values ile "tamam" demek.

---

## 7. Smoke Expectations

### 7.1 Count smoke

| SetCode | Expected Count | Actual | ☐ |
|---|---|---|---|
| `territory-coverage-scope` | **7** | — | ☐ |
| `territory-level` | **6** | — | ☐ |
| `territory-model-status` | **7** | — | ☐ |
| `territory-node-status` | **5** | — | ☐ |
| `territory-assignment-status` | **4** | — | ☐ |
| `territory-assignment-source` | **4** | — | ☐ |
| `business-scope-type` | **7** | — | ☐ |
| `territory-resource-role` | **11** | — | ☐ |
| `territory-rule-type` | **9** | — | ☐ |
| `territory-conflict-policy` | **4** | — | ☐ |
| **TOPLAM** | **64** | — | ☐ |

Opsiyonel: `planning-period-type` **4** · `territory-change-type` **7**.

### 7.2 Metadata smoke — `territory-level`

| ValueCode | rank | sortOrder var? | ☐ |
|---|---|---|---|
| `division` | **10** | ✅ | ☐ |
| `country` | **20** | ✅ | ☐ |
| `region` | **30** | ✅ | ☐ |
| `area` | **40** | ✅ | ☐ |
| `zone` | **50** | ✅ | ☐ |
| `microzone` | **60** | ✅ | ☐ |
| **rank kesin artan** | 10<20<30<40<50<60 | — | ☐ |

### 7.3 Metadata smoke — `territory-coverage-scope`

| ValueCode | requiresTerritoryId | requiresBusinessScope | ☐ |
|---|---|---|---|
| `exact-territory` | **true** | false | ☐ |
| `territory-subtree` | **true** | false | ☐ |
| `business-unit` | false | **true** | ☐ |
| `product-portfolio` | false | **true** | ☐ |
| `business-scope` | false | **true** | ☐ |
| `model-wide` | **false** | **false** | ☐ |
| `all-business-scopes` | **false** | **false** | ☐ |

Ayrıca `allowsTerritoryId` ve `allowsBusinessScope` 7/7 value'da dolu olmalı.

### 7.4 Metadata smoke — `territory-resource-role`

| Role | defaultCoverageScope | ☐ |
|---|---|---|
| `medical-representative` | `exact-territory` | ☐ |
| `area-manager` | `territory-subtree` | ☐ |
| `regional-manager` | `territory-subtree` | ☐ |
| `division-manager` | `territory-subtree` | ☐ |
| `product-manager` | `product-portfolio` | ☐ |
| `business-unit-manager` | `business-unit` | ☐ |
| `hoc` | `all-business-scopes` | ☐ |
| `commercial-manager` | `territory-subtree` | ☐ |
| `admin` | `model-wide` | ☐ |
| `viewer` | `model-wide` | ☐ |
| `operational-resource` | `business-scope` | ☐ |
| **Çapraz referans:** 11/11 değer `territory-coverage-scope` valueCode'larında **mevcut** | — | ☐ |

### 7.5 Metadata smoke — `business-scope-type`

| ValueCode | isSalesScopeDefault | includeInSalesPerformanceDefault | ☐ |
|---|---|---|---|
| `business-unit` | **true** | **true** | ☐ |
| `product-portfolio` | **true** | **true** | ☐ |
| `brand-group` | **true** | **true** | ☐ |
| `operational-scope` | **false** | **false** | ☐ |
| `non-sales-resource-planning` | **false** | **false** | ☐ |
| `channel` | conditional (true kabul) | conditional | ☐ |
| `segment` | conditional (true kabul) | conditional | ☐ |
| `ownerModule` 7/7 dolu | — | — | ☐ |

> `business-unit` / `product-portfolio` / `brand-group` için **true/true** beklenir; operator gerekçeli kanıtla
> (iş sahibi kararı) override ederse **evidence tablosuna yazmalıdır**.
> `operational-scope` ve `non-sales-resource-planning` için **false/false zorunludur** — bu, pack D2 kararının
> (Production Admin satış roll-up'ına girmez) tek teknik dayanağıdır.

### 7.6 Bütünlük smoke

| Kontrol | Beklenen | ☐ |
|---|---|---|
| Duplicate valueCode | **yok** | ☐ |
| lowercase-kebab ihlali | **yok** | ☐ |
| `isDeprecated=false` tüm value'larda | ✅ | ☐ |
| `micro-zone` diye ayrı set | **yok** (MicroZone = `territory-level` value'su) | ☐ |
| UPPER_SNAKE alias | **yok** | ☐ |
| Tüm set'ler `scope_type=tenant` ve doğru tenant | ✅ | ☐ |

### Smoke PASS şartı
10 required set bulundu **ve** 64 value döndü **ve** §7.2–7.6'daki tüm kontroller PASS.
Herhangi biri fail → **FU01 live create smoke açılmaz.**

---

## 8. Evidence Table (operator doldurur)

Her set için bir satır:

| environment | tenantId | setCode | scopeType | scopeKey | version | authorUser | approverUser | publishTimestamp | correlationId / requestId | expectedCount | actualCount | metadataSmokeResult | publishedValuesSmokeResult | notes | evidenceLink / screenshotRef |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | territory-coverage-scope | tenant | 97c59330-… | | | | | | 7 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | territory-level | tenant | 97c59330-… | | | | | | 6 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | territory-model-status | tenant | 97c59330-… | | | | | | 6 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | territory-node-status | tenant | 97c59330-… | | | | | | 4 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | territory-assignment-status | tenant | 97c59330-… | | | | | | 4 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | territory-assignment-source | tenant | 97c59330-… | | | | | | 4 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | business-scope-type | tenant | 97c59330-… | | | | | | 7 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | territory-resource-role | tenant | 97c59330-… | | | | | | 11 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | territory-rule-type | tenant | 97c59330-… | | | | | | 9 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | territory-conflict-policy | tenant | 97c59330-… | | | | | | 4 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | planning-period-type *(opsiyonel)* | tenant | 97c59330-… | | | | | | 4 | | | | | |
| | 97c59330-dbc4-4665-b29c-0c26dbb5cc93 | territory-change-type *(opsiyonel)* | tenant | 97c59330-… | | | | | | 7 | | | | | |

**Özet satırı:** required set 10/10 · required value __/64 · metadata smoke PASS/FAIL · overall verdict PASS/FAIL.

---

## 9. Rollback / Recovery

**Temel kural: published value HARD DELETE EDİLMEZ.** Düzeltme her zaman **yeni versiyon** ile yapılır.

| Senaryo | Doğru aksiyon | Yanlış aksiyon |
|---|---|---|
| Yanlış/eksik value | Yeni version oluştur → düzelt → validate → submit → approve (SoD) → publish | Value'yu silmek · Mongo'dan düzenlemek |
| Yanlış metadata (örn. rank) | Düzeltilmiş yeni version publish et | Mevcut published version'ı in-place değiştirmek |
| Yanlış valueCode adı | **Yeni value ekle + eski value'yu `is_active=false` / deprecated yap** | Rename — geçmiş atamaları koparır |
| **`territory-level.rank` değişimi** | ⚠️ **Yüksek risk** — child hierarchy validation'ını etkiler. Aktif TerritoryModel varsa önce etki analizi; yeni ara seviye gerekiyorsa **yeni value** ekle (10'ar aralık bunun için bırakıldı) | Mevcut rank'leri yeniden numaralandırmak |
| Yanlış tenant scope'a publish | Doğru tenant scope'a **yeni publish** yap; yanlış scope'taki set'i deprecate/archive et | Hard delete |
| Yanlış scope_type (`global` ↔ `tenant`) | Doğru scope ile yeni set publish et; eskisini deprecate et. **Consumer çağrı şekli değişir** → kod etkisi kontrol edilmeli | Set'in scope'unu in-place değiştirmeye çalışmak |
| Publish yarıda kaldı | Aynı `Idempotency-Key` ile tekrar dene (idempotent) | Yeni key ile körlemesine tekrar |
| Smoke fail | **FU01 live create smoke açılmaz.** Düzeltilmiş version publish edilir, smoke tekrarlanır | Fail'i "sonra bakarız" diye geçmek |
| Required set eksik kaldı | MOD-0151 **fail-closed** davranmalı: create/update **400**, activation **422**. Bu doğru davranıştır | Fallback/varsayılan değer eklemek |

**Aktif tüketici varken destructive değişiklik yapılmaz.** MOD-0151 FU01 canlıya çıktıktan sonra bu set'lerin
tüketicisi vardır; her değişiklik yeni versiyon + smoke gerektirir.

---

## 10. FU01 Readiness Gate

| Aşama | Publish gerekli mi? | Karar |
|---|---|---|
| **FU01 kod implementasyonu** (TerritoryModel/TerritoryNode aggregate, validation, reference validator, testler) | ❌ **Hayır** | FU01 **fail-closed validation** yazabilir; required set yokken kontrollü 400/422 döndürmek zaten beklenen davranıştır. MOD-0149/0150 precedent'i aynıdır |
| **FU01 unit / integration testleri** | ❌ Hayır | Reference validator mock/stub ile test edilir |
| **FU01 live create smoke** | ✅ **Evet** | 10 required set + 64 value publish PASS olmadan **PASS sayılamaz** |
| FU03 rule preview canlı | ✅ Evet | `territory-rule-type` + `territory-conflict-policy` gerekir |
| FU04 resource assignment canlı | ✅ Evet | `territory-resource-role` + `territory-coverage-scope` + `business-scope-type` gerekir |
| FU06 activation canlı | ✅ Evet | 6 koşulsuz activation-gate set'i + MOD-0023 template gerekir |

### Readiness status akışı

```
TEMPLATE_READY            ✅ (F1 tamamlandı, 2026-07-23)
OPERATOR_RUNBOOK_READY    ✅ (bu doküman)
PUBLISHED_VALUES_PENDING  ⏳ (operator aksiyonu bekliyor)
PUBLISHED_VALUES_READY    ⏸️ (10/10 set + 64/64 value + metadata smoke PASS sonrası)
LIVE_SMOKE_READY          ⏸️ (FU01 kodu + PUBLISHED_VALUES_READY sonrası)
```

**Bu task sonundaki durum:** `OPERATOR_RUNBOOK_READY` + `PUBLISHED_VALUES_PENDING`.

---

## 11. Guardrails

1. Bu runbook **publish yapmaz** — operator aksiyonunu tarif eder.
2. **Mongo hand-edit yasak.** Tüm işlemler MOD-0048 governance akışı üzerinden.
3. **CRM local seed / hardcoded fallback yasak.**
4. **SoD atlatılamaz** — `publish-override` ile SoD aşmaya çalışmak governance ihlalidir ve audit'e yazılır.
5. **Boş set publish edilmez** (`brand-group`, `commercial-role-scope-policy`).
6. **`micro-zone` adında set oluşturulmaz** — MicroZone `territory-level` value'sudur.
7. **valueCode rename yasak** — deprecate + yeni ekle.
8. **Publish sonrası scope değiştirilmez.**
9. Metadata **string** olarak yazılır (`attributes: Dictionary<string,string>`).
10. Smoke fail → FU01 live smoke açılmaz; sessizce geçilmez.

# MOD-0151 FU04B — Plan vs Current Visibility · Live Smoke Closeout

Tarih: 2026-07-31
Target tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
Module: `MOD-0151 — Territory Management` · FU: `FU04B-resource-assignment-plan-vs-current-visibility`
Tür: **canlı doğrulama** — kod, config, gateway, RBAC, reference data ve Mongo değiştirilmedi.

**Sonuç: 95 kontrol / 95 PASS / 0 FAIL** (API 66 · UI server-render 25 · UI veri yolu 4)

---

## 1. Preflight

### 1.1 Fleet health

| Servis | Port | Sonuç |
|---|---|---|
| Gateway | 5000 | **200** |
| Web | 5001 | ayakta (`/health` yok; `/CRM/TerritoryManagement` 302→login) |
| AuthService | 5056 | **200** |
| Platform | 5057 | **200** |
| CrmService | 5061 | **200** (yalnız `/health`; business API'ye direkt çağrı yapılmadı) |

### 1.2 Authenticated tenant session

Login: `POST {gateway}/api/tenant-auth/login` + `X-Tenant-Id: 97c5…cc93`.

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| Tenant claim | `97c59330-dbc4-4665-b29c-0c26dbb5cc93` | aynı | **PASS** |
| CRM territory permission'ları | ≥1 `crm.territory.*` | `crm.territory.read`, `crm.territory.model.read`, `crm.territory.model.manage`, `crm.territory.node.read`, `crm.territory.node.manage` (5/5) | **PASS** |
| Yasak permission'lar | yok | `crm.territory.delete` ve `crm.micro-zone.manage` **token'da yok** | **PASS** |

> **Operasyonel not:** Web (5001) tarafında `tenantId` verilmeden yapılan login **platform tenant `…0001`**'e düşüyor
> ve tenant 97c5'in modelleri görünmüyor (Details sayfası listeye redirect ediyor). UI doğrulaması
> `/account/login?tenantId=97c5…` + body'de `tenantId` ile yapıldı. Bu bilinen davranıştır, FU04B kusuru değildir.

---

## 2. Contract Verification

`GET {gateway}/api/crm/territory-management/contract` — canlı gövdeden okundu.

| Flag | Beklenen | Gerçekleşen |
|---|---|---|
| `supportsResourceAssignmentPlanSnapshot` | true | **true** |
| `supportsResourceAssignmentPlanVsCurrent` | true | **true** |
| `supportsPositionBasedResourceAssignment` | true | **true** |
| `supportsResourceAssignments` | true | **true** |
| `supportsResourceAssignmentLifecycle` | true | **true** |
| `supportsResourceReplacement` | true | **true** |
| `supportsResourceTransfer` | true | **true** |
| `supportsCurrentResponsibility` | true | **true** |
| `supportsWorkflowActivation` | **false** | **false** |
| `isReady` | true | **true** |

10/10 PASS.

---

## 3. Gateway Route Verification

Kimlik doğrulamadan önce (fail-closed kanıtı) — hepsi **401**, hiçbiri 404:

| Route | Gateway |
|---|---|
| `/api/crm/territory-management/contract` | 401 |
| `/api/crm/territory-models` | 401 |
| `/api/crm/territory-models/{id}/resource-assignment-plan-snapshot` | 401 |
| `/api/crm/territory-models/{id}/resource-assignment-plan-vs-current` | 401 |
| `/api/crm/territory-models/{id}/resource-responsibilities/current` | 401 |
| `/api/crm/resources/{id}/resource-assignment-plan-vs-current` | 401 |
| `/api/crm/resources/{id}/territory-responsibilities` (FU04A) | 401 |
| Web `/CRM/TerritoryManagement` | 302 → `/account/login` |

`/api/crm/resources/**` rotası FU04B implementation task'ında eklenmişti; bu koşuda **çalıştığı doğrulandı** ve
**değiştirilmedi**. Tüm business trafiği yalnız `:5000` üzerinden yürütüldü; `:5061`'e hiçbir business çağrısı yapılmadı.

---

## 4. Smoke Data Setup

Tüm kayıtlar Gateway üzerinden, payload'da `TenantId` **gönderilmeden** oluşturuldu.

| Nesne | Değer |
|---|---|
| Model code | `SMOKE-MOD0151-FU04B-20260731225851` |
| Model id | `a461850c-3e6a-4dc0-98e8-8751fbe9f257` |
| Country / Business Unit | `tr` / **`gamma`** |
| Effective window | 2026-07-30 → 2027-07-31 |
| Node 1 | `FU04B-KESAN-…` "Kesan Zone" (zone) · `2d08513a-18fe-4d8d-8a8c-dbce0b6afed8` |
| Node 2 | `FU04B-SULEYMANPASA-…` "Suleymanpasa Zone" (zone) · `06c8cef8-7435-4bda-b045-ff67ac8a7b76` |
| Position | `medical-representative` / "Medical Representative" / `person-position` |
| Resource A | `fu04b-ayse-…` "Ayse Hanim" |
| Resource B | `fu04b-mehmet-…` "Mehmet Bey" |
| Plan assignment | `f0c06c28-9228-46f3-b643-f5ef1cacb162` (Keşan + gamma + MR → Ayşe) |
| Replacement assignment | `a3f2796e-08f1-493e-8d19-7503792971d4` (Mehmet) |
| Transfer assignment | `98cb09d6-3f39-401e-9650-46646e5826b8` (Mehmet @ Süleymanpaşa) |

### 4.1 Business unit neden `alpha` değil `gamma`?

İlk deneme `alpha` ile yapıldı ve **activation 409** döndü:

```json
{"errors":["An overlapping active territory model already exists for the same country and business-unit scope."],"statusCode":409}
```

Sebep: tenant'ta `DENEME` modeli **tr + alpha** ile **active** ve efektif pencereleri çakışıyor. Bu **doğru FU02B
single-active-model guard davranışıdır**, FU04B kusuru değildir — yan ürün olarak guard'ın canlı çalıştığı da
doğrulanmış oldu. Mevcut operasyonel modeli deaktive etmemek için (mutasyon yasağı) çakışmayan `gamma` scope'u
seçildi ve senaryo aynen yürütüldü.

**Artık kayıt:** `SMOKE-MOD0151-FU04B-20260731225753` (`c726159c-…`) draft olarak kaldı — aktive edilemedi.
`crm.territory.delete` yasak ve gereksiz mutasyondan kaçınıldığı için silinmedi (FU05 smoke precedent'i).

---

## 5. Draft State

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| `plan-vs-current` state | `not-yet-activated` | `not-yet-activated` | **PASS** |
| Satır sayısı | 0 | 0 | **PASS** |
| `plan-snapshot` state | 200 + state (404 **değil**) | 200 + `not-yet-activated` | **PASS** |
| Proposed kayıt current sayılıyor mu? | Hayır | `resource-responsibilities/current` → **0 kayıt** | **PASS** |

---

## 6. Activation Snapshot

Activation: `POST /api/crm/territory-models/{id}/activate` → `data=true`.

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| Activation | true | true | **PASS** |
| Snapshot state | `available` | `available` | **PASS** |
| `snapshotVersion` | 1 | 1 | **PASS** |
| `capturedAt` | dolu | `2026-07-31T19:58:52.06Z` | **PASS** |
| `activationCorrelationId` | `fu04b-smoke-20260731225851` | aynı | **PASS** |
| Planned resource | Ayse Hanim | Ayse Hanim | **PASS** |
| `positionCode` | `medical-representative` | aynı | **PASS** |
| `positionTitle` | Medical Representative | aynı | **PASS** |
| `sourceAssignmentId` | `f0c06c28-…` | aynı | **PASS** |
| Snapshot'ta Role alanı | **0** | 0 (`role` içeren hiçbir alan yok) | **PASS** |

---

## 7. Plan vs Current — Unchanged

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| State | `available` | `available` | **PASS** |
| Satır sayısı | 1 | 1 | **PASS** |
| `diffType` | `Unchanged` | `Unchanged` | **PASS** |
| Planned | Ayse Hanim | Ayse Hanim | **PASS** |
| Current | Ayse Hanim | Ayse Hanim | **PASS** |

---

## 8. Plan vs Current — Replacement

Replacement: Keşan + gamma + MR Position → Ayşe yerine Mehmet.
Reason: *"Ayse Hanim Suleymanpasa bolgesine transfer edildi."*

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| Replacement oluştu | guid | `a3f2796e-…` | **PASS** |
| Satırda diff type | `Replaced` | `Replaced` | **PASS** |
| Territory node | Keşan | Kesan Zone | **PASS** |
| Planned resource | Ayse Hanim | Ayse Hanim | **PASS** |
| Current resource | Mehmet Bey | Mehmet Bey | **PASS** |
| `replacementReason` görünür | evet | tam metin döndü | **PASS** |
| `replacedAssignmentId` görünür | `f0c06c28-…` | aynı | **PASS** |
| `correlationId` görünür | dolu | `fu04b-smoke-repl-…` | **PASS** |
| Ayşe'nin eski kaydı | **silinmedi, ended** | history'de `status=ended` | **PASS** |

---

## 9. Plan vs Current — Transfer

Transfer: Mehmet'in Keşan ataması → Süleymanpaşa Zone. Reason: *"Suleymanpasa bolge dengelemesi."*

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| Transfer oluştu | guid | `98cb09d6-…` | **PASS** |
| Diff type kümesi | TransferredOut + TransferredIn | `TransferredOut,TransferredIn` | **PASS** |
| TransferredOut node | Keşan | Kesan Zone | **PASS** |
| TransferredIn node | Süleymanpaşa | Suleymanpasa Zone | **PASS** |
| TransferredIn current | Mehmet Bey | Mehmet Bey | **PASS** |
| `transferFromAssignmentId` | `a3f2796e-…` | aynı | **PASS** |
| `transferReason` görünür | evet | tam metin | **PASS** |
| Source assignment | ended | ended | **PASS** |
| Target assignment | active | active | **PASS** |
| **Determinizm** | aynı sorgu → aynı diff | iki ardışık çağrı aynı sonucu verdi | **PASS** |

Not: Zincir `Ayşe (plan) → replacement → Mehmet → transfer → Süleymanpaşa` şeklinde iki kenar içeriyor. Pack §22.4
öncelik kuralı uyarınca terminal node değiştiği için `TransferredOut`/`TransferredIn` üretildi; `Replaced` bastırıldı.
Bu, dokümante edilen deterministik önceliğin canlı doğrulamasıdır.

### 9.1 Filtreler

| Filtre | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| `diffType=TransferredIn` | yalnız o tip | yalnız `TransferredIn` | **PASS** |
| `territoryNodeId={süleymanpaşa}` | ≥1 | 2 | **PASS** |
| `positionCode=area-manager` (eşleşmeyen) | 0 | 0 | **PASS** |
| `businessUnit=gamma` | ≥1 | 2 | **PASS** |

---

## 10. Resource-Level View

`GET /api/crm/resources/{resourceId}/resource-assignment-plan-vs-current`

| Kaynak | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| Ayşe | planned Keşan + transfer/ended görünümü | 1 satır, `TransferredOut`, planned node = Kesan Zone | **PASS** |
| Mehmet | current görünümü | 1 satır, `TransferredIn`, current = Mehmet | **PASS** |

Her iki çağrı da Gateway üzerinden (`/api/crm/resources/**` rotası) çalıştı.

---

## 11. UI Smoke

Web (5001) üzerinde tenant 97c5 oturumu ile **sunucu-render** doğrulaması yapıldı
(`/CRM/TerritoryManagement/Models/{modelId}` → HTTP 200, başlık "FU04B Smoke 20260731225851").

### 11.1 Yapı

| Kontrol | Sonuç |
|---|---|
| `#tab-planvscurrent` sekme butonu | **PASS** |
| `#pane-planvscurrent` sekme paneli | **PASS** |
| `#tab-hierarchy` korunmuş | **PASS** |
| `#dt-territoryhierarchy` hierarchy grid'i bozulmamış | **PASS** |
| `#dt-planvscurrent` + `data-dt-standard="v2"` | **PASS** |
| `plan-vs-current.js` yükleniyor | **PASS** |
| `#territory-plan-vs-current-data` l10n bloğu | **PASS** |
| Ayrı skeleton id (`#plan-vs-current-skeleton`, id çakışması yok) | **PASS** |
| `#plan-vs-current-state` / `#plan-vs-current-meta` host'ları | **PASS** |
| Read-only uyarısı metni | **PASS** |

### 11.2 Kolonlar (render edilen `<th>` metinleri)

`Change type` · `Territory node` · **`Business Unit Scope`** · `Position` · `Planned resource` ·
`Current resource` · `Effective Date` · `Reason` → **8/8 PASS**.

> Business Unit kolonu ekranda **"Business Unit Scope"** olarak render ediliyor (mevcut `BusinessUnitScope` RESX
> anahtarı yeniden kullanıldı). Bu pack terminolojisiyle uyumludur; içerik olarak beklenen kolondur.

### 11.3 Filtreler

`filterPvcDiffType` · `filterPvcNode` · `filterPvcBusinessUnit` · `filterPvcPosition` · `filterPvcEffectiveAt`
→ **5/5 PASS**. Filtre id'leri hierarchy filtresiyle çakışmıyor.

### 11.4 Read-only doğrulaması

Panel içindeki **tüm** butonlar: `Reset`, `Filter`. Başka buton yok.

| Aranan | Sonuç |
|---|---|
| Apply / Replace / Transfer / End | **yok** — PASS |
| Workflow / Approve / Submit / Evidence | **yok** — PASS |
| `Role` etiketi / `roleCode` sızıntısı | **yok** — PASS (yalnız Position kullanılıyor) |

### 11.5 UI veri yolu (JS'in çağırdığı proxy)

`GET /CRM/TerritoryManagement/Models/{id}/PlanVsCurrent/Json` (aynı oturum, Gateway üzerinden):

| Kontrol | Gerçekleşen | Sonuç |
|---|---|---|
| Aktif model | `success=true`, `state=available`, 2 satır (`TransferredOut,TransferredIn`) | **PASS** |
| Summary | planned=1, current=1, changed=2 | **PASS** |
| Snapshot meta | `capturedBy=authenticated-user`, `version=1`, `isHistorical=false` | **PASS** |
| Provenance | `transferFrom=a3f2796e-…`, reason dolu, `correlationId` dolu | **PASS** |
| Draft model state | `success=true`, `state=not-yet-activated`, 0 satır | **PASS** |

### 11.6 Doğrulanamayan (browser gerektiren)

Gerçek tarayıcı sürülmediği için şunlar **çalıştırılmadı**: DataTable satır render'ı, sekmeye ilk tıklamada lazy
init, diff type rozet renkleri, responsive child-row provenance açılımı, state notice'larının görsel gösterimi,
`effectiveAt` filtresinin yeniden fetch tetiklemesi. Markup, script yüklemesi ve besleyen veri yolu doğrulandı;
eksik olan yalnız istemci tarafı etkileşimidir.

---

## 12. Guard Checks

| Guard | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** (mevcut rota yalnız doğrulandı) |
| RBAC seed/grant changed? | **No** |
| MOD-0048 publish changed? | **No** |
| Mongo hand-edit? | **No** |
| Account mutated? | **No** |
| Contact mutated? | **No** |
| AccountTerritoryAssignment apply changed? | **No** |
| Resource assignment **behavior** changed? | **No** — yalnız mevcut FU04A komutları çağrıldı |
| Workflow/evidence/import-export opened? | **No** — `supportsWorkflowActivation` canlıda `false` |
| Visit/route implementation opened? | **No** |
| Hard delete used? | **No** — eski kayıtlar `ended`, history korundu |
| Direct 5061 business call? | **No** — yalnız `/health` |
| `TenantId` payload used? | **No** — claim/`X-Tenant-Id` header |
| RoleCode diff/snapshot key olarak kullanıldı mı? | **No** — snapshot'ta 0 role alanı, UI'da sızıntı yok |
| New standalone menu page opened? | **No** — yalnız Details içinde sekme |
| Plan-vs-current okuması mutasyon yaptı mı? | **No** — assignment sayısı 3→3, snapshot version 1→1 |

---

## 13. Created / Updated Files

| Dosya | Aksiyon |
|---|---|
| `docs/audits/mod-0151-fu04b-resource-assignment-plan-vs-current-live-smoke-closeout-2026-07-31.md` | Created |

Kod, runtime, module pack, gateway, reference data, RBAC ve Mongo değiştirilmedi.
Tenant'ta oluşturulan smoke kayıtları §4'te listelenmiştir (tamamı MOD-0151 territory alanında; Account/Contact/FU05
verisi oluşturulmadı veya değiştirilmedi).

---

## 14. Final Verdict

### **PARTIAL** — canlı zincirin tamamı PASS; iki bilinen kalıntı

**95 kontrol / 95 PASS / 0 FAIL.** Task'ın PASS listesindeki maddelerin tamamı canlıda karşılandı:
contract flag'leri · gateway-only · draft state · activation snapshot · Unchanged · Replacement · Transfer ·
resource-level endpoint · UI sekmesi · guardrail'ler · sıfır kod/config değişikliği.

PARTIAL gerekçeleri (task rubriğinin PARTIAL maddeleriyle birebir):

1. **`changedBy` boş, `CapturedBy` sabit (`authenticated-user`).** Canlıda doğrulandı ve beklendiği gibi. Bu
   **FU04A actor dependency**'sidir: assignment üzerinde aktör persist edilmiyor, gerçek aktör MOD-0021 audit
   event'inde. FU04B bunu düzeltemez (FU04A yazma davranışını değiştirmek yasak).
2. **UI smoke browser'sız yapıldı.** Markup, script yüklemesi, kolonlar, read-only garantisi ve besleyen veri yolu
   doğrulandı; istemci tarafı etkileşimi (DataTable render, lazy tab init, rozet/child-row görünümü) doğrulanmadı.

Bu ikisi dışında FU04B implementation raporundaki "canlı smoke atlandı" PARTIAL gerekçesi **kapanmıştır**.

Ek bulgular (defect değil):

- **Single-active-model guard canlıda doğrulandı** — `tr + alpha` ile aktivasyon 409 döndü (`DENEME` modeli aktif).
- **Web login tenant varsayılanı** — `tenantId` verilmeden yapılan login platform tenant `…0001`'e düşüyor.
- **Artık draft** `SMOKE-MOD0151-FU04B-20260731225753` aktive edilemediği için draft kaldı ve silinmedi.

---

## 15. Next Recommended Prompt

```text
MOD-0151 FU05 Live Smoke Closeout
```

Kalan iki PARTIAL kalemi için (opsiyonel, düşük öncelik):

```text
MOD-0151 FU04B UI Browser Smoke — tenant 97c5 oturumunda model a461850c-3e6a-4dc0-98e8-8751fbe9f257
Details > Plan vs Current sekmesini tarayıcıda aç; DataTable satır render'ını, lazy tab init'i, diff-type
rozetlerini, child-row provenance açılımını ve effectiveAt filtresinin yeniden fetch'ini doğrula.
Kod/config değiştirme.
```

```text
MOD-0151 FU04A-ACTOR — assignment yazma yollarında aktör (changedBy) persist edilmesi; FU04A pack scope
authorization gerektirir.
```

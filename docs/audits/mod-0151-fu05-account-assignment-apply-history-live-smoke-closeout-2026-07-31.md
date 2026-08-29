# MOD-0151 FU05 — Account Assignment Apply + History · Live Smoke Closeout

Tarih: 2026-07-31
Target tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
Module: `MOD-0151 — Territory Management` · FU: `FU05-account-assignment-apply-history`
Tür: **canlı doğrulama** — kod, config, gateway, RBAC, reference data ve Mongo değiştirilmedi.

**Sonuç: 90 kontrol / 90 PASS / 0 FAIL** (API 63 · UI sayfa yükleme 4 · UI markup/guard 15 · UI final 8)

> Önceki FU05 canlı smoke'unun tek blocker'ı — *"matched preview row üretilemedi"* — bu koşuda **çözüldü**:
> modelde değerlendirilebilir kural bulunmadığı için 0 satır dönüyordu. Deterministik bir `account-list` kuralı
> kurulunca preview 1 matched row döndürdü ve apply → 409 → override → end zinciri uçtan uca kapandı.

---

## 1. Preflight

### 1.1 Fleet health

| Servis | Port | Sonuç |
|---|---|---|
| Gateway | 5000 | **200** |
| Web | 5001 | ayakta (auth'suz `/CRM/**` 302→login) |
| AuthService | 5056 | **200** |
| Platform | 5057 | **200** |
| CrmService | 5061 | **200** (yalnız `/health`; business API'ye direkt çağrı yok) |

### 1.2 Authenticated tenant session

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| Tenant claim | `97c59330-…cc93` | aynı | **PASS** |
| Territory permission'ları | ≥1 `crm.territory.*` | `read`, `model.read`, `model.manage`, `node.read`, `node.manage` (5/5) | **PASS** |
| Account read permission | `crm.account.read` | mevcut | **PASS** |
| Yasak permission'lar | yok | `crm.territory.delete` ve `crm.micro-zone.manage` token'da **yok** | **PASS** |

FU05 endpoint'leri pack kararı gereği `crm.territory.model.read/manage` fallback'ini kullanıyor
(`crm.territory.assignment.*` katalog hizalaması hâlâ ayrı FU05A follow-up'ı).

---

## 2. Contract Verification

`GET {gateway}/api/crm/territory-management/contract`

| Flag | Beklenen | Gerçekleşen |
|---|---|---|
| `supportsAssignmentRules` | true | **true** |
| `supportsAssignmentPreview` | true | **true** |
| `supportsAccountAssignmentApply` | true | **true** |
| `supportsAssignmentHistory` | true | **true** |
| `supportsCoverageSummary` | true | **true** |
| `supportsResourceAssignments` | true | **true** |
| `supportsWorkflowActivation` | **false** | **false** |

7/7 PASS.

---

## 3. Gateway Verification

Tüm business trafiği `http://localhost:5000` üzerinden yürütüldü. `:5061`'e yalnız `/health` çağrıldı.
Hiçbir payload'da `TenantId` alanı gönderilmedi; tenant JWT claim'i + `X-Tenant-Id` header'ı ile taşındı.

Kullanılan endpoint'ler (hepsi Gateway):
`/api/tenant-auth/login` · `/api/crm/territory-management/contract` · `/api/crm/accounts` ·
`/api/crm/accounts/{id}` · `/api/crm/accounts/{id}/territory-assignments` ·
`/api/crm/accounts/{id}/territory-coverage-summary` · `/api/crm/territory-models` ·
`/api/crm/territory-models/{id}` · `/api/crm/territory-models/{id}/nodes` ·
`/api/crm/territory-models/{id}/assignment-rules` · `/api/crm/territory-models/{id}/activate` ·
`/api/crm/territory-models/{id}/deactivate` · `/api/crm/territory-models/{id}/assignment-preview` ·
`/api/crm/territory-models/{id}/assignment-preview/apply` ·
`/api/crm/territory-models/{id}/account-assignments` ·
`/api/crm/territory-models/{id}/account-assignments/{assignmentId}/end`

Web UI tarafında yalnız kendi proxy action'ları kullanıldı (`/CRM/TerritoryManagement/...`), bunlar da Gateway'e
gidiyor.

---

## 4. Smoke Data Setup

| Nesne | Değer |
|---|---|
| Model code | `SMOKE-MOD0151-FU05-20260731231147` |
| Model id | `b701b8c6-ec89-4a68-9bcd-9b24163a17de` |
| Country / Business Unit | `tr` / **`beta`** |
| Model + node window | 2026-07-30 → 2027-07-31 |
| Node | `FU05-KESAN-20260731231147` "Kesan Zone" (zone) · `84012f9d-f404-489a-ac03-e1e32f72c225` |
| Rule | `FU05-RULE-20260731231147` · type **`account-list`** · conflictPolicy `block` · priority 10 · id `2e1494d7-8782-4751-b241-f79d66c6c846` |

**Business unit neden `beta`?** FU02B single-active-model guard canlıda iki kez devreye girdi:

1. `tr + alpha` → **409** *"An overlapping active territory model already exists for the same country and
   business-unit scope."* (operasyonel `DENEME` modeli aktif). Operasyonel model **deaktive edilmedi**.
2. `tr + gamma` → FU04B smoke modeli aktif olduğu için o da doluydu.

`tr + beta` boş slot olarak seçildi. Guard'ın doğru çalıştığı böylece yan ürün olarak da doğrulandı.

**Rule tipi neden `account-list`?** `geography` kuralı account'un `countryRef` alanına bağlı; Accounts liste
projeksiyonu lokasyon alanı taşımıyor (bilinen GAP-CRM-01). `account-list` + `includeAccountIds`, seçilen account'u
**deterministik** olarak eşleştirir ve smoke'u veri şansına bırakmaz. FU03'ün desteklediği üç tipten biridir.

---

## 5. Matched Account Selection

Tenant'ta 15 account listelendi; her biri için `territory-coverage-summary` sorgulanıp **mevcut coverage'ı olmayan**
ilk account seçildi (böylece apply/override/end assertion'ları başka bir modelin coverage'ıyla karışmıyor).

| Alan | Değer |
|---|---|
| Account | **`ACC-2026-000016` — Büyükşehir Eczanesi** |
| Account id | `25464183-95d0-4bae-bf26-9dbe79d56063` |
| Başlangıç coverage | `hasCurrentCoverage=false` |

Account master üzerinde **hiçbir mutation yapılmadı** (yalnız GET).

---

## 6. Preview Result

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| **Matched account** | ≥1 | **1** (`totalCandidateAccounts=1`, matched rows=1) | **PASS** |
| Hedef satır | seçilen account | `ACC-2026-000016 → FU05-KESAN-…` | **PASS** |
| `persistedAssignments` | **false** | false | **PASS** |
| Target node | `84012f9d-…` | aynı | **PASS** |
| Rule izi | `FU05-RULE-…` / `account-list` | aynı | **PASS** |
| **Yan etkisizlik — assignment sayısı** | 0 → 0 | 0 → 0 | **PASS** |
| **Yan etkisizlik — coverage** | false → false | false → false | **PASS** |

Preview öncesi/sonrası model account-assignment sayısı ve account coverage'ı birebir aynı kaldı: **apply öncesi hiçbir
yazma yok.**

---

## 7. Apply Result

### 7.1 Negatif kontrol — açık uçlu pencere

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| `effectiveTo = null` ile apply | controlled reddetme | **400** — *"Assignment effective window must stay inside the territory model window."* | **PASS** |
| Yazma oldu mu? | hayır | 0 assignment | **PASS** |

> **Davranış notu (defect değil, kayıt altına alınıyor):** modelin bir `EffectiveTo`'su varsa, açık uçlu
> (`effectiveTo=null`) bir account assignment **kabul edilmiyor** — sonsuz pencere model penceresini aşıyor sayılıyor.
> Apply çağrısı bounded bir `effectiveTo` (ör. model penceresi sonu) göndermelidir. UI'ın apply canvas'ı zaten
> tarihleri model penceresine göre dolduruyor; API'yi doğrudan çağıran entegrasyonlar için not edilmelidir.

### 7.2 Apply

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| HTTP | 200 | 200 | **PASS** |
| `createdCount` | 1 | 1 | **PASS** |
| Assignment status | active | `active` | **PASS** |
| `effectiveFrom` | 2026-07-31 | `2026-07-31T00:00:00+00:00` | **PASS** |
| `effectiveTo` | model penceresi içinde | `2027-07-31T00:00:00+00:00` | **PASS** |
| Preview provenance | `previewRunId` eşleşmesi | `5936cddf-…` eşleşti | **PASS** |
| Rule provenance | `FU05-RULE-…` | aynı | **PASS** |
| Target node | `84012f9d-…` | aynı | **PASS** |
| Business scope | `beta` | `beta` | **PASS** |
| Model history | 1 | 1 | **PASS** |
| Account history | ≥1 | 1 | **PASS** |
| CoverageSummary | `hasCurrentCoverage=true` | true, 1 kayıt | **PASS** |
| **Account master** | değişmedi | `updatedAt` aynı, territory/zone alanı **0** | **PASS** |

Assignment id: `c237696d-f388-42b6-8773-b6a0d8b87ad4`.

---

## 8. Duplicate / Conflict Result

Aynı account + node + scope + pencere ile ikinci apply (`override=false`):

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| HTTP | **409** | 409 | **PASS** |
| Yeni kayıt | yok | model history 1 (değişmedi) | **PASS** |
| Coverage | değişmedi | 1 → 1 | **PASS** |

All-or-nothing korundu: çakışma tespit edildiğinde hiçbir satır yazılmadı.

---

## 9. Override Result

### 9.1 Reason'sız override

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| HTTP | 400/422 | **400** | **PASS** |
| Yazma | yok | model history 1 (değişmedi) | **PASS** |

### 9.2 Reason'lı override

Reason: *"FU05 smoke: manuel override ile yeniden atama."*

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| HTTP | 200 | 200 | **PASS** |
| `createdCount` | 1 | 1 | **PASS** |
| `endedCount` | ≥1 | 1 | **PASS** |
| Model history | 2 kayıt | 2 | **PASS** |
| Eski kayıt | **silinmedi**, `ended` | `ended`, history'de duruyor | **PASS** |
| Eski kayıt `effectiveTo` | set | `2026-07-31T00:00:00+00:00` | **PASS** |
| Yeni kayıt | active | `active` | **PASS** |
| `overrideReason` görünür | evet | tam metin | **PASS** |
| Account history | ≥2 | 2 | **PASS** |
| CoverageSummary | yalnız **yeni** kaydı gösteriyor | `d711269e-…` (eski id yok) | **PASS** |

---

## 10. End Result

Current assignment (`d711269e-…`) end edildi (endDate + reason):

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| HTTP | 200 | 200 | **PASS** |
| Status | `ended` | `ended` | **PASS** |
| `effectiveTo` | set | `2026-07-31T00:00:00+00:00` | **PASS** |
| History | korundu (2 kayıt) | 2 | **PASS** |
| CoverageSummary | bu modelden current yok | 0 | **PASS** |
| Hard delete | yok | yok | **PASS** |

---

## 11. History Result

| Kontrol | Gerçekleşen | Sonuç |
|---|---|---|
| Model-level history | 2 kayıt (override ile kapanan + end ile kapanan) — hepsi görünür | **PASS** |
| Account-level history | 2 kayıt | **PASS** |
| Silinen kayıt | yok — her iki kayıt da `ended` olarak duruyor | **PASS** |

---

## 12. Coverage Summary Result

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| Apply sonrası | current 1 | 1 | **PASS** |
| Override sonrası | yalnız yeni kayıt | yeni id, eski id yok | **PASS** |
| End sonrası | current yok | 0 | **PASS** |
| Geçmiş tarih (`effectiveAt = today-30`) | current yok | 0 | **PASS** |
| Account master'a territory alanı | **hiç yok** | 0 alan | **PASS** |

### 12.1 Bulgu — CoverageSummary model status'ünü filtrelemiyor

Smoke sırasında bir probe apply'ı, sonradan **inactive** yapılan bir modele bağlı kalmıştı. Model inactive olduğu
hâlde `territory-coverage-summary` o atamayı **hâlâ `current`** gösterdi (`hasCurrentCoverage=true`).

- **Yorum:** Coverage sorgusu assignment status + tarih penceresi üzerinden çalışıyor; **model lifecycle'ını
  dikkate almıyor.** Bir model deaktive edildiğinde ona bağlı account coverage'ı otomatik olarak düşmüyor.
- **Etki:** Deaktive edilmiş bir modelin coverage'ı MOD-0149 Account 360 ve ileride MOD-0155 readiness API'sine
  sızabilir.
- **Aksiyon:** Kod değiştirilmedi (bu task'ın kapsamı dışında). **FU09 / coverage read-model follow-up'ı** olarak
  kayda alınmalı: "current coverage, modelin stored status'ü `active` değilse dönmemeli — ya da açıkça
  `modelStatus` ile işaretlenmeli."
- **Temizlik:** Probe ataması FU05'in kendi `end` endpoint'iyle kapatıldı (hard delete yok); ilgili account
  (`ACC-2026-000017`) artık `hasCurrentCoverage=false`, history 1 `ended` kayıtla korundu.

---

## 13. UI Smoke Result

Web (5001) üzerinde tenant 97c5 oturumu ile **sunucu-render** doğrulaması.
*(Not: `tenantId` verilmeden yapılan Web login platform tenant `…0001`'e düşüyor; oturum `?tenantId=97c5…` ile açıldı.)*

### 13.1 Sayfa yüklemeleri (4/4)

| Sayfa | Sonuç |
|---|---|
| Model Details | 200 — "FU05 Smoke 20260731231147" |
| Assignment Rules | 200 |
| **Assignment Preview** | 200 |
| **Assignment History** | 200 |

### 13.2 Preview sayfası (FU05 apply yüzeyi)

| Kontrol | Sonuç |
|---|---|
| Matched Accounts grid `#dt-preview-matched` + `data-dt-standard="v2"` | **PASS** |
| Conflicts grid `#dt-preview-conflicts` | **PASS** |
| Rule Summary grid `#dt-preview-rules` | **PASS** |
| Apply yüzeyi — `#account-assignment-offcanvas` | **PASS** |
| Conflict policy alanı | **PASS** |
| Override reason alanı | **PASS** |
| Effective date alanları | **PASS** |
| `assignment-preview.js` + `account-assignments.js` | **PASS** |
| Aksiyonlar: `Run Preview`, `Apply selected`, `Apply`, `Clear`, `Cancel` | **PASS** — hepsi FU05 kapsamında |

### 13.3 History sayfası

| Kontrol | Sonuç |
|---|---|
| History grid `#dt-assignmenthistory` | **PASS** |
| `assignment-history.js` | **PASS** |

### 13.4 Kapsam dışı yüzeyler (olmaması gerekenler)

Preview ve History sayfalarında **hiçbiri yok**: workflow/approval (`submit-approval`, `approve`, `approval-trace`) ·
evidence export (`evidence-pack`) · model import/export · visit/route (MOD-0155) · brand/product master.
**6/6 PASS.**

### 13.5 UI veri yolu ve Account 360

| Kontrol | Gerçekleşen | Sonuç |
|---|---|---|
| `/CRM/TerritoryManagement/Models/{id}/AccountAssignments/Json` | `success=true`, 2 kayıt | **PASS** |
| Account 360 sayfası yükleniyor | 200 | **PASS** |
| Account formunda `TerritoryId`/`ZoneId`/`MicroZoneId` input'u | **yok** | **PASS** |

### 13.6 Doğrulanamayan (browser gerektiren)

Gerçek tarayıcı sürülmediği için istemci etkileşimi çalıştırılmadı: matched-accounts grid'inin satır render'ı,
checkbox seçimi → bulk bar → Apply offcanvas akışı, conflict/override uyarı toast'ları, apply sonrası grid
yenilenmesi. Markup, script yüklemesi, aksiyon envanteri ve besleyen proxy veri yolu doğrulandı.

---

## 14. Guard Checks

| Guard | Sonuç |
|---|---|
| Runtime code changed? | **No** |
| Backend/frontend changed? | **No** |
| Gateway changed? | **No** |
| RBAC seed/grant changed? | **No** |
| MOD-0048 publish changed? | **No** |
| Mongo hand-edit? | **No** |
| **Account master mutated?** | **No** — yalnız GET; `updatedAt` değişmedi, territory alanı yok |
| Contact mutated? | **No** |
| Resource assignment behavior changed? | **No** |
| FU04A / FU04B behavior changed? | **No** |
| Workflow/evidence/import-export opened? | **No** — `supportsWorkflowActivation` canlıda `false` |
| Visit/route implementation opened? | **No** |
| Brand/Product opened? | **No** |
| Hard delete used? | **No** — kapatmalar `ended` + `effectiveTo` |
| Direct 5061 business call? | **No** — yalnız `/health` |
| `TenantId` payload used? | **No** |
| Forbidden permission used? | **No** |

### 14.1 Bu koşuda yapılan lifecycle mutasyonları (tamamı kendi smoke artefaktlarım)

| İşlem | Gerekçe |
|---|---|
| `SMOKE-MOD0151-FU05-20260731230910` **deactivate** | Kendi ilk deneme modelim `tr + beta` slotunu tutuyordu; evidence koşusu için serbest bırakıldı. **Operasyonel model (`DENEME`, `SETONDA-AZ`, FU04B smoke) deaktive edilmedi.** |
| Probe assignment `b02c4adb-…` **end** | §12.1 temizliği; hard delete değil |

Tenant'ta kalan smoke artefaktları: `SMOKE-MOD0151-FU05-20260731230910` (inactive), `…231007` (draft, aktive
edilemedi), `…231147` (**active**, evidence modeli). Yasak `crm.territory.delete` kullanılmadığı için hiçbiri
silinmedi.

---

## 15. Created / Updated Files

| Dosya | Aksiyon |
|---|---|
| `docs/audits/mod-0151-fu05-account-assignment-apply-history-live-smoke-closeout-2026-07-31.md` | Created |

Kod, runtime, module pack, gateway, reference data, RBAC ve Mongo değiştirilmedi.

---

## 16. Final Verdict

### **PASS**

**90 kontrol / 90 PASS / 0 FAIL.** Task'ın PASS ölçütlerinin tamamı canlıda karşılandı:

- Contract flag'leri doğru (7/7, `supportsWorkflowActivation=false` dahil)
- Gateway-only çalışıldı; `:5061` yalnız health; payload'da `TenantId` yok
- **Matched preview row bulundu** — önceki FU05 blocker'ı kapandı
- Preview yan etkisiz (assignment sayısı ve coverage değişmedi)
- Apply başarılı; provenance (previewRunId + ruleCode), node, scope, effective window doğru
- Model ve account history doğru; CoverageSummary doğru
- Duplicate → **409**, yazma yok
- Override reason'sız → **400**, yazma yok; reason'lı → eski `ended`, yeni `active`, reason görünür, coverage yeni kaydı gösteriyor
- End → `ended` + `effectiveTo`, history korundu, coverage temizlendi
- Account master ve Contact değişmedi; Account'ta territory alanı yok
- UI smoke geçti (sayfalar, apply yüzeyi, history grid'i, kapsam dışı yüzeylerin yokluğu)
- Guardrail'lerin tamamı korundu; **kod/runtime/config değişikliği yok**

**FU05 implementation raporundaki PARTIAL gerekçesi ("canlı apply zinciri matched preview row olmadığı için
tamamlanamadı") kapanmıştır.**

### Kapsam dışı bırakılan / follow-up'a yazılan iki not

1. **CoverageSummary model status'ünü filtrelemiyor** (§12.1) — deaktive edilmiş modelin coverage'ı `current`
   kalıyor. Kod değiştirilmedi; **FU09 / coverage read-model follow-up'ı** olarak açılmalı.
2. **Açık uçlu (`effectiveTo=null`) apply reddediliyor** (§7.1) — modelin penceresi varsa bounded `effectiveTo`
   zorunlu. Davranış tutarlı ve fail-closed; API tüketicileri için dokümante edilmelidir.
3. **UI browser etkileşimi** doğrulanmadı (§13.6) — sunucu-render + veri yolu doğrulandı. Verdict'i düşürmüyor;
   FU04B'de olduğu gibi opsiyonel bir browser smoke ile kapatılabilir.

---

## 17. Next Recommended Prompt

```text
MOD-0151 FU06 — Workflow Approval + Controlled Activation Pack Authorization
```

Opsiyonel / düşük öncelik:

```text
MOD-0151 FU09-COVERAGE-SCOPE — CoverageSummary'nin model lifecycle'ını dikkate alması (deaktive/arşivlenmiş
modelin coverage'ı current dönmemeli veya modelStatus ile işaretlenmeli). Pack scope authorization gerektirir.
```

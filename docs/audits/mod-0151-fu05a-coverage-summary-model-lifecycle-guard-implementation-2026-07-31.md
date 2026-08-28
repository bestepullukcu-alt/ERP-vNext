# MOD-0151 FU05A — CoverageSummary Model Lifecycle Guard — Implementation

- **Rapor tarihi (dosya adı):** 2026-07-31 (FU05A pack authorization ile aynı seri)
- **Fiili çalıştırma tarihi:** 2026-08-01
- **Modül:** MOD-0151 — Territory Management (`Diten.CrmService`)
- **Task tipi:** Runtime implementation (backend read-projection guard) + gateway-only live smoke
- **Scope:** `FU05A-coverage-summary-model-lifecycle-guard`
- **Target tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
- **Verdict:** **PASS**

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| FU05A pack authorization PASS | ✅ `docs/audits/mod-0151-fu05a-coverage-summary-model-lifecycle-guard-pack-authorization-2026-07-31.md` |
| Pack runtime scope'ta FU05A var | ✅ `runtime_code_scope` içinde `FU05A-coverage-summary-model-lifecycle-guard` (additive) |
| FU05 live smoke closeout PASS | ✅ 90/90 — `mod-0151-fu05-account-assignment-apply-history-live-smoke-closeout-2026-07-31.md` |
| Bulgunun kaynağı | ✅ Aynı raporun **§12.1** — "CoverageSummary model status'ünü filtrelemiyor" |
| Bu task workflow / approval / ChangeRequest / MOD-0023 mi? | ❌ **Hayır** — hiçbiri açılmadı |
| Otorite sırası | Blueprint Excel > Module Pack > Domain Config > AGENTS.md > `.antigravity/rules/` |

İncelenen kaynaklar: MOD-0151 module pack (§22.2a), FU05A pack authorization raporu, FU05 live smoke closeout,
`AccountTerritoryAssignment` aggregate + repository + handler'ları, CoverageSummary query/handler, account-level
history query, `TerritoryModel` lifecycle mantığı (FU02B `TerritoryLifecycle` + deactivate/archive handler'ları),
FU05 testleri ve contract endpoint.

**Preflight bulgusu (bu task'ın dışında oluşmuş, düzeltildi):** `Diten.CrmService.Application.Tests` projesi
zaten **derlenmiyordu** — `FakeAccountTerritoryAssignmentRepo`, daha önce eklenen
`IAccountTerritoryAssignmentRepository.ListActiveByAccountIdsAsync` üyesini implemente etmiyordu (CS0535). Test
süiti çalıştırılamadığı için fake tamamlandı; davranış değişikliği değildir, yalnız seam'in Mongo repository ile
birebir aynı davranışı (status filtresi query'de, effective-window in-memory) taklit etmesi sağlandı.

---

## 2. Scope Confirmation

| Yapıldı | Yapılmadı (yasak listesi) |
|---|---|
| CoverageSummary current projection'ına model lifecycle guard | Workflow approval / controlled activation |
| Account list "current territory" kolonuna aynı guard | ChangeRequest / Change Approval Trace / MOD-0023 |
| Bulk model lookup seam (`ListByIdsAsync`) | Lifecycle guard dışında apply davranışı değişikliği |
| Contract flag + runtime scope + limitation | Assignment rule / preview davranışı değişikliği |
| 29 yeni FU05A testi | Resource assignment / FU04A / FU04B davranışı değişikliği |
| Gateway-only live smoke | Account master mutasyonu · Contact mutasyonu · `ContactTerritoryAssignment` |
| — | Evidence pack · import/export · visit/route planning · Brand Scope · Product/Brand master |
| — | Hard delete · Mongo hand-edit · RBAC seed/grant · MOD-0048 publish |
| — | `crm.territory.delete` · `crm.micro-zone.manage` · direct 5061 business call · TenantId payload |

---

## 3. CoverageSummary Lifecycle Gap

**Kusur (FU05, düzeltmeden önce):** `GetTerritoryCoverageSummaryHandler` current'ı yalnız **assignment satırından**
türetiyordu:

```csharp
.Where(a => a.AssignmentStatus == "active" && a.EffectiveFrom <= at && (a.EffectiveTo is null || a.EffectiveTo >= at))
```

Bağlı `TerritoryModel` hiç okunmuyordu. Sonuç: **deactivated / archived / superseded** bir modele bağlı
`AccountTerritoryAssignment` `hasCurrentCoverage=true` dönmeye devam ediyordu.

**İkinci sızıntı noktası (aynı kusur, ayrı yüzey):** `GetAccountListHandler.EnrichCurrentTerritoryAsync` — account
grid'inin "current territory" kolonu da aynı iki-koşullu olmayan mantığı kullanıyordu. Bu, "bu account şu an hangi
territory'de?" sorusunun kullanıcıya görünen ikinci cevabıdır, bu yüzden guard oraya da uygulandı.

**Etki alanı:** Account 360, "bu account'tan hangi MR sorumlu?", contact derived territory coverage (FU09),
FU09 Visit/Route Readiness API, MOD-0155 Visit Planning, MR'ın "benim account'larım / benim doktorlarım" listesi.

---

## 4. Implementation Summary

### 4.1 Tek doğruluk kaynağı — `TerritoryCoverageLifecyclePolicy`

Yeni `TerritoryCoverageLifecyclePolicy` (Application/Features/Territory/AccountAssignments), "current account
territory coverage" tanımının **tek** yeri:

| Üye | Anlamı |
|---|---|
| `IsModelCurrent(model, at)` | model null değil · soft-delete değil · `status == active` · effective window `at`'i kapsıyor |
| `IsAssignmentCurrent(assignment, at)` | soft-delete değil · `EndedAt is null` · `status == active` · window `at`'i kapsıyor |
| `IsCurrent(assignment, models, at)` | **iki kapı birden** |
| `FilterCurrent(...)` | aday kümesini current'a indirger (sıra korunur) |
| `ModelIdsOf(...)` | lookup için yüklenecek distinct model id'leri |

Sınıf **saf read projection**'dır: hiçbir yazma üyesi yoktur, assignment'ı sonlandırmaz, silmez.

### 4.2 Repository seam

`ITerritoryModelRepository.ListByIdsAsync(tenantId, ids, ct)` eklendi (tenant-scoped, soft-delete hariç, `In` filtresi).
Status ve effective-window değerlendirmesi **in-memory** yapılır — böylece hiçbir `DateTimeOffset` Mongo range
filtresine girmez (bilinen BSON-array parallel-array / instant-vs-date tuzağı).

### 4.3 Uygulandığı yerler

| Yüzey | Değişiklik |
|---|---|
| `GetTerritoryCoverageSummaryHandler` | Aday assignment'lar → owning model'ler yüklenir → `FilterCurrent` |
| `GetAccountListHandler.EnrichCurrentTerritoryAsync` | Aynı guard; grid kolonu artık inactive/archived modeli göstermez |
| `GetAccountTerritoryAssignmentHistoryHandler` | **Değiştirilmedi** — history bilerek filtresiz kalır |
| `ApplyAccountTerritoryAssignmentsHandler` | **Değiştirilmedi** — FU05 apply davranışı aynen korundu |

---

## 5. Current Coverage Policy

Bir assignment ancak **hepsi** sağlanınca current sayılır:

| # | Koşul | Nerede |
|---|---|---|
| 1 | TerritoryModel `status = active` | `IsModelCurrent` |
| 2 | TerritoryModel effective window `effectiveAt`'i kapsıyor | `IsModelCurrent` |
| 3 | TerritoryModel inactive/archived/superseded/deactivated **değil** | (1)'in doğal sonucu — `active` tek geçerli değer |
| 4 | TerritoryModel soft-delete değil | `IsModelCurrent` + repository filtresi |
| 5 | Assignment `status = active` (open) | `IsAssignmentCurrent` |
| 6 | Assignment effective window `effectiveAt`'i kapsıyor | `IsAssignmentCurrent` |
| 7 | Assignment soft-delete değil | `IsAssignmentCurrent` + repository filtresi |
| 8 | Assignment ended değil (`EndedAt is null`) | `IsAssignmentCurrent` |
| 9 | Tenant claim'den gelir (payload'dan değil) | `ITenantContext` |
| 10 | Account master mutate edilmez | `ITerritoryAccountReader`'ın yazma üyesi yok |

`territory-model-status` yayınlanmış sözlüğü **7 değerdir**: `draft · review · approved · active · inactive ·
superseded · archived`. Bunlardan **yalnız `active`** current coverage üretir; diğer 6'sı üretmez.

---

## 6. Historical Coverage Behavior

```text
History  : geçmiş + ended + inactive model + archived model + superseded model kayıtlarını gösterir.
Current  : yalnız active model + active assignment.
```

| Kural | Durum |
|---|---|
| History kayıtları silinmez | ✅ history query'sine hiç dokunulmadı |
| Assignment hard delete edilmez | ✅ kodda hard delete yok; canlıda doğrulandı |
| Model inactive/archive/superseded olunca assignment otomatik `ended` yapılmaz | ✅ lifecycle handler'ları assignment'a dokunmaz; canlıda `status=active, endedAt=null` kaldı |
| Current projection guard ile filtrelenir | ✅ tek mekanizma budur |

---

## 7. EffectiveAt Behavior

| Senaryo | Davranış |
|---|---|
| `effectiveAt` verilmezse | `DateTimeOffset.UtcNow` kullanılır (yanıtın `effectiveAt` alanında geri döner) |
| Geçmiş tarih | O tarihte **hem** model **hem** assignment effective ise current döner |
| Bugün | Yalnız bugün active olan model + assignment |
| Model o tarihte effective değil | Current dönmez |
| Assignment o tarihte effective değil | Current dönmez |

**Bilinçli karar — status versiyonlanmaz:** Model `status` alanı tarihsel olarak versiyonlanmadığı için (FU05A
scope'unda bir status-history aggregate'i yoktur), deaktive edilmiş bir model **her** `effectiveAt` için current
üretmez; geçmişte ne olduğu **history**'de durur. Bu, pack §22.2a'nın "stored status" kararıyla uyumludur ve testle
sabitlenmiştir (`Model_status_is_evaluated_as_stored_...`).

---

## 8. Contact Derived Coverage Readiness

- Contact için **doğrudan** `TerritoryAssignment` **oluşturulmadı**; `ContactTerritoryAssignment` aggregate'i
  **eklenmedi**. Contact hiçbir noktada mutate edilmedi.
- Korunan karar:

```text
Contact coverage = AccountContactLink → Account → current AccountTerritoryAssignment / CoverageSummary
```

- FU05A guard bu türetmenin **prerequisite**'idir: contact coverage account'ın current coverage'ından türeyeceği için,
  account current coverage'ı yanlışsa contact coverage'ı da yanlış olurdu. Guard sonrası account current coverage
  doğrulanmıştır → FU09 contact-derived coverage için okuma tabanı hazırdır.
- FU09 contact derived coverage endpoint'i bu task'ta **yapılmadı** (scope dışı).

---

## 9. Contract Flags

`GET {gateway}/api/crm/territory-management/contract` — canlı (tenant 97c5) yanıt:

| Flag | Beklenen | Gerçekleşen |
|---|---|---|
| `supportsAccountAssignmentApply` | true | **true** |
| `supportsAssignmentHistory` | true | **true** |
| `supportsCoverageSummary` | true | **true** |
| `supportsCoverageSummaryModelLifecycleGuard` | **true (yeni)** | **true** |
| `supportsResourceAssignmentPlanVsCurrent` | true | **true** |
| `supportsWorkflowActivation` | **false** | **false** |
| `supportsApprovalTrace` | false | **false** |
| `evidencePack` / `importExport` | false | **false / false** |

`runtimeScope` (canlı):

```text
FU01-territory-model-node-backend-only; FU02-territory-hierarchy-ui-viewer;
FU02A-country-business-unit-scope-selector-hardening; FU02B-lifecycle-computed-expiry-draft-soft-delete;
FU03-assignment-rules-and-preview; FU04-resource-assignments; FU05-account-assignment-apply-history;
FU05A-coverage-summary-model-lifecycle-guard
```

Yeni limitation satırı: *"current coverage requires an active territory model; assignments of an
inactive/archived/superseded model stay in history only"*. Workflow / approval readiness flag'i **eklenmedi**.

---

## 10. Tests

`dotnet test Diten.CrmService.Application.Tests` → **406 test / 401 PASS / 0 FAIL / 5 skipped**.
Bunun **29'u** yeni `TerritoryCoverageLifecycleFu05ATests` testidir (`--filter` ile ayrıca doğrulandı: 29/29 PASS).

### Coverage current

| Test | Sonuç |
|---|---|
| active model + active assignment → current döner | ✅ |
| `inactive` model + active assignment → current dönmez | ✅ |
| `archived` model + active assignment → current dönmez | ✅ |
| `superseded` model + active assignment → current dönmez | ✅ |
| `draft` / `review` / `approved` model → current dönmez | ✅ (3 ek theory case) |
| model window expired / future → current dönmez | ✅ |
| soft-deleted model → current dönmez | ✅ |
| active model + ended assignment → current dönmez | ✅ |
| active model + future assignment → current dönmez | ✅ |
| active model + expired assignment → current dönmez | ✅ |
| soft-deleted assignment → current dönmez | ✅ |
| iki model (biri archived) → yalnız active olan projekte edilir | ✅ |
| model'i olmayan (dangling) assignment → current dönmez | ✅ |
| cross-tenant model guard'ı geçmez | ✅ |

### History

| Test | Sonuç |
|---|---|
| inactive model assignment'ı history'de görünür | ✅ |
| archived model assignment'ı history'de görünür | ✅ |
| superseded model assignment'ı history'de görünür | ✅ |
| ended assignment history'de görünür | ✅ |
| history silinmez / hard delete yok / otomatik ended yok | ✅ (`Deactivating_the_model_never_deletes_or_ends_the_assignment` — deaktivasyon öncesi/sonrası satır snapshot'ı birebir eşit) |

### EffectiveAt

| Test | Sonuç |
|---|---|
| geçmiş `effectiveAt`, o tarihte active model + assignment varsa current döner | ✅ |
| geçmiş `effectiveAt`, model window o tarihi kapsamıyorsa current dönmez | ✅ |
| geçmiş `effectiveAt`, assignment henüz başlamamışsa current dönmez | ✅ |
| bugün, inactive/archived model current dönmez | ✅ |
| `effectiveAt` verilmezse now kullanılır | ✅ |

### Boundary

| Test | Sonuç |
|---|---|
| Account master territory alanı almaz / mutate edilmez | ✅ (`Coverage_query_never_mutates_the_account_...`) |
| Contact mutate edilmez · `ContactTerritoryAssignment` yok | ✅ (seam'de yazma üyesi yok, aggregate mevcut değil — derlenemez) |
| Account grid kolonu aynı guard'ı uygular | ✅ (`Account_list_current_territory_column_applies_the_same_guard`) |
| FU05 apply davranışı bozulmadı | ✅ mevcut `AccountTerritoryAssignmentTests` (apply/override/conflict/end) aynen PASS |
| FU04A / FU04B davranışı bozulmadı | ✅ `TerritoryResourceAssignmentFu04ATests`, `TerritoryPlanVsCurrentFu04BTests` PASS |
| workflow / approval / ChangeRequest yok | ✅ contract testi `supportsWorkflowActivation=false` iddiasını korur |
| Contract: `supportsCoverageSummaryModelLifecycleGuard=true`, runtimeScope FU05A | ✅ `TerritoryContractTests` |

---

## 11. Gateway-only Live Smoke

Tüm business trafiği **Gateway 5000** üzerinden yürütüldü. `:5061`'e **yalnız `/health`** çağrıldı.
Hiçbir payload'da `tenantId` alanı gönderilmedi; tenant JWT claim'i + `X-Tenant-Id` header'ı ile taşındı.

### 11.1 Fleet health

| Servis | Port | Sonuç |
|---|---|---|
| Gateway | 5000 | **200** |
| Web | 5001 | **302 → login** (ayakta) |
| Auth | 5056 | **200** |
| Platform | 5057 | **200** |
| CRM | 5061 | **200** (yalnız `/health`) |

Deploy doğrulaması: çalışan `Diten.CrmService.Application.dll` içinde `TerritoryCoverageLifecyclePolicy`,
`FU05A-coverage-summary-model-lifecycle-guard` runtime scope'u ve yeni limitation string'i mevcut (watch rebuild).

### 11.2 Authenticated tenant

| Kontrol | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|
| Login | `POST {gateway}/api/tenant-auth/login` + `X-Tenant-Id: 97c5…` | **200** | **PASS** |
| Tenant claim | `97c59330-dbc4-4665-b29c-0c26dbb5cc93` | aynı | **PASS** |
| `crm.territory.*` permission | ≥1 | `read`, `model.read`, `model.manage`, `node.read`, `node.manage` (5/5) | **PASS** |
| `crm.account.read` | mevcut | mevcut | **PASS** |
| Yasak `crm.territory.delete` | yok | **yok** | **PASS** |
| Yasak `crm.micro-zone.manage` | yok | **yok** | **PASS** |

*(Kimlik bilgisi yalnız bellekte kullanıldı; token/parola hiçbir dosyaya, log'a veya bu rapora yazılmadı.)*

### 11.3 Contract flags (canlı)

§9 tablosu — **8/8 PASS**, `supportsCoverageSummaryModelLifecycleGuard=true`, `supportsWorkflowActivation=false`.

### 11.4 Smoke data

Yeni, ayrılmış smoke fixture (mevcut FU05 kanıt modeline dokunulmadı):

| Nesne | Değer |
|---|---|
| Model 1 | `bbc309ff-f17f-4d64-8944-439eb29e55ba` — `SMOKE-MOD0151-FU05A-20260801223620` (tr · business-unit alpha+gamma · 2026-07-01→2027-06-30) |
| Node 1 | `3aa20231-7356-4475-90f4-52a03c0d7755` — `FU05A-ZONE-20260801223620` (zone, active) |
| Model 2 | `92beb74c-376a-463c-809b-47b3d9702388` — `SMOKE-MOD0151-FU05A-G-20260801223835` |
| Node 2 | `7f817110-b79c-4c90-b95c-b8c325cb2ed0` — `FU05A-GRID-20260801223835` |
| Account | `25464183-…d56063` — `ACC-2026-000016` (Büyükşehir Eczanesi) |
| Assignment 1 | `a42f6277-9696-411e-8da9-2e2d393de85d` (model 1, active, 2026-07-15→2027-06-30) |
| Assignment 2 | `764039a1-3d2e-4848-ba4b-6748277b8670` (model 2, active) |

Başlangıç durumu: account'ın `hasCurrentCoverage=false` (mevcut 2 kaydı `ended`), account master snapshot alındı.

### 11.5 Model lifecycle guard — ana senaryo

| Adım | Model status | CoverageSummary | History | Assignment satırı | Sonuç |
|---|---|---|---|---|---|
| 1 — apply sonrası | `active` | **current=true**, 1 kayıt (`FU05A-ZONE-…`) | 3 kayıt | `active`, `endedAt=null` | **PASS** |
| 2 — `POST …/deactivate` | `inactive` | **current=false**, 0 kayıt | 3 kayıt (aynı) | `active`, `endedAt=null` (**değişmedi**) | **PASS** |
| 3 — `POST …/archive` | `archived` | **current=false**, 0 kayıt | 3 kayıt (aynı) | `active`, `endedAt=null` (**değişmedi**) | **PASS** |

Bu, FU05 closeout §12.1'de raporlanan kusurun **birebir tekrar üretimi ve kapanışıdır**: aynı senaryoda eski kod
`hasCurrentCoverage=true` dönüyordu, yeni kod `false` dönüyor ve history bozulmuyor.

### 11.6 Archived / superseded davranışı

| Durum | Doğrulama şekli | Sonuç |
|---|---|---|
| `inactive` | **canlı** (adım 2) | **PASS** |
| `archived` | **canlı** (adım 3) | **PASS** |
| `superseded` | **automated test** — `superseded` `territory-model-status` sözlüğünde yayınlıdır ancak onu yazan lifecycle komutu **FU06** kapsamındadır (bugün endpoint yok). Guard `active` dışındaki **her** değeri reddettiği için davranış aynıdır. | **PASS (test-only)** |
| `draft` / `review` / `approved` | automated test | **PASS (test-only)** |

### 11.7 EffectiveAt (canlı)

| `effectiveAt` | Model durumu | Beklenen | Gerçekleşen | Sonuç |
|---|---|---|---|---|
| (yok → now) | active | current=true | **true**, 1 kayıt | **PASS** |
| `2026-07-20` (geçmiş, iki pencere de kapsıyor) | active | current=true | **true**, 1 kayıt | **PASS** |
| `2026-07-10` (assignment henüz başlamamış) | active | current=false | **false** | **PASS** |
| `2026-06-15` (model penceresi öncesi) | active | current=false | **false** | **PASS** |
| `2027-08-01` (iki pencere de bitmiş) | active | current=false | **false** | **PASS** |
| `2026-07-20` | **inactive** | current=false | **false** | **PASS** |

### 11.8 Account grid "current territory" kolonu (canlı)

| Adım | Model 2 status | `GET /api/crm/accounts` satırı | Sonuç |
|---|---|---|---|
| apply sonrası | `active` | `territoryNodeCode = FU05A-GRID-…`, `territoryNodeName = FU05A Grid Zone` | **PASS** |
| deactivate sonrası | `inactive` | `territoryNodeCode = null`, `territoryNodeName = null` | **PASS** |

Ayrıca çapraz kontrol: model 1 archived + model 2 active iken CoverageSummary **yalnız** model 2'nin node'unu
döndürdü (archived modelin `active` statülü assignment'ı sızmadı).

### 11.9 Account master

| Kontrol | Sonuç |
|---|---|
| Account detay yanıtı smoke öncesi/sonrası **byte-identical** | **PASS** |
| Account master'da territory/zone/MR alanı | **0 alan** (`id, accountName, accountCode, accountType, accountCategory, parentAccountId, status, countryRef, cityRef, districtRef, addressLine, latitude, longitude, responsiblePerson*, notes, createdAt, updatedAt, externalReferences, attributes, logoDataUri`) | 
| `updatedAt` değişimi | yok (null → null) | **PASS** |

---

## 12. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Workflow approval eklendi mi? | **Hayır** |
| Controlled activation eklendi mi? | **Hayır** |
| ChangeRequest / Change Approval Trace eklendi mi? | **Hayır** |
| MOD-0023 entegrasyonu eklendi mi? | **Hayır** |
| `supportsWorkflowActivation` false kaldı mı? | **Evet** |
| FU05 apply davranışı değişti mi? | **Hayır** (handler'a dokunulmadı; testleri PASS) |
| Assignment rule / preview davranışı değişti mi? | **Hayır** |
| Resource assignment davranışı değişti mi? | **Hayır** |
| FU04A replacement/transfer değişti mi? | **Hayır** |
| FU04B Plan vs Current değişti mi? | **Hayır** |
| Account master mutate edildi mi? | **Hayır** (canlıda byte-identical) |
| Contact mutate edildi mi? | **Hayır** (hiç dokunulmadı) |
| `ContactTerritoryAssignment` eklendi mi? | **Hayır** |
| Evidence pack / import-export / visit-route eklendi mi? | **Hayır** |
| Brand Scope / Product / Brand master eklendi mi? | **Hayır** |
| Hard delete yapıldı mı? | **Hayır** (kodda yok; canlıda tüm satırlar duruyor) |
| History silindi mi? | **Hayır** (4 kayıt korunuyor) |
| Assignment otomatik `ended` yapıldı mı? | **Hayır** (`status=active`, `endedAt=null` kaldı) |
| Mongo hand-edit yapıldı mı? | **Hayır** |
| RBAC seed/grant değişti mi? | **Hayır** |
| MOD-0048 publish değişti mi? | **Hayır** |
| `crm.territory.delete` kullanıldı/açıldı mı? | **Hayır** (token'da yok) |
| `crm.micro-zone.manage` kullanıldı/açıldı mı? | **Hayır** (token'da yok) |
| Direct 5061 business API çağrısı yapıldı mı? | **Hayır** (yalnız `/health`) |
| Payload'da `TenantId` gönderildi mi? | **Hayır** (claim + `X-Tenant-Id` header) |
| Yeni sayfa yapıldı mı? | **Hayır** (backend/read-model guard) |

---

## 13. Created / Updated Files

### Created

| Dosya | İçerik |
|---|---|
| `services/Diten.CrmService/src/Diten.CrmService.Application/Features/Territory/AccountAssignments/TerritoryCoverageLifecyclePolicy.cs` | Current coverage'ın tek tanımı (model gate + assignment gate + filter/lookup helper'ları) |
| `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Territory/TerritoryCoverageLifecycleFu05ATests.cs` | 29 FU05A testi (current / history / effectiveAt / boundary / grid parity) |
| `docs/audits/mod-0151-fu05a-coverage-summary-model-lifecycle-guard-implementation-2026-07-31.md` | Bu rapor |

### Updated

| Dosya | Değişiklik |
|---|---|
| `…/Diten.CrmService.Domain/Repositories/ITerritoryModelRepository.cs` | `ListByIdsAsync` seam'i (bulk model lookup) |
| `…/Diten.CrmService.Persistence/Repositories/TerritoryModelRepository.cs` | `ListByIdsAsync` Mongo implementasyonu (`In` filtresi, tarih filtresi **yok**) |
| `…/Application/Features/Territory/AccountAssignments/Handlers/AccountTerritoryAssignmentQueryHandlers.cs` | CoverageSummary handler'ı model lookup + iki kapılı guard kullanır |
| `…/Application/Features/Account/Handlers/QueryHandlers/GetAccountListHandler.cs` | Grid "current territory" kolonuna aynı guard |
| `…/Application/Features/Territory/Contract/TerritoryContractDto.cs` | `SupportsCoverageSummaryModelLifecycleGuard` flag'i (additive, true) |
| `…/Application/Features/Territory/Contract/GetTerritoryContractHandler.cs` | RuntimeScope'a FU05A; yeni limitation satırı |
| `frontend/Diten.Web/Models/CRM/TerritoryViewModels.cs` | Contract view-model paritesi (yeni flag alanı) |
| `…/tests/…/Territory/FakeTerritoryInfrastructure.cs` | `ListByIdsAsync` fake'i + eksik `ListActiveByAccountIdsAsync` (önceden var olan derleme kırığının düzeltmesi) |
| `…/tests/…/Territory/AccountTerritoryAssignmentTests.cs` | CoverageSummary handler'ının yeni bağımlılığına uyarlama (davranış iddiaları aynen korundu) |
| `…/tests/…/Territory/TerritoryContractTests.cs` | Yeni flag + FU05A runtime scope iddiaları |

---

## 14. Final Verdict

**PASS**

- CoverageSummary model lifecycle guard uygulandı; current coverage yalnız **active model + active assignment**
  kesişiminden üretiliyor.
- `inactive` ve `archived` modeller **canlıda** current dönmüyor; `superseded` / `draft` / `review` / `approved`
  automated testlerle doğrulandı (bu statüleri yazan lifecycle komutu FU06 kapsamındadır).
- History korunuyor; hard delete yok; assignment otomatik `ended` yapılmıyor.
- Account master mutate edilmedi (canlıda byte-identical); Contact'a dokunulmadı; `ContactTerritoryAssignment` yok.
- `effectiveAt` davranışı (default now / geçmiş tarih / pencere sınırları) canlıda doğrulandı.
- Contract flag'i doğru: `supportsCoverageSummaryModelLifecycleGuard=true`, `supportsWorkflowActivation=false`
  korundu, workflow/approval flag'i eklenmedi.
- Build PASS · Tests **401/401 PASS (5 skipped)**, 29'u yeni FU05A testi · Gateway-only live smoke PASS.
- Ek kazanım: test projesindeki mevcut derleme kırığı (`ListActiveByAccountIdsAsync` implemente edilmemiş) giderildi,
  süit yeniden çalıştırılabilir hâle geldi.
- Ek kapsama: aynı kusurun ikinci yüzeyi olan account grid "current territory" kolonu da guard'landı.

**Açık follow-up'lar (bu task'ın kapsamı dışında, değiştirilmedi):**

- `FU05-RBAC` — `crm.territory.assignment.*` permission katalog hizalaması (FU05 endpoint'leri hâlâ
  `crm.territory.model.read/manage` fallback'ini kullanıyor).
- FU06 — `superseded` lifecycle komutu; canlı superseded doğrulaması ancak o zaman mümkün olacak.
- FU09 — contact derived coverage endpoint'i (prerequisite artık hazır).

---

## 15. Next Recommended Prompt

```
@orchestrator MOD-0151 FU08 — Import/Export Hardening Pack Authorization
```

# MOD-0151 FU08 — Import/Export Hardening — Implementation

- **Tarih:** 2026-08-01
- **Modül:** MOD-0151 — Territory Management (`Diten.CrmService`)
- **Task tipi:** Runtime implementation (backend + frontend) + gateway-only live smoke
- **Scope:** `FU08-import-export-hardening`
- **Referans:** `docs/audits/mod-0151-fu08-import-export-hardening-pack-authorization-2026-08-01.md` (PASS)
- **Target tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
- **Verdict:** **PASS**

---

## 1. Preflight

| Kontrol | Sonuç |
|---|---|
| FU08 pack authorization PASS | ✅ pack §22.5 + §7.5b + §17 FU08 permission kararı |
| Pack `runtime_code_scope` içinde FU08 | ✅ `FU08-import-export-hardening` |
| MOD-0150 Contact Import/Export deseni incelendi | ✅ `mod-0150-contact-import-export-task2-xlsx-upload-dryrun-apply.md` + `Features/ImportExport/Xlsx/*` |
| Mevcut XLSX altyapısı (ClosedXML 0.102.2) yeniden kullanıldı | ✅ **yeni import framework yazılmadı** |
| FU03 rule/preview · FU05 apply/history · FU05A coverage guard · FU04A/FU04B read model'leri incelendi | ✅ |
| Bu task workflow / approval / ChangeRequest / MOD-0023 / visit-route / campaign mı? | ❌ **Hiçbiri** |

**Endpoint yolu kararı (prompt'tan sapma, gerekçeli):** prompt `/api/crm/territory-import-export/import-file` yolunu
öneriyordu. Bu yol mevcut Gateway wildcard'larının hiçbirine düşmüyor ve `ocelot.json`'a **yeni route** eklemeyi
gerektirirdi; pack §19 bu dosyayı **protected** ilan ediyor ve MOD-0150 aynı durumda "no new Gateway route" kararını
vermişti. Bu yüzden FU08 endpoint'leri pack §19'un zaten önerdiği model-scoped yollara kondu ve **mevcut**
`/api/crm/territory-models/{everything}` wildcard'ı üzerinden çalışıyor — **gateway hiç değiştirilmedi**:

```text
GET  /api/crm/territory-models/{id}/export
GET  /api/crm/territory-models/{id}/import-template
POST /api/crm/territory-models/{id}/import-file?dryRun=true   (varsayılan dryRun=true)
POST /api/crm/territory-models/{id}/import-file/apply         (AYRI rota)
GET  /api/crm/territory-models/{id}/import-runs
```

---

## 2. Implementation Summary

**Temel mimari kural uygulandı:** *import bir taşıma yoludur, ikinci bir business-rule engine değildir.*
Her satır UI'dan girilmiş gibi mevcut guard'lardan geçer; validator kendi paralel kurallarını koymaz.

| Katman | Eklenen |
|---|---|
| Domain | `TerritoryImportRun` (+`TerritoryImportRunResult`, `TerritoryImportRunSheetCount`) · `ITerritoryImportRunRepository` (**insert + 2 read; update/delete üyesi YOK**) |
| Persistence | `TerritoryImportRunRepository` · class map (`TerritoryModelId` string-Guid) · 2 index (`UploadedAt` DateTimeOffset olduğu için **hiçbir key'e konmadı**, sıralama in-memory) · DI |
| Application | `TerritoryWorkbookSchema` (sheet/kolon/operasyon/error-code sözleşmesi) · `TerritoryWorkbookBuilder` (export+template tek writer) · `TerritoryWorkbookReader` · `TerritoryImportContext` + `TerritoryImportValues` · `TerritoryImportValidator` (dry-run planı) · `TerritoryImportEngine` (parse→validate→apply→record) · contracts + 3 handler |
| API | `TerritoryModelsController`'a 5 endpoint (10 MB `RequestSizeLimit`, `.xlsx` zorunlu) |
| Contract | 5 additive flag + runtime scope + 6 yeni limitation satırı |
| Frontend | `ImportExportPage` + 5 proxy action · `ImportExport.cshtml` · `import-export.js` · 8 view model · Details sayfasına giriş butonu · **47 anahtar × 7 dil** RESX |
| Tests | 45 FU08 testi + 3 scope-guard testi + contract iddiaları |

---

## 3. Export

`GET {gateway}/api/crm/territory-models/{id}/export` → XLSX, **read-only**.

| Sheet | İçerik |
|---|---|
| `ValidationNotes` | Kurallar, dry-run-first uyarısı, sheet-bazlı ne yapılabilir tablosu, `<CLEAR>` semantiği, TenantId uyarısı, reference-data durumu |
| `Model` | Model metadata (ModelId/Code/Name/CountryScope/BusinessUnitScopes/pencere/Status) |
| `Nodes` | Node'lar + **hiyerarşi** (`ParentTerritoryCode` ile, id ile değil) + level + geo + pencere + SortOrder |
| `AssignmentRules` | Rule tanımı + hedef node kodu + conflict policy + kriterler |
| `AccountAssignments` | **current + history** (ended kayıtlar dahil, `AssignmentStatus`/`EndedAt` ile) |
| `ResourceAssignments` | **current + history** (position/resource/scope/pencere/status) |
| `CoverageSummary` | **Export-only.** FU05A guard'ının aynı kuralı (active model + açık assignment) uygulanır |
| `PlanVsCurrent` | **Export-only.** FU04B `TerritoryPlanVsCurrentEngine.Compute` **yeniden kullanılır** — sheet, tab'dan sapamaz |
| `ReferenceValues` | Canlı MOD-0048 published değerleri; yayınlanmamış set `NOT_PUBLISHED` işaretlenir, **asla local liste konmaz** |

Kurallar: tenant claim'den çözülür · çıktıda **`TenantId` kolonu yok** · Gateway üzerinden · Account/Contact master
okunur, mutate edilmez · her hücre TEXT formatında (GUID/ISO tarih/başında sıfırlı kod Excel'de bozulmaz).

---

## 4. Template

`GET .../import-template` → doldurulabilir XLSX, **tam 7 sheet**:
`ValidationNotes · Model · Nodes · AssignmentRules · AccountAssignments · ResourceAssignments · ReferenceValues`.

İçerik: required kolonlar · kabul edilen değerler (in-cell dropdown, canlı MOD-0048 aralığından) · reference-data
ipuçları · validation kuralı açıklamaları · **resource assignment apply desteklenmiyor** uyarısı ·
**CoverageSummary / Plan vs Current import edilemez** uyarısı · **TenantId Excel'de kullanılmaz** uyarısı.

Export ve template **aynı writer**'dan çıkar → bir export dosyası reader'a kolon-kolon geri girer.

---

## 5. Dry-Run

`POST .../import-file?dryRun=true` — **varsayılan `dryRun=true`**, yani flag'i unutan bir çağrı yalnızca önizleyebilir.

**Dry-run hiçbir şey persist etmez:** model/node/rule/assignment yazımı yok, coverage değişmez ve **`TerritoryImportRun`
satırı da yazılmaz** (testle ve canlıda ayrı ayrı kanıtlandı).

**Satır sözleşmesi:** `Sheet · RowNumber` (gerçek Excel satırı) `· Severity · ErrorCode` (stabil, makine-okunur)
`· Message · SuggestedFix · Blocking · Operation · EntityType · ResolvedKey · ChangedFields · Status`.
**Özet sayaçları:** creates · updates · ends · skips · errors · conflicts · warnings.

**Uygulanan kontroller** (hepsi `TerritoryImportErrorCodes` altında stabil kodlarla):

| Kategori | Kodlar |
|---|---|
| Yapısal | `required_field_missing` · unknown/duplicate kolon (file warning/error) · `invalid_data_type` · boş satır (sessiz atlanır) · eksik zorunlu sheet · bozuk/parolalı dosya (file error, exception metni **sızdırılmaz**) |
| Node/hiyerarşi | `duplicate_node_code` · `duplicate_row` · `invalid_parent` · `hierarchy_cycle` · `invalid_territory_level` · `level_order_violation` |
| Scope | `invalid_business_unit_scope` · `model_scope_overflow` · `invalid_country_scope` |
| Rule | `invalid_rule_type` · `invalid_conflict_policy` · `invalid_target_node` |
| Account | `invalid_account` · `unresolved_account_reference` · `cross_tenant_account` |
| Resource (yalnız dry-run) | `resource_apply_not_supported` (+ satırın FU08A'da da düşeceği sorunlar bilgi olarak) |
| Tarih | `window_containment` (assignment ⊆ node ⊆ model) · `invalid_date_window` |
| Lifecycle | `active_model_overlap` (**uyarı**, bloklamaz — activation zaten reddeder) · `model_not_editable` · `model_not_active` |
| Reference data | `reference_set_not_published` → **fail-closed** (hem satır hem dosya seviyesinde) |
| Tenant | `TenantId` kolonu **yok sayılır + file warning** |

**Blocking / non-blocking ayrımı** her satırda ayrı bir alan; blocking satır hiçbir koşulda apply edilmez,
non-blocking uyarı apply'ı tek başına bloklamaz ama raporlanır ve run history'de sayılır.

---

## 6. Safe Apply

`POST .../import-file/apply` — **ayrı rota**, `dryRun` parametresi yok.

| Karar | Uygulama |
|---|---|
| Dry-run olmadan apply | Backend her apply'da **aynı validation pass'i yeniden çalıştırır**; UI ayrıca o dosya için `canApply` dönmüş bir dry-run olmadan Apply'ı **etkinleştirmez** (dosya değişirse onay düşer) |
| Yıkıcı çağrının kazara tetiklenmesi | Farklı rota + `dryRun` varsayılanı true → bir "önizleme" isteği asla yazamaz |
| Genel motor | validate-all → apply |
| `Model`/`Nodes`/`AssignmentRules` | **Sheet-level all-or-nothing** — tek blocking satır o sheet'in tamamını yazılmamış bırakır ve yazılacak satırlar `not_applied` + `sheet_blocked` olarak yeniden etiketlenir |
| `AccountAssignments` | **Batch-level all-or-nothing** (FU05 §22.2 policy #2 ile birebir) |
| `ResourceAssignments` | Apply yolu **yok** → `resource_apply_not_supported` (blocking) |
| Strict mode | Herhangi bir blocking satır → **dosya-seviyesi** hiçbir şey yazılmaz |
| Sessiz partial apply | **Yasak.** Her sheet için `applied` + `notAppliedReason` raporlanır ve run history'ye yazılır |
| Hata oranı eşiği | Blocking oran > **%20** → "yanlış dosya/eski şablon" olarak apply bloklanır |
| Uygulanacak satır yok | Apply bloklanır ("nothing to apply") — yanıltıcı başarı gösterilmez |
| Reference set eksik | Fail-closed |
| Hard delete | **Yok.** `delete` → `unsupported_operation`; kapatma yalnız `end` semantiğiyle |
| Boş hücre / `<CLEAR>` | Boş = **değiştirme**; `<CLEAR>` = açık temizleme; zorunlu alan temizlenemez |
| Immutable alan | `ModelCode`, yanlış `ModelId`/`NodeId` → `immutable_field` |
| Idempotency | Doğal anahtar (model+code / model+account+scope+pencere); aynı dosya ikinci kez → `no_change` skip veya controlled conflict |
| Provenance | Yazılan/kapatılan her kayıtta `ImportRunId` yerine geçen `CorrelationId` + run kaydında **source file hash** |
| Raw file | **Saklanmaz** — yalnız SHA-256 |
| `TenantId` | Payload'da yok; claim'den. Excel kolonu varsa ignore + warning |
| Atomiklik | Account assignment yazımı FU05'in `CommitApplyAsync` yolunu (standalone Mongo compensation fallback dahil) kullanır; sheet yazımı başarısız olursa yalnız o sheet `not_applied` olur ve raporlanır |

---

## 7. Account Assignment Import

Import satırı FU05 apply guard setinin **aynısından** geçer:

| FU05 kuralı | FU08'de |
|---|---|
| Yalnız `active` model | ✅ `model_not_active` (draft modelde canlıda doğrulandı) |
| Batch all-or-nothing | ✅ tek kötü satır tüm batch'i yazılmamış bıraktı (canlı) |
| Kesişen scope + örtüşen pencere → conflict | ✅ `duplicate_assignment`; birebir aynı satır → `no_change` (duplicate üretmez) |
| Override yalnız non-empty reason ile | ✅ `override_reason_required` |
| Eski kayıt silinmez, `ended`+`EffectiveTo`/`EndedAt` ile kapanır | ✅ testle kanıtlandı (2 kayıt kalır, biri `ended`) |
| Pencere ⊆ node ⊆ model | ✅ `window_containment` |
| Scope model scope'unu aşamaz | ✅ `model_scope_overflow` |
| `AppliedRuleId`/`AppliedRuleCode` provenance | ✅ rule kodu çözülürse taşınır, çözülmezse temizlenir |
| Manuel import satırı işareti | ✅ `AssignmentSource=import` (override satırı `override` kalır) |
| FU05A coverage guard | ✅ bozulmadı — canlıda apply sonrası `hasCurrentCoverage=true`, kaynak `import` |
| Account master | ✅ **byte-identical** (canlı) |

---

## 8. Resource Assignment Boundary

- `ResourceAssignments`: **export ✅ · template ✅ · dry-run ✅ · apply ❌**
- Apply denemesi → `resource_apply_not_supported` (blocking) + satırın FU08A'da da düşeceği sorunlar bilgi olarak.
- Contract: **`supportsResourceAssignmentImportApply=false`**.
- **Yapısal sınır:** validator'da bu sheet için `Action` üretilmez, controller'da resource-import rotası yoktur ve
  scope-guard testi böyle bir rota eklenmesini engeller. FU04A `proposed`/`active` ayrımı, replacement ve transfer
  sözleşmesi bir Excel satırıyla bypass edilemez.
- **Follow-up: `MOD-0151 FU08A — Resource Assignment Import Apply`** (pack §23 F20).

---

## 9. Import Run History

`TerritoryImportRun` — **append-only** (pack §7.5b):
`ImportRunId · TenantId` (server-resolved) `· TerritoryModelId · ModelCode · FileName · FileHash · UploadedBy ·
UploadedAt · Status · DryRunResult` (özet + sheet outcome) `· AppliedAt · AppliedBy · CorrelationId ·
SheetCounts` (total/created/updated/ended/skipped) `· ErrorCount · WarningCount`.

| Kural | Durum |
|---|---|
| Yalnız **apply** run kaydı yazar | ✅ dry-run 0 kayıt (canlı + test) |
| Bloklanan apply da kaydedilir (ama hiçbir şey yazılmaz) | ✅ `status=blocked` |
| Update/delete komutu yok | ✅ interface'te üye yok — **reflection testiyle** sabitlendi |
| Raw file saklanmaz | ✅ yalnız SHA-256 (64 hex) |
| Approval/evidence artefaktı değil | ✅ FU06/FU07 sahipliği değişmedi |

---

## 10. UI

`/CRM/TerritoryManagement/Models/{id}/ImportExport` (Details sayfasından buton).

Akış: **Export XLSX** · **Download template** · dosya yükle · **Run dry-run** → sonuç tablosu (sheet özeti +
satır tablosu, blocking satırlar kırmızı, `SuggestedFix` ayrı kolon, "yalnız blocking göster" filtresi) ·
**Apply** onayı (`showConfirm`) · **Import run history** listesi.

Güvenlik davranışı: Apply butonu, **o dosya için** `canApply` dönmüş bir dry-run olmadan pasif; dosya veya strict-mode
değişirse onay düşer; apply sonrası tekrar dry-run gerekir. Dry-run proxy `model.read`, apply proxy `model.manage`
ister (fail-closed). Tüm çağrılar Web proxy → Gateway; tarayıcı `:5061`'e gitmez ve `TenantId` göndermez.

UI'da **yok**: workflow · approval · ChangeRequest · Evidence Pack · visit/route · campaign/frequency ·
Brand/Product · contact mutasyonu. Sayfada görülen tek "workflow" kelimesi mevcut MOD-0023 sidebar menü öğesidir.

7 dil RESX paritesi: **276 anahtar × 7 dosya**, duplicate yok, XML geçerli (+47 yeni FU08 anahtarı).

---

## 11. Permissions

Canonical hedefler `crm.territory.export` / `crm.territory.import` **tanımlandı** (`TerritoryPermissions.Export/Import`)
ama RBAC kataloğunda olmadıkları için `TerritoryPermissions.All`'a **eklenmedi** — contract sahip olunmayan bir
yeteneği ilan etmez. Pack §22.5 fallback'i uygulandı:

| Endpoint | Permission (fallback) |
|---|---|
| export · import-template · import-runs | `crm.territory.model.read` |
| import-file (dry-run) · import-file/apply | `crm.territory.model.manage` |

Fallback **yetki genişletmez**: dosya account/resource sheet'i içeriyorsa ilgili FU05 / FU04A guard'ları yine çalışır.
Runtime seed/grant **değiştirilmedi**. **Follow-up: `MOD-0151 FU08-RBAC — Import/Export Permission Catalog Alignment`**.

---

## 12. Contract Flags

Canlı yanıt (tenant 97c5, Gateway):

| Flag | Beklenen | Gerçekleşen |
|---|---|---|
| `supportsTerritoryExport` | true | **true** |
| `supportsTerritoryImportExport` | true | **true** |
| `supportsTerritoryImportDryRun` | true | **true** |
| `supportsTerritoryImportApply` | true | **true** |
| `supportsResourceAssignmentImportApply` | **false** | **false** |
| `supportsWorkflowActivation` | **false** | **false** |
| `supportsAssignmentRules` / `supportsAssignmentPreview` | true | **true / true** |
| `supportsAccountAssignmentApply` / `supportsAssignmentHistory` | true | **true / true** |
| `supportsCoverageSummary` / `supportsCoverageSummaryModelLifecycleGuard` | true | **true / true** |
| `supportsResourceAssignments` / `supportsResourceAssignmentPlanVsCurrent` | true | **true / true** |

`runtimeScope` sonu: `… FU05A-coverage-summary-model-lifecycle-guard; **FU08-import-export-hardening**`.

---

## 13. Tests

`dotnet test Diten.CrmService.Application.Tests` → **454 test / 449 PASS / 0 FAIL / 5 skipped**.
Bunun **45'i** yeni `TerritoryImportExportFu08Tests`, **3'ü** yeni scope-guard testi.

### Contract
`supportsTerritoryExport/ImportExport/ImportDryRun/ImportApply=true`, `supportsResourceAssignmentImportApply=false`,
`supportsWorkflowActivation=false`, runtimeScope FU08 içerir — hepsi ✅.

### Dry-run
dry-run **hiçbir şey persist etmez** (node/rule/assignment/run ayrı ayrı) · required column (file-level) ·
okunamayan dosya (exception sızmadan) · duplicate node code · invalid parent · self-parent cycle ·
level order violation · invalid territory level · unpublished reference set (fail-closed) · window containment ·
`TenantId` kolonu ignore + warning · blocking/non-blocking sayımı · boş Operation = skip · `delete` = unsupported.

### Apply
dry-run planı olmadan yazma yok (apply kendi validation'ını çalıştırır) · sheet-level all-or-nothing (%10 blocking
senaryosunda bile sheet hiç yazılmadı) · %20 eşiği · strict mode dosya-seviyesi · "nothing to apply" bloklanır ·
aynı dosya ikinci kez → duplicate yok + `no_change` · farklı değerli `add` → controlled conflict ·
override reason yoksa blocking · override reason varsa eski kayıt `ended` (silinmedi) · `AssignmentSource=import` ·
`end` kaydı korur · Account master mutate edilmez · in-file forward reference (aynı dosyada oluşturulan parent) ·
boş hücre değiştirmez / `<CLEAR>` temizler.

### ImportRun
apply run yazar / dry-run yazmaz · blocked apply kaydedilir ama yazmaz · raw file saklanmaz, hash tutulur (64 hex) ·
**interface'te update/delete/remove üyesi yok** (reflection) · run history query.

### Export/Template
template tam 7 sheet · export CoverageSummary + PlanVsCurrent + ResourceAssignments taşır · **hiçbir sheet'te
`TenantId` kolonu yok** · export satırları boş `Operation` ile iner.

### Guard
resource apply hiçbir koşulda çalışmaz · CoverageSummary/PlanVsCurrent importable sheet listesinde yok · upload'daki
read-model sheet'i ignore + warning · cross-tenant model 404 · model sheet yalnız draft · ModelCode immutable ·
controller'da workflow/approval/evidence/coverage-rollup rotası yok · resource-import rotası yok ·
`TerritoryPermissions.All` hâlâ 5 anahtar.

**Regresyon:** FU02B lifecycle, FU03 preview, FU04/FU04A/FU04B, FU05 apply/history, FU05A coverage guard testleri
aynen PASS.

---

## 14. Live Smoke (gateway-only, tenant 97c5)

Tüm business trafiği **Gateway 5000** üzerinden; `:5061`'e **yalnız `/health`**; hiçbir payload'da `TenantId` yok.

### 14.1 Fleet + oturum + contract

| Kontrol | Sonuç |
|---|---|
| Gateway 5000 / Auth 5056 / Platform 5057 / CRM 5061 `/health` | **200 / 200 / 200 / 200** |
| Web 5001 | ayakta (auth'suz `/CRM/**` 302→login) |
| Deploy doğrulaması (çalışan assembly) | `FU08-import-export-hardening` + `TerritoryImportEngine` + yeni limitation string'i **var** |
| Tenant claim | `97c59330-…cc93` ✅ |
| `crm.territory.*` | read · model.read · model.manage · node.read · node.manage (5/5) ✅ |
| Yasak `crm.territory.delete` / `crm.micro-zone.manage` | **token'da yok** ✅ |
| Contract flags | **12/12 PASS** (§12) |

### 14.2 Smoke fixture

Yeni model: `b8957d89-…e04c` — `SMOKE-MOD0151-FU08-20260801233936` (tr · business-unit beta+gamma ·
2026-07-01→2027-06-30). Account: `ACC-2026-000017`.

### 14.3 Template + export

| Kontrol | Sonuç |
|---|---|
| `GET .../import-template` | **200**, 17 633 bayt |
| Template sheet sayısı | **7** — `ValidationNotes · ReferenceValues · Model · Nodes · AssignmentRules · AccountAssignments · ResourceAssignments` ✅ |
| `GET .../export` | **200**, 20 169 bayt |
| Export sheet'leri | yukarıdaki 7 + **`CoverageSummary`** + **`PlanVsCurrent`** ✅ |
| Export'ta `TenantId` | **hiçbir yerde yok** (shared strings taraması 0) ✅ |

### 14.4 Dry-run

**Geçersiz dosya (6 satır):** `canApply=false`, blockedReason *"5 of 6 rows are blocked — this looks like the wrong
file or an outdated template."* Satır kodları: `invalid_territory_level` · `invalid_parent` · `duplicate_row` ·
`window_containment` · `unsupported_operation` (delete) — hepsi BLOCKING; 1 satır geçerli create.
**Sonrası: node 0, import run 0** → dry-run hiçbir şey yazmadı. ✅

**Geçerli dosya (4 node):** `canApply=true`, creates=4, errors=0. **Sonrası: node 0.** ✅

### 14.5 Apply

| Adım | Sonuç |
|---|---|
| Nodes apply | `applied=true`, runStatus=`applied`, **4 node** yazıldı (REG → AREA → ZONE-A/ZONE-B hiyerarşisi, aynı dosyadaki forward reference çözüldü) |
| **Aynı dosya ikinci apply** | `applied=false`, `prevApplies=1`, 4 satır **`no_change`**, blockedReason "nothing to apply", **node sayısı hâlâ 4** ✅ duplicate yok |
| AssignmentRules apply | `applied=true`, 1 rule yazıldı |
| `TenantId` kolonlu dosya | file warning + satır normal işlendi ✅ |
| **ResourceAssignments apply** | `applied=false`, satır `resource_apply_not_supported` **BLOCKING**, runStatus=`blocked` ✅ |
| CoverageSummary + PlanVsCurrent sheet'li upload | iki file warning ("export-only read model … ignored"), o sheet'lerden **hiç satır üretilmedi** ✅ |
| Eksik zorunlu kolon | file error ×2, `canApply=false` ✅ |

### 14.6 Account assignment (FU05 guard yolu)

Model activate edildi (`active`, node'lar `active`).

| Adım | Sonuç |
|---|---|
| Dry-run | creates=1, errors=0, **hiçbir şey yazılmadı** |
| Apply | `applied=true`; CoverageSummary **`hasCurrentCoverage=true`**, node `FU08-ZONE-A`, kaynak **`import`** ✅ |
| Account master | apply öncesi/sonrası **byte-identical**, master'da **0 territory alanı** ✅ |
| Aynı dosya tekrar | `no_change`, **atama sayısı 1** (duplicate yok) ✅ |
| 1 geçerli + 1 geçersiz satırlı batch | `unresolved_account_reference` BLOCKING → **geçerli satır da yazılmadı**, atama sayısı hâlâ **1** ✅ batch all-or-nothing |

### 14.7 Import run history

`GET .../import-runs` → **7 run** (3 `applied`, 4 `blocked`), her biri dosya adı · SHA-256 hash · yükleyen ·
c/u/e/s sayaçları · sheet outcome metni ile. Aynı dosyanın iki çalıştırması **aynı hash** ile göründü (re-run tespiti).

### 14.8 UI

| Kontrol | Sonuç |
|---|---|
| `/CRM/TerritoryManagement/Models/{id}/ImportExport` (authenticated) | **200**, 71 493 bayt |
| Sayfa bileşenleri | upload form · dry-run · apply · strict mode · sonuç tablosu · sheet tablosu · run history · "yalnız blocking" filtresi · export/template linkleri · antiforgery — **12/12 OK** |
| RESX | 5 örnek anahtarın hiçbiri ham key olarak sızmadı (tam lokalize) ✅ |
| Web proxy dry-run (multipart) | **200**, JSON envelope, 4 satır ✅ |
| Web proxy run history JSON | **200**, 7 run ✅ |
| `import-export.js` | **200** |
| Yasak UI yüzeyleri | approval / evidence / visit / campaign / ChangeRequest **yok**; tek "workflow" geçişi mevcut sidebar menüsü |

---

## 15. Guard Checks

| Kontrol | Sonuç |
|---|---|
| Workflow approval / controlled activation eklendi mi? | **Hayır** |
| ChangeRequest / Change Approval Trace / MOD-0023 eklendi mi? | **Hayır** |
| Visit/route planning eklendi mi? | **Hayır** |
| Campaign / frequency / call-cycle / digital detailing / survey / GPS eklendi mi? | **Hayır** |
| Brand Scope / Product / Brand master eklendi mi? | **Hayır** |
| Account master mutate edildi mi? | **Hayır** (canlıda byte-identical, 0 territory alanı) |
| Contact mutate edildi mi? | **Hayır** (hiç dokunulmadı) |
| `ContactTerritoryAssignment` eklendi mi? | **Hayır** |
| CoverageSummary import edilebiliyor mu? | **Hayır** (importable sheet listesinde yok; upload'da ignore + warning) |
| Plan vs Current import edilebiliyor mu? | **Hayır** (aynı) |
| Resource assignment apply açıldı mı? | **Hayır** (`resource_apply_not_supported`, contract flag false, rota yok) |
| Yeni import framework yazıldı mı? | **Hayır** (ClosedXML + MOD-0150 deseni yeniden kullanıldı) |
| Hard delete yapıldı mı? | **Hayır** (`delete` refused; kapatma yalnız `end`) |
| Mongo hand-edit yapıldı mı? | **Hayır** |
| RBAC seed/grant değişti mi? | **Hayır** (yalnız FU08-RBAC follow-up'ı) |
| MOD-0048 publish değişti mi? | **Hayır** |
| `crm.territory.delete` / `crm.micro-zone.manage` | **Kullanılmadı / açılmadı** |
| Direct 5061 business API çağrısı | **Yok** (yalnız `/health`) |
| Payload'da `TenantId` | **Yok** (claim + `X-Tenant-Id` header) |
| Gateway (`ocelot.json`) değişti mi? | **Hayır** (mevcut wildcard) |
| FU03 / FU04A / FU04B / FU05 / FU05A davranışı değişti mi? | **Hayır** (testleri aynen PASS) |
| `supportsWorkflowActivation` false kaldı mı? | **Evet** |

---

## 16. Created / Updated Files

### Created — backend

| Dosya | İçerik |
|---|---|
| `…/Domain/Entities/TerritoryImportRun.cs` | Append-only run aggregate + result/sheet-count value objects |
| `…/Domain/Repositories/ITerritoryImportRunRepository.cs` | Insert + 2 read (update/delete üyesi yok) |
| `…/Persistence/Repositories/TerritoryImportRunRepository.cs` | Mongo repo (in-memory sıralama, DateTimeOffset index'e girmez) |
| `…/Application/Features/Territory/ImportExport/TerritoryWorkbookSchema.cs` | Sheet/kolon/operasyon/status/error-code sözleşmesi |
| `…/ImportExport/TerritoryImportExportModels.cs` | Preview / summary / sheet outcome / run DTO'ları |
| `…/ImportExport/TerritoryWorkbookBuilder.cs` | Export + template writer (ClosedXML, dropdown'lar canlı MOD-0048'ten) |
| `…/ImportExport/TerritoryWorkbookReader.cs` | Reader (header normalize, TenantId ignore, read-model sheet'leri parse edilmez) |
| `…/ImportExport/TerritoryImportContext.cs` | Read-only doğrulama bağlamı + parse helper'ları |
| `…/ImportExport/TerritoryImportValidator.cs` | Dry-run plan üretici (tüm kontroller) |
| `…/ImportExport/TerritoryImportEngine.cs` | parse → validate → apply → record |
| `…/ImportExport/TerritoryImportExportContracts.cs` | 4 MediatR mesajı |
| `…/ImportExport/Handlers/TerritoryImportExportHandlers.cs` | Export/template · dry-run/apply · run history |

### Created — frontend / tests / docs

| Dosya |
|---|
| `frontend/Diten.Web/Views/CRM/TerritoryManagement/ImportExport.cshtml` |
| `frontend/Diten.Web/wwwroot/assets/js/CRM/TerritoryManagement/import-export.js` |
| `services/Diten.CrmService/tests/…/Territory/TerritoryImportExportFu08Tests.cs` (45 test) |
| `docs/audits/mod-0151-fu08-import-export-hardening-implementation-2026-08-01.md` (bu rapor) |

### Updated

| Dosya | Değişiklik |
|---|---|
| `…/Persistence/DependencyInjection.cs` | Run repo DI + class map + 2 index |
| `…/Application/DependencyInjection.cs` | `TerritoryImportEngine` kaydı |
| `…/Api/Controllers/CRM/TerritoryModelsController.cs` | 5 FU08 endpoint + upload helper'ları |
| `…/Features/Territory/TerritoryPermissions.cs` | `Export`/`Import` canonical anahtarları (All'a eklenmedi) |
| `…/Features/Territory/Contract/TerritoryContractDto.cs` | 5 additive flag |
| `…/Features/Territory/Contract/GetTerritoryContractHandler.cs` | RuntimeScope + 6 limitation satırı |
| `frontend/Diten.Web/Controllers/CRM/TerritoryManagementController.cs` | Sayfa + 5 proxy action + dosya/upload helper'ları |
| `frontend/Diten.Web/Models/CRM/TerritoryViewModels.cs` | 8 FU08 view model |
| `frontend/Diten.Web/Views/CRM/TerritoryManagement/Details.cshtml` | Import/Export butonu |
| `frontend/Diten.Web/Resources/Views/CRM/TerritoryManagement/*.resx` (7 dil) | +47 anahtar → 276 |
| `services/…/tests/…/Territory/FakeTerritoryInfrastructure.cs` | Update'i gerçekten uygulayan fake'ler + append-only run fake |
| `services/…/tests/…/Territory/TerritoryContractTests.cs` | FU08 flag + runtimeScope iddiaları |
| `services/…/tests/…/Territory/TerritoryScopeGuardTests.cs` | export/import artık yasak listede değil; FU08 rotaları + resource-apply yokluğu + permission sabitliği |

**Gateway (`ocelot.json`), RBAC seed, MOD-0048 publish ve MOD-0023 kodu değiştirilmedi.**

---

## 17. Final Verdict

**PASS**

- FU08 export / template / dry-run / apply / run-history uçtan uca çalışıyor (canlı doğrulandı).
- **Dry-run hiçbir şey persist etmiyor** — model/node/rule/assignment ve run-history satırı dahil.
- Apply ayrı rota; upload varsayılanı `dryRun=true`; UI o dosya için onaylanmış bir dry-run olmadan apply etmiyor.
- `Model`/`Nodes`/`AssignmentRules` sheet-level, `AccountAssignments` batch-level all-or-nothing — ikisi de canlıda
  kanıtlandı; sessiz partial apply yok.
- Account assignment import'u FU05 guard setinin aynısını kullanıyor; eski kayıt kapanıyor, silinmiyor.
- **ResourceAssignments apply kapalı** (`supportsResourceAssignmentImportApply=false`), CoverageSummary ve
  Plan vs Current **import edilemiyor**.
- `TerritoryImportRun` append-only (interface'te update/delete üyesi yok, reflection testiyle sabit); raw file
  saklanmıyor, yalnız SHA-256.
- Export read-only ve `TenantId` içermiyor; UI çalışıyor; 7 dil RESX paritesi 276/276.
- Contract flag'leri doğru; `supportsWorkflowActivation=false` korundu.
- Build PASS · Tests **449/449 PASS (5 skipped)**, 45'i yeni FU08 testi · Gateway-only live smoke PASS.
- Workflow / approval / ChangeRequest / MOD-0023 / visit-route / campaign / Brand-Product kapsamı açılmadı.

**Açık follow-up'lar (yetkilendirilmiş, bu task'ın dışında):**

- **`MOD-0151 FU08-RBAC`** — `crm.territory.export` / `crm.territory.import` katalog hizalaması (bugün model.read /
  model.manage fallback'i kullanılıyor).
- **`MOD-0151 FU08A`** — resource assignment import **apply** (FU04A proposed/active + reason/provenance korunarak).
- **Model sheet sınırı:** rota model-scoped olduğu için `Model` sheet'i yalnız **update** destekler; import ile yeni
  model **yaratılmaz**. Yeni model hâlâ ekrandan açılır. (Pack §22.5 ile uyumlu, raporda açıkça kayıt altında.)

---

## 18. Next Recommended Prompt

```
MOD-0151 FU09A — Visit/Route Readiness: Coverage, Contact Availability and Frequency Input Boundaries Pack Authorization
```

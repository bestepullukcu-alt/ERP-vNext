# WP-MVP1-SYNC1 — BLOCKED (2026-10-12)

> **Çözüldü (aynı gün):** sahip kararı **B** (main'in eski-kayıt davranışı kalır) ve CT kararı v1.2 ile merge tamamlandı → `mvp1-sync1-evidence-2026-10-12.md`.

**BLOCKED — eski (alan öncesi) Workflow örneklerinde başlatanın kendi onayı: MVP-1 (WP-0023-FX) 409 istiyor, main
(WP-WORKFLOW-APPROVAL-STATUS-01) 200 istiyor. Aynı girdi, zıt sonuç; tek uygulama iki testi birden yeşil tutamaz.**

| | |
|---|---|
| Prompt | CT-MVP1-SYNC1 v1 |
| Taban | `98da00832` (MVP-1 dalı ucu) |
| main | `a814bad0f` (fetch 2026-10-12) — 395 ileride; bizde main'de olmayan 213 commit; merge-base `90fde0846` |
| Çalışma ağacı | `C:\Users\Ali\erp-vnext-wt-mvp1-sync1`, dal `feature/mvp1-sync1` |
| Merge | `git merge origin/main --no-ff --no-commit` → 26 çakışma (CT ölçümüyle aynı liste) → **`git merge --abort`**; ağaç `98da00832`'de temiz |
| Derleme / test / sabotaj / probe | **Koşulmadı** — hata protokolü gereği durdu. Süreç başlatılmadı; kapatılacak PID yok. |

## 1. Engelleyen çelişki

### Bizim (WP-0023-FX, CT-ACCEPTED)
- `WorkflowTaskTransitionSupport.IsStarterAsync` (98da00832, `WorkflowTaskTransitionSupport.cs:383-400`):
  `StartedByUserId` boşsa (alan öncesi örnek) **Start transition log satırının `ActorId`'sine** düşer; o satır her zaman
  başlatanın kullanıcı kimliğini taşır (`StartWorkflowInstanceHandler`: `ActorId = _currentUserContext.UserId…`).
- Test: `WorkflowCrossServiceTests.Fx_an_instance_started_before_the_fix_still_blocks_its_starter_through_the_start_log`
  — eski satır (StartedBy = e-posta, kimlik alanı yok, Start log = başlatan) → başlatanın onayı **409 `SOD_VIOLATION`**.

### Main'in (WP-WORKFLOW-APPROVAL-STATUS-01, `94731f870` + v2 `59761261c`)
- `WorkflowTaskTransitionSupport.IsStarter` (origin/main `WorkflowTaskTransitionSupport.cs:785-794`): `StartedByUserId`
  (`Guid?`) boşsa **eski karşılaştırmayı korur** (`StartedBy` adı ↔ onaylayan kimliği — hiç eşleşmez). Alan belgesi:
  "instances started before this field existed keep null and keep the old comparison — no migration".
- Test: `WorkflowApprovalStatusHttpMongoTests.An_instance_from_before_the_field_keeps_the_old_comparison` — HTTP ile
  başlatılan örnekte `StartedByUserId` `$unset` edilir (Start log satırı başlatanın kimliğiyle yerinde kalır) → başlatanın
  onayı **200 OK** beklenir.

### Neden birleştirilemez
İki testin girdisi aynı biçimdedir (StartedBy = ad/e-posta, `StartedByUserId` yok, Start log ActorId = başlatan). Ayırt
edici bir alan yok. Bizim fallback'i bırakırsak main'in testi kırmızı; main'in davranışını bırakırsak bizim FX testi
kırmızı ve kabul edilmiş bir güvenlik davranışımız (eski açık görevlerde dört-göz) silinir. Prompt: "iki taraf aynı noktada
çelişen davranış istiyorsa → BLOCKED".

### Sahibin / CT'nin kararı gereken soru
| Seçenek | Sonuç | Kırılan kabul |
|---|---|---|
| **A (öneri)** — tek uygulama main'in `IsStarter`'ı + bizim Start-log fallback'i | Alan öncesi açık görevlerde de başlatan onaylayamaz (dört-göz kontrolü geriye dönük kapalı) | Main testi `An_instance_from_before_the_field_keeps_the_old_comparison` 409 bekleyecek şekilde değişir (main'in kabul edilmiş testi — yazılı karar gerekir) |
| B — main'in davranışı | Alan öncesi örneklerde başlatan kendi onayını verebilir (eski açık sürer) | Bizim `Fx_an_instance_started_before_the_fix_…` testi ve WP-0023-FX'in bir kabul maddesi silinir |

Öneri gerekçesi: main'in kendi yorumu eski karşılaştırmanın "hiç eşleşmediğini" söylüyor — yani test istenen bir
davranışı değil, bilinen bir açığı sabitliyor; Start log her iki dalda da başlatanın kimliğini taşıyor, migration gerekmez.

## 2. Çözülebilir bulunan çakışmalar (karar verilirse uygulanacak plan)

| Dosya | Bizim | Main'in | Plan |
|---|---|---|---|
| `WorkflowDefinitionsController.cs` (5) | `WorkflowActorBinding.Bind` (FX) | `TryBindActor` (B4) — ikisi de 403 `WORKFLOW_ACTOR_MISMATCH` | **Tek uygulama: `WorkflowActorBinding`** (main'in kabul ettiği her girdiyi kabul eder; ek olarak çağıranın kendi e-postası). Main'in B4 testleri (başkası → 403; boş/kendi id → 200) karşılanır. `TryBindActor` silinir. |
| `WorkflowModels.cs` (otomatik merge) | `WorkflowActorMismatch` | aynı sabit | **Çift tanım → derleme hatası**; tek sabit bırakılır |
| `Approve/Reject/Cancel/Delegate/RequestInfo…Handler` | `ActorId ?? string.Empty` | `ActorId!` | `?? string.Empty` (null güvenli; controller her zaman doldurur) |
| `WorkflowInstance.cs` | `string? StartedByUserId` | `Guid? StartedByUserId` (aynı BSON alanı) | **Main'in `Guid?`'ı**; `IsStarterAsync` Guid'e uyarlanır. ⚠ MVP-1 ortamlarında string olarak yazılmış satırlar Guid'e okunamaz → CT notu (L3 test DB'leri etkilenmez; sahibin filo DB'si etkilenebilir) |
| `StartWorkflowInstanceHandler.cs` | `UserId.ToString()` | `UserId` | main'inki (tip ile birlikte) |
| `WorkflowTaskTransitionSupport.cs` | `IsStarterAsync` (fallback'li) | `IsStarter` (Approve + Delegate'te başlatana devretme reddi) | **§1 kararına bağlı.** Delegate yolundaki main reddi (WP-WCN-DELEGATE-LOGTIME-01) her iki seçenekte korunur |
| `WorkItemProjectionService.cs` | `WithSignature` (E4/E5W2) | `DelegationExcludedTargets`, `BuildApprove(task, instance, …)` | Birleşim: main imzası + `WithSignature` sarmalı |
| `WorkAggregationModels.cs` | `SignatureRequired/Meaning/ActionCode` | `AcceptsNote`, `RequiresTargetPerson`, `ExcludedTargetPrincipalIds` | Birleşim (hepsi sonda, varsayılanlı, null'da yazılmaz) |
| `WorkItemActionDispatchModels.cs` | `SignatureId` | `PlannedStartAt`, `PlannedDurationMinutes` | Birleşim |
| `PlatformSchemaManifest.cs` | `ESignatureCollections` | `TimeEntryCollections` | Birleşim |
| `PlatformSchemaMigrations.cs` | E5B eski tekil indeks düşürme | MOD-0280 T1a indeks düşürme | Birleşim (ikisi de manifestten önce) |
| `SchemaProfile.cs` | `ESignature = 12` | `TimeEntry = 12` | **Değer çakışması**: main 12'yi tutar, `ESignature = 13` (değerin kalıcı yazılıp yazılmadığı ölçülecek) |
| `PlatformRecurringJobRegistrar.cs` | e-imza talep süresi taraması | 3 timesheet/timer işi | Birleşim |
| `DefaultRolePermissionTemplate.cs` | `"esign"` + `DefaultSignerPermissions` | `"shipment-tracking-pod"` (Q358, geçici, 2026-11-03) | Birleşim: AdminModules = ikisinin birleşimi, main'in Q358 yorumu korunur |
| `WorkCenterNext/app.js` (2) | `signatureForApproval` + `body.signatureId` | Timer bloğu + BL-491 `targetPrincipalId` | Birleşim (iki blok yan yana; gövdeye iki satır) |
| `SharedResource.*.resx` (7) | e-İmza / karantina anahtarları | main anahtarları | Anahtar birleşimi; aynı anahtar farklı metinse main |
| `DCP-009-supply-chain-inventory.md` | MVP-1 içerik | MVP-6 içerik | Birleşim |

Sözleşme dosyasında çakışma yok.

## 3. İkincil karar notları (bloklamaz, kabulde görülmeli)
1. `StartedByUserId` tip birleşmesi (`string?` → `Guid?`): MVP-1 dalıyla çalışmış bir DB'de string değerli satırlar
   deserialize edilemez. L3 testleri etkilenmez; sahibin filo DB'si için ya tolerant okuma ya veri notu gerekir.
2. `SchemaProfile.ESignature` 12 → 13.

## 4. Durum
- `feature/mvp1-sync1` = `98da00832` + yalnız bu kanıt dosyası. Push yok, MVP-1 dalına ve main'e yazılmadı, ana checkout'a dokunulmadı.
- §1 kararı gelince: merge yeniden yapılır (main ilerlemişse güncel `origin/main` ile), §2 planı uygulanır, ardından derleme, testler (önce/sonra), K3 sabotajları ve probe.

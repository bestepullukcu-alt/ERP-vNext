# WMS-WAVE-BUNDLE v1 — proposal (MOD-0180 Wave Planning / Dalga Planlama)

> **Durum: TASLAK (proposal) — sözleşme değil. Sürüm `1.0.0-draft`.** `docs/analysis/contracts/` altında WMS-WAVE-BUNDLE yok (bu dosya ilk
> taslaktır). Blueprint 8.8 satır 181 sözleşmeyi "WMS-WAVE-BUNDLE (wave objects, optimization params, KPI bindings)" diye adlandırır.
> **Dondurma ayrı CT adımıdır** (paket §5, §18; MOD-0178 §19.7 / MOD-0181 deseni): geliştirici bu dosyayı dondurmaz, `FROZEN` işareti koymaz.
> Dondurmada `docs/analysis/contracts/wms-wave-bundle.openapi.yaml` (`info.version: 1.0.0`, `x-status: FROZEN`, `x-owner: MOD-0180`,
> `x-owner-service: Diten.InventoryService`) yazılır; donduktan sonra yalnız eklemeli değişiklik (K16).
>
> **Kaynak (davranış otoritesi):** paket `execution/domains/supply-chain-execution/module-packs/MOD-0180-wave-planning.md` **r11**
> (LF sha256 `226ab7f9f3690c422fba1bf5a3008d004d82791cfe6b20ebb2b74c0cd47cb095`, `status: ready-for-dev`, onay satırı
> `execution/approvals/approvals.tsv` — onaylayan Ali / `GMG-ERP` 2026-11-13, commit `e12bf26c8`). Bu dosya yalnız kablo şeklidir; paketle
> çelişirse **paket kazanır** ve bu dosya düzeltilir. İş paketi: `execution/domains/supply-chain-execution/work-packs/mvp5-wms/WP-0180-DL1-wave-core-backend.md`.
>
> **Dilim durumu:** paket §3'teki **23 işlemin hepsi** burada (AC-1). **§4.1 (DL-1, 14 işlem) bağlayıcı taslaktır** — kod bu tabloya karşı
> yazılır ve sözleşme testi (`INVT/WavePlanning/` altında; emsal `INVT/CycleCount/CycleCountContract.cs` + `INVT/TestInfrastructure/ContractSchemas.cs`)
> her `/api/warehouse/waves*` DL-1 cevabını §3 şemalarına karşı kapalı (bilinmeyen üye = kırmızı) doğrular. **§4.2 (DL-2 … DL-5, 9 işlem)**
> paketin §3 / §8 / §16'sından çıkarılmış **ön taslaktır**; her dilim kendi satırlarını ilk adımında bağlayıcı yapar (gövde ayrıntısı o adımda
> kesinleşir, yalnız eklemeli). DL-1 sonrası controller'da 14 uç olur; taslak ile klasör eşitliği DL-5 sonrası (paket AC-55).

## 1. Rules

1. Sunucu `/api/warehouse` (Gateway → `Diten.InventoryService` **5067**, mevcut `/api/warehouse/{everything}` rotası; `ocelot.json` değişmez —
   paket §15). Bütün işlemler `/api/warehouse/waves…` ailesindedir. Kiracı kabuğu kullanıcıları (`actor_type = tenant_user`).
2. `TenantId` token'dan, şirket `X-Legal-Entity-Id`'den sunucuda çözülür (servisin scope middleware'i). Başka kiracı / başka şirket / bilinmeyen
   id → **404**. `tenantId` / `legalEntityId` gövdede → 400 `INVALID_REQUEST` (istek okuyucu), sorguda → 400 `INVALID_REQUEST` (scope middleware)
   (paket AC-4).
3. Zarf: InventoryService'in ölçülmüş kablosu — başarı gövdesi şemanın kendisi (sarmalanmamış); hata `Error` =
   `{ error: { code, message, details?, correlationId }, contractVersion: "v1" }` (INVENTORY `Error`; WMS-BUNDLE / WMS-COUNT-BUNDLE ile aynı).
4. **Boş isteğe bağlı üye yazılmaz** (JSON `null` yok). İstekte `null` isteğe bağlı üyenin yokluğu sayılır. Şemalarda `?` isteğe bağlı üyedir.
5. Her yazma `Idempotency-Key` başlığı alır, gövde `idempotencyKey` ile eşit (8–100 karakter). Aynı anahtar + aynı gövde → **200 replay**
   (kaydın güncel hâli; ilk cevabın kodu tekrarlanmaz), farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`. Anahtar, isteyen kullanıcı + operasyon
   (`operationId`) + hedef kayıt kapsamında saklanır (yaratma: dalga kaydında, yaratanla — tekil `(TenantId, LegalEntityId, CreatedBy,
   IdempotencyKey)`; diğerleri: komutun ilk yazımının `WaveEvent`'inde, `RequestOp` = `operationId`) ve **`If-Match`'ten ve bütün iş kurallarından
   önce** aranır. Parmak izi = operasyon + hedef (`waveId` / `warehouseId`) + normalleşmiş gövde; başlıklar girmez. Eşzamanlı iki özdeş istekten
   kaybeden 200 replay alır. Hiç yazmadan reddedilen istek anahtar bırakmaz (paket AC-6).
6. Değişebilir kayıt (`Wave`, `WaveWarehouseSettings`, DL-3'te `WaveException`) değişiklikte `If-Match: W/"<version>"` ister; eksik → 400
   `INVALID_REQUEST`, bayat → 409 `WAVE_CONFLICT` (D-14). Cevap `ETag: W/"<version>"`. Bayatlık kural 5'ten sonra değerlendirilir. Dalga üyeliğini
   değiştiren işlemler (`addWaveOrders`, `removeWaveOrders`) **dalganın** `If-Match`'ini ister ve dalganın `version`'ını artırır.
7. **Bu sözleşme stok, rezervasyon, görev ya da outbound yazmaz** (paket §2.3). MOD-0178 verisi süreç içi **yalnız `Find`** ile okunur; toplama
   görevini MOD-0178 kendi portuyla açar / geri çeker (§7, bilgi).
8. **PUT / DELETE yok** (paket §8.1, AC-8): güncelleme, iptal ve ayar yazımı `POST` komut uçlarıdır. Silme yoktur; Taslak dalganın tek
   kaldırma yolu `cancelWave`'dir (durum, silme değil — `isDeleted` hiç `true` olmaz).
9. Tarihler: `date-time` = UTC an (`…Z`, milisaniye); `localDateTime` = deponun saat diliminde saniyesiz yerel an (`yyyy-MM-ddTHH:mm`, ofsetsiz);
   `timeZoneId` = IANA kimliği (ör. `Europe/Istanbul`). Kesim dönüşümü paket §8.4 (§6.3).
10. Yollardaki `{waveId}`, `{exceptionId}` `:guid` kısıtlıdır; sabit kesimler (`dashboard`, `candidates`, `exceptions`, `kpis`, `settings`)
    parametreli yoldan önce eşleşir (paket §3). `{warehouseId}` uuid'dir.
11. Correlation: `X-Correlation-Id` her cevapta (servis geneli).
12. **İzin:** her işlem `x-permission` anahtarını `[HasPermission]` ile zorlar; eksik izin → 403 (gövde servis genelinin yetki cevabı).
13. **Ağırlık / hacim (D-12):** hiçbir şema ağırlık / hacim üyesi taşımaz (veri yok); ekran "—" yazar.

## 2. Sabitler

| Sabit | Değer | Kaynak |
|---|---|---|
| Modül / manifest kodu | `MOD-0180` / `WAVE-PLANNING` (SortOrder 180) | paket §14 |
| Merkezi denetim kaynak modülü | `WAVE-PLANNING` | paket §8.13 |
| Kayıt kodu | `{ŞİRKET}-WAV-{000001}` — MOD-0178 geçici sayacı, tür `WAV` (`WarehouseRecordCodeRules.TypePrefixes`, X-6); hedef MOD-0040-FU02 | paket §8.14, §22, Q-4 = A |
| Şirket öneki | MDM tüzel kişilik `Code`, `^[A-Z]{2,5}$` | paket §8.14 |
| `waveType` | `Cargo` \| `Customer` \| `Region` \| `Manual` | paket §4.1 |
| `shipToFilter` | 1..100 karakter (trim; boş = yok) | paket §12 |
| `maxOrders` / `maxLines` | 1..1000 / 1..20000 (iş sınırı, engellemez — D-7) | paket §4.1, §8.5 |
| Teknik üst sınır | dalga başına 500 sipariş, Σ `plannedTaskCount` 2.000 (aşım 422 `WAVE_TOO_LARGE`) | paket §8.5, Q-8 = A |
| Not | ≤ 500 karakter (trim; boş = yok) | paket §12 |
| Gerekçe (iptal, ayar, geri çekme, istisna eylemi) | 10..500 karakter (trim) | paket §12, §8.13 |
| Taslak çıkarma gerekçesi | isteğe bağlı, ≤ 500 | paket §8.2 kural 4 |
| `outboundIds` (ekle / çıkar) | 1..200, uuid, tekrarsız | paket §12 |
| Kesim geçmiş toleransı | `cutoffAtUtc ≥ şimdi − 1 saat` | paket §8.4 |
| Öneri üst sınırı | kalan kapasite; sınır yoksa 200 | paket §8.2 kural 5 |
| Ayar varsayılanları | `waveManaged = false`, `timeZoneId` yok, 90 / 70 / 30 | paket §4.5 |
| Ayar aralıkları | `riskMinutesBeforeCutoff` 1..1440, `riskProgressPercent` 1..100, `pendingTaskMinutes` 1..1440 | paket §12 |
| Sayfa | `page` ≥ 1 (varsayılan 1), `pageSize` 1..200 (varsayılan 25) | WMS-BUNDLE §3.8 emsali |
| KPI aralığı | `from ≤ to`, en çok 92 gün | paket §8.16 |

## 3. Schemas

### 3.1 `Wave`
```json
{
  "waveId": "uuid", "number": "GMG-WAV-000001", "warehouseId": "uuid", "warehouseCode": "WH-01",
  "waveType": "Cargo | Customer | Region | Manual",
  "cutoffLocal": "2026-11-20T14:30", "cutoffAtUtc": "2026-11-20T11:30:00.000Z", "timeZoneId": "Europe/Istanbul",
  "cutoffAmbiguousResolved": true,
  "shipToFilter": "string?", "maxOrders": 100, "maxLines": 2000, "responsibleUserId": "uuid?", "note": "string?",
  "status": "Draft | Releasing | Released | Picking | Completed | PartiallyCompleted | Cancelled",
  "orderCount": 0, "lineCount": 0, "plannedTaskCount": 0,
  "limitWarnings": [ { "kind": "MaxOrders | MaxLines", "limit": 100, "actual": 120 } ],
  "createdBy": "uuid", "createdAt": "date-time", "updatedAt": "date-time?",
  "releasedBy": "uuid?", "releasedAt": "date-time?",
  "cancelledBy": "uuid?", "cancelledAt": "date-time?", "cancelReason": "string?",
  "completedAt": "date-time?",
  "version": 1, "contractVersion": "v1"
}
```
- İsteğe bağlı üyeler (kural 4 — boşken yazılmaz): `shipToFilter`, `maxOrders`, `maxLines`, `responsibleUserId`, `note`, `cutoffAmbiguousResolved`,
  `limitWarnings`, `updatedAt`, `releasedBy`, `releasedAt`, `cancelledBy`, `cancelledAt`, `cancelReason`, `completedAt`. Diğer üyeler zorunludur.
- `number`, `status`, `cutoffAtUtc`, `timeZoneId`, `warehouseCode` ve sayımlar sunucu üyesidir; istekte gelirse 400 `INVALID_REQUEST` (paket AC-10).
- `timeZoneId` yaratmada depo ayarından kopyalanır, sonra değişmez; `updateWave` kesimi dalganın kendi `timeZoneId`'siyle dönüştürür.
- `cutoffAmbiguousResolved` yalnız `true` iken yazılır (kural 4): kesim çakışan yerel saatteydi, erken ofset seçildi (paket §8.4).
- `orderCount` / `lineCount` / `plannedTaskCount` = etkin üyeliklerin (`state ∈ Planned | Issued`) toplamı (okuma anında; stok değil).
- `limitWarnings` yalnız aşım varken yazılır; aşım yazımı engellemez (D-7).
- `Releasing` iç durumdur; listede görünür (rozet), istemci yazma yapmaz (DL-2). DL-2 eki: `routeSummary { taskCount, fallbackCount,
  proxyDistance, computedAtUtc, algorithm: "S-SHAPE-v1" }`; DL-3 eki: `progress { pickPercent, packPercent, shipPercent, readyToShipCount }`,
  `atRisk`, `recalls[]` (eklemeli, aynı kural 4).

### 3.2 `WaveOrder`
```json
{
  "waveId": "uuid", "outboundId": "uuid", "outboundNumber": "GMG-OUT-000123",
  "lineCount": 3, "allocationCount": 4, "plannedTaskCount": 5,
  "state": "Planned | Issued | Withdrawn | Excluded | Removed | OrderCancelled",
  "addedBy": "uuid", "addedAt": "date-time", "addedVia": "Manual | Suggestion",
  "closedBy": "uuid?", "closedAt": "date-time?", "closeReason": "string?",
  "version": 1
}
```
- Sayımlar eklemede MOD-0178 outbound'undan okunur (satır, tahsis, `PlannedBins` sayısı — paket §4.2).
- `closedBy` sistem kapanışında yazılmaz, `closeReason` `ReleaseAborted` \| `WaveManagementDisabled` olur (paket §4.2).
- DL-2 ekleri: `issuedTaskNumbers[]`, `issuedAtUtc`; DL-3 eki: `movedToWaveId`, `pickStatus`, `packStatus`, `shipStatus`.

### 3.3 `WaveEvent`
```json
{ "eventId": "uuid", "waveId": "uuid", "seq": 1, "kind": "Created", "actorUserId": "uuid?", "system": true, "atUtc": "date-time",
  "data": { "key": "string" } }
```
- `kind` (paket §4.3): `Created`, `Updated`, `OrdersAdded`, `OrdersRemoved`, `Cancelled` (DL-1); `ReleaseStarted`, `Released`,
  `ReleaseAborted`, `OrderExcluded` (DL-2); `PickingStarted`, `RemainingRecalled`, `OrderWithdrawn`, `OrderMoved`, `OrderCancelledUpstream`,
  `Completed`, `PartiallyCompleted`, `ExceptionRaised`, `ExceptionResolved`, `ExceptionIgnored`, `ExceptionCleared` (DL-3). Ayar akışının
  türleri (`SettingsUpdated`, `PendingOrdersIssued`, `PendingOrdersStuckChanged`) bu uçtan **dönmez** (akış `Settings`, paket §4.3).
- `actorUserId` kullanıcı yazımında, `system: true` sistem yazımında (biri yazılır). `data` değerleri dizedir; gerekçe `data.reason`'dadır.
  İstek anahtarı, parmak izi ve `RequestOp` depoda tutulur, kabloya yazılmaz. Insert-only, iş yazımıyla aynı işlemde.

### 3.4 `WaveWarehouseSettings`
```json
{
  "warehouseId": "uuid", "waveManaged": false, "timeZoneId": "Europe/Istanbul?",
  "riskMinutesBeforeCutoff": 90, "riskProgressPercent": 70, "pendingTaskMinutes": 30,
  "deferredOrderCount": 0, "releasingOrderCount": 0,
  "stuckOrders": [ { "outboundId": "uuid", "outboundNumber": "string", "code": "OutboundUnreadable | PlanInconsistent | Unexpected | RepeatedConnectionError", "firstFailedAtUtc": "date-time" } ],
  "version": 0, "contractVersion": "v1"
}
```
- Kayıt yokken varsayılanlar döner ve **`version: 0`**; ilk yazım `If-Match: W/"0"` ile kaydı yaratır (öneri — §9 açık nokta 3).
- `deferredOrderCount`, `releasingOrderCount`, `stuckOrders` sunucu üyesidir (paket §4.5); DL-1'de ertelenmiş outbound oluşamaz, bu yüzden üçü
  hep `0` / `0` / `[]`'dır (bilgi, bekçi değil — paket AC-15 c). `stuckOrders` boşken de yazılır (dizi; kural 4 yalnız skaler isteğe bağlı üyeyi kapsar).
- `gateSeq` ve `pendingReleaseStuck` iç sistem alanıdır; kablo üyesi değildir, istekte gelirse 400 `INVALID_REQUEST` (paket §12).

### 3.5 `WaveCandidate` (planlayıcı sol listesi ve öneri satırı)
```json
{
  "outboundId": "uuid", "outboundNumber": "GMG-OUT-000123", "orderRef": "string?",
  "shipTo": { "name": "string", "country": "tr", "city": "string?", "postalCode": "string?" },
  "lineCount": 3, "allocationCount": 4, "plannedTaskCount": 5, "createdAt": "date-time",
  "eligible": true, "ineligibleReason": "AllocationPending"
}
```
- `eligible = true` ⇔ paket §8.2 kural 2 (aynı depo, outbound `Allocated` + `PickIssuance.State = Deferred`, etkin üyelik yok).
- `eligible = false` satır yalnız `Allocating` outbound içindir (gri, "rezervasyon bekliyor" — `ineligibleReason = AllocationPending`); başka uygun
  olmayan outbound listede **yoktur** (paket §8.2 kural 2). Öneri (`listWaveSuggestions`) yalnız `eligible = true` döner.
- `shipTo` WAREHOUSE-OUTBOUND `Address`'in ad / ülke / şehir / posta kodu üyeleridir (`line1` / `line2` yazılmaz). KVKK: kablo dışında log / OTel'e
  yazılmaz. Sipariş başına kesim yoktur (Q-22) — ekran "—".

### 3.6 `WaveOrdersResult`
```json
{
  "wave": { "…": "Wave (§3.1)" },
  "orders": [ { "outboundId": "uuid", "outcome": "Added | Removed | NotMember", "order": { "…": "WaveOrder (§3.2)?" } } ],
  "contractVersion": "v1"
}
```
- `addWaveOrders`: hepsi ya da hiçbiri — bir sipariş uygun değilse 422 `WAVE_ORDER_NOT_ELIGIBLE`, başka etkin dalgadaysa 409
  `WAVE_ORDER_IN_OTHER_WAVE`; ikisinde de `error.details.orders[] { outboundId, code }` (`code` ∈ `NotFound` \| `OtherWarehouse` \| `NotAllocated` \|
  `NotDeferred` \| `InOtherWave`) ve **yazım yok**. Başarıda her satır `Added`; sınır aşımı `wave.limitWarnings`'tedir (paket AC-13).
- `removeWaveOrders`: dalganın `Planned` üyesi olan satır `Removed`; üyesi olmayan satır `NotMember` (yazım yok, hata değil).

### 3.7 `WaveLoad` (yük sekmesi)
```json
{
  "zones": [ { "zoneKey": "A", "aisle": "string?", "plannedTaskCount": 12, "orderCount": 4 } ],
  "pickers": [ { "userId": "uuid", "taskCount": 7 } ],
  "contractVersion": "v1"
}
```
- `zones`: etkin üyeliklerin kalıcı pick planındaki (`PlannedBins`) bin'lerin LOCATION bölge anahtarı ve (ayrıştırılabilirse) koridoru başına sayım
  (paket §8.7 girdileri). DL-1'de ertelenmiş outbound oluşamadığı için boştur.
- `pickers`: açılmış dalga görevlerinin atanan toplayıcıya göre sayımı — DL-2 / DL-3'te dolar; DL-1'de hep `[]`.

### 3.8 Sayfalar
`{ items, total, page, pageSize, contractVersion }`, `pageSize ≤ 200`.

### 3.9 DL-2 … DL-5 şemaları (ön taslak)
- `WaveReleaseResult` (DL-2): `{ wave, issuedOrderCount, issuedTaskCount, excludedOrders[] { outboundId, outboundNumber, code }, contractVersion }`
  (`code` = port `Ineligible` kodu: `OutboundCancelled` \| `NotDeferred` \| `IssuedToOtherWave` \| `PlanMismatch` \| `NotAllocated`).
- `WaveTask` (DL-2): `{ taskId, taskNumber, outboundId, outboundNumber, sequence, fromLocationId, binCode, zoneKey, routeFallback?, status,
  assigneeUserId?, plannedQuantity, confirmedQuantity, baseUomId }` — MOD-0178 görevinden okunur.
- `WaveRecallResult` (DL-3): `{ wave, withdrawn[] { outboundId, outboundNumber }, started[] { outboundId, outboundNumber }, failed[] { outboundId, code } }`.
- `WaveDashboard` (DL-3 / DL-4): `{ releasedToday, open, atRisk, avgCompletionMinutes7d?, openExceptions, deferredNotInWave }` (paket §8.15; KPI'lar okuma anında).
- `WaveException` (DL-3): paket §4.4 alanları — `{ exceptionId, waveId, waveNumber, warehouseId, kind, outboundId, outboundNumber, taskId?, taskNumber?,
  lineSeq?, detectedAt, status: Open | Resolved | Ignored, resolution? { action: RemoveOrder | MoveToNextWave | Ignore | SystemCleared,
  targetWaveId?, by?, at, reason? }, version }`.
- `WaveKpiSeries` (DL-5): `{ warehouseId, from, to, waveType?, days[] { date (depo yerel günü), releasedCount, avgCompletionMinutes?,
  cutoffHitRate?, linesPerPickerHour? } }` (paket §8.16).
- `PendingRelease` (DL-2, ayar kapatma cevabı eki): `{ issuedCount, failed[] { outboundId, outboundNumber, code }, remainingCount, stopReason? }`;
  `stopReason` ∈ `ConnectionError` \| `RepeatedOrderErrors` \| `NewStuckLimit` \| `Reopened` (ilk turda; `RoundBudgetReached` ve
  `ListFullSkipLimit` yalnız bakım turunda, kabloya çıkmaz); `failed[].code` ∈ `WAVE_INVENTORY_UNAVAILABLE` \| `WAVE_PENDING_ORDER_ERROR` (paket §8.19 Akış adım 3).

## 4. Operations

### 4.1 DL-1 — bağlayıcı (14 işlem)

| Yöntem + yol | operationId | x-permission | Gövde / parametre | Başarı | x-error-codes |
|---|---|---|---|---|---|
| `GET /waves` | `listWaves` | `warehouse.waves.read` | `warehouseId` (zorunlu), `status*` (tekrarlanabilir), `waveType?`, `q?` (numara), `cutoffFrom?`, `cutoffTo?` (`date-time`), `page`, `pageSize` | 200 sayfa `Wave` (sıra: `cutoffAtUtc` artan, sonra `number`) | INVALID_REQUEST |
| `POST /waves` | `createWave` | `warehouse.waves.create` | `{ idempotencyKey, warehouseId, waveType, cutoffLocal, shipToFilter?, maxOrders?, maxLines?, responsibleUserId?, note? }` | 201 `Wave` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, UNKNOWN_LOCATION, WAVE_SETTINGS_REQUIRED, WAVE_CUTOFF_INVALID, WAVE_RESPONSIBLE_INVALID, WAVE_RESPONSIBLE_DIRECTORY_UNAVAILABLE, WAVE_COMPANY_CODE_INVALID, LEGAL_ENTITY_MASTER_UNAVAILABLE, WAVE_RECORD_CODE_UNAVAILABLE, WAVE_INVENTORY_UNAVAILABLE |
| `GET /waves/{waveId}` | `getWave` | `warehouse.waves.read` | — | 200 `Wave` + ETag | WAVE_NOT_FOUND |
| `POST /waves/{waveId}/update` | `updateWave` | `warehouse.waves.update` | If-Match; `{ idempotencyKey, warehouseId, waveType, cutoffLocal, shipToFilter?, maxOrders?, maxLines?, responsibleUserId?, note? }` — **Taslak formunun tamamı** (yok olan isteğe bağlı üye temizlenir); `warehouseId` dalganınkine eşit olmalı; yalnız `Draft` | 200 `Wave` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WAVE_NOT_FOUND, WAVE_CONFLICT, WAVE_INVALID_STATE, WAVE_WAREHOUSE_IMMUTABLE, WAVE_CUTOFF_INVALID, WAVE_RESPONSIBLE_INVALID, WAVE_RESPONSIBLE_DIRECTORY_UNAVAILABLE |
| `GET /waves/{waveId}/orders` | `listWaveOrders` | `warehouse.waves.read` | `state*` (tekrarlanabilir; varsayılan bütün durumlar), `page`, `pageSize` (sıra: `addedAt`, sonra `outboundNumber`) | 200 sayfa `WaveOrder` | WAVE_NOT_FOUND, INVALID_REQUEST |
| `POST /waves/{waveId}/orders` | `addWaveOrders` | `warehouse.waves.update` | If-Match (dalga); `{ idempotencyKey, outboundIds[], via? }` (`via` = `Manual` (varsayılan) \| `Suggestion`); yalnız `Draft` | 200 `WaveOrdersResult` + ETag (dalga; replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WAVE_NOT_FOUND, WAVE_CONFLICT, WAVE_INVALID_STATE, WAVE_SETTINGS_REQUIRED, WAVE_ORDER_NOT_ELIGIBLE, WAVE_ORDER_IN_OTHER_WAVE, WAVE_TOO_LARGE, WAVE_INVENTORY_UNAVAILABLE |
| `POST /waves/{waveId}/orders/remove` | `removeWaveOrders` | `warehouse.waves.update` | If-Match (dalga); `{ idempotencyKey, outboundIds[], reason? }`; yalnız `Draft` | 200 `WaveOrdersResult` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WAVE_NOT_FOUND, WAVE_CONFLICT, WAVE_INVALID_STATE |
| `GET /waves/{waveId}/suggestions` | `listWaveSuggestions` | `warehouse.waves.update` | — (süzgeç dalganın `shipToFilter`'ı; sıra outbound `createdAt` artan; sayı kalan `maxOrders` / `maxLines` kapasitesi, sınır yoksa 200) — **yazmaz**; yalnız `Draft` | 200 `{ items: [WaveCandidate], limitedBy: "MaxOrders" \| "MaxLines" \| "Default", contractVersion }` | WAVE_NOT_FOUND, WAVE_INVALID_STATE, WAVE_INVENTORY_UNAVAILABLE |
| `POST /waves/{waveId}/cancel` | `cancelWave` | `warehouse.waves.cancel` | If-Match; `{ idempotencyKey, reason }` — yalnız `Draft`; durum `Cancelled`, etkin üyelikler `Removed` (silinmez) | 200 `Wave` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WAVE_REASON_REQUIRED, WAVE_NOT_FOUND, WAVE_CONFLICT, WAVE_INVALID_STATE |
| `GET /waves/candidates` | `listWaveCandidates` | `warehouse.waves.read` | `warehouseId` (zorunlu), `q?` (outbound numarası / `orderRef`), `shipTo?` (ad / şehir / posta / ülke içerir), `page`, `pageSize` (sıra: `createdAt` artan) | 200 sayfa `WaveCandidate` | INVALID_REQUEST, WAVE_INVENTORY_UNAVAILABLE |
| `GET /waves/{waveId}/load` | `getWaveLoad` | `warehouse.waves.read` | — | 200 `WaveLoad` | WAVE_NOT_FOUND, WAVE_INVENTORY_UNAVAILABLE |
| `GET /waves/{waveId}/events` | `listWaveEvents` | `warehouse.waves.read` | `page`, `pageSize` (sıra: `seq`) | 200 sayfa `WaveEvent` | WAVE_NOT_FOUND, INVALID_REQUEST |
| `GET /waves/settings/{warehouseId}` | `getWaveWarehouseSettings` | `warehouse.waves.read` | — | 200 `WaveWarehouseSettings` + ETag | UNKNOWN_LOCATION, WAVE_INVENTORY_UNAVAILABLE |
| `POST /waves/settings/{warehouseId}/update` | `updateWaveWarehouseSettings` | `warehouse.waves.settings.configure` | If-Match (`W/"0"` kayıt yokken); `{ idempotencyKey, waveManaged, timeZoneId?, riskMinutesBeforeCutoff, riskProgressPercent, pendingTaskMinutes, reason }` — bütün ayar (yok olan isteğe bağlı üye temizlenir); `waveManaged = true` iken `timeZoneId` zorunlu; `deferredOrderCount` / `releasingOrderCount` / `stuckOrders` / `gateSeq` / `pendingReleaseStuck` gövdede → 400 | 200 `WaveWarehouseSettings` (+ `pendingRelease`, yalnız `true → false` isteğinde — §3.9, DL-2) + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WAVE_REASON_REQUIRED, UNKNOWN_LOCATION, WAVE_CONFLICT, WAVE_INVENTORY_UNAVAILABLE |

**Yaratma sırası (MOD-0181 `createCycleCount` / MOD-0178 `CreatePutawayTaskHandler` emsali):** (1) gövde şekli (400); (2) yaratanın anahtarı —
aynı anahtar + aynı gövde → 200 replay, numara **alınmaz**; farklı gövde → 400; (3) iş kuralları (depo LOCATION'da `Warehouse`, `Active`, şirketin — aksi 422 `UNKNOWN_LOCATION`, §5.2; LOCATION okunamazsa 503 `WAVE_INVENTORY_UNAVAILABLE`;
depo ayarı `waveManaged = true` + `timeZoneId`; kesim dönüşümü; sorumlu; şirket öneki); (4) numara (geçici sayaç, işleme **katılmaz**); (5) tek
işlem (dalga + `Created` olayı + merkezi denetim); (6) işlem tekil index'e çarparsa önce anahtar yeniden aranır — ikizin kaydı varsa 200 replay.
Numarası alınıp yazılamayan istek numarayı **atlar** (yeniden verilmez).

**Ekleme (paket §8.2 kural 3, §8.5):** sıra — anahtar (kural 5) → `If-Match` → `Draft` → satır uygunluğu (hepsi ya da hiçbiri, §3.6) → teknik üst
sınır (500 sipariş / 2.000 görev → 422 `WAVE_TOO_LARGE`) → tek işlem: üyelikler (`Planned`, `Active = true`), dalga `version` + 1, `OrdersAdded`
olayı, merkezi denetim **ve** depo ayarı kaydına koşullu yazım (`waveManaged = true` + `gateSeq` + 1; eşleşmezse 422 `WAVE_SETTINGS_REQUIRED`, yazım
yok — paket §8.19 "Sıralama"). Eşzamanlı iki eklemede `wms_wave_orders` tekil kısmi index'i `(TenantId, LegalEntityId, OutboundId)`
`{ Active: true }` ikinciyi reddeder → 409 `WAVE_ORDER_IN_OTHER_WAVE`, kaybedende kayıt yok (paket AC-11). `iş sınırı` aşımı yazar (D-7).

**Kesim (paket §8.4):** `cutoffAtUtc = ToUtc(cutoffLocal, timeZoneId)`; yaz saati boşluğundaki yerel saat → 400 `WAVE_CUTOFF_INVALID`; çakışan
yerel saat → erken ofset + `cutoffAmbiguousResolved: true`; `cutoffAtUtc < şimdi − 1 saat` → 400 `WAVE_CUTOFF_INVALID`. Dönüşüm sunucunun
saat dilimini kullanmaz (paket AC-14).

**Ayar (paket §4.5, AC-15 a…e):** okuma `read`, yazma `settings.configure` (yalnız `update` taşıyana 403); gerekçe zorunlu (400
`WAVE_REASON_REQUIRED`); aralık dışı eşik ve geçersiz IANA kimliği → 400 `INVALID_REQUEST` (`details.field`), kayıt değişmez; başka şirketin deposu →
404 `UNKNOWN_LOCATION`; `false → true` ve eşik değişikliği her zaman 200. `true → false` reddedilmez (Y-1 = A); bekleyen siparişlerin görev açılışı
(`pendingRelease`) DL-2'dedir (paket AC-15 f…q). Yazım `SettingsUpdated` olayını (akış `Settings`) ve merkezi denetimi aynı işlemde yazar.

### 4.2 DL-2 … DL-5 — ön taslak (9 işlem; paket §3; her dilimde bağlayıcı olur)

| Yöntem + yol | operationId | x-permission | Gövde / parametre | Başarı | x-error-codes | Dilim |
|---|---|---|---|---|---|---|
| `POST /waves/{waveId}/release` | `releaseWave` | `warehouse.waves.release` | If-Match; `{ idempotencyKey, confirmLimitOverride? }` | 200 `WaveReleaseResult` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WAVE_NOT_FOUND, WAVE_CONFLICT, WAVE_INVALID_STATE, WAVE_SETTINGS_REQUIRED, WAVE_EMPTY, WAVE_ORDERS_INELIGIBLE, WAVE_TOO_LARGE, WAVE_LIMIT_CONFIRMATION_REQUIRED, WAVE_RELEASE_IN_PROGRESS, WAVE_RELEASE_OUTCOME_UNKNOWN, WAVE_NOTHING_ISSUED, WAVE_INVENTORY_UNAVAILABLE | DL-2 |
| `GET /waves/{waveId}/tasks` | `listWaveTasks` | `warehouse.waves.read` | `page`, `pageSize` (sıra: `sequence`) | 200 sayfa `WaveTask` | WAVE_NOT_FOUND, INVALID_REQUEST, WAVE_INVENTORY_UNAVAILABLE | DL-2 |
| `POST /waves/{waveId}/recall` | `recallWaveRemaining` | `warehouse.waves.recall` | If-Match; `{ idempotencyKey, reason }`; `Released` \| `Picking` | 200 `WaveRecallResult` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WAVE_REASON_REQUIRED, WAVE_NOT_FOUND, WAVE_CONFLICT, WAVE_INVALID_STATE, WAVE_INVENTORY_UNAVAILABLE | DL-3 |
| `GET /waves/dashboard` | `getWaveDashboard` | `warehouse.waves.read` | `warehouseId` (zorunlu) | 200 `WaveDashboard` | INVALID_REQUEST, WAVE_INVENTORY_UNAVAILABLE | DL-3 (KPI kartları DL-4 ekranında) |
| `GET /waves/exceptions` | `listWaveExceptions` | `warehouse.waves.exceptions.read` | `warehouseId` (zorunlu), `status*`, `kind*`, `waveId?`, `page`, `pageSize` (sıra: `detectedAt` azalan) | 200 sayfa `WaveException` | INVALID_REQUEST | DL-3 |
| `GET /waves/exceptions/{exceptionId}` | `getWaveException` | `warehouse.waves.exceptions.read` | — | 200 `WaveException` + ETag | WAVE_EXCEPTION_NOT_FOUND | DL-3 |
| `POST /waves/exceptions/{exceptionId}/resolve` | `resolveWaveException` | `warehouse.waves.exceptions.resolve` | If-Match; `{ idempotencyKey, action: RemoveOrder \| MoveToNextWave \| Ignore, targetWaveId?, reason }` | 200 `{ exception, wave?, targetWave? }` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WAVE_REASON_REQUIRED, WAVE_EXCEPTION_NOT_FOUND, WAVE_CONFLICT, WAVE_INVALID_STATE, WAVE_ORDER_STARTED, WAVE_TARGET_INVALID, WAVE_SETTINGS_REQUIRED, WAVE_TOO_LARGE, WAVE_INVENTORY_UNAVAILABLE | DL-3 |
| `GET /waves/kpis` | `getWaveKpis` | `warehouse.waves.kpis.read` | `warehouseId` (zorunlu), `from`, `to` (`date`, depo yerel günü), `waveType?` | 200 `WaveKpiSeries` | INVALID_REQUEST, WAVE_KPI_RANGE_TOO_LARGE | DL-5 |
| `GET /waves/kpis/export` | `exportWaveKpis` | `warehouse.waves.kpis.export` | `getWaveKpis` ile aynı | 200 `text/csv; charset=utf-8` (başlık `Accept-Language`'da, 7 dil; sayılar değişmez kültür) | INVALID_REQUEST, WAVE_KPI_RANGE_TOO_LARGE | DL-5 |

**İşlem sayımı:** §4.1 14 + §4.2 9 = **23** (paket §3 uç listesiyle birebir; paket AC-1, AC-55).

## 5. Error codes

### 5.1 WMS-WAVE-BUNDLE'ın kendi kodları (paket §8.17 "öneri" listesi burada kesinleşir)

| Kod | HTTP | Ne zaman | Dilim |
|---|---|---|---|
| `WAVE_NOT_FOUND` | 404 | dalga yok, başka kiracı / şirket | DL-1 |
| `WAVE_EXCEPTION_NOT_FOUND` | 404 | istisna yok, başka kiracı / şirket | DL-3 |
| `WAVE_CONFLICT` | 409 | `If-Match` bayat (D-14) | DL-1 |
| `WAVE_INVALID_STATE` | 409 | durumda izinli olmayan komut (ör. `Draft` olmayan dalgada `update` / `orders` / `cancel` / `suggestions`) | DL-1 |
| `WAVE_SETTINGS_REQUIRED` | 422 | depo dalga yönetimli değil ya da saat dilimi yok — yaratma, ekleme, serbest bırakma, "Sonraki dalgaya taşı" | DL-1 |
| `WAVE_CUTOFF_INVALID` | 400 | yaz saati boşluğu, `cutoffAtUtc < şimdi − 1 saat` | DL-1 |
| `WAVE_WAREHOUSE_IMMUTABLE` | 400 | `updateWave`'de `warehouseId` dalganınkinden farklı | DL-1 |
| `WAVE_ORDER_NOT_ELIGIBLE` | 422 | eklenen outbound uygun değil (`details.orders[]`) | DL-1 |
| `WAVE_ORDER_IN_OTHER_WAVE` | 409 | outbound başka etkin dalgada ya da eşzamanlı ekleme kaybedildi (D-8) | DL-1 |
| `WAVE_TOO_LARGE` | 422 | teknik üst sınır (500 / 2.000) | DL-1 |
| `WAVE_REASON_REQUIRED` | 400 | gerekçesiz / 10..500 dışı iptal, ayar, geri çekme, istisna eylemi | DL-1 |
| `WAVE_COMPANY_CODE_INVALID` | 422 | MDM şirket `Code`'u `^[A-Z]{2,5}$` değil | DL-1 |
| `WAVE_RECORD_CODE_UNAVAILABLE` | 503 | sayaç yazılamadı / tür reddedildi; dalga yaratılmaz | DL-1 |
| `WAVE_INVENTORY_UNAVAILABLE` | 503 | MOD-0178 / LOCATION okuması açılamadı; kapatma turunda bağlantı hatası satır kodu (DL-2) | DL-1 |
| `WAVE_RESPONSIBLE_INVALID` | 422 | `responsibleUserId` kiracının / şirketin etkin kullanıcısı değil — **ek** (paket §12 kuralının kodu; §8.17'de adsız; MOD-0181 `COUNT_ASSIGNEE_INVALID` emsali) | DL-1 |
| `WAVE_RESPONSIBLE_DIRECTORY_UNAVAILABLE` | 503 | kullanıcı dizini cevap vermedi; yazım yok — **ek** (MOD-0181 `COUNT_ASSIGNEE_DIRECTORY_UNAVAILABLE` emsali) | DL-1 |
| `WAVE_EMPTY` | 422 | siparişsiz serbest bırakma | DL-2 |
| `WAVE_ORDERS_INELIGIBLE` | 422 | serbest bırakma ön kontrolünde uygun olmayan sipariş (`details.orders[] { outboundId, code }`) | DL-2 |
| `WAVE_LIMIT_CONFIRMATION_REQUIRED` | 409 | sınır aşımı onaysız (D-7; `details.limitWarnings[]`) | DL-2 |
| `WAVE_RELEASE_IN_PROGRESS` | 409 | `Releasing` dalgada aynı anahtar ya da yeni istek | DL-2 |
| `WAVE_RELEASE_OUTCOME_UNKNOWN` | 503 | 0178 portu sonuç vermedi; bakım tamamlar | DL-2 |
| `WAVE_NOTHING_ISSUED` | 422 | serbest bırakmada hiç görev açılamadı; dalga `Draft`'a döner | DL-2 |
| `WAVE_PENDING_ORDER_ERROR` | — (yalnız satır kodu) | kapatma turunda siparişe özgü hata (`pendingRelease.failed[]`); HTTP 200 kalır | DL-2 |
| `WAVE_ORDER_STARTED` | 409 | başlamış siparişi çıkarma / taşıma | DL-3 |
| `WAVE_TARGET_INVALID` | 422 | taşıma hedefi yok / `Draft` değil / başka depo | DL-3 |
| `WAVE_KPI_RANGE_TOO_LARGE` | 400 | > 92 gün | DL-5 |

### 5.2 Yeniden tanımlanmayan, kullanılan kodlar
`InventoryErrorCodes`'tan; bu sözleşme anlamlarını değiştirmez:

| Kod | HTTP | Bu sözleşmede ne zaman |
|---|---|---|
| `INVALID_REQUEST` | 400 | gövde / sorgu şekli, eksik `If-Match`, `tenantId` / `legalEntityId` ya da sunucu üyesi istekte |
| `IDEMPOTENCY_KEY_REUSED` | 400 | aynı anahtar, farklı gövde (paket §8.17) |
| `UNKNOWN_LOCATION` | **422** (gövdede) / **404** (yolda) | depo yok / `Warehouse` değil / `Active` değil / başka şirketin. Gövdedeki `warehouseId` (`createWave`) → 422 (MOD-0181 `createCycleCount` emsali, `CycleCountChecks.cs:68`); yoldaki `{warehouseId}` (`getWaveWarehouseSettings`, `updateWaveWarehouseSettings`) → 404 (paket AC-15 d, AC-3) — §9 açık nokta 8 |
| `LEGAL_ENTITY_MASTER_UNAVAILABLE` | 503 | MDM tüzel kişilik okunamadı (şirket öneki; scope middleware — servis geneli) |
| `CROSS_LE_FORBIDDEN` | 403 | başlık / token / üyelik uyuşmazlığı (scope middleware — servis geneli, her işlemde) |

## 6. State machine ve kurallar

### 6.1 Dalga durumu (paket §8.3)

| Durum | Geçiş | Operasyon | Dilim |
|---|---|---|---|
| `Draft` | → `Cancelled` (silinmez; etkin üyelikler `Removed`) | `cancelWave` (gerekçe zorunlu) | DL-1 |
| `Draft` | → `Releasing` → `Released` | `releaseWave` (+ bakım `CompleteWaveReleaseCommand`) | DL-2 |
| `Releasing` | → `Draft` (`ReleaseAborted`, hiç görev açılmadı) | `releaseWave` / bakım | DL-2 |
| `Released` | → `Picking` | sistem (uzlaştırma) | DL-3 |
| `Released` \| `Picking` | → `Completed` \| `PartiallyCompleted` | sistem (uzlaştırma) | DL-3 |
| `Released` \| `Picking` | (durum değişmez) | `recallWaveRemaining` | DL-3 |
| `Completed` \| `PartiallyCompleted` \| `Cancelled` | son durum | her dalga yazması 409 `WAVE_INVALID_STATE` | — |

`Draft` dışındaki dalgada `updateWave`, `addWaveOrders`, `removeWaveOrders`, `listWaveSuggestions`, `cancelWave` → 409 `WAVE_INVALID_STATE`.

### 6.2 Üyelik durumu (paket §4.2)
`Planned` (Taslakta, DL-1) → `Removed` (Taslaktan çıkarma ya da iptal, DL-1) · `Planned` → `Issued` \| `Excluded` (DL-2) · `Issued` → `Withdrawn` \|
`OrderCancelled` (DL-3). `Active = true` ⇔ `Planned` \| `Issued`.

### 6.3 Kesim saati (paket §8.4) — §4.1 "Kesim" paragrafı.

## 7. MOD-0178 port yüzü (bilgi — kablo değil; paket §8.6, X-4; DL-2)

0180 bu portları süreç içi çağırır; HTTP uçları yoktur. Sahibi ve uygulayıcısı MOD-0178'dir.
- `IPickIssuancePolicy` (0178; varsayılan `ImmediatePickIssuancePolicy`): depo için `Immediate` \| `Deferred`; 0180 `WavePickIssuancePolicy`'yi
  kaydeder (`waveManaged = true` → `Deferred`).
- `IWavePickIssuance.IssueAsync({ WaveId, WaveNumber, ActorUserId, OutboundId, Items[] { AllocSeq, LocationId, Sequence } })` → `Issued { TaskIds,
  TaskNumbers }` \| `Ineligible { Code: OutboundCancelled | NotDeferred | IssuedToOtherWave | PlanMismatch | NotAllocated }`; sipariş başına bir
  çağrı, 0178 işleminde hepsi ya da hiçbiri; aynı dalgayla `Issued` → replay.
- `IWavePickIssuance.WithdrawAsync({ WaveId, OutboundId, ActorUserId, Reason })` → `Withdrawn` \| `Started`; replay.
- `IWavePickIssuance.IssueImmediateAsync({ OutboundId, ActorUserId, Reason })` → `Issued` \| `Ineligible { OutboundCancelled | NotAllocated |
  NotDeferred | IssuedToOtherWave }`; dalga yönetimini kapatma turu (paket §8.19).
- Teknik hata / zaman aşımı istisna olarak yükselir (sonuç bilinmiyor). Portun yazımları çağıran 0180 komutunun merkezi kaydına enlist olur.

## 8. Merkezi denetim (paket §8.13, §21)

Her yazma komutu BL-501 kaydı bildirir: kategori `MasterData`, kaynak modül `WAVE-PLANNING`, işlem Platform enum adı. DL-1 komutları:
`CreateWaveCommand` (`Create`), `UpdateWaveCommand` (`Update`), `AddWaveOrdersCommand` (`Update`), `RemoveWaveOrdersCommand` (`Update`),
`CancelWaveCommand` (`LifecycleTransition`), `UpdateWaveWarehouseSettingsCommand` (`Update`). Kayıt ad ve gövde taşımaz; değerler ve gerekçeler
yalnız `wms_wave_events.Data`'da. Başarılı ve yazan komut 1 kayıt; replay / 0 kayıt 0; başarısız 0. KPI ve CSV okumadır (kayıt yok).

## 9. Açık noktalar (paketle çelişki değil; CT incelemesine / dondurmaya öneri)

1. **Ek kodlar:** §5.1'deki `WAVE_RESPONSIBLE_INVALID` ve `WAVE_RESPONSIBLE_DIRECTORY_UNAVAILABLE` paket §8.17'de adsızdı; paket §12'nin
   "kullanıcı dizininde, şirkette etkin" kuralına kod verirler (MOD-0181 `COUNT_ASSIGNEE_*` emsali). Paket revizyonunda §8.17'ye eklenmesi önerilir.
2. **Ekleme hepsi-ya-da-hiçbiri:** paket §8.2 kural 3 "her biri uygun olmalı (aksi 422 …, satır sonucu)" der; taslak bunu "bir satır uygun değilse
   istek bütünüyle reddedilir, satır sonuçları `error.details.orders[]`'ta" diye okur (yazım yok). Kısmi ekleme isteniyorsa dondurmadan önce söylenmeli.
3. **Ayarın ilk yazımı:** paket "yaratma dışı her yazma `If-Match` ister" der, ayar kaydı ilk yazımda yaratılır. Taslak kayıt yokken GET'te
   `version: 0` döner ve ilk yazımı `If-Match: W/"0"` ile kabul eder (eşzamanlı iki ilk yazımda tekil index ikinciyi 409 `WAVE_CONFLICT`'e çevirir).
4. **`listWaveSuggestions` izni:** paket §14 öneriyi `update` satırına yazar (Taslağa ekleme hazırlığı); taslak `update` ister. `read` taşıyan
   ama `update` taşımayan kullanıcı öneri göremez.
5. **`getWaveLoad` DL-1'de:** WP X-14 dağılımı yük ucunu DL-1'de sayar; ertelenmiş outbound DL-1'de oluşamadığı için `zones` ve `pickers` DL-1'de
   boştur, uç şekliyle teslim edilir; dolu davranış DL-2 / DL-3'te ölçülür.
6. **`updateWaveWarehouseSettings` `true → false` DL-1'de:** paket Y-1 = A reddetmez; DL-1'de bekleyen (`Deferred`) outbound ve Taslak `Planned`
   üyeliği oluşamaz (paket AC-15 başlığı), bu yüzden DL-1 yazımı yalnız ayarı yazar ve `pendingRelease` üyesini **yazmaz**; kapatma turu ve
   `pendingRelease` DL-2'de bağlayıcı olur (AC-15 f…q). Dondurma 1.0.0'ı DL-5 sonrası yapılacaksa üye o sürümde baştan vardır.
7. **`WaveEvent.system`:** paket olayın aktörünü `ActorUserId | system` diye yazar; taslak kabloda `actorUserId` ya da `system: true`'dan birini yazar.
8. **`UNKNOWN_LOCATION` iki durum koduyla:** servis kodu gövdedeki bilinmeyen depoya 422 verir (MOD-0181 emsali); paket AC-15 (d) / AC-3 ayar
   ucunda başka şirketin deposuna **404** ister. Taslak aynı kodu yoldaki `{warehouseId}` için 404, gövdedeki `warehouseId` için 422 ile kullanır
   (§5.2). Dondurmada kod başına tek durum isteniyorsa ayar uçları için ayrı bir 404 kodu (ör. `WAVE_SETTINGS_NOT_FOUND`) seçilmeli — CT kararı.
9. **`WaveOrder.version` ve `If-Match`:** paket §8.12 `WaveOrder`'ı da `Version` taşıyan kayıt sayar; §3'te üyeliği tek başına değiştiren uç yok.
   Taslak üyelik yazımlarında **dalganın** `If-Match`'ini ister (kural 6) ve `WaveOrder.version`'ı yalnız okuma üyesi olarak yazar; sistem
   yazımları (DL-2 / DL-3) üyelik `Version`'ını depoda koşullu kullanır.
10. **LOCATION okunamazsa (paket §13 "0178 / LOCATION erişilemez → 503, yazım yok"):** `createWave` ve `updateWaveWarehouseSettings` de
    `WAVE_INVENTORY_UNAVAILABLE` taşır (MOD-0181 `createCycleCount` bu durumu kodlamaz; burada paket §13 / §8.17 geçerlidir).

## 10. Doğrulama kaydı (geliştirici oturumu, 2026-11-13)

Bu dosya paket r11'e (LF sha256 `226ab7f9…cb095`) karşı satır satır doğrulandı: §3 uç listesi (23 işlem; DL-1 14 = WP X-14 dağılımı), §4 alanlar
ve index'ler, §8.1–§8.5, §8.6 port yüzü, §8.12–§8.14, §8.17 kodları (24 kodun hepsi burada, HTTP durumları eşit; 2 ek kod §9 madde 1), §12, §13,
§14 izin anahtarları (11 anahtar, işlem başına eşleşme), §21 denetim işlemleri, §22 kayıt kodu, AC-1…AC-17. Doğrulamada düzeltilenler: (1)
`createWave` ve `updateWaveWarehouseSettings` hata listesine `WAVE_INVENTORY_UNAVAILABLE` (§9 madde 10); (2) §5.2'de yeniden kullanılan kodların
HTTP durumları — `UNKNOWN_LOCATION` gövdede 422 / yolda 404 (§9 madde 8); (3) §3.1 isteğe bağlı üye listesi; (4) §3.4'teki yanlış atıf (§8 → §9);
(5) §9'a madde 8, 9, 10. §4.2 (DL-2 … DL-5) ön taslaktır: ad, izin ve hata kodu düzeyinde doğrulandı, gövde ayrıntısı kendi diliminde bağlanır.

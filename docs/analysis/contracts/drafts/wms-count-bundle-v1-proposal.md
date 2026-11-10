# WMS-COUNT-BUNDLE v1 — proposal (MOD-0181 Cycle Counting / Sayım)

> **Durum: TASLAK (proposal) — sözleşme değil.** `docs/analysis/contracts/` altında WMS-COUNT-BUNDLE yok (`git grep WMS-COUNT` docs +
> execution: yalnız MOD-0181 paketi). Blueprint 8.8 satır 182 sözleşmeyi "WMS-COUNT-BUNDLE (count tasks, variance schema, approvals optional,
> evidence export)" diye adlandırır. Dondurma ayrı CT adımıdır (MOD-0178 §19.7 deseni): DL-3 CT kabulünden sonra tek yazıcı adımıyla
> `docs/analysis/contracts/wms-count-bundle.openapi.yaml` (`info.version: 1.0.0`, `x-status: FROZEN`, `x-owner: MOD-0181`,
> `x-owner-service: Diten.InventoryService`) yazılır; donduktan sonra yalnız eklemeli değişiklik (K16).
>
> **Kaynak (davranış otoritesi):** paket `execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md` **r7**
> (sha256 `acf91c8506f4ff0e4468d89fdacdb2f73ef20e6c332b560f895d51449930dedd`, onay satırı `execution/approvals/approvals.tsv`, sahip «onayla»
> Beste 2026-11-06). Bu dosya yalnız kablo şeklidir; paketle çelişirse paket kazanır ve bu dosya düzeltilir. Sahip kararları Q-1…Q-24
> (paket §19.4) ve F-1 = A (PUT / DELETE yok, komut uçları; tek istisna `PUT /count-settings`).
>
> **Dilim durumu:** §4.1 (DL-1) ve §4.2 (DL-2) **bağlayıcı taslak** — kod bu tablolara karşı yazılır ve `CycleCountDraftContract` (test
> projesi) her `/api/warehouse/cycle-counts*` cevabını bu şemalara karşı doğrular. §4.3 (DL-3 … DL-5) paketin §3 / §8 / §16'sından çıkarılmış
> **ön taslaktır**; her dilim kendi satırlarını bağlayıcı yapar (gövde ayrıntısı o dilimin ilk adımında kesinleşir).

## 1. Rules

1. Sunucu `/api/warehouse` (Gateway → `Diten.InventoryService` **5067**, mevcut `/api/warehouse/{everything}` rotası; `ocelot.json` değişmez —
   Q-15 = A). Kiracı kabuğu kullanıcıları (`actor_type = tenant_user`).
2. `TenantId` token'dan, şirket `X-Legal-Entity-Id`'den sunucuda çözülür (servisin scope middleware'i). Başka kiracı / başka şirket / bilinmeyen
   id → **404**. `tenantId` / `legalEntityId` gövdede → 400 `INVALID_REQUEST` (istek okuyucu), sorguda → 400 `INVALID_REQUEST` (scope middleware).
3. Zarf: InventoryService'in ölçülmüş kablosu — başarı gövdesi şemanın kendisi (sarmalanmamış); hata `Error` =
   `{ error: { code, message, details?, correlationId }, contractVersion: "v1" }` (INVENTORY `Error`; WMS-BUNDLE §1 kural 3 ile aynı).
4. **Boş isteğe bağlı üye yazılmaz** (JSON `null` yok; WMS-BUNDLE §1 kural 4). İstekte `null` isteğe bağlı üyenin yokluğu sayılır.
5. Her yazma `Idempotency-Key` başlığı alır, gövde `idempotencyKey` ile eşit (8–100). Aynı anahtar + aynı gövde → **200 replay** (kaydın güncel
   hâli; ilk cevabın kodu tekrarlanmaz), farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`. Anahtar isteyen kullanıcı + operasyon (`operationId`) +
   hedef kayıt kapsamında saklanır (yaratma: kayıtta, yaratanla; diğerleri: komutun ilk yazımının olayında) ve **`If-Match`'ten ve bütün iş
   kurallarından önce** aranır. Parmak izi = operasyon + hedef (`countId`) + normalleşmiş gövde; başlıklar girmez. Eşzamanlı iki özdeş istekten
   kaybeden 200 replay alır. Hiç yazmadan reddedilen istek anahtar bırakmaz.
6. Değişebilir kayıt (sayım) değişiklikte `If-Match: W/"<version>"` ister; eksik → 400 `INVALID_REQUEST`, bayat → 409 `COUNT_CONFLICT`. Cevap
   `ETag: W/"<version>"`. Bayatlık kural 5'ten sonra değerlendirilir.
7. **Bu sözleşme stok bakiyesi tutmaz ve stok değiştirmez** (paket §2.3). Beklenen miktar (`expectedQuantity`) anlık görüntü kanıtıdır, bir kez
   yazılır. Stok farkı yalnız DL-3'te, INVENTORY `postMovement`'in süreç içi yolundan `COUNT_GAIN` / `COUNT_LOSS` olarak yazılır
   (`sourceModule: MOD-0181`, `sourceType: CYCLE_COUNT`, anahtar `cycle-count:{lineId}`). Yeni stok durumu ya da hareket tipi tanımlanmaz.
8. **PUT / DELETE yok** (F-1 = A): güncelleme, iptal ve toplu silme `POST` komut uçlarıdır. Tek istisna DL-3'ün `PUT /count-settings`'idir
   (`CountVariancesController:count-settings`, paket §5 / AC-51).
9. Tarihler: `date` = UTC takvim günü (`yyyy-MM-dd`), `date-time` = UTC an (`…Z`, milisaniye).
10. Miktarlar: decimal string (`^-?\d+(\.\d{1,6})?$`), SKU'nun **baz biriminde** (`baseUomId`).
11. Correlation: `X-Correlation-Id` her cevapta (servis geneli).

## 2. Sabitler

| Sabit | Değer | Kaynak |
|---|---|---|
| Modül / manifest kodu | `MOD-0181` / `CYCLE-COUNTING` (SortOrder 181) | paket §14, Q-16 = A |
| Kayıt kodu | `{ŞİRKET}-CNT-{000001}` — MOD-0178 geçici sayacı, tür `CNT` (`WarehouseRecordCodeRules.TypePrefixes`'e eklemeli, XMC-001) | paket §8.12, §22, Q-7 = B |
| Kapsam düğümü sayısı | `Cycle`'da 1..200 (`StorageLocation` \| `Zone` \| `Bin`), `Full`'da 0 | paket §4.1 |
| Kapsam SKU sayısı | 0..200, tekrarsız | paket §4.1 |
| Not | ≤ 500 karakter (trim; boş = yok) | paket §4.1 |
| Gerekçe (iptal, toplu silme) | 1..500 karakter (trim) | paket §12 |
| Satır sınırı (`MaxLinesPerCount`) | 2000 (DL-1'de sabit varsayılan; DL-3'te şirket ayarı) | Q-24 = A |
| Anlık görüntü penceresi W | 10 dakika | paket §8.2 madde 4 |
| Toplu silme satırı | ≤ 200 | liste sayfası (WMS-BUNDLE emsali) |
| Sayfa | `page` ≥ 1 (varsayılan 1), `pageSize` 1..200 (varsayılan 25) | WMS-BUNDLE §3.8 |
| Hareket gerekçesi (DL-3) | sabit opak `CYCLE_COUNT_VARIANCE` | Q-17 = A |

## 3. Schemas

### 3.1 `CycleCount`
```json
{
  "countId": "uuid", "number": "GMG-CNT-000001", "warehouseId": "uuid",
  "countType": "Cycle | Full", "blind": false,
  "scopeLocationIds": ["uuid"], "scopeSkuIds": ["uuid"],
  "plannedDate": "2026-11-07", "assignedTo": "uuid?", "note": "string?",
  "status": "Draft | Released | Counting | Submitted | Closed | Cancelled",
  "snapshotAt": "date-time?", "scopeBinIds": ["uuid"], "lineCount": 0,
  "recountOfId": "uuid?",
  "createdBy": "uuid", "createdAt": "date-time", "updatedAt": "date-time?",
  "cancelledBy": "uuid?", "cancelledAt": "date-time?", "cancelReason": "string?",
  "version": 1, "contractVersion": "v1"
}
```
- `scopeBinIds` ve `snapshotAt` `release`'ten sonra yazılır, sonra değişmez; `Draft`'ta `scopeBinIds = []`, `snapshotAt` yok.
- `snapshotTransactionIds` kablo üyesi **değildir** (iç kanıt; Evidence Pack'te — DL-5).
- DL-2 ekleri (`submittedBy`, `submittedAt`) eklemeli üyedir — submit'te sunucu yazar.
- **Durum ve satır durumu metinleri (SB3/F-2):** ekranda ve kanıtta etiket, değerin kendisi değil yerelleştirme anahtarıdır — sayım durumu
  `CycleCount.Status.{Status}`, satır durumu `CycleCount.LineStatus.{Status}`, karar `CycleCount.Decision.{Kind}`, karar kaynağı
  `CycleCount.Decision.Source.{Source}`. Önekler ayrıdır; servis sabitleri `CycleCountTextKeys`'tedir (DL-1'de sabit), her önekin Web
  kaynak ailesi orada adlıdır ve DL-4 / DL-5 parite bekçisi bu eşlemeyi ölçer.

### 3.2 `CycleCountLine`
```json
{
  "lineId": "uuid", "countId": "uuid", "warehouseId": "uuid", "locationId": "uuid",
  "item": { "itemId": "uuid", "skuId": "uuid", "skuLevel": "Gsku | Lsku | FinishedGood | Item" },
  "lotId": "string?", "stockStatus": "AVAILABLE", "baseUomId": "EA",
  "origin": "Snapshot | Found | Recount", "serialControlled": false,
  "expectedQuantity": "12.000000?", "countedQuantity": "decimal?",
  "status": "Open | Counted | NoVariance | AwaitingApproval | Approved | Rejected | Recounted",
  "version": 1
}
```
- **Kör sayım (Q-1 = A, paket §8.2 madde 5):** `blind = true` sayımda `expectedQuantity` `Submit`'ten önce **hiçbir** cevapta yazılmaz
  (yetkiye göre değişmez). DL-2 ekleri (eklemeli): `countedBy`, `countedAt` (sunucu yazar — gövdede gelmez), `movementsSinceSnapshot`,
  `expectedAtCount`, `varianceQuantity` (submit'te yazılır), `movementDuringCount`, `snapshotDrift` (boolean; yalnız submit'ten sonra yazılır),
  `scanRef` (opak, ≤ 200; Q-12 = A). **Gizleme (DL-2, r5 SB1/F-6):** `blind = true` sayımda `expectedQuantity`, `movementsSinceSnapshot`,
  `expectedAtCount`, `varianceQuantity` submit'ten önce hiçbir cevapta, submit'ten sonra yalnız `cycle-count.variances.read` taşıyan çağırana
  yazılır; aynı kural olay `data`'sındaki aynı adlı üyelere uygulanır (§3.3). Bayraklar ve durum gizlenmez. DL-3 ekleri: `decision`,
  `adjustment` (eklemeli, aynı gizleme kuralıyla).
- v1'de satır yalnız `stockStatus = AVAILABLE` olur (Q-8 = A).

### 3.3 `CycleCountEvent`
```json
{ "eventId": "uuid", "countId": "uuid", "lineId": "uuid?", "seq": 1, "kind": "Created", "actorUserId": "uuid?", "atUtc": "date-time",
  "data": { "key": "string" } }
```
`kind` ∈ `Created`, `Updated`, `Released`, `Cancelled`, `LineCounted`, `LineAdded`, `Submitted`, `VarianceComputed`, `Approved`, `Rejected`,
`RecountRequested`, `AdjustmentPending`, `AdjustmentPosted`, `AdjustmentRefused`, `AdjustmentIntegrityError`, `IntegrityResolved`, `Closed`
(paket §4.3). Insert-only, iş yazımıyla aynı işlemde. `Released.data` yalnız sayıları taşır (`snapshotAt`, `binCount`, `lineCount`,
`claimedBinCount`) — satır başına beklenen miktar **hiçbir sayımda** yok (F-13). `Cancelled.data.softDeleted` (`"true"` Draft iptali ve toplu
silmede), `Cancelled.data.bulk` (`"true"` yalnız toplu silmede) — ayrı `Deleted` türü yok (02/F-4). `data` değerleri dizedir.
**DL-2:** `LineCounted.data` = `countedQuantity`, `previousCountedQuantity?`, `scanRef?`, `lineVersion`, `countStatus?` (ilk girişte `Counting`);
`LineAdded.data` = `locationId`, `itemId`, `skuId`, `skuLevel`, `lotId?`, `countedQuantity`, `scanRef?`, `countStatus?` — ikisi de yalnız sayılan
miktarı taşır (F-13). `Submitted.data` = `previousStatus`, `lineCount`, `noVarianceLineCount`, `awaitingApprovalLineCount`,
`movementDuringCountLineCount`, `snapshotDriftLineCount` (yalnız sayılar; anahtar olayı budur). Satır başına bir `VarianceComputed` (`lineId`
dolu): `expectedQuantity`, `movementsSinceSnapshot`, `expectedAtCount`, `varianceQuantity`, `countedQuantity`, `movementDuringCount`,
`snapshotDrift`, `status` — kör sayımda ilk dört üye §3.2 gizleme kuralıyla okuma izdüşümünde yazılmaz (saklanan olay değişmez).
Bütün satırlar son durumdaysa (`NoVariance`) aynı işlemde `Closed` (`reason: AllLinesFinal`, `releasedBinClaims`).

### 3.4 Sayfalar
`{ items, total, page, pageSize, contractVersion }` (WMS-BUNDLE §3.8 emsali), `pageSize ≤ 200`.

### 3.5 `BulkDeleteCycleCountsResult`
```json
{ "results": [ { "countId": "uuid", "outcome": "Deleted | Conflict | NotFound | InvalidState", "version": 2 } ], "contractVersion": "v1" }
```
Satır başına sonuç: `Deleted` (Draft soft delete; `version` = yazılan sürüm), `Conflict` (satırın `version`'ı bayat), `NotFound` (yok / silinmiş /
başka şirket), `InvalidState` (`Draft` değil — silinmez, AC-10). Replay (kural 5): yazılan satır `Deleted`, yazılmayan `Conflict` / `NotFound` /
`InvalidState` olarak yeniden değerlendirilir; anahtar yalnız `bulkDeleteCycleCounts` yazımlarında aranır.

## 4. Operations

### 4.1 DL-1 — bağlayıcı

| Yöntem + yol | operationId | x-permission | Gövde / parametre | Başarı | x-error-codes |
|---|---|---|---|---|---|
| `GET /cycle-counts` | `listCycleCounts` | `cycle-count.counts.read` | `status*` (tekrarlanabilir), `warehouseId?`, `q?` (numara), `page`, `pageSize` | 200 sayfa `CycleCount` (silinmişler yok; yeni önce) | INVALID_REQUEST |
| `POST /cycle-counts` | `createCycleCount` | `cycle-count.counts.create` | `{ idempotencyKey, warehouseId, countType, blind, scopeLocationIds[], scopeSkuIds[], plannedDate, assignedTo?, note? }` — `number`, `status` ve sunucu alanları **üye değildir** (400, AC-12) | 201 `CycleCount` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, UNKNOWN_LOCATION, COUNT_SCOPE_NODE_INVALID, COUNT_ASSIGNEE_INVALID, COUNT_ASSIGNEE_DIRECTORY_UNAVAILABLE, COUNT_COMPANY_CODE_INVALID, LEGAL_ENTITY_MASTER_UNAVAILABLE, COUNT_RECORD_CODE_UNAVAILABLE |
| `GET /cycle-counts/{countId}` | `getCycleCount` | `cycle-count.counts.read` | — | 200 + ETag | COUNT_NOT_FOUND |
| `POST /cycle-counts/{countId}/update` | `updateCycleCount` | `cycle-count.counts.update` | If-Match; `{ idempotencyKey, warehouseId, countType, blind, scopeLocationIds[], scopeSkuIds[], plannedDate, assignedTo?, note? }` — **Draft formunun tamamı** (yok olan isteğe bağlı üye temizlenir); yalnız `Draft` | 200 + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, COUNT_NOT_FOUND, COUNT_CONFLICT, COUNT_INVALID_STATE, UNKNOWN_LOCATION, COUNT_SCOPE_NODE_INVALID, COUNT_ASSIGNEE_INVALID, COUNT_ASSIGNEE_DIRECTORY_UNAVAILABLE |
| `POST /cycle-counts/bulk-delete` | `bulkDeleteCycleCounts` | `cycle-count.counts.bulk-delete` | `{ idempotencyKey, reason, items: [{ countId, version }] }` (1..200, tekrarsız `countId`) | 200 `BulkDeleteCycleCountsResult` | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, COUNT_REASON_REQUIRED |
| `POST /cycle-counts/{countId}/release` | `releaseCycleCount` | `cycle-count.counts.update` | If-Match; `{ idempotencyKey }` — anlık görüntü + bin talebi (paket §8.2) | 200 `CycleCount` + ETag (replay 200) | IDEMPOTENCY_KEY_REUSED, COUNT_NOT_FOUND, COUNT_CONFLICT, COUNT_INVALID_STATE, COUNT_SCOPE_EMPTY, COUNT_SCOPE_TOO_LARGE, COUNT_SCOPE_OVERLAP, COUNT_INVENTORY_UNAVAILABLE, PRODUCT_MASTER_UNAVAILABLE, PRODUCT_MASTER_DATA_INCOMPLETE |
| `POST /cycle-counts/{countId}/cancel` | `cancelCycleCount` | `cycle-count.counts.cancel` | If-Match; `{ idempotencyKey, reason }` — `Draft`'ta soft delete (`IsDeleted`, `DeletedAt`, durum `Cancelled`; sonra GET 404); `Released` \| `Counting`'de iptal durumu (silinmez, bin talepleri `open = false`) | 200 `CycleCount` + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, COUNT_REASON_REQUIRED, COUNT_NOT_FOUND, COUNT_CONFLICT, COUNT_INVALID_STATE |
| `GET /cycle-counts/{countId}/lines` | `listCycleCountLines` | `cycle-count.counts.read` | `page`, `pageSize` (sıra: bin, SKU, lot) | 200 sayfa `CycleCountLine` (kör sayımda beklensiz) | COUNT_NOT_FOUND, INVALID_REQUEST |
| `GET /cycle-counts/{countId}/events` | `listCycleCountEvents` | `cycle-count.counts.read` | `page`, `pageSize` (sıra: `seq`) | 200 sayfa `CycleCountEvent` | COUNT_NOT_FOUND, INVALID_REQUEST |

**Yaratma sırası (SB3/F-1, MOD-0178 `CreatePutawayTaskHandler` emsali):** (1) gövde şekli (400); (2) yaratanın anahtarı — aynı anahtar +
aynı gövde → 200 replay, numara **alınmaz**; farklı gövde → 400; (3) iş kuralları (depo, kapsam, atanan, şirket öneki); (4) numara
(geçici sayaç, işleme **katılmaz**); (5) tek işlem (kayıt + `Created` olayı + merkezi denetim); (6) işlem tekil index'e çarparsa önce anahtar yeniden
aranır — ikizin kaydı varsa 200 replay. Numarası alınıp yazılamayan istek numarayı **atlar**: o numara yeniden verilmez, sonraki yaratma bir
sonrakini alır. Aynı kural DL-3'ün yeniden sayımında (`requestCountRecount`) geçerlidir.

**Release (paket §8.2):** yalnız `Draft`. Tek `ReadConcern.Snapshot` işleminde: (a) kapsam düğümlerinin stoklanabilir `Active` alt ağacı
(`Full`: deponun bütün stoklanabilir düğümleri) → `scopeBinIds`; (b) bu bin'lerde `OnHand > 0`, `AVAILABLE`, `scopeSkuIds` süzgeçli bakiye
satırları → her biri bir `Snapshot` satırı (`expectedQuantity` = `OnHand`); (c) bu bin'lere dokunan ve `CreatedAt ≥ snapshotAt − W` olan
defter hareketlerinin kimlikleri → `SnapshotTransactionIds` (iç); (d) satırlar, **her** kapsam bin'i için bir bin talebi (stoksuz bin dahil) ve
`Released` olayı. Bin yok → 422 `COUNT_SCOPE_EMPTY`; bin ya da satır sayısı > `MaxLinesPerCount` → 422 `COUNT_SCOPE_TOO_LARGE`; bin başka açık
sayımda ya da eşzamanlı release kaybedildi → 409 `COUNT_SCOPE_OVERLAP` (kaybedende talep / satır / olay yok); okuma açılamaz → 503
`COUNT_INVENTORY_UNAVAILABLE`. Defter hareketi hiçbir koşulda engellenmez (lokasyon kilidi yok, Q-6 = B).

### 4.2 DL-2 — bağlayıcı

| Yöntem + yol | operationId | x-permission | Gövde / parametre | Başarı | x-error-codes |
|---|---|---|---|---|---|
| `POST /cycle-counts/{countId}/lines/{lineId}/count` | `recordCycleCountLine` | `cycle-count.counts.record` | If-Match = **satırın** `version`'ı; `{ idempotencyKey, countedQuantity, scanRef? }` — `countedQuantity` decimal string ≥ 0, ≤ 6 ondalık (seri kontrollüde tam sayı); `countedBy` / `countedAt` sunucudan. Sayım `Released` \| `Counting`, satır `Open` \| `Counted` (submit'e kadar yeniden girilebilir); ilk giriş sayımı `Counting`'e alır | 200 `CycleCountLine` + ETag (satırın; replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, COUNT_QUANTITY_INVALID, COUNT_NOT_FOUND, COUNT_LINE_NOT_FOUND, COUNT_CONFLICT, COUNT_INVALID_STATE |
| `POST /cycle-counts/{countId}/lines` | `addCycleCountLine` | `cycle-count.counts.record` | `{ idempotencyKey, locationId, item: { itemId, skuId, skuLevel }, lotId?, countedQuantity, scanRef? }` — statü üyesi yok (v1 `AVAILABLE`, Q-8 = A); `If-Match` yok (yeni satır). Bin `scopeBinIds`'te, `scopeSkuIds` doluysa SKU onda (aksi 422 `COUNT_LINE_OUT_OF_SCOPE`); lot kontrollüde lot zorunlu (400 `COUNT_LOT_REQUIRED`) ve MOD-0174'te bu SKU için kayıtlı (422 `COUNT_LOT_UNKNOWN`); lot kontrollü değilse lot 422 `LOT_NOT_ALLOWED`; anahtar sayımda varsa 409 `COUNT_LINE_DUPLICATE` | 201 `CycleCountLine` (`origin = Found`, beklenen 0, `Counted`) + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, COUNT_QUANTITY_INVALID, COUNT_NOT_FOUND, COUNT_INVALID_STATE, COUNT_LINE_OUT_OF_SCOPE, COUNT_LOT_REQUIRED, COUNT_LOT_UNKNOWN, LOT_NOT_ALLOWED, COUNT_LINE_DUPLICATE, UNKNOWN_ITEM, SKU_ITEM_MISMATCH, SKU_LEVEL_NOT_STOCKABLE, PRODUCT_MASTER_UNAVAILABLE, PRODUCT_MASTER_DATA_INCOMPLETE, COUNT_CONFLICT |
| `POST /cycle-counts/{countId}/submit` | `submitCycleCount` | `cycle-count.counts.record` | If-Match (sayımın); `{ idempotencyKey }` — sayım `Released` \| `Counting`; her `Snapshot` ve `Recount` satırı sayılmış (aksi 422, durum değişmez). Fark paket §8.4 (aşağıda) | 200 `CycleCount` (`Submitted`, ya da bütün satırlar `NoVariance` ise `Closed`) + ETag (replay 200) | IDEMPOTENCY_KEY_REUSED, COUNT_NOT_FOUND, COUNT_CONFLICT, COUNT_INVALID_STATE, COUNT_LINES_UNCOUNTED, COUNT_INVENTORY_UNAVAILABLE |

**Fark (paket §8.4):** submit tek `ReadConcern.Snapshot` işleminde satırları ve defteri okur: kapsam bin'lerine dokunan `CreatedAt ≥ snapshotAt − W`
hareketleri (zaman ölçütü `CreatedAt`, `PostingDate` değil) ve bin'lerin bugünkü `AVAILABLE` bakiyesi. Satır anahtarı (bin, SKU, seviye, lot,
statü) için: anlık görüntü sonrası = penceredeki hareketlerden `SnapshotTransactionIds`'te **olmayanlar**; `movementsSinceSnapshot` = bunlardan
`CreatedAt ≤ countedAt` olanların net etkisi (gelen +, giden −; defterin bacak kuralı); `expectedAtCount = expectedQuantity + movementsSinceSnapshot`;
`varianceQuantity = countedQuantity − expectedAtCount`; `movementDuringCount` = bunlardan en az biri anahtara dokundu; `snapshotDrift` =
`expectedQuantity + Σ(anlık görüntü sonrası hareketlerin tümü) ≠ bugünkü bakiye`. Satır `NoVariance` yalnız fark 0 **ve** iki bayrak da
`false` ise; aksi `AwaitingApproval` (tolerans DL-3'tür; DL-2'de her fark onay bekler). Defter okunamazsa 503 `COUNT_INVENTORY_UNAVAILABLE`,
sayım ve satırlar değişmez.

### 4.3 DL-3 … DL-5 — ön taslak (paket §3; her dilimde bağlayıcı olur)

| Yöntem + yol | operationId | x-permission | Dilim |
|---|---|---|---|
| `GET /cycle-counts/dashboard` | `getCycleCountDashboard` | `cycle-count.counts.read` | DL-4 (KPI'lar, AC-40) |
| `GET /count-variances` (`?source=count\|manual\|all`) | `listCountVariances` | `cycle-count.variances.read` | DL-3 |
| `GET /count-variances/{lineId}` | `getCountVariance` | `cycle-count.variances.read` | DL-3 |
| `POST /count-variances/{lineId}/approve` | `approveCountVariance` | `cycle-count.variances.approve` | DL-3 |
| `POST /count-variances/{lineId}/reject` | `rejectCountVariance` | `cycle-count.variances.reject` | DL-3 |
| `POST /cycle-counts/{countId}/recount` | `requestCountRecount` | `cycle-count.variances.recount` | DL-3 |
| `POST /count-variances/{lineId}/retry` | `retryCountAdjustment` | `cycle-count.variances.post` | DL-3 |
| `POST /count-variances/{lineId}/resolve-integrity` | `resolveCountAdjustmentIntegrity` | `cycle-count.variances.resolve` | DL-3 |
| `GET /count-settings` | `getCycleCountSettings` | `cycle-count.settings.read` | DL-3 |
| `PUT /count-settings` | `updateCycleCountSettings` | `cycle-count.settings.configure` | DL-3 (tek PUT istisnası) |
| `POST /cycle-counts/{countId}/evidence-exports` | `createCountEvidenceExport` | `cycle-count.evidence.export` | DL-5 |
| `GET /count-evidence-exports` | `listCountEvidenceExports` | `cycle-count.evidence.read` | DL-5 |
| `GET /count-evidence-exports/{exportId}/file` | `getCountEvidenceExportFile` | `cycle-count.evidence.read` | DL-5 |

## 5. Error codes

### 5.1 WMS-COUNT-BUNDLE'ın kendi kodları (paket §8.14 "öneri" listesi burada **kesinleşir**)

| Kod | HTTP | Ne zaman | Dilim |
|---|---|---|---|
| `COUNT_NOT_FOUND` | 404 | sayım yok, silinmiş, başka kiracı / şirket | DL-1 |
| `COUNT_LINE_NOT_FOUND` | 404 | satır yok ya da başka sayımın | DL-2 |
| `COUNT_CONFLICT` | 409 | `If-Match` bayat | DL-1 |
| `COUNT_INVALID_STATE` | 409 | durumda izinli olmayan komut (ör. `Draft` olmayan sayımda `update` / `release`, `Submitted` ve sonrasında `cancel`) | DL-1 |
| `COUNT_SCOPE_EMPTY` | 422 | release'te kapsamda stoklanabilir `Active` düğüm yok | DL-1 |
| `COUNT_SCOPE_TOO_LARGE` | 422 | bin ya da satır sayısı `MaxLinesPerCount`'u aşar | DL-1 |
| `COUNT_SCOPE_OVERLAP` | 409 | bin başka açık sayımda ya da eşzamanlı release kaybedildi | DL-1 |
| `COUNT_SCOPE_NODE_INVALID` | 422 | kapsam düğümü `StorageLocation` \| `Zone` \| `Bin` değil, `Active` değil ya da `warehouseId`'nin yolunda değil (`details.locationId`) — **ek** (paket §12 "tip … yolunda" kuralının kodu; §8.14'te adı yoktu) | DL-1 |
| `COUNT_ASSIGNEE_INVALID` | 422 | `assignedTo` kiracının etkin kullanıcısı değil — **ek** (paket §12 "kullanıcı dizininde, şirkette etkin"; MOD-0178 `WMS_ASSIGNEE_INVALID` emsali) | DL-1 |
| `COUNT_ASSIGNEE_DIRECTORY_UNAVAILABLE` | 503 | kullanıcı dizini cevap vermedi; yazım yok — **ek** (MOD-0178 Q-13 A emsali) | DL-1 |
| `COUNT_SNAPSHOT_DRIFT` | 409 | `SnapshotDrift` satırda onay | DL-2 / DL-3 |
| `COUNT_LINES_UNCOUNTED` | 422 | submit'te sayılmamış `Snapshot` / `Recount` satırı | DL-2 |
| `COUNT_QUANTITY_INVALID` | 400 | negatif, > 6 ondalık, sayı değil | DL-2 |
| `COUNT_SELF_APPROVAL_FORBIDDEN` | 403 | dört göz (Q-5 = A) | DL-3 |
| `COUNT_LOT_UNKNOWN` | 422 | `Found` satırda kayıtsız lot | DL-2 |
| `COUNT_LOT_REQUIRED` | 400 | lot kontrollü SKU'da lotsuz bulunan satır | DL-2 |
| `COUNT_SERIAL_NOT_POSTABLE` | 422 | seri kontrollü satırda onay | DL-3 |
| `COUNT_REASON_REQUIRED` | 400 | ret / iptal / toplu silme / `resolve-integrity` / yeniden sayım gerekçesiz (1..500) | DL-1 |
| `COUNT_RECOUNT_LIMIT` | 409 | zincir `MaxRecounts`'u aşar | DL-3 |
| `COUNT_COMPANY_CODE_INVALID` | 422 | MDM şirket `Code`'u `^[A-Z]{2,5}$` değil | DL-1 |
| `COUNT_RECORD_CODE_UNAVAILABLE` | 503 | sayaç yazılamadı / tür reddedildi / kayıt saklanamadı; sayım yaratılmaz | DL-1 |
| `COUNT_INVENTORY_UNAVAILABLE` | 503 | defter okuması açılamadı / posting sonucu bilinmiyor | DL-1 |
| `COUNT_ADJUSTMENT_INTEGRITY` | 409 | anahtar başka içerikle / tutucu ters kaydedilmiş | DL-3 |
| `COUNT_LINE_OUT_OF_SCOPE` | 422 | bulunan satır kapsam dışı / `RecountExcludedKeys`'te | DL-2 |
| `COUNT_LINE_DUPLICATE` | 409 | bulunan satırın anahtarı sayımda zaten var | DL-2 |

### 5.2 Yeniden tanımlanmayan, kullanılan kodlar
`INVALID_REQUEST`, `IDEMPOTENCY_KEY_REUSED`, `UNKNOWN_LOCATION` (depo yok / `Warehouse` değil / `Active` değil / başka şirketin),
`LEGAL_ENTITY_MASTER_UNAVAILABLE`, `PRODUCT_MASTER_UNAVAILABLE`, `PRODUCT_MASTER_DATA_INCOMPLETE`, `CROSS_LE_FORBIDDEN` (servis geneli);
DL-2'de bulunan satır için `UNKNOWN_ITEM`, `SKU_ITEM_MISMATCH`, `SKU_LEVEL_NOT_STOCKABLE`, `LOT_NOT_ALLOWED` —
`InventoryErrorCodes`'tan; DL-3'te defter retleri (`INSUFFICIENT_STOCK`, `LOT_BLOCKED`, `LOT_EXPIRED`, `SERIAL_COUNT_MISMATCH`, …) aynen.

## 6. State machine (paket §8.3)

| Durum | Geçiş | Operasyon |
|---|---|---|
| `Draft` | → `Released` | `releaseCycleCount` |
| `Draft` | → `Cancelled` + soft delete | `cancelCycleCount`, `bulkDeleteCycleCounts` (gerekçe zorunlu) |
| `Released` | → `Counting` | ilk `recordCycleCountLine` / `addCycleCountLine` (DL-2) |
| `Released` \| `Counting` | → `Submitted` | `submitCycleCount` (DL-2) |
| `Released` \| `Counting` | → `Closed` (submit'te, bütün satırlar `NoVariance`; bin talepleri `open = false`) | `submitCycleCount` (DL-2) |
| `Released` \| `Counting` | → `Cancelled` (silinmez; bin talepleri `open = false`) | `cancelCycleCount` (gerekçe zorunlu; posting yokken) |
| `Submitted` | → `Closed` | sistem (DL-3) |
| `Closed` \| `Cancelled` | son durum | her yazma 409 `COUNT_INVALID_STATE` |

## 7. Bin talebi (paket §4.7)
`wms_count_bin_claims`: `(TenantId, LegalEntityId, BinId)` üzerinde `Open = true` kısmi **tekil** index — bir bin aynı anda en çok bir açık sayımda.
Release her kapsam bin'i için (stoksuz dahil) talep yazar; `Closed` / `Cancelled` kendi taleplerini aynı işlemde `Open = false` yapar (soft); DL-3
yeniden sayımı seçili satırların bin'lerindeki talepleri devreder. Talep stok kaydı değildir, hareketi engellemez.

## 8. Merkezi denetim (paket §8.11, §21)
Her yazma komutu BL-501 kaydı bildirir: kategori `MasterData`, kaynak modül `CYCLE-COUNTING`, işlem Platform enum adı
(`Create`, `Update`, `Delete`, `LifecycleTransition`, `Execute`, `Export`). Kayıt ad ve gövde taşımaz; değerler yalnız `wms_count_events.Data`'da.
Başarılı ve yazan komut 1 kayıt; replay / 0 kayıt 0; başarısız 0.

## 9. Açık noktalar (paketle çelişki değil, dondurmaya öneri)
- §5.1'deki üç **ek** kod (`COUNT_SCOPE_NODE_INVALID`, `COUNT_ASSIGNEE_INVALID`, `COUNT_ASSIGNEE_DIRECTORY_UNAVAILABLE`) paket §8.14 listesinde
  adsızdı; paket §12'nin kurallarına kod verirler. Paket revizyonunda §8.14'e eklenmesi önerilir (CT).
- `CycleCount.scopeBinIds` kablo üyesidir (sayım formu ve dashboard bin listesini buradan alır); `snapshotTransactionIds` değildir.

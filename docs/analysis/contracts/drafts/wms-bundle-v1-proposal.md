# WMS-BUNDLE v1 — proposal (MOD-0178 Putaway / Picking / Packing)

> **Durum: TASLAK (proposal) — sözleşme değil.** `docs/analysis/contracts/` altında WMS-BUNDLE yok; Blueprint 8.8 MOD-0178 satırı
> sözleşmeyi "WMS-BUNDLE (warehouse task schema, scan events, exception model, OTel)" diye adlandırır. Bugün donmuş olan tek
> MOD-0178 sözleşmesi **WAREHOUSE-OUTBOUND 1.0.0**'dır (`warehouse-outbound.openapi.yaml`, `x-status: FROZEN`, `x-owner: MOD-0178`,
> `x-front-loaded-for: MOD-0183`). CT kabulünden sonra S1 dilimi bunu tek yazıcı adımıyla
> `docs/analysis/contracts/wms-bundle.openapi.yaml` (`info.version: 1.0.0`, `x-status: FROZEN`, `x-owner: MOD-0178`,
> `x-owner-service: Diten.InventoryService`) olarak yazar ve README satırını ekler. Donduktan sonra yalnız eklemeli değişiklik (K16).
>
> **Kaynak:** pack `execution/domains/supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md` (davranış otoritesi;
> bu dosya kablo şeklidir). Sahip kararları W-1…W-5, W-10, E-1, E-2, E-6, paket soruları Q-01…Q-09, Q-11…Q-13, Q-MDM-1, Q-MDM-2 ve mockup kararları M-1…M-6
> (Beste, 2026-10-21; pack §19.1.1, §19.1.2) ve bağımsız inceleme 01 düzeltmeleri F-1…F-16 (pack §19.1.3). **E-imza yok** (M-1): bu sözleşmede imzalanabilir eylem ve imza alanı tanımlanmaz.
> Açık sorular yalnız diğer ekiplerde + Q-10 (pack §19.5).
>
> **Superset kuralı:** WMS-BUNDLE, WAREHOUSE-OUTBOUND 1.0.0'ın **yollarını ve şemalarını aynen** içerir (§2) — tek bir yol, parametre,
> özellik, tip, enum değeri, zorunlu liste ya da durum kodu değişmez. **Dosya stratejisi — karar Q-06 (Beste 2026-10-21):** WMS-BUNDLE
> **ayrı dosya** (`wms-bundle.openapi.yaml`); WAREHOUSE-OUTBOUND 1.0.0 dosyası **değişmez** (MVP-6'nın tükettiği alt küme); iki dosyanın
> outbound kısmının eşit olduğunu bir sözleşme testi ölçer. `WMS_*` hata kodları yalnız WMS-BUNDLE'da tanımlıdır (§5).
>
> **Tüketilen donmuş sözleşmeler (değişmez):** INVENTORY-BUNDLE 1.5.0, LOCATION 1.1.0, TRACE 1.4.0, QUARANTINE 1.3.0, FEFO-BUNDLE
> 1.3.0, RECALL-BUNDLE 1.9.0, PRODUCT-MASTER (MDM, INVENTORY üzerinden), SHIPMENT-BUNDLE 3.3.0 (MVP-6, yalnız okuma). Başka sahiplerden
> gereken eklemeli değişiklikler §13'tedir ve bu sözleşmenin parçası **değildir**.

## 1. Rules

1. Sunucu `/api/warehouse` (Gateway → `Diten.InventoryService` **5067**, yeni route INT-WMS, integration-agent). Kiracı kabuğu
   kullanıcıları (`actor_type = tenant_user`).
2. `TenantId` token'dan. Şirket W-0173-02 (claim, yoksa `X-Legal-Entity-Id` + MDM doğrulaması). 403 `CROSS_LE_FORBIDDEN` yalnız
   başlık ↔ token / üyelik uyuşmazlığıdır; seçili şirketle başka şirketin kaydına id ile erişim → **404**; başka kiracı / bilinmeyen
   id → **404** (F-11). Her okuma ve yazma kiracı + şirket kapsamlı. `tenantId` /
   `legalEntityId` gövde ya da sorguda → 400.
3. Zarf: InventoryService'in ölçülmüş kablosu — başarı gövdesi aşağıdaki şemanın kendisi (sarmalanmamış; INVENTORY / FEFO / RECALL
   R-1 emsali); hata `Error` = `{ error: { code, message, details?, correlationId }, contractVersion: "v1" }` (INVENTORY `Error`).
   WAREHOUSE-OUTBOUND `Error`'u (`details` yok) bunun alt kümesidir; outbound uçları `details` göndermez.
4. **Boş isteğe bağlı üye yazılmaz** (JSON `null` yok): OAS 3.1'de `nullable` anahtar sözcüğü JSON null'a izin vermez ve MVP-6
   okuyucusu null'ı reddeder (`FrozenReadSchema.cs:8`, GAP-0183-03). Liste sonunda `nextCursor` üyesi hiç yazılmaz.
5. Her yazma `Idempotency-Key` başlığı alır, gövde `idempotencyKey` ile eşit (8–100). Aynı anahtar + aynı gövde → ilk sonuç (200);
   farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`. **Bütün yazma operasyonlarında** (pack §8.1; bağımsız inceleme 10 F-1): anahtar, isteği
   yapan kullanıcı ve hedef kayıt kapsamında saklanır (yaratma: kayıtta; diğerleri: komutun ilk yazımının olayında) ve **`If-Match`'ten ve
   bütün iş kurallarından önce** aranır. Replay = 200 + kaydın güncel hâli (onayda sonuç `confirmations[seq].status`; ilk cevabın 422 /
   409 / 503 kodu tekrarlanmaz), yazım yok. Parmak izi = operasyon + hedef (`taskId` / `seq` / `roleId`) + normalleşmiş gövde; başlıklar
   (`Idempotency-Key`, `If-Match`) girmez. Eşzamanlı iki özdeş istekten kaybeden 409 değil 200 replay alır. Toplu operasyonların replay'i
   yazılan satırı yazılmış sonucuyla, yazılmayanı `Conflict` (bulunamayanı `NotFound`) döner. Hiç yazmadan reddedilen istek anahtar bırakmaz.
   İstemci her kullanıcı eyleminde yeni anahtar üretir.
6. Değişebilir kayıtlar (görev, lokasyon rolü; S2 outbound, koli) değişiklikte `If-Match: W/"<version>"` ister; eksik → 400
   `INVALID_REQUEST`, bayat → 409 `WMS_CONFLICT`. Cevap `ETag: W/"<version>"`. Bayatlık kural 5'in anahtar kontrolünden sonra
   değerlendirilir: işlenmiş isteğin ağ tekrarı (aynı `If-Match`) 409 değil 200 replay alır.
7. **Bu sözleşme stok bakiyesi tutmaz ve stok değiştirmez.** Her fiziksel yer değişikliği INVENTORY `postMovement` ile aynı süreç içi
   yoldan `LOCATION_TRANSFER` olarak yazılır (`sourceModule: MOD-0178`). `GOODS_ISSUE_SALES` bu modülün değildir (W-4, MOD-0183).
8. Correlation (E-1): `X-Correlation-Id` Gateway'de üretilir, servis okur, iletir; olay yüklerinde UUID (GAP-0183-01). Geçersiz kimlik
   davranışı servis genelidir (E-1 hedefi 400; Inventory'nin 400 WP'sine kadar servis davranışı — pack Q-10).
9. Tarihler (E-6): `date` = UTC takvim günü, `date-time` = UTC an.
10. Miktarlar: decimal string (`^-?\d+(\.\d+)?$`, en çok 6 ondalık), SKU'nun **baz biriminde** (`baseUomId`); farklı SKU / birim
    toplanmaz.
11. Yeni stok durumu ya da hareket tipi tanımlanmaz.

## 2. WAREHOUSE-OUTBOUND 1.0.0 — aynen kapsanan kısım (FROZEN)

| Yol | operationId | Not |
|---|---|---|
| `GET /outbound-shipments` (`status`, `warehouseId`, `cursor`) | `listOutboundShipments` | aynen; sayfa `{ items, nextCursor?, contractVersion }` |
| `GET /outbound-shipments/{outboundId}` | `getOutboundShipment` | aynen; 404 `UNKNOWN_OUTBOUND` |

Şemalar aynen: `Decimal`, `SkuLevel` (`Gsku | Lsku | FinishedGood` — INVENTORY'nin `Item`'ı burada **yok**), `OutboundStatus`
(`Allocated | Picked | Packed | ReadyToShip | Shipped | Cancelled`), `Address`, `OutboundLine`, `Package`, `OutboundShipment`,
`OutboundShipmentReadyEvent`, `Error`, `UnknownOutbound`.

### 2.1 Üretici kuralları (1.0.0'ı değiştirmeyen, uygulayıcı yükümlülükleri)
- Yalnız 6 durumdan birindeki kayıt donmuş uçlarda görünür; 0178'de "taslak" outbound yoktur (S2: outbound tahsisle birlikte açılır,
  tahsis başarısızsa açılmaz — pack AC-26).
- `ReadyToShip`'ten sonra detayın tek değişen üyesi `status`'tur (`Shipped` / `Cancelled`): MOD-0183 anlık görüntü hash'iyle drift
  arar (MOD-0183 pack §8.2).
- Outbound satırında `skuLevel: Item` yazılmaz (enum'da yok; MVP-6 `SkuLevels` 3 değer — pack Q-M6-6).
- `itemId` / `skuId` UUID "D" biçimi; tarih `…Z`.
- S1'de liste boş (`{ "items": [], "contractVersion": "v1" }`), detay 404.

### 2.2 Durum anlamı (0178 tarafı, W-4)
`Allocated` = tüm satırlar lot rezervasyonu aldı ve pick görevleri açıldı · `Picked` = tüm pick görevleri `Completed` (stok staging'de,
`AVAILABLE`, rezerve) · `Packed` = tüm satır miktarı kolilere dağıtıldı · `ReadyToShip` = sevke hazır (+ `OutboundShipmentReady`) ·
`Shipped` = MVP-6 sevkiyatı `Dispatched` (0178 `GOODS_ISSUE_SALES` yazmaz; MOD-0183 yazar) · `Cancelled` = iptal (rezervasyonlar
bırakılır).

### 2.3 Eklemeli öneri (S2 — v1.1.0; MVP-6 onayı gerekir, pack Q-M6-1…9)
Hepsi **isteğe bağlı**, yoksa yazılmaz:

| Şema | Yeni üye | Neden |
|---|---|---|
| `OutboundShipment` | `outboundNumber` (RCS `{ŞİRKET}-OUT-000001`) | `outboundId` sunucu UUID'si kalır, görünen kod ayrı (ASSUMPTION-0178-06) |
| `OutboundShipment` | `legalEntityId` (uuid) | MVP-6 güvenilir LE bağı (GAP-0183-02) |
| `OutboundShipment` | `shipment { shipmentId, shipmentNumber, dispatchedAt }` | `Shipped`'in kaynağı |
| `OutboundLine` | `lotId` | `GOODS_ISSUE_SALES` `lotId` ister; 1.0.0 yalnız `lotNumber` taşır |
| `OutboundLine` | `fromWarehouseId`, `fromLocationId` (staging), `stockStatus` | `GOODS_ISSUE_SALES` kaynağı |
| `OutboundLine` | `reservationId` | MVP-6 çıkışının 0178 rezervasyonunu tüketmesi (SHIPMENT satırı `inventoryReferenceId`) — W-5'e bağlı |
| `OutboundLine` | `fefoBypassReason` (10–1000) | FEFO sapması MVP-6 çıkışına taşınır (INVENTORY 1.3.0 `fefoBypassReason`) |
| `OutboundShipmentReadyEvent` | `legalEntityId`, `tenantId`, `outboundNumber`; `correlationId` UUID | GAP-0183-01 / 02 |
| `Package` | `contents [{ orderLineId?, lotId?, quantity }]` | paket içeriği (MVP-6 isterse) |

Bir satır birden çok lot / staging lokasyonuyla toplanırsa: 1.0.0 `OutboundLine` tek lot taşır → **satır lot başına bölünür**
(aynı `orderLineId`, ayrı satırlar). MVP-6'nın buna dayanıklılığı sorulur (Q-M6-1).

## 3. Schemas — S1 (warehouse task schema)

### 3.1 `WarehouseTask`
```json
{
  "taskId": "uuid",
  "number": "GMG-PUT-000123",
  "companyCodePrefix": "GMG",
  "warehouseCode": "WH-01",
  "kind": "Putaway",
  "status": "Open | InProgress | Completed | Cancelled",
  "origin": "Manual | Receipt | Production | Cancellation | External",
  "sourceRef": { "sourceModule": "MOD-0142", "sourceType": "GOODS_RECEIPT", "sourceDocumentId": "…", "sourceLineId": "…", "transactionId": "…" },
  "sourceSystem": "…", "externalRef": "…",
  "legalEntityId": "uuid",
  "warehouseId": "string",
  "item": { "itemId": "uuid", "skuId": "uuid", "skuLevel": "Gsku | Lsku | FinishedGood | Item" },
  "lotId": "string", "lotNumber": "string",
  "stockStatus": "AVAILABLE",
  "fromLocationId": "string",
  "toLocationId": "string",
  "suggestedToLocationId": "string",
  "targetSource": "Manual | SuggestionApplied",
  "plannedQuantity": "60.000", "confirmedQuantity": "40.000", "pendingQuantity": "0.000", "remainingQuantity": "20.000",
  "baseUomId": "EA",
  "assignee": { "userId": "uuid", "name": "string" },
  "priority": "Low | Normal | High",
  "note": "string",
  "serialControlled": false,
  "confirmations": [ TaskConfirmation ],
  "integrationFlag": "None | PostingPending | PostingRefused | PostingIntegrityError",
  "allowedActions": ["Confirm", "Claim", "Update", "Cancel", "CloseShort", "Retry", "MarkReturned", "ResolveIntegrity"],
  "disabledActions": [ { "action": "Cancel", "reasonCode": "HasPostedConfirmations" } ],
  "shortCloseReason": "string", "cancelReason": "string",
  "created": { "userId": "uuid", "name": "string", "atUtc": "date-time" },
  "updated": { "userId": "uuid", "name": "string", "atUtc": "date-time" },
  "version": 3,
  "contractVersion": "v1"
}
```
`kind: Pick` S2'de aynı şemayı kullanır (+ `outboundId`, `outboundLineRef`, `reservationId`, `fefoRank`, `fefoBypassReason`).
`item` şekli FEFO / RECALL R-12 emsalidir (`{ itemId, skuId, skuLevel }`). İsim üyeleri (`name`) RECALL 1.8.0 kuralıdır: eylemi yapan
kişinin o andaki adı; yoksa üye yazılmaz, istemci "bilinmeyen kullanıcı" gösterir.

### 3.2 `TaskConfirmation`
```json
{
  "seq": 1,
  "quantity": "40.000",
  "toLocationId": "string",
  "deviationReason": "string",
  "status": "Pending | Posted | Refused | IntegrityError",
  "postingKey": "wms:put:{taskId}:1",
  "transactionDate": "date-time", "postingDate": "date-time",
  "serialIds": ["string"],
  "transactionId": "string", "transactionNumber": "INV-2026-0000123",
  "error": { "code": "INSUFFICIENT_STOCK", "message": "…", "details": { }, "status": 422 },
  "resolution": "SupersededByNewConfirmation | ReturnedToSource",
  "priorResolution": "SupersededByNewConfirmation | ReturnedToSource",
  "resolutionReason": "string",
  "channel": "Ui | Api | External",
  "scanRef": "string",
  "confirmedBy": { "userId": "uuid", "name": "string" }, "confirmedAtUtc": "date-time",
  "attempts": 1, "lastAttemptAtUtc": "date-time",
  "refusalSource": "PreCheck | Ledger4xx | Resolve", "refusedOnRetry": true, "refusedAtUtc": "date-time",
  "inFlightUntilUtc": "date-time",
  "integrityResolution": "ResolvedOwnMovement | ReturnedToRefused", "resolveReason": "string",
  "holderMovement": { "transactionId": "string", "transactionNumber": "string", "sourceModule": "MOD-0178", "sourceType": "PUTAWAY_TASK", "sourceDocumentId": "string", "sourceLineId": "1", "quantity": "40.000", "uomId": "EA", "baseQuantity": "40.000", "fromWarehouseId": "string", "fromLocationId": "string", "toWarehouseId": "string", "toLocationId": "string", "postingDate": "date-time", "lotId": "string", "serialIds": ["string"], "fromStockStatus": "AVAILABLE", "toStockStatus": "AVAILABLE", "reversedByTransactionId": "string" }
}
```

- `transactionDate` / `postingDate` işlem 1'de bir kez yazılır, her denemede aynen gönderilir (ledger idempotency izi tarihleri kapsar).
- `serialIds` yalnız seri kontrollü SKU'da: kaynak kovanın bütün serileri sistemce okunur (istemci göndermez); adet = miktar (F-1).
- INVENTORY cevabı: 2xx → `Posted`; **400 `IDEMPOTENCY_KEY_REUSED` → `IntegrityError`** (bütünlük hatası; inceleme 02 F-1); 422 ve
  diğer 4xx (400 / 403 / 404 / 409) → kalıcı `Refused` (`error.status` saklanır); yalnız 503 / zaman aşımı / ağ hatası → `Pending` (F-2).
  `resolution` yalnız `Refused` onayda (F-4); `IntegrityError`'da resolution kapalı (`priorResolution`'a taşınır; tek koşullu güncelleme,
  `version` CAS — bağımsız inceleme 06 F-3). `refusalSource` / `refusedOnRetry` / `refusedAtUtc` savunma dalının seçim ölçütüdür
  (yalnız `PreCheck` / `Ledger4xx` + `refusedOnRetry = true`, son 30 dk; bağımsız inceleme 06 F-2).
- `serialIds` = seri **numarası** (ledger / TRACE `NormalizeSerial` ile normalleştirir); seri okuma açık tutma / bloke serilerini dışlar
  (MOD-0175 emsali); aynı lokasyonda aynı lot + SKU'nun AVAILABLE dışı stoğu varsa seri görevi açılmaz (inceleme 02 F-2, F-8).

### 3.3 `PutawayCandidate` (okuma — saklanmaz, INVENTORY bakiyesinden hesaplanır)
```json
{
  "candidateKey": "{warehouseId}|{locationId}|{skuId}|{skuLevel}|{lotId}|{stockStatus}",
  "warehouseId": "string", "locationId": "string", "locationCode": "R-01",
  "item": { "itemId": "uuid", "skuId": "uuid", "skuLevel": "Lsku" },
  "lotId": "string", "lotNumber": "string", "lotStatus": "Draft | Released | Blocked | Expired",
  "stockStatus": "AVAILABLE",
  "onHand": "100.000", "openTaskQuantity": "60.000", "openQuantity": "40.000", "baseUomId": "EA",
  "eligible": true,
  "notEligibleReason": "StockHeld | LotBlocked | LotExpired | LotUnderRecall",
  "blockingDocument": { "kind": "QuarantineHold | BlockedStock | RecallCase | LotStatus", "id": "string", "number": "string" },
  "storageConditions": ["COOL_2_8"],
  "lastPostingDate": "date-time"
}
```
`openQuantity = onHand − Σ (açık görevlerin planned − confirmed)`; okuma anında. Uygun olmayan satırlar listede görünür
(`eligible: false` + neden + engelleyen belge numarası — M-4), görev açılmaz; satır gizlenmez. `storageConditions` ürünün
PRODUCT-MASTER v1.1 `getSku.storageConditions` kopyasıdır (yalnız gösterim; kural §3.6).

### 3.4 `WarehouseTaskEvent`
```json
{ "seq": 4, "kind": "Created | Updated | Assigned | Claimed | SuggestionApplied | ConfirmationRecorded | ConfirmationPosted | ConfirmationRefused | ConfirmationRetried | ConfirmationReconciled | ConfirmationIntegrityError | IntegrityResolved | ConfirmationDeviation | ConfirmationSuperseded | ConfirmationReturned | ClosedShort | Completed | Cancelled",
  "actor": { "userId": "uuid", "name": "string" }, "system": false,
  "atUtc": "date-time", "data": { }, "correlationId": "uuid" }
```
Olayın istek anahtarı ve parmak izi (pack §4.3 "anahtar olayı"; bağımsız inceleme 10 F-1) depoda tutulur, bu şemaya **yazılmaz**.

### 3.5 `WarehouseLocationRole`
```json
{ "roleId": "uuid", "legalEntityId": "uuid", "warehouseId": "string", "locationId": "string", "locationCode": "COLD-A", "locationType": "StorageLocation | Zone | Bin",
  "roles": ["Receiving"], "acceptedStorageConditions": ["COOL_2_8", "PROTECT_FROM_LIGHT"],
  "note": "string", "version": 1, "contractVersion": "v1" }
```
Düğüm başına **tek kayıt** (pack §4.4 tasarım gerekçesi): `roles` 0..n (`Receiving` S1, `Staging` S2; Receiving + Staging birlikte
olamaz), `acceptedStorageConditions` 0..n — PRODUCT-MASTER v1.1 `StorageConditionCode` kodları (`AMBIENT_15_25, COOL_2_8,
FROZEN_BELOW_MINUS_20, PROTECT_FROM_LIGHT, PROTECT_FROM_MOISTURE`; `product-master-bundle.openapi.yaml:601-604`), en çok bir sıcaklık
kodu. İkisinden en az biri dolu. Kayıt düğümün stoklanabilir alt ağacına uygulanır; LOCATION'da düğüm yaratılmaz, LOCATION
sözleşmesi değişmez (karar Q-04).

### 3.6 Saklama koşulu uyumu (M-3, S1 kuralı)
- Etkin kabul kümesi = `getLocationPath` üzerinde hedef bine en yakın (kendisi dahil) ve `acceptedStorageConditions`'ı dolu kaydın kümesi.
- Uyum: ürün `storageConditions` ⊆ etkin küme. Etkin küme yoksa (etiketsiz): ürün kümesi ⊆ `{AMBIENT_15_25}` (koşulsuz ya da yalnız
  AMBIENT ürün).
- Ürün alanı yoksa (bilinmiyor) → 422 `WMS_STORAGE_CONDITIONS_UNKNOWN`; uyumsuz → 422 `WMS_STORAGE_CONDITION_MISMATCH`
  (`details: productConditions, locationConditions, inheritedFromLocationId`). Görev oluşturma, düzenleme (hedef değişince) ve onayda
  uygulanır.
- Okuma yolu: InventoryService bugün `storageConditions` okumuyor; MOD-0173 `SkuReference` / `ProductMasterHttpClient` seam'i ile
  eklenir (pack §5). Kabul: dev PRODUCT-MASTER köprüsü 4010 (veri seti genişletilerek) E3 / E4; canlı PM-LIVE sonrası (F-5).
- Kapsam: yalnız putaway hedefi. Mal kabul alanının saklama koşulu uyumu MVP-2 GRN'in işidir (pack Q-M2-3).
- Rolsüz kayıt (yalnız `acceptedStorageConditions`) geçerlidir; "Depolama" rolü yoktur. Okumalarda kalıtılan küme
  `effectiveStorageConditions { codes[], inheritedFromLocationId?, inheritedFromLocationCode? }` olarak döner (ekranda "üst düğümden").

### 3.7 Görev numarası (M-2)
`number = {companyCodePrefix}-PUT-{000001}`; `companyCodePrefix` = görev açılırken okunan MDM tüzel kişilik `Code`'u, `^[A-Z]{2,5}$`
değilse görev açılmaz (422 `WMS_COMPANY_CODE_INVALID`, `details.companyCode`). Önek bir kez yazılır, değişmez; sayaç (kiracı, önek, tür)
başına; numara **kiracı genelinde tekil**; `999999`'dan sonra 7 hane; depo kodu numarada yok (`warehouseCode` ayrı alan). Şirket kodu geçersizken yalnız `createPutawayTask` reddedilir; mevcut
görevler okunur ve onaylanır.

### 3.8 Liste sayfaları
`{ items, total, page, pageSize, contractVersion }` (FEFO / RECALL R-6 emsali), `pageSize ≤ 200`. (Donmuş outbound listesi kendi
cursor şeklini korur, §2.)

## 4. Operations — S1

| Yöntem + yol | operationId | x-permission (öneri) | Gövde / parametre | Başarı | x-error-codes |
|---|---|---|---|---|---|
| `GET /putaway-candidates` | `listPutawayCandidates` | `warehouse.putaway-tasks.read` | `warehouseId?`, `skuId?`, `lotId?`, `eligible?`, `q?`, `page`, `pageSize` | 200 sayfa `PutawayCandidate` | INVALID_REQUEST, CROSS_LE_FORBIDDEN |
| `GET /putaway-tasks` | `listPutawayTasks` | `warehouse.putaway-tasks.read` | `status*`, `warehouseId?`, `assigneeUserId?`, `priority?`, `q?` (numara / lot), `orderBy`, `page`, `pageSize` | 200 sayfa `WarehouseTask` | INVALID_REQUEST, CROSS_LE_FORBIDDEN |
| `POST /putaway-tasks` | `createPutawayTask` | `warehouse.putaway-tasks.create` | `{ idempotencyKey, candidateKey \| (warehouseId, fromLocationId, item, lotId?, stockStatus), plannedQuantity, toLocationId?, targetSource?, assigneeUserId?, priority?, note? }` | 201 `WarehouseTask` + ETag (replay 200) | WMS_STORAGE_CONDITION_MISMATCH, WMS_STORAGE_CONDITIONS_UNKNOWN, WMS_COMPANY_CODE_INVALID, LEGAL_ENTITY_MASTER_UNAVAILABLE, PRODUCT_MASTER_UNAVAILABLE, INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WMS_SOURCE_STOCK_NOT_FOUND, WMS_SOURCE_NOT_RECEIVING, WMS_STOCK_NOT_PUTAWAY_ELIGIBLE, WMS_QUANTITY_EXCEEDS_OPEN, WMS_TARGET_NOT_ALLOWED, WMS_ASSIGNEE_INVALID, UNKNOWN_LOCATION, LOT_BLOCKED, LOT_EXPIRED, LOT_UNDER_RECALL, CROSS_LE_FORBIDDEN, WMS_RECORD_CODE_UNAVAILABLE, WMS_SERIAL_BUCKET_MIXED_STATUS, SKU_LEVEL_NOT_STOCKABLE, PRODUCT_MASTER_DATA_INCOMPLETE |
| `GET /putaway-tasks/{taskId}` | `getPutawayTask` | `warehouse.putaway-tasks.read` | — | 200 + ETag | WMS_TASK_NOT_FOUND, CROSS_LE_FORBIDDEN |
| `PUT /putaway-tasks/{taskId}` | `updatePutawayTask` | `warehouse.putaway-tasks.update` | If-Match; `{ idempotencyKey, plannedQuantity?, toLocationId?, targetSource?, assigneeUserId?, priority?, note? }` — kaynak kova alanları **kabul edilmez** (400); planned ≥ confirmed + pending | 200 + ETag (replay 200) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WMS_CONFLICT, WMS_INVALID_TRANSITION, WMS_CONFIRMATION_PENDING, WMS_QUANTITY_EXCEEDS_OPEN, WMS_QUANTITY_BELOW_CONFIRMED, WMS_SERIAL_PARTIAL_NOT_SUPPORTED, WMS_TARGET_NOT_ALLOWED, UNKNOWN_LOCATION, WMS_STORAGE_CONDITION_MISMATCH, WMS_STORAGE_CONDITIONS_UNKNOWN, LOT_UNDER_RECALL, WMS_TASK_NOT_FOUND, WMS_POSTING_INTEGRITY_ERROR |
| `POST /putaway-tasks/{taskId}/claim` | `claimPutawayTask` | `warehouse.putaway-tasks.confirm` | If-Match; `{ idempotencyKey }` — kendine ata (atanmamış görev) | 200 + ETag (replay 200) | IDEMPOTENCY_KEY_REUSED, WMS_TASK_ALREADY_ASSIGNED, WMS_CONFIRMATION_PENDING, WMS_INVALID_TRANSITION, WMS_CONFLICT, WMS_POSTING_INTEGRITY_ERROR |
| `POST /putaway-tasks/bulk-assign` | `bulkAssignPutawayTasks` | `warehouse.putaway-tasks.update` | `{ idempotencyKey, assigneeUserId, items: [{ taskId, version }] }` (≤ 100) | 200 `{ results: [{ taskId, outcome: Assigned \| Conflict \| NotFound \| InvalidTransition \| PendingConfirmation, version? }] }` — satır başına sonuç, kısmi başarı işaretli; Pending onaylı görev atanmaz (`PendingConfirmation`; bağımsız inceleme 07 F-1); replay 200: yazılan satır `Assigned`, yazılmayan `Conflict` / `NotFound` (kural 5) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED, WMS_ASSIGNEE_INVALID, WMS_CONFIRMATION_PENDING (satır sonucu) |
| `POST /putaway-tasks/{taskId}/confirmations` | `confirmPutawayTask` | `warehouse.putaway-tasks.confirm` | If-Match; `{ idempotencyKey, quantity, toLocationId, deviationReason?, channel?, scanRef? }` — seri istemciden alınmaz (F-1) | 201 `WarehouseTask` (onay Posted) · 422 onay Refused (INVENTORY kodu aynen, gövde `Error` + `details.task`) · 503 onay Pending · **replay 200** `WarehouseTask` (aynı anahtar + aynı gövde; If-Match'ten önce — kural 5) | **anahtar:** IDEMPOTENCY_KEY_REUSED (400, farklı gövde); **ön kontrol (onay kaydı açılmadan):** INVALID_REQUEST, REASON_REQUIRED, WMS_CONFLICT, WMS_NOT_ASSIGNEE (403), WMS_CONFIRMATION_PENDING, WMS_INVALID_TRANSITION, WMS_QUANTITY_EXCEEDS_REMAINING, WMS_SERIAL_PARTIAL_NOT_SUPPORTED, WMS_SERIAL_BUCKET_CHANGED, WMS_TARGET_NOT_ALLOWED, UNKNOWN_LOCATION, WMS_STORAGE_CONDITION_MISMATCH, WMS_STORAGE_CONDITIONS_UNKNOWN, WMS_STOCK_NOT_PUTAWAY_ELIGIBLE, LOT_BLOCKED, LOT_EXPIRED, LOT_UNDER_RECALL, CROSS_LE_FORBIDDEN, WMS_SERIAL_BUCKET_MIXED_STATUS, WMS_POSTING_INTEGRITY_ERROR (görev kilitli); **işlem 4 bütünlüğü:** WMS_POSTING_INTEGRITY_ERROR (409, defter `IDEMPOTENCY_KEY_REUSED` ya da 0-satır (D): bu deneme Posted aldı, onay Refused ya da zaten `IntegrityError`); **defter (Refused):** INVENTORY postMovement 4xx kodları aynen (`INSUFFICIENT_STOCK`, `LOT_RETEST_DUE`, `SERIAL_NOT_AVAILABLE`, `ITEM_NOT_ACTIVE`, …); **503 (Pending):** INVENTORY_UNAVAILABLE, PRODUCT_MASTER_UNAVAILABLE, LOCATION_MASTER_UNAVAILABLE, WMS_CONFIRMATION_CONFLICT_RETRY (0-satır (A) tükendi); **0-satır (C):** WMS_CONFIRMATION_IN_FLIGHT (409); **(E):** WMS_CONFLICT (409) |
| `POST /putaway-tasks/{taskId}/confirmations/{seq}/retry` | `retryPutawayConfirmation` | `warehouse.putaway-tasks.confirm` | `{ idempotencyKey }` (yalnız Pending; aynı `postingKey`, aynı tarihler, aynı seriler; deneme sınırı yok, `attempts` / `lastAttemptAtUtc` döner). **Sıra (inceleme 02 F-1):** (i) önce ledger'da `postingKey` aranır (süreç içi, upstream'siz) — işlenmişse ve holder ters kaydedilmemişse Posted, ön kontrol koşmaz; işlenmiş ama **ters kaydedilmişse** → `IntegrityError` (`HolderReversed`; bağımsız inceleme 05 F-2); farklı parmak izi → `IntegrityError`; (ii) yalnız işlenmemişse 0178 ön kontrolleri (recall, `AVAILABLE`, saklama koşulu, seri kovası) — tutmazsa Refused, INVENTORY çağrılmaz — sonra posting. Sonuç yazımı koşulludur (`status == Pending`, F-3). Arka plan sweeper'ı yalnız (i)'yi yapar (kullanıcı token'ı yok; inceleme 02 F-5) | 200 `WarehouseTask` (replay 200) | IDEMPOTENCY_KEY_REUSED, WMS_CONFIRMATION_IN_FLIGHT (409 — uçuş kilidi; inceleme 03 F-1), WMS_POSTING_INTEGRITY_ERROR (409), WMS_NOT_ASSIGNEE (403; atanan ya da sorumlu — bağımsız inceleme 05 F-6), WMS_CONFIRMATION_NOT_RETRYABLE, WMS_SERIAL_BUCKET_MIXED_STATUS, LOT_UNDER_RECALL, WMS_STOCK_NOT_PUTAWAY_ELIGIBLE, WMS_STORAGE_CONDITION_MISMATCH, WMS_STORAGE_CONDITIONS_UNKNOWN, WMS_SERIAL_BUCKET_CHANGED, WMS_CONFLICT ((E) dalı), + defter 4xx (Refused), 503 (Pending; WMS_CONFIRMATION_CONFLICT_RETRY dahil) |
| `POST /putaway-tasks/{taskId}/confirmations/{seq}/returned` | `markPutawayConfirmationReturned` | `warehouse.putaway-tasks.confirm` | If-Match; `{ idempotencyKey, reason }` (10–1000) — "Geri taşındı": mal kaynağa iade edildi, hareket yok (F-4) | 200 `WarehouseTask` (`resolution: ReturnedToSource`; replay 200) | IDEMPOTENCY_KEY_REUSED, REASON_REQUIRED, WMS_CONFLICT, WMS_CONFIRMATION_NOT_RESOLVABLE, WMS_NOT_ASSIGNEE, WMS_POSTING_INTEGRITY_ERROR (önce ledger araması; geç commit — bağımsız inceleme 05 F-1), WMS_CONFIRMATION_PENDING (görevde Pending onay var — bağımsız inceleme 07 F-1) |
| `POST /putaway-tasks/{taskId}/close-short` | `closePutawayTaskShort` | `warehouse.putaway-tasks.confirm` | If-Match; `{ idempotencyKey, reason }` (en az bir Posted onay; seri SKU'da yok) | 200 (replay 200) | IDEMPOTENCY_KEY_REUSED, REASON_REQUIRED, WMS_CONFLICT, WMS_INVALID_TRANSITION, WMS_CONFIRMATION_PENDING, WMS_SERIAL_PARTIAL_NOT_SUPPORTED, WMS_POSTING_INTEGRITY_ERROR (görev kilitli ya da önce ledger araması geç commit buldu), WMS_NOT_ASSIGNEE (403; atanan ya da sorumlu) |
| `POST /putaway-tasks/{taskId}/cancel` | `cancelPutawayTask` | `warehouse.putaway-tasks.cancel` | If-Match; `{ idempotencyKey, reason }` (yalnız Posted onay yokken) | 200 (replay 200) | IDEMPOTENCY_KEY_REUSED, REASON_REQUIRED, WMS_CONFLICT, WMS_INVALID_TRANSITION, WMS_CONFIRMATION_PENDING, WMS_POSTING_INTEGRITY_ERROR (görev kilitli ya da önce ledger araması geç commit buldu) |
| `POST /putaway-tasks/{taskId}/confirmations/{seq}/resolve-integrity` | `resolvePutawayIntegrity` | `warehouse.putaway-tasks.resolve` (öneri, Tier-3) | If-Match; `{ idempotencyKey, reason }` (10–1000) — "Uzlaştır" (inceleme 03 F-2, inceleme 04 F-1): ledger yeniden okunur, anahtarı tutan hareket (`holderMovement`) döner; kaynağı bu onaysa (`MOD-0178` / bu görev / bu `seq`) → onay Posted, defter değerleri yazılır, fark varsa sapma olayı; yabancıysa, işlenmemişse **ya da kendi hareketi ters kaydedilmişse** → onay çözümsüz Refused (`refusalSource = Resolve`); görev açıksa **açık kalır** (kapatma ayrı İptal / Kalanı kapat), **görev terminalse terminal kalır** (yeniden açılmaz, yalnız onay kaydı düzelir; §6 onay geçişleri); otomatik kapatma yok; upstream çağrısı yok | 200 `WarehouseTask` (`integrityResolution`; replay 200) | IDEMPOTENCY_KEY_REUSED, REASON_REQUIRED, WMS_CONFLICT, WMS_CONFIRMATION_NOT_RESOLVABLE (onay `IntegrityError` değil), WMS_CONFIRMATION_PENDING (görevde Pending / uçuştaki onay var — inceleme 05 F-1), WMS_CONFIRMATION_IN_FLIGHT, WMS_TASK_NOT_FOUND, 403 (izin yok) |
| `GET /putaway-tasks/{taskId}/events` | `getPutawayTaskEvents` | `warehouse.putaway-tasks.read` | `page`, `pageSize` | 200 sayfa `WarehouseTaskEvent` | WMS_TASK_NOT_FOUND |
| `GET /location-roles` | `listLocationRoles` | `warehouse.location-roles.read` | `warehouseId?`, `role?`, `page`, `pageSize` | 200 sayfa | INVALID_REQUEST |
| `POST /location-roles` | `createLocationRole` | `warehouse.location-roles.manage` | `{ idempotencyKey, locationId, roles[], acceptedStorageConditions[], note? }` | 201 + ETag (replay 200 — aynı anahtar + aynı gövde 409 `WMS_LOCATION_ROLE_EXISTS` almaz) | IDEMPOTENCY_KEY_REUSED, UNKNOWN_LOCATION, WMS_LOCATION_ROLE_EXISTS, WMS_LOCATION_ROLE_CONFLICT, WMS_LOCATION_ROLE_INHERITED_CONFLICT, WMS_LOCATION_TYPE_NOT_ALLOWED, WMS_STORAGE_CONDITION_INVALID |
| `GET /location-roles/{roleId}` | `getLocationRole` | `warehouse.location-roles.read` | — | 200 + ETag | WMS_LOCATION_ROLE_NOT_FOUND |
| `PUT /location-roles/{roleId}` | `updateLocationRole` | `warehouse.location-roles.manage` | If-Match; `{ idempotencyKey, roles?, acceptedStorageConditions?, note? }` | 200 (replay 200) | IDEMPOTENCY_KEY_REUSED, WMS_CONFLICT, WMS_LOCATION_ROLE_CONFLICT, WMS_STORAGE_CONDITION_INVALID, WMS_LOCATION_ROLE_INHERITED_CONFLICT, WMS_LOCATION_HAS_OPEN_TASKS (Receiving kaldırılırken) |
| `DELETE /location-roles/{roleId}` | `deleteLocationRole` | `warehouse.location-roles.manage` | If-Match; Idempotency-Key | 200 (soft delete; replay 200) | IDEMPOTENCY_KEY_REUSED, WMS_CONFLICT, WMS_LOCATION_ROLE_NOT_FOUND, WMS_LOCATION_HAS_OPEN_TASKS |
| `POST /location-roles/bulk-delete` | `bulkDeleteLocationRoles` | `warehouse.location-roles.manage` | `{ idempotencyKey, items: [{ roleId, version }] }` | 200 satır başına sonuç; replay 200: silinen satır silindi, silinmeyen `Conflict` / `NotFound` (kural 5) | INVALID_REQUEST, IDEMPOTENCY_KEY_REUSED |
| §2 `GET /outbound-shipments`, `GET /outbound-shipments/{outboundId}` | (frozen) | `warehouse.outbound-shipments.read` | aynen | aynen | aynen |

Onay cevabı: `TaskConfirmation.status` her durumda gövdede (`Posted` 201, `Refused` 422 `details.task`, `Pending` 503 `details.task`,
replay 200) — istemci yeniden denemenin güvenli olduğunu `postingKey`'den bilir; aynı isteğin ağ tekrarı aynı `Idempotency-Key` ile
200 replay alır (kural 5).

## 5. Error codes

### 5.1 WMS-BUNDLE'ın kendi kodları (öneri)
| Kod | HTTP | Anlam |
|---|---|---|
| `WMS_TASK_NOT_FOUND` | 404 | görev yok / başka kiracı |
| `WMS_LOCATION_ROLE_NOT_FOUND` | 404 | rol yok |
| `WMS_CONFLICT` | 409 | bayat If-Match; ya da 0-satır kuralının (C2) / (E) dalı — kayıt bu denemeden bağımsız değişti, yazım yok, `details.task` güncel görev (pack §8.3; bağımsız inceleme 09 F-5) |
| `WMS_INVALID_TRANSITION` | 409 | durum geçişi izinsiz |
| `WMS_CONFIRMATION_PENDING` | 409 | Pending onay varken yeni onay / düzenleme / üstlenme / kısa kapatma / iptal / **görevdeki başka bir onayın "Uzlaştır"ı** / **"Geri taşındı"** / **toplu atama** (satır sonucu `PendingConfirmation`) (görev kilidi, M-4; inceleme 05 F-1, bağımsız inceleme 07 F-1, 08 F-5) |
| `WMS_CONFIRMATION_CONFLICT_RETRY` | 503 | onay yazımı 0-satır kuralının (A) dalında 3 kez CAS'ı kaybetti; onay Pending, kilit korunur; hareket defterde olabilir — "Yeniden dene" ya da uzlaştırıcı çözer (bağımsız inceleme 08 F-5) |
| `WMS_CONFIRMATION_NOT_RETRYABLE` | 409 | Posted / Refused onay yeniden denenemez |
| `WMS_SOURCE_STOCK_NOT_FOUND` | 422 | kaynak kova şirkette yok / OnHand 0 |
| `WMS_SOURCE_NOT_RECEIVING` | 422 | kaynak Receiving rolünde değil |
| `WMS_STOCK_NOT_PUTAWAY_ELIGIBLE` | 422 | kova `AVAILABLE` değil (tutulan stok) — oluşturmada ve **onay anında** (ekranda "Stok karantinaya girdi") |
| `WMS_QUANTITY_EXCEEDS_OPEN` | 422 | planlanan > açık miktar (`details.openQuantity`) |
| `WMS_QUANTITY_EXCEEDS_REMAINING` | 422 | onay > kalan (`details.remainingQuantity`) |
| `WMS_TARGET_NOT_ALLOWED` | 422 | hedef bin kuralı (`details.rule`: NotStockable / OtherWarehouse / SameAsSource / RoleLocation / Inactive) |
| `WMS_ASSIGNEE_INVALID` | 422 | atanan kullanıcı geçersiz |
| `WMS_LOCATION_ROLE_EXISTS` | 409 | düğüm için kayıt zaten var (düğüm başına tek kayıt) |
| `WMS_LOCATION_ROLE_CONFLICT` | 409 | aynı düğüm hem Receiving hem Staging |
| `WMS_LOCATION_TYPE_NOT_ALLOWED` | 422 | rol için tip Site / Warehouse |
| `WMS_RECORD_CODE_UNAVAILABLE` | 503 | geçici RCS sayacı yazılamadı (pack Q-07 B) |
| `WMS_COMPANY_CODE_INVALID` | 422 | MDM tüzel kişilik `Code` `^[A-Z]{2,5}$` değil — yerelleştirilmiş "şirket kodunu düzeltin" (M-2) |
| `WMS_STORAGE_CONDITION_MISMATCH` | 422 | hedefin etkin kabul kümesi ürün koşullarını karşılamıyor (§3.6) |
| `WMS_STORAGE_CONDITIONS_UNKNOWN` | 422 | ürünün `storageConditions` alanı PRODUCT-MASTER'da yok (bilinmiyor, boş sayılmaz) |
| `WMS_STORAGE_CONDITION_INVALID` | 400 | etiket kodu sözleşmede yok / tekrar / birden çok sıcaklık kodu |
| `WMS_LOCATION_HAS_OPEN_TASKS` | 409 | açık görevi olan düğümden Receiving kaldırma / kayıt silme |
| `WMS_NOT_ASSIGNEE` | 403 | onaylayan atanan değil ve sorumlu değil |
| `WMS_TASK_ALREADY_ASSIGNED` | 409 | atanmış görev üstlenilemez (sorumlu `updatePutawayTask` / `bulkAssignPutawayTasks` ile yeniden atar) |
| `WMS_QUANTITY_BELOW_CONFIRMED` | 422 | planlanan < onaylanan + bekleyen |
| `WMS_SERIAL_PARTIAL_NOT_SUPPORTED` | 422 | seri kontrollü SKU'da kısmi onay / kısa kapatma / miktar düzenleme (S1 yalnız tam miktar, F-1) |
| `WMS_SERIAL_BUCKET_CHANGED` | 422 | onay ya da yeniden deneme anında kovadaki seri kümesi görev miktarından farklı (`details.expected`, `details.found`) |
| `WMS_CONFIRMATION_NOT_RESOLVABLE` | 409 | "Geri taşındı" yalnız çözümsüz Refused onayda; "Uzlaştır" yalnız `IntegrityError` onayda |
| `WMS_LOCATION_ROLE_INHERITED_CONFLICT` | 409 | atası / alt ağacı karşı rolü taşıyan düğüme Receiving / Staging verilemez (F-13) |
| `WMS_POSTING_INTEGRITY_ERROR` | 409 | ledger aynı `postingKey` ile farklı bir hareket işlemiş (`IDEMPOTENCY_KEY_REUSED`) ya da 0-satır (D): bu deneme Posted / replay aldı ama onay Refused (→ `IntegrityError`, `StatusDiverged`) ya da zaten `IntegrityError` (yazım yok, `priorResolution` korunur; bağımsız inceleme 10 F-2) ya da koşullu artış planlananı aşıyor (`details.reason = OverPlanned`, savunma; inceleme 04 F-2) ya da holder ters kaydedilmiş (`HolderReversed`) ya da kapatıcı komutun **ön araması** geç commit buldu (`StatusDiverged`; yeni onay / "Geri taşındı" / İptal / Kalanı kapat 409 döner, kendi değişikliğini yazmaz) ya da savunma dalı geç commit buldu (`StatusDiverged`); onay `IntegrityError`, görev kilitli; çıkış yalnız `resolvePutawayIntegrity` (inceleme 02 F-1, inceleme 03 F-1 / F-2, bağımsız inceleme 05 F-1 / F-2) |
| `WMS_CONFIRMATION_IN_FLIGHT` | 409 | onayın uçuş kilidi geçerli (asıl istek ya da başka bir deneme sürüyor); `details.inFlightUntilUtc` (inceleme 03 F-1) |
| `WMS_SERIAL_BUCKET_MIXED_STATUS` | 422 | aynı lokasyonda aynı lot + SKU'nun AVAILABLE dışı stoğu var — seri görevi S1'de açılmaz / onaylanmaz (inceleme 02 F-2) |
| S2: `WMS_ALLOCATION_FAILED` (422), `WMS_OUTBOUND_NOT_EDITABLE` (409), `WMS_PACKAGE_INCOMPLETE` (409), `UNKNOWN_OUTBOUND` (404, frozen) | | |

### 5.2 Yeniden tanımlanmayan, kullanılan kodlar
`INVALID_REQUEST`, `IDEMPOTENCY_KEY_REUSED`, `REASON_REQUIRED`, `CROSS_LE_FORBIDDEN`, `UNKNOWN_LOCATION`, `LOT_BLOCKED`, `LOT_EXPIRED`,
`LOT_UNDER_RECALL` (INVENTORY 1.4.0 şekli; `details.movementType: LOCATION_TRANSFER` — 0178 ön kontrolü, §8), `LOT_RETEST_DUE`,
`INSUFFICIENT_STOCK`, `SERIAL_NOT_AVAILABLE`, `SERIAL_COUNT_MISMATCH`, `ITEM_NOT_ACTIVE`, `SKU_LEVEL_NOT_STOCKABLE`,
`PRODUCT_MASTER_DATA_INCOMPLETE`, `PRODUCT_MASTER_UNAVAILABLE`, `LOCATION_MASTER_UNAVAILABLE`, `LEGAL_ENTITY_MASTER_UNAVAILABLE`,
`INVENTORY_UNAVAILABLE` — INVENTORY'nin cevabı **değiştirilmeden** iletilir.

## 6. State machines

### 6.1 Putaway görevi (S1)
| Kimden | Kime | Tetik | Koşul |
|---|---|---|---|
| — | `Open` | createPutawayTask | §4 kuralları |
| `Open` | `InProgress` | ilk Posted onay, kalan > 0 | |
| `Open` / `InProgress` | `Completed` | Posted onay ile kalan = 0 | |
| `Open` / `InProgress` | `Completed` | closePutawayTaskShort | ≥ 1 Posted, Pending yok, gerekçe |
| `Open` / `InProgress` | `Cancelled` | cancelPutawayTask | hiç Posted onay yok, Pending yok, gerekçe (Posted varsa yalnız closePutawayTaskShort) |

**Onay geçişleri (inceleme 04 F-4):**
| Kimden | Kime | Tetik | Koşul |
|---|---|---|---|
| — | `Pending` | confirmPutawayTask (işlem 1) | ön kontroller geçti; uçuş kilidi alındı |
| `Pending` | `Posted` | işlem 2 (2xx) / retry veya uzlaştırıcı (ledger `Committed`) | `AttemptId` (sweeper: kilit boş / geçmiş) + `Confirmed + q ≤ Planned` |
| `Pending` | `Refused` | işlem 2 (4xx) / retry ön kontrol reddi | — |
| `Pending` | `IntegrityError` | `IDEMPOTENCY_KEY_REUSED` · `Confirmed + q > Planned` (savunma; gerçek akışta tetiklenmez) | — |
| `Refused` | `IntegrityError` | 0-satır (D): bu deneme Posted / replay aldı, onay başka denemenin yazımıyla Refused; filtre `version` + `status == Refused` | `StatusDiverged`; `resolution` → `priorResolution`, `resolution = null`; onay zaten `IntegrityError` ise yazım yok, `priorResolution` korunur, 409 (bağımsız inceleme 10 F-2) |
| `Refused` (`refusalSource ∈ {PreCheck, Ledger4xx}`, `refusedOnRetry = true`, `refusedAtUtc` son 30 dk; `resolution`'dan ve görev durumundan bağımsız) | `IntegrityError` | uzlaştırıcının savunma dalı (ayrı imleç ve bütçe), ledger `Committed` (geç commit); filtre `version` + `status == Refused` | `StatusDiverged`; `resolution` → `priorResolution`, `resolution = null` (inceleme 05 F-3, bağımsız inceleme 05 F-1, 06 F-2, 07 F-5 e) |
| `Pending` | `Posted` (CAS yinelemesi) | işlem 2 / retry (i) / uzlaştırıcı yazımı 0 kayıt; onay hâlâ Pending ve deneme bu (uzlaştırıcı: kilit boş) | yeni `version` ile yinelenir (≤ 3); onay Refused ise (D) → `IntegrityError` (`StatusDiverged`), zaten `IntegrityError` ise yazım yok; başka denemenin kilidi: geçerliyse 409 `WMS_CONFIRMATION_IN_FLIGHT` (bağımsız inceleme 07 F-1), dolmuşsa yazım yok, 409 `WMS_CONFLICT` (bağımsız inceleme 09 F-5) |
| `Refused` | `IntegrityError` (+ komut 409) | yeni onay / markPutawayConfirmationReturned / cancelPutawayTask / closePutawayTaskShort — önce ledger araması, `Committed` | `StatusDiverged`; komut kendi değişikliğini yazmaz (bağımsız inceleme 05 F-1) |
| `Pending` | `IntegrityError` | retry (i) / uzlaştırıcı: ledger `Committed` ama `reversedByTransactionId` dolu | `HolderReversed` (bağımsız inceleme 05 F-2) |
| `IntegrityError` | `Posted` | resolvePutawayIntegrity (a) | görevde Pending onay yok; anahtarı tutan hareket bu onayın (`MOD-0178`, görev, `seq`); defter değerleri (lot, seriler dahil) yazılır; `confirmedQuantity > plannedQuantity` olabilir → sapma olayı |
| `IntegrityError` | `Refused` (çözümsüz) | resolvePutawayIntegrity (b) | anahtarı tutan hareket yabancı, işlenmemiş **ya da ters kaydedilmiş**; görev açık kalır |
| `IntegrityError` (görev terminal) | `Posted` / `Refused` | resolvePutawayIntegrity | yalnız onay kaydı düzelir; görev yeniden açılmaz; Cancelled görevde Posted → sapma `PostedAfterCancel` (bağımsız inceleme 05 F-1) |
| `Refused` | `Refused` + `resolution` | yeni Posted onay / markPutawayConfirmationReturned | `SupersededByNewConfirmation` / `ReturnedToSource` |

Onay: `Pending → Posted | Refused`; `Pending` yalnız `retry` ile ilerler (aynı `postingKey`, aynı tarihler) ve görevi kilitler; sonuç
yazımı koşulludur (`status == Pending`, F-3). `Refused` → `resolution: SupersededByNewConfirmation` (aynı görevde yeni Posted onay) ya
da `ReturnedToSource` ("Geri taşındı", hareket yok) (F-4). `IntegrityError` istisnadır: resolution ve görev eylemleri kapalı
(inceleme 02 F-1); **tek çıkış** `resolvePutawayIntegrity` (sorumlu, gerekçe; anahtarı tutan hareket bu onaysa Posted, yabancıysa
çözümsüz Refused — görev açık kalır, otomatik kapatma yok; inceleme 03 F-2, inceleme 04 F-1). Uçuş kilidi (`inFlightUntilUtc`) geçerliyken yeniden deneme 409 `WMS_CONFIRMATION_IN_FLIGHT` (posting süresi aşımında da kilit süresi
dolana kadar korunur — inceleme 05 F-3); arka plan uzlaştırıcısı
kaydı atlar, `NotCommitted` turunda hiçbir şey yazmaz (inceleme 03 F-1, 02 F-5). Durum onaylanan miktardan
türetilir; `integrationFlag` ayrı rozettir. Açık görevde otomatik askı yoktur; karantina / recall kontrolü onay anındadır (M-5).

### 6.2 Outbound (S2, §2.2) ve pick görevi (S2 — putaway ile aynı makine, `Kind = Pick`).

## 7. Events

| Olay | Yön | Şema | Durum |
|---|---|---|---|
| `OutboundShipmentReady` | 0178 → MOD-0183 | WAREHOUSE-OUTBOUND `OutboundShipmentReadyEvent` (aynen) + §2.3 eklemeli üyeler | S2; outbox'a yazılır; taşıma (MOD-0035 bus) bugün kapalı → Pending; donmuş alternatif = `GET /outbound-shipments?status=ReadyToShip` poll |
| `WarehouseTaskCompleted` | iç | `{ taskId, number, kind, legalEntityId, warehouseId, item, lotId, confirmedQuantity, transactionIds[], correlationId }` | S1'de **yazılmaz** (pack §8.9: S1'de iç olay yok; görevin tamamlanması yerel zaman çizelgesinde `Completed`, pack §4.3 — bağımsız inceleme 09 F-2 d); tüketici yok — **yayınlanmış sözleşme değildir** (INVENTORY ASSUMPTION-0173-08 emsali) |
| MVP-6 `ShipmentDispatched` | MOD-0183 → 0178 | SHIPMENT-BUNDLE `LifecycleEventEnvelope` (`receiveShipmentLifecycleEvent`) | S2; bus kapalıyken poll: `GET /api/shipment-bundle/shipments?sourceDocumentId={outboundId}&legalEntityId=…` (pack Q-M6-4) |

Olay zarfı: `eventId` (uuid), `occurredAt`, `correlationId` (uuid), `tenantId`, `legalEntityId` — LE bağı olaya yazılır (GAP-0183-02).

## 8. Scan events (MOD-0182'ye ayrılmış)

0178 tarama olayı **tutmaz**. Onay gövdesi opak `scanRef` (≤ 200) ve `channel` taşır; MOD-0182 kendi `ScanEvent` şemasını
(cihaz, okunan kod, GTIN / SSCC, zaman, kullanıcı, sonuç) WMS-BUNDLE'a eklemeli bölüm olarak getirir ve `scanRef` ile bağlar. GTIN
çözümü MDM P6'ya bağlıdır — 0178'in değil.

## 9. Exception model (S3 iskeleti)

`WarehouseException { exceptionId, kind: PostingRefused | PostingPendingTooLong | OutboundDrift | DispatchNotSeen | CancelAfterReady,
taskId? | outboundId?, code, details, openedAtUtc, status: Open | Acknowledged | Resolved, resolvedBy?, note? }` — kaynakları S1 onay
kayıtları ve S2 outbound durumu; salt okunur liste `GET /exceptions` (`warehouse.exceptions.read`). Çözümleme eylemleri S3'te.
Activity Trace: `GET /activity` = görev olayları + `sourceModule = MOD-0178` INVENTORY hareketleri (okuma). Integration Monitor:
`GET /integration/status` = outbox Pending / Delivered sayıları, son poll zamanı, MVP-6 okunabilirliği.

## 10. OTel (öneri — Blueprint WMS-BUNDLE)

`ActivitySource "Diten.Warehouse"`; span'ler `wms.putaway.create`, `wms.putaway.confirm`, `wms.posting` (INVENTORY çağrısı alt span),
`wms.outbound.release`; öznitelikler `wms.task.kind`, `wms.task.status`, `wms.posting.result`, `wms.error.code` (kişisel veri yok,
adres yok). Sayaçlar: `wms_task_confirmations_total{kind,result}`, `wms_posting_refused_total{code}`, `wms_pending_confirmations`
(gauge), `wms_outbound_ready_total`. Servisin exporter'ı ölçülmedi (pack §8.11, Q-EA-5).

## 11. S2 iskeleti (v1.1.0 — W-5 ve MVP-6 cevaplarından sonra kesinleşir)

| Yöntem + yol | operationId | x-permission | Not |
|---|---|---|---|
| `POST /outbound-shipments` | `createOutboundShipment` | `warehouse.outbound-shipments.create` | `{ idempotencyKey, warehouseId, orderRef?, shipTo, lines[] }` → tahsis (FEFO `getFefoOrder` + lot rezervasyonu W-5) + pick görevleri; başarısızsa 422 `WMS_ALLOCATION_FAILED` (satır başına neden), kayıt açılmaz |
| `POST /outbound-shipments/{outboundId}/cancel` | `cancelOutboundShipment` | `warehouse.outbound-shipments.cancel` | rezervasyonları bırakır; staging'deki stok için `Origin = Cancellation` putaway görevi; `ReadyToShip` sonrası kural Q-M6-7 |
| `POST /outbound-shipments/{outboundId}/release` | `releaseOutboundShipment` | `warehouse.outbound-shipments.release` | `Packed → ReadyToShip`, `readyAt`, olay |
| `GET /pick-tasks`, `GET /pick-tasks/{taskId}` | `listPickTasks`, `getPickTask` | `warehouse.pick-tasks.read` | §3.1 `kind: Pick` |
| `POST /pick-tasks/{taskId}/confirmations` | `confirmPickTask` | `warehouse.pick-tasks.confirm` | bin → staging `LOCATION_TRANSFER`; recall ön kontrolü; başka lot seçimi = FEFO sapması → `fefoBypassReason` (10–1000) + yeniden rezervasyon |
| `POST /outbound-shipments/{outboundId}/packages` | `createPackage` | `warehouse.packages.create` | `{ idempotencyKey, weightKg?, dimensionsCm?, contents[] }` — ağırlık / ölçü elle; MDM P9 gelene kadar ambalaj seviyesi dönüşümü yok (Q-MDM-1) |
| `PUT /packages/{packageId}` | `updatePackage` | `warehouse.packages.update` | If-Match |
| (iç) dispatch alımı | — | — | olay ya da poll → `Shipped` |

## 12. Field mapping — tüketilen sözleşmeler

| WMS alanı | Kaynak sözleşme · operasyon · alan | Yön | Kural |
|---|---|---|---|
| `warehouseId` | LOCATION `getLocation` / `getLocationPath` · `type = Warehouse` | okuma | rol ve görev deposu; `LocationRef.legalEntityId` = çağıranın şirketi |
| `fromLocationId`, `toLocationId` | LOCATION `getLocation` · `locationId`, `type`, `status`, `isStockable`; `getLocationPath` | okuma | hedef `Bin` / stoklanabilir `StorageLocation`, `Active`, aynı depo yolunda |
| `WarehouseLocationRole.locationId` | LOCATION `getLocation` · `type ∈ {StorageLocation, Zone, Bin}` | okuma | düğüm yaratılmaz |
| `PutawayCandidate.onHand`, `stockStatus`, `lotId`, `baseUomId`, `lastPostingDate` | INVENTORY `getBalance` · `BalanceView.onHand / stockStatus / lotId / baseUomId / lastPostingDate` (süreç içi aynı sorgu) | okuma | saklanmaz; `openQuantity` okuma anında |
| `item { itemId, skuId, skuLevel }` | INVENTORY `BalanceView.itemId / skuId / skuLevel` (PRODUCT-MASTER kimliği) | okuma | `Item` seviyesi görevde kabul, outbound'da değil (§2.1) |
| `lotNumber`, `lotStatus` | TRACE `getLot` · `LotRef.lotNumber`, `LotRef.status` (`trace.openapi.yaml:288-300`) | okuma | Blocked / Expired uygun değil |
| `storageConditions` (ürün) | PRODUCT-MASTER v1.1 `getSku` · `storageConditions` (`StorageConditionCode[]`, `product-master-bundle.openapi.yaml:601-604, 750`; **1.1.0-draft.16, donmamış**) | okuma | §3.6; alan yok = bilinmiyor |
| `acceptedStorageConditions` (lokasyon) | aynı `StorageConditionCode` kod kümesi | 0178 sahibi | §3.5 |
| `companyCodePrefix` | MDM tüzel kişilik `Code` (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:11`) — InventoryService'in MDM okuması (hangi uç: geliştiricinin ilk ölçümü) | okuma | `^[A-Z]{2,5}$` (M-2) |
| `warehouseCode` | LOCATION `getLocation` · `LocationRef.code` (Warehouse) | okuma | yalnız gösterim / sütun |
| `blockingDocument` | MOD-0175 tutma / bloke okuması (süreç içi); RECALL `getLotRecallStatus.cases[]` (vaka no); TRACE `LotRef.status` | okuma | belge numarası gösterimi (M-4) |
| `notEligibleReason: LotUnderRecall`, `LOT_UNDER_RECALL` | RECALL `getLotRecallStatus` · `LotRecallStatus.underRecall`, `cases[]` (süreç içi `IRecallLotStatusReader`) | okuma | kiracı geneli (CT-R7) |
| `notEligibleReason: StockHeld` | INVENTORY `BalanceView.stockStatus ≠ AVAILABLE`; QUARANTINE hold kovası (`warehouseId, locationId`) | okuma | yalnız `AVAILABLE` yerleştirilir (karar Q-02); ret `WMS_STOCK_NOT_PUTAWAY_ELIGIBLE` + yerelleştirilmiş mesaj |
| onay → hareket | INVENTORY `postMovement` · `MovementRequest`: `movementType = LOCATION_TRANSFER`, `fromWarehouseId = toWarehouseId = warehouseId`, `fromLocationId`, `toLocationId`, `fromStockStatus = toStockStatus = stockStatus`, `itemId`, `skuId`, `skuLevel`, `lotId`, `serialIds`, `quantity`, `uomId = baseUomId`, `sourceModule = MOD-0178`, `sourceType = PUTAWAY_TASK` (S2 `PICK_TASK`), `sourceDocumentId = taskId`, `sourceLineId = seq`, `documentGroupId = number`, `idempotencyKey = postingKey`, `transactionDate = postingDate = sunucu UTC`, `sourceSystem` (External) | yazma (süreç içi) | tek stok kapısı; ret kodu aynen |
| `transactionId`, `transactionNumber` | INVENTORY `MovementResponse.transactionId / transactionNumber` | okuma | replay'de aynı |
| S2 `fefoRank`, önerilen lot | FEFO `getFefoOrder` · `FefoEligibleLot.rank / lotId / available / allocated`; `FefoExcludedLot.reason` (RECALLED dahil) | okuma | öneri; sapma gerekçe ister |
| S2 `fefoBypassReason` | INVENTORY `MovementRequest.fefoBypassReason` (1.3.0; MVP-6'nın `GOODS_ISSUE_SALES`'inde) | taşıma | 10–1000 |
| S2 `reservationId` | INVENTORY `createReservation` → `ReservationResponse.reservationId` (W-5 eklemeli lot düzeyi) | yazma | SHIPMENT satırı `inventoryReferenceId` |
| `outboundId` | SHIPMENT-BUNDLE `createShipment` · `sourceModule = MOD-0178`, `sourceType = WAREHOUSE_OUTBOUND`, `sourceDocumentId = outboundId`, `warehouseReferenceId = warehouseId` (MOD-0183 pack `:91-92`) | MVP-6 okur | 0178 yazmaz |
| `Shipped` | SHIPMENT-BUNDLE `ShipmentStatus = Dispatched` (`queryShipments?sourceDocumentId`, ya da `ShipmentDispatched` olayı) | okuma | W-4 |
| `shipTo` | (kaynak sipariş — SoR belirsiz, pack Q-EA-3) | — | KVKK: log / OTel'e yazılmaz |
| `legalEntityId` | token / W-0173-02 | sunucu | gövdeden alınmaz |
| `correlationId` | Gateway `X-Correlation-Id` (E-1) | ilet | UUID |

## 13. Diğer sahiplerden gereken eklemeli değişiklikler (bu sözleşmenin parçası DEĞİL)

| Sahip | İhtiyaç | Dilim | Pack |
|---|---|---|---|
| MOD-0173 (MVP-1 CT, W-5) | lot (+ depo / lokasyon) düzeyinde rezervasyon; `postMovement` isteğe bağlı `reservationId` ile tüketim; ayrılmış miktarın `LOCATION_TRANSFER`'ı | S2 | §8.10 |
| MOD-0176 (MVP-1 CT) | FEFO kancasının başka outbound'a ayrılmış lotları değerlendirmesi | S2 | §8.10 (4) |
| MOD-0175 (MVP-1 CT) | (isteğe bağlı) tutmanın lokasyonunun taşınması | sonra | FU-0178-QH-01 |
| MVP-6 (MOD-0183) | §2.3 eklemeli üyelerin kabulü + anlık görüntü beyaz listesi; dispatch sinyali; rezervasyon tüketimi | S2 | Q-M6-1…9 |
| MDM (MOD-0290) | P9 ambalaj hiyerarşisi (bağımlılık değil; P9 gelene kadar koli elle, dönüşüm yok — karar Q-MDM-1) | S2 sonrası | FU-0178-P9; MDM'ye yalnız P9 takvimi (bilgi) |
| RCS-001 / BL-565 | ortak kod servisi (o zamana kadar geçici RCS sayacı, karar Q-07 B) + Tüzel Kişilik kısa kodu (Q-MDM-2) + önek kaydı (sahip onayı, FU-0178-RCS-02) | S1 | FU-0178-RCS-01 / 02 |

## 14. 3PL kapısı (W-3 — kapsam değil)

MOD-0179 ertelendi. Sözleşme dış kaynağa şu kapıları bırakır: görev `origin: External` + `sourceSystem` + `externalRef`; onay
`channel: External` (aynı `confirmPutawayTask` / `confirmPickTask` yolu, aynı defter kuralları); outbound `sourceSystem`. Dış
sistemin kimliği / anahtarı (servis kimliği, `X-Internal-Api-Key` deseni) MOD-0179 paketinde tanımlanır. Depo
`LocationRef.operatingMode = external-integrated` olan bir depoda 0178'in iç görev üretmemesi (dış WMS'in yaptığı) MOD-0179 kararıdır.

## 15. Açık kararlar

İnceleme 01 F-1…F-16 (pack §19.1.3), inceleme 02 F-1…F-10 (§19.1.4), inceleme 03 F-1…F-5 (§19.1.5), inceleme 04 F-1…F-4 (§19.1.6), inceleme 05 ön kontrolü F-1…F-5 (§19.1.7) ve bağımsız inceleme 05 F-1…F-7 (§19.1.8), bağımsız inceleme 06 F-1…F-6 (§19.1.9) ve bağımsız inceleme 07 F-1…F-5 (§19.1.10) ve bağımsız inceleme 08 F-1…F-5 (§19.1.11; merkezi denetim yalnız başarılı komutlarda — BL-501 olduğu gibi) ve bağımsız inceleme 09 F-1…F-5 (§19.1.12; merkezi kayıt ad ve gövde taşımaz — değerler yerel zaman çizelgesinde; (C2) → 409 `WMS_CONFLICT`) ve bağımsız inceleme 10 F-1…F-3 (§19.1.13; bütün yazma operasyonlarında istek idempotency'si — anahtar If-Match'ten önce, kural 5; 0-satır (D) yalnız Refused, zaten `IntegrityError` ise yazım yok) düzeltmeleri işlendi; W-6 (`Item` outbound) Beste'nin açık S2 kararıdır (pack §19.5).
Karar verildi (pack §19.1.1 / §19.1.2, Beste 2026-10-21): Q-01…Q-09, Q-11…Q-13, Q-MDM-1, Q-MDM-2, M-1…M-6 — bu taslak onlara göre
yazıldı (rol + saklama koşulu kaydı 0178'in, ayrı dosya, `WMS_*` burada, geçici RCS sayacı + MDM `Code` öneki, `warehouse.*` 0178'in,
yalnız `AVAILABLE` yerleştirilir, recall'daki lot reddedilir, saklama koşulu uyumu, üstlenme, onay kilidi, e-imza yok, koli elle).
Açık (pack §19.5): Q-10 (MVP-1 CT), Q-M2-1…2 (MVP-2), Q-M6-1…9 (MVP-6), Q-MDM-3 + kısa kod alanı / P9 takvimleri (bilgi) (MDM),
Q-EA-1…5 (EA). Kablo şeklini değiştirebilecek olanlar: Q-M6-1…9 (§2.3); PRODUCT-MASTER v1.1 donarken `storageConditions` değişirse §3.6.

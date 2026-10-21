---
id: MOD-0178
name: Putaway / Picking / Packing
domain: supply-chain-execution
service: Diten.InventoryService
shell: tenant
golden_reference: slim
data_mode: server
screens:
  - module: PutawayTasks
    data_mode: server
  - module: WarehouseLocationRoles
    data_mode: server
entity_base: EntityBase
status: draft
approval_status: pending
status_note: "draft — P0 (MOD-0178 pack + WMS-BUNDLE taslağı), MVP-1 CT (W-10) için 2026-10-21. Sahip kararları W-1 (Build), W-2 (Diten.InventoryService 5067, ayrı bounded context, ikinci bakiye yok), W-3 (MOD-0179 ertelendi), W-4 (GOODS_ISSUE_SALES MOD-0183'ün), W-5 (lot / lokasyon rezervasyonu MVP-1 CT'den), W-10, E-1, E-2, E-6 bağlayıcı (§19.1). Paket soruları Q-01…Q-09, Q-11…Q-13, Q-MDM-1, Q-MDM-2 ve mockup incelemesi kararları M-1…M-6 sahip kararıyla kapandı (Beste 2026-10-21, §19.1.1, §19.1.2). Onay yok; açık sorular yalnız diğer ekiplerde + Q-10 (§19.5). golden_reference / form_field_count yalnız S1 ekranları içindir (§9); S2 outbound ekranı compact olacak ve S2 paket revizyonunda screens listesine girer."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0178-putaway-picking-packing
started: 2026-10-21
target: 2026-12-31
form_field_count: 6
dependencies: [MOD-0173, MOD-0173-FU02, MOD-0174, MOD-0175, MOD-0176, MOD-0177, MOD-0290, INVENTORY-BUNDLE-v1.5.0, LOCATION-v1.1.0, TRACE-v1.4.0, QUARANTINE-v1.3.0, FEFO-BUNDLE-v1.3.0, RECALL-BUNDLE-v1.9.0, WAREHOUSE-OUTBOUND-v1.0.0 (owned), SHIPMENT-BUNDLE-v3.3.0 (MVP-6, read only), BL-501 (central audit), BL-565 (RCS-001 shared code service — not built), DEV-0000]
---

# MOD-0178 — Putaway / Picking / Packing

> **Kimlik (DCP-002) — ölçüldü 2026-10-21, dal `chore/ct/wms-ct-01` @ `47a2c1cb9`:**
> `python .antigravity/scripts/verify_module_id.py . --check-id MOD-0178 --name "Putaway / Picking / Packing"` → çıkış **0**
> ("OK MOD-0178: proven against Blueprint/registry"). Kanonik ad boşluklu yazılır: `Putaway/Picking/Packing` (boşluksuz)
> denemesi fail-closed döner (canonical-name mismatch). Registry satırı
> `execution/registries/module-id-registry.md:298` (`MOD-0178 | Putaway / Picking / Packing | putaway-picking-packing | Module |
> reserved / planned | … | supply-chain-execution | Blueprint W-3 (Warehouse WMS). DCP-002 gate OK 2026-09-11. DCP-009. Posts
> movements to MOD-0173; no second balance.`). Bu paket registry satırını değiştirmez.
>
> **Keşif (`verify_module_id.py --discover "Putaway Picking Packing"`, CT koşusu):** çıkış 3, rapor-sha256
> `bcffb5e33af7d925f634e58da8c0db0eaff6747e11f6affed64a97e3fd151eba`. Bulgular ve kararlar:
> 1. **Blueprint 8.8 `Blueprint_Data` satırı = bu modül** (Warehouse (WMS), W-3, bağımlılık "Inventory Ledger & Valuation;
>    Data Contract Registry", soft pages "WMS Integration Monitor; Activity Trace; Exception Inbox", sözleşme WMS-BUNDLE —
>    warehouse task schema, scan events, exception model, OTel; SoR warehouse tasks, execution events, pick/pack confirmations).
> 2. **Registry satırı = bu modül** (yukarıda).
> 3. **`execution/domains/master-data-management/module-packs/MDM-057-putaway-picking-packing.md`** yalnız eski dallarda var;
>    eski takma ad, yanlış domain (MDM) → **mükerrer / terk edilmiş, KULLANILMAZ.** Bu paket ondan hiçbir şey almaz.
>
> Keşif rapor dosyası henüz commit edilmedi: **keşif raporu CT tarafından kaydedilecek.**
>
> **Blueprint sapması (W-1):** Blueprint MOD-0178'i "Buy/Partner" işaretler. **Sahip kararı 2026-10-21: BİZ YAPIYORUZ
> (Build).** Bu paket Build varsayar; Blueprint düzeltmesi **EA'ya açık iş** (FU-0178-EA-01). Paket Blueprint'i değiştirmez.
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`.
> Sahip kararları (2026-10-21, `docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md` §2.1, §4.1) bağlayıcıdır.
> Eski intake `docs/analysis/workpackages/WP-MVP5-warehouse.md` ("pick→issue/transfer post") **W-4 ile çelişir; bu pakette W-4
> geçerlidir:** 0178 `GOODS_ISSUE_SALES` yazmaz.
>
> **Sözleşme:** sahibi olunan `WAREHOUSE-OUTBOUND` 1.0.0 (`docs/analysis/contracts/warehouse-outbound.openapi.yaml`,
> `x-status: FROZEN`, `x-owner: MOD-0178`, `x-front-loaded-for: MOD-0183`) **bozulmaz**. WMS-BUNDLE taslağı
> `docs/analysis/contracts/drafts/wms-bundle-v1-proposal.md` onu aynen kapsar (superset). Tüketilen donmuş sözleşmeler bu
> paketle değişmez: INVENTORY-BUNDLE 1.5.0, LOCATION 1.1.0, TRACE 1.4.0, QUARANTINE 1.3.0, FEFO-BUNDLE 1.3.0, RECALL-BUNDLE
> 1.9.0, SHIPMENT-BUNDLE 3.3.0 (MVP-6).

## 1. Module Summary

MOD-0178, ERP-vNext deposunun **yürütme katmanıdır**: mal kabul alanından rafa **yerleştirme (putaway)**, sevkiyat için
**toplama (picking)** ve **paketleme (packing)** görevlerini, onaylarını ve "sevke hazır" outbound kaydını tutar. Stok gerçeği
MOD-0173'tedir; 0178 **ikinci bakiye tutmaz**, defter koleksiyonlarına yazmaz. Her fiziksel yer değişikliği INVENTORY'nin olağan
kayıt yolundan (`InternalMovementPostingService` — `postMovement` ile aynı parser, idempotency, PRODUCT-MASTER / LOCATION / lot /
recall / FEFO / retest kuralları ve atomik defter yazımı; `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Inventory/Services/InternalMovementPostingService.cs:18-31`)
`LOCATION_TRANSFER` olarak geçer.

Hedef kullanıcı: depo operatörü (görevi yapar ve onaylar), depo sorumlusu (görev açar, atar, iptal eder, lokasyon rollerini
yönetir), sevkiyat planlayıcısı (S2: outbound oluşturur, sevke hazır yapar). Kiracı modülüdür (`_LayoutTenantShell`, 7 dil).

**Mülkiyet sınırı (W-4):** 0178 stoğu düşürmez. Toplama / paketlemede rezervasyon (W-5) + sevk alanına (staging)
`LOCATION_TRANSFER` yapar ve outbound'u `ReadyToShip` yapıp `OutboundShipmentReady` yayınlar; MVP-6 (MOD-0183) sevkiyat
`Dispatched` olduğunda `GOODS_ISSUE_SALES`'i **tek yazıcı** olarak yazar; 0178 bu dispatch sinyaliyle outbound'u `Shipped` yapar.

### Slice plan (her dilim kendi WP'si; bir sonrakine CT kabulünden sonra geçilir)

| Dilim | Kapsam | Sözleşme | Bağımlılık | Kabul |
|---|---|---|---|---|
| **S1 — Yerleştirme (putaway)** | Lokasyon rolleri (Receiving); yerleştirilecek stok listesi (INVENTORY bakiyesinden okunur, saklanmaz); elle / listeden putaway görevi; atama; kısmi ve tam onay → `LOCATION_TRANSFER`; kısa kapatma; iptal; idempotent onay + Pending kurtarma; görev zaman çizelgesi; manifest + nav; ekranlar `PutawayTasks`, `WarehouseLocationRoles` | WMS-BUNDLE v1.0.0 (S1 operasyonları + WAREHOUSE-OUTBOUND 1.0.0 aynen) | **bugünkü sözleşmelerle yapılabilir**; görev numarası RCS biçiminde geçici sayaçtan (Q-07 B, FU-0178-RCS-01), şirket öneki MDM tüzel kişilik `Code` (M-2); saklama koşulu uyumu S1 engelidir (M-3) — ürün koşulları PRODUCT-MASTER v1.1 `storageConditions` | §16 AC-1…AC-24 |
| **INT-WMS** | Gateway `/api/warehouse` + `/api/warehouse/{everything}` → 5067 + route kapsama testi (integration-agent) | — | S1 controller | §16 AC-25 |
| **S2 — Toplama / Paketleme / Sevke hazır** | Outbound oluşturma (compact ekran), tahsis = FEFO önerisi (`getFefoOrder`) + lot / lokasyon rezervasyonu, pick görevi, `LOT_UNDER_RECALL` ret, FEFO sapma gerekçesi, staging'e `LOCATION_TRANSFER`, paket / koli, `ReadyToShip` + `OutboundShipmentReady`, MVP-6 dispatch → `Shipped`, iptal + geri yerleştirme | WMS-BUNDLE v1.1.0 (S2 iskeleti §11 WMS taslağı) + WAREHOUSE-OUTBOUND eklemeli üyeler | **W-5 (INVENTORY lot / lokasyon rezervasyonu, MVP-1 CT ayrı WP)**, MVP-6 cevapları §19.5, paket revizyonu | §16 AC-26…AC-35 (iskelet; S2 revizyonunda kesinleşir) |
| **S3 — İzleme ekranları (gerekirse)** | Exception Inbox, Activity Trace, WMS Integration Monitor (Blueprint soft pages) | WMS-BUNDLE okuma uçları | S1 + S2 | §16 AC-36 (iskelet) |
| **L10n** | Her dilimin anahtarları 7 dilde (en, tr, fr, es, zh, ar, ru) | — | ilgili dilim | §16 AC-23 |

Sıra: **S1 → INT-WMS → (W-5 kabul) → S2 → S3**. S1, W-5'i beklemez. MOD-0181 Sayım 0178 ile paralel yürüyebilir (yalnız
0173'e bağlı; plan §4); MOD-0180 Dalga ve MOD-0182 Barkod sonra.

## 2. Ownership and Boundaries

### Owns
- Depo görevleri (`WarehouseTask`, tür `Putaway` S1, `Pick` S2) ve onayları (`TaskConfirmation`), görev olayları (insert-only).
- Lokasyon rolleri ve kabul edilen saklama koşulları (`WarehouseLocationRole`: LOCATION düğümü başına tek kayıt — roller Receiving S1,
  Staging S2 + düğümün kabul ettiği saklama koşulu kodları, alt ağaca kalıtım; §4.4, §8.12). LOCATION'da bin
  **icat edilmez**; rol yalnız var olan bir LOCATION düğümünü işaret eder (FU02 pack `:78`, `:585`: "putaway rules, capacity,
  bin types, slotting — MOD-0178+").
- S2: `OutboundShipment` (WAREHOUSE-OUTBOUND kaynağı), `HandlingUnit` / `Package` (koli), pick / pack onayları.
- `WMS-BUNDLE` sözleşmesi (taslak) ve donmuş `WAREHOUSE-OUTBOUND` 1.0.0.

### Does not own
| Şey | Sahibi | 0178 nasıl dokunur |
|---|---|---|
| Stok bakiyesi, hareket, rezervasyon | MOD-0173 | bakiyeyi süreç içi okur; hareketi yalnız `InternalMovementPostingService` ile yazar; rezervasyonu (S2) INVENTORY'nin rezervasyon komutuyla yapar — **defter / bakiye / pozisyon koleksiyonuna doğrudan yazmaz** |
| Lokasyon ağacı (Site→…→Bin) | MOD-0173-FU02 | LOCATION ile okur / doğrular; düğüm yaratmaz, değiştirmez |
| Lot, seri, soyağacı | MOD-0174 | lot durumunu okur (Draft / Released / Blocked / Expired) |
| Karantina / bloke kararları | MOD-0175 | karar vermez; tutulan (held) stoğu yerleştirmez (§8.4) |
| FEFO politikası ve sırası | MOD-0176 | S2'de `getFefoOrder`'ı süreç içi okur; sapmada gerekçe ister |
| Geri çağırma | MOD-0177 | lotun geri çağırmada olup olmadığını okur (`IRecallLotStatusReader`) |
| Ürün kimliği, UoM, saklama koşulları, ambalaj hiyerarşisi (P9), GTIN (P6) | MOD-0290 (MDM) | `itemId / skuId / skuLevel` + baz birim + `getSku.storageConditions` (M-3, okuma); P6 / P9'a **bağımlı değildir** |
| Tüzel kişilik `Code` | MDM (MOD-0220) | görev numarası öneki için okunur (M-2); değiştirilmez |
| E-imza | MOD-0022 | **kullanılmaz** — putaway onayı e-imza istemez (M-1, §8.14) |
| Satış çıkışı (`GOODS_ISSUE_SALES`), sevkiyat, POD | MOD-0183 (MVP-6) | yazmaz (W-4); dispatch'i okur (S2) |
| Barkod / tarama olayları (ScanEvent) | MOD-0182 | görev onayında opak `scanRef` kapısı bırakır; ScanEvent tutmaz |
| 3PL dış depo | MOD-0179 (ertelendi, W-3) | dış kaynaklı görev / onaya kapı bırakır (`origin: External`, `sourceSystem`, `externalRef`) — kapsam değil |
| Dalga planlama, sayım | MOD-0180 / MOD-0181 | yok |

### Boundary rules
1. **İkinci bakiye yok.** 0178'in hiçbir koleksiyonu miktar toplamı tutmaz; "açık miktar" (bakiye − açık görev kalanı) okuma
   anında hesaplanır, saklanmaz. Mimari test: `wms_*` koleksiyonlarında OnHand / Available / Reserved alanı yok.
2. **Stok yalnız INVENTORY'nin olağan yolundan değişir.** `IInventoryLedgerRepository` ya da defter koleksiyonu adı 0178 koduna
   referans olarak girmez (mimari test); tek kapı `WarehouseMovementPoster` → `InternalMovementPostingService`
   (`QuarantineMovementPoster` deseni, `…/Features/Quarantine/Services/QuarantineMovementPoster.cs:34-39`).
3. **Aynı doğrulamalar:** recall, FEFO, karantina, retest, lot, idempotency, negatif stok kuralları INVENTORY'nindir; 0178 onları
   yeniden yazmaz, ret kodunu olduğu gibi iletir. **Tek istisna** (§8.5): `LOCATION_TRANSFER` INVENTORY 1.4.0'da recall
   engelinden muaftır (`inventory-bundle.openapi.yaml:43-46`), bu yüzden 0178 putaway / pick öncesi lotun recall durumunu kendisi
   okur ve reddeder.
4. Her okuma ve yazma kiracı + şirket (`LegalEntityId`, W-0173-02) kapsamındadır.
5. 0178 `GOODS_ISSUE_SALES`, `STATUS_TRANSFER`, `WAREHOUSE_TRANSFER` yazmaz (S1/S2). `WAREHOUSE_TRANSFER` (depolar arası)
   kapsam dışıdır (recall engelli tür, `inventory-bundle.openapi.yaml:44`).

## 3. Owned Objects

| Nesne | Dilim | Koleksiyon (öneri, `wms_*`) | Not |
|---|---|---|---|
| `WarehouseTask` | S1 (Putaway), S2 (Pick) | `wms_tasks` | Değişebilir baş kayıt, `Version` (If-Match); durum makinesi §8.3 |
| `TaskConfirmation` (gömülü) | S1 | `wms_tasks.Confirmations[]` | Her onay bir INVENTORY hareketi; Pending → Posted / Refused |
| `WarehouseTaskEvent` | S1 | `wms_task_events` | Insert-only zaman çizelgesi |
| `WarehouseLocationRole` | S1 (Receiving + saklama koşulları), S2 (Staging) | `wms_location_roles` | LOCATION düğümü başına tek kayıt: roller + kabul edilen saklama koşulları; soft delete |
| `OutboundShipment` | S2 | `wms_outbound_shipments` | WAREHOUSE-OUTBOUND kaynağı |
| `HandlingUnit` (Package) | S2 | `wms_handling_units` | Koli; içerik satırları; ağırlık / ölçü elle |
| Outbox olayları | S1 (iç), S2 (`OutboundShipmentReady`) | servisin mevcut outbox'ı | Taşıma (bus) bugün kapalı → Pending (§8.9) |
| ScanEvent | — | — | **MOD-0182'ye bırakılır**; 0178 yalnız opak `scanRef` saklar |

**Komutlar (S1):** `CreatePutawayTaskCommand`, `UpdatePutawayTaskCommand`, `BulkAssignPutawayTaskCommand`,
`ClaimPutawayTaskCommand`, `ConfirmPutawayTaskCommand`, `RetryPutawayConfirmationCommand`, `ClosePutawayTaskShortCommand`,
`CancelPutawayTaskCommand`, `CreateWarehouseLocationRoleCommand`, `UpdateWarehouseLocationRoleCommand`, `DeleteWarehouseLocationRoleCommand`,
`BulkDeleteWarehouseLocationRoleCommand`.

**Sorgular (S1):** `GetPutawayTaskListQuery`, `GetPutawayTaskByIdQuery`, `GetPutawayCandidateListQuery`,
`GetPutawayTaskEventListQuery`, `GetWarehouseLocationRoleListQuery`, `GetWarehouseLocationRoleByIdQuery`.

**API (S1, taban `/api/warehouse`, WMS taslağı §4):**
`GET/POST /putaway-tasks` · `GET/PUT /putaway-tasks/{taskId}` · `POST /putaway-tasks/bulk-assign` · `POST /putaway-tasks/{taskId}/claim` ·
`POST /putaway-tasks/{taskId}/confirmations` · `POST /putaway-tasks/{taskId}/confirmations/{seq}/retry` ·
`POST /putaway-tasks/{taskId}/close-short` · `POST /putaway-tasks/{taskId}/cancel` · `GET /putaway-tasks/{taskId}/events` ·
`GET /putaway-candidates` · `GET/POST /location-roles` · `GET/PUT/DELETE /location-roles/{roleId}` ·
`POST /location-roles/bulk-delete` · donmuş `GET /outbound-shipments`, `GET /outbound-shipments/{outboundId}` (S1'de boş liste /
404; S2 doldurur).

**Frontend rotaları (S1):** `/Warehouse/PutawayTasks`, `/Warehouse/LocationRoles`.

**İzinler:** §14.

## 4. Entity Fields

Tüm varlıklar `EntityBase`'den türer (Id, TenantId, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, IsDeleted, DeletedAt).
Her varlık `LegalEntityId` taşır (W-0173-02). `TenantId` / `LegalEntityId` sunucuda çözülür; gövde / sorguda gelirse 400.
Miktarlar SKU'nun **baz biriminde** decimal string (float yok, en çok 6 ondalık); farklı SKU / birim toplanmaz. Zamanlar UTC
(E-6).

### 4.1 `WarehouseTask` (S1 Putaway; S2 Pick aynı baş kayıt)
| Alan | Tip | Kural |
|---|---|---|
| `Number` | string | RCS-001 kodu `{ŞİRKET}-PUT-{000001}`; `{ŞİRKET}` = görev açılırken okunan MDM tüzel kişilik `Code`'u (M-2), numaraya **bir kez** yazılır, sonra değişmez; depo kodu numarada **yok** (listede ayrı sütun); OTOMATİK, değiştirilemez; geçici sayaçtan (Q-07 B); önek kaydı FU-0178-RCS-02 |
| `CompanyCodePrefix` | string `^[A-Z]{2,5}$` | numaraya yazılan önekin kopyası (sayaç anahtarı); sonradan değişmez |
| `WarehouseCode` | string | LOCATION depo kodunun gösterim kopyası (liste sütunu) |
| `Kind` | enum `Putaway \| Pick` | S1 yalnız `Putaway` |
| `Status` | enum `Open \| InProgress \| Completed \| Cancelled` | §8.3 |
| `Origin` | enum `Manual \| Receipt \| Production \| Cancellation \| External` | S1: `Manual` (listeden ya da elle); diğerleri kapı (§8.2, W-3) |
| `SourceRef` | `{ sourceModule, sourceType, sourceDocumentId, sourceLineId, transactionId? }` \| null | ör. GRN'nin INVENTORY hareketi; opak |
| `SourceSystem`, `ExternalRef` | string ≤ 100 \| null | DEC-INV-19 / W-3 kapısı |
| `WarehouseId` | string (LOCATION Warehouse) | zorunlu |
| `ItemId`, `SkuId`, `SkuLevel` | uuid, uuid, enum | INVENTORY bakiye satırından; `Item` seviyesi S1'de kabul (putaway ürün ayrımı yapmaz) |
| `LotId`, `LotNumber` | string \| null | bakiye satırından; `LotNumber` gösterim kopyası |
| `SerialIds` | string[] \| null | seri kontrollü SKU'da zorunlu, adet = baz miktar (INVENTORY kuralı) |
| `StockStatus` | enum | kaynak kovanın durumu; S1'de yalnız `AVAILABLE` (§8.4) |
| `FromLocationId` | string | Receiving rolü altındaki stoklanabilir lokasyon |
| `ToLocationId` | string \| null | planlanan hedef bin; onayda zorunlu; saklama koşulu uyumu (§8.12) |
| `SuggestedToLocationId` | string \| null | sunucu önerisi (§8.6), yalnız bilgi; otomatik doldurmaz |
| `TargetSource` | enum `Manual \| SuggestionApplied` | hedef "Uygula" ile öneriden alındıysa `SuggestionApplied` → zaman çizelgesine `SuggestionApplied` olayı (M-4) |
| `PlannedQuantity` | decimal string | > 0; yaratmada açık miktarı aşamaz (§12) |
| `BaseUomId` | string | bakiye satırından |
| `ConfirmedQuantity` | decimal string | Posted onayların toplamı (görev içi türetme, stok bakiyesi değil); `Status` bundan türer (M-4) |
| `IntegrationFlag` (türetilmiş, saklanmaz) | enum `None \| PostingPending \| PostingRefused` | durumdan **ayrı** rozet: "Onay bekliyor" (Pending onay var) / "Stok reddetti" (son onay Refused) (M-4) |
| `Confirmations[]` | §4.2 | |
| `AssigneeUserId` | uuid \| null | atanan operatör; sorumlu atar (kullanıcı listesi ucu varsa, Q-13) ya da operatör atanmamış görevi **üstlenir** (kendine atar, M-5) |
| `Priority` | enum `Low \| Normal \| High` | varsayılan `Normal` |
| `Note` | string ≤ 1000 \| null | |
| `ShortCloseReason` | string 10–1000 \| null | `ClosePutawayTaskShort` |
| `CancelReason` | string 10–1000 \| null | |
| `Version` | long | If-Match `W/"<version>"` |
| `IdempotencyKey` | string 8–100 | yaratma tekilliği `(TenantId, LegalEntityId, IdempotencyKey)` |
| `CorrelationId` | uuid | ilk isteğin |

### 4.2 `TaskConfirmation` (gömülü)
| Alan | Kural |
|---|---|
| `Seq` | 1'den artan |
| `Quantity` | > 0, ≤ kalan (`Planned − Confirmed − Pending`) |
| `ToLocationId` | zorunlu; §12 hedef kuralları |
| `DeviationReason` | 10–1000; hedef planlanandan farklıysa zorunlu |
| `Status` | `Pending \| Posted \| Refused` |
| `PostingKey` | `wms:put:{taskId}:{seq}` — INVENTORY Idempotency-Key; yeniden denemede **aynı** anahtar |
| `TransactionId`, `TransactionNumber` | Posted'da INVENTORY'den |
| `ErrorCode`, `ErrorMessage`, `ErrorDetails` | Refused'da INVENTORY'nin cevabı aynen |
| `Channel` | `Ui \| Api \| External` (W-3 kapısı) |
| `ScanRef` | string ≤ 200 \| null — MOD-0182 kapısı, opak |
| `ConfirmedBy`, `ConfirmedAtUtc`, `Attempts`, `LastAttemptAtUtc` | |

### 4.3 `WarehouseTaskEvent` (insert-only)
`TaskId, Seq, Kind (Created | Updated | Assigned | Claimed | SuggestionApplied | ConfirmationRecorded | ConfirmationPosted |
ConfirmationRefused | ConfirmationRetried | ClosedShort | Completed | Cancelled), Actor { userId, name? } | system, AtUtc, Data { … }, CorrelationId`.

### 4.4 `WarehouseLocationRole`
| Alan | Kural |
|---|---|
| `WarehouseId` | LOCATION Warehouse; düğümün yolunda olmalı (`getLocationPath`) |
| `LocationId` | LOCATION düğümü, tip `StorageLocation \| Zone \| Bin`, `Active`; kayıt düğümün **stoklanabilir alt ağacına** uygulanır |
| `Roles` | küme, 0..n: `Receiving` (S1), `Staging` (S2); S2'de belki `Packing`. Bir düğüm birden çok role sahip olabilir; **Receiving + Staging birlikte olamaz** (M-5) |
| `AcceptedStorageConditions` | küme, 0..n, PRODUCT-MASTER v1.1 `StorageConditionCode` kodları (`AMBIENT_15_25, COOL_2_8, FROZEN_BELOW_MINUS_20, PROTECT_FROM_LIGHT, PROTECT_FROM_MOISTURE` — `product-master-bundle.openapi.yaml:601-604`); en çok bir sıcaklık kodu (PRODUCT-MASTER kuralının aynısı, `:681-682`); alt ağaca kalıtım (§8.12) |
| `Note` | ≤ 500 \| null |
| `Version` | If-Match |
| Kural | `Roles` ve `AcceptedStorageConditions`'tan en az biri dolu; açık görevi olan düğümden `Receiving` kaldırılamaz / kayıt silinemez (409 `WMS_LOCATION_HAS_OPEN_TASKS`, M-5) |
| Tekillik | `(TenantId, LegalEntityId, LocationId)` canlı kayıtlarda tekil — düğüm başına tek kayıt |

**Tasarım kararı (M-3) — neden ayrı bir koşul nesnesi değil, aynı kayıtta küme:** saklama koşulu düğümün fiziksel özelliğidir, WMS
rolüne bağlı değildir; raf bölgelerinin çoğunun rolü yoktur (Receiving / Staging değildir) ama koşul taşır. Rol başına kayıt
tutulsaydı aynı düğümün iki rol kaydı çelişen koşul kümeleri taşıyabilirdi. Düğüm başına tek kayıt (roller + koşullar) bu çelişkiyi
yapısal olarak engeller, alt ağaç kalıtımını tek okumaya indirir ve ikinci ekran / ikinci izin seti açmaz (ekran, rota ve izinler
aynı kalır: `WarehouseLocationRoles`, `/Warehouse/LocationRoles`, `warehouse.location-roles.*`).

### 4.5 S2 varlıkları (iskelet — S2 revizyonunda kesinleşir)
- `OutboundShipment`: `OutboundId` (uuid, sunucu), `OutboundNumber` (RCS `{ŞİRKET}-OUT-…`), `WarehouseId`, `OrderRef`, `ShipTo`
  (Address, KVKK: kişisel veri olabilir — loglanmaz), `Status` (WAREHOUSE-OUTBOUND enum), `Lines[]` (item / SKU / level /
  quantity / uom / orderLineId + tahsis: lot, kaynak bin, rezervasyon id, FEFO sapma gerekçesi), `Packages[]` ref, `ReadyAtUtc`,
  `ShipmentRef { shipmentId, shipmentNumber, dispatchedAtUtc }`, `CancelReason`, `Version`.
- `HandlingUnit`: `PackageId` (RCS `{ŞİRKET}-PKG-…`), `OutboundId`, `Contents[] { lineRef, lotId, quantity }`, `WeightKg`,
  `DimensionsCm` ("LxWxH"), `Status (Open | Closed)`, `Version`.
- Pick görevi = `WarehouseTask` (`Kind = Pick`, `FromLocationId` = kaynak bin, `ToLocationId` = Staging, `ReservationId`,
  `FefoRank`, `FefoBypassReason`).

### 4.6 Indexes (kiracı + şirket önce)
- `wms_tasks`: tekil `(TenantId, LegalEntityId, Number)`; tekil `(TenantId, LegalEntityId, IdempotencyKey)`;
  `(TenantId, LegalEntityId, Kind, Status, WarehouseId)`; `(TenantId, LegalEntityId, FromLocationId, SkuId, LotId, Status)`
  (açık miktar hesabı); `(TenantId, LegalEntityId, AssigneeUserId, Status)`; `(TenantId, Confirmations.PostingKey)` (kurtarma).
- `wms_task_events`: `(TenantId, TaskId, Seq)` tekil.
- `wms_location_roles`: tekil kısmi `(TenantId, LegalEntityId, LocationId)` where `IsDeleted = false`;
  `(TenantId, LegalEntityId, WarehouseId, Roles)`.
- `wms_number_counters`: tekil `(TenantId, CompanyCodePrefix, TypePrefix)` (geçici RCS sayacı, M-2 / Q-07 B).
- Test DB adı DB-010 (`_test` / `_tests` / `_itest` parçası); paket testleri kendi replika setini açar (`INVENTORY_TEST_MONGO`,
  27017 reddedilir — MOD-0173 FU-0173-TEST-01).

## 5. Repo Scope

Yalnız yeni dosyalar (seam'ler aşağıda tek tek):
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/**` (varlıklar, enum'lar, saf kurallar:
  `PutawayTaskRules`, `LocationRoleRules`)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/PutawayTasks/**`,
  `…/Features/WarehouseLocationRoles/**`, `…/Features/Warehouse/{Ports,Services}/**` (`WarehouseMovementPoster`,
  `IWarehouseRecordCodeAllocator`, okuma portları) ve `…/Features/Warehouse/WarehouseErrorCodes.cs` (`WMS_*` kodları — WMS-BUNDLE'da tanımlı, Q-06; `InventoryErrorCodes.cs` değişmez)
- `services/Diten.InventoryService/src/Diten.InventoryService.Persistence/Warehouse/**` (depolar, index kurucu, Pending kurtarma
  hosted service)
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/Controllers/Warehouse/**`,
  `…/Api/Warehouse/**` (DI uzantısı, `PutawayPickingPackingManifestProvider`)
- `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Authorization/WarehousePermissions.cs`
- `services/Diten.InventoryService/tests/**/Warehouse/**`
- `frontend/Diten.Web/Controllers/PutawayTasksController.cs`, `frontend/Diten.Web/Controllers/WarehouseLocationRolesController.cs`,
  `frontend/Diten.Web/Views/Warehouse/{PutawayTasks,WarehouseLocationRoles}/**`,
  `frontend/Diten.Web/wwwroot/assets/js/Warehouse/{PutawayTasks,WarehouseLocationRoles}/**`,
  `frontend/Diten.Web/Resources/Views/Warehouse/**` (7 dil)
- `docs/analysis/contracts/wms-bundle.openapi.yaml` (yalnız CT kabulünden sonra, tek yazıcı adımı) + `docs/analysis/contracts/README.md` satırı
- `docs/records/audits/2026-1x/mod-0178-*` (kanıt), bu paketin §19 Implementation Notes bölümü

**Seam'ler (var olan dosyalar, en az değişiklik; satırlar `47a2c1cb9`'da okundu):**
| Dosya | Satır | Değişiklik | Dilim |
|---|---|---|---|
| `services/Diten.InventoryService/src/Diten.InventoryService.Api/Program.cs` | manifest kayıtları `:45-56` | `PutawayPickingPackingManifestProvider` kaydı + `AddWarehouse()` | S1 |
| `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Middleware/InventoryScopeResolutionMiddleware.cs` | `ScopedBasePaths` `:57-60` | `WarehouseContract.BasePath` (`/api/warehouse`) — 0176 / 0177 emsali | S1 |

`execution/registries/module-implementation-status.md` dev dilimince yazılmaz; rapor satırı önerir, CT kabulde yazar.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, `CLAUDE.md`, `.claude/rules/**`.
- `gateway/Diten.ApiGateway/**` (`ocelot.json` yalnız integration-agent, INT-WMS).
- Donmuş sözleşmeler: `docs/analysis/contracts/*.openapi.yaml` (WAREHOUSE-OUTBOUND dahil — 1.0.0 değişmez; eklemeli değişiklik
  yalnız CT kabullü tek yazıcı adımıyla), `shipment-bundle.openapi.yaml` ve MVP-6 semantik dosyaları (asla).
- MOD-0173…0177 kodu: defter (`InventoryLedgerRepository`, `InternalMovementPostingService` — yalnız çağrılır), rezervasyon,
  LOCATION, TRACE, QUARANTINE karar motoru, FEFO kuralları (`FefoRules.cs`, `ExpiryPostingPolicy.cs`), recall kapısı.
- `execution/portfolio/**` (DCP-009 notu — açık iş FU-0178-DCP-01), `execution/registries/**`,
  `execution/domains/supply-chain-execution/domain-config.md` (yalnız öneri).
- Başka servisler: `services/Diten.SupplyChainService/**` (MVP-6), `services/Diten.ProcurementService/**` (MVP-2),
  `services/Diten.MdmService/**`, `services/Diten.Platform/**`.
- Paylaşılan: `Diten.BuildingBlocks` / `Diten.Building.Blocks`, `frontend/Diten.Web/Views/Shared/_Layout*.cshtml`,
  `dt-defaults.js`, `diten-datatable.js`, `core.css`, `SharedResource`, kabuklar.
- `execution/domains/master-data-management/module-packs/MDM-057-*` (terk edilmiş; okunmaz, yazılmaz).

## 7. Dependencies

| Bağımlılık | Tür | Durum | Kullanım |
|---|---|---|---|
| MOD-0173 defter + `InternalMovementPostingService` | HARD (süreç içi) | canlı, INVENTORY 1.5.0 | `LOCATION_TRANSFER` (§8.3); bakiye okuma (aday listesi) |
| MOD-0173-FU02 LOCATION | HARD | canlı, LOCATION 1.1.0 | `getLocation`, `getLocationPath`, `searchLocations` (Bin, Active, isStockable) |
| MOD-0174 TRACE | HARD | canlı, TRACE 1.4.0 | lot durumu / numarası (`LotRef.status`, `trace.openapi.yaml:300`) |
| MOD-0175 QUARANTINE | okuma | canlı, 1.3.0 | held stoğun putaway dışı tutulması (§8.4) |
| MOD-0176 FEFO | S2 HARD | canlı, 1.3.0 | `getFefoOrder` (`fefo.openapi.yaml:104-122`; RECALLED dışlanır) |
| MOD-0177 RECALL | HARD (S1 + S2) | canlı, 1.9.0 | `IRecallLotStatusReader.UnderRecallAsync` (`…/Features/Recall/Ports/IRecallLotStatusReader.cs:9-11`) |
| **W-5 lot / lokasyon rezervasyonu** (INVENTORY eklemeli) | **S2 HARD ENGEL** | yok — MVP-1 CT ayrı WP | pick tahsisi; gereksinimler §8.10 |
| BL-501 merkezi denetim | HARD | InventoryService'te var: `AddCentralAudit<InventoryCentralAuditScope>(…, RequireEnlistment = true)` (`Program.cs:63`); defter izi `inventory-merkezi-iletim` yol b (`tests/architecture/audit-ledger/Diten.InventoryService.md:12`) | §21 |
| BL-565 RCS-001 ortak kod servisi | geçici yol ile aşıldı (Q-07 B) | yok (`docs/roadmap/backlog/product-backlog.md` BL-565 AÇIK); geçici sayaç + feragat FU-0178-RCS-01 | §22 |
| MDM tüzel kişilik `Code` | HARD (numara öneki, M-2) | var: `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:11` (`public string Code`); InventoryService'in hangi MDM okumasında (`lookup-validation` ya da tüzel kişilik okuması) döndüğü **geliştiricinin ilk ölçümü** | erişilemezse 503 `LEGAL_ENTITY_MASTER_UNAVAILABLE` |
| PRODUCT-MASTER v1.1 `getSku.storageConditions` | **HARD (S1 saklama koşulu uyumu, M-3)** | sözleşme `1.1.0-draft.16`, **DRAFT** (`product-master-bundle.openapi.yaml:19-20`); filo bugün 4010 köprüsünde; canlı bağ PM-LIVE (G1-D2) | alan yoksa / bilinmiyorsa fail-closed (§8.12) |
| W-0173-02 şirket seçici | HARD | canlı | `X-Legal-Entity-Id` MDM ile doğrulanır |
| Gateway `/api/warehouse` | INT-WMS | yok (`ocelot.json`'da `/api/warehouse` 0 eşleşme) | integration-agent |
| MVP-2 GRN gerçek hareketi | S1 değil | MOCK (plan §1, Dalga 1) | S1 e2e test hareketiyle koşar; GRN tetikleyicisi açık soru Q-M2-1 |
| MVP-6 dispatch sinyali | S2 | olay taşıma kapalı (plan §3 Dalga 3) | Q-M6-4 |
| MDM P6 / P9 | **bağımlılık DEĞİL** | MDM'de yok | barkod MOD-0182'nin; P9 gelene kadar koli elle (karar Q-MDM-1), geçiş FU-0178-P9 |
| Görev atama kullanıcı listesi | S1 yumuşak | ölçülmedi — geliştiricinin ilk ölçümü (Q-13) | yoksa atama alanı S1'de yok |

**XMC-001:** Bu paket başka modülde değişiklik istemez; S2'nin INVENTORY ihtiyaçları (W-5) sahibinin (MVP-1 CT) ayrı WP'sidir,
MVP-6 ihtiyaçları Natig'e sorudur (§19.5). İkisi de bu paketin dev diliminde yapılmaz.

## 8. Runtime Constraints

### 8.1 Servis ve kapsam
- Servis `Diten.InventoryService`, port **5067** (AGENTS.md §3 satır 80). Depo kodu ayrı bounded context bölümünde
  (`Domain/Warehouse`, `Features/PutawayTasks`, `Features/WarehouseLocationRoles`, `Features/Warehouse`), MVP-1 modüllerinin
  koduna yalnız §5 seam'lerinden değer.
- Taban yol `/api/warehouse` (WAREHOUSE-OUTBOUND `servers.url`).
- Kiracı token'dan; şirket W-0173-02 (claim varsa claim, yoksa `X-Legal-Entity-Id` + MDM doğrulaması). Başka şirket → 403
  `CROSS_LE_FORBIDDEN`; başka kiracı / bilinmeyen id → **404**. `tenantId` / `legalEntityId` gövde / sorguda → 400.
- **Correlation (E-1):** kimliği Gateway üretir; 0178 servis genel `CorrelationIdMiddleware`'i kullanır, kendi ara katmanını
  yazmaz; her süreç içi ve dış çağrıya iletir; olay yüklerinde UUID taşır. E-1 hedefi geçersize 400'dür; Inventory bugün
  G1-D3 = A gereği "kabul et ve değiştir" yapıyor ve 400'e dönüş ayrı küçük WP'dir (plan §2.1 E-1) — 0178 o WP neyse onu izler,
  ayrı davranış yazmaz (Q-10).
- **Gün sınırı (E-6):** her tarih UTC; "bugün" UTC günü.
- **Tel biçimi:** InventoryService'in ölçülmüş kablosu — başarı gövdesi sarmalanmamış şema (INVENTORY / FEFO / RECALL R-1
  emsali), hata INVENTORY `Error` şeması (`error { code, message, details, correlationId }`, `contractVersion: "v1"`).
  WAREHOUSE-OUTBOUND `Error`'u (`details` yok) bunun alt kümesidir.
- **Boş isteğe bağlı üye gönderilmez** (null yerine üye yok): MVP-6 okuyucusu OAS 3.1 `nullable`'ı JSON null kabul etmez
  (`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/SourceIntake/FrozenReadSchema.cs:8,26`,
  GAP-0183-03). Liste sonunda `nextCursor` yazılmaz.
- **Idempotency:** her yazma `Idempotency-Key` başlığı = gövde `idempotencyKey` (8–100). Aynı anahtar + aynı gövde → ilk
  sonuç (200); farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`.
- **Eşzamanlılık:** değişebilir kayıtlar (görev, rol) `If-Match: W/"<version>"`; eksik → 400, bayat → 409 `WMS_CONFLICT`.
- **Soft delete:** yalnız `WarehouseLocationRole` silinebilir (`IsDeleted`, `DeletedAt`). Görevlerin silme yolu yoktur; iptal
  edilir (GxP iz). Silme alanları standart gereği vardır, görevlerde hep `false` (MOD-0173 emsali).
- **Fail-closed:** LOCATION / PRODUCT-MASTER / MDM şirket doğrulaması / RECALL okuması erişilemezse 503, yazma yok.

### 8.2 Görev kaynağı (S1)
- S1'de görev **elle** açılır: depo sorumlusu "Yerleştirilecek stok" listesinden bir satır seçer (ya da aynı listeyi seçicide
  arar) → form ön doldurulur. Liste = Receiving rolündeki lokasyonların alt ağacında, şirketin `AVAILABLE` ve `OnHand > 0`
  INVENTORY bakiye satırları; her satırda **açık miktar** = OnHand − aynı kovadaki açık görevlerin kalanı (okuma anında).
- **GRN → putaway tetikleyicisi tanımsızdır** (plan §4 "Boşluklar") → açık soru Q-M2-1 (Cihan). Seçenekler: (A) S1 gibi elle;
  (B) INVENTORY'nin süreç içi `IPostedMovementObserver`'ı (`…/Features/Inventory/Ports/IPostedMovementObserver.cs:6-10`,
  post-commit, ret edemez) ile Receiving lokasyonuna giren `GOODS_RECEIPT_PO` / `GOODS_RECEIPT_PROD` için otomatik `Origin =
  Receipt` görev — 0173 DI seam'ine dokunur; (C) MVP-2'nin GRN sonrası WMS API'sini çağırması. **Karar (Q-11, Beste 2026-10-21):**
  S1 = A (elle / listeden); otomatik tetikleyici (B / C / D) Cihan'ın cevabına kadar açık soru (Q-M2-1).

### 8.3 Durum makinesi ve onay algoritması (S1 Putaway)

```
Open ──(onay, kalan > 0)──> InProgress ──(onaylanan = planlanan)──> Completed
Open|InProgress ──(close-short: en az bir Posted onay + gerekçe)──> Completed (ShortCloseReason)
Open|InProgress ──(cancel: hiç Posted onay yok + Pending yok + gerekçe)──> Cancelled
```
- `Completed` / `Cancelled` terminaldir. İptal edilen görevin Posted hareketleri geri alınmaz (geri alma INVENTORY `REVERSAL`'dır,
  0178'in değil; stok yeni lokasyonda kalır). **Onay yoksa yalnız İptal, Posted onay varsa yalnız Kalanı kapat** (M-4): Posted onaylı
  görevin iptali 409 `WMS_INVALID_TRANSITION`. **Gönderilen onay cevap gelene kadar görevi kilitler** (M-4): Pending onay varken yeni
  onay, düzenleme, üstlenme, kısa kapatma ve iptal 409 `WMS_CONFIRMATION_PENDING`; yalnız `retry` çalışır.
- **Onay (`ConfirmPutawayTaskCommand`):**
  1. Doğrula: görev `Open|InProgress`, Pending onay yok, If-Match, onaylayan = atanan ya da sorumlu (§8.13), miktar ≤ kalan
     (planlanandan fazla onay yasak, M-5), hedef kuralları (§12) ve saklama koşulu uyumu (§8.12), lot recall değil (§8.5), lot
     durumu. Açık görevin stoğu sonradan karantinaya / recall'a girerse **otomatik askı yoktur**; kontrol onay anında yapılır ve ret
     verilir (M-5).
  2. İşlem 1 (Mongo transaction, merkezi denetim kaydı listeye alınır — `RequireEnlistment`): `Confirmations[seq] = Pending`,
     `PostingKey = wms:put:{taskId}:{seq}`, `Version++`, olay `ConfirmationRecorded`.
  3. `WarehouseMovementPoster` → `InternalMovementPostingService`: `LOCATION_TRANSFER`, `fromWarehouseId = toWarehouseId =
     WarehouseId`, `fromLocationId = FromLocationId`, `toLocationId`, `fromStockStatus = toStockStatus = StockStatus` (tek adım,
     durum korunur — `MovementDirectionRules.cs:82-94`), `lotId`, `serialIds`, `quantity` + `BaseUomId`, `sourceModule = MOD-0178`,
     `sourceType = PUTAWAY_TASK`, `sourceDocumentId = taskId`, `sourceLineId = seq`, `documentGroupId = Number`,
     `idempotencyKey = PostingKey`, `transactionDate = postingDate = sunucu UTC şimdi`, aktör = onaylayan kullanıcı.
  4. İşlem 2: Posted → `TransactionId/Number`, `ConfirmedQuantity +=`, durum (InProgress / Completed), olay; ya da Refused →
     INVENTORY kodu aynen, görev miktarı değişmez, olay. Cevap: Posted 201; Refused 422 INVENTORY kodu ve `details` ile.
  5. INVENTORY 503 / zaman aşımı / süreç çöküşü → onay `Pending` kalır; cevap 503. **Kurtarma:** `RetryPutawayConfirmationCommand`
     (ekranda "Yeniden dene" + hosted sweeper, 2 dakikadan eski Pending'ler 60 sn'de bir — 0177 KR-B emsali; karar Q-09) **aynı
     PostingKey** ile tekrar dener; defter anahtarı önceden işlediyse kayıtlı sonucu döner (çift hareket yok —
     `QuarantineMovementPoster.cs:34-39` deseni).
- **Kısmi yerleştirme:** bir görev birden çok onayla (farklı hedef binlere de) tamamlanabilir; her onay ayrı hareket.

### 8.4 Karantinadaki ve tutulan stok (S1 kuralı)
- MOD-0175 tutmaları (hold) kovaya bağlıdır: `WarehouseId`, `LocationId` (MOD-0175 pack `:173`, karar V7 "current balance of the
  bucket ≥ decision quantity", `:760`). Tutulan stoğu başka lokasyona taşımak karar uygulamasını `QUARANTINE_STOCK_CHANGED` /
  `ApplyFailed`'a düşürür.
- **S1 kuralı (karar Q-02, Beste 2026-10-21):** putaway görevi yalnız `AVAILABLE` kovadan açılır. `QUARANTINE`, `QUALITY_INSPECTION`, `BLOCKED`,
  `DAMAGED`, `EXPIRED` kovalar adaylarda "yerleştirilemez — kalite kararı bekliyor" olarak görünür, görev açılırsa 422
  `WMS_STOCK_NOT_PUTAWAY_ELIGIBLE` (`details.stockStatus`) ve 7 dilde yerelleştirilmiş mesaj (`Warehouse.Error.WMS_STOCK_NOT_PUTAWAY_ELIGIBLE`). E-4 gereği GRN `toStockStatus = QUARANTINE` yazar; MOD-0175 serbest bıraktığında stok aynı
  lokasyonda `AVAILABLE` olur ve aday listesine girer. Karantina bölgesine fiziksel taşıma ihtiyacı → QUARANTINE eklemeli
  "tutmanın lokasyonu taşınır" değişikliği (sahibi MOD-0175) ya da S2 sonrası FU-0178-QH-01.
- **Onay anında aynı kural (tur 2):** görev açıldıktan sonra kaynak kova karantinaya / kalite kontrole / blokeye geçerse onay 422
  `WMS_STOCK_NOT_PUTAWAY_ELIGIBLE` (`details.stockStatus`) ile reddedilir; ekrandaki "Stok karantinaya girdi" ret durumu **bu koddur**
  (onay `Refused` değil, istek öncesi ret — hareket denenmez). Otomatik askı yok (M-5).
- Lot durumu `Blocked` / `Expired` → INVENTORY `LOT_BLOCKED` / `LOT_EXPIRED` (CT-D4: yalnız SCRAP / WRITE_OFF / REVERSAL); 0178
  adaylarda işaretler, görev açılmasını 422 aynı kodla reddeder (TRACE okuması), onayda defter yine son sözü söyler.
- Retest süresi geçmiş lot: INVENTORY "transfer"i reddeder (`inventory-bundle.openapi.yaml:16`, MOD-0175 pack `:546`); bunun
  `LOCATION_TRANSFER`'ı kapsayıp kapsamadığı ölçülmedi → **geliştiricinin ilk ölçümü (Q-12)**. Varsayılan davranış: 0178 kendi retest
  ya da Draft-lot kuralı yazmaz; defter reddederse kod aynen iletilir, kabul ederse 0178 engellemez (ASSUMPTION-0178-04).

### 8.5 Geri çağırma (recall) — 0178'in tek ek kuralı
INVENTORY 1.4.0 `LOT_UNDER_RECALL`'u yalnız `GOODS_ISSUE_SALES`, `GOODS_ISSUE_PROD`, `WAREHOUSE_TRANSFER` için uygular;
`LOCATION_TRANSFER` muaftır (`inventory-bundle.openapi.yaml:43-46`). Bu yüzden:
- **S1 (karar Q-03, Beste 2026-10-21):** geri çağırmadaki lotun putaway görevi açılmaz / onaylanmaz → 422 `LOT_UNDER_RECALL` (INVENTORY'nin kod
  adı ve `details` şekli: `lotId, lotNumber, recallCaseId?, recallCaseNumber, movementType: LOCATION_TRANSFER`). Okuma:
  `IRecallLotStatusReader.UnderRecallAsync` (süreç içi, kiracı geneli CT-R7). Gerekçe: recall'daki stok rafa değil ayrılmış
  alana gider (MOD-0177 KR-B karantina isteği).
- **S2 (aynı karar):** pick öncesi aynı okuma; ret `LOT_UNDER_RECALL`. Ek güvence: MVP-6'nın `GOODS_ISSUE_SALES`'i INVENTORY'de yine reddedilir.
- Okuma erişilemezse 503 (fail-closed), yazma yok.

### 8.6 Hedef bin önerisi (S1)
Strateji master'ı yok (kapasite, slotting FU-0178-SLOT-01). Öneri = aynı depoda aynı SKU'nun (varsa aynı lotun) en son stoklandığı,
Receiving / Staging rolü olmayan bin (INVENTORY bakiye okuması); yoksa öneri yok. Kullanıcı hedefi LOCATION aramasından seçer
(`searchLocations type=Bin, status=Active, isStockable=true, parentId` deponun altı). Kapasite kontrolü **yok** (veri yok) —
ekranda yazılır, kabul ölçütü değildir. Öneri yalnız saklama koşulu uyumlu binlerden seçilir (§8.12). **Öneri formu otomatik
doldurmaz**; kullanıcı "Uygula" der, hedef alanı dolar ve kayıtta `TargetSource = SuggestionApplied` + olay `SuggestionApplied`
zaman çizelgesine yazılır (M-4).

### 8.7 Kayıt kodları
§22. Ortak servis (BL-565) yok → karar Q-07 B: RCS biçiminde geçici sayaç, port arkasında; geçiş + feragat FU-0178-RCS-01; önek
kaydı FU-0178-RCS-02. **Şirket öneki (M-2, Q-MDM-2 kapandı):** şimdilik MDM tüzel kişiliğin mevcut `Code` alanı
(`LegalEntity.cs:11`). Görev açılırken okunur; `^[A-Z]{2,5}$`'e uymuyorsa görev **açılmaz** → 422 `WMS_COMPANY_CODE_INVALID`
(`details.companyCode`) + 7 dilde "şirket kodunu düzeltin (2–5 büyük harf A–Z)" mesajı; MDM erişilemezse 503. Önek numaraya **bir
kez** yazılır, sonra değişmez (şirket kodu sonradan değişse de). Sayaç (kiracı, şirket öneki, tür) başına (RCS-001 §5.1). Depo kodu
numarada yer almaz; listede ayrı sütundur. RCS-001 §5.4 kısa kod alanı gelince geçiş FU-0178-RCS-01'dedir. **Şirket kodu geçersizken
mevcut görevler görünür ve onaylanabilir** (önekleri numaralarına yazılı); yalnız **yeni görev açılamaz** (tur 2).

### 8.8 Merkezi denetim (E-2, BL-501)
Her yazma komutu `ICentralAuditDeclaration` ile işaretlenir; olay servisin `central_audit_outbox`'ına komutun kendi Mongo
transaction'ında yazılır (`RequireEnlistment = true`, `Program.cs:63`; rehber `docs/guides/operations/central-audit-connect.md`).
Görev olayları (`wms_task_events`) ürün içi zaman çizelgesidir, merkezi kaydın yerine geçmez. INVENTORY hareketinin kendi denetim
kaydı ayrıca yazılır (defterin işi).

### 8.9 Olaylar ve taşıma
- S1: iç olaylar yok (görev olayları koleksiyonda). S2: `OutboundShipmentReady` (WAREHOUSE-OUTBOUND şeması) servisin outbox'ına
  yazılır; olay taşıma bugün kapalı / kayıtsız (plan §3 Dalga 3) → Pending kalır; MVP-6 için donmuş alternatif **poll**'dur
  (`GET /outbound-shipments?status=ReadyToShip`).
- Olay zarfına `legalEntityId` / `tenantId` eklenmesi GAP-0183-02'yi kapatır → WMS taslağı §7, Q-M6-5.

### 8.10 W-5 için gereksinimler (S2 engeli; sahibi MVP-1 CT — bu paket yazmaz)
1. Rezervasyon **lot** düzeyinde (gerekirse depo + lokasyon) — bugünkü `ReservationRequest` yalnız SKU (`inventory-bundle.openapi.yaml:459-474`, ASSUMPTION-0173-12).
2. **Rezervasyon kimliğiyle tüketim:** MVP-6'nın `GOODS_ISSUE_SALES`'i `sourceModule = MOD-0183` taşır; bugünkü eşleme
   `(sourceModule, sourceDocumentId, sourceLineId)` (ASSUMPTION-0173-15) 0178'in rezervasyonunu tüketmez → "ayrılmış stok başkasına
   çıkamaz" kuralı (ASSUMPTION-0173-14) MVP-6'nın çıkışını `INSUFFICIENT_STOCK` ile reddedebilir. Öneri: `postMovement`'a eklemeli
   isteğe bağlı `reservationId` (SHIPMENT satırındaki `inventoryReferenceId` zaten rezervasyon kimliği örneği taşır,
   `shipment-bundle.openapi.yaml` `inventoryReferenceId: rsv-4001`).
3. Ayrılmış miktarın sahibi tarafından **`LOCATION_TRANSFER`'ı** (bin → staging) rezervasyonu düşürmeden yapılabilmeli (lot
   düzeyinde rezervasyon bunu doğal karşılar).
4. FEFO kancası (0176) MVP-6 çıkışında "ilk uygun lot"u değerlendirirken başka outbound'lara ayrılmış lotları nasıl sayacak?
   (sapma gerekçesi taşıma: §11 WMS taslağı `fefoBypassReason`) — 0176 + W-5 ortak kararı.

### 8.11 OTel (Blueprint WMS-BUNDLE)
Servisin OTel kablolaması bu oturumda ölçülmedi. Öneri adlar: `ActivitySource "Diten.Warehouse"`, span'ler
`wms.putaway.confirm`, `wms.posting`; sayaçlar `wms_task_confirmations_total{result}`, `wms_posting_refused_total{code}`,
`wms_pending_confirmations` (gauge), `wms_outbound_ready_total`. Exporter yoksa sayaçlar kodda durur, canlı ölçüm iddia edilmez
(MOD-0183 O-3 emsali). Q-EA-5.

### 8.12 Saklama koşulu uyumu (M-3 — S1 engeli)
- **Ürün tarafı:** PRODUCT-MASTER v1.1 `getSku.storageConditions` (çoklu, `StorageConditionCode`; boş liste = koşul yok;
  `product-master-bundle.openapi.yaml:601-604, 703, 750`). Alan **yoksa** (bilinmiyor — taslak kayıt, v1.0 cevabı) "koşul yok"
  sayılmaz: 422 `WMS_STORAGE_CONDITIONS_UNKNOWN` (bilinmeyen boş okunmaz — MOD-0173 PM1 bayrak kuralının aynısı); PRODUCT-MASTER
  erişilemezse 503 `PRODUCT_MASTER_UNAVAILABLE`.
- **Lokasyon tarafı:** hedef binin **etkin kabul kümesi** = `getLocationPath` üzerinde bine en yakın (kendisi dahil) ve
  `AcceptedStorageConditions`'ı dolu `WarehouseLocationRole` kaydının kümesi (alt ağaç kalıtımı).
- **Kural:** ürünün her koşul kodu etkin kümede olmalı (ürün kümesi ⊆ etkin küme). Sözleşmenin açık enum'u büyürse ürünün bilinmeyen
  kodu da kümede aranır; yoksa ret.
- **Etiketsiz lokasyon (karar, açık soru değil):** etkin kümesi olmayan rafa **yalnız koşulu olmayan ürün ya da yalnız `AMBIENT_15_25`
  koşullu ürün** konur (ürün kümesi ⊆ `{AMBIENT_15_25}`); diğer her ürün (ör. `COOL_2_8`, `PROTECT_FROM_LIGHT`) reddedilir.
- **Ret:** görev oluşturma, düzenleme (hedef değişince) ve onay → 422 `WMS_STORAGE_CONDITION_MISMATCH` (`details: productConditions,
  locationConditions, inheritedFromLocationId?`) + 7 dilde mesaj; ekranda rozet. Etiketler ya da ürün koşulları görev açıkken
  değişirse kontrol onayda yeniden yapılır (otomatik askı yok).
- Etiket seçici seçenekleri sözleşmedeki beş koddur (yerelleştirilmiş ad); MOD-0048 `storage-condition` listesi okunabilir hâle
  gelirse ona geçiş FU-0178-STOR-01 (Q-MDM-3).

### 8.13 Onay yetkisi ve üstlenme (M-5)
- Onayı **atanan operatör** ya da **sorumlu** (`warehouse.putaway-tasks.update` sahibi) verir; diğerleri 403 `WMS_NOT_ASSIGNEE`.
- Operatör **atanmamış** görevi üstlenebilir: `ClaimPutawayTaskCommand` = kendine ata; ayrı izin yok, **`warehouse.putaway-tasks.confirm`
  yeter** (öneri, sahip yönlendirmesi "önerdiğin gibi"). Atanmış görevi üstlenme 409 `WMS_TASK_ALREADY_ASSIGNED`; yeniden atama
  sorumlunun işidir. Kullanıcı listesi ucu yoksa (Q-13) atama yalnız üstlenmeyle olur.
- Üstlenme **seçili depodaki atanmamış görevler** içindir ("Üstlen" listesi depo filtresine bağlı); sorumlu atanmış görevi yeniden
  atayabilir (`UpdatePutawayTask` / `BulkAssignPutawayTask`, `warehouse.putaway-tasks.update`) (tur 2).
- **Pending yeniden deneme:** deneme sınırı yok (aynı `PostingKey` çift hareket üretmez); `Attempts` ve son deneme zamanı ekranda
  görünür; INVENTORY reddederse onay `Refused` olur ve "Stok reddetti" rozeti çıkar (tur 2).

### 8.14 E-imza — kapsam dışı (M-1)
Putaway onayı **e-imza istemez** ve MOD-0022'ye imzalanabilir eylem kaydetmez. Gerekçe: onay imzalı bir kalite kararı değil, bir
**hareket kaydıdır**; kim / ne / ne zaman merkezi denetim izinde (yol b, K2, §21) ve INVENTORY hareketinin kendi kaydında durur.
Kalite kararları (serbest bırakma, red) MOD-0175'te imzalıdır; 0178 onlara dokunmaz.

### 8.15 Ekran kuralları (M-4, S1)
- Gönderilen onay cevap gelene kadar görevi **kilitler** (yeni onay / düzenleme / üstlenme / kapama / iptal kapalı; sunucu 409
  `WMS_CONFIRMATION_PENDING`).
- **Stok satırı oluşturulunca değişmez** (`UpdatePutawayTask` kaynak kovayı kabul etmez → 400); yanlış satır = iptal + yeni görev.
  Planlanan miktar onaylanan + bekleyenin altına inemez (422 `WMS_QUANTITY_BELOW_CONFIRMED`).
- **Seçilemeyen stok gizlenmez:** adaylarda kilit simgesi + neden + engelleyen belge numarası (karantina tutma no / recall vaka no /
  bloke kaydı no / lot durumu) gösterilir; numaralar sahibi modülün süreç içi okumasından (MOD-0175 tutma okuması, RECALL
  `getLotRecallStatus.cases[]`, TRACE lot durumu).
- **Raf önerisi** "Uygula" ile alınır, otomatik doldurmaz; uygulandıysa zaman çizelgesine yazılır (§8.6).
- **Menü:** onay yoksa yalnız "İptal", Posted onay varsa yalnız "Kalanı kapat" etkin; pasif menü öğesi nedenini yazar (tooltip /
  ikincil metin, 7 dil).
- **Raf alanı** klavye ve barkod okuyucu (klavye öykünmeli) girişini kabul eder: kod yazılır / okutulur → LOCATION `searchLocations`
  tam kod eşleşmesi → bin seçilir; eşleşme yoksa alan hatası. Okuyucu olay akışı (ScanEvent) MOD-0182'de sonra.
- **Mobil 375 px:** dokunma alanları ≥ 48 px, miktar alanında −/+ düğmeleri, sabit alt eylem çubuğu (Onayla / Yeniden dene).
- **Durum ile entegrasyon bayrağı ayrıdır:** durum (Open / InProgress / Completed / Cancelled) onaylanan miktardan türer; ayrı
  rozetler "Onay bekliyor" (`PostingPending`) ve "Stok reddetti" (`PostingRefused`).
- Diller en, tr, fr, es, zh, ar, ru; **RTL yalnız ar**.

## 9. Layout & Shell Contract

- `shell: tenant` → tüm `Views/Warehouse/PutawayTasks/*.cshtml` ve `Views/Warehouse/WarehouseLocationRoles/*.cshtml` sayfalarında
  Razor bloğunda **`Layout = "_LayoutTenantShell";` AÇIKÇA** yazılır (`_ViewStart.cshtml` varsayılanına güvenilmez; Golden Slim
  `Views/DevEnablement/GoldenReferenceSlim/Index.cshtml:8` emsali).
- **`/Platform/` rotası yok** (rota PermissionScope'u türetir — self-registration §2c); manifest rota testi.
- **Golden Reference kararı (alan sayımı):**
  | Ekran | Form | Kullanıcı alanları | Sayı | Karar |
  |---|---|---|---|---|
  | `PutawayTasks` — oluştur / düzenle (offcanvas) | kaynak stok satırı (seçici), miktar, hedef bin, atanan, öncelik, not (atanan Q-13 ölçümüne bağlı → 5) | 6 | ≤ 8 | **slim** |
  | `PutawayTasks` — onay (offcanvas eylemi) | miktar, gerçek hedef bin, sapma gerekçesi | 3 | ≤ 8 | slim içi eylem paneli |
  | `WarehouseLocationRoles` — oluştur / düzenle | lokasyon, roller (çoklu), kabul edilen saklama koşulları (çoklu), not | 4 | ≤ 8 | **slim** |
  | S2 `OutboundShipments` — oluştur | depo, sipariş ref., alıcı adı, ülke, şehir, posta kodu, adres 1, adres 2 + satırlar (ürün, miktar, birim, sipariş satırı) | ≥ 12 | > 8 | **compact** (S2 revizyonu) |
  Sayılmayanlar: Id, TenantId, şirket, denetim alanları, sayfa / onay kolonları; kaynak satırdan gelen salt okunur alanlar
  (ürün, lot, kaynak lokasyon, durum). Frontmatter `golden_reference: slim` + `form_field_count: 6` S1 ekranlarını anlatır.
- UAS-001: sayfa anahtarı olmayan kullanıcıya iskelet çizilmez (başlık, kart, boş tablo, buton yok), yönlendirme yok; tek açıklama.
- 375 px: operatör tablet / telefonla onaylar → onay paneli 375 px'te ölçülür (S1 kabulü).

## 10. Backend File Convention

Golden Reference Slim birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`):

```text
Application/Features/PutawayTasks/
├── Commands/        CreatePutawayTaskCommand.cs · UpdatePutawayTaskCommand.cs · BulkAssignPutawayTaskCommand.cs
│                    ClaimPutawayTaskCommand.cs · ConfirmPutawayTaskCommand.cs · RetryPutawayConfirmationCommand.cs ·
│                    ClosePutawayTaskShortCommand.cs
│                    CancelPutawayTaskCommand.cs                                   (sealed record)
├── Queries/         GetPutawayTaskListQuery.cs · GetPutawayTaskByIdQuery.cs · GetPutawayCandidateListQuery.cs
│                    GetPutawayTaskEventListQuery.cs                               (sealed record)
├── Handlers/
│   ├── CommandHandlers/   CreatePutawayTaskHandler.cs · UpdatePutawayTaskHandler.cs · BulkAssignPutawayTaskHandler.cs
│   │                      ClaimPutawayTaskHandler.cs · ConfirmPutawayTaskHandler.cs · RetryPutawayConfirmationHandler.cs
│   │                      ClosePutawayTaskShortHandler.cs · CancelPutawayTaskHandler.cs   (sealed class, suffix YOK)
│   └── QueryHandlers/     GetPutawayTaskListHandler.cs · GetPutawayTaskByIdHandler.cs · GetPutawayCandidateListHandler.cs
│                          GetPutawayTaskEventListHandler.cs
├── Validators/      CreatePutawayTaskValidator.cs · UpdatePutawayTaskValidator.cs · ConfirmPutawayTaskValidator.cs
│                    ClosePutawayTaskShortValidator.cs · CancelPutawayTaskValidator.cs · GetPutawayTaskListValidator.cs
└── PutawayTaskModels.cs                                                            (tek dosyada DTO / ViewModel)

Application/Features/WarehouseLocationRoles/   (aynı iskelet: Create / Update / Delete / BulkDelete + GetList / GetById,
                                                WarehouseLocationRoleModels.cs)
Application/Features/Warehouse/Ports/          IWarehouseRecordCodeAllocator.cs · IWarehouseStockReader.cs · IWarehouseLotReader.cs
Application/Features/Warehouse/Services/       WarehouseMovementPoster.cs (tek stok kapısı → InternalMovementPostingService)
Domain/Warehouse/{Entities,Enums,Rules}/       WarehouseTask, TaskConfirmation, WarehouseTaskEvent, WarehouseLocationRole,
                                               PutawayTaskRules (geçişler, kalan), LocationRoleRules
Persistence/Warehouse/                          WarehouseTaskRepository, WarehouseLocationRoleRepository, WarehouseIndexInitializer,
                                               PendingConfirmationSweeperHostedService
Api/Controllers/Warehouse/                      PutawayTasksController.cs · WarehouseLocationRolesController.cs ·
                                               OutboundShipmentsController.cs (donmuş GET'ler; S1'de boş)
Api/Warehouse/                                  WarehouseServiceCollectionExtensions.cs · PutawayPickingPackingManifestProvider.cs
Infrastructure/Authorization/WarehousePermissions.cs
```

- Tek dosyada tek public tip; handler / validator adlarında `Command` / `Query` eki yok.
- **Golden'dan bilinçli sapmalar (onaylı — Q-05, Beste 2026-10-21):** görevlerde `Delete` / `BulkDelete` yoktur (GxP iz; iptal gerekçeli);
  yerine `Cancel` ve toplu işlem olarak `BulkAssign` (bulk action bar: "Ata"). `WarehouseLocationRoles` Golden'a birebir
  (Delete + BulkDelete, soft delete).
- Controller'lar HTTP durumunu handler sonucuna göre açıkça verir (`CustomBaseController`); `[HasPermission]` her eylemde.
- Domain Mongo / framework bağımsız; generic depo yok (Tenant / LE yüklemi düşmesin — MOD-0173 §10).

## 11. Frontend File Contract

### 11.1 Ekran → rota → izin (hepsi Scope = Tenant, `_LayoutTenantShell`)
| Ekran | PageCode | Rota | Tür | Sayfa izni | Nav |
|---|---|---|---|---|---|
| Yerleştirme Görevleri | `PUTAWAYTASKS` | `/Warehouse/PutawayTasks` | Slim DataTable (server) + oluştur / düzenle offcanvas + QuickView (onaylar, zaman çizelgesi) + onay paneli | `warehouse.putaway-tasks.read` | evet |
| Depo Lokasyon Rolleri | `WAREHOUSELOCATIONROLES` | `/Warehouse/LocationRoles` | Slim DataTable (server) + offcanvas | `warehouse.location-roles.read` | evet |
| S2 Outbound Sevkiyatlar | `OUTBOUNDSHIPMENTS` | `/Warehouse/OutboundShipments` (+ Create / Edit / Details) | Compact | `warehouse.outbound-shipments.read` | S2 |
| S2 Toplama Görevleri | `PICKTASKS` | `/Warehouse/PickTasks` | Slim | `warehouse.pick-tasks.read` | S2 |
| S2 Paketleme | `PACKINGSTATION` | `/Warehouse/Packing/{outboundId}` | detay + form (DataTable değil) | `warehouse.packages.update` | hayır (OUTBOUNDSHIPMENTS altı) |
| S3 İstisna Kutusu / Etkinlik İzi / Entegrasyon İzleme | `WMSEXCEPTIONS` / `WMSACTIVITY` / `WMSINTEGRATION` | `/Warehouse/Exceptions` … | Slim liste | §14 S3 | S3 |

### 11.2 Dosyalar (S1, Slim seti birebir)
```text
frontend/Diten.Web/Views/Warehouse/PutawayTasks/
├── Index.cshtml                  (Layout = "_LayoutTenantShell" AÇIKÇA; bölüm sırası Filter → BulkActionBar → DataTable → Offcanvas)
├── _Filter.cshtml                (durum, depo, atanan, öncelik, lot / ürün arama)
├── _DataTable.cshtml             (data-dt-standard="v2", data-dt-data-mode="server", skeleton)
├── _IndexL10n.cshtml
├── _CreateEditOffcanvas.cshtml   (6 alan; "Yerleştirilecek stok" seçicisi /api/warehouse/putaway-candidates)
├── _DetailsQuickView.cshtml      (görev başı, onaylar + INVENTORY hareket no, zaman çizelgesi, "Yeniden dene", onay paneli)
└── PutawayTasksIndex.cs          (marker)
frontend/Diten.Web/Views/Warehouse/WarehouseLocationRoles/   (aynı Slim seti, WarehouseLocationRolesIndex.cs)
frontend/Diten.Web/wwwroot/assets/js/Warehouse/{PutawayTasks,WarehouseLocationRoles}/index.js · index.l10n.js
frontend/Diten.Web/Resources/Views/Warehouse/{PutawayTasks,WarehouseLocationRoles}/{Module}Index.{en,tr,fr,es,zh,ar,ru}.resx
frontend/Diten.Web/Controllers/PutawayTasksController.cs · WarehouseLocationRolesController.cs   (aynı origin MVC proxy → Gateway 5000)
```
- Tarayıcı JS servis portuna (5067) gitmez; same-origin MVC proxy → Gateway.
- Seçiciler yalnız master'lardan: lokasyon LOCATION `searchLocations`, lot / ürün aday satırından; sabit yedek liste yok.
- Liste ekranı dokunma protokolü (BL-440): `verify_datatable_page.py . --area Warehouse --module {Module} --format gaps`.

### 11.3 Ekran → düğme → eylem → izin (manifest Actions)
| Ekran | Düğme | ActionCode | İzin |
|---|---|---|---|
| PutawayTasks | "Yeni görev" (Toolbar) | `PUTAWAY_TASK_CREATE` | `warehouse.putaway-tasks.create` |
| PutawayTasks | "Düzenle" (RowAction; Open/InProgress) | `PUTAWAY_TASK_UPDATE` | `warehouse.putaway-tasks.update` |
| PutawayTasks | "Ata" (bulk bar; yalnız Q-13 ölçümü kullanıcı listesi bulursa) | `PUTAWAY_TASK_ASSIGN` | `warehouse.putaway-tasks.update` |
| PutawayTasks | "Onayla" (RowAction + QuickView) | `PUTAWAY_TASK_CONFIRM` | `warehouse.putaway-tasks.confirm` |
| PutawayTasks | "Yeniden dene" (QuickView, Pending onay) | `PUTAWAY_TASK_RETRY` | `warehouse.putaway-tasks.confirm` |
| PutawayTasks | "Kalanı kapat" (RowAction, gerekçe) | `PUTAWAY_TASK_CLOSE_SHORT` | `warehouse.putaway-tasks.confirm` |
| PutawayTasks | "İptal" (RowAction, gerekçe, IsDangerous; yalnız onay yokken) | `PUTAWAY_TASK_CANCEL` | `warehouse.putaway-tasks.cancel` |
| PutawayTasks | "Üstlen" (RowAction; atanmamış görev) | `PUTAWAY_TASK_CLAIM` | `warehouse.putaway-tasks.confirm` |
| PutawayTasks | "Uygula" (raf önerisi, formda) | — (form içi; kayıtta `TargetSource`) | `warehouse.putaway-tasks.create` / `.update` |
| WarehouseLocationRoles | "Yeni rol" / "Düzenle" / "Sil" / toplu "Sil" | `LOCATION_ROLE_CREATE/UPDATE/DELETE/BULK_DELETE` | `warehouse.location-roles.manage` |
Düğme anahtarı yoksa gizlenir; sunucu yine 403 verir.

### 11.3a Ekran notları (tur 2, Beste 2026-10-21)
- **"Depolama" rolü yoktur.** `WarehouseLocationRoles` formunda saklama koşulu girmek için rol seçmek gerekmez: rolsüz, yalnız
  `AcceptedStorageConditions`'lı kayıt geçerlidir (§4.4 kuralı). Üst düğümün etiketi alt düğümlere geçer; listede ve hedef bin
  seçicisinde kalıtılan küme **"üst düğümden"** rozetiyle (ve kaynak düğüm kodu) gösterilir.
- **Ürün koşulu bilinmiyorsa** (`WMS_STORAGE_CONDITIONS_UNKNOWN`): aday satırında ve formda "Saklama koşulu bilinmiyor — ürün
  kartında tamamlanmalı (MDM)" uyarı rozeti; hedef bin seçici pasif, "Kaydet" / "Onayla" kapalı ve nedenini yazar; satır gizlenmez.
- **"Stok karantinaya girdi"** ret durumu onayda `WMS_STOCK_NOT_PUTAWAY_ELIGIBLE`'dır (§8.4).
- **Şirket kodu geçersiz:** "Yeni görev" pasif + "şirket kodunu düzeltin" nedeni; mevcut görevler görünür ve onaylanabilir (§8.7).
- **Pending onay:** QuickView'da deneme sayısı (`Attempts`) ve son deneme zamanı; "Yeniden dene" sınırsız (§8.13).

### 11.4 Resx
`Warehouse.*` anahtarları (başlıklar, kolonlar, filtreler, düğmeler, durumlar, öncelikler, kökenler, roller, onay durumları, hata
kodları — INVENTORY'den geçen kodlar dahil), `Nav.Module.PUTAWAYPICKINGPACKING`, `Nav.Page.PUTAWAYTASKS`,
`Nav.Page.WAREHOUSELOCATIONROLES` — **7 dil** (kiracı modülü). Nav anahtar biçimi `NavNameLocalizer.Normalize` (büyük harf, yalnız
harf ve rakam).

## 12. Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol | Hata |
|---|---|---|---|---|---|
| `Idempotency-Key` = `idempotencyKey` | Evet | 8–100, eşit | tekil index | — | 400 `INVALID_REQUEST` / `IDEMPOTENCY_KEY_REUSED` |
| kaynak stok satırı (`warehouseId, fromLocationId, itemId, skuId, skuLevel, lotId?, stockStatus`) | Evet | şirketin INVENTORY bakiye satırı, OnHand > 0 | — | bakiye okuması | 422 `WMS_SOURCE_STOCK_NOT_FOUND` |
| kaynak lokasyon | Evet | Receiving rolünün stoklanabilir alt ağacında | — | rol + `getLocationPath` | 422 `WMS_SOURCE_NOT_RECEIVING` |
| `stockStatus` | Evet | yalnız `AVAILABLE` (§8.4) | — | — | 422 `WMS_STOCK_NOT_PUTAWAY_ELIGIBLE` |
| lot durumu | lot varsa | `Blocked` / `Expired` değil; `Draft` için 0178 kuralı yok — defter ne derse (Q-12 ölçümü) | — | TRACE | 422 `LOT_BLOCKED` / `LOT_EXPIRED` |
| lot recall | lot varsa | geri çağırmada değil | — | RECALL okuması | 422 `LOT_UNDER_RECALL` |
| `plannedQuantity` | Evet | decimal string > 0, ≤ 6 ondalık, ≤ açık miktar | — | açık miktar hesabı (iyimser; nihai koruma defter) | 400 / 422 `WMS_QUANTITY_EXCEEDS_OPEN` |
| `toLocationId` (plan / onay) | onayda Evet | LOCATION `Bin` ya da stoklanabilir `StorageLocation`, `Active`, `isStockable`, aynı depo yolunda, `≠ fromLocationId`, Receiving / Staging rolü yok | — | `getLocation` + `getLocationPath` | 422 `UNKNOWN_LOCATION` / `WMS_TARGET_NOT_ALLOWED` |
| `assigneeUserId` | Hayır | kiracının aktif kullanıcısı (alan yalnız Q-13 ölçümü bir kullanıcı listesi ucu bulursa) | — | kullanıcı listesi | 422 `WMS_ASSIGNEE_INVALID` |
| `priority` | Hayır | `Low \| Normal \| High` | — | — | 400 |
| `note` | Hayır | trim, ≤ 1000 | — | — | 400 |
| onay `quantity` | Evet | > 0, ≤ kalan (Pending'ler düşülür) | — | — | 422 `WMS_QUANTITY_EXCEEDS_REMAINING` |
| onay `deviationReason` | hedef ≠ plan ise | 10–1000 | — | — | 400 `REASON_REQUIRED` |
| `shortCloseReason` / `cancelReason` | Evet | 10–1000 | — | — | 400 `REASON_REQUIRED` |
| durum geçişi | — | §8.3 | — | — | 409 `WMS_INVALID_TRANSITION` |
| Pending onay varken iptal / kısa kapatma | — | — | — | — | 409 `WMS_CONFIRMATION_PENDING` |
| `If-Match` | değişiklikte Evet | `W/"<version>"` | `Version` filtresi | — | 400 / 409 `WMS_CONFLICT` |
| rol `locationId` | Evet | Active, tip `StorageLocation \| Zone \| Bin`, şirketin | tekil `(LocationId)` | LOCATION | 422 `UNKNOWN_LOCATION` / 409 `WMS_LOCATION_ROLE_EXISTS` |
| rol `acceptedStorageConditions` | rol yoksa Evet | sözleşmenin beş kodundan, tekrarsız, en çok bir sıcaklık kodu | — | — | 400 `WMS_STORAGE_CONDITION_INVALID` |
| rol kaldırma / kayıt silme | — | düğümün alt ağacında açık görev (`FromLocationId`) yoksa | — | açık görev sorgusu | 409 `WMS_LOCATION_HAS_OPEN_TASKS` |
| saklama koşulu uyumu (oluştur / düzenle / onay) | hedef varsa | §8.12 | — | PRODUCT-MASTER + rol kalıtımı | 422 `WMS_STORAGE_CONDITION_MISMATCH` / `WMS_STORAGE_CONDITIONS_UNKNOWN` |
| şirket öneki | görev oluştur | MDM `Code` `^[A-Z]{2,5}$` | — | MDM okuması | 422 `WMS_COMPANY_CODE_INVALID` / 503 |
| onaylayan | onayda | atanan ya da sorumlu | — | — | 403 `WMS_NOT_ASSIGNEE` |
| üstlen | — | görev atanmamış, Pending yok | — | — | 409 `WMS_TASK_ALREADY_ASSIGNED` / `WMS_CONFIRMATION_PENDING` |
| düzenlemede kaynak kova | — | değiştirilemez | — | — | 400 `INVALID_REQUEST` |
| düzenlemede miktar | — | ≥ onaylanan + bekleyen | — | — | 422 `WMS_QUANTITY_BELOW_CONFIRMED` |
| rol çakışması | — | aynı düğüm hem Receiving hem Staging değil | — | — | 409 `WMS_LOCATION_ROLE_CONFLICT` |
| sayfa | — | `page ≥ 1`, `pageSize ≤ 200` | — | — | 400 |
| INVENTORY ret kodları (onayda) | — | aynen iletilir: `INSUFFICIENT_STOCK`, `LOT_BLOCKED`, `LOT_EXPIRED`, `LOT_RETEST_DUE`, `UNKNOWN_LOCATION`, `SERIAL_NOT_AVAILABLE`, `ITEM_NOT_ACTIVE`, … | — | — | 422 (onay `Refused`) |

**Yeni kod adları (`WMS_*`) öneridir;** WMS-BUNDLE §5'te tek listede. `LOT_UNDER_RECALL`, `LOT_BLOCKED`, `LOT_EXPIRED`,
`UNKNOWN_LOCATION`, `REASON_REQUIRED`, `IDEMPOTENCY_KEY_REUSED`, `CROSS_LE_FORBIDDEN` mevcut kodlardır (yeniden tanımlanmaz).

## 13. Failure Path to Verify

1. **Duplicate:** aynı anahtarla iki "görev oluştur" → 1 görev, 2. cevap 200 aynı gövde; farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`.
2. **Çift onay / tekrar:** aynı onay isteği iki kez → tek hareket (`sourceModule MOD-0178`, `sourceDocumentId = taskId`,
   `sourceLineId = seq`), ikinci cevap 200 replay; defterde aynı `PostingKey` ile 1 satır.
3. **Çöküş kurtarma:** işlem 1 sonrası INVENTORY 503 → onay Pending, cevap 503; sweeper / "Yeniden dene" aynı anahtarla posting
   yapar → Posted; INVENTORY'nin önceden yazdığı (cevap kaybolan) durumda replay → yine tek hareket.
4. **Missing / invalid:** miktar 0 / negatif / üslü / 7 ondalık, hedef yok, hedef = kaynak, hedef Receiving rolünde, hedef başka
   depoda, gerekçe < 10 → 400 / 422, yazma yok.
5. **Fazla miktar:** açık miktarı aşan görev → 422; iki eşzamanlı görev açık miktarı birlikte aşarsa ikisi de açılabilir
   (iyimser) ama onayda defter `INSUFFICIENT_STOCK` verir → onay Refused, görev miktarı değişmez, stok eksiye düşmez.
6. **Tutulan stok:** QUARANTINE / QI kovadan görev → 422 `WMS_STOCK_NOT_PUTAWAY_ELIGIBLE`; MOD-0175 kararı sonrası aynı kova
   `AVAILABLE` → aday.
7. **Recall:** recall'daki lot → görev / onay 422 `LOT_UNDER_RECALL`; RECALL okuması hata → 503, yazma yok.
8. **Lot Blocked / Expired:** 422 aynı kod; görev açıkken lot bloke olursa onay defterden Refused `LOT_BLOCKED`.
9. **Unauthorized:** token yok → 401; anahtar yok → 403 (her uç); ekran anahtarı yok → UAS-001 (iskelet yok, yönlendirme yok);
   "Onayla" düğmesi `confirm` anahtarı yoksa görünmez, API yine 403.
10. **Cross-tenant / cross-company:** B kiracısı A'nın görevini okuyamaz → 404; aynı kiracıda başka şirket → 403
    `CROSS_LE_FORBIDDEN`; listede başka şirketin satırı yok.
11. **Concurrency:** iki kullanıcı aynı görevi aynı sürümle düzenler / onaylar → biri 409 `WMS_CONFLICT`; sessiz üzerine yazma yok.
12. **Geçersiz geçiş:** Completed görevi onaylama / iptal → 409 `WMS_INVALID_TRANSITION`; Pending varken iptal → 409
    `WMS_CONFIRMATION_PENDING`.
13. **İkinci bakiye yok / doğrudan defter yazımı yok:** mimari test (Boundary rules 1–2).
14. **Fail-closed üst akış:** LOCATION / MDM şirket / RECALL okuması yok → 503; INVENTORY'nin kendi 503'ü (`PRODUCT_MASTER_UNAVAILABLE`)
    onayı Pending bırakır.

## 14. Authorization Convention

### 14.1 Namespace
Yeni namespace **`warehouse.*`** (karar Q-08, Beste 2026-10-21; PKS-001 §4 bir namespace = bir sahip) — sahibi MOD-0178, manifest
`putaway-picking-packing`. Seçim gerekçesi: WAREHOUSE-OUTBOUND taban yolu `/api/warehouse`; repo'da `warehouse.` anahtarı yok
(2026-10-21 grep, 0 eşleşme). **Karar Q-08:** MOD-0180 / 0181 / 0182 aynı namespace'i paylaşamaz (tek sahip); kendi paketlerinde kendi namespace'lerini açarlar (önerilen adlar
`wave.*`, `cycle-count.*`, `barcode.*`; kesin ad o paketlerde). `warehouse.*` 0178'in kalır. `inventory.*` (MOD-0173), `supplychain.*`
(MVP-6) kullanılmaz. Manifest yalnız `warehouse.*` anahtarlarını bildirir; dış anahtarlar (`inventory.*`, `expiry.*`,
`recall.*`, `trace.*`) bildirilmez (FU-0174-CAT-01 dersi, `ManifestNamespaceOwnershipTests` genişletilir).

### 14.2 Anahtarlar (öneri — PKS-001'den türetildi)
| Anahtar | Tier | Dilim | Anlam |
|---|---|---|---|
| `warehouse.putaway-tasks.read` | 1 | S1 | liste, detay, olaylar, aday stok listesi |
| `warehouse.putaway-tasks.create` | 1 | S1 | görev aç |
| `warehouse.putaway-tasks.update` | 1 | S1 | düzenle, ata (tekil + toplu) |
| `warehouse.putaway-tasks.confirm` | 3 (bu pakette kayıtlı) | S1 | onayla, yeniden dene, kısa kapat |
| `warehouse.putaway-tasks.cancel` | 3 (`cancel`) | S1 | iptal |
| `warehouse.location-roles.read` | 1 | S1 | rol listesi |
| `warehouse.location-roles.manage` | 3 (`manage`) | S1 | rol oluştur / düzenle / sil |
| `warehouse.outbound-shipments.read` | 1 | S1 (donmuş GET'ler) / S2 | MVP-6 okuyucusu da bunu ister (Q-M6-8) |
| `warehouse.outbound-shipments.create` / `.update` / `.cancel` | 1 / 1 / 3 | S2 | |
| `warehouse.outbound-shipments.release` | 3 (bu pakette kayıtlı) | S2 | ReadyToShip yap |
| `warehouse.pick-tasks.read` / `.update` / `.confirm` | 1 / 1 / 3 | S2 | |
| `warehouse.packages.read` / `.create` / `.update` | 1 | S2 | |
| `warehouse.exceptions.read`, `warehouse.activity.read`, `warehouse.integration-monitor.read` | 1 | S3 | |

### 14.3 Politika ve aktör
- `[Authorize]` (tenant shell) + her eylemde `[HasPermission(WarehousePermissions.X)]` (sabitler; `AuthorizationGuardTests`
  her eylemi ölçer). SuperAdmin atlaması yok (FU-S1-02 emsali).
- Aktör: `tenant_user`. Süreç içi tüketiciler (MOD-0177 KR-B gibi) bu paketin yazma uçlarını kullanmaz. MVP-6 okuyucusu
  kullanıcı token'ıyla Gateway'den okur (doğrulanacak, Q-M6-8).
- Üstlenme ayrı izin istemez: `warehouse.putaway-tasks.confirm` yeter (M-5, §8.13).
- Rol şablonu önerisi (yalnız yeni kiracılar, ROLE1): Depo operatörü = putaway-tasks.read + confirm; Depo sorumlusu = tüm
  putaway-tasks.* + location-roles.*; Yönetim = read'ler.

### 14.4 Manifest
| Alan | Değer |
|---|---|
| ModuleCode | `putaway-picking-packing` (öneri) |
| SortOrder | 178 |
| Pages (S1) | `PUTAWAYTASKS` (nav), `WAREHOUSELOCATIONROLES` (nav) |
| Actions (S1) | §11.3, her gerçek düğme bir eylem |
| ApiPermissions | `warehouse.outbound-shipments.read` (S1'de UI'sız, MVP-6 okuması için — RSV1 `ApiPermissions` deseni, Scope Tenant) |

## 15. Gateway / API Routing Decision

Karar: **Gateway değişikliği gerekli** (INT-WMS, integration-agent). Bugün `/api/warehouse` route'u yok
(`gateway/Diten.ApiGateway/ocelot.json` grep 0). Öneri: `/api/warehouse` + `/api/warehouse/{everything}` → `127.0.0.1:5067`,
GET / POST / PUT / DELETE / OPTIONS (DELETE yalnız `location-roles` için anlamlı; PATCH yok), route kapsama testi; iletilen başlıklar:
`Authorization`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match`. Dev dilimi `ocelot.json`'a yazmaz.
Süreç içi gerekçe: putaway onayının defter yazımı ve recall / lot / FEFO okumaları aynı serviste (W-2); ayrı servis olsaydı
`LOCATION_TRANSFER` HTTP üzerinden giderdi ve recall ek kuralı (§8.5) için RECALL'a ayrı HTTP okuması gerekirdi.

## 16. Acceptance Criteria

**S1 (bağlayıcı hedef; ready-for-dev için):**
- [ ] AC-1 (zorunlu) — `Diten.InventoryService` 0 hata / 0 yeni uyarı ile derlenir; mevcut Inventory testlerinin hepsi yeşil kalır (sayı kabulde ölçülür, kırmızı eklenmez).
- [ ] AC-2 (zorunlu) — Altın akış: test kurulumu Receiving rolündeki `R-01` bin'ine `AVAILABLE` 100 EA lot L1 koyar → `GET /api/warehouse/putaway-candidates` satırı açık miktar 100 döner → görev 60 açılır (201, numara RCS biçiminde) → aday açık miktarı 40 → 40'lık onay `A-01` → 201, INVENTORY'de `LOCATION_TRANSFER` (sourceModule `MOD-0178`, sourceType `PUTAWAY_TASK`, sourceDocumentId = görev id, sourceLineId `1`), bakiye R-01 60 / A-01 40, görev `InProgress` → 20'lik onay → görev `Completed`; servis yeniden başlatıldıktan sonra görev, onaylar ve bakiye aynı.
- [ ] AC-3 (zorunlu) — Kısmi yerleştirme farklı hedeflere: aynı görev iki onayla iki ayrı bin'e; her onay ayrı hareket.
- [ ] AC-4 (zorunlu) — İdempotent onay: aynı onay iki kez → defterde aynı `PostingKey` ile tek satır, 2. cevap 200 replay (§13.2).
- [ ] AC-5 (zorunlu) — Pending kurtarma: INVENTORY 503 enjekte → onay Pending + 503; "Yeniden dene" ve sweeper aynı anahtarla tek hareket üretir; INVENTORY'nin yazıp cevabı kaybettiği durumda da tek hareket (§13.3); deneme sınırı yok (ör. 5 ardışık 503 sonrası 6. deneme yine aynı anahtarla gider), `Attempts` ve son deneme zamanı API'de ve ekranda; INVENTORY 422 dönerse onay `Refused` + "Stok reddetti" rozeti, görev miktarı değişmez.
- [ ] AC-6 (zorunlu) — İkinci bakiye yok: mimari test `wms_*` koleksiyonlarında stok toplam alanı ve 0178 kodunda `IInventoryLedgerRepository` / defter koleksiyon adı referansı bulmaz; tek stok kapısı `WarehouseMovementPoster`.
- [ ] AC-7 (zorunlu) — Tutulan stok: QUARANTINE / QUALITY_INSPECTION / BLOCKED kovadan görev 422 `WMS_STOCK_NOT_PUTAWAY_ELIGIBLE`; adaylarda "yerleştirilemez" işaretli.
- [ ] AC-8 (zorunlu) — Recall: geri çağırmadaki lot için görev ve onay 422 `LOT_UNDER_RECALL` (`details.movementType = LOCATION_TRANSFER`); okuma hatası 503, yazma yok.
- [ ] AC-9 (zorunlu) — Defter retleri aynen iletilir: onayda `INSUFFICIENT_STOCK` / `LOT_BLOCKED` / `LOT_EXPIRED` (ve ölçülürse `LOT_RETEST_DUE`) → 422 aynı kod + `details`, onay `Refused`, görev miktarı değişmez.
- [ ] AC-10 (zorunlu) — Hedef kuralları §12 (bin / aktif / stoklanabilir / aynı depo / ≠ kaynak / Receiving-Staging değil); ihlal → 422, yazma yok.
- [ ] AC-11 (zorunlu) — Durum makinesi §8.3: izinsiz geçiş 409 `WMS_INVALID_TRANSITION`; Pending varken iptal / kısa kapatma 409 `WMS_CONFIRMATION_PENDING`; Posted onaylı görevin iptali 409 (yalnız "Kalanı kapat"); iptal Posted hareketleri geri almaz.
- [ ] AC-12 (zorunlu) — Concurrency: bayat If-Match → 409 `WMS_CONFLICT`; eksik → 400.
- [ ] AC-13 (zorunlu) — Kiracı + şirket: başka kiracı 404, başka şirket 403 `CROSS_LE_FORBIDDEN` her uçta; liste yalnız seçili şirket.
- [ ] AC-14 (zorunlu) — RBAC: her uçta `[HasPermission]` (guard testi); token yok 401, anahtar yok 403.
- [ ] AC-15 (zorunlu) — Merkezi denetim (E-2): §21 tablosundaki her komut başarıda `central_audit_outbox`'a komutun kendi transaction'ında bir satır yazar (yol b, `RequireEnlistment`); başarısız komut kayıt bırakmaz; AuditTrail mimari testinde Inventory yeni-denetimsiz **0**.
- [ ] AC-16 (zorunlu) — Kayıt kodu RCS-001: görev numarası `IWarehouseRecordCodeAllocator` portundan, RCS biçiminde geçici sayaçla (Q-07 B; ortak servis gelince FU-0178-RCS-01); eşzamanlı iki görev farklı numara; numara API'de değiştirilemez; ASCII.
- [ ] AC-17 (zorunlu) — Lokasyon rolleri: oluştur / düzenle / sil (soft delete, `IsDeleted` + `DeletedAt`) / toplu sil; düğüm başına tek kayıt (roller + kabul edilen saklama koşulları); bir düğüm birden çok role sahip olabilir, Receiving + Staging birlikte 409; açık görevi olan düğümden Receiving kaldırma / silme 409 `WMS_LOCATION_HAS_OPEN_TASKS`.
- [ ] AC-18 (zorunlu) — WAREHOUSE-OUTBOUND 1.0.0 değişmez: dosya byte-eşit; donmuş GET uçları S1'de boş liste / 404 `UNKNOWN_OUTBOUND` döner, gövde MVP-6 `FrozenReadSchema.Warehouse` / `WarehousePage` kurallarını geçer (null üye yok).
- [ ] AC-19 (zorunlu) — Sözleşme uyumu: her S1 cevabı CT kabullü `wms-bundle.openapi.yaml` şemasına karşı otomatik doğrulanır.
- [ ] AC-20 (zorunlu) — Self-registration: manifest `putaway-picking-packing` 2 sayfa + §11.3 eylemleri + `ApiPermissions`; rota `/Platform/` değil (Scope Tenant); namespace testi `warehouse.*` dışı anahtar bildirmez.
- [ ] AC-21 (zorunlu) — Layout: `Views/Warehouse/PutawayTasks/*.cshtml` ve `Views/Warehouse/WarehouseLocationRoles/*.cshtml` Razor bloğunda `Layout = "_LayoutTenantShell"` AÇIKÇA yazılı (test).
- [ ] AC-22 (zorunlu) — DataTable: her iki ekran `verify_datatable_page.py . --area Warehouse --module {PutawayTasks|WarehouseLocationRoles}` PASS, `data-dt-data-mode="server"` pakete eşit; `quality-gate-datatable` PASS; UAS-001 testi (anahtarsız kullanıcıya iskelet yok, yönlendirme yok).
- [ ] AC-23 (zorunlu) — Yerelleştirme: tüm `Warehouse.*`, nav ve yardım anahtarları 7 dilde (en, tr, fr, es, zh, ar, ru); parite testi; `check_pack_nav_keys.py` PASS.
- [ ] AC-24 (zorunlu) — Sayfa yardımı (UXH-001): `PUTAWAYTASKS` ve `WAREHOUSELOCATIONROLES` için `Help.{KOD}.Purpose/Audience/Step1..n/Step{n}Title` 7 dilde; tur yok.
- [ ] AC-25 (zorunlu) — INT-WMS: Gateway `/api/warehouse` + `/{everything}` → 5067, route kapsama testi; Idempotency-Key, If-Match ve X-Correlation-Id Gateway'den geçer; DELETE yalnız location-roles yolunda anlamlı.

**S2 (iskelet — S2 paket revizyonunda kesinleşir; W-5 kabulü ön şart):**
- [ ] AC-26 (isteğe bağlı) — Outbound oluşturma tahsisle birlikte: tüm satırlar `getFefoOrder` sırasıyla lot rezervasyonu alır ya da 422 (kısmi tahsis yok); durum `Allocated`.
- [ ] AC-27 (isteğe bağlı) — Pick onayı bin → Staging `LOCATION_TRANSFER`, durum `AVAILABLE` korunur, rezervasyon düşmez; recall lotu 422 `LOT_UNDER_RECALL`.
- [ ] AC-28 (isteğe bağlı) — FEFO sapması gerekçe ister (10–1000) ve outbound satırında MVP-6'ya taşınır.
- [ ] AC-29 (isteğe bağlı) — Paketleme: koli = fiziksel paket, ağırlık / ölçü elle (karar Q-MDM-1), ambalaj seviyesi dönüşümü yok; tüm satırlar kolide → `Packed`.
- [ ] AC-30 (isteğe bağlı) — `ReadyToShip`: outbound donmuş şemaya uyar, `OutboundShipmentReady` outbox'a yazılır, poll listesinde görünür; sonrası detay yalnız `status` değiştirir (MVP-6 drift kuralı).
- [ ] AC-31 (isteğe bağlı) — MVP-6 dispatch sinyali → `Shipped`; 0178 hiçbir koşulda `GOODS_ISSUE_SALES` yazmaz (mimari test).
- [ ] AC-32 (isteğe bağlı) — İptal: rezervasyon bırakılır; staging'deki stok için `Origin = Cancellation` putaway görevi.
- [ ] AC-33 (isteğe bağlı) — Outbound satırında `skuLevel: Item` reddedilir (MVP-6 enum'u Item tanımaz, Q-M6-6).
- [ ] AC-34 (isteğe bağlı) — S2 ekranları (compact Outbound, slim PickTasks, Paketleme) 7 dil, verifier PASS.
- [ ] AC-35 (isteğe bağlı) — Golden e2e: GRN (ya da test girişi) → putaway → outbound → pick → pack → ReadyToShip → MVP-6 Dispatched (`GOODS_ISSUE_SALES` MVP-6'dan) → Shipped; defterde toplam stok tek kaynaktan tutarlı.

**S1 — mockup incelemesi kararları (M-1…M-5, Beste 2026-10-21):**
- [ ] AC-37 (zorunlu) — Saklama koşulu uyumu (§8.12): `COOL_2_8` ürünü etiketsiz rafa ya da `AMBIENT_15_25` etiketli rafa → görev oluşturma, düzenleme ve onay 422 `WMS_STORAGE_CONDITION_MISMATCH`; üst düğümde `COOL_2_8` etiketli bölgenin alt bin'ine → kabul (kalıtım); koşulsuz ve yalnız `AMBIENT_15_25` ürün etiketsiz rafa → kabul; `storageConditions` alanı olmayan SKU → 422 `WMS_STORAGE_CONDITIONS_UNKNOWN`; mesajlar 7 dilde.
- [ ] AC-38 (zorunlu) — Şirket öneki (M-2): MDM `Code` `GMG` → numara `GMG-PUT-000001`; `Code` `gmg1` / tek harf / 6 harf → görev açılmaz, 422 `WMS_COMPANY_CODE_INVALID` + yerelleştirilmiş "şirket kodunu düzeltin"; `Code` sonradan değişince eski numaralar aynı kalır, yeni görev yeni önek + kendi sayacı; depo kodu numarada yok, listede sütun.
- [ ] AC-39 (zorunlu) — Onay kilidi: Pending onay varken yeni onay / düzenleme / üstlenme / kapama / iptal 409 `WMS_CONFIRMATION_PENDING`; ekranda düğmeler pasif ve nedenli; yalnız "Yeniden dene" çalışır.
- [ ] AC-40 (zorunlu) — Kaynak kova değişmez: düzenlemede kaynak alanı gönderilirse 400; planlanan miktar onaylanan + bekleyenin altına → 422 `WMS_QUANTITY_BELOW_CONFIRMED`; planlanandan fazla onay → 422 `WMS_QUANTITY_EXCEEDS_REMAINING`.
- [ ] AC-41 (zorunlu) — Seçilemeyen stok listede kilitli görünür: neden + engelleyen belge numarası (karantina tutma / recall vaka / bloke kaydı / lot durumu); satır gizlenmez, görev açılamaz.
- [ ] AC-42 (zorunlu) — Raf önerisi formu otomatik doldurmaz; "Uygula" sonrası kayıt `TargetSource = SuggestionApplied` taşır ve zaman çizelgesinde `SuggestionApplied` olayı var; öneri yalnız koşul uyumlu binlerden.
- [ ] AC-43 (zorunlu) — Menü kuralı: onay yokken yalnız "İptal", Posted onay varken yalnız "Kalanı kapat" etkin; pasif öğe nedenini 7 dilde yazar; sunucu da aynı kuralı uygular (AC-11).
- [ ] AC-44 (zorunlu) — Raf alanı klavye ve barkod okuyucu (klavye öykünmeli) girişini kabul eder: tam kod → bin seçilir; bilinmeyen kod → alan hatası, gönderim yok.
- [ ] AC-45 (zorunlu) — Mobil 375 px: onay paneli dokunma alanları ≥ 48 px, miktar −/+, sabit alt eylem çubuğu (ölçüm ekran görüntüsü + DOM boyut testi).
- [ ] AC-46 (zorunlu) — Durum ile entegrasyon bayrağı ayrı: durum onaylanan miktardan türer; "Onay bekliyor" / "Stok reddetti" rozetleri ayrı; Refused onay durumu değiştirmez.
- [ ] AC-47 (zorunlu) — Onay yetkisi ve üstlenme: atanmamış görevi operatör "Üstlen" ile kendine atar (yalnız `warehouse.putaway-tasks.confirm`); atanmış görevi başkası onaylayamaz (403 `WMS_NOT_ASSIGNEE`), sorumlu (`.update`) onaylayabilir; atanmış görevi üstlenme 409 `WMS_TASK_ALREADY_ASSIGNED`.
- [ ] AC-48 (zorunlu) — E-imza yok: putaway onayı imza istemez; MOD-0022'ye imzalanabilir eylem kaydı yok (manifest / kayıt testi); onayın kim / ne / ne zaman bilgisi merkezi denetim satırında.
- [ ] AC-49 (zorunlu) — Açık görevin lotu sonradan recall'a ya da stoğu karantinaya girerse görev askıya alınmaz; onay anında 422 (`LOT_UNDER_RECALL` / defter kodu), görev açık kalır.
- [ ] AC-50 (zorunlu) — RTL: `ar` dilinde iki ekran sağdan sola, diğer altı dil soldan sağa (Razor / CSS testi).

**S3 (iskelet):**
- [ ] AC-36 (isteğe bağlı) — İstisna Kutusu (Refused / uzun Pending onaylar, outbound uyuşmazlıkları), Etkinlik İzi (görev olayları + `sourceModule MOD-0178` hareketleri), Entegrasyon İzleme (outbox / poll durumu); hepsi salt okunur, şirket kapsamlı.

## 17. Test Expectations

- **DCP-002:** `verify_module_id.py . --check-id MOD-0178 --name "Putaway / Picking / Packing"` çıkış 0 (2026-10-21 ölçüldü; dev
  dalında yeniden).
- **Unit (Domain):** `PutawayTaskRules` (geçiş tablosu, kalan = planlanan − Posted − Pending, kısa kapatma koşulu), hedef kuralları,
  decimal ayrıştırma (6 ondalık, üs yok), `LocationRoleRules` (çakışma).
- **Handler / entegrasyon (kendi replika seti, `INVENTORY_TEST_MONGO` ya da paketin geçici replika seti; 27017 YOK):** gerçek
  `InternalMovementPostingService` + gerçek defter ile onay (Prism / sahte değil), idempotent replay, Pending kurtarma (enjekte 503 +
  "yazıldı ama cevap kayboldu"), tutulan stok, recall (gerçek `IRecallLotStatusReader` + tohumlanmış vaka), lot Blocked, eşzamanlı iki
  onay → biri `INSUFFICIENT_STOCK`, kiracı / şirket izolasyonu, izin retleri, merkezi denetim satırları.
- **API sözleşme testleri:** her durum kodu + hata kodu; cevaplar `wms-bundle.openapi.yaml`'a karşı; WAREHOUSE-OUTBOUND GET'leri
  MVP-6'nın `FrozenReadSchema` kurallarının (null yok, UUID "D", SkuLevel 3 değer) bir kopyası olmayan, **üretim şemasından türetilen**
  doğrulamaya karşı (kural kendi kopyasıyla ölçülmez — Çekirdek 10).
- **Mimari:** ikinci bakiye yok; defter deposuna / koleksiyonlarına referans yok; `GOODS_ISSUE_SALES` yazan 0178 kodu yok; manifest
  namespace; `/Platform/` rota yok; her HTTP eyleminde izin (`AuthorizationGuardTests`); AuditTrail yeni-denetimsiz 0.
- **Web:** Razor sayfa testleri (Layout, UAS-001, düğme gizleme), 7 dil parite (`NavManifestL10nGuardTests`), verifier.
- **Çalışma zamanı kanıtı (E4):** servis 5067 + Gateway + Web: §16 AC-2 akışı ekran + API cevabı + Mongo satırları (`wms_tasks`,
  `inventory_transactions`, `central_audit_outbox` Delivered) + yeniden başlatma sonrası.
- **Sabotaj kanıtı (K3) — her biri bir testi kırmızıya çevirmeli, geri alınınca yeşil, diff boş:**
  (a) onay anahtarını her denemede yeni üret → çift hareket testi kırmızı; (b) recall ön kontrolünü kaldır → AC-8 kırmızı;
  (c) `stockStatus` filtresini kaldır → AC-7 kırmızı; (d) görev deposundan `LegalEntityId` yüklemini düşür → şirket izolasyon testi
  kırmızı; (e) If-Match kontrolünü atla → concurrency kırmızı; (f) Pending'i iptale izin ver → AC-11 kırmızı; (g) merkezi denetim
  işaretini bir komuttan sil → AuditTrail testi kırmızı; (h) WAREHOUSE-OUTBOUND cevabına `nextCursor: null` ekle → AC-18 kırmızı;
  (i) kalıtımı kaldır (yalnız binin kendi etiketi) → AC-37 alt bin testi kırmızı; (j) etiketsiz rafa her ürünü kabul et → AC-37 kırmızı;
  (k) bilinmeyen `storageConditions`'ı boş say → AC-37 kırmızı; (l) şirket kodu regex'ini kaldır → AC-38 kırmızı; (m) Pending kilidini
  kaldır → AC-39 kırmızı; (n) atanan kontrolünü kaldır → AC-47 kırmızı.
- Koşulmayan: `AccountKindAcceptanceHost.Tests` (MVP-1 emsali).

## 18. Ready-for-dev Checklist

| # | Madde | Durum |
|---|---|---|
| 1 | Golden Reference Slim kodu okundu (backend + frontend) | ✅ |
| 2 | Frontmatter zorunlu alanlar (service, shell, golden_reference, data_mode, entity_base) | ✅ |
| 3 | Layout & Shell Contract'ta `Layout = "_LayoutTenantShell"` açık + AC-21 | ✅ |
| 4 | Backend File Convention Golden ile birebir; sapmalar (Cancel / BulkAssign) | ✅ onaylı (Q-05) |
| 5 | Frontend File Contract Slim seti tam | ✅ |
| 6 | Validation Rules her alan | ✅ §12 |
| 7 | Failure Path ≥ 4 | ✅ §13 (14) |
| 8 | Authorization: namespace + anahtarlar + politika + aktör | ✅ (Q-08) |
| 9 | Gateway kararı + integration-agent işi | ✅ §15 (INT-WMS) |
| 10 | Acceptance criteria ölçülebilir, kimlikli | ✅ §16 |
| 11 | Test expectations build / verifier / RESX / smoke / sabotaj | ✅ §17 |
| 12 | Audited Events her yazma komutu, yol b, K2 | ✅ §21 (S2 iskelet) |
| 13 | Record Codes (RCS-001) | ⏳ geçici sayaç ✅ (Q-07 B), şirket öneki ✅ (M-2); önek kaydı FU-0178-RCS-02 sahip onayı bekliyor |
| 13a | Saklama koşulu uyumu için PRODUCT-MASTER v1.1 `storageConditions` canlı (PM-LIVE) | ⏳ Q-MDM-3 |
| 14 | Sayfa yardımı (UXH-001) + tur kararı | ✅ §24 |
| 15 | Teslim dilimleri | ✅ §23 |
| 16 | XMC-001: başka modül değişikliği yok; S2 ihtiyaçları sahiplere soru | ✅ §7 |
| 17 | WMS-BUNDLE taslağı CT kabulü (S1 kısmı) | ⏳ |
| 18 | Paket kararları Q-01…Q-09, Q-11…Q-13, Q-MDM-1 | ✅ §19.1.1 (Q-12 / Q-13 geliştiricinin ilk ölçümü); açık: Q-10 + diğer ekipler §19.5 |
| 19 | DCP-002 ön kontrol çıkış 0 | ✅ 2026-10-21 |
| 20 | DCP-009 servis notu güncellemesi (SupplyChainService → InventoryService) | ⏳ FU-0178-DCP-01 (DCP sahibi) |
| 21 | Bağımsız inceleme (Codex ya da Claude, ayrı oturum) + `reviews.tsv` satırı (CT-APR-02) | ⏳ |
| 22 | Sürüme bağlı onay satırı `approvals.tsv` (CT-APR-01; ilk onay = yüksek risk, yazmayan onaylayıcı) | ⏳ |
| 23 | Port 5067 pakette tutarlı | ✅ |

## 19. Implementation Notes

### 19.1 Sahip kararları (2026-10-21) — bağlayıcı
| # | Karar | Pakette |
|---|---|---|
| W-1 | Build (Blueprint "Buy/Partner" sapması; EA düzeltir) | başlık, FU-0178-EA-01 |
| W-2 | `Diten.InventoryService` 5067; ayrı bölüm; stoğa yalnız INVENTORY'nin olağan yolundan; defter koleksiyonuna doğrudan yazmaz; ikinci bakiye yok; DCP-009 notu güncellenir | §2, §5, §8.1, §8.3, FU-0178-DCP-01 |
| W-3 | MOD-0179 (3PL) ertelendi; sözleşme dış kaynaklı görev / onaya kapı bırakır | `Origin External`, `Channel External`, `SourceSystem`, `ExternalRef`; WMS §14 |
| W-4 | `GOODS_ISSUE_SALES`'i 0178 yazmaz, MOD-0183 `Dispatched`'ta yazar; 0178: rezervasyon + staging `LOCATION_TRANSFER` + `ReadyToShip`; dispatch → `Shipped` | §1, §11.1 S2, AC-31 |
| W-5 | Lot / lokasyon rezervasyonu INVENTORY'ye eklemeli, MVP-1 CT ayrı WP; S2 buna bağlı | §7, §8.10 |
| W-10 | MVP-5 Control Tower = MVP-1 CT | `owner` |
| E-1 | Correlation: Gateway üretir, ilet, geçersiz 400 | §8.1, Q-10 |
| E-2 | BL-501 merkezi denetim şimdi | §8.8, §21 |
| E-6 | UTC gün sınırı | §8.1 |
| — | MDM P6 / P9'a bağımlılık yok; barkod MOD-0182 | §2, §19.1.1 Q-MDM-1 |

#### 19.1.1 Karar verildi — paket soruları (kaynak: sahip kararı 2026-10-21, Beste)
| # | Soru | Karar | Pakette |
|---|---|---|---|
| Q-01 | W-5 rezervasyon tasarımı | W-5 rezervasyon WP'si **MVP-1 CT'nin**, **S2'den önce**; §8.10'daki dört gereksinim pakette S2 bağımlılığı olarak kalır | §7, §8.10, FU-0178-W5 |
| Q-02 | Tutulan stok putaway'i | **Yalnız `AVAILABLE` stok yerleştirilir**; QUARANTINE / QUALITY_INSPECTION / BLOCKED (ve DAMAGED / EXPIRED) hariç; ret 422 `WMS_STOCK_NOT_PUTAWAY_ELIGIBLE` + 7 dilde yerelleştirilmiş mesaj | §8.4, §12, AC-7 |
| Q-03 | Recall'daki lotun putaway'i | **Reddedilir** (422 `LOT_UNDER_RECALL`); 0178 recall durumunu kendisi okur; S2 pick için de aynı kural | §8.5, AC-8, AC-27 |
| Q-04 | Lokasyon rollerinin yeri | **0178'in nesnesi** (`WarehouseLocationRole`); LOCATION sözleşmesi değişmez | §3, §4.4, ASSUMPTION-0178-02 kaldırıldı |
| Q-05 | Golden sapması | **Onaylı:** görevlerde Delete / BulkDelete yok; gerekçeli Cancel + BulkAssign | §10, §11.3 |
| Q-06 | Dosya stratejisi + hata kodları | **WMS-BUNDLE ayrı dosya** (`wms-bundle.openapi.yaml`); WAREHOUSE-OUTBOUND 1.0.0 değişmez; `WMS_*` hata kodları WMS-BUNDLE'da tanımlı (kodda 0178'e ait `WarehouseErrorCodes`) | §5, §12, WMS taslağı başlık + §5 |
| Q-07 | Kayıt kodu (BL-565 yok) | **B:** RCS-001 biçiminde (`{ŞİRKET}-PUT-000001`) geçici sayaç, `IWarehouseRecordCodeAllocator` portunun arkasında; BL-565 ortak servisi gelince ona geçilir; geçiş ve feragat FU olarak yazılı (FU-0178-RCS-01). Önek kaydı (`.antigravity/rules/record-coding-standard.md` §3 tablosu) **ayrı sahip onayı bekleyen açık iş** — bu paket `.antigravity`'ye dokunmaz | §8.7, §22, FU-0178-RCS-01, FU-0178-RCS-02 |
| Q-08 | İzin namespace'i | `warehouse.*` MOD-0178'in; MOD-0180 / 0181 / 0182 kendi paketlerinde kendi namespace'lerini açar (tek sahip kuralı) | §14.1 |
| Q-09 | Pending onay sweeper'ı | Kabul: 2 dakikadan eski Pending onaylar 60 sn'de bir, aynı `PostingKey` ile | §8.3 |
| Q-11 | GRN → putaway tetikleyicisi | S1'de görev **elle / listeden**; otomatik tetikleyici (gözlemci dahil) Cihan'ın (MVP-2) cevabına kadar açık soru | §8.2, Q-M2-1 |
| Q-12 | Retest-due ve Draft lot | **Geliştiricinin ilk ölçümü.** Varsayılan davranış: 0178 retest ya da Draft için kendi kuralı yazmaz; defter `LOCATION_TRANSFER`'ı reddederse kod aynen iletilir, kabul ederse 0178 engellemez. Ölçüm sonucu bu pakete Implementation Notes olarak yazılır | §8.4, §12 |
| Q-MDM-1 | P9 gelene kadar paketleme | Koli = fiziksel paket; ağırlık / ölçü **elle** girilir; ambalaj seviyesi dönüşümü yok (koli içi standart adet önerisi de yok); P9 geldiğinde geçiş FU-0178-P9 | §4.5, §11.1 S2, AC-29, FU-0178-P9 |
| Q-MDM-2 | Şirket öneki kaynağı | MDM tüzel kişilik `Code` (M-2 ile kapandı) | §8.7 |
| Q-13 | Atanan kullanıcı listesi | **Geliştiricinin ilk ölçümü.** Gateway üzerinden kiracı kullanıcı listesi ucu varsa atama alanı kalır (form 6); yoksa S1'de atama alanı ve "Ata" toplu eylemi çıkar (form 5, `BulkAssign` S1'de kurulmaz) ve ölçüm notu yazılır | §4.1, §9, §11.3, §12 |

#### 19.1.2 Karar verildi — mockup incelemesi (kaynak: sahip kararı 2026-10-21, Beste)
| # | Karar | Pakette |
|---|---|---|
| M-1 | E-imza yok: putaway onayı hareket kaydıdır, imzalı kalite kararı değil; kim / ne / ne zaman denetim izinde | §2, §8.14, AC-48 |
| M-2 | Görev numarası öneki = MDM tüzel kişilik `Code` (`LegalEntity.cs:11`), `^[A-Z]{2,5}$` değilse görev açılmaz (yerelleştirilmiş "şirket kodunu düzeltin"); önek bir kez yazılır, değişmez; sayaç (kiracı, şirket öneki, tür) başına; depo kodu numarada değil, listede sütun; §5.4 kısa kod alanı gelince geçiş FU-0178-RCS-01 | §4.1, §8.7, §12, AC-38, FU-0178-RCS-01 |
| M-3 | Saklama koşulu uyumu S1 engeli: lokasyon kaydı kabul edilen koşulları (PRODUCT-MASTER `StorageConditionCode`) taşır, alt ağaca kalıtır; uyumsuzsa oluştur / düzenle / onay reddedilir; etiketsiz rafa yalnız koşulsuz ya da yalnız `AMBIENT_15_25` ürün | §4.4 (tasarım gerekçesi), §8.12, §9, §12, AC-37 |
| M-4 | Ekran kuralları: onay kilidi, kaynak kova değişmez, seçilemeyen stok kilit + neden + belge no ile görünür, öneri "Uygula" ile, menü kuralı, raf alanı klavye / okuyucu, mobil 375 px, durum ≠ entegrasyon bayrağı | §8.15, AC-39…AC-46 |
| M-5 | Planlanandan fazla onay yasak; onayı atanan ya da sorumlu verir; operatör atanmamış görevi üstlenir (`confirm` izni yeter); Pending sweeper aynı anahtarla; açık görevde otomatik askı yok, onayda kontrol; bir düğüm çok rol alabilir ama Receiving + Staging değil; açık görevi olan düğümden rol kaldırılamaz; diller 7, RTL yalnız ar | §4.4, §8.3, §8.13, §8.15, AC-17, AC-40, AC-47, AC-49, AC-50 |
| M-6 | S2'ye öneriler (kapsam değil) | §20 FU-0178-S2-UX |

### 19.2 Ölçülen gerçekler (`47a2c1cb9`, salt okuma)
- `InternalMovementPostingService.cs:18-31` — `postMovement` ile aynı yol; post-commit `IPostedMovementObserver` bildirimi.
- `QuarantineMovementPoster.cs:34-39` — tek stok kapısı + belgeden türeyen idempotency anahtarı deseni.
- `MovementDirectionRules.cs:82-94` — `LOCATION_TRANSFER` aynı depo, farklı lokasyon, durum korunur (tek adım).
- `inventory-bundle.openapi.yaml:43-46` — recall engeli `LOCATION_TRANSFER`'ı kapsamaz.
- `IRecallLotStatusReader.cs:9-11` — `UnderRecallAsync(lotIds)` süreç içi okuma.
- `IExpiryReadModel.cs:32` — `FefoCandidatesAsync(warehouseId, itemId, skuId, skuLevel)` (S2 FEFO okuması).
- `InventoryScopeResolutionMiddleware.cs:57-60` — `ScopedBasePaths` (0176 / 0177 ekleme emsali).
- `Program.cs:45-56` manifestler, `:63` `AddCentralAudit(…RequireEnlistment = true)`.
- `tests/architecture/audit-ledger/Diten.InventoryService.md:12` — iz `inventory-merkezi-iletim` yol b.
- MVP-6 okuyucusu: `ReadOnlySourceClients.cs:12,18` (`/api/warehouse/outbound-shipments[/{id}]`), `FrozenReadSchema.cs:8-39`
  (JSON null reddi, UUID "D", SkuLevel yalnız Gsku / Lsku / FinishedGood, Statuses 6 değer, ek üyelere tolerans).
- MOD-0183 pack §8.2: outbound anlık görüntü hash'i ile drift tespiti → `ReadyToShip` sonrası detay değişmemeli.
- `ocelot.json`: `/api/warehouse` yok. Repo: `warehouse.` izin anahtarı yok. RCS-001 ortak servis yok (BL-565 AÇIK).

### 19.3 Varsayımlar (yalnız yeni paket sürümüyle değişir)
- **ASSUMPTION-0178-01:** S1 görev kaynağı elle (Q-M2-1 cevabına kadar).
- ~~ASSUMPTION-0178-02~~ → karar Q-04: lokasyon rolleri 0178'in nesnesidir, LOCATION sözleşmesi değişmez.
- **ASSUMPTION-0178-03:** Açık miktar iyimser ön kontroldür; nihai koruma defterin negatif stok kuralıdır.
- **ASSUMPTION-0178-04:** Retest için 0178 kuralı yok; defterin cevabı geçerli.
- **ASSUMPTION-0178-05:** Görev ve onay olayları ürün içi zaman çizelgesidir; merkezi denetimin yerine geçmez.
- **ASSUMPTION-0178-06:** `outboundId` (S2) sunucu UUID'sidir, görünen kod `outboundNumber` (eklemeli) — frozen örnek `OUT-9001`
  yalnız örnektir. Q-M6-9.
- **ASSUMPTION-0178-07:** `target: 2026-12-31` şema gereği planlama varsayılanıdır, taahhüt değildir.

### 19.4 Altın akış (e2e) özeti
S1: (GRN ya da test girişi, Receiving bin, `AVAILABLE`) → aday listesi → putaway görevi → kısmi / tam onay → `LOCATION_TRANSFER`
→ raf bininde stok → görev `Completed`. S2: outbound (Allocated, FEFO + rezervasyon) → pick (bin → Staging) → Picked → paket →
Packed → ReadyToShip (+ olay / poll) → MVP-6 Shipment → Dispatched (MVP-6 `GOODS_ISSUE_SALES`) → 0178 `Shipped`.

### 19.5 Açık sorular (yalnız diğer ekipler + Q-10)

MVP-1 CT / sahip soruları Q-01…Q-09, Q-11…Q-13, Q-MDM-1, Q-MDM-2 ve mockup kararları M-1…M-6 **karar verildi** (§19.1.1, §19.1.2). Açık kalan:

**Beste / MVP-1 CT:**
- **Q-10:** E-1'in Inventory 400 WP'si ne zaman? 0178 o zamana kadar servis davranışını (kabul et, değiştir) izler, kendi
  correlation ara katmanını yazmaz.

**Cihan / MVP-2:**
- **Q-M2-1:** GRN → putaway tetikleyicisi: (A) elle (S1), (B) 0178 süreç içi gözlemci, (C) GRN sonrası WMS API çağrısı, (D) GoodsReceived
  olayı (bus kapalı). GRN'nin `locationId`'si Receiving rolündeki bir lokasyon olacak mı?
- **Q-M2-2:** Gerçek `IInventoryPostingClient` (Dalga 1) ne zaman? S1 e2e'si o zamana kadar test girişiyle koşar.
- **Q-M2-3 (bilgi notu):** mal kabul alanının saklama koşulu uyumu 0178 S1'in kontrolü **değildir** — malı Receiving lokasyonuna
  GRN (MVP-2) koyar. 0178 yalnız putaway hedefini denetler (§8.12). Soğuk zincir ürününün uygun kabul alanına alınması MVP-2'nin
  GRN kuralıdır; MVP-2 isterse 0178'in `WarehouseLocationRole.AcceptedStorageConditions` okumasını (WMS-BUNDLE `GET /location-roles`)
  kullanabilir.

**Natig / MVP-6:**
- **Q-M6-1:** `Dispatched`'ta `GOODS_ISSUE_SALES` için `fromWarehouseId` + `fromLocationId` (staging) + `lotId` gerekir; donmuş outbound
  satırında lokasyon ve `lotId` yok (yalnız `lotNumber`). Eklemeli isteğe bağlı üyeler (WMS §2.3) kabul mü? MOD-0183 §8.2 anlık görüntü
  beyaz listesi güncellenir mi?
- **Q-M6-2:** Çıkışta 0178 rezervasyonunun tüketimi: shipment satırı `inventoryReferenceId` = `reservationId` olarak taşınabilir mi?
- **Q-M6-3:** FEFO sapma gerekçesinin (`fefoBypassReason`) `GOODS_ISSUE_SALES`'e taşınması.
- **Q-M6-4:** Dispatch sinyali: `ShipmentDispatched` olayı (bus kapalı) mı, `queryShipments?sourceDocumentId={outboundId}` poll mu?
- **Q-M6-5:** `OutboundShipmentReady` olayına `legalEntityId` / `tenantId` (GAP-0183-02) ve UUID `correlationId` (GAP-0183-01) — kabul mü?
- **Q-M6-6:** `skuLevel: Item` outbound'da yasak mı (MVP-6 `SkuLevels` 3 değer)?
- **Q-M6-7:** `ReadyToShip` sonrası iptal / MVP-6 shipment açtıktan sonra iptal sözleşmesi.
- **Q-M6-8:** MVP-6 outbound'u hangi token / izinle (`warehouse.outbound-shipments.read`) ve `X-Legal-Entity-Id` ile mi okuyor?
- **Q-M6-9:** `outboundId` UUID + `outboundNumber` ayrımı MVP-6 eşlemesine (`sourceDocumentId = outboundId`) uygun mu?

**Ali / MDM:**
- **P9 takvimi (bilgi):** P9 (ambalaj hiyerarşisi) ne zaman geliyor? Yalnız planlama bilgisi; 0178 buna bağlı değil (Q-MDM-1 karara
  bağlandı, §19.1.1). P6 (GTIN) 0178'e gerekmez (MOD-0182).
- **Kısa kod alanı takvimi (bilgi):** RCS-001 §5.4 Tüzel Kişilik kısa kod alanı ne zaman geliyor? (Q-MDM-2 kapandı — şimdilik `Code`,
  M-2; geçiş FU-0178-RCS-01.)
- **Q-MDM-3:** MOD-0048 `storage-condition` listesi okunabilir bir uçla açılacak mı? O zamana kadar 0178 sözleşmedeki beş kodu
  (`StorageConditionCode`) kullanır (FU-0178-STOR-01). Ayrıca PRODUCT-MASTER v1.1'in (`storageConditions`) donma / PM-LIVE takvimi —
  S1 saklama koşulu uyumu buna bağlı.

**EA:**
- **Q-EA-1:** Blueprint 8.8 MOD-0178 "Buy/Partner" → "Build" düzeltmesi (W-1).
- **Q-EA-2:** DCP-009 `:45` servis notu (`Diten.SupplyChainService` 5066 "warehouse") W-2 ile güncellenmeli (DCP sahibi).
- **Q-EA-3:** S2 outbound talebinin kaynağı: satış siparişi SoR'u yok (MOD-0172 ATP commercial "SoR EA-TBD") — outbound'u kim / hangi
  modül açar?
- **Q-EA-4:** RCS-001 tür önekleri (PUT / PCK / OUT / PKG) — öneklerin seçimi EA ile; tabloya kaydı ayrı sahip onayı bekleyen açık iş
  (FU-0178-RCS-02, `.antigravity/**`).
- **Q-EA-5:** Blueprint WMS-BUNDLE "OTel" beklentisi: yalnız ad sözleşmesi mi, exporter mı?

### 19.6 CT kabul kayıtları
(Henüz yok — draft.)

## 20. Follow-up Items

- **FU-0178-EA-01 (EA):** Blueprint "Buy/Partner" → Build (W-1).
- **FU-0178-DCP-01 (DCP-009 sahibi):** servis notu `Diten.InventoryService` 5067 (W-2).
- **FU-0178-RCS-01 (BL-565 — geçiş + feragat, sahip kararı Q-07 B, Beste 2026-10-21):** S1 görev numarası (S2'de outbound / koli)
  `IWarehouseRecordCodeAllocator` portunun arkasındaki **geçici yerel sayaçtan** gelir: RCS-001 biçimi `{ŞİRKET}-PUT-000001`, sayaç
  (kiracı, şirket öneki, tür) başına atomik (`findOneAndUpdate` + `$inc`), sıfırlanmaz, ASCII, harcanan numara geri verilmez.
  **Feragat:** RCS-001 §5.12 / §6 "modül kendi sayacını yazmaz" kuralından geçici sapma — sahip: Beste · gerekçe: ortak servis (BL-565)
  yok, S1 beklemesin · telafi: biçim RCS-001'e birebir, sayaç tek port arkasında, kod değiştirilemez · bitiş: BL-565 ortak servisi
  teslim edilince. **Geçiş:** port ortak servise bağlanır; sayaç ortak servise o türün en büyük numarasının üstünden devam edecek
  şekilde devredilir (RCS-001 §5.11 deseni); verilmiş kodlar değişmez (§2 "mevcut kodlar değiştirilmez").
  **Önek kaynağı (M-2):** şimdilik MDM tüzel kişilik `Code` (`^[A-Z]{2,5}$`); RCS-001 §5.4 kısa kod alanı gelince önek ondan okunur —
  yeni önek yeni sayaç başlatır, verilmiş numaralar değişmez.
- **FU-0178-RCS-02 (sahip onayı bekleyen açık iş):** önekler (`PUT`, `PCK`, `OUT`, `PKG`) `.antigravity/rules/record-coding-standard.md`
  §3 tablosuna yazılır — korunan yol, ayrı sahip onayıyla; bu paket ve dev dilimi `.antigravity`'ye dokunmaz.
- **FU-0178-P9 (MDM P9 gelince, karar Q-MDM-1):** paketlemede ambalaj seviyesi (P9) kullanımına geçiş — koli içi standart adet önerisi,
  ambalaj seviyesi dönüşümü; o zamana kadar ağırlık / ölçü elle ve dönüşüm yok. Geçiş ayrı WP + paket revizyonu.
- **FU-0178-STOR-01 (MDM):** saklama koşulu etiket seçicisinin MOD-0048 `storage-condition` listesinden okunması (Q-MDM-3).
- **FU-0178-S2-UX (S2'ye öneri, kapsam değil — M-6):** özet kartları (açık / bekleyen / reddedilen / bugün tamamlanan); gerekçeli 3 raf
  adayı (koşul uyumu, aynı SKU / lot yakınlığı, boş bin); "Benim görevlerim" yürüyüş sırası (lokasyon koduna göre rota); toplu
  öncelik değiştirme + klavye kısayolları (UXH-001 kısayol katmanı); hata kurtarma kuyruğu (Refused / uzun Pending onaylar — S3
  İstisna Kutusu ile birleşebilir).
- **FU-0178-GRN-01:** GRN → putaway otomatik tetikleyici (Q-M2-1 / Q-11).
- **FU-0178-QH-01 (MOD-0175 sahibi):** tutulan stoğun karantina bölgesine taşınması (QUARANTINE eklemeli).
- **FU-0178-SLOT-01:** putaway stratejisi, bin kapasitesi, slotting (veri yok).
- **FU-0178-WHSCOPE-01 (SEC-002):** kullanıcının depo bazlı veri kapsamı (yalnız atandığı depolar) — S1'de şirket kapsamı.
- **FU-0178-SEARCH-01:** görev numarasıyla Ctrl+K kaydı (`platform-global-search-registry`).
- **FU-0178-W5 (MVP-1 CT):** W-5 WP'si (§8.10).
- **FU-0178-MVP6:** Q-M6-1…9 cevaplarına göre WAREHOUSE-OUTBOUND eklemeli adım ve MOD-0183 uyumu.
- **FU-0178-OTEL:** exporter / canlı ölçüm.
- MOD-0180 dalga, MOD-0181 sayım (`COUNT_GAIN` / `COUNT_LOSS` — 0181'in işi), MOD-0182 barkod (ScanEvent, `scanRef` kapısını doldurur),
  MOD-0179 3PL (W-3).

## 21. Audited Events

Servisin denetim altyapısı **bugün var:** `tests/architecture/audit-ledger/Diten.InventoryService.md` `## İzler` →
`inventory-merkezi-iletim | b | işaret | ICentralAuditDeclaration`; `RequireEnlistment = true` ile kayıt komutun kendi Mongo
transaction'ında yazılır (yazılamazsa komut geri alınır → K2 kapalı-başarısız). Depo görevleri ve onaylar GxP / GDP stok yeri
kaydıdır → **K2 sınıfı**; yol b bu koşulu sağlar (test: denetim yazımı enjekte hata → komut geri alınır, görev değişmez).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki / sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `CreatePutawayTaskCommand` | `warehouse.putaway-task.created` | WarehouseTask | b (K2) | After: Number, kaynak kova, PlannedQuantity, ToLocationId, Assignee, Priority, Origin | — |
| `UpdatePutawayTaskCommand` | `warehouse.putaway-task.updated` | WarehouseTask | b | Before / After: ToLocationId, PlannedQuantity, Assignee, Priority, Note | — |
| `BulkAssignPutawayTaskCommand` | `warehouse.putaway-task.assigned` (görev başına bir kayıt) | WarehouseTask | b | Before / After: AssigneeUserId | — |
| `ConfirmPutawayTaskCommand` | `warehouse.putaway-task.confirmation-recorded` + `warehouse.putaway-task.confirmation-posted` / `.confirmation-refused` | WarehouseTask | b (K2) | Seq, Quantity, From / To, PostingKey, TransactionNumber ya da ErrorCode, Status | — (hareketin kendi kaydı defterde ayrıca) |
| `RetryPutawayConfirmationCommand` | `warehouse.putaway-task.confirmation-retried` | WarehouseTask | b (K2) | Seq, Attempts, sonuç | — (sweeper bu komutu çağırır; sweeper tetikleyicisi İ2) |
| `ClosePutawayTaskShortCommand` | `warehouse.putaway-task.closed-short` | WarehouseTask | b (K2) | Planned, Confirmed, ShortCloseReason | — |
| `CancelPutawayTaskCommand` | `warehouse.putaway-task.cancelled` | WarehouseTask | b (K2) | Before Status, CancelReason | — |
| `ClaimPutawayTaskCommand` | `warehouse.putaway-task.claimed` | WarehouseTask | b | Before / After: AssigneeUserId (null → kendisi) | — |
| `CreateWarehouseLocationRoleCommand` | `warehouse.location-role.created` | WarehouseLocationRole | b | After: LocationId, Roles, AcceptedStorageConditions | — |
| `UpdateWarehouseLocationRoleCommand` | `warehouse.location-role.updated` | WarehouseLocationRole | b | Before / After: Roles, AcceptedStorageConditions, Note | — |
| `DeleteWarehouseLocationRoleCommand` | `warehouse.location-role.deleted` | WarehouseLocationRole | b | Before: LocationId, Role | — |
| `BulkDeleteWarehouseLocationRoleCommand` | `warehouse.location-role.deleted` (kayıt başına) | WarehouseLocationRole | b | Before: LocationId, Role | — |
| Pending sweeper hosted service | — | — | — | — | İ2 — kendisi kayıt değiştirmez; çağırdığı `RetryPutawayConfirmationCommand` denetlenir |
| S2 `CreateOutboundShipmentCommand`, `CancelOutboundShipmentCommand`, `ConfirmPickTaskCommand`, `CreateHandlingUnitCommand`, `UpdateHandlingUnitCommand`, `ReleaseOutboundShipmentCommand` (ReadyToShip), `MarkOutboundShippedCommand` | `warehouse.outbound.*`, `warehouse.pick-task.*`, `warehouse.package.*` | — | b (K2) | S2 revizyonunda alan alan | — (iskelet; S2'de tam yazılır) |

Kayda yazılmayan alanlar: `ShipTo` adres / alıcı adı (S2, KVKK — yalnız outbound id ve alan adı "değişti" işareti), token, gizli
anahtarlar.

## 22. Record Codes

| Kayıt türü | Sınıf | TÜR öneki (öneri) | Üretici | Gerekçe |
|---|---|---|---|---|
| Putaway / Pick görevi (`WarehouseTask.Number`) | OTOMATİK | `PUT` (putaway), `PCK` (pick, S2) | geçici sayaç (Q-07 B, FU-0178-RCS-01) → BL-565 ortak servisi | yüksek hacimli, kullanıcı değiştirmez |
| Outbound sevkiyat (`OutboundNumber`, S2) | OTOMATİK | `OUT` | geçici sayaç → ortak servis | yüksek hacim |
| Koli (`PackageId`, S2) | OTOMATİK | `PKG` | geçici sayaç → ortak servis | yüksek hacim; WAREHOUSE-OUTBOUND `packageId` string |
| Lokasyon rolü | — | kod yok | — | kullanıcıya görünen kodu yok (LOCATION kodu gösterilir) |

Biçim `{ŞİRKET}-{TÜR}-{000001}`, sayaç (kiracı, şirket öneki, tür) başına, sıfırlanmaz, ASCII. Önekler RCS-001 §3 tablosuna henüz
yazılmadı (`.antigravity/rules/record-coding-standard.md` korunan yol — ayrı sahip onayı bekleyen açık iş FU-0178-RCS-02). Sahip kararı
Q-07 B (Beste 2026-10-21): ortak servis gelene kadar RCS biçiminde geçici sayaç (tek port; feragat ve geçiş FU-0178-RCS-01).
`{ŞİRKET}` öneki şimdilik MDM tüzel kişilik `Code`'u (M-2; `^[A-Z]{2,5}$` değilse görev açılmaz, 422 `WMS_COMPANY_CODE_INVALID`),
numaraya bir kez yazılır; RCS-001 §5.4 kısa kod alanı gelince geçiş FU-0178-RCS-01. Depo kodu numarada yok (liste sütunu).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | S1 çekirdek: görev + onay + lokasyon rolleri / saklama koşulları (backend), WMS-BUNDLE S1 dondurma | 3, 4.1–4.4, 4.6, 5, 8.1–8.8, 8.12–8.14, 10, 12, 13, 14, 21, 22; AC-1…AC-20, AC-37, AC-38, AC-40, AC-47…AC-49 |
| DL-2 | INT-WMS Gateway (integration-agent) | 15; AC-25 |
| DL-3 | S1 ekranlar + manifest + yardım | 8.15, 9, 11, 24; AC-20…AC-24, AC-39, AC-41…AC-46, AC-50 |
| DL-4 | S2a outbound + tahsis (W-5 sonrası, paket revizyonu ile) | 4.5, 8.10, 11.1 S2; AC-26, AC-28, AC-33 |
| DL-5 | S2b pick / pack / ReadyToShip / Shipped | 8.9; AC-27, AC-29…AC-32, AC-35 |
| DL-6 | S2 ekranları (compact Outbound, slim PickTasks, Paketleme) | 11.1 S2; AC-34 |
| DL-7 | S3 izleme ekranları | 11.1 S3; AC-36 |

## 24. Page Help (UXH-001)

- `PUTAWAYTASKS` → `Help.PUTAWAYTASKS.Purpose`, `.Audience`, `.Step1Title`/`.Step1` … `.Step4` (1: yerleştirilecek stoğu seç,
  2: hedef bin ve atanan, 3: onayla (kısmi olabilir), 4: Pending onayda "Yeniden dene"); `.Immediate` ("Onay stoğu hemen taşır;
  geri alma Stok Hareketleri'nde ters kayıttır"); ilgili sayfalar `Help.PUTAWAYTASKS.Related.WAREHOUSELOCATIONROLES`, stok defteri.
- `WAREHOUSELOCATIONROLES` → `Purpose`, `Audience`, `Step1..3` (lokasyon seç, rol ver, mal kabul lokasyonlarını doğrula);
  ilgili `Related.PUTAWAYTASKS`.
- Kiracı modülü → 7 dil. **Tur yok** (beş akıştan biri değil).

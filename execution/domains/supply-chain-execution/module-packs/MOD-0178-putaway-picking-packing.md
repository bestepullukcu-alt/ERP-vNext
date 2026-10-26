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
status_note: "draft — P0 (MOD-0178 pack + WMS-BUNDLE taslağı), MVP-1 CT (W-10) için 2026-10-21. Sahip kararları W-1 (Build), W-2 (Diten.InventoryService 5067, ayrı bounded context, ikinci bakiye yok), W-3 (MOD-0179 ertelendi), W-4 (GOODS_ISSUE_SALES MOD-0183'ün), W-5 (lot / lokasyon rezervasyonu MVP-1 CT'den), W-10, E-1, E-2, E-6 bağlayıcı (§19.1). Paket soruları Q-01…Q-09, Q-11…Q-13, Q-MDM-1, Q-MDM-2 ve mockup incelemesi kararları M-1…M-6 sahip kararıyla kapandı (Beste 2026-10-21, §19.1.1, §19.1.2). Bağımsız inceleme 01 (hedef 0cb3940af, KOŞULLU) bulguları F-1…F-16 işlendi (§19.1.3); inceleme 02 (hedef 5f7833253, KOŞULLU) bulguları F-1…F-10 işlendi (§19.1.4); inceleme 03 (hedef 367e8ff78, KOŞULLU, ENGEL yok) bulguları F-1…F-5 + inceleme 02 F-5 kalanı işlendi (§19.1.5); inceleme 04 (hedef 13d343c9a, KOŞULLU) bulguları F-1…F-4 işlendi (§19.1.6); inceleme 05 ön kontrolü (hedef 1f185d6a4, KOŞULLU, bağımsız değil) bulguları F-1…F-5 + akış tablosu işlendi (§19.1.7); bağımsız inceleme 05 (hedef 0f0cf322c, oturum d2ae856a, KOŞULLU, ENGEL yok) bulguları F-1…F-7 işlendi (§19.1.8); bağımsız inceleme 06 (hedef d86c1123e, oturum 0e2faf59, KOŞULLU, ENGEL yok) bulguları F-1…F-6 işlendi (§19.1.9); bağımsız inceleme 07 (hedef 6b6af2664, oturum 6217bf78, KOŞULLU, ENGEL yok) bulguları F-1…F-5 işlendi (§19.1.10); bağımsız inceleme 08 (hedef 58c2cd6e6, oturum 55a37ec9, KOŞULLU, ENGEL yok) bulguları F-1…F-5 işlendi — F-1 sahip kararıyla: BL-501 olduğu gibi, merkezi denetim yalnız başarılı komutlarda (§19.1.11); bağımsız inceleme 09 (hedef 963f6230a, oturum 88fcdff2, KOŞULLU, ENGEL yok) bulguları F-1…F-5 işlendi (§19.1.12); bağımsız inceleme 10 (hedef b456a9b22, oturum c90bad53, KOŞULLU, ENGEL yok) bulguları F-1…F-3 işlendi — yaratma dışı komutların istek idempotency'si, 0-satır (D) dalı, AC-15 sayımları (§19.1.13); bağımsız inceleme 11 (hedef ed521029f, oturum 708cc355, KOŞULLU, ENGEL ve ORTA yok) bulguları F-1…F-3 sahip kararıyla işlendi — onay cevabında `seq`, anahtar kapsamı (kullanıcı + operasyon), yaratmada her yinelenen kayıt hatasında anahtar araması, (D) tükenmesi (§19.1.14); bağımsız inceleme 12 (hedef 1b4350fe6, oturum 771fbbcf, KOŞULLU) F-1…F-4 ve Codex incelemesi (sahip tarafından, CONDITIONAL PASS) P1 / P2 sahip kararıyla işlendi — `CreatedBy` alanı, seri slot replay'i, `RequestOp` ölçümü, WMS `details.seq` / `ConfirmPutawayTaskResult`, `pack_approval.slices()` uyumlu teslim dilimleri (DL-1…DL-8), commit sonucu bilinmeyen posting → Pending (§19.1.15); bağımsız inceleme 13 (hedef 53afff318, oturum 075041e2, KOŞULLU, ENGEL ve ORTA yok) F-1…F-3 sahip kararıyla işlendi — eşleme notları, iptal, uzun Pending rozeti (bilinçli kabul; deneme sınırı yok) (§19.1.16); bağımsız inceleme 14 (hedef ac2971beb, oturum c4e41a9c, KOŞULLU, ENGEL ve ORTA yok) F-1…F-3 sahip kararıyla işlendi — bayrak öncelik sırası, S3 istisna türü adı, `LongPendingMinutes` açılış çapraz doğrulaması (§19.1.17). Onay yok; açık sorular yalnız diğer ekiplerde + Q-10 (§19.5). golden_reference / form_field_count yalnız S1 ekranları içindir (§9); S2 outbound ekranı compact olacak ve S2 paket revizyonunda screens listesine girer."
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
> `execution/registries/module-id-registry.md:298` (`47a2c1cb9`'da; `d86c1123e`'de aynı satır :304'te) (`MOD-0178 | Putaway / Picking / Packing | putaway-picking-packing | Module |
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
| **S1 — Yerleştirme (putaway)** | Lokasyon rolleri (Receiving); yerleştirilecek stok listesi (INVENTORY bakiyesinden okunur, saklanmaz); elle / listeden putaway görevi; atama; kısmi ve tam onay → `LOCATION_TRANSFER`; kısa kapatma; iptal; idempotent onay + Pending kurtarma; görev zaman çizelgesi; manifest + nav; ekranlar `PutawayTasks`, `WarehouseLocationRoles` | WMS-BUNDLE v1.0.0 (S1 operasyonları + WAREHOUSE-OUTBOUND 1.0.0 aynen) | Donmuş sözleşmelerle yapılabilir **bir istisnayla:** saklama koşulu uyumu (M-3) ürün koşullarını PRODUCT-MASTER **v1.1** `storageConditions`'tan okur — v1.1 hâlâ DRAFT (`1.1.0-draft.16`) ve InventoryService bu alanı bugün okumuyor (MOD-0173 seam, §5). S1 kabul ortamı: dev PRODUCT-MASTER köprüsü 4010 (veri setine `storageConditions` eklenerek) ile E3 / E4; **canlı kabul PM-LIVE sonrası** (bağımlılık). Görev numarası geçici RCS sayacından (Q-07 B), önek MDM `Code` (M-2). Seri kontrollü SKU yalnız tam miktar (F-1, §8.16) | §16 AC-1…AC-24, AC-37…AC-70 (AC-25 INT-WMS'te) |
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
| `WarehouseLocationRoleEvent` | S1 | `wms_location_role_events` | Insert-only rol geçmişi: rol komutlarının önceki / sonraki değerleri (merkezi kayıt gövde taşımaz; bağımsız inceleme 09 F-1; sahip kararı Beste 2026-10-26: kabul, okuma ekranı sonra) |
| `WarehouseLocationRole` | S1 (Receiving + saklama koşulları), S2 (Staging) | `wms_location_roles` | LOCATION düğümü başına tek kayıt: roller + kabul edilen saklama koşulları; soft delete |
| `OutboundShipment` | S2 | `wms_outbound_shipments` | WAREHOUSE-OUTBOUND kaynağı |
| `HandlingUnit` (Package) | S2 | `wms_handling_units` | Koli; içerik satırları; ağırlık / ölçü elle |
| Outbox olayları | S1 (iç), S2 (`OutboundShipmentReady`) | servisin mevcut outbox'ı | Taşıma (bus) bugün kapalı → Pending (§8.9) |
| ScanEvent | — | — | **MOD-0182'ye bırakılır**; 0178 yalnız opak `scanRef` saklar |

**Komutlar (S1):** `CreatePutawayTaskCommand`, `UpdatePutawayTaskCommand`, `BulkAssignPutawayTaskCommand`,
`ClaimPutawayTaskCommand`, `ConfirmPutawayTaskCommand`, `RetryPutawayConfirmationCommand`, `ClosePutawayTaskShortCommand`,
`CancelPutawayTaskCommand`, `MarkPutawayConfirmationReturnedCommand`, `ResolvePutawayIntegrityCommand`,
`ReconcilePendingConfirmationCommand` (yalnız sweeper), `CreateWarehouseLocationRoleCommand`,
`UpdateWarehouseLocationRoleCommand`, `DeleteWarehouseLocationRoleCommand`,
`BulkDeleteWarehouseLocationRoleCommand`.

**Sorgular (S1):** `GetPutawayTaskListQuery`, `GetPutawayTaskByIdQuery`, `GetPutawayCandidateListQuery`,
`GetPutawayTaskEventListQuery`, `GetWarehouseLocationRoleListQuery`, `GetWarehouseLocationRoleByIdQuery`.

**API (S1, taban `/api/warehouse`, WMS taslağı §4):**
`GET/POST /putaway-tasks` · `GET/PUT /putaway-tasks/{taskId}` · `POST /putaway-tasks/bulk-assign` · `POST /putaway-tasks/{taskId}/claim` ·
`POST /putaway-tasks/{taskId}/confirmations` · `POST /putaway-tasks/{taskId}/confirmations/{seq}/retry` ·
`POST /putaway-tasks/{taskId}/confirmations/{seq}/returned` · `POST /putaway-tasks/{taskId}/confirmations/{seq}/resolve-integrity` ·
`POST /putaway-tasks/{taskId}/close-short` · `POST /putaway-tasks/{taskId}/cancel` · `GET /putaway-tasks/{taskId}/events` ·
`GET /putaway-candidates` · `GET/POST /location-roles` · `GET/PUT/DELETE /location-roles/{roleId}` ·
`POST /location-roles/bulk-delete` · donmuş `GET /outbound-shipments`, `GET /outbound-shipments/{outboundId}` (S1'de boş liste /
404; S2 doldurur).

**Frontend rotaları (S1):** `/Warehouse/PutawayTasks`, `/Warehouse/LocationRoles`.

**İzinler:** §14.

## 4. Entity Fields

Tüm varlıklar InventoryService'in `EntityBase`'inden türer: `Id`, `TenantId`, `LegalEntityId`, `CreatedAt`, `UpdatedAt`, `Version` (int),
`IsDeleted`, `DeletedAt` (`…/Domain/Entities/EntityBase.cs`; bağımsız inceleme 12 F-1 ile düzeltildi). **Kullanıcı alanı `EntityBase`'de
yoktur**: gereken varlık kendi alanını taşır (emsal `QuarantineHold.CreatedBy`, `…/Domain/Quarantine/Entities/QuarantineEntities.cs:47`) —
0178'de `WarehouseTask.CreatedBy` (§4.1) ve `WarehouseLocationRole.CreatedBy` (§4.4).
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
| `SerialControlled` | bool | görev açılırken ledger ile **aynı kural** (`MovementMasterDataValidator.cs:144-149`, OD-10): Lsku / FinishedGood / Item'da `getSku.serialControlled`; **Gsku'da ürünün bayrağı** (`getProduct.serialControlled`) — Gsku'da yalnız ürün bayrağı açıkça `false` ise görev açılır (seri SKU değildir), `true` / bilinmiyorsa 422 `SKU_LEVEL_NOT_STOCKABLE` (ledger'ın kodu; seri stok Lsku / FinishedGood'da tutulur). Lsku / FinishedGood'da bilinmiyorsa görev açılmaz (`PRODUCT_MASTER_DATA_INCOMPLETE`) (inceleme 02 F-3) |
| `SerialIds` | — | görevde **tutulmaz**; seri kontrollü SKU'da onay anında kaynak kovanın bütün serileri sistemce okunur ve onaya yazılır (§8.16, F-1) |
| `StockStatus` | enum | kaynak kovanın durumu; S1'de yalnız `AVAILABLE` (§8.4) |
| `FromLocationId` | string | Receiving rolü altındaki stoklanabilir lokasyon |
| `ToLocationId` | string \| null | planlanan hedef bin; onayda zorunlu; saklama koşulu uyumu (§8.12) |
| `SuggestedToLocationId` | string \| null | sunucu önerisi (§8.6), yalnız bilgi; otomatik doldurmaz |
| `TargetSource` | enum `Manual \| SuggestionApplied` | hedef "Uygula" ile öneriden alındıysa `SuggestionApplied` → zaman çizelgesine `SuggestionApplied` olayı (M-4) |
| `PlannedQuantity` | decimal string | > 0; yaratmada açık miktarı aşamaz (§12); **seri kontrollü SKU'da = kovadaki bütün miktar** ve değiştirilemez (§8.16) |
| `BaseUomId` | string | bakiye satırından |
| `ConfirmedQuantity` | decimal string | Posted onayların toplamı (görev içi türetme, stok bakiyesi değil); `Status` bundan türer (M-4). **Kural:** `ConfirmedQuantity ≤ PlannedQuantity` (işlem 2'nin koşullu artışı). **Tek istisna (inceleme 05 F-1):** "Uzlaştır-a" defter gerçeğini yazar — o zaman `ConfirmedQuantity > PlannedQuantity` olabilir; görev `Completed` olur ve olay `ConfirmationDeviation` (`OverPlanned`) açılır. Defter doğrudur, görev ona uyar |
| `IntegrationFlag` (türetilmiş, saklanmaz) | enum `None \| PostingPending \| PostingPendingLong \| PostingRefused \| PostingIntegrityError` | durumdan **ayrı** rozet: "Onay bekliyor" (Pending onay var) / "Uzun süredir bekliyor" (Pending onay `ConfirmedAtUtc`'den bu yana `LongPendingMinutes`'tan uzun; `PostingPending` yerine; §8.3 adım 5, bağımsız inceleme 13 F-3) / "Stok reddetti" (son onay Refused) / "Bütünlük hatası" (onay `IntegrityError`, inceleme 03 F-2) (M-4). **Tek değer, görev düzeyinde öncelik (bağımsız inceleme 14 F-1):** görevin onayları arasında en yüksek öncelikli durum gösterilir — `PostingIntegrityError` > `PostingPendingLong` > `PostingPending` > `PostingRefused` > `None` (ör. aynı görevde Pending ve `IntegrityError` onay varsa "Bütünlük hatası") |
| `Confirmations[]` | §4.2 | |
| `AssigneeUserId` | uuid \| null | atanan operatör; sorumlu atar (kullanıcı listesi ucu varsa, Q-13) ya da operatör atanmamış görevi **üstlenir** (kendine atar, M-5) |
| `Priority` | enum `Low \| Normal \| High` | varsayılan `Normal` |
| `Note` | string ≤ 1000 \| null | |
| `ShortCloseReason` | string 10–1000 \| null | `ClosePutawayTaskShort` |
| `CancelReason` | string 10–1000 \| null | |
| `Version` | int (`EntityBase`) | If-Match `W/"<version>"` |
| `IdempotencyKey` | string 8–100 | yaratma tekilliği **kullanıcı kapsamında** `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)` — `CreatedBy` = isteği yapanın `ActorUserId`'si (0175 / 0176 / 0177 emsali: `QuarantineIndexConfiguration.cs:51` `CreatedBy`, `ExpiryIndexConfiguration.cs:64` `OpenedBy`, `RecallIndexConfiguration.cs:84` `OwnerUserId`); başka kullanıcının aynı anahtarı ayrı anahtardır (bağımsız inceleme 11 F-2) |
| `CreatedBy` | uuid | isteği yapanın `ActorUserId`'si; **yalnız yaratmada** yazılır, sonra güncellenmez — yaratma anahtarının kapsamı (§4.6; bağımsız inceleme 12 F-1) |
| `RequestFingerprint` | string | yaratma isteğinin parmak izi (alanları §8.1); aynı anahtar + farklı parmak izi → 400 `IDEMPOTENCY_KEY_REUSED` (bağımsız inceleme 10 F-1). Yaratma dışı komutların anahtarı görevde değil, yazdıkları olayda saklanır (§4.3) |
| `CorrelationId` | uuid | ilk isteğin |

### 4.2 `TaskConfirmation` (gömülü)
| Alan | Kural |
|---|---|
| `Seq` | 1'den artan |
| `Quantity` | > 0, ≤ kalan (`Planned − Confirmed − Pending`); seri kontrollü SKU'da = planlanan (kısmi yok, §8.16) |
| `SerialIds` | seri kontrollü SKU'da işlem 1'de kovadan okunup yazılır (adet = miktar); her denemede aynen gönderilir |
| `TransactionDate`, `PostingDate` | **işlem 1'de** yazılır (fiziksel hareket zamanı, sunucu UTC — E-6) ve her denemede aynen gönderilir: ledger idempotency izi tarihleri kapsar (`RequestFingerprint.cs:42-43`) (F-2) |
| `ToLocationId` | zorunlu; §12 hedef kuralları |
| `DeviationReason` | 10–1000; hedef planlanandan farklıysa zorunlu |
| `Status` | `Pending \| Posted \| Refused \| IntegrityError` — `IntegrityError`: ledger aynı `PostingKey` ile **farklı** bir hareket işlemiş (`IDEMPOTENCY_KEY_REUSED`); Refused değildir; Superseded / Returned ve görev eylemleri kapalı, istisna olarak listelenir (inceleme 02 F-1); tek çıkış sorumlunun **"Uzlaştır"** komutudur (`ResolvePutawayIntegrityCommand`, §8.3, inceleme 03 F-2) |
| `AttemptId`, `InFlightUntilUtc` | **uçuş kilidi** (inceleme 03 F-1): işlem 1'de ve her yeniden deneme başında atomik alınır (koşul: `Status == Pending` ve `InFlightUntilUtc` boş ya da geçmiş); süre = `Warehouse:Putaway:InFlightLeaseSeconds` (öneri 120 sn). **Dayanak (inceleme 04 F-2):** hareket yazımı (işlem 3) süreç içidir; üst sınırı açık bir **posting süresi** `Warehouse:Putaway:PostingTimeoutSeconds` (öneri 60 sn) — işlem 3 bağlı bir `CancellationTokenSource` ile bu süreyle sınırlanır; içindeki her upstream çağrısı ayrıca 5 sn ile sınırlı (`…/Infrastructure/DependencyInjection.cs:51`, `…/Api/appsettings.json:35` `UpstreamServices:TimeoutSeconds`). Açılışta `InFlightLeaseSeconds ≥ PostingTimeoutSeconds + 30` doğrulanır, değilse host başlamaz (fail-closed ayar). Aynı biçimde `Warehouse:Putaway:LongPendingMinutes > 0` ve `LongPendingMinutes × 60 > InFlightLeaseSeconds` doğrulanır, değilse host başlamaz — rozet ilk denemenin kilidi dolmadan çıkmaz (§8.3 adım 5; bağımsız inceleme 14 F-3). Posting süresi aşılırsa onay **Pending kalır** ve kilit **`InFlightUntilUtc`'ye kadar korunur** — hemen bırakılmaz (inceleme 05 F-3): iptal istemci tarafındadır, sunucuya ulaşmış bir commit (`InventoryLedgerRepository.cs:173` `WithTransactionAsync`) yine uygulanabilir; +30 sn pay bu görünürlük gecikmesini ve Mongo işlem ömrünü kapsamak içindir (sunucu `transactionLifetimeLimitSeconds` varsayılanı 60 sn — dokümantasyon bilgisi, ölçülmedi). Kilit dolduktan sonraki ilk deneme ledger'a bakar. Kilit ve posting süreleri **`TimeProvider`** üzerinden ölçülür (0175 emsali `QuarantineServiceCollectionExtensions.cs:36`); testler sahte saatle süreyi doldurur, gerçek ayarlar ve açılış doğrulaması değişmez (inceleme 05 F-2). İşlem 2 bitince kilit boşaltılır. Kilit geçerliyken yeniden deneme 409 `WMS_CONFIRMATION_IN_FLIGHT`, sweeper kaydı atlar |
| `IntegrityResolution` | `null \| ResolvedOwnMovement \| ReturnedToRefused` + `ResolvedBy`, `ResolvedAtUtc`, `ResolveReason` (10–1000), `HolderMovement { transactionId, transactionNumber, sourceModule, sourceType, sourceDocumentId, sourceLineId, quantity, uomId, baseQuantity, fromWarehouseId, fromLocationId, toWarehouseId, toLocationId, lotId, serialIds, fromStockStatus, toStockStatus, postingDate, reversedByTransactionId? }` — §5 port alanlarıyla birebir (bağımsız inceleme 05 F-5); "Uzlaştır" sonucu (inceleme 04 F-1) |
| `PostingKey` | `wms:put:{taskId}:{seq}` — INVENTORY Idempotency-Key; yeniden denemede **aynı** anahtar. İstemcinin istek anahtarından (`Idempotency-Key` başlığı) **ayrıdır**: istek anahtarı onayda değil, komutun yazdığı olayda saklanır (§4.3, §8.1; bağımsız inceleme 10 F-1) |
| `TransactionId`, `TransactionNumber` | Posted'da INVENTORY'den |
| `ErrorCode`, `ErrorMessage`, `ErrorDetails`, `ErrorStatus` | Refused'da INVENTORY'nin (ya da yeniden deneme ön kontrolünün) cevabı aynen; HTTP durumu da saklanır |
| `RefusalSource`, `RefusedOnRetry`, `RefusedAtUtc` | Refused'ın kaynağı **kayıtta** (bağımsız inceleme 06 F-2): `RefusalSource = PreCheck \| Ledger4xx \| Resolve` (Resolve = Uzlaştır-b'nin geri dönüşü), `RefusedOnRetry` (Refused bir yeniden denemede mi yazıldı), `RefusedAtUtc` (sunucu UTC). Savunma dalı yalnız `RefusalSource ∈ {PreCheck, Ledger4xx}` **ve** `RefusedOnRetry = true` **ve** `RefusedAtUtc ≥ şimdi − 30 dk` olanları tarar; hata koduna bakarak çıkarım yapılmaz |
| `Resolution` | `null \| SupersededByNewConfirmation \| ReturnedToSource` — Refused onayın operatör kapanışı (§8.3, F-4); `ReturnedToSource` gerekçe 10–1000 ister (`ResolutionReason`). Onay geç commit nedeniyle `IntegrityError`'a geçerse `Resolution` **`PriorResolution`**'a taşınır ve boşaltılır (`IntegrityError` + `Resolution` birlikte bulunmaz; bağımsız inceleme 05 F-1) |
| `Channel` | `Ui \| Api \| External` (W-3 kapısı) |
| `ScanRef` | string ≤ 200 \| null — MOD-0182 kapısı, opak |
| `ConfirmedBy`, `ConfirmedAtUtc`, `Attempts`, `LastAttemptAtUtc` | |

### 4.3 `WarehouseTaskEvent` (insert-only)
`TaskId, Seq, Kind (Created | Updated | Assigned | Claimed | SuggestionApplied | ConfirmationRecorded | ConfirmationPosted |
ConfirmationRefused | ConfirmationRetried | ConfirmationReconciled | ConfirmationIntegrityError | IntegrityResolved | ConfirmationDeviation |
ConfirmationSuperseded | ConfirmationReturned | ClosedShort |
Completed | Cancelled), Actor { userId, name? } | system, AtUtc, Data { … }, CorrelationId, IdempotencyKey?, RequestOp?, RequestFingerprint?`.

**İstek anahtarı — "anahtar olayı" (bağımsız inceleme 10 F-1; 0176 / 0177 emsali, §8.1):** yaratma dışındaki her yazma komutu
`Idempotency-Key`'ini ve isteğin parmak izini (`RequestFingerprint`, alanları §8.1) **kendi ilk iş yazımının yerel olayına** yazar; o
olay iş yazımıyla aynı işlemdedir. Olay ayrıca komutun operasyon adını taşır (`RequestOp` = WMS `operationId`'si, ör.
`updatePutawayTask`, `bulkAssignPutawayTasks`, `deleteLocationRole`, `bulkDeleteLocationRoles`): anahtarın kapsamı kiracı + şirket +
kullanıcı + `RequestOp` + hedeftir; başka operasyonda aynı anahtar ayrı anahtardır (bağımsız inceleme 11 F-2). Komut → anahtar olayı: düzenle → `Updated`; üstlen → `Claimed`; toplu atama → yazılan **her** görevin
`Assigned`'ı; onay → işlem 1'in `ConfirmationRecorded`'ı; yeniden dene → kilit alımının `ConfirmationRetried`'ı; "Geri taşındı" →
`ConfirmationReturned`; Kalanı kapat → `ClosedShort`; İptal → `Cancelled`; Uzlaştır → `IntegrityResolved`. Aynı komutun sonraki
olayları (ör. `ConfirmationPosted`, `ConfirmationSuperseded`, `Completed`), ön aramanın 409'da yazdığı `ConfirmationIntegrityError` (komutun
kendi yazımı yoktur, satır 20) ve sistem yazımları (sweeper, savunma dalı) anahtar taşımaz — `IdempotencyKey` / `RequestOp` / `RequestFingerprint` boş
(üye yok). Hiç yazmadan biten istek (doğrulama / yetki / iş kuralı reddi) anahtar bırakmaz; aynı anahtarla yeniden gönderimi baştan
değerlendirilir (emsal davranış). Alanlar depoda tutulur, `GET …/events` kablosuna yazılmaz (WMS taslağı §3.4).

**Her `Kind`'ı yazan yazım (bağımsız inceleme 09 F-2 d, tek yer):** olay, yazdığı iş yazımıyla **aynı işlemde** ve yalnız o yazım en az
bir kayıt etkilediyse yazılır (0 kayıtta olay yok, §8.8). `Data` merkezi kayıtta olmayan değerleri taşır (§21).

| Kind | Yazan yazım | `Data` |
|---|---|---|
| `Created` / `Updated` | görev yaratma / düzenleme | After ya da Before / After: Number, kaynak kova, PlannedQuantity, ToLocationId, AssigneeUserId, Priority, Note, Origin |
| `SuggestionApplied` | aynı yaratma / düzenleme yazımı, `TargetSource` o yazımda `SuggestionApplied` olursa (§8.6) | önerilen ve seçilen bin |
| `Assigned` | toplu atamanın o göreve yazımı (görev başına bir satır) | Before / After: AssigneeUserId |
| `Claimed` | üstlenme | Before / After: AssigneeUserId |
| `ConfirmationRecorded` | işlem 1 | onay `seq`, Quantity, From / To, PostingKey, TransactionDate, SerialIds, DeviationReason, Channel |
| `ConfirmationRetried` | yeniden denemenin kilit alımı | onay `seq`, `attempt` |
| `ConfirmationPosted` | işlem 2 / retry (i) / retry (ii) Posted yazımı (CAS yinelemesinde bir kez) | onay `seq`, TransactionId / Number, kapatılan `seq` listesi |
| `ConfirmationReconciled` | sweeper'ın Posted yazımı (bu yazımda `ConfirmationPosted` yazılmaz) | onay `seq`, TransactionId / Number, kapatılan `seq` listesi |
| `ConfirmationRefused` | defter 4xx ya da ön kontrol reddi yazımı | onay `seq`, Error\*, Ret\* |
| `ConfirmationIntegrityError` | `IntegrityError` yazan her yazım (işlem 2, retry, sweeper, savunma dalı, kapatıcı komutun ön araması) | onay `seq`, `reason`, Error\*, `holder` (varsa), `PriorResolution` |
| `IntegrityResolved` | Uzlaştır | onay `seq`, IntegrityResolution, gerekçe, `holder`, kapatılan `seq` listesi |
| `ConfirmationDeviation` | Uzlaştır-a yazımı; defter değerleri onaydan farklıysa ya da toplam planlananı aşarsa | farklı alanlar, `OverPlanned` / `PostedAfterCancel` |
| `ConfirmationSuperseded` | Posted yazan yazım (akış satırı 2 / 3 / 4 / 13 / 23 / 25); kapatılan **her** Refused onay için bir satır | kapatılan ve kapatan `seq` |
| `ConfirmationReturned` | "Geri taşındı" | onay `seq`, ResolutionReason |
| `Completed` | görevin türetilmiş durumunu onaylanan miktarla `Completed` yapan Posted yazımı (akış satırı 2 / 3 / 4 / 13 / 25); terminal görevde (satır 23) ve Kalanı kapat'ta **yazılmaz** | Planned, Confirmed |
| `ClosedShort` / `Cancelled` | Kalanı kapat / İptal | Planned, Confirmed, ShortCloseReason / Before Status, CancelReason |

**`WarehouseLocationRoleEvent`** (insert-only, `wms_location_role_events`; bağımsız inceleme 09 F-1; sahip kararı Beste 2026-10-26): `RoleId, Seq, Kind (Created |
Updated | Deleted), Actor, AtUtc, Data { Before / After: LocationId, Roles, AcceptedStorageConditions, Note }, CorrelationId,
IdempotencyKey?, RequestOp?, RequestFingerprint?`. Rol komutunun yazımıyla aynı işlemde yazılır; toplu silmede silinen kayıt başına bir satır.
**Anahtar olayı (bağımsız inceleme 10 F-1):** düzenle → `Updated`, sil → `Deleted`, toplu sil → silinen **her** kaydın `Deleted`'ı
istek anahtarını ve parmak izini taşır; rol yaratmanın anahtarı görevdeki gibi kayıttadır (§4.4). Merkezi kayıt rolün değerlerini taşımadığı için
önceki / sonraki değerlerin tek yeri budur. S1'de okuma ucu ve ekranı yoktur (denetim okuması veritabanından).

### 4.4 `WarehouseLocationRole`
| Alan | Kural |
|---|---|
| `WarehouseId` | LOCATION Warehouse; düğümün yolunda olmalı (`getLocationPath`) |
| `LocationId` | LOCATION düğümü, tip `StorageLocation \| Zone \| Bin`, `Active`; kayıt düğümün **stoklanabilir alt ağacına** uygulanır |
| `Roles` | küme, 0..n: `Receiving` (S1), `Staging` (S2); S2'de belki `Packing`. Bir düğüm birden çok role sahip olabilir; **Receiving + Staging birlikte olamaz** (M-5) |
| `AcceptedStorageConditions` | küme, 0..n, PRODUCT-MASTER v1.1 `StorageConditionCode` kodları (`AMBIENT_15_25, COOL_2_8, FROZEN_BELOW_MINUS_20, PROTECT_FROM_LIGHT, PROTECT_FROM_MOISTURE` — `product-master-bundle.openapi.yaml:601-604`); en çok bir sıcaklık kodu (PRODUCT-MASTER kuralının aynısı, `:681-682`); alt ağaca kalıtım (§8.12) |
| `Note` | ≤ 500 \| null |
| `Version` | If-Match |
| `CreatedBy` | uuid; isteği yapanın `ActorUserId`'si; **yalnız yaratmada** yazılır, sonra güncellenmez — rol yaratma anahtarının kapsamı (§4.6; bağımsız inceleme 12 F-1) |
| `IdempotencyKey`, `RequestFingerprint` | rol yaratma isteğinin anahtarı (8–100) ve parmak izi — tekil `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)` (kullanıcı kapsamında, §4.1 deseni; bağımsız inceleme 11 F-2); aynı anahtar + aynı parmak izi → 200 replay (409 `WMS_LOCATION_ROLE_EXISTS` değil), farklı → 400 `IDEMPOTENCY_KEY_REUSED` (bağımsız inceleme 10 F-1); eşzamanlı iki özdeş yaratmada kaybeden, yinelenen kaydı hangi tekil index'ten (anahtar ya da `LocationId`) alırsa alsın önce anahtarı yeniden arar (§8.1; bağımsız inceleme 11 F-3) |
| Kural | `Roles` ve `AcceptedStorageConditions`'tan en az biri dolu; açık görevi olan düğümden `Receiving` kaldırılamaz / kayıt silinemez (409 `WMS_LOCATION_HAS_OPEN_TASKS`, M-5) |
| Rol kalıtımı (F-13) | `Receiving` (S2'de `Staging` de) düğümün **stoklanabilir alt ağacına kalıtılır**: kaynak uygunluğu ve "hedef Receiving / Staging olamaz" kuralı etkin (kalıtılmış dahil) role bakar. **Atalar arası çakışma:** atası `Receiving` olan düğüm `Staging` olamaz, atası `Staging` olan düğüm `Receiving` olamaz (ve tersi: alt ağacında karşı rol taşıyan düğüme rol verilemez) → 409 `WMS_LOCATION_ROLE_INHERITED_CONFLICT` |
| Tekillik | `(TenantId, LegalEntityId, LocationId)` canlı kayıtlarda tekil — düğüm başına tek kayıt; bu index'ten yinelenen kayıt hatasında önce istek anahtarı yeniden aranır (bulunursa replay), anahtar yoksa 409 `WMS_LOCATION_ROLE_EXISTS` (§8.1; bağımsız inceleme 11 F-3) |

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
- İstek idempotency'si S1 kuralıyla (§8.1): yaratmanın anahtarı ve parmak izi kayıtta, diğer komutlarınki anahtar olayında; alanlar S2
  revizyonunda kesinleşir.

### 4.6 Indexes (kiracı + şirket önce)
- `wms_tasks`: tekil **`(TenantId, Number)`** — kiracı genelinde (RCS-001 §6; numara kimliktir, şirketler arası transferde de çakışmaz);
  tekil kısmi `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)` where `IdempotencyKey` string — kullanıcı kapsamında (`QuarantineIndexConfiguration.cs:51` `CreatedBy`, `ExpiryIndexConfiguration.cs:64` `OpenedBy`, `RecallIndexConfiguration.cs:84` `OwnerUserId`; bağımsız
  inceleme 11 F-2);
  `(TenantId, LegalEntityId, Kind, Status, WarehouseId)`; `(TenantId, LegalEntityId, FromLocationId, SkuId, LotId, Status)`
  (açık miktar hesabı); `(TenantId, LegalEntityId, AssigneeUserId, Status)`; `(TenantId, Confirmations.PostingKey)` (kurtarma).
- `wms_tasks` (seri SKU, inceleme 02 F-6): tekil kısmi `(TenantId, LegalEntityId, WarehouseId, FromLocationId, SkuId, SkuLevel, LotId)`
  where `OpenSerialSlot = true` — `OpenSerialSlot` seri SKU görevinde `Open` / `InProgress` iken `true`, `Completed` / `Cancelled`'da
  kaldırılır (kısmi index'te yalnız eşitlik filtresi). **Farklı anahtarlı** eşzamanlı iki seri görev yaratmasından biri bu index'ten
  yinelenen kayıt alır → 422 `WMS_QUANTITY_EXCEEDS_OPEN`; aynı kullanıcı + aynı anahtar + aynı gövde ise kaybeden önce anahtarı yeniden
  arar ve 200 replay alır (§8.1; bağımsız inceleme 12 F-2).
- `wms_tasks` (sweeper taramaları, bağımsız inceleme 07 F-4): normal dal `("Confirmations.Status", "Confirmations.LastAttemptAtUtc",
  TenantId, _id)`; savunma dalı `("Confirmations.RefusedOnRetry", "Confirmations.RefusedAtUtc", TenantId, _id)` (aynı dizinin alanları —
  çok anahtarlı bileşik index'e izinli; dokümantasyon bilgisi, geliştirici kurulumda ölçer).
- `wms_task_events`: `(TenantId, TaskId, Seq)` tekil; **istek anahtarı** (bağımsız inceleme 10 F-1, 11 F-2): tekil kısmi
  `(TenantId, TaskId, ActorUserId, RequestOp, IdempotencyKey)` where `IdempotencyKey` string (`$type: string`) — 0176 / 0177 emsali
  (`…/Persistence/Expiry/ExpiryIndexConfiguration.cs:78-80`, `…/Persistence/Recall/RecallIndexConfiguration.cs:94-96`); emsalden tek
  fark `RequestOp`'tur: 0178'de tekil ve toplu komutlar aynı olay koleksiyonuna yazar, operasyonlar birbirinin anahtarını görmez. Toplu
  atama araması için tekil olmayan kısmi `(TenantId, LegalEntityId, ActorUserId, RequestOp, IdempotencyKey)` (aynı filtre); arama
  yalnız `RequestOp = bulkAssignPutawayTasks` olaylarını alır.
- `wms_location_role_events`: `(TenantId, RoleId, Seq)` tekil (bağımsız inceleme 09 F-1); tekil kısmi `(TenantId, RoleId, ActorUserId,
  RequestOp, IdempotencyKey)` ve toplu silme araması için tekil olmayan kısmi `(TenantId, LegalEntityId, ActorUserId, RequestOp,
  IdempotencyKey)` (aynı filtre; arama yalnız `RequestOp = bulkDeleteLocationRoles` olaylarını alır — tekil silmenin `Deleted` olayları
  sayılmaz; bağımsız inceleme 10 F-1, 11 F-2).
- `wms_location_roles`: tekil kısmi `(TenantId, LegalEntityId, LocationId)` where `IsDeleted = false`;
  `(TenantId, LegalEntityId, WarehouseId, Roles)`; tekil kısmi `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)` (rol yaratma,
  kullanıcı kapsamında; bağımsız inceleme 10 F-1, 11 F-2). Rol yaratmada iki tekil index'ten (anahtar, `LocationId`) gelen her
  yinelenen kayıt hatası §8.1'in yeniden arama kuralına girer (bağımsız inceleme 11 F-3).
- `wms_number_counters`: tekil `(TenantId, CompanyCodePrefix, TypePrefix)` (geçici RCS sayacı, M-2 / Q-07 B).
- Test DB adı DB-010 (`_test` / `_tests` / `_itest` parçası); paket testleri kendi replika setini açar (`INVENTORY_TEST_MONGO`,
  27017 reddedilir — MOD-0173 FU-0173-TEST-01).

## 5. Repo Scope

Yalnız yeni dosyalar (seam'ler aşağıda tek tek):
- `services/Diten.InventoryService/src/Diten.InventoryService.Domain/Warehouse/**` (varlıklar, enum'lar, saf kurallar:
  `PutawayTaskRules`, `LocationRoleRules`)
- `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/PutawayTasks/**`,
  `…/Features/WarehouseLocationRoles/**`, `…/Features/Warehouse/{Ports,Services}/**` (`WarehouseMovementPoster`,
  `IWarehouseRecordCodeAllocator`, okuma portları) ve `…/Features/Warehouse/WarehouseErrorCodes.cs` (`WMS_*` kodları — WMS-BUNDLE'da tanımlı, Q-06; `InventoryErrorCodes.cs` değişmez),
  `…/Features/Warehouse/WarehouseCentralAudit.cs` (BL-501 bildirim yardımcısı, §21; `InventoryCentralAudit.cs` değişmez)
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
| `services/Diten.InventoryService/src/Diten.InventoryService.Application/Common/Upstream/SkuReference.cs` | record `:13-24` | eklemeli isteğe bağlı `IReadOnlyList<string>? StorageConditions` (yok = bilinmiyor; boş liste = koşul yok) — **sahibi MOD-0173 (MVP-1 CT, aynı sahip; XMC-001 gerekmez, seam açık listelenir)** (F-5) | S1 (DL-1) |
| `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Upstream/ProductMasterHttpClient.cs` | `GetSkuAsync` `:62-82` | `storageConditions` dizisini oku (alan yoksa null) — sahibi MOD-0173 (F-5) | S1 (DL-1) |
| Seri kovası okuması (MOD-0174 / MOD-0175 emsali) | **ölçüldü (inceleme 02 F-8):** emsal `QuarantineLedgerReads.SerialsAtAsync` (`…/Persistence/Quarantine/QuarantineLedgerReads.cs:108-120`, port `…/Features/Quarantine/Ports/QuarantinePorts.cs:50-55`): kiracı + item + SKU + seviye + lot + depo + lokasyon, `Status Active`, alım sırası, **seri numarası** döner; MOD-0175 `StockActionHandlers.SerialsForAsync` (`…/Quarantine/Handlers/StockActionHandlers.cs:175-200`, ASSUMPTION-0175-Q4-02) açık tutma ve açık bloke kayıtlarının serilerini dışlar | 0178 portu `IWarehouseSerialReader` aynı okumayı ve **aynı dışlamayı** yapar (§8.16); yeni repository sorgusu açılmaz — `SerialsAtAsync` okuması tekrar kullanılır ya da aynı filtreyle 0178 Persistence'ta okunur (geliştirici seçer, ikisi de emsal) — sahibi MOD-0174 / 0175 (MVP-1 CT) | S1 (DL-2) |
| Ledger anahtar araması (MOD-0173) — **yeni port** | `IInventoryLedgerRepository.FindByIdempotencyKeyAsync` (`…/Domain/Repositories/IInventoryLedgerRepository.cs:16`); `InternalMovementPostingService` bunu doğrulamadan **önce** çağırır (`InternalMovementPostingService.cs:93-99`; parmak izi farklıysa `Replay` → 400 `IDEMPOTENCY_KEY_REUSED`, `:216-226`) | MOD-0173 Application'a eklemeli port `IPostingKeyLookup.LookupAsync(storedMovementRequest)` → `NotCommitted \| Committed(holder) \| KeyReusedDifferentPayload(holder)`; **`holder`** = anahtarı tutan hareket: `transactionId`, `transactionNumber`, `sourceModule`, `sourceType`, `sourceDocumentId`, `sourceLineId`, `quantity` + `uomId` / `baseQuantity`, `fromWarehouseId` / `fromLocationId`, `toWarehouseId` / `toLocationId`, **`lotId`, `serialIds`, `fromStockStatus` / `toStockStatus`** (inceleme 05 F-4), `postingDate`, **`reversedByTransactionId?`** (hareket ters kaydedildiyse ters kaydın kimliği; bağımsız inceleme 05 F-2 — bugün yalnız `IsReversedAsync` (bool) var: `IInventoryLedgerRepository.cs:20`, `InventoryLedgerRepository.cs:110-113` `ReversesTransactionId == id`; ters kaydın kimliğini döndürmek için aynı filtreyle eklemeli okuma `FindReversalOfAsync(transactionId)` — MOD-0173'te **dördüncü seam**, aşağıdaki satır) (repository + `RequestFingerprint.Compute`; **upstream çağrısı yok**; inceleme 04 F-1 / F-4). Defter append-only'dir (`IInventoryLedgerRepository.cs:11-12`): bir anahtara bağlanmış hareket REVERSAL ile serbest kalmaz, yani `KeyReusedDifferentPayload` kalıcıdır. 0178 depoya doğrudan bağlanmaz (AC-6 sınırı korunur) — sahibi MOD-0173 (MVP-1 CT, aynı sahip) (inceleme 02 F-1) | S1 (DL-2) |
| Ters kayıt okuması (MOD-0173) — **dördüncü seam** (bağımsız inceleme 06 F-1) | `…/Domain/Repositories/IInventoryLedgerRepository.cs:20` (`IsReversedAsync`, bool) ve `…/Persistence/Repositories/InventoryLedgerRepository.cs:110-113` (filtre `ReversesTransactionId == id`). **Ölçüldü:** mimari test `…/tests/Diten.InventoryService.Tests/Architecture/LedgerImmutabilityArchitectureTests.cs:28-36` arayüzde `Find` / `Query` / `List` / `Is` önekli üyeleri kabul eder, tek yazıcı `PostAsync`; tekil kısmi index `…/Persistence/InventoryIndexConfiguration.cs:49-55` (`TenantId + LegalEntityId + ReversesTransactionId`, `Unique`, `$type: string`) bir harekete **en çok bir** ters kayıt verir | arayüze ve uygulamaya **yalnız eklemeli okuma üyesi** `Task<InventoryTransaction?> FindReversalOfAsync(Guid transactionId, CancellationToken)` — `IsReversedAsync` deseniyle aynı filtre, `FirstOrDefault`; yazım üyesi eklenmez (mimari test yeşil kalır); `IPostingKeyLookup` bunu `holder.reversedByTransactionId` için çağırır — sahibi MOD-0173 (MVP-1 CT, aynı sahip) | S1 (DL-2) |
| BL-501 bildirim testi (MOD-0173) — **beşinci seam** (bağımsız inceleme 09 F-3) | `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CentralAudit/CentralAuditDeclarationTests.cs` (`963f6230a`'da okundu): `:22-27` Application derlemesindeki her yazma komutunu tarar; `:35` `Assert.Equal(67, WriteCommands.Length)`; `:42-49` beş manifest sağlayıcısı, `:56` `SourceModule` bu listede aranır; `:54` `CentralAuditPlatformValues.IsKnown`; `:66` dışarıda-işlem listesi; `:107` `Assert.Equal(10, seen)` (Persistence'taki `WithTransactionAsync` callback sayısı, her biri enlist eder) | `:35` **67 → 82** (0178'in 15 S1 yazma komutu, §3; DL-1'in 4 rol komutuyla ara değer 71) ve yanındaki yorum; `:42-49` listesine `PutawayPickingPackingManifestProvider`; `:107` sayısı 0178 Persistence'ının `WithTransactionAsync` callback sayısı kadar artar (sayı geliştiricinin ölçümü; her callback enlist eder, §8.8); `:66` (0178'de `ICentralAuditOutsideTransaction` yok) ve `:76-79` sweeper listesi (uzlaştırıcı hosted service işlem açmaz) **değişmez**; başka mevcut test değişmez — sahibi MOD-0173 (MVP-1 CT, aynı sahip) | S1 (DL-1, DL-2) |
| Dev PRODUCT-MASTER köprüsü 4010 veri seti | depoda izlenmiyor (`git ls-files` 0) — yeri geliştirici ölçer | örnek SKU'lara `storageConditions` eklenir (kabul ortamı, F-5). **Kural (inceleme 02 F-10):** E3 / E4 kabul kanıtında kullanılan veri setinin **kopyası ve SHA-256'sı kanıt klasörüne** konur (klasör yolu kabul aşamasında yazılır); kopyası olmayan koşu yeniden üretilebilir sayılmaz | S1 |

`execution/registries/module-implementation-status.md` dev dilimince yazılmaz; rapor satırı önerir, CT kabulde yazar.

## 6. Protected Paths

- `.antigravity/**`, `AGENTS.md`, `CLAUDE.md`, `.claude/rules/**`.
- `gateway/Diten.ApiGateway/**` (`ocelot.json` yalnız integration-agent, INT-WMS).
- Donmuş sözleşmeler: `docs/analysis/contracts/*.openapi.yaml` (WAREHOUSE-OUTBOUND dahil — 1.0.0 değişmez; eklemeli değişiklik
  yalnız CT kabullü tek yazıcı adımıyla), `shipment-bundle.openapi.yaml` ve MVP-6 semantik dosyaları (asla).
- MOD-0173…0177 kodu — **istisna (inceleme 03 F-4, bağımsız inceleme 06 F-1, 09 F-3):** §5'teki **beş** MOD-0173 seam'i (`SkuReference`,
  `ProductMasterHttpClient`, yeni `IPostingKeyLookup` portu, `IInventoryLedgerRepository` / `InventoryLedgerRepository`'ye eklemeli okuma
  üyesi `FindReversalOfAsync` ve BL-501 bildirim testi `CentralAuditDeclarationTests.cs`'in komut sayısı + manifest listesi) ve MOD-0174 / 0175 seri okuması; sahip aynı (MVP-1 CT), XMC-001 notu §7'de. Bunlar dışında: defter
  (`InventoryLedgerRepository`, `InternalMovementPostingService` — bu dosyalarda **yalnız eklemeli okuma üyesi**; yazım yolu yalnız
  çağrılır, değiştirilmez), rezervasyon,
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

**XMC-001 (inceleme 03 F-4, bağımsız inceleme 06 F-1 ve 09 F-3 ile düzeltildi):** Bu paket **MOD-0173'te beş seam** açar — `SkuReference.cs`
(+ `StorageConditions`), `ProductMasterHttpClient.cs` (`storageConditions` okuması), yeni `IPostingKeyLookup` portu,
`IInventoryLedgerRepository.cs:20` yanına / `InventoryLedgerRepository.cs:110-113` deseniyle eklemeli okuma `FindReversalOfAsync` ve
BL-501 bildirim testi `…/tests/Diten.InventoryService.Tests/CentralAudit/CentralAuditDeclarationTests.cs` (komut sayısı 67 → 82, manifest
listesine 0178 sağlayıcısı) — ve MOD-0174 / 0175 seri okuma
emsalini kullanır (§5). Sahip aynıdır (MVP-1 CT), bu yüzden ayrı XMC-001 onayı gerekmez; seam'ler §5'te dosya ve satırıyla açık
listelenir. Başka modül değişikliği yoktur. S2'nin INVENTORY ihtiyaçları (W-5) sahibinin (MVP-1 CT) ayrı WP'sidir,
MVP-6 ihtiyaçları Natig'e sorudur (§19.5). İkisi de bu paketin dev diliminde yapılmaz.

## 8. Runtime Constraints

### 8.1 Servis ve kapsam
- Servis `Diten.InventoryService`, port **5067** (AGENTS.md §3 satır 80). Depo kodu ayrı bounded context bölümünde
  (`Domain/Warehouse`, `Features/PutawayTasks`, `Features/WarehouseLocationRoles`, `Features/Warehouse`), MVP-1 modüllerinin
  koduna yalnız §5 seam'lerinden değer.
- Taban yol `/api/warehouse` (WAREHOUSE-OUTBOUND `servers.url`).
- Kiracı token'dan; şirket W-0173-02 (claim varsa claim, yoksa `X-Legal-Entity-Id` + MDM doğrulaması). **403 `CROSS_LE_FORBIDDEN`
  yalnız başlık ↔ token / üyelik uyuşmazlığıdır** (MOD-0173 pack `:186`, `:395`). Seçili şirketle **başka şirketin kaydına id ile
  erişim → 404** (kayıt varlığı sızdırılmaz); başka kiracı / bilinmeyen id → **404** (F-11). `tenantId` / `legalEntityId` gövde / sorguda → 400.
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
- **Idempotency:** her yazma `Idempotency-Key` başlığı = gövde `idempotencyKey` (8–100). Aynı anahtar + aynı gövde → **200
  replay** (kaydın güncel hâli; onayda ayrıca isteğin kendi onayının `confirmationSeq`'i — aşağıda); farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`. **Depolama ve sıra (bağımsız inceleme 10 F-1; 0176 / 0177 emsali):**
  - **Nerede:** görev ve rol yaratma → kaydın `IdempotencyKey` + `RequestFingerprint`'i, yaratanın (`CreatedBy`) kapsamında (§4.1, §4.4); yaratma dışındaki **bütün** yazma
    komutları (görev: düzenle, üstlen, toplu atama, onay, yeniden dene, "Geri taşındı", Kalanı kapat, İptal, Uzlaştır; rol: düzenle, sil,
    toplu sil — DL-1 dahil) → komutun **anahtar olayı** (§4.3), tekil kısmi index `(TenantId, TaskId | RoleId, ActorUserId,
    RequestOp, IdempotencyKey)` (§4.6). Sweeper iç komutu istemci isteği değildir, anahtar taşımaz.
  - **Sıra:** kimlik, izin ve istek biçimi (başlık = gövde, 8–100) kontrollerinden sonra, **If-Match'ten ve bütün iş kurallarından önce**
    (durum, atanan, Pending kilidi, ön kontroller, ön arama) anahtar aranır — kiracı + şirket kapsamında, isteği yapanın `ActorUserId`'siyle ve komutun `RequestOp`'uyla (yaratmada: kaydın `CreatedBy`'ı)
    (Recall emsali: `…/Features/Recall/Handlers/CaseCommandHandlers.cs:540` replay, `:546` sürüm kontrolü; replay yardımcısı `:191-196`).
    **Aynı anahtar + aynı parmak izi** → **replay = 200 + kaydın güncel hâli** (`WarehouseTask` / `WarehouseLocationRole`; Recall emsali
    güncel kaydı döner); ilk cevabın gövdesi ve 422 / 409 / 503 kodu tekrarlanmaz. **Onayda** cevap ayrıca isteğin kendi onayının
    `confirmationSeq`'ini taşır (değer anahtar olayı `ConfirmationRecorded`'ın `Data.seq`'inden okunur, §4.3); sonuç gövdedeki
    `confirmations[confirmationSeq].status`'tur (Posted / Refused / Pending / IntegrityError) — görevde sonradan başka onay açılmış olsa da
    istemci kendi onayını bulur (bağımsız inceleme 11 F-1). **Yazım yok, enlist yok, yerel olay yok** (merkezi kayıt `CentralAuditRecorder.cs:160-168`
    gereği doğmaz; AC-15 "tekrar = 0 / 0 / 0"). **Aynı anahtar + farklı parmak izi** → 400 `IDEMPOTENCY_KEY_REUSED`, yazım yok. Aynı
    anahtar başka görevde / rolde, başka kullanıcıda ve başka operasyonda (`RequestOp`) ayrı anahtardır (index'ler bunları içerir, §4.6;
    bağımsız inceleme 11 F-2).
  - **Toplu komutlar:** anahtar, yazılan satırların olaylarında, **yalnız aynı `RequestOp`'un** olaylarında aranır (tekil olmayan index,
    §4.6; ör. toplu rol silme tekil `deleteLocationRole`'ün `Deleted` olaylarını saymaz — bağımsız inceleme 11 F-2). Replay: anahtarı taşıyan satır yazılmış
    sonucuyla (`Assigned` / silindi) ve güncel `version`'la döner; yazılmamış satırın ilk cevaptaki nedeni saklanmadığı için `Conflict`
    (bulunamayan `NotFound`) döner, yazılmaz — istemci o satırları yeni anahtarla gönderir. Hiçbir satır yazılmadıysa anahtar yoktur.
  - **Parmak izi** (Recall `RecallSupport.Fingerprint(("op", …), …)` deseni, `CaseCommandHandlers.cs:539`): eylem adı + hedef + normalleşmiş
    gövde; `Idempotency-Key` ve `If-Match` girmez. Komut başına: görev yarat = `op`, kaynak kova (`candidateKey` ya da `warehouseId`,
    `fromLocationId`, `itemId`, `skuId`, `skuLevel`, `lotId`, `stockStatus`), `plannedQuantity` (normal decimal), `toLocationId`,
    `targetSource`, `assigneeUserId`, `priority`, `note` (trim) · düzenle = `op`, `taskId`, gönderilen gövde alanları (aynı normalleştirme) ·
    üstlen = `op`, `taskId` · toplu atama = `op`, `assigneeUserId`, sıralı `items[(taskId, version)]` · onay = `op`, `taskId`, `quantity`,
    `toLocationId`, `deviationReason` (trim), `channel`, `scanRef` · yeniden dene = `op`, `taskId`, `seq` · "Geri taşındı" / Uzlaştır =
    `op`, `taskId`, `seq`, `reason` (trim) · Kalanı kapat / İptal = `op`, `taskId`, `reason` (trim) · rol yarat = `op`, `locationId`, sıralı
    `roles`, sıralı `acceptedStorageConditions`, `note` · rol düzenle = `op`, `roleId`, gönderilen gövde alanları · rol sil = `op`, `roleId` ·
    toplu rol sil = `op`, sıralı `items[(roleId, version)]`.
  - **Eşzamanlı aynı anahtar:** iki istek de aramayı geçebilir. Kazanan anahtar olayını (ya da yaratılan kaydı) commit eder; kaybeden ya
    tekil index'ten yinelenen kayıt hatası alır ya da aynı işlemdeki CAS'ı kaybeder (0 kayıt). **Yaratmada her yinelenen kayıt hatası**
    aynı kurala girer, hangi tekil index olursa olsun: anahtar index'i, rolde canlı `(TenantId, LegalEntityId, LocationId)`, görevde seri
    slot index'i (`OpenSerialSlot`) (bağımsız inceleme 11 F-3). Her durumda kaybeden, satırın kendi 409 / 422 / 0-satır kuralını
    uygulamadan **önce anahtarı yeniden arar**: bulunursa aynı parmak izi → 200 replay, farklı → 400; bulunmazsa satırın kendi kuralı
    (rol `LocationId` → 409 `WMS_LOCATION_ROLE_EXISTS`; seri slot → 422 `WMS_QUANTITY_EXCEEDS_OPEN`) (Inventory emsali `…/Features/Inventory/Services/InternalMovementPostingService.cs:188-194` `IdempotencyKeyTaken → Replay`;
    Recall'un `SaveAsync`'i yinelenen anahtarda `false`, `WriteCaseAsync` 409 döner, `CaseCommandHandlers.cs:218-221`, `:224-227` — 0178
    Inventory emsalini izler). Kaybedenin işlemi geri
    alındığı için olayı ve enlist'i yoktur.
  - **İstemci:** her kullanıcı eylemi (ör. her "Yeniden dene" tıklaması) yeni anahtar üretir; aynı anahtar yalnız aynı isteğin ağ
    tekrarıdır.
- **Eşzamanlılık:** değişebilir kayıtlar (görev, rol) `If-Match: W/"<version>"`; eksik → 400, bayat → 409 `WMS_CONFLICT`. Bayatlık
  istek anahtarı kontrolünden **sonra** değerlendirilir: ilk isteği işlenmiş bir ağ tekrarı (sürümü ilk istek artırdı) 409 değil 200
  replay alır (bağımsız inceleme 10 F-1).
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

Onay (Confirmation):
(yok) ──(işlem 1)──> Pending
Pending ──(işlem 2: 2xx / ledger Committed)──> Posted
Pending ──(işlem 2: 4xx / ön kontrol reddi)──> Refused
Pending ──(IDEMPOTENCY_KEY_REUSED · Confirmed + q > Planned · HolderReversed)──> IntegrityError   (satır 9 / 10 / 12 / 21)
Refused ──(0-satır (D) · savunma dalı · kapatıcı komutun ön araması: ledger Committed)──> IntegrityError   (satır 11 / 18 / 20)
IntegrityError ──(Uzlaştır-a: anahtarı tutan hareket bu onayın)──> Posted   (IntegrityResolution = ResolvedOwnMovement)
IntegrityError ──(Uzlaştır-b: anahtarı tutan hareket yabancı / NotCommitted)──> Refused (çözümsüz; IntegrityResolution = ReturnedToRefused)
Refused ──(aynı görevde yeni Posted onay)──> Refused (Resolution = SupersededByNewConfirmation)
Refused ──("Geri taşındı")──> Refused (Resolution = ReturnedToSource)
```
Görev durumu Uzlaştır'la **değişmez**, yalnız onaylanan miktar değişirse (Uzlaştır-a) türetilmiş durum ilerler; görevi kapatmak **ayrı ve
açık bir seçimdir**: İptal (sorumlu, `cancel` izni) ya da Kalanı kapat (atanan operatör ya da sorumlu, `confirm` izni) (inceleme 04 F-1,
bağımsız inceleme 05 F-6). **Terminal görevde** (Completed / Cancelled) Uzlaştır ve savunma dalı çalışır; yalnız onay kaydını düzeltir,
görevi **yeniden açmaz** (satır 23–24). Cancelled görevde Uzlaştır-a ile Posted olan onay, "onay yoksa yalnız İptal" kuralının bilinçli
istisnasıdır: defter gerçeği yazılır, olay `ConfirmationDeviation` (`PostedAfterCancel`) (bağımsız inceleme 05 F-1).

**Akış tablosu — her geçiş (inceleme 05 tutarlılık geçişi; bağımsız inceleme 07 F-5, 08 F-1 / F-2 / F-4 / F-5 ve 09 F-1 / F-2 / F-5, 10 F-1 / F-2 ile
§4.2 alan listesi, CAS kuralı, §4.3, §21 ve AC-15'e satır satır eşitlendi; boş hücre yok).** Kısaltmalar: **V++** = görev belgesinin `Version`'ı CAS ile
kontrol edilir ve artırılır (her yazım; okuma-yalnız satırlarda "V değişmez"); **Error\*** = `ErrorCode`, `ErrorMessage`, `ErrorDetails`
(`ErrorDetails.reason` dahil), `ErrorStatus`; **Ret\*** = `RefusalSource`, `RefusedOnRetry`, `RefusedAtUtc`; **Kilit↓** = `AttemptId` ve
`InFlightUntilUtc` boşaltılır; **Superseded** = aynı yazımda `arrayFilters` ile `Status == Refused` ve `Resolution == null` olan kardeş
onaylara `Resolution = SupersededByNewConfirmation` (IntegrityError olan kardeş atlanır); **Slot↓** = türetilmiş görev durumu `Completed`
olursa seri görevin `OpenSerialSlot`'u aynı yazımda kaldırılır; **anahtar olayı** = isteğin `IdempotencyKey` + `RequestFingerprint`'ini
taşıyan olay (§4.3, §8.1; bağımsız inceleme 10 F-1). **Olay sütunu:** önce yerel zaman çizelgesi (`wms_task_events`, aynı işlem,
silinmez), sonra **merkezi denetim (BL-501)** — merkezi kayıt yalnız **başarılı** komutta 1, her başarısız cevapta (Refused / Pending /
IntegrityError / her 409 / 503) 0 (§8.8, sahip kararı 2026-10-26). Merkezi sütundaki ad komutun **`RequestType`**'ıdır — BL-501 kaydı
olay adı ve veri taşımaz (§21); "merkezi 0" = iletilen (`Pending` / `Delivered`) satır 0, komutun bir işlemi commit olduysa satır
`Abandoned` olarak durur (bağımsız inceleme 09 F-1 / F-2). Görev düzeyindeki diğer yazımlar (düzenleme, üstlenme, toplu atama)
If-Match = `Version` CAS ile yapılır; Pending onay varken 409 `WMS_CONFIRMATION_PENDING` (toplu atamada satır sonucu `PendingConfirmation`).
| # | Geçiş | Tetikleyici | Yazılan alanlar | Zaman çizelgesi (`Kind`) / merkezi (BL-501 `RequestType`) | AC | Sabotaj |
|---|---|---|---|---|---|---|
| 1 | (yok) → Pending | confirm, işlem 1 (önce satır 20'nin ön araması) | onayın kendi değerleri: Seq, Quantity, ToLocationId, DeviationReason, Channel, ScanRef; Status = Pending, PostingKey, TransactionDate / PostingDate, SerialIds, AttemptId, InFlightUntilUtc, Attempts = 1, LastAttemptAtUtc, ConfirmedBy / ConfirmedAtUtc, V++; komutun merkezi olayı bu işleme enlist edilir | ConfirmationRecorded (anahtar olayı) / merkezi: komutun sonucuna bağlı — Posted → 1 `ConfirmPutawayTaskCommand`; başka her sonuç → 0 iletilen (satır `Abandoned`); işlem 1 sonrası çökme → 10 dk sonra 1 `Unconfirmed` (§8.8) | AC-2, AC-5, AC-15 | (a), (x), (v) |
| 2 | Pending → Posted | işlem 2 (2xx) — ilk onay ya da retry (ii)'nin kendi posting'i | Status = Posted, TransactionId / Number, görev ConfirmedQuantity + türetilmiş durum, Slot↓, Kilit↓, Superseded, V++ | ConfirmationPosted (+ ConfirmationSuperseded; + Completed) / merkezi 1 (confirm: `ConfirmPutawayTaskCommand`; retry: `RetryPutawayConfirmationCommand`) | AC-2, AC-3, AC-4, AC-51 | (a), (w), (bi) |
| 3 | Pending → Posted | retry (i), ledger `Committed`, holder ters kaydedilmemiş | satır 2 ile aynı | ConfirmationPosted (+ ConfirmationSuperseded; + Completed; kilit alımındaki ConfirmationRetried satır 7'de) / merkezi 1 `RetryPutawayConfirmationCommand` | AC-5, AC-54, AC-51 | (z), (w), (bi) |
| 4 | Pending → Posted | sweeper (iç komut), ledger `Committed`, holder ters kaydedilmemiş, kilit boş / geçmiş | satır 2 ile aynı (Kilit↓ yok — sweeper kilit tutmaz) | ConfirmationReconciled (+ ConfirmationSuperseded; + Completed) / merkezi 1 `ReconcilePendingConfirmationCommand` | AC-66, AC-15, AC-51 | (as), (ao), (bi) |
| 5 | Pending → Refused | işlem 2 (ledger **kesin** 4xx; 5xx / istisna Refused yazmaz — satır 7, Codex P2) — ilk onay ya da retry (ii) | Status = Refused, Error\*, Ret\* (`RefusalSource = Ledger4xx`, `RefusedOnRetry` = confirm: false / retry: true, `RefusedAtUtc`), Kilit↓, V++ | ConfirmationRefused / merkezi 0 | AC-5, AC-9, AC-57, AC-66, AC-15 | (x), (ag), (bc), (bh) |
| 6 | Pending → Refused | retry (ii), ön kontrol reddi | Status = Refused, Error\* (`ErrorStatus` 422), Ret\* (`RefusalSource = PreCheck`, `RefusedOnRetry = true`, `RefusedAtUtc`), Kilit↓, V++ | ConfirmationRefused / merkezi 0 | AC-56, AC-66, AC-15 | (y), (bc), (bh) |
| 7 | Pending → Pending | retry (0) kilit alımı; ardından commit sonucu bilinmeyen sonuç: her 5xx (500 / 502 / 503 / 504), zaman aşımı, posting süresi aşımı, ağ hatası, iptal (`OperationCanceledException`), beklenmeyen istisna (Codex P2, bağımsız inceleme 13 F-2; ilk onayda aynı sonuç satır 1'den sonra yazımsızdır) | (kilit alımında) AttemptId, InFlightUntilUtc, Attempts++, LastAttemptAtUtc, V++; (sonuçta) yazım yok — kilit `InFlightUntilUtc`'ye kadar korunur | ConfirmationRetried (kilit alımında, `data.attempt`; anahtar olayı) / merkezi 0 (cevap 503); ilk onayda yalnız satır 1'in olayı | AC-5, AC-67 (1), AC-15 | (x), (ap) |
| 8 | Pending → Pending | sweeper, `NotCommitted` — hosted service iç komutu **göndermez** | hiçbir şey (V değişmez) | yok / merkezi 0 | AC-66 | (an) |
| 9 | Pending → IntegrityError | `IDEMPOTENCY_KEY_REUSED` (confirm ya da retry) | Status = IntegrityError, Error\* (`reason = KeyReused`), Kilit↓, V++ | ConfirmationIntegrityError / merkezi 0 (409) | AC-63, AC-15 | (aa) |
| 10 | Pending → IntegrityError | sweeper, `KeyReusedDifferentPayload` | Status = IntegrityError, Error\* (`reason = KeyReused`), V++ | ConfirmationIntegrityError / merkezi 0 (iç komut başarısız zarfla döner) | AC-66 | (aq) |
| 11 | Refused → IntegrityError | 0-satır kuralının (D) dalı: bu deneme Posted / replay aldı, onayın **kendi** durumu Refused (başka bir denemenin yazımı); filtre V + `Status == Refused`; onay zaten `IntegrityError` ise yazım yok (`PriorResolution` korunur) ve yine 409 (bağımsız inceleme 10 F-2) | Status = IntegrityError, Error\* (`reason = StatusDiverged`), `PriorResolution = Resolution`, `Resolution = null`, V++ (kilit Refused yazımında zaten boşaltıldı) | ConfirmationIntegrityError (zaten IntegrityError ise yok) / merkezi 0 (409) | AC-64, AC-15 | (al), (bo) |
| 12 | Pending → IntegrityError (`OverPlanned`) | işlem 2 / retry (i) / sweeper yazımında koşullu artış tutmaz — ilk yazımda ya da 0-satır kuralının (A) yeniden okumasında (**maskelenmez**); gerçek akışta tetiklenmez (savunma) | Status = IntegrityError, Error\* (`reason = OverPlanned`), Kilit↓, V++ | ConfirmationIntegrityError / merkezi 0 (409) | AC-67 (5) birim testi, AC-67 (6) yazım yolu | (ap) |
| 13 | IntegrityError → Posted | Uzlaştır-a (kendi hareketi); filtre V + `Status == IntegrityError` + görevde Pending yok | Status = Posted, TransactionId / Number, miktar, hedef, lot, seriler, stok durumu (defterden), IntegrityResolution = ResolvedOwnMovement, HolderMovement, ResolvedBy / ResolvedAtUtc, ResolveReason, görev ConfirmedQuantity + türetilmiş durum, Slot↓, Superseded, V++ | IntegrityResolved (anahtar olayı; + ConfirmationDeviation; + ConfirmationSuperseded; + Completed) / merkezi 1 `ResolvePutawayIntegrityCommand` | AC-65, AC-67 (3) | (ar) |
| 14 | IntegrityError → Refused (çözümsüz) | Uzlaştır-b (yabancı / `NotCommitted`) | Status = Refused, Ret\* (`RefusalSource = Resolve`, `RefusedOnRetry = false`, `RefusedAtUtc`), IntegrityResolution = ReturnedToRefused, HolderMovement, ResolvedBy / ResolvedAtUtc, ResolveReason, V++ | IntegrityResolved (anahtar olayı) / merkezi 1 `ResolvePutawayIntegrityCommand` | AC-65 | (am) |
| 15 | Refused → Refused + Superseded | başka bir onayın Posted yazımının parçası (satır 2 / 3 / 4 / 13 / 23 / 25); hedef `arrayFilters` `Status == Refused` ve `Resolution == null` — **IntegrityError olan kardeş atlanır** | Resolution = SupersededByNewConfirmation (aynı yazım, tek V++) | ConfirmationSuperseded (kapatılan her onay için bir satır) / merkezi: ayrı kayıt yok — Posted komutunun tek kaydı gövde taşımaz; kapatılan `seq` listesi yalnız yerel `Data`'da (§4.3) | AC-55, AC-64 | (at) |
| 16 | Refused → Refused + Returned | "Geri taşındı": hedef **yalnız bu onay**; ön arama yalnız onun anahtarı; filtre V + hedef `Status == Refused` ve `Resolution == null` | Resolution = ReturnedToSource, ResolutionReason, V++ | ConfirmationReturned (anahtar olayı) / merkezi 1 `MarkPutawayConfirmationReturnedCommand` | AC-55, AC-70 | (af), (bb) |
| 17 | (ret) kilit geçerli | retry (0) | hiçbir şey (409 `WMS_CONFIRMATION_IN_FLIGHT`; V değişmez) | yok / merkezi 0 | AC-64, AC-67 (1) | (ak) |
| 18 | Refused (`RefusalSource ∈ {PreCheck, Ledger4xx}`, `RefusedOnRetry = true`, `RefusedAtUtc` son 30 dk; **`Resolution`'dan bağımsız**; görev terminal olsa da) → IntegrityError | sweeper savunma dalı (iç komut; ayrı imleç ve bütçe), ledger `Committed` (geç commit); filtre V + `Status == Refused` | Status = IntegrityError, Error\* (`reason = StatusDiverged`), `PriorResolution = Resolution`, `Resolution = null`, V++ | ConfirmationIntegrityError / merkezi 0 (iç komut başarısız zarfla döner) | AC-66, AC-67 (3), AC-68 (ii), AC-70 | (au), (ax), (bb), (bc), (bf) |
| 19 | (ret) Uzlaştır, görevde Pending onay | resolve | hiçbir şey (409 `WMS_CONFIRMATION_PENDING`; V değişmez) | yok / merkezi 0 | AC-65, AC-67 (4) | (av) |
| 20 | Refused → IntegrityError + komut 409 | ön arama `Committed` (ters kaydedilmemiş) — hedef kümeler: yeni onay = çözümsüz Refused'lar; "Geri taşındı" = yalnız bu onay; İptal / Kalanı kapat = görevin **tüm** Refused onayları; filtre V + hedef `Status == Refused` | hedefte: Status = IntegrityError, Error\* (`reason = StatusDiverged`), `PriorResolution = Resolution`, `Resolution = null`, V++; komutun kendi yazımı **yok** | hedefte ConfirmationIntegrityError / merkezi 0 (komut 409; enlist edilmiş satır `Abandoned`) | AC-68 (i), AC-15 | (aw), (bh) |
| 21 | Pending → IntegrityError (`HolderReversed`) | retry (i) ya da sweeper; ledger `Committed` ama holder ters kaydedilmiş | Status = IntegrityError, Error\* (`reason = HolderReversed`), HolderMovement, Kilit↓ (retry), V++ | ConfirmationIntegrityError / merkezi 0 | AC-69 | (az) |
| 22 | IntegrityError → Refused (çözümsüz) | Uzlaştır, holder ters kaydedilmiş — (b) yolu | satır 14'ün alanları | IntegrityResolved (anahtar olayı) / merkezi 1 `ResolvePutawayIntegrityCommand` | AC-69 | (az) |
| 23 | IntegrityError → Posted, **görev terminal** (Completed / Cancelled) | Uzlaştır-a | satır 13'ün alanları (Superseded dahil); görev durumu **değişmez** (yeniden açılmaz); `OpenSerialSlot` yeniden alınmaz; V++ | IntegrityResolved (anahtar olayı) + ConfirmationDeviation (`OverPlanned` / `PostedAfterCancel`) / merkezi 1 `ResolvePutawayIntegrityCommand` | AC-67 (3), AC-68 (iii) | (ay) |
| 24 | IntegrityError → Refused (çözümsüz), **görev terminal** | Uzlaştır-b | satır 14'ün alanları; görev durumu değişmez; yeni onay yok, yalnız "Geri taşındı" açık; V++ | IntegrityResolved (anahtar olayı) / merkezi 1 `ResolvePutawayIntegrityCommand` | AC-68 (iii) | (ay) |
| 25 | Pending → Posted (CAS yinelemesi) | işlem 2 / retry (i) / sweeper yazımı 0 kayıt; yeniden okuma: onay **hâlâ Pending**, `AttemptId` bu deneme (sweeper: kilit boş / geçmiş) ve koşullu artış **tutuyor** — yalnız görev `Version`'ı değişmiş | aynı yazım yeni V ile yinelenir (en çok 3 kez; Slot↓ dahil); artış tutmuyorsa satır 12; üçü de kaybederse yazım yok, onay Pending, 503 `WMS_CONFIRMATION_CONFLICT_RETRY`, kilit korunur | satır 2 / 3 / 4'ün olayı bir kez / merkezi 1 (başarıda) ya da 0 (503) | AC-64 | (bd) |
| 26 | (ret) 0 kayıt, onay Pending ama `AttemptId` başka denemenin — kilit geçerli olsun olmasın (bağımsız inceleme 09 F-5) | işlem 2 / retry (i) / (ii) sonuç yazımı (bu denemenin `AttemptId`'siyle) | hiçbir şey (V değişmez); **(C1)** o kilit geçerli → 409 `WMS_CONFIRMATION_IN_FLIGHT`, kayıt öbür denemeyle ilerler; **(C2)** o kilit dolmuş → 409 `WMS_CONFLICT` + güncel görev, kayıt sweeper'ın uzlaştırmasıyla (kilidi geçmiş kayıt onun filtresine girer) ya da kullanıcının "Yeniden dene"siyle ilerler | yok / merkezi 0 (işlem 1 / kilit alımı satırı `Abandoned`) | AC-64 ((C1) / (C2) vakaları) | (bd), (bj) |
| 27 | Görev Open / InProgress → Cancelled / Completed | İptal / Kalanı kapat: ön arama görevin **tüm** Refused onayları; filtre V + hedeflerin `Status == Refused` (Resolution koşulu **yok**) | görev Status, CancelReason / ShortCloseReason, `OpenSerialSlot` kaldırılır, V++ | Cancelled / ClosedShort (anahtar olayı; `Completed` yazılmaz) / merkezi 1 `CancelPutawayTaskCommand` / `ClosePutawayTaskShortCommand` | AC-55, AC-68 (i), AC-70 | (aw), (bb) |
| 28 | (ret / no-op) Pending'den çıkan **diğer** yazımlar 0 kayıt | kilit alımı (satır 7), ön kontrol reddi (satır 6), defter 4xx (satır 5), IntegrityError yazımları (satır 9 / 10 / 21) | (kilit alımında önce istek anahtarı yeniden aranır — satır 29) yeniden okunur: hâlâ Pending ve deneme bu (kilit alımında: kilit boş / geçmiş — başka denemenin **dolmuş** kilidi de buraya girer) → yeni V ile yinelenir (≤ 3; tükenirse 503 `WMS_CONFIRMATION_CONFLICT_RETRY`); kilit başka denemede ve **geçerli** → 409 `WMS_CONFIRMATION_IN_FLIGHT` (C1); sonuç yazımında `AttemptId` başka denemenin ve o kilit **dolmuş** → yazım yok, 409 `WMS_CONFLICT` + güncel görev (C2, satır 26); onay artık Pending değil (bu denemenin sonucu Refused / IntegrityError iken kayıt Posted ya da başka bir sonuçta) → **yazım yok**, 409 `WMS_CONFLICT` + güncel görev (kayıt doğru kabul edilir; kilit alımında 409 `WMS_CONFIRMATION_NOT_RETRYABLE`) | yok / merkezi 0 (komutun önceki işlemi enlist ettiyse satır `Abandoned`; 0 kayıt etkileyen yazım enlist etmez) | AC-64, AC-15 | (bd), (bk) |
| 29 | (replay / ret) istek anahtarı kontrolü — her yaratma dışı yazma komutu (bağımsız inceleme 10 F-1) | confirm / retry / "Geri taşındı" / Uzlaştır / İptal / Kalanı kapat (ve düzenle / üstlen / toplu atama, rol komutları — akış dışı); If-Match ve iş kurallarından **önce**; ayrıca anahtarlı yazım (işlem 1, kilit alımı, komutun kendi yazımı) yinelenen kayıt hatası (yaratmada hangi tekil index olursa olsun; bağımsız inceleme 11 F-3) ya da CAS kaybıyla 0 kayıt etkilerse satırın kendi 409 / 0-satır kuralından önce yeniden | hiçbir şey (V değişmez); aynı anahtar + aynı parmak izi → 200 + güncel görev, onayda + `confirmationSeq` (isteğin kendi onayının güncel `status`'u gövdede; ilk cevabın kodu tekrarlanmaz); aynı anahtar + farklı parmak izi → 400 `IDEMPOTENCY_KEY_REUSED`; anahtar yoksa satırın kendi akışı | yok / merkezi 0 (enlist yok; `CentralAuditRecorder.cs:160-168`) | AC-4, AC-15 | (bl), (bm), (bn) |

- `Completed` / `Cancelled` terminaldir. İptal edilen görevin Posted hareketleri geri alınmaz (geri alma INVENTORY `REVERSAL`'dır,
  0178'in değil; stok yeni lokasyonda kalır). **Onay yoksa yalnız İptal, Posted onay varsa yalnız Kalanı kapat** (M-4): Posted onaylı
  görevin iptali 409 `WMS_INVALID_TRANSITION`. **Eylem başına izin ve atanan kuralı (bağımsız inceleme 05 F-6, tek yer):**
  onay / "Yeniden dene" / Kalanı kapat / "Geri taşındı" = `warehouse.putaway-tasks.confirm` **ve** (atanan operatör **ya da** sorumlu =
  `warehouse.putaway-tasks.update` sahibi), aksi 403 `WMS_NOT_ASSIGNEE`; üstlen = `confirm` (yalnız atanmamış görev); İptal =
  `warehouse.putaway-tasks.cancel` (sorumlu); Uzlaştır = `warehouse.putaway-tasks.resolve` (sorumlu). **Gönderilen onay cevap gelene kadar görevi kilitler** (M-4): Pending onay varken yeni
  onay, düzenleme, üstlenme, kısa kapatma, iptal **ve görevdeki başka bir onayın "Uzlaştır"ı** 409 `WMS_CONFIRMATION_PENDING`; yalnız
  `retry` çalışır. Gerekçe (inceleme 05 F-1): Pending onay sürerken defter durumu değişebilir; Uzlaştır-a'nın artışı Pending onayın
  kalanını tüketebilir — bu yasakla işlem 2'nin `Confirmed + q ≤ Planned` koşulu yalnız **savunma** kontrolü olarak kalır.
- **Onay (`ConfirmPutawayTaskCommand`):**
  1. Doğrula: görev `Open|InProgress`, Pending onay yok, If-Match, onaylayan = atanan ya da sorumlu (§8.13), miktar ≤ kalan
     (planlanandan fazla onay yasak, M-5), hedef kuralları (§12) ve saklama koşulu uyumu (§8.12), lot recall değil (§8.5), lot
     durumu. Açık görevin stoğu sonradan karantinaya / recall'a girerse **otomatik askı yoktur**; kontrol onay anında yapılır ve ret
     verilir (M-5).
  2. İşlem 1 (Mongo transaction, merkezi denetim kaydı listeye alınır — `RequireEnlistment`): `Confirmations[seq] = Pending`,
     `PostingKey = wms:put:{taskId}:{seq}`, **`TransactionDate = PostingDate = sunucu UTC şimdi` (bir kez, burada)**, seri SKU'da
     kovadan okunan `SerialIds`, **uçuş kilidi** (`AttemptId` + `InFlightUntilUtc`, §4.2; inceleme 03 F-1), `Version++`, olay
     `ConfirmationRecorded`.
  3. **Posting süresiyle sınırlı** (`PostingTimeoutSeconds`, öneri 60 sn; aşımda onay Pending, **kilit `InFlightUntilUtc`'ye kadar korunur**
     — §4.2, inceleme 04 F-2, inceleme 05 F-3)
     `WarehouseMovementPoster` → `InternalMovementPostingService`: `LOCATION_TRANSFER`, `fromWarehouseId = toWarehouseId =
     WarehouseId`, `fromLocationId = FromLocationId`, `toLocationId`, `fromStockStatus = toStockStatus = StockStatus` (tek adım,
     durum korunur — `MovementDirectionRules.cs:82-94`), `lotId`, `serialIds`, `quantity` + `BaseUomId`, `sourceModule = MOD-0178`,
     `sourceType = PUTAWAY_TASK`, `sourceDocumentId = taskId`, `sourceLineId = seq`, `documentGroupId = Number`,
     `idempotencyKey = PostingKey`, `transactionDate` / `postingDate` = **onaya işlem 1'de yazılan değerler** (her denemede aynen —
     ledger izi tarihleri kapsar, `RequestFingerprint.cs:42-43`; F-2), aktör = onaylayan kullanıcı.
  4. İşlem 2 — **koşullu güncelleme** (F-3): filtre `Confirmations[seq].Status == Pending` **ve `AttemptId == bu deneme`** ve Posted
     sonucunda **`ConfirmedQuantity + q ≤ PlannedQuantity`** (koşullu artış; aşarsa onay `IntegrityError`, `details.reason = OverPlanned`;
     inceleme 04 F-2 — **savunma koşulu**, gerçek akışta tetiklenmez: Pending onay varken başka onay Posted olamaz ve Uzlaştır yasaktır,
     akış satır 12) ve **`Version == okunan sürüm`** (CAS; yazım `Version++` yapar — bağımsız inceleme 06 F-3); uçuş kilidi
     boşaltılır. **Etkilenen 0 ise — tek kural (bağımsız inceleme 07 F-1, 08 F-2; Pending'den çıkan **her** yazım için: işlem 2, retry (0) kilit
     alımı, retry (i) / (ii) sonuç yazımı, sweeper uzlaştırması ve IntegrityError yazımları):** kayıt yeniden okunur. **(A)** Onay **hâlâ
     Pending** ve deneme bu (`AttemptId` bu denemenin; kilit alımında ve sweeper'da: kilit boş ya da geçmiş) → yalnız görev `Version`'ı
     değişmiştir (kardeş onaya yazım) → koşullu artış (`ConfirmedQuantity + q ≤ PlannedQuantity`) **taze değerlerle yeniden
     değerlendirilir**: tutuyorsa aynı yazım yeni `Version` ile yinelenir (en çok 3 kez; üçü de kaybederse yazım yok, onay Pending, 503
     `WMS_CONFIRMATION_CONFLICT_RETRY`, kilit korunur; satır 25 / 28), tutmuyorsa satır 12 (`OverPlanned`, maskelenmez). **(B)** Onayın kendi
     durumu bu denemenin sonucuyla **aynı** (Posted, aynı `TransactionId`) → artış ve olay yok, güncel görev döner. **(C)** Onay Pending ama
     `AttemptId` **başka bir denemenin** — kilit geçerli olsun olmasın bu deneme **yazmaz** (bağımsız inceleme 09 F-5): **(C1)** o kilit
     geçerli → 409 `WMS_CONFIRMATION_IN_FLIGHT`; **(C2)** o kilit dolmuş → 409 `WMS_CONFLICT` + güncel görev; kayıt sweeper'ın
     uzlaştırmasıyla (kilidi geçmiş kayıt onun filtresine girer) ya da kullanıcının "Yeniden dene"siyle (yeni kilit, önce ledger araması)
     ilerler (satır 26). Kilit alımında ve sweeper yazımında başka denemenin dolmuş kilidi (C) değil (A)'dır — onların filtresi zaten
     "kilit boş ya da geçmiş"tir. **(D)** Bu deneme
     **Posted / replay** aldı ve onayın **kendi** durumu **Refused** (başka bir denemenin yazımı) → onay `IntegrityError` (`StatusDiverged`)
     — tek koşullu güncelleme, filtre `Version == okunan sürüm` **ve `Status == Refused`**, aynı yazımda `PriorResolution = Resolution`,
     `Resolution = null`, `Version++`; cevap 409 `WMS_POSTING_INTEGRITY_ERROR` (satır 11). Onay **zaten `IntegrityError`** ise (savunma dalı
     ya da bir ön arama önce yazdı) **yazım yok**, `PriorResolution` korunur — ezilmez (AC-70 ilkesi) — ve cevap yine 409
     `WMS_POSTING_INTEGRITY_ERROR`. (D) yazımı 0 kayıt etkilerse yeniden okunur: `IntegrityError` → yazım yok, 409 (aynı); hâlâ Refused →
     yeni `Version` ile yinelenir (≤ 3; tükenirse yazım yok, 409 `WMS_CONFLICT` + güncel görev — ayrışmayı savunma dalı ya da kapatıcı
     komutların ön araması yakalar, satır 18 / 20). "Başka `TransactionId` ile Posted" dalı **yoktur**: aynı `PostingKey` defterde tek
     harekete bağlanır (`InternalMovementPostingService.cs:93-99` önce anahtarı arar, replay aynı hareketi döner; `:188-194`), bu yüzden
     Posted bir onayın `TransactionId`'si bu denemeninkinden farklı olamaz (bağımsız inceleme 10 F-2). **(E)** Bu denemenin sonucu
     **Refused / IntegrityError** iken onay artık Pending değil (ör. sweeper Posted yazdı) → **yazım yok**, 409 `WMS_CONFLICT` + güncel
     görev (kayıt doğru kabul edilir; kilit alımında 409 `WMS_CONFIRMATION_NOT_RETRYABLE`) (satır 28). Superseded işaretinin hedefleri yazım
     anında `arrayFilters` ile seçilir (`Status == Refused`, `Resolution == null`); bu arada `IntegrityError` olan kardeş onay **atlanır**,
     yazımı engellemez; yinelemede kapatılan `seq` listesi yeniden okumadan hesaplanır. Posted → `TransactionId/Number`, `ConfirmedQuantity +=`, durum
     (InProgress / Completed), olay; ya da Refused → INVENTORY kodu aynen, görev miktarı değişmez, olay. Cevap: Posted 201; Refused
     422 INVENTORY kodu ve `details` ile. **İşlem 1'den sonraki her cevap onayın `seq`'ini taşır** (bağımsız inceleme 11 F-1): 201 ve
     replay 200 gövdede `confirmationSeq`; 422 / 503 / 409 `details.seq` (`details.task` yanında); işlem 1'den önceki retlerde onay ve `seq`
     yoktur.
     **INVENTORY cevap eşlemesi (F-2, inceleme 02 F-1; Codex incelemesi P2):** 2xx → Posted; **400 `IDEMPOTENCY_KEY_REUSED` →
     `IntegrityError`** (aynı anahtarla farklı bir hareket işlenmiş — bütünlük hatası, Refused değil; 409 `WMS_POSTING_INTEGRITY_ERROR`,
     operatör eylemi kapalı, istisna); **kesin 4xx — 422 ve diğer 4xx (400 / 403 / 404 / 409): defter isteği işlemeden reddetti → kalıcı
     Refused** (hata aynen saklanır, yeniden denenmez); **commit sonucu kesin bilinmeyen her sonuç → Pending:** her 5xx (500 / 502 / 503 /
     504), zaman aşımı ve posting süresi aşımı, ağ hatası, iptal ve posting çağrısından çıkan **beklenmeyen istisna** (işleyici yakalar).
     Bu sonuçlarda onay Pending kalır, kilit `InFlightUntilUtc`'ye kadar korunur, cevap 503 (`INVENTORY_UNAVAILABLE` ya da INVENTORY'nin
     kendi 503 kodu; 500 / 502 / 504 ve istisnada `details.reason = OutcomeUnknown`, `details.seq`); sonraki deneme ve sweeper **önce
     `PostingKey` aramasıyla** uzlaştırır (commit olduysa Posted, olmadıysa yeniden deneme aynı anahtarla posting yapar). 5xx ya da istisna
     **Refused yazmaz**: commit olmuş bir hareket Refused sayılırsa defter ile görev ayrışır.
  5. Commit sonucu bilinmeyen her sonuç (yukarıda) ya da süreç çöküşü → onay `Pending` kalır; cevap 503. **Bilinçli kabul —
     belirlenimci 5xx / istisna (sahip kararı Beste 2026-10-26, bağımsız inceleme 13 F-3):** her denemede tekrarlanan bir 5xx ya da
     istisna onayı süresiz Pending'de bırakabilir; güvenli tercih kalır (bilinmeyen sonuç Refused sayılmaz, **deneme sınırı yok** — M-5).
     Bu sürede görevde yeni onay, düzenleme, üstlenme, iptal ve kısa kapatma **409 `WMS_CONFIRMATION_PENDING`** alır (§8.15 kilidi);
     "Uzlaştır" yalnız `IntegrityError` içindir, Pending'i çözmez. **Çıkış yolu:** (i) onay `ConfirmedAtUtc`'den bu yana
     `Warehouse:Putaway:LongPendingMinutes` (öneri 30; açılışta `LongPendingMinutes > 0` ve `LongPendingMinutes × 60 > InFlightLeaseSeconds`
     doğrulanır, değilse host başlamaz — fail-closed ayar, §4.2; bağımsız inceleme 14 F-3) süresinden uzun Pending kalırsa
     türetilmiş bayrak `PostingPendingLong` ve ekranda "Uzun süredir bekliyor" rozeti (§4.1, §8.15; süre `TimeProvider` ile ölçülür);
     (ii) bu kayıtlar S3 İstisna Kutusu'na girer — istisna türü `PostingPendingLong`, bayrakla aynı ad ve aynı `LongPendingMinutes` eşiği
     (WMS taslağı §9; bağımsız inceleme 14 F-2) (AC-36 kapsamı; S1'de yalnız rozet, liste kodu S3'te); (iii) çözüm stok tarafındaki
     hatanın giderilmesi (MOD-0173 sahibi) + "Yeniden dene"dir (aynı anahtar, deneme sınırı yok); sweeper `PostingKey` aramasını sürdürür. **Kurtarma (inceleme 02 F-1, F-5):**
     - **Yeniden deneme sırası** (`RetryPutawayConfirmationCommand`, kullanıcının "Yeniden dene"si): (0) **uçuş kilidi atomik alınır**;
       kilit başka bir denemedeyse ve süresi dolmadıysa 409 `WMS_CONFIRMATION_IN_FLIGHT`, hiçbir şey yazılmaz (inceleme 03 F-1);
       (i) **önce** ledger'da `PostingKey` aranır (`IPostingKeyLookup`, §5; upstream çağrısı yok). Sonuç `Committed` ve holder ters
       kaydedilmemişse onay **Posted** yazılır (işlem 2), **ön kontrol koşmaz**, upstream çağrılmaz. Sonuç `Committed` ama holder ters
       kaydedilmişse (`reversedByTransactionId` dolu) onay Posted **yazılmaz**, `IntegrityError` (`details.reason = HolderReversed`) olur;
       aynı kural sweeper için geçerlidir (bağımsız inceleme 05 F-2). Sonuç `KeyReusedDifferentPayload` ise onay `IntegrityError` olur. (ii) Yalnız `NotCommitted` ise 0178 ön kontrolleri koşar
       (aşağıda) ve tutarsa aynı `PostingKey`, aynı tarihler, aynı serilerle posting yapılır (kullanıcının token'ıyla).
     - **Sweeper = yalnız uzlaştırıcı** (sahip notu Beste 2026-10-23; inceleme 02 F-5 kalanı): hosted service
       (`PendingConfirmationReconcilerHostedService`; 2 dakikadan eski Pending'ler 60 sn'de bir; karar Q-09) arka planda **kullanıcı
       token'ı taşımaz** (`ForwardCallerHeadersHandler.cs:21-35` Authorization'ı yalnız `HttpContext`'ten iletir). Her kayıt için ayrı
       komut **`ReconcilePendingConfirmationCommand`** çalışır: **yalnız ledger araması** (`IPostingKeyLookup`), upstream çağrısı yok,
       ön kontrol ve posting yok. **Yazım filtresi (inceleme 04 F-3):** sweeper kilit almaz ve `AttemptId` taşımaz; okuma ve yazma
       filtresi `Status == Pending` **ve `InFlightUntilUtc` boş ya da geçmiş** (+ `ConfirmedQuantity + q ≤ PlannedQuantity`). Okuma ile
       yazma arasında bir kullanıcı denemesi kilit aldıysa yazım 0 kayıt etkiler ve sweeper o kaydı **hiçbir şey yazmadan bırakır**
       (**yalnız bu "0 kayıt etkilendi" durumunda** sweeper `IntegrityError` üretmez; kayıt kullanıcı denemesiyle ilerler — `Committed` /
       `KeyReusedDifferentPayload` sonuçlarının kendi dalları aşağıdadır). **Savunma dalı (inceleme 05 F-3):** sweeper ayrıca son 30 dk
       içinde **bir yeniden denemede** Refused olmuş onayları — ret kaynağı ön kontrol **ya da defter 4xx** (`RefusalSource ∈ {PreCheck,
       Ledger4xx}`, `RefusedOnRetry = true`, `RefusedAtUtc` kayıtta; bağımsız inceleme 06 F-2: kilit dolduktan sonraki denemenin defter 4xx'i
       de aynı geç commit riskini taşır) — **`Resolution`'dan bağımsız** (Superseded / Returned dahil) ve **görev durumundan
       bağımsız** (terminal görev dahil; bağımsız inceleme 05 F-1) — ledger'da arar; `Committed` (ters kaydedilmemiş) bulursa (geç commit —
       hareket defterde, WMS'te Refused) onayı **tek koşullu güncellemeyle** `IntegrityError` (`details.reason = StatusDiverged`) yapar (filtre:
       `Status == Refused` ve `Version == okunan sürüm`; aynı yazımda `PriorResolution = Resolution`, `Resolution = null`, `Version++`),
       yerel olay `ConfirmationIntegrityError`, merkezi denetim 0 (iç komut başarısız zarfla döner, §8.8); böylece ayrışma kalıcı olmaz ve "Uzlaştır" ile çözülür. **Kayıt başına hata yalıtımı:** her kayıt kendi
       try/catch'inde koşar, bir kaydın hatası diğerlerini durdurmaz (`RecallQuarantineSweeper.cs:68-88` deseni). **Seçim (bağımsız
       inceleme 07 F-4):** kayda yazmadan **ilerleyen imleç** — `(TenantId, _id)` sırası, tur başına en çok 200 kayıt, imleç turlar arasında
       bellekte tutulur (tur bir sonrakine son görülen `(TenantId, _id)`'den devam eder; uçtan sonra başa döner, host yeniden başlarsa
       sıfırdan); `NotCommitted` kayıt bir sonraki turda imleç ilerlediği için aynı 200'ü tıkamaz (0177'nin `DeferAsync` yazımı gerekmez,
       2026-10-23 "NotCommitted'da yazım yok" kararıyla uyumlu). **Normal dal ve savunma dalı ayrı imleç ve ayrı bütçe** (her biri tur başına
       ≤ 200); iki sorgunun index'leri §4.6. **Kiracılar arası tarama okuması açık istisnadır** (yalnız hosted service; her kayıt sonra kendi kiracı + şirket
       kapsamında işlenir). Kilit geçerliyse kayıt atlanır. `Committed` → Posted (işlem 2 koşullu, yerel olay `ConfirmationReconciled`,
       merkezi denetim 1); `KeyReusedDifferentPayload` → `IntegrityError` (yerel olay `ConfirmationIntegrityError`, merkezi denetim 0);
       **`NotCommitted` → hiçbir şey yazılmaz: `Attempts` artmaz, yerel olay ve merkezi denetim yok — iç komut hiç gönderilmez** — onay Pending kalır, kullanıcı "Yeniden
       dene" ile ilerletir. **Kapsam (ölçüldü):** `RecallQuarantineSweeper.cs:72-76` deseni — her kayıt için yeni DI kapsamı,
       `ITenantContext.SetScope(TenantId, LegalEntityId, aktör)`, `IPostingActorName.Use(ad)`, yeni correlation; ledger deposu bu kapsama
       bağlıdır (`InventoryLedgerRepository.cs:100-103` `TransactionScope()`); aktör = onayın `ConfirmedBy`'ı. Zamanlama 0177 KR-B sweeper'ına benzer ama **aynı değildir**:
       0177 `MaxDeferredAttempts` ile sınırlıdır (`RecallQuarantineSweeper.cs:19`, varsayılan 10; FU-0177-SWEEP-01); 0178'de deneme sınırı
       yoktur (M-5) çünkü sweeper upstream çağırmaz ve kullanıcı denemesi çift hareket üretmez. Sınırsız deneme **kullanıcının
       "Yeniden dene"si** için geçerlidir; sweeper yalnız uzlaştırır (sahip notu Beste 2026-10-23).
- **Kısmi yerleştirme:** bir görev birden çok onayla (farklı hedef binlere de) tamamlanabilir; her onay ayrı hareket. **Seri kontrollü
  SKU'da kısmi onay ve kısa kapatma yoktur** (§8.16).
- **Yeniden denemede ön kontroller (F-4, sıra inceleme 02 F-1):** ön kontroller **yalnız ledger anahtarı işlenmemişse** (`NotCommitted`)
  koşar: recall (§8.5), kaynak kova hâlâ `AVAILABLE` (§8.4), saklama koşulu uyumu (§8.12), seri kovası değişmedi (§8.16). Tutmazsa onay
  **Refused** olur (ön kontrol kodu, `ErrorStatus` 422), hareket denenmez — bekleyen onayın lotu bu arada recall'a girerse denetimsiz
  `LOCATION_TRANSFER` yazılmaz. Anahtar işlenmişse (cevabı kaybolmuş hareket) kaynak kova boşalmış ve seriler hedefe geçmiş olduğundan
  ön kontrol **koşturulmaz**; onay Posted olur — böylece işlenmiş hareket Refused sayılmaz ve "yeni onay" ikinci hareket yazmaz.
- **`IntegrityError` (inceleme 02 F-1):** "yeni onay ile kapatma" (Superseded) ve "Geri taşındı" (Returned) **kapalıdır** (409
  `WMS_CONFIRMATION_NOT_RESOLVABLE`); görev kilitli kalır (yeni onay, düzenleme, iptal, kısa kapatma 409 `WMS_POSTING_INTEGRITY_ERROR`);
  durum istisna olarak listelenir (S3 İstisna Kutusu; o gelene kadar görev detayında kırmızı rozet "Bütünlük hatası — Control Tower'a
  bildirin"). Defter düzeltmesi / REVERSAL 0178'in değil MOD-0173 sahibinin işidir.
- **"Uzlaştır" — `IntegrityError`'dan tek çıkış (inceleme 03 F-2):** `ResolvePutawayIntegrityCommand`, yalnız sorumlu (izin
  `warehouse.putaway-tasks.resolve`, §14.2), zorunlu gerekçe (10–1000). **Görevde Pending ya da uçuştaki bir onay varken 409
  `WMS_CONFIRMATION_PENDING`** (inceleme 05 F-1). Önce ledger **yeniden okunur** (`IPostingKeyLookup`, anahtarı
  tutan hareketi `holder` olarak döndürür, §5). Defter append-only olduğu için "defter sonradan düzeltildi" yolu **yoktur** (inceleme 04
  F-1); karar anahtarı tutan hareketin **kaynağına** göre verilir:
  - **(a) Kaynak bu onay** — `holder.sourceModule = MOD-0178`, `sourceType = PUTAWAY_TASK`, `sourceDocumentId = taskId`,
    `sourceLineId = seq` (`Committed` ya da parmak izi farklı `KeyReusedDifferentPayload`; ör. tarih / seri farkı yaratan bir hata): stok
    gerçekten taşınmıştır → onay **Posted** (`IntegrityResolution = ResolvedOwnMovement`); onaya **defterdeki gerçek değerler** yazılır
    (`TransactionId/Number`, miktar, hedef lokasyon, **lot, seriler**, stok durumu — seri SKU'da onayın işlem 1'deki seri listesi
    defterin serileriyle değiştirilir, fark `ConfirmationDeviation`'da listelenir; inceleme 05 F-4), `ConfirmedQuantity +=` defter miktarı
    (`ConfirmedQuantity > PlannedQuantity` olabilir — §4.1 istisnası; görev `Completed`). Onayın kendi değerlerinden farkı varsa ya da
    toplam planlananı aşıyorsa olay **`ConfirmationDeviation`** (`details`: farklı alanlar, `OverPlanned`) açılır; görev türetilmiş
    durumuyla ilerler (InProgress / Completed). Ledger doğrudur, görev ona uyar.
  - **(b) Kaynak yabancı** (başka modül / başka görev / başka satır), `NotCommitted` **ya da kendi hareketi ama ters kaydedilmiş**
    (`holder.reversedByTransactionId` dolu — defterde stok kaynakta; bağımsız inceleme 05 F-2): bu onayın geçerli bir hareketi yoktur → onay
    **çözümsüz Refused**'a döner (`IntegrityResolution = ReturnedToRefused`, yabancı hareketin `holder` değerleri olay verisine yazılır —
    MOD-0173 sahibine istisna notu). **Görev AÇIK kalır**; normal Refused akışı kullanılır: neden giderilince **yeni onay** (yeni `seq`,
    yeni anahtar) ya da **"Geri taşındı"**. Operatör / sorumlu malın fiziksel konumunu doğrulamaya yönlendirilir (ekran metni).
    **Görevi kapatmak ayrı ve açık bir seçimdir** (İptal: sorumlu; Kalanı kapat: atanan ya da sorumlu); "Uzlaştır" görevi **otomatik
    kapatmaz**. Terminal görevde (Completed / Cancelled) "Uzlaştır" yalnız onay kaydını düzeltir, görevi yeniden açmaz (akış tablosu satır 23–24).
    Açık miktar, `OpenSerialSlot` ve rol kilidi görev kapanınca ya da tamamlanınca serbest kalır.
  - Yerel olay `IntegrityResolved` (+ gerekirse `ConfirmationDeviation`), merkezi denetim 1 (K2; komut başarılı). Ön kontrol ve posting yapmaz; upstream
    çağırmaz. `NotCommitted` + `IntegrityError` gerçek akışta oluşmaz (bütünlük hatası ancak işlenmiş bir anahtarla doğar); savunma
    amaçlı (b) gibi ele alınır.
  - **Not (inceleme 03 F-2):** `PostingKey` (`wms:put:{taskId}:{seq}`) tahmin edilebilir ve INVENTORY idempotency alanı kiracı + şirket
    içinde bütün çağıranlarla ortaktır; `postMovement` izni olan bir kullanıcı sıradaki anahtarı önceden harcayıp onayı `IntegrityError`'a
    düşürebilir (0175 anahtar deseniyle aynı risk; hareket denetim izi bırakır). "Uzlaştır" bu durumu kapatır; anahtarın tahmin
    edilemez yapılması (işlem 1'de bir kez üretilen rastgele bir parça anahtara eklenir, onaya yazılır ve her denemede aynen
    gönderilir — tarihler ve seriler gibi) FU-0178-KEY-01.
- **Refused sonrası operatör akışı (F-4):** onay `Refused` iken mal fiziksel olarak hedefte, defter kaynakta kalır. Operatörün iki yolu:
  (a) neden giderilince **aynı görevde yeni onay** (aynı ya da başka hedef; yeni `seq`, yeni anahtar) → Posted olunca eski Refused
  onay `Resolution = SupersededByNewConfirmation`; (b) **"Geri taşındı"** — mal fiziksel olarak kaynağa iade edildi:
  `MarkPutawayConfirmationReturnedCommand` (gerekçe 10–1000), hareket yok, `Resolution = ReturnedToSource`, "Stok reddetti" rozeti
  kalkar, görev kalan miktarla açık kalır (sonra yeni onay, iptal ya da kısa kapatma). Çözümsüz Refused onaylar ileride S3 İstisna
  Kutusu'nda listelenir.
- **Geç commit savunması — kapatmadan önce ledger araması (bağımsız inceleme 05 F-1):** yeni onay (Superseded'a yol açar), "Geri
  taşındı", İptal ve Kalanı kapat, **komutun kendi yazımından önce** görevin kapatacağı / etkileyeceği Refused onaylarının anahtarlarını
  ledger'da arar (`IPostingKeyLookup`, upstream'siz). Biri `Committed` (ters kaydedilmemiş) çıkarsa o onay `IntegrityError`
  (`StatusDiverged`, eski `Resolution` → `PriorResolution`) olur, olay `ConfirmationIntegrityError`, komut **409
  `WMS_POSTING_INTEGRITY_ERROR`** döner ve kendi değişikliğini yazmaz (sorumlu önce "Uzlaştır"). Commit komuttan **sonra** görünürse
  savunma dalı (sweeper) onu `Resolution`'dan ve görev durumundan bağımsız yakalar (satır 18).
- **Sürüm koşulu (CAS) — her yazım (bağımsız inceleme 06 F-3, 07 F-1 / F-2):** görev kaydına yapılan **her** yazım filtreye
  `Version == okunan sürüm` koyar ve `Version++` yapar. Hedef küme ve filtre **komut başına**:
  | Yazım | Hedef küme | Filtre (V'ye ek) | 0 kayıt etkilenirse |
  |---|---|---|---|
  | İşlem 2 / retry (i) Posted yazımı / sweeper uzlaştırması | bu onay (+ Superseded hedefleri `arrayFilters`) | `Status == Pending`, `AttemptId` (sweeper: kilit boş / geçmiş) | **istisna:** yukarıdaki tek kural (A)–(E): yeniden oku, onay hâlâ Pending ve deneme bunsa koşullu artışı yeniden değerlendir ve yinele (≤ 3; tükenirse 503 `WMS_CONFIRMATION_CONFLICT_RETRY`) — aynı kural kilit alımı, ön kontrol reddi ve IntegrityError yazımları için de |
  | Yeni onay (işlem 1) | yeni onay; ön arama = görevin çözümsüz Refused onayları | If-Match = V, görevde Pending yok | yeniden oku → hedef `IntegrityError` ise 409 `WMS_POSTING_INTEGRITY_ERROR`, değilse 409 `WMS_CONFLICT` |
  | "Geri taşındı" | yalnız bu onay (ön arama yalnız onun anahtarı) | `Status == Refused`, `Resolution == null` | aynı |
  | İptal / Kalanı kapat | görev; ön arama görevin **tüm** Refused onayları (`Resolution`'dan bağımsız — "Geri taşındı"dan sonra da çalışır) | hedeflerin `Status == Refused` (**`Resolution` koşulu yok**) | aynı |
  | Savunma dalı / ön aramanın `IntegrityError` yazımı | tek onay | `Status == Refused` | yazım yok; sweeper bir sonraki turda yeniden değerlendirir (30 dk penceresi içinde) |
  | Uzlaştır | tek onay | `Status == IntegrityError`, görevde Pending yok | yeniden oku → 409 `WMS_CONFLICT` |
  | Commit sonucu bilinmeyen posting (5xx / zaman aşımı / ağ hatası / iptal (`OperationCanceledException`) / beklenmeyen istisna; Codex P2, bağımsız inceleme 13 F-2) | yazım yok | — (sonuç yazımı yapılmaz) | geçerli değil: onay Pending, kilit korunur, 503; sonraki deneme / sweeper `PostingKey` aramasıyla uzlaştırır |
  | 0-satır (D) yazımı (bu deneme Posted / replay, kayıt Refused; satır 11; bağımsız inceleme 10 F-2) | tek onay | `Status == Refused` | yeniden oku → onay `IntegrityError` ise yazım yok (`PriorResolution` korunur), 409 `WMS_POSTING_INTEGRITY_ERROR`; hâlâ Refused ise yinele (≤ 3), tükenirse yazım yok, 409 `WMS_CONFLICT` + güncel görev |
  | Her anahtarlı komut yazımı (yaratma: kayıt; diğerleri: anahtar olayı; satır 29; bağımsız inceleme 10 F-1, 11 F-3) | komutun kendi hedefi + anahtar olayı | satırın kendi filtresi + tekil anahtar index'i (§4.6); yaratmada ayrıca rolün canlı `LocationId` / görevin seri slot tekil index'i | **her** yinelenen kayıt hatası (hangi tekil index olursa olsun) ya da 0 kayıt → **önce anahtar yeniden aranır**: aynı parmak izi → 200 replay, farklı → 400 `IDEMPOTENCY_KEY_REUSED`; anahtar yoksa satırın kendi kuralı (rol `LocationId` → 409 `WMS_LOCATION_ROLE_EXISTS`; seri slot → 422 `WMS_QUANTITY_EXCEEDS_OPEN`) |

  Böylece `IntegrityError` ile `Resolution` aynı onayda **asla birlikte bulunmaz** (savunma ve ön arama yazımı `Resolution = null`'u aynı
  güncellemede yapar; "Geri taşındı" `Status == Refused` ister) ve sistem yazımı kullanıcının If-Match'ini geçersiz kılar (kullanıcı ekranı
  yeniden yükler; yeniden yüklenen komut bir sonraki denemede geçer — sonsuz 409 yok).
- **Açık miktar yarışı (CT ek bulgusu; tek yol inceleme 02 F-7):** görev yaratmada açık miktar kontrolü **iyimserdir** (aynı anda açılan
  iki görev açık miktarı birlikte aşabilir; kilit yok). 0178'in onay ön kontrolü kovanın **durumuna** bakar, **miktar yeterliliğine
  bakmaz**; yeterlilik yalnız defterin işidir: onay kaydı açılır (işlem 1), ledger 422 `INSUFFICIENT_STOCK` döner → onay **Refused**
  (merkezi denetim 0, yerel zaman çizelgesi 2: Recorded + Refused — AC-15); stok eksiye düşmez. Seri SKU'da eşzamanlı ikinci görev kısmi tekil index ile engellenir (§4.6), yine de
  onayda `WMS_SERIAL_BUCKET_CHANGED` güvenlik ağıdır.

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

### 8.8 Merkezi denetim (E-2 / BL-501) ve yerel GxP izi — sahip kararı (Beste 2026-10-26)
- **BL-501 olduğu gibi kalır** (`services/Diten.Building.Blocks/**` korunan yol, §6). Ölçülen davranış (`58c2cd6e6`; kayıt biçimi ve
  satır durumları bağımsız inceleme 09 için `963f6230a`'da yeniden okundu):
  - **Kayıt biçimi — ad ve gövde yok (09 F-1):** bildirim `CentralAuditMetadata(Category, Operation, EntityType, SourceModule)` ve
    `CentralAuditEntityId(response)`'tur (`…/Diten.BuildingBlocks.CentralAudit.Abstractions/ICentralAuditDeclaration.cs:13-30`: "the request
    body is never copied into the record"). Kayda ayrıca aktör, kiracı, şirket, korelasyon, zaman ve `RequestType` (komut sınıfının adı)
    yazılır (`…/Diten.BuildingBlocks.CentralAudit/CentralAuditRecorder.cs:96-116`, `CentralAuditBehavior.cs:36`); Platform'a giden
    `Metadata` yalnız eventId / companyId / writtenInCommandTransaction / confirmedByCommand'dır (`PlatformCentralAuditClient.cs:63-89`).
    `Category` ve `Operation` Platform enum **adlarıdır** (`CentralAuditPlatformValues.cs`; Operation: Create, Update, Delete, …, Assign, …,
    Execute, LifecycleTransition); enum dışı ad iletimde `Rejected` olur (`PlatformCentralAuditClient.cs:54-60`) ve
    `CentralAuditDeclarationTests.cs:39-57`'de kırmızıdır. Bildirim komut türünden, işleyici koşmadan okunur (`CentralAuditRecorder.cs:95`):
    komut başına **sabittir**, sonuca göre seçilemez. Sonuç: merkezi kayıt **kim, hangi komut, hangi nesne, ne zaman, hangi sonuç**
    sorusunu cevaplar; **ne değişti** (onay `seq`, miktar, TransactionNumber, kapatılan `seq` listesi, `holder`, önceki / sonraki değerler)
    yalnız yerel zaman çizelgesindedir (§4.3). Komut başına değerler §21'de.
  - **Satır durumları (09 F-2 a):** komutun işlemi enlist edince satır `AwaitingCommand` olarak işlemle birlikte yazılır
    (`CentralAuditRecorder.cs:123-140`); aynı komutun sonraki enlist'leri aynı satırı bulur ve no-op'tur (`CentralAuditOutboxStore.cs:81-91`,
    `CentralAuditRecorder.cs:11-12`). Komut **başarılı** biterse satır `Pending`'e geçer (`ConfirmAsync`, `CentralAuditOutboxStore.cs:111`)
    ve dağıtıcı iletince `Delivered` olur; istisna ya da başarısız zarf (`Response.IsSuccessful = false`) → `Abandoned` (`:113`;
    `CentralAuditBehavior.cs:51`, `:59-63`) ve **iletilmez**. Hiçbir işlemi commit olmamış başarısız komut satır bırakmaz.
  - **Yazmadan başarılı komut (09 F-2 c):** `RequireEnlistment` altında hiç enlist etmeden başarılı biten komut **kayıt bırakmaz**
    (`CentralAuditRecorder.cs:160-168`, "idempotent replay, no-op"). 0178'de: **her** yazma komutunun tekrarı (aynı anahtar + aynı parmak izi, 200 replay; §8.1, AC-4, §13.1–13.2, §13.24),
    sweeper iç komutunun (B) dalı ve 0-kayıt bırakması, hiçbir satırı yazılmayan toplu atama / toplu rol silme → merkezi 0 / yerel 0.
  - **Çökme (09 F-2 b):** komutun bir işlemi commit olduktan sonra, `ConfirmAsync` / `AbandonAsync`'ten önce süreç ölürse satır
    `AwaitingCommand` kalır; dağıtıcı `OrphanAfter` (10 dk, `CentralAuditOptions.cs:37`) sonunda onu `ConfirmedByCommand = false` ile
    `Pending`'e alır (`CentralAuditOutboxStore.cs:121-131`, `CentralAuditDispatcher.cs:54`) ve Platform'a **`Unconfirmed`** sonucuyla iletir
    (`CentralAuditPlatformValues.cs:64-67`). 0178'de en olası yer onayın işlem 1'i ya da yeniden denemenin kilit alımı sonrasıdır →
    1 `Unconfirmed` `ConfirmPutawayTaskCommand` / `RetryPutawayConfirmationCommand` kaydı. Onay sonradan Refused olsa da bu kayıt kalır;
    sweeper sonra Posted yazarsa aynı onay için **2** merkezi kayıt olur (`Unconfirmed` + `ReconcilePendingConfirmationCommand`
    `Succeeded`). Bu BL-501'in tasarımıdır ve kabul edilir; doğru sıra yerel zaman çizelgesidir.
  - Defterin `PostAsync`'i işlem içinde `CentralAuditTransaction.EnlistAsync` çağırır (`InventoryLedgerRepository.cs:181`): 0178 komutu
    `InternalMovementPostingService`'i süreç içi çağırdığında (ayrı bir MediatR komutu yok) defterin enlist'i **0178 komutunun satırına**
    gider ve işlem 1'de zaten yazılmış olduğu için no-op'tur — **ayrı bir "defterin hareket satırı" merkezi kaydı doğmaz**. Hareketin
    defterdeki kendi işlem içi izi (MOD-0173 `inventory_audit` + outbox) defterin işidir.
- **Enlist kuralı (0178; bağımsız inceleme 09 F-2):** her 0178 yazımı `WithTransactionAsync` içinde koşar; **iş yazımı en az bir kayıt
  etkiledikten sonra** aynı callback'te yerel olayı yazar ve `CentralAuditTransaction.EnlistAsync(session, entityId)` çağırır. 0 kayıtta
  callback ne olay yazar ne enlist eder (satır yok). Toplu komutlar `entityId` vermez (satırın `EntityId`'si boş; tek nesne yok).
  `CentralAuditDeclarationTests.cs:73-108` her callback'te enlist çağrısını arar (§5 beşinci seam).
- **Sonuç (bağlayıcı):** merkezi denetim **yalnız başarılı ve yazan komutları** iletir; komut başına **1** kayıt (çökmede `Unconfirmed`).
  Retler (Refused), Pending (503), IntegrityError, her 409 ve her 503 **iletilen** merkezi kayıt bırakmaz (0; satır varsa `Abandoned`).
- **Yerel GxP izi:** bu sonuçların ve bütün değerlerin izi depo modülünün **silinmeyen, yalnız ekleme yapılan** zaman çizelgelerinde
  tutulur — görev için `wms_task_events`, rol için `wms_location_role_events` (her `Kind`'ı yazan yazım §4.3'te) — ve **ilgili yazımla aynı
  Mongo işleminde** yazılır. Zaman çizelgesi yazılamazsa iş yazımı da geri alınır.
- **K2'nin anlamı (bu paket için):** K2 = başarılı komutun merkezi kaydı veriyle **aynı işlemde** (`RequireEnlistment = true`; yazılamazsa
  komut geri alınır — kapalı-başarısız); başarısız sonuçların ve bütün değerlerin izi **yerel** zaman çizelgesindedir (aynı işlem,
  silinmez). Merkezi kayıt bir rette ve değer düzeyinde "boş" görünür; tam GxP izi zaman çizelgesidir.
- **Sweeper iç komutu:** hosted service ledger aramasını işaretsiz bir okumayla yapar ve iç komutu (`ReconcilePendingConfirmationCommand`)
  yalnız yazılacak bir şey varsa gönderir; kapsamı onayın `ConfirmedBy`'ıdır (kayıt o kullanıcıyı `TenantUser` olarak adlandırır, adsız —
  `…/Api/CentralAudit/InventoryCentralAuditScope.cs:13-15`). Komut Posted yazarsa başarılıdır (merkezi 1); IntegrityError yazarsa
  (KeyReused / HolderReversed / savunma dalı) **başarısız zarfla** döner (iletilen 0, satır `Abandoned`, yerel iz var); `NotCommitted`'ta
  komut hiç gönderilmez (0 / 0); (B) dalı ve 0-kayıt bırakması yazmaz (0 / 0).

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
- **Okuma yolu ve kabul ortamı (F-5):** InventoryService bugün `storageConditions` okumuyor (`SkuReference.cs:13-24`,
  `ProductMasterHttpClient.cs:62-82`; repo grep 0). Okuma §5'teki MOD-0173 seam'iyle eklenir. S1 kabulü dev PRODUCT-MASTER köprüsü
  4010 (veri setine `storageConditions` eklenmiş) ile E3 / E4; canlı kabul PM-LIVE sonrası. Köprü alanı döndürmezse her oluşturma ve
  onay 422 `WMS_STORAGE_CONDITIONS_UNKNOWN` alır — bu beklenen fail-closed davranıştır, kabul sayılmaz.

### 8.13 Onay yetkisi ve üstlenme (M-5)
- Onayı **atanan operatör** ya da **sorumlu** (`warehouse.putaway-tasks.update` sahibi) verir; diğerleri 403 `WMS_NOT_ASSIGNEE`.
- Operatör **atanmamış** görevi üstlenebilir: `ClaimPutawayTaskCommand` = kendine ata; ayrı izin yok, **`warehouse.putaway-tasks.confirm`
  yeter** (öneri, sahip yönlendirmesi "önerdiğin gibi"). Atanmış görevi üstlenme 409 `WMS_TASK_ALREADY_ASSIGNED`; yeniden atama
  sorumlunun işidir. Kullanıcı listesi ucu yoksa (Q-13) atama yalnız üstlenmeyle olur.
- Üstlenme **seçili depodaki atanmamış görevler** içindir ("Üstlen" listesi depo filtresine bağlı); sorumlu atanmış görevi yeniden
  atayabilir (`UpdatePutawayTask` / `BulkAssignPutawayTask`, `warehouse.putaway-tasks.update`) (tur 2).
- **Pending yeniden deneme:** deneme sınırı yok (aynı `PostingKey` çift hareket üretmez); `Attempts` ve son deneme zamanı ekranda
  görünür; INVENTORY reddederse onay `Refused` olur ve "Stok reddetti" rozeti çıkar (tur 2). Sweeper yalnız ledger anahtarını kontrol eder;
  işlenmemiş Pending onayı kullanıcının "Yeniden dene"si ilerletir (§8.3 adım 5, inceleme 02 F-5). Asıl onay uçuştayken "Yeniden dene"
  409 `WMS_CONFIRMATION_IN_FLIGHT` alır; ekran "Onay işleniyor — kilit {süre} sonra kalkar" der ve düğmeyi o süre pasif tutar
  (inceleme 03 F-1).

### 8.14 E-imza — kapsam dışı (M-1)
Putaway onayı **e-imza istemez** ve MOD-0022'ye imzalanabilir eylem kaydetmez. Gerekçe: onay imzalı bir kalite kararı değil, bir
**hareket kaydıdır**; kim, hangi komut, hangi görev, ne zaman merkezi kayıtta (yol b, K2, §21 — gövde yok); ne taşındı (onay `seq`,
miktar, from / to, TransactionNumber) yerel görev zaman çizelgesinde (§4.3) ve INVENTORY hareketinin kendi kaydında durur (bağımsız
inceleme 09 F-1).
Kalite kararları (serbest bırakma, red) MOD-0175'te imzalıdır; 0178 onlara dokunmaz.

### 8.15 Ekran kuralları (M-4 / S1)
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
  rozetler "Onay bekliyor" (`PostingPending`), "Uzun süredir bekliyor" (`PostingPendingLong` — Pending onay `LongPendingMinutes`'tan eski;
  §8.3 adım 5, bağımsız inceleme 13 F-3), "Stok reddetti" (`PostingRefused`) ve "Bütünlük hatası" (`PostingIntegrityError`). Görevde tek rozet görünür; öncelik §4.1'deki
  sıradır: `PostingIntegrityError` > `PostingPendingLong` > `PostingPending` > `PostingRefused` > `None` (bağımsız inceleme 14 F-1).
- Diller en, tr, fr, es, zh, ar, ru; **RTL yalnız ar**.

### 8.16 Seri kontrollü SKU — yalnız tam miktar (F-1; sahip kararı Beste 2026-10-21)
- Ledger her harekette ayrı seri sayısı = baz miktar ister (`MovementMasterDataValidator.cs:257-262`, `SERIAL_COUNT_MISMATCH`).
- S1'de seri kontrollü SKU (Lsku / FinishedGood `getSku.serialControlled = true`; Gsku'da seri stok yoktur — §4.1, inceleme 02 F-3)
  **yalnız tam miktar** yerleştirilir: görev miktarı = kaynak kovadaki (depo + lokasyon + SKU + lot, `AVAILABLE`) bütün miktar; kova
  başına tek açık görev (kısmi tekil index, §4.6); planlanan miktar düzenlenemez.
- **Seri kaydı stok durumunu bilmez** (`SerialNumber.cs:28,30` yalnız depo / lokasyon). Bu yüzden (inceleme 02 F-2):
  - okuma MOD-0175 emsaliyle yapılır: lokasyondaki Active seriler (`QuarantineLedgerReads.cs:108-120`) eksi aynı kovanın **açık
    tutma ve açık bloke kayıtlarının** serileri (`StockActionHandlers.cs:175-200`, ASSUMPTION-0175-Q4-02);
  - ek olarak **aynı lokasyonda aynı lot + SKU'nun `AVAILABLE` dışı stoğu** (QUARANTINE / QUALITY_INSPECTION / BLOCKED / DAMAGED /
    EXPIRED bakiye satırı) varsa seri SKU görevi S1'de **açılmaz ve onaylanmaz** → 422 `WMS_SERIAL_BUCKET_MIXED_STATUS` (7 dilde
    "aynı lokasyonda bu lotun kalite bekleyen parçası var; önce kalite kararını tamamlayın"). Böylece yanlış (karantinadaki) seri
    taşınamaz ve TRACE recall izi bozulmaz.
- Onayda kovadaki **bütün seriler sistemce otomatik okunur** (`IWarehouseSerialReader`, §5; kullanıcı seri girmez) ve harekete konur;
  seri sayısı = miktar. **Ölçülmüş tel biçimi (F-8):** `serialIds` = **seri numarası**; ledger / TRACE numarayı
  `LotIdentityPolicy.NormalizeSerial` ile normalleştirir (`…/Persistence/Traceability/InventoryLotDirectory.cs:161`); okuma da numara
  döner (`QuarantineLedgerReads.cs:118`). **Ölçülmüş lokasyon güncellemesi (F-8):** TRACE `LOCATION_TRANSFER`'da Active serilerin
  `WarehouseId` / `LocationId`'sini hedefe günceller (`InventoryLotDirectory.cs:275-300`, `:295`). Okunan seriler işlem 1'de onaya yazılır
  ve her denemede aynen gönderilir.
- Kısmi onay ve kısa kapatma seri SKU'da yoktur → 422 `WMS_SERIAL_PARTIAL_NOT_SUPPORTED` (7 dilde mesaj).
- Onay anında (ve her yeniden denemede) kovadaki seri kümesi / sayısı görev miktarından farklıysa (başka bir hareket kovayı değiştirdi)
  → 422 `WMS_SERIAL_BUCKET_CHANGED` (`details.expected`, `details.found`); görev iptal edilip yenisi açılır.
- Seri okuması erişilemezse 503 `INVENTORY_UNAVAILABLE`, yazma yok.
- Kısmi seri yerleştirme (okutulan serilerle) MOD-0182 barkod ile gelir: FU-0178-SERIAL-01.

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
│                    ClosePutawayTaskShortCommand.cs · CancelPutawayTaskCommand.cs ·
│                    MarkPutawayConfirmationReturnedCommand.cs · ResolvePutawayIntegrityCommand.cs ·
│                    ReconcilePendingConfirmationCommand.cs                        (sealed record)
├── Queries/         GetPutawayTaskListQuery.cs · GetPutawayTaskByIdQuery.cs · GetPutawayCandidateListQuery.cs
│                    GetPutawayTaskEventListQuery.cs                               (sealed record)
├── Handlers/
│   ├── CommandHandlers/   CreatePutawayTaskHandler.cs · UpdatePutawayTaskHandler.cs · BulkAssignPutawayTaskHandler.cs
│   │                      ClaimPutawayTaskHandler.cs · ConfirmPutawayTaskHandler.cs · RetryPutawayConfirmationHandler.cs
│   │                      ClosePutawayTaskShortHandler.cs · CancelPutawayTaskHandler.cs ·
│   │                      MarkPutawayConfirmationReturnedHandler.cs · ResolvePutawayIntegrityHandler.cs ·
│   │                      ReconcilePendingConfirmationHandler.cs   (sealed class, suffix YOK)
│   └── QueryHandlers/     GetPutawayTaskListHandler.cs · GetPutawayTaskByIdHandler.cs · GetPutawayCandidateListHandler.cs
│                          GetPutawayTaskEventListHandler.cs
├── Validators/      CreatePutawayTaskValidator.cs · UpdatePutawayTaskValidator.cs · ConfirmPutawayTaskValidator.cs
│                    ClosePutawayTaskShortValidator.cs · CancelPutawayTaskValidator.cs · GetPutawayTaskListValidator.cs ·
│                    MarkPutawayConfirmationReturnedValidator.cs · ResolvePutawayIntegrityValidator.cs (gerekçe 10–1000)
└── PutawayTaskModels.cs                                                            (tek dosyada DTO / ViewModel)

Application/Features/WarehouseLocationRoles/   (aynı iskelet: Create / Update / Delete / BulkDelete + GetList / GetById,
                                                WarehouseLocationRoleModels.cs)
Application/Features/Warehouse/Ports/          IWarehouseRecordCodeAllocator.cs · IWarehouseStockReader.cs · IWarehouseLotReader.cs
Application/Features/Warehouse/Services/       WarehouseMovementPoster.cs (tek stok kapısı → InternalMovementPostingService)
Domain/Warehouse/{Entities,Enums,Rules}/       WarehouseTask, TaskConfirmation, WarehouseTaskEvent, WarehouseLocationRole,
                                               PutawayTaskRules (geçişler, kalan), LocationRoleRules
Persistence/Warehouse/                          WarehouseTaskRepository, WarehouseLocationRoleRepository, WarehouseIndexInitializer,
                                               PendingConfirmationReconcilerHostedService
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

### 11.1 Ekran → rota → izin (hepsi Scope = Tenant; `_LayoutTenantShell`)
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
| PutawayTasks | "Geri taşındı" (QuickView, çözümsüz Refused onay, gerekçe) | `PUTAWAY_TASK_RETURNED` | `warehouse.putaway-tasks.confirm` |
| PutawayTasks | "Uzlaştır" (QuickView, `IntegrityError` onay, gerekçe, IsDangerous; yalnız sorumlu) | `PUTAWAY_TASK_RESOLVE_INTEGRITY` | `warehouse.putaway-tasks.resolve` |
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
`Nav.Page.WAREHOUSELOCATIONROLES` — **7 dil** (kiracı modülü). İnceleme 03 ile eklenen anahtarlar (7 dil): `Warehouse.Action.ResolveIntegrity`
("Uzlaştır"), `Warehouse.Action.ResolveIntegrity.Confirm`, `Warehouse.Action.ResolveIntegrity.Reason`, `Warehouse.Flag.PostingIntegrityError`
("Bütünlük hatası"), `Warehouse.Error.WMS_CONFIRMATION_IN_FLIGHT`, `Warehouse.Status.InFlight` ("Onay işleniyor"),
`Warehouse.Resolution.ResolvedPosted`, `Warehouse.Resolution.ResolvedRefused`. Bağımsız inceleme 13 F-3 ile (7 dil):
`Warehouse.Flag.PostingPendingLong` ("Uzun süredir bekliyor"). Nav anahtar biçimi `NavNameLocalizer.Normalize` (büyük harf, yalnız
harf ve rakam).

## 12. Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol | Hata |
|---|---|---|---|---|---|
| `Idempotency-Key` = `idempotencyKey` | Evet (her yazma) | 8–100, eşit; anahtar If-Match'ten ve iş kurallarından önce aranır; aynı anahtar + aynı parmak izi → 200 replay (yazım yok) (§8.1; bağımsız inceleme 10 F-1) | tekil index: yaratma `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)` kayıtta; diğer komutlar `(TenantId, TaskId \| RoleId, ActorUserId, RequestOp, IdempotencyKey)` anahtar olayında (§4.6; bağımsız inceleme 11 F-2) | anahtar araması (eşzamanlı kaybeden yeniden arar) | 400 `INVALID_REQUEST` / `IDEMPOTENCY_KEY_REUSED` (farklı parmak izi) |
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
| seri SKU kısmi onay / kısa kapatma / miktar düzenleme | — | yok (§8.16) | — | — | 422 `WMS_SERIAL_PARTIAL_NOT_SUPPORTED` |
| seri kovası (onay + yeniden deneme) | seri SKU | kovadaki seri sayısı = görev miktarı | — | `IWarehouseSerialReader` | 422 `WMS_SERIAL_BUCKET_CHANGED` / 503 `INVENTORY_UNAVAILABLE` |
| seri kovası karışık durum (oluştur + onay) | seri SKU | aynı lokasyonda aynı lot + SKU'nun `AVAILABLE` dışı stoğu yok | — | INVENTORY bakiye okuması | 422 `WMS_SERIAL_BUCKET_MIXED_STATUS` |
| Gsku seri bayrağı (oluştur) | Gsku | ürün bayrağı (`getProduct.serialControlled`) açıkça `false` | — | PRODUCT-MASTER | 422 `SKU_LEVEL_NOT_STOCKABLE` |
| ledger bütünlüğü (onay / yeniden deneme / sweeper) | — | aynı `PostingKey` ile farklı hareket yok | — | `IPostingKeyLookup` | 409 `WMS_POSTING_INTEGRITY_ERROR` (onay `IntegrityError`) |
| uçuş kilidi (yeniden deneme; onay ucunda 0-satır (C) dalı) | — | kilit boş ya da süresi dolmuş; işlem 2'de `AttemptId` bu deneme | atomik koşullu güncelleme | — | 409 `WMS_CONFIRMATION_IN_FLIGHT` (C1, yabancı kilit geçerli); sonuç yazımında yabancı kilit dolmuş → 409 `WMS_CONFLICT` (C2; bağımsız inceleme 09 F-5) |
| CAS yinelemesi tükendi | — | 0-satır (A) en çok 3 kez | — | — | 503 `WMS_CONFIRMATION_CONFLICT_RETRY` (onay Pending, kilit korunur; hareket defterde olabilir — "Yeniden dene" / sweeper çözer) |
| "Uzlaştır" | sorumlu | yalnız `IntegrityError` onay, gerekçe 10–1000, görevde Pending / uçuştaki onay yok | — | `IPostingKeyLookup` | 409 `WMS_CONFIRMATION_NOT_RESOLVABLE` / 409 `WMS_CONFIRMATION_PENDING` / 400 `REASON_REQUIRED` / 403 |
| yeniden deneme | — | yalnız Pending onay; atanan ya da sorumlu | — | — | 409 `WMS_CONFIRMATION_NOT_RETRYABLE` / 403 `WMS_NOT_ASSIGNEE` |
| "Geri taşındı" | — | yalnız çözümsüz Refused onay, gerekçe 10–1000; atanan ya da sorumlu; görevde Pending onay yok; önce ledger araması (geç commit → 409) | — | `IPostingKeyLookup` | 409 `WMS_CONFIRMATION_NOT_RESOLVABLE` / 409 `WMS_POSTING_INTEGRITY_ERROR` / 409 `WMS_CONFIRMATION_PENDING` / 400 `REASON_REQUIRED` / 403 `WMS_NOT_ASSIGNEE` |
| toplu atama | — | Pending onaylı görev atanmaz | If-Match = V | — | satır sonucu `PendingConfirmation` (409 `WMS_CONFIRMATION_PENDING`) |
| Kalanı kapat / İptal (ve yeni onay) | — | hedef kümenin (§8.3 CAS tablosu; İptal / Kalanı kapat'ta görevin **tüm** Refused onayları) anahtarı ledger'da `Committed` değil; Kalanı kapat: atanan ya da sorumlu; İptal: `cancel` izni | — | `IPostingKeyLookup` | 409 `WMS_POSTING_INTEGRITY_ERROR` / 403 `WMS_NOT_ASSIGNEE` |
| geçici sayaç yazımı | görev oluştur | atomik artış | tekil `(TenantId, CompanyCodePrefix, TypePrefix)` | — | 503 `WMS_RECORD_CODE_UNAVAILABLE` |
| rol için lokasyon tipi | — | `Site` / `Warehouse` değil | — | LOCATION | 422 `WMS_LOCATION_TYPE_NOT_ALLOWED` |
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
| atalar arası rol çakışması (F-13) | — | atası / alt ağacı karşı rolü taşıyan düğüme rol verilemez | — | `getLocationPath` + alt ağaç | 409 `WMS_LOCATION_ROLE_INHERITED_CONFLICT` |
| sayfa | — | `page ≥ 1`, `pageSize ≤ 200` | — | — | 400 |
| INVENTORY ret kodları (onayda) | — | aynen iletilir: `INSUFFICIENT_STOCK`, `LOT_BLOCKED`, `LOT_EXPIRED`, `LOT_RETEST_DUE`, `UNKNOWN_LOCATION`, `SERIAL_NOT_AVAILABLE`, `ITEM_NOT_ACTIVE`, … | — | — | 422 (onay `Refused`) |

**Yeni kod adları (`WMS_*`) öneridir;** WMS-BUNDLE §5'te tek listede. `LOT_UNDER_RECALL`, `LOT_BLOCKED`, `LOT_EXPIRED`,
`UNKNOWN_LOCATION`, `REASON_REQUIRED`, `IDEMPOTENCY_KEY_REUSED`, `CROSS_LE_FORBIDDEN` mevcut kodlardır (yeniden tanımlanmaz).

## 13. Failure Path to Verify

1. **Duplicate:** aynı kullanıcı aynı anahtarla iki "görev oluştur" → 1 görev, 2. cevap 200 replay (görevin **güncel** hâli; ilk cevabın
   gövdesi değil — §8.1); farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`; başka kullanıcının aynı anahtarı ayrı istektir (yeni görev)
   (bağımsız inceleme 11 F-1 / F-2).
2. **Çift onay / tekrar:** aynı onay isteği (aynı `Idempotency-Key`, aynı gövde, aynı `If-Match`) iki kez → tek hareket
   (`sourceModule MOD-0178`, `sourceDocumentId = taskId`, `sourceLineId = seq`), ikinci cevap **200 replay** — 409 `WMS_CONFLICT` değil:
   anahtar If-Match'ten önce aranır (§8.1; bağımsız inceleme 10 F-1); defterde aynı `PostingKey` ile 1 satır, ikinci onay kaydı yok; replay cevabı ilk isteğin onayının `confirmationSeq`'ini taşır
   (bağımsız inceleme 11 F-1). Aynı
   anahtar + farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`.
3. **Çöküş kurtarma (inceleme 03 ile düzeltildi):** işlem 1 sonrası INVENTORY 503 — ya da commit sonucu bilinmeyen başka bir sonuç (500 / 502 / 504, zaman aşımı, ağ hatası,
   iptal (`OperationCanceledException`), beklenmeyen istisna; Codex P2, bağımsız inceleme 13 F-2) → onay Pending, cevap 503. Hareket yazılmamışsa
   yalnız kullanıcının "Yeniden dene"si aynı anahtarla posting yapar → Posted; sweeper bu durumda hiçbir şey yazmaz. INVENTORY'nin
   önceden yazdığı (cevap kaybolan) durumda sweeper (`ReconcilePendingConfirmationCommand`) ya da "Yeniden dene" ledger araması ile
   Posted yazar → yine tek hareket.
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
10. **Cross-tenant / cross-company:** B kiracısı A'nın görevini okuyamaz → 404; aynı kiracıda seçili şirketle başka şirketin
    görevine id ile erişim → **404**; `X-Legal-Entity-Id` token / üyelikle uyuşmazsa → 403 `CROSS_LE_FORBIDDEN`; listede başka
    şirketin satırı yok (F-11).
11. **Concurrency:** iki kullanıcı aynı görevi aynı sürümle düzenler / onaylar → biri 409 `WMS_CONFLICT`; sessiz üzerine yazma yok.
12. **Geçersiz geçiş:** Completed görevi onaylama / iptal → 409 `WMS_INVALID_TRANSITION`; Pending varken iptal → 409
    `WMS_CONFIRMATION_PENDING`.
13. **İkinci bakiye yok / doğrudan defter yazımı yok:** mimari test (Boundary rules 1–2).
14. **Fail-closed üst akış:** LOCATION / MDM şirket / RECALL okuması yok → 503; INVENTORY'nin kendi 503'ü (`PRODUCT_MASTER_UNAVAILABLE`)
    onayı Pending bırakır.
15. **Eşzamanlı iki yeniden deneme** (kullanıcı + sweeper) aynı Pending onayda → tek hareket, `ConfirmedQuantity` bir kez artar (F-3).
16. **INVENTORY 400 / 403 / 404 / 409** → onay kalıcı Refused, sweeper denemez (F-2).
17. **Seri kovası değişti** → 422 `WMS_SERIAL_BUCKET_CHANGED`; seri SKU'da kısmi onay → 422 `WMS_SERIAL_PARTIAL_NOT_SUPPORTED` (F-1).
18. **Cevabı kaybolmuş hareket + koşul değişti** (inceleme 02 F-1): ledger yazdı, 0178 Pending; bu arada lot recall'a girdi → "Yeniden dene"
    önce anahtarı bulur → Posted; ön kontrol koşmaz; ikinci hareket yok.
19. **Aynı anahtar, farklı hareket** (`IDEMPOTENCY_KEY_REUSED`) → onay `IntegrityError`, Superseded / Returned 409, görev kilitli.
20. **Sweeper, işlenmemiş anahtar** → onay Pending kalır; PRODUCT-MASTER / LOCATION'a çağrı yok (inceleme 02 F-5).
21. **Uçuştaki onay + "Yeniden dene"** → 409 `WMS_CONFIRMATION_IN_FLIGHT`; kilit dolunca replay → Posted (inceleme 03 F-1).
22. **`IntegrityError` → "Uzlaştır"** (sorumlu, gerekçe) → anahtarı tutan hareket bu onayınsa Posted (defter değerleriyle, gerekirse
    sapma olayı); yabancıysa çözümsüz Refused, görev açık kalır (inceleme 03 F-2, inceleme 04 F-1).
23. **Posting süresi aşımı** → onay Pending, kilit `InFlightUntilUtc`'ye kadar korunur; sonraki deneme ledger araması ile çözer. Commit
    kilit süresinden de geç görünürse ve bu arada deneme ön kontrolle Refused yazdıysa, sweeper'ın savunma dalı onayı `IntegrityError`
    (`StatusDiverged`) yapar; "Uzlaştır-a" defter gerçeğini yazar (gerekirse `OverPlanned` sapması) (inceleme 04 F-2, inceleme 05 F-1 / F-3).
24. **Yaratma dışı komut tekrarı** (bağımsız inceleme 10 F-1): düzenle, üstlen, toplu atama, yeniden dene, "Geri taşındı", Kalanı kapat,
    İptal, Uzlaştır ve rol oluştur / düzenle / sil / toplu sil — aynı anahtar + aynı gövde iki kez → tek yazım, ikinci cevap 200 replay
    (ilk istek `Version`'ı artırmış olsa da 409 değil; rol yaratmada 409 `WMS_LOCATION_ROLE_EXISTS` değil); farklı gövde → 400
    `IDEMPOTENCY_KEY_REUSED`; iki özdeş istek aynı anda → biri yazar, öbürü 200 replay; tekrar ne yerel olay ne merkezi kayıt bırakır. İki özdeş rol
    yaratma aynı anda (aynı `locationId`) → biri 201, öbürü 200 replay — kaybeden yinelenen kaydı anahtar ya da `LocationId` index'inden
    hangisinden alırsa alsın (409 `WMS_LOCATION_ROLE_EXISTS` değil); başka anahtarla aynı `locationId` → 409 `WMS_LOCATION_ROLE_EXISTS`;
    tekil silmenin anahtarı toplu silmede ayrı anahtardır (bağımsız inceleme 11 F-2 / F-3).

## 14. Authorization Convention

### 14.1 Namespace
Yeni namespace **`warehouse.*`** (karar Q-08, Beste 2026-10-21; PKS-001 §4 bir namespace = bir sahip) — sahibi MOD-0178, manifest
`PUTAWAY-PICKING-PACKING`. Seçim gerekçesi: WAREHOUSE-OUTBOUND taban yolu `/api/warehouse`; repo'da `warehouse.` anahtarı yok
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
| `warehouse.putaway-tasks.confirm` | 3 (bu pakette kayıtlı) | S1 | onayla, yeniden dene, kısa kapat, "Geri taşındı", üstlen — üstlen dışındakiler ayrıca **atanan ya da sorumlu** kuralına bağlı (§8.3; operatör şablonunda var) |
| `warehouse.putaway-tasks.cancel` | 3 (`cancel`) | S1 | iptal |
| `warehouse.putaway-tasks.resolve` | 3 (bu pakette kayıtlı; öneri — PKS-001 §1 dilbilgisi ve §5 Tier-3 kuralından türetildi) | S1 | `IntegrityError` onayın "Uzlaştır"ı; yalnız sorumlu rol şablonunda (inceleme 03 F-2) |
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
- `warehouse.putaway-tasks.resolve` yalnız Depo sorumlusu şablonunda; operatöre verilmez.
- Rol şablonu önerisi (yalnız yeni kiracılar, ROLE1): Depo operatörü = putaway-tasks.read + confirm; Depo sorumlusu = tüm
  putaway-tasks.* + location-roles.*; Yönetim = read'ler.

### 14.4 Manifest
| Alan | Değer |
|---|---|
| ModuleCode | `PUTAWAY-PICKING-PACKING` (sahip kararı Beste 2026-10-26; InventoryService'teki beş manifestin büyük harf emsali — `RecallReadinessManifestProvider.cs:23` `RECALL-READINESS`; merkezi kaydın `SourceModule`'u aynı sabittir, §21; bağımsız inceleme 09 F-1) |
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
- [ ] AC-1 (zorunlu) — `Diten.InventoryService` 0 hata / 0 yeni uyarı ile derlenir; mevcut Inventory testlerinin hepsi yeşil kalır (sayı kabulde ölçülür, kırmızı eklenmez). BL-501 bildirim testi `CentralAuditDeclarationTests.cs` §5'in beşinci seam'iyle güncellenir ve yeşildir: komut sayısı DL-1 sonunda 71, DL-2 sonunda **82**; 0178'in her komutu `IsKnown` (Platform enum adı) ve `SourceModule` = 0178 manifest kodu; `seen` 0178'in `WithTransactionAsync` callback'leri kadar artar ve her biri enlist eder (bağımsız inceleme 09 F-3).
- [ ] AC-2 (zorunlu) — Altın akış: test kurulumu Receiving rolündeki `R-01` bin'ine `AVAILABLE` 100 EA lot L1 koyar → `GET /api/warehouse/putaway-candidates` satırı açık miktar 100 döner → görev 60 açılır (201, numara RCS biçiminde) → aday açık miktarı 40 → 40'lık onay `A-01` → 201, INVENTORY'de `LOCATION_TRANSFER` (sourceModule `MOD-0178`, sourceType `PUTAWAY_TASK`, sourceDocumentId = görev id, sourceLineId `1`), bakiye R-01 60 / A-01 40, görev `InProgress` → 20'lik onay → görev `Completed`; servis yeniden başlatıldıktan sonra görev, onaylar ve bakiye aynı.
- [ ] AC-3 (zorunlu) — Kısmi yerleştirme farklı hedeflere: aynı görev iki onayla iki ayrı bin'e; her onay ayrı hareket.
- [ ] AC-4 (zorunlu) — İdempotent onay: aynı onay iki kez → defterde aynı `PostingKey` ile tek satır, 2. cevap 200 replay (§13.2). **Her yazma komutu (bağımsız inceleme 10 F-1, §8.1):** aynı `Idempotency-Key` + aynı gövde + ilk isteğin `If-Match`'i ile ikinci gönderim → **200 replay** (409 `WMS_CONFLICT` değil; rol yaratmada 409 `WMS_LOCATION_ROLE_EXISTS` değil), görev / rol `Version`'ı değişmez, yeni olay yok; aynı anahtar + farklı gövde → 400 `IDEMPOTENCY_KEY_REUSED`, yazım yok; aynı anahtar başka görevde ayrı istektir. Ayrı testler: görev yarat, düzenle, üstlen, toplu atama (replay'de yazılan satır `Assigned`, yazılmayan `Conflict` / `NotFound`), onay (ilk sonuç Posted ve ayrıca Refused — replay 200, gövdede onay `Refused`), yeniden dene, "Geri taşındı", Kalanı kapat, İptal, Uzlaştır, rol yarat / düzenle / sil / toplu sil. **Eşzamanlı vaka:** iki özdeş onay (ve iki özdeş yeniden deneme) bir bariyerle aynı anda → tek yazım, öbürü 200 replay (CAS kaybı / yinelenen anahtar sonrası anahtar yeniden aranır; 409 yok). Merkezi / yerel sayımlar AC-15 "tekrar = 0 / 0 / 0". **Onayın `seq`'i (bağımsız inceleme 11 F-1):** 201 cevabı `confirmationSeq`, 422 / 503 cevabı `details.seq` taşır; kurulum: ilk onay (seq 1) Refused → aynı görevde yeni onay (seq 2) Posted → ilk isteğin replay'i 200, `confirmationSeq = 1`, `confirmations[1].status = Refused` (son onayın seq'i değil). **Anahtar kapsamı (11 F-2):** A ve B kullanıcıları aynı anahtarla görev (ve ayrı testte rol) yaratır → iki ayrı kayıt, replay yok; aynı kullanıcı aynı anahtarı önce `deleteLocationRole`'de, sonra `bulkDeleteLocationRoles`'te kullanır → toplu silme replay sayılmaz, satırlarını yazar (200, satır `Deleted`). **Eşzamanlı rol yaratma (11 F-3):** iki özdeş rol yaratma (aynı `locationId`) bariyerle aynı anda → biri 201, öbürü 200 replay, 409 `WMS_LOCATION_ROLE_EXISTS` yok; Mongo'nun önce hangi index'i raporladığı geliştiricinin ölçümüdür, `LocationId` dalı ayrıca depo düzeyinde o index'in adıyla yinelenen kayıt hatası veren bir test kurulumuyla ölçülür; farklı anahtarla aynı `locationId` → 409 `WMS_LOCATION_ROLE_EXISTS`. **Bağımsız inceleme 12:** (F-4) işlem 1'den sonraki 409 cevabı da `details.seq` taşır (ör. AC-64 (C1) kurulumunda X'in 409 `WMS_CONFIRMATION_IN_FLIGHT`'i X'in onayının `seq`'ini); (F-3) **aynı hedef, iki işlem:** aynı kullanıcı aynı anahtarla önce `bulkAssignPutawayTasks` (görev T), sonra `updatePutawayTask` (aynı T) gönderir → düzenleme replay ya da 400 sayılmaz, yazılır (200, `Updated` olayı; anahtarlar `RequestOp` ile ayrı); (F-2) **özdeş eşzamanlı seri görev yaratma:** aynı kullanıcı, aynı anahtar, aynı gövde bariyerle aynı anda → biri 201, öbürü 200 replay (422 `WMS_QUANTITY_EXCEEDS_OPEN` yok); seri slot dalı ayrıca depo düzeyinde o index'in adıyla yinelenen kayıt veren kurulumla ölçülür.
- [ ] AC-5 (zorunlu) — Pending kurtarma: INVENTORY 503 enjekte → onay Pending + 503; **commit sonucu bilinmeyen sonuçlar (Codex P2), ayrı testler:** INVENTORY 500, 502, 504, posting çağrısının iptali (`OperationCanceledException`, sahte defter iptal fırlatır; bağımsız inceleme 13 F-2) ve posting çağrısından beklenmeyen istisna → her biri onay Pending + 503 (`details.reason = OutcomeUnknown`, `details.seq`), Refused yazılmaz; **commit öncesi 500** (sahte defter yazmadan 500 döner) → "Yeniden dene" anahtarı `NotCommitted` bulur, aynı anahtarla posting yapar → Posted, tek hareket; **commit sonrası 500** (sahte defter hareketi commit eder, sonra 500 / istisna) → sweeper ya da "Yeniden dene" anahtarı `Committed` bulur → Posted, ikinci hareket yok, ön kontrol koşmaz; tarihler (`transactionDate` / `postingDate`) işlem 1'de yazılır ve her denemede byte-eşit gönderilir (yeniden denemede `IDEMPOTENCY_KEY_REUSED` yok); INVENTORY 400 / 403 / 404 / 409 → kalıcı Refused, hata saklanır, yeniden denenmez; **400 `IDEMPOTENCY_KEY_REUSED` → `IntegrityError`** (Refused değil); **cevap kayboldu + koşul değişti** (ledger yazdı, ardından lot recall'a girdi / kova karantinaya geçti) → "Yeniden dene" önce ledger anahtarını bulur ve onayı **Posted** yazar, ön kontrol koşmaz, ikinci hareket yok (inceleme 02 F-1); **sweeper** yalnız ledger anahtar kontrolü yapar — işlenmiş anahtar → Posted, işlenmemiş → Pending kalır, sweeper koşusunda PRODUCT-MASTER / LOCATION'a 0 çağrı (sahte upstream sayacı) (inceleme 02 F-5); "Yeniden dene" aynı anahtarla tek hareket üretir (§13.3); deneme sınırı yok (ör. 5 ardışık 503 sonrası 6. deneme yine aynı anahtarla gider), `Attempts` ve son deneme zamanı API'de ve ekranda; INVENTORY 422 dönerse onay `Refused` + "Stok reddetti" rozeti, görev miktarı değişmez.
- [ ] AC-6 (zorunlu) — İkinci bakiye yok: mimari test `wms_*` koleksiyonlarında stok toplam alanı ve 0178 kodunda `IInventoryLedgerRepository` / defter koleksiyon adı referansı bulmaz; tek stok kapısı `WarehouseMovementPoster`.
- [ ] AC-7 (zorunlu) — Tutulan stok: QUARANTINE / QUALITY_INSPECTION / BLOCKED kovadan görev 422 `WMS_STOCK_NOT_PUTAWAY_ELIGIBLE`; adaylarda "yerleştirilemez" işaretli.
- [ ] AC-8 (zorunlu) — Recall: geri çağırmadaki lot için görev ve onay 422 `LOT_UNDER_RECALL` (`details.movementType = LOCATION_TRANSFER`); okuma hatası 503, yazma yok.
- [ ] AC-9 (zorunlu) — Defter retleri aynen iletilir: onayda `INSUFFICIENT_STOCK` / `LOT_BLOCKED` / `LOT_EXPIRED` (ve ölçülürse `LOT_RETEST_DUE`) → 422 aynı kod + `details`, onay `Refused`, görev miktarı değişmez.
- [ ] AC-10 (zorunlu) — Hedef kuralları §12 (bin / aktif / stoklanabilir / aynı depo / ≠ kaynak / Receiving-Staging değil); ihlal → 422, yazma yok.
- [ ] AC-11 (zorunlu) — Durum makinesi §8.3: izinsiz geçiş 409 `WMS_INVALID_TRANSITION`; Pending varken iptal / kısa kapatma 409 `WMS_CONFIRMATION_PENDING`; Posted onaylı görevin iptali 409 (yalnız "Kalanı kapat"); iptal Posted hareketleri geri almaz.
- [ ] AC-12 (zorunlu) — Concurrency: bayat If-Match → 409 `WMS_CONFLICT`; eksik → 400.
- [ ] AC-13 (zorunlu) — Kiracı + şirket (F-11): başka kiracının kaydı 404; seçili şirketle başka şirketin kaydına id ile erişim 404 (her uçta); `X-Legal-Entity-Id` token / üyelikle uyuşmazsa 403 `CROSS_LE_FORBIDDEN`; liste yalnız seçili şirket — iki durum ayrı testlerde ölçülür.
- [ ] AC-14 (zorunlu) — RBAC: her uçta `[HasPermission]` (guard testi); token yok 401, anahtar yok 403.
- [ ] AC-15 (zorunlu) — Merkezi denetim ve yerel GxP izi (E-2, BL-501; sahip kararı Beste 2026-10-26; bağımsız inceleme 08 F-1, 09 F-1 / F-2, 10 F-1 / F-2 / F-3). **(a) Merkezi kayıt** platformsuz testte `central_audit_outbox`'tan, komutun `CorrelationId`'siyle ölçülür; satırın `RequestType`'ı komut sınıfının adı, `Category` / `Operation` / `EntityType` / `SourceModule` / `EntityId` §21'deki değerlerdir (gövde alanı yoktur, aranmaz). **Başarılı ve yazan komut:** tam **1** satır, durumu `Pending` (iletimden sonra `Delivered`), `ConfirmedByCommand = true`. **Başarısız komut:** `Pending` + `Delivered` = **0**; komutun bir işlemi commit olduysa tam 1 satır `Abandoned`, olmadıysa satır yok. **Yazmadan başarılı komut** (enlist yok, `CentralAuditRecorder.cs:160-168`): satır yok. **Çökme vakası:** onayın işlem 1'i commit olur ve komut `ConfirmAsync` / `AbandonAsync`'e ulaşmadan kesilir (satırı `AwaitingCommand`'da bırakan kesme; yöntem geliştiricinin) → sahte saat `OrphanAfter`'ı (10 dk) geçer → dağıtıcının `RunOnceAsync`'inden sonra tam 1 satır `Pending`, `ConfirmedByCommand = false` (Platform'a `Unconfirmed`); onay sonra sweeper ile Posted olursa ikinci satır `ReconcilePendingConfirmationCommand` (`ConfirmedByCommand = true`). Senaryo başına **iletilen merkezi (`Pending` / `Delivered`) / `Abandoned` / (b) yerel zaman çizelgesi satırı** — (b) sayımı görevi tamamlamayan ve Refused kapatmayan senaryoda verilir; görevi `Completed` yapan Posted yazımı +1 `Completed`, kapatılan her Refused +1 `ConfirmationSuperseded` (§4.3): ilk onay Posted = **1** / 0 / 2 (Recorded + Posted); ilk onay Refused (422) = **0** / 1 / 2 (Recorded + Refused); ilk onay Pending (503) = **0** / 1 / 1 (Recorded); ilk onay bütünlük hatası (409) = **0** / 1 / 2 (Recorded + IntegrityError); (A) tükenmesi 503 `WMS_CONFIRMATION_CONFLICT_RETRY` — **sonuç yazımında** (işlem 2 / retry (i) / (ii)) = **0** / 1 / ilk yazımın olayı (Recorded ya da Retried), **kilit alımında** (retry (0)'ın kilit yazımı üç kez kaybeder; komut hiçbir şey commit etmez) = **0** / 0 / 0 (bağımsız inceleme 10 F-3); **(B) dalı** — kullanıcı komutunun sonuç yazımı 0 kayıt, onay bu denemenin `TransactionId`'siyle zaten Posted (ör. AC-54'ün ikinci yeniden denemesi; komut 200 / 201 döner) = **1** / 0 / ilk yazımın olayı (Recorded ya da Retried) (bağımsız inceleme 10 F-3; AC-54 ile aynı); (C1) / (C2) / (E) 409 **sonuç yazımında** = **0** / 1 / ilk yazımın olayı, **kilit alımında** ((C1) = kilit reddi; (E) = 409 `WMS_CONFIRMATION_NOT_RETRYABLE`) = **0** / 0 / 0; (D) onay zaten `IntegrityError` (yazım yok, 409) = **0** / 1 / ilk yazımın olayı (bağımsız inceleme 10 F-2); (D) tükenmesi (Refused'a `IntegrityError` yazımı üç kez CAS kaybeder; yazım yok, onay Refused kalır, 409 `WMS_CONFLICT`) = **0** / 1 / ilk yazımın olayı (bağımsız inceleme 11 F-3); **her yazma komutunun tekrarı** (AC-4; aynı anahtar + aynı parmak izi, 200 replay; onayda ilk sonuç Posted, Refused, Pending ya da IntegrityError olsa da) ve aynı anahtar + farklı parmak izi (400) = **0** / 0 / 0 (bağımsız inceleme 10 F-1); yeniden deneme Posted (yeni posting ya da replay) = **1** / 0 / 2 (Retried + Posted); yeniden deneme Refused (ön kontrol ya da defter 4xx) = **0** / 1 / 2; yeniden deneme IntegrityError (KeyReused / HolderReversed / (D)) = **0** / 1 / 2; yeniden deneme 503 = **0** / 1 / 1 (Retried); kilit reddi 409 = **0** / 0 / 0; sweeper `Committed` = **1** / 0 / 1 (Reconciled); sweeper KeyReused / HolderReversed / savunma dalı = **0** / 1 / 1 (IntegrityError); sweeper `NotCommitted`, (B) dalı ve 0-kayıt bırakması = **0** / 0 / 0 (`Attempts` değişmez); "Uzlaştır" = **1** / 0 / 1 (IntegrityResolved; Deviation varsa +1); "Geri taşındı" = **1** / 0 / 1 (Returned); İptal = **1** / 0 / 1 (Cancelled); Kalanı kapat = **1** / 0 / 1 (ClosedShort; `Completed` yazılmaz); kapatıcı komutun ön araması geç commit buldu (409) = **0** / 1 / 1 (hedef onayda IntegrityError); If-Match `WMS_CONFLICT`, `WMS_CONFIRMATION_PENDING` ve diğer ön kontrol 409'ları = **0** / 0 / 0; görev oluştur / düzenle / üstlen = **1** / 0 / 1 (Created / Updated / Claimed; hedef öneriden geldiyse + `SuggestionApplied`); görev oluşturma tekrarı = **0** / 0 / 0; toplu atama, n görev yazıldı = **1** (`EntityId` boş) / 0 / n (Assigned), hiçbiri yazılmadı = **0** / 0 / 0; rol oluştur / düzenle / sil = **1** / 0 / 1 (`wms_location_role_events`); toplu rol silme, n kayıt = **1** / 0 / n, hiçbiri = **0** / 0 / 0. Hareketin defterdeki enlist'i ayrı merkezi kayıt **doğurmaz** (no-op; `CentralAuditRecorder.cs:11-12`, `InventoryLedgerRepository.cs:181`). **Veri yalnız yerelde:** kapatılan `seq` listesi, TransactionNumber, `holder` ve önceki / sonraki değerler zaman çizelgesinin `Data`'sından ölçülür. **Doğrulama / yetki reddi** (400 / 401 / 403 / 404 ve onay kaydı açılmadan verilen ön kontrol 422'leri) ne merkezi ne yerel iz bırakır (satır yok). AuditTrail mimari testinde Inventory yeni-denetimsiz **0**.
- [ ] AC-16 (zorunlu) — Kayıt kodu RCS-001: görev numarası `IWarehouseRecordCodeAllocator` portundan, RCS biçiminde geçici sayaçla (Q-07 B; ortak servis gelince FU-0178-RCS-01); eşzamanlı iki görev farklı numara; numara API'de değiştirilemez; ASCII; numara **kiracı genelinde tekil** `(TenantId, Number)`; sayaç `999999`'dan sonra 7 haneye uzar (`GMG-PUT-1000000`), sıfıra dönmez (F-12).
- [ ] AC-17 (zorunlu) — Lokasyon rolleri: oluştur / düzenle / sil (soft delete, `IsDeleted` + `DeletedAt`) / toplu sil; düğüm başına tek kayıt (roller + kabul edilen saklama koşulları); bir düğüm birden çok role sahip olabilir, Receiving + Staging birlikte 409; açık görevi olan düğümden Receiving kaldırma / silme 409 `WMS_LOCATION_HAS_OPEN_TASKS`; **rol kalıtımı** (F-13): Receiving olan Zone'un alt bin'i kaynak olarak uygun, hedef olarak yasak; atası Receiving olan düğüme Staging → 409 `WMS_LOCATION_ROLE_INHERITED_CONFLICT`.
- [ ] AC-18 (zorunlu) — WAREHOUSE-OUTBOUND 1.0.0 değişmez: dosya byte-eşit; donmuş GET uçları S1'de boş liste / 404 `UNKNOWN_OUTBOUND` döner, gövde MVP-6 `FrozenReadSchema.Warehouse` / `WarehousePage` kurallarını geçer (null üye yok).
- [ ] AC-19 (zorunlu) — Sözleşme uyumu (inceleme 02 F-4): DL-2, her S1 cevabını **taslaktan türetilmiş şemaya** (`docs/analysis/contracts/drafts/wms-bundle-v1-proposal.md`'den test projesine alınan şema; dev dilimi taslak dosyayı değiştirmez) karşı otomatik doğrular. Dondurulmuş dosyaya karşı yeniden koşu CT dondurma adımının (§19.7) kabul ölçütüdür (AC-60) — iki adım birbirini beklemez.
- [ ] AC-20 (zorunlu) — Self-registration: manifest `PUTAWAY-PICKING-PACKING` 2 sayfa + §11.3 eylemleri + `ApiPermissions`; rota `/Platform/` değil (Scope Tenant); namespace testi `warehouse.*` dışı anahtar bildirmez.
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
- [ ] AC-33 (isteğe bağlı) — Outbound satırında `skuLevel: Item` davranışı **W-6 kararına bağlı** (Beste'nin açık S2 kararı, §19.5); W-6 "yok" derse 422 ile reddedilir (MVP-6 enum'u bugün Item tanımaz), "var" derse WAREHOUSE-OUTBOUND eklemeli adım + MVP-6 uyumu (F-16).
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
- [ ] AC-46 (zorunlu) — Durum ile entegrasyon bayrağı ayrı: durum onaylanan miktardan türer; "Onay bekliyor" / "Stok reddetti" / "Bütünlük hatası" rozetleri ayrı; Refused ve IntegrityError onay durumu değiştirmez. **Uzun Pending (bağımsız inceleme 13 F-3):** sahte defter her denemede 500 döner; onay Pending kalır, sahte saat `LongPendingMinutes`'tan önce bayrak `PostingPendingLong` değil `PostingPending`, süre aşılınca `PostingPendingLong` ve ekranda "Uzun süredir bekliyor" rozeti (7 dil); bu sırada yeni onay / iptal / kısa kapatma 409 `WMS_CONFIRMATION_PENDING`; hata giderilince "Yeniden dene" → Posted, rozet kalkar (sabotaj (by)). **Öncelik (bağımsız inceleme 14 F-1):** aynı görevde bir onay Pending (uzun ya da değil) iken savunma dalı başka bir onayı `IntegrityError` yapar → bayrak `PostingIntegrityError`, rozet "Bütünlük hatası"; ayrı testlerde Pending + Refused → `PostingPending`, uzun Pending + Refused → `PostingPendingLong` (sabotaj (bz)). **Açılış doğrulaması (bağımsız inceleme 14 F-3):** `LongPendingMinutes = 0` ve `LongPendingMinutes = 1` (60 sn ≤ `InFlightLeaseSeconds` 120) ile host başlamaz (açılış doğrulama hatası); `LongPendingMinutes = 3` (180 sn > 120) ile başlar (sabotaj (ca)).
- [ ] AC-47 (zorunlu) — Onay yetkisi ve üstlenme: atanmamış görevi operatör "Üstlen" ile kendine atar (yalnız `warehouse.putaway-tasks.confirm`); atanmış görevi başkası onaylayamaz (403 `WMS_NOT_ASSIGNEE`), sorumlu (`.update`) onaylayabilir; atanmış görevi üstlenme 409 `WMS_TASK_ALREADY_ASSIGNED`.
- [ ] AC-48 (zorunlu) — E-imza yok: putaway onayı imza istemez; MOD-0022'ye imzalanabilir eylem kaydı yok (manifest / kayıt testi). Başarılı onayın **kim / hangi komut / hangi görev / ne zaman** bilgisi merkezi kayıtta ölçülür (`RequestType` `ConfirmPutawayTaskCommand`, `Operation` `Execute`, `EntityId` = görev, aktör, zaman); **ne onaylandı** (onay `seq`, miktar, from / to, TransactionNumber) ve her sonucun izi yerel görev zaman çizelgesinde ölçülür (§4.3, §8.8; bağımsız inceleme 09 F-1).
- [ ] AC-49 (zorunlu) — Açık görevin lotu sonradan recall'a ya da stoğu karantinaya girerse görev askıya alınmaz; onay anında 422 (`LOT_UNDER_RECALL` / defter kodu), görev açık kalır.
- [ ] AC-50 (zorunlu) — RTL: `ar` dilinde iki ekran sağdan sola, diğer altı dil soldan sağa (Razor / CSS testi).

**S1 — bağımsız inceleme 01 düzeltmeleri (F-1…F-4 + CT ek bulgusu):**
- [ ] AC-51 (zorunlu) — Seri kontrollü SKU tam miktar (F-1): kovada 5 seri → görev miktarı 5 (değiştirilemez); onay kullanıcıdan seri almaz, sistem 5 seriyi okuyup harekete koyar; defterde `SERIAL_COUNT_MISMATCH` yok; serilerin yeni lokasyonu TRACE'te hedef bin; görev **onayla `Completed` olunca `OpenSerialSlot` kaldırılır** — aynı lotun aynı Receiving lokasyonuna **ikinci girişinde** (kısmi teslimat) aynı kova için **yeni görev açılır** (422 `WMS_QUANTITY_EXCEEDS_OPEN` yok; bağımsız inceleme 08 F-4).
- [ ] AC-52 (zorunlu) — Seri SKU'da kısmi onay (3 / 5), kısa kapatma ve miktar düzenleme → 422 `WMS_SERIAL_PARTIAL_NOT_SUPPORTED`, 7 dilde mesaj; aynı kovaya ikinci açık görev → 422 `WMS_QUANTITY_EXCEEDS_OPEN` — **sıralı vaka** ve **eşzamanlı vaka** ayrı test: iki görev yaratma (**farklı** `Idempotency-Key`; aynı anahtar AC-4'te replay'dir — bağımsız inceleme 12 F-2) bir bariyerle aynı anda başlar, ikisi de açık miktar kontrolünü geçer, yalnız biri yazılır, öbürü kısmi tekil index'ten yinelenen anahtar alır → 422 (sabotaj (ae) eşzamanlı vakayı hedefler; bağımsız inceleme 06 F-6 d).
- [ ] AC-53 (zorunlu) — Seri kovası değişti: görev açıldıktan sonra kovadan 1 seri başka harekete gider → onay (ve Pending'in yeniden denemesi) 422 `WMS_SERIAL_BUCKET_CHANGED` (`expected 5`, `found 4`), hareket yok.
- [ ] AC-54 (zorunlu) — Eşzamanlı iki yeniden deneme (inceleme 04 F-3 ile kurulabilir hâle getirildi): **fikstür 2q ≤ planlanan** (60'lık görevde 20'lik onay; böylece `Confirmed + q ≤ Planned` koşulu ikinci artışı tek başına engelleyemez ve sabotaj (w) ayrıştırıcı olur — bağımsız inceleme 05 F-4); ledger hareketi yazmış, cevap kaybolmuş, onay Pending; **kilidin süresi sahte saatle (`TimeProvider`) doldurulur** — ayarlar gerçek kalır, açılış doğrulaması bozulmaz (inceleme 05 F-2); iki kullanıcı "Yeniden dene"si aynı anda → ikisi de sırayla kilit alır, ikisi de ledger'da `Committed` bulur; yalnız biri yazar (`AttemptId` + `Status == Pending` filtresi), öbürü 0 kayıt etkiler, yeniden okur, aynı `TransactionId` ile Posted görür ve değişiklik yazmaz; tek hareket, `ConfirmedQuantity` bir kez artar, **tek `ConfirmationPosted` yerel olayı** (iki `ConfirmationRetried` kilit alımı olayı); merkezi denetim: iki komut da 200 döndüğü için her biri 1 `RetryPutawayConfirmationCommand` kaydı (`Operation` `Execute`, komut başına sabit; ikinci denemenin kilit alımı yazımı vardır) — bağımsız inceleme 08 F-1. Ayrıca sweeper + kullanıcı: sweeper okuduktan sonra kullanıcı kilit alırsa sweeper'ın yazımı 0 kayıt etkiler, sweeper hiçbir şey yazmaz.
- [ ] AC-55 (zorunlu) — Refused sonrası akış (F-4): aynı görevde yeni onay (başka hedef) → Posted, eski onay `Resolution = SupersededByNewConfirmation` + zaman çizelgesinde `ConfirmationSuperseded` yerel olayı; merkezi denetimde ayrı kayıt yok — Posted komutunun tek merkezi kaydı (`RequestType` = komutun adı, `EntityId` = görev) gövde taşımaz; kapatılan `seq`'ler zaman çizelgesindeki `ConfirmationSuperseded` satırlarında ve `ConfirmationPosted.Data`'da ölçülür (§4.3, §8.8; bağımsız inceleme 09 F-1); "Geri taşındı" (gerekçe) → hareket yok, `Resolution = ReturnedToSource`, `ConfirmationReturned` olayı, "Stok reddetti" rozeti kalkar, görev kalan miktarla açık; Posted / Pending onayda "Geri taşındı" → 409 `WMS_CONFIRMATION_NOT_RESOLVABLE`. **"Geri taşındı" → İptal** (Posted onay yok) **başarılı** (200, görev Cancelled) ve **"Geri taşındı" → Kalanı kapat** (önceden bir Posted onay var) **başarılı** (200, görev Completed) — İptal / Kalanı kapat'ın filtresinde `Resolution` koşulu yoktur (bağımsız inceleme 07 F-2).
- [ ] AC-56 (zorunlu) — Yeniden deneme ön kontrolleri (F-4): Pending onayın lotu recall'a girer ya da kova karantinaya geçer ya da hedefin saklama koşulu etiketi değişir → yeniden deneme INVENTORY'yi çağırmadan onayı Refused yapar (ön kontrol kodu ile); `LOCATION_TRANSFER` yazılmaz.
- [ ] AC-57 (zorunlu) — Açık miktar yarışı (CT; tek yol inceleme 02 F-7): 100'lük kovada aynı anda iki 60'lık görev açılabilir (iyimser); ilk onay 60 Posted; ikincinin 60'lık onayı ön kontrolden geçer (durum `AVAILABLE`), onay kaydı açılır, **ledger 422 `INSUFFICIENT_STOCK`** → onay **Refused** (merkezi 0, yerel 2); bakiye eksiye düşmez; ikinci görev 40'lık yeni onayla Posted olur. **Ek (seri olmayan SKU, eşzamansız):** 100'lük kovada 60'lık görev açıkken ikinci 60'lık görev → 422 `WMS_QUANTITY_EXCEEDS_OPEN` (açık miktar kontrolü; §13.5).

**S1 — bağımsız inceleme 02 düzeltmeleri:**
- [ ] AC-58 (zorunlu) — AC-17'nin görev isteyen parçası (DL-2, inceleme 02 F-4): açık görevi olan düğümden Receiving kaldırma / silme 409 `WMS_LOCATION_HAS_OPEN_TASKS`; Receiving Zone'un alt bin'i kaynak olarak uygun, hedef olarak yasak.
- [ ] AC-59 (zorunlu) — Saklama koşulu etiketleri görevsiz ölçülür (DL-1, inceleme 02 F-4): etiket oluştur / düzenle / sil, en çok bir sıcaklık kodu (400 `WMS_STORAGE_CONDITION_INVALID`), etkin küme okuması (`effectiveStorageConditions`, alt ağaç kalıtımı, "üst düğümden" kaynağı) doğru düğümü döner.
- [ ] AC-60 (zorunlu) — CT dondurma adımının (§19.7) kabul ölçütü (inceleme 02 F-4): `docs/analysis/contracts/wms-bundle.openapi.yaml` dondurulduktan sonra DL-2'nin sözleşme testleri **dondurulmuş dosyaya** karşı yeniden koşar ve yeşildir; outbound kısmı WAREHOUSE-OUTBOUND 1.0.0 ile eşit (uyum testi).
- [ ] AC-61 (zorunlu) — Gsku seri bayrağı (inceleme 02 F-3): Gsku stoğunda görev açılışı ürün bayrağını okur; ürün `serialControlled: false` → görev açılır, seri okunmaz; `true` ya da bilinmiyor → 422 `SKU_LEVEL_NOT_STOCKABLE`; Lsku'da `getSku` bayrağı kullanılır.
- [ ] AC-62 (zorunlu) — Seri kovası ayrımı (inceleme 02 F-2): aynı lokasyonda aynı lot + SKU'nun 3 serisi QUARANTINE tutmasında, 5'i AVAILABLE → seri görevi açılmaz, 422 `WMS_SERIAL_BUCKET_MIXED_STATUS` (7 dilde); tutma kararı sonrası (hepsi AVAILABLE) görev 8 ile açılır ve onayda 8 seri taşınır; açık tutma / bloke serileri asla harekete girmez.
- [ ] AC-63 (zorunlu) — Bütünlük hatası (inceleme 02 F-1): ledger aynı `PostingKey` ile farklı hareket işlemişse onay `IntegrityError`, cevap 409 `WMS_POSTING_INTEGRITY_ERROR`; "yeni onay ile kapatma" / "Geri taşındı" 409 `WMS_CONFIRMATION_NOT_RESOLVABLE`; görevde yeni onay / iptal / kısa kapatma 409; istisna listesinde görünür; tek çıkış "Uzlaştır" (AC-65).
- [ ] AC-64 (zorunlu) — Uçuş kilidi (inceleme 03 F-1): asıl onay INVENTORY'de uçuştayken (sahte gecikme) "Yeniden dene" → 409 `WMS_CONFIRMATION_IN_FLIGHT`, hiçbir şey yazılmaz; sweeper kaydı atlar; kilit süresi **sahte saatle** (`TimeProvider`) doldurulunca "Yeniden dene" → ledger araması → replay → Posted (tek hareket; inceleme 05 F-2). İşlem 2'de etkilenen 0 ve yazılı durum farklı (kilit süresi dolmuş, başka deneme Refused yazmış, bu deneme Posted almış) → onay `IntegrityError` + olay `ConfirmationIntegrityError`; sessiz dönüş yok. **Kardeş vaka (bağımsız inceleme 07 F-1):** N bir yeniden denemede Refused; N+1 onayı uçuştayken savunma dalı N'yi `IntegrityError` yapar (görev `Version++`); N+1'in işlem 2'si CAS'ı kaybeder, yeniden okur, N+1 hâlâ Pending ve `AttemptId` kendisinin → yazımı yeni `Version` ile yineler → **N+1 Posted, N+1'de IntegrityError yok**; N `IntegrityError` olduğu için Superseded hedefinde atlanır. **(C) vakası, kullanıcı yolu (bağımsız inceleme 08 F-3):** X'in kilidi sahte saatle dolar, Y "Yeniden dene" ile kilidi alır; X'in işlem 2'si 0 kayıt etkiler → 409 `WMS_CONFIRMATION_IN_FLIGHT`, Y'nin Pending onayı değişmez, X yeni yerel olay bırakmaz, iletilen merkezi satırı 0 (X'in işlem 1 satırı `Abandoned`). **(C2) vakası (bağımsız inceleme 09 F-5):** X duraklar, Y "Yeniden dene" ile kilidi alır ve kesilir, Y'nin kilidi de sahte saatle dolar; X'in işlem 2'si 0 kayıt etkiler → yazım yok, 409 `WMS_CONFLICT` + güncel görev, onay Pending kalır; ardından sweeper (ledger `Committed`) onayı Posted yazar — tek hareket, IntegrityError yok. **(E) vakası:** retry'ın ön kontrolü Refused bulurken sweeper aynı onayı Posted yazmış → retry yazmaz, 409 `WMS_CONFLICT`, onay Posted kalır. **(A) + aşım:** yeniden okumada koşullu artış tutmazsa onay `OverPlanned` `IntegrityError` olur (yineleme döngüsüne girmez). **(D) vakaları (bağımsız inceleme 10 F-2):** (i) X'in kilidi sahte saatle dolar, Y "Yeniden dene" ile ön kontrolde onayı Refused yazar (`RefusedOnRetry = true`), X'in işlem 2'si defterden Posted almıştır → 0 kayıt → yeniden okuma Refused → onay `IntegrityError` (`StatusDiverged`, `PriorResolution = Resolution`), 409 `WMS_POSTING_INTEGRITY_ERROR`; (ii) aynı kurulumda X'in yeniden okumasından önce savunma dalı onayı `IntegrityError` yapmıştır → X **yazmaz**, onayın alanları (`PriorResolution`, `ErrorDetails` dahil) ve `Version` değişmez (ezilmez), yeni `ConfirmationIntegrityError` olayı yok, cevap 409 `WMS_POSTING_INTEGRITY_ERROR`; (iii) **(D) tükenme vakası (bağımsız inceleme 11 F-3):** (i) kurulumunda X'in (D) yazımı üç denemede de CAS kaybeder (test kancası her yeniden okumadan sonra görevde Refused onayı değiştirmeyen bir yazımla, ör. not düzenlemesiyle, `Version`'ı artırır; yöntem geliştiricinin) → yazım yok, onay Refused kalır, 409 `WMS_CONFLICT` + güncel görev, X yeni yerel olay bırakmaz; savunma dalı sonraki turda onayı `IntegrityError` yapar (satır 18).
- [ ] AC-65 (zorunlu) — "Uzlaştır" (inceleme 03 F-2, inceleme 04 F-1): yalnız `warehouse.putaway-tasks.resolve` sahibi (operatör 403); gerekçe < 10 → 400; yalnız `IntegrityError` onayda (diğerleri 409 `WMS_CONFIRMATION_NOT_RESOLVABLE`). **Kendi hareketi:** anahtarı tutan hareket `MOD-0178` / bu görev / bu `seq` (fikstür: aynı anahtarla tarihi farklı bir 0178 hareketi) → onay Posted, onaya defterin `TransactionId`, miktarı ve hedefi yazılır, fark varsa `ConfirmationDeviation` olayı, görev ilerler. **Yabancı hareket:** anahtarı başka bir `sourceModule` / görev tutuyor → onay çözümsüz Refused, yabancı hareketin kimliği olay verisinde, **görev açık kalır** (durumu değişmez); ardından "yeni onay" ya da "Geri taşındı" çalışır; görevi kapatmak ayrı İptal / Kalanı kapat ile. Görevde Pending onay varken "Uzlaştır" → 409 `WMS_CONFIRMATION_PENDING`. Seri SKU'da kendi hareketi: onaya defterin serileri yazılır, fark `ConfirmationDeviation`'da. Denetim satırı 1; düğme ve metin 7 dilde.
- [ ] AC-66 (zorunlu) — Sweeper = uzlaştırıcı (inceleme 02 F-5 kalanı): `ReconcilePendingConfirmationCommand` her kayıt için `SetScope(tenant, LE, ConfirmedBy)` ile koşar; PRODUCT-MASTER / LOCATION'a 0 çağrı; `Committed` → Posted (merkezi 1, yerel 1); **`KeyReusedDifferentPayload` → `IntegrityError` + yerel olay `ConfirmationIntegrityError` (merkezi 0)** (inceleme 05 F-5); **savunma dalı:** son 30 dk'da bir yeniden denemede Refused olmuş onay — **ön kontrol reddi ve defter 4xx reddi ayrı vakalar** (`RefusalSource = PreCheck` / `Ledger4xx`, `RefusedOnRetry = true`) — için ledger `Committed` → `IntegrityError` (`StatusDiverged`) (merkezi 0, yerel 1); ilk onayın defter 4xx'i (`RefusedOnRetry = false`) ve Uzlaştır-b dönüşü (`RefusalSource = Resolve`) taranmaz — **ölçüt (bağımsız inceleme 07 F-3):** sahte `IPostingKeyLookup` çağrı sayacı bu onaylar için 0 (inceleme 05 F-3, bağımsız inceleme 06 F-2); **seçim (bağımsız inceleme 07 F-4):** 200'den fazla `NotCommitted` Pending varken 201. sıradaki `Committed` kayıt bir sonraki turda uzlaştırılır (imleç); normal dalda 200'den fazla kayıt varken savunma dalının adayı aynı turda taranır **ve ters yönde** 200'den fazla savunma adayı varken normal dalın `Committed` kaydı aynı turda uzlaştırılır (ayrı bütçe; hangi dal önce koşarsa koşsun — bağımsız inceleme 08 F-3); **`HolderReversed`:** cevabı kaybolmuş Pending onayın holder'ı ters kaydedilmişse → `IntegrityError` (`HolderReversed`) (merkezi 0, yerel 1); `NotCommitted` → `Attempts` aynı, yerel olay yok, merkezi 0 (iç komut gönderilmez); kilitli kayıt atlanır; **kayıt başına yalıtım:** tur içinde bir kaydın hatası (enjekte istisna) diğer kayıtların işlenmesini durdurmaz, tur en çok 200 kayıt alır (inceleme 04 F-3).
- [ ] AC-67 (zorunlu) — Posting süresi, aşım ve geç commit (inceleme 04 F-2; gerçek akışa göre yeniden yazıldı, inceleme 05 F-1 / F-3): (1) `PostingTimeoutSeconds` aşılırsa (sahte yavaş ledger, sahte saat) onay Pending kalır, cevap 503, **kilit `InFlightUntilUtc`'ye kadar korunur** (bu sürede "Yeniden dene" 409 `WMS_CONFIRMATION_IN_FLIGHT`); (2) `InFlightLeaseSeconds < PostingTimeoutSeconds + 30` gerçek ayarıyla host başlamaz; (3) **geç commit yolu (bağımsız inceleme 05 F-1 ile kendi senaryosunda geçebilir):** kova ≥ 120 (`AVAILABLE`), 60'lık görev; N onayı (60) posting süresini aşar (sahte gecikmeli commit: hareket sonradan görünür); kilit sahte saatle dolar; lot geçici olarak karantinaya alınır → "Yeniden dene" ledger'da `NotCommitted` görür, ön kontrolde N'yi Refused yazar; lot **serbest bırakılır** (MOD-0175 kararı, `AVAILABLE`) — **fikstür kurulumu (bağımsız inceleme 06 F-6):** karantinaya alma ve serbest bırakma test kurulumunda MOD-0175'in kendi süreç içi hareket kapısı `QuarantineMovementPoster` üzerinden iki `STATUS_TRANSFER` (AVAILABLE → QUARANTINE, sonra QUARANTINE → AVAILABLE) ile yapılır; karar iş akışı (onay / imza) bu testin konusu değildir, geç commit sahte gecikmeli ledger fikstürüyle kurulur; P onayı (60) başlar — kapatacağı N'nin anahtarı hâlâ `NotCommitted` → P Posted, N `Superseded`, görev `Completed`; ardından N'nin hareketi defterde görünür → savunma dalı (**`Resolution`'dan ve terminal görevden bağımsız**) N'yi `IntegrityError` (`StatusDiverged`, `PriorResolution = SupersededByNewConfirmation`) yapar → görevde Pending onay yokken "Uzlaştır" → kendi hareketi → N Posted, `ConfirmedQuantity` = 120 > 60, görev **`Completed` kalır** (yeniden açılmaz), `ConfirmationDeviation` (`OverPlanned`); (4) görevde Pending onay varken "Uzlaştır" → 409 `WMS_CONFIRMATION_PENDING`; (5) işlem 2'nin `Confirmed + q ≤ Planned` savunma koşulu **`PutawayTaskRules` birim testiyle** ölçülür (gerçek akışta tetiklenmez; birim testi koşul tablosunu doğrudan çağırır); (6) **yazım yolu (bağımsız inceleme 08 F-2):** test kancası işlem 2'den önce görevin `ConfirmedQuantity`'sini artırır → ilk yazım ve (A) yeniden okuması aşımı görür → onay `IntegrityError` (`OverPlanned`), yineleme döngüsü ve 503 yok.
- [ ] AC-68 (zorunlu) — Geç commit savunması ve terminal görev (bağımsız inceleme 05 F-1): (i) Refused N'nin hareketi defterde görünür hâldeyken yeni onay, "Geri taşındı", İptal ve Kalanı kapat (her biri ayrı test) → komut 409 `WMS_POSTING_INTEGRITY_ERROR`, N `IntegrityError` (`StatusDiverged`), komutun kendi yazımı yok; (ii) commit "Geri taşındı" / İptal **sonrasında** görünürse savunma dalı N'yi yakalar (`PriorResolution = ReturnedToSource` ya da görev Cancelled); (iii) terminal görevde "Uzlaştır": (a) → onay Posted, görev durumu değişmez (Cancelled görevde `ConfirmationDeviation` `PostedAfterCancel`), `OpenSerialSlot` yeniden alınmaz; (b) → onay çözümsüz Refused, görev terminal kalır, yalnız "Geri taşındı" açıktır.
- [ ] AC-69 (zorunlu) — Ters kaydedilmiş kendi hareketi (bağımsız inceleme 05 F-2): cevabı kaybolmuş Pending onayın hareketi MOD-0173 sahibince `REVERSAL` ile ters kaydedilir → "Yeniden dene" ve sweeper onayı Posted **yazmaz**, `IntegrityError` (`HolderReversed`) yapar; ardından "Uzlaştır" → (b) yolu: onay çözümsüz Refused, görev açık kalır, `ConfirmedQuantity` artmaz, seri SKU'da onaya geri alınmış seriler yazılmaz; port `holder.reversedByTransactionId`'yi döndürür.
- [ ] AC-70 (zorunlu) — Sürüm koşulu yarışı (bağımsız inceleme 06 F-3): Refused N üzerinde "Geri taşındı" (If-Match v) ile savunma dalı bir bariyerle aynı anda çalışır → yalnız biri yazar; son durum ya `Resolution = ReturnedToSource` (ardından savunma dalı bir sonraki turda N'yi `IntegrityError` + `PriorResolution = ReturnedToSource` yapar) ya da `IntegrityError` (komut yeniden okur, 409 `WMS_POSTING_INTEGRITY_ERROR`); **hiçbir anda** aynı onayda `IntegrityError` ile `Resolution` birlikte bulunmaz; aynı yarış İptal ve Kalanı kapat için ayrı testlerde; her sistem yazımı `Version`'ı artırır (eski If-Match'li kullanıcı komutu 409 `WMS_CONFLICT`). **Bariyerli sıra vakası (bağımsız inceleme 07 F-3):** savunma dalı N'yi okur (Refused, `Resolution = null`, V = v) → bariyer → "Geri taşındı" yazar ve **200** döner (V = v+1) → savunma `Version == v` ile yazar → 0 kayıt → bir sonraki turda yeniden değerlendirir → son durum `IntegrityError` ve **`PriorResolution = ReturnedToSource`** (iade kaydı kaybolmaz). **İptal / Kalanı kapat vakası:** "Geri taşındı" sonrasında İptal / Kalanı kapat ön araması iade edilmiş N'yi de tarar; N'nin geç commit'i görünürse 409 + N `IntegrityError`, görünmezse komut başarılı.

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
  kaldır → AC-39 kırmızı; (n) atanan kontrolünü kaldır → AC-47 kırmızı;
  (o) 0178 koduna bir `IInventoryLedgerRepository` referansı / `wms_*` koleksiyonuna `OnHand` alanı ekle → AC-6 mimari testi kırmızı;
  (p) görev deposundan `TenantId` yüklemini düşür → kiracılar arası 404 testi kırmızı (AC-13; (d) şirket yüklemi ayrıdır);
  (q) bir controller eyleminden `[HasPermission]`'ı kaldır → `AuthorizationGuardTests` kırmızı (AC-14);
  (r) anahtarsız kullanıcıya sayfa iskeletini çiz → UAS-001 testi kırmızı (AC-22);
  (s) sayaç artışını koşulsuz oku-yaz yap (atomik değil) → eşzamanlı iki görev aynı numara testi kırmızı; `UpdatePutawayTask`'ın
      `Number`'ı kabul etmesine izin ver → değişmezlik testi kırmızı (AC-16);
  (t) 0178'e `GOODS_ISSUE_SALES` yazan bir çağrı ekle → mimari test kırmızı (AC-31 / §17);
  (u) atalar arası rol çakışma kontrolünü kaldır → AC-17 kalıtım testi kırmızı; açık görev kontrolünü kaldır →
      `WMS_LOCATION_HAS_OPEN_TASKS` testi kırmızı (AC-17);
  (v) seri okumasını atla (boş `serialIds` gönder) → AC-51 kırmızı; seri kovası karşılaştırmasını kaldır → AC-53 kırmızı;
  (w) işlem 2'nin `Status == Pending` **ve** `AttemptId` koşullarını birlikte kaldır (aynı sonuçta yeniden okuma kontrolü de) → AC-54
      "tek artış" testi kırmızı (inceleme 04 F-3: tek koşul kaldırılınca öbürü yakalar; sabotaj ikisine birden hedeflenir);
  (x) yeniden denemede tarihleri yeniden üret → AC-5 (replay) kırmızı; 4xx'i Pending say → AC-5 (kalıcı Refused) kırmızı;
  (y) yeniden deneme ön kontrollerini atla → AC-56 kırmızı;
  (z) yeniden deneme sırasını ters çevir (ön kontroller ledger anahtar aramasından önce) → AC-5 "cevap kayboldu + koşul değişti → Posted"
      testi kırmızı (inceleme 02 F-1);
  (aa) `IDEMPOTENCY_KEY_REUSED`'u Refused say → AC-63 kırmızı;
  (ab) sweeper'a ön kontrol / posting ekle → AC-5 "sweeper 0 upstream çağrısı" testi kırmızı (inceleme 02 F-5);
  (ac) seri okumasında tutma / bloke dışlamasını kaldır ya da karışık durum kontrolünü kaldır → AC-62 kırmızı (inceleme 02 F-2);
  (ad) Gsku'da `getSku` bayrağını oku → AC-61 kırmızı (inceleme 02 F-3);
  (ae) `OpenSerialSlot` kısmi tekil index'ini kaldır → AC-52 "ikinci açık görev" **eşzamanlı** testi kırmızı (yalnız index'e hedefli;
       bağımsız inceleme 05 F-4) (inceleme 02 F-6, F-9);
  (ba) açık miktar kontrolünü kaldır → AC-57 "seri olmayan SKU, ikinci görev 422" testi kırmızı (bağımsız inceleme 05 F-4);
  (af) "Geri taşındı"yı Posted / Pending onayda da kabul et → AC-55 kırmızı (inceleme 02 F-9);
  (ag) 422 `INSUFFICIENT_STOCK`'u Pending say ya da ikinci onayı defter çağrısı olmadan Posted yaz → AC-57 kırmızı (inceleme 02 F-7, F-9);
  (ah) `effectiveStorageConditions` okumasından kalıtımı kaldır (yalnız düğümün kendi etiketi) → AC-59 kırmızı (DL-1, inceleme 03 F-5);
  (ai) iki sıcaklık koduna izin ver → AC-59 kırmızı (DL-1);
  (aj) `IntegrityError` onaylı görevde iptale izin ver → AC-63 "görev kilitli" kırmızı (inceleme 03 F-5);
  (ak) uçuş kilidini kaldır → AC-64 "uçuştayken retry → 409" kırmızı (inceleme 03 F-1);
  (al) işlem 2'de etkilenen 0'da sessiz dön → AC-64 "farklı durum → IntegrityError" kırmızı;
  (am) "Uzlaştır"da kaynak ayrımını kaldır (her `holder`'ı kendi hareketi say) → AC-65 "yabancı hareket" testi kırmızı; Uzlaştır'a görev
       kapatma ekle → AC-65 "görev açık kalır" testi kırmızı; izin kontrolünü kaldır →
       AC-65 operatör 403 testi kırmızı;
  (an) sweeper `NotCommitted` turunda `Attempts` artır ya da iç komutu gönder / yerel olay yaz → AC-66 kırmızı; sweeper'a upstream çağrısı ekle →
       AC-66 "0 çağrı" kırmızı;
  (ao) sweeper yazım filtresinden `InFlightUntilUtc` koşulunu kaldır → AC-54 "sweeper + kullanıcı" testi kırmızı; kayıt başına try/catch'i
       kaldır → AC-66 yalıtım testi kırmızı (inceleme 04 F-3);
  (ap) işlem 3'ün posting süresi sınırını kaldır → AC-67 (1) kırmızı; aşımda kilidi hemen bırak → AC-67 (1) "kilit korunur" kırmızı;
       açılış ayar doğrulamasını kaldır → AC-67 (2) kırmızı; `ConfirmedQuantity + q ≤ PlannedQuantity` koşulunu kaldır →
       `PutawayTaskRules` birim testi (AC-67 (5)) kırmızı — gerçek akışta tetiklenmeyen savunma koşulu birim testiyle kanıtlanır
       (inceleme 04 F-2, inceleme 05 F-1); (A) yeniden okumasında koşullu artış değerlendirmesini kaldır → AC-67 (6) kırmızı (bağımsız
       inceleme 08 F-2);
  (aq) sweeper'ın `KeyReusedDifferentPayload` dalını kaldır (Pending bırak) → AC-66 KeyReused testi kırmızı (inceleme 05 F-5);
  (ar) "Uzlaştır"da her holder'ı yabancı say → AC-65 "kendi hareketi" testi kırmızı; seri listesini defterden yazmayı kaldır → AC-65
       seri testi kırmızı (inceleme 05 F-4, F-5);
  (as) sweeper'ın `Committed` → Posted yazımını kaldır (Pending bırak) → AC-66 `Committed` testi kırmızı (inceleme 05 F-5);
  (at) yeni Posted onayda eski Refused onayların `SupersededByNewConfirmation` işaretini / `ConfirmationSuperseded` olayını kaldır →
       AC-55 Superseded testi kırmızı (inceleme 05 F-5);
  (au) sweeper savunma dalını kaldır → AC-67 (3) geç commit testi kırmızı (inceleme 05 F-3);
  (av) "Uzlaştır"daki Pending yasağını kaldır → AC-67 (4) kırmızı (inceleme 05 F-1);
  (aw) yeni onay / "Geri taşındı" / İptal / Kalanı kapat'taki ön ledger aramasını kaldır → AC-68 (i) kırmızı (bağımsız inceleme 05 F-1);
  (ax) savunma dalına `Resolution` boş koşulunu geri koy → AC-67 (3) ve AC-68 (ii) kırmızı;
  (ay) terminal görevde "Uzlaştır"ın görevi yeniden açmasına izin ver → AC-68 (iii) kırmızı;
  (az) `reversedByTransactionId`'yi yok say (ters kaydı `Committed` gibi Posted yaz) → AC-69 kırmızı (bağımsız inceleme 05 F-2);
  (bb) savunma dalının yazımından `Version` koşulunu kaldır → AC-70 **bariyerli sıra vakası** ("son `PriorResolution = ReturnedToSource`")
       kırmızı (ReturnedToSource kaybolur); sistem yazımında `Version++`'ı kaldır → AC-70 "eski If-Match 409" testi kırmızı (bağımsız
       inceleme 06 F-3, 07 F-3 — ayrıştırıcı olmayan "Resolution == null" yarısı çıkarıldı);
  (bc) savunma dalı seçimini yalnız `RefusalSource = PreCheck`'e daralt → AC-66 "defter 4xx reddi" vakası kırmızı; `RefusedOnRetry`
       koşulunu kaldır → AC-66 "sahte `IPostingKeyLookup` çağrı sayacı 0" ölçütü kırmızı (bağımsız inceleme 06 F-2, 07 F-3);
  (bd) işlem 2'nin 0-satır tek kuralında (A) yinelemesini kaldır (her 0 satırı "farklı → IntegrityError" say) → AC-64 kardeş vaka kırmızı;
       (C) dalını kaldır → AC-64 **(C) vakası** (Y'nin Pending'i `IntegrityError` olur) kırmızı; (E) dalını kaldır → AC-64 (E) vakası
       kırmızı (bağımsız inceleme 07 F-1, 08 F-3);
  (be) sweeper seçiminde imleci kaldır (her tur baştan, `Limit(200)`) → AC-66 ">200 NotCommitted, 201. Committed" vakası kırmızı;
  (bf) normal dal ile savunma dalını tek bütçede birleştir → AC-66 "ayrı bütçe" vakalarından **en az biri** kırmızı (iki yön de ölçülür;
       hangi dal önce koşarsa koşsun öbürü aç kalır — bağımsız inceleme 07 F-4, 08 F-3);
  (bh) Refused / IntegrityError / ön arama 409 yolunda yerel zaman çizelgesi yazımını kaldır → AC-15 "(b) `wms_task_events`" ret izi
       sayımı kırmızı; başarısız cevapta satırı `Abandoned` yerine onayla (başarısız sonucu başarı zarfıyla dön) → AC-15 (a) "başarısızda `Pending` +
       `Delivered` = 0, 1 `Abandoned`" kırmızı (satır `Pending` olur; ölçüt `Pending`'i saydığı için ayrıştırıcıdır — bağımsız inceleme 08 F-1,
       09 F-2 a);
  (bi) onay yolunda (satır 2 / 3 / 4 / 13 / 25) `OpenSerialSlot` kaldırmayı çıkar → AC-51 "ikinci giriş → yeni görev" kırmızı
       (bağımsız inceleme 08 F-4);
  (bg) İptal / Kalanı kapat filtresine `Resolution == null` koy → AC-55 "Geri taşındı → İptal / Kalanı kapat başarılı" kırmızı (bağımsız
       inceleme 07 F-2);
  (bj) yeniden okumada `AttemptId` karşılaştırmasını kaldır ((C2)'yi (A) say) → AC-64 "(C2) vakası" kırmızı (X yazımı yineler ve Posted
       yazar; 409 `WMS_CONFLICT` beklenir) (bağımsız inceleme 09 F-5);
  (bk) 0 kayıt etkileyen yazımda da enlist et ve yerel olayı yaz → AC-15 "kilit reddi 409 = 0 / 0 / 0" kırmızı (`Abandoned` satır ve
       olay görünür) (bağımsız inceleme 09 F-2 c);
  (bl) istek anahtarı kontrolünü kaldır (ya da If-Match'ten sonraya al) → AC-4 "aynı onay, ilk isteğin If-Match'i → 200 replay" testi
       kırmızı (409 `WMS_CONFLICT` döner) ve yaratma dışı komut replay testleri kırmızı (bağımsız inceleme 10 F-1);
  (bm) parmak izi karşılaştırmasını kaldır (anahtar bulununca hep replay) → AC-4 "aynı anahtar + farklı gövde → 400" testi kırmızı;
  (bn) yinelenen anahtar / CAS kaybından sonra anahtarın yeniden aranmasını kaldır → AC-4 **eşzamanlı vaka** kırmızı (kaybeden 409 alır);
  (bo) (D)'de "zaten `IntegrityError` → yazım yok" kontrolünü **ve** yazım filtresindeki `Status == Refused`'ı birlikte kaldır (biri öbürünü
       yakalar; (w) deseni) → AC-64 "(D) vaka (ii)" kırmızı (`Version` artar, `PriorResolution` ezilir, ikinci
       `ConfirmationIntegrityError` olayı görünür) (bağımsız inceleme 10 F-2);
  (bp) rol yaratmada `LocationId` index'inden gelen yinelenen kayıtta anahtarın yeniden aranmasını kaldır → AC-4 "eşzamanlı rol
       yaratma" `LocationId` dalı kırmızı (kaybeden 409 `WMS_LOCATION_ROLE_EXISTS` alır) (bağımsız inceleme 11 F-3);
  (bq) onay replay'inde `confirmationSeq`'i anahtar olayından değil görevin son onayından al → AC-4 "onayın `seq`'i" kırmızı
       (`confirmationSeq = 2`) (bağımsız inceleme 11 F-1);
  (br) toplu aramadaki `RequestOp` filtresini kaldır → AC-4 "tekil silme → toplu silme" kırmızı (toplu silme 400
       `IDEMPOTENCY_KEY_REUSED` alır — parmak izi operasyonu içerir, §8.1; satırlar yazılmaz) (bağımsız inceleme 11 F-2, 12 F-3);
  (bs) yaratma index'inden ve aramasından `CreatedBy`'ı çıkar → AC-4 "A ve B aynı anahtar" kırmızı (B, A'nın kaydını replay alır ya da
       400) (bağımsız inceleme 11 F-2);
  (bt) (D) yinelemesinin üst sınırını kaldır → AC-64 "(D) tükenme vakası" kırmızı (409 `WMS_CONFLICT` gelmez, test zaman aşımı)
       (bağımsız inceleme 11 F-3);
  (bu) hedef başı tekil olay index'inden (`wms_task_events`) `RequestOp`'u kaldır → AC-4 "aynı hedef, iki işlem" kırmızı (düzenlemenin
       anahtar olayı yinelenen kayıt alır, düzenleme yazılmaz) (bağımsız inceleme 12 F-3);
  (bv) seri slot index'inden gelen yinelenen kayıtta anahtarın yeniden aranmasını kaldır → AC-4 "özdeş eşzamanlı seri görev
       yaratma" kırmızı (kaybeden 422 alır) (bağımsız inceleme 12 F-2);
  (bw) commit sonucu bilinmeyen sonucu (5xx / istisna) Refused'a eşle → AC-5 "commit sonrası 500" kırmızı (onay Refused, defterde hareket
       var) (Codex incelemesi P2);
  (bx) posting çağrısındaki beklenmeyen istisnayı ya da iptali (`OperationCanceledException`) yakalama → AC-5 "beklenmeyen istisna" ve
       "iptal" vakaları kırmızı (503 + Pending yerine 500) (Codex incelemesi P2, bağımsız inceleme 13 F-2);
  (by) `PostingPendingLong` türetmesini kaldır (uzun Pending de `PostingPending` döner) → AC-46 "uzun Pending" vakası kırmızı (rozet
       "Uzun süredir bekliyor" görünmez) (bağımsız inceleme 13 F-3);
  (bz) bayrak öncelik sırasını ters çevir (`PostingRefused` > `PostingPending` > `PostingPendingLong` > `PostingIntegrityError`) → AC-46
       "öncelik" vakası kırmızı (Pending + `IntegrityError` görevde "Bütünlük hatası" yerine "Onay bekliyor") (bağımsız inceleme 14 F-1);
  (ca) `LongPendingMinutes × 60 > InFlightLeaseSeconds` çapraz kontrolünü kaldır (yalnız > 0 kalsın) → AC-46 "açılış doğrulaması"
       kırmızı (`LongPendingMinutes = 1` ile host başlar) (bağımsız inceleme 14 F-3).
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
| 7 | Failure Path ≥ 4 | ✅ §13 (24) |
| 8 | Authorization: namespace + anahtarlar + politika + aktör | ✅ (Q-08) |
| 9 | Gateway kararı + integration-agent işi | ✅ §15 (INT-WMS) |
| 10 | Acceptance criteria ölçülebilir, kimlikli | ✅ §16 |
| 11 | Test expectations build / verifier / RESX / smoke / sabotaj | ✅ §17 |
| 12 | Audited Events her yazma komutu, yol b, K2 | ✅ §21 (S2 iskelet) |
| 13 | Record Codes (RCS-001) | ⏳ geçici sayaç ✅ (Q-07 B), şirket öneki ✅ (M-2); önek kaydı FU-0178-RCS-02 sahip onayı bekliyor |
| 13a | Saklama koşulu uyumu için PRODUCT-MASTER v1.1 `storageConditions` canlı (PM-LIVE) | ⏳ Q-MDM-3 |
| 14 | Sayfa yardımı (UXH-001) + tur kararı | ✅ §24 |
| 15 | Teslim dilimleri | ✅ §23 |
| 15a | **Kabul şartı (Codex incelemesi P1):** `scripts/pack_approval.py` `slices()` bu paket için DL listesi döndürür (None değil) — `PYTHONUTF8=1 python3 -P -c "import sys; sys.path.insert(0,'scripts'); import pack_approval as p; t=open('<paket>',encoding='utf-8').read(); print(p.slices(t))"` | ✅ 2026-10-26: `['DL-1', 'DL-2', 'DL-3', 'DL-4', 'DL-5', 'DL-6', 'DL-7', 'DL-8']` |
| 16 | XMC-001: MOD-0173'te **beş seam** (aynı sahip MOD-0173 / MVP-1 CT, §5 / §6 / §7'de açık: `SkuReference`, `ProductMasterHttpClient`, `IPostingKeyLookup`, `FindReversalOfAsync`, `CentralAuditDeclarationTests.cs` sayı 67 → 82 + manifest listesi); başka modül değişikliği yok; S2 ihtiyaçları sahiplere soru | ✅ §7 (inceleme 03 F-4, bağımsız inceleme 06 F-1, 09 F-3) |
| 17 | WMS-BUNDLE taslağı CT kabulü (S1 kısmı) | ⏳ |
| 18 | Paket kararları Q-01…Q-09, Q-11…Q-13, Q-MDM-1 | ✅ §19.1.1 (Q-12 / Q-13 geliştiricinin ilk ölçümü); açık: Q-10 + diğer ekipler §19.5 |
| 19 | DCP-002 ön kontrol çıkış 0 | ✅ 2026-10-21 |
| 20 | DCP-009 servis notu güncellemesi (SupplyChainService → InventoryService) | ⏳ FU-0178-DCP-01 (DCP sahibi) |
| 20a | domain-config `:36`, `:72` W-2 ile hizalanması | ⏳ FU-0178-DC-01 |
| 20b | İnceleme 01 F-1…F-16, inceleme 02 F-1…F-10, inceleme 03 F-1…F-5 + 02 F-5 kalanı işlendi (§19.1.4, §19.1.5). İnceleme 03 bağımsızdı (yeni oturum, effort high; KOŞULLU, ENGEL yok); sahip kararı (Beste 2026-10-23): onaydan önce hepsi düzeltilsin. İnceleme 04 F-1…F-4 işlendi (§19.1.6); inceleme 04, inceleme 03 ile aynı oturumdandı — CT-APR-02 bağımsızlığını CT değerlendirir. İnceleme 05 ön kontrolü (ebfad1f4, bağımsız değil) F-1…F-5 işlendi (§19.1.7). **Bağımsız inceleme 05** (oturum d2ae856a, effort high, KOŞULLU, ENGEL yok) F-1…F-7 işlendi (§19.1.8). **Bağımsız inceleme 06** (oturum 0e2faf59, effort high, KOŞULLU, ENGEL yok; 05'in F-1…F-7'si kapandı) F-1…F-6 işlendi (§19.1.9). **Bağımsız inceleme 07** (oturum 6217bf78, effort high, KOŞULLU, ENGEL yok; 06'nın F-1 / F-4 / F-5 / F-6'sı kapandı, F-2 / F-3 kısmen) F-1…F-5 işlendi (§19.1.10). **Bağımsız inceleme 08** (oturum 55a37ec9, effort high, KOŞULLU, ENGEL yok; 07'nin F-1…F-5'i kapandı) F-1…F-5 işlendi, F-1 sahip kararıyla (§19.1.11). **Bağımsız inceleme 09** (oturum 88fcdff2, effort high, KOŞULLU, ENGEL yok; 08'in F-2…F-5'i kapandı, F-1 kısmen) F-1…F-5 işlendi (§19.1.12). **Bağımsız inceleme 10** (oturum c90bad53, effort high, KOŞULLU, ENGEL yok; 09'un F-1…F-5'i kapandı) F-1…F-3 işlendi (§19.1.13). **Bağımsız inceleme 11** (oturum 708cc355, effort high, KOŞULLU, ENGEL ve ORTA yok; 10'un F-1…F-3'ü kapandı) F-1…F-3 sahip kararıyla işlendi (§19.1.14). **Bağımsız inceleme 12** (oturum 771fbbcf, effort high, KOŞULLU; 11'in F-1…F-3'ü kapandı) F-1…F-4 ve **Codex incelemesi** (sahip tarafından verildi, 2026-10-26; CONDITIONAL PASS; CT doğruladı) P1 / P2 sahip kararıyla işlendi (§19.1.15). **Bağımsız inceleme 13** (oturum 075041e2, effort high, KOŞULLU, ENGEL ve ORTA yok; 12'nin F-1…F-4'ü ve Codex P1 / P2 kapandı) F-1…F-3 sahip kararıyla işlendi (§19.1.16). **Bağımsız inceleme 14** (oturum c4e41a9c, effort high, KOŞULLU, ENGEL ve ORTA yok; 13'ün F-1…F-3'ü kapandı) F-1…F-3 sahip kararıyla işlendi (§19.1.17) | ✅ işlendi / ⏳ düzeltmelerin CT kontrolü + onay satırı |
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
| Q-09 | Pending onay sweeper'ı | Kabul: 2 dakikadan eski Pending onaylar 60 sn'de bir, aynı `PostingKey` ile. **Not — Beste 2026-10-23: sweeper yalnız uzlaştırır; sınırsız deneme kullanıcının "Yeniden dene"si için geçerli** | §8.3 |
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
| M-5 | Planlanandan fazla onay yasak; onayı atanan ya da sorumlu verir; operatör atanmamış görevi üstlenir (`confirm` izni yeter); Pending sweeper aynı anahtarla; açık görevde otomatik askı yok, onayda kontrol; bir düğüm çok rol alabilir ama Receiving + Staging değil; açık görevi olan düğümden rol kaldırılamaz; diller 7, RTL yalnız ar. **Not — Beste 2026-10-23: sweeper yalnız uzlaştırır; sınırsız deneme kullanıcının "Yeniden dene"si için geçerli** | §4.4, §8.3, §8.13, §8.15, AC-17, AC-40, AC-47, AC-49, AC-50 |
| M-6 | S2'ye öneriler (kapsam değil) | §20 FU-0178-S2-UX |

#### 19.1.3 Bağımsız inceleme 01 — kararlar ve düzeltmeler (hedef 0cb3940af, KOŞULLU; CT değerlendirmesi + sahip kararı Beste 2026-10-21)
| Bulgu | Karar / düzeltme | Pakette |
|---|---|---|
| F-1 (ENGEL) | **Sahip kararı:** seri SKU'da S1 yalnız tam miktar; kovadaki bütün seriler sistemce okunur; kısmi onay / kısa kapatma yok; kova değiştiyse ret; kısmi = MOD-0182 FU | §4.1, §4.2, §5, §8.16, §12, §13, AC-51…AC-53, FU-0178-SERIAL-01 |
| F-2 | Tarihler işlem 1'de yazılır, her denemede aynen; 422 dışı 4xx kalıcı Refused, yalnız 503 / zaman aşımı / ağ Pending | §4.2, §8.3, AC-5 |
| F-3 | İşlem 2 koşullu güncelleme (`Status == Pending`) | §8.3, AC-54, sabotaj (w) |
| F-4 | Refused sonrası yeni onay ya da "Geri taşındı"; her yeniden denemede ön kontroller yeniden | §4.2, §8.3, §11.3, AC-55, AC-56 |
| F-5 | §1 cümlesi düzeltildi; MOD-0173 `storageConditions` okuma seam'i §5'te; kabul ortamı 4010 köprüsü (E3 / E4), canlı kabul PM-LIVE sonrası | §1, §5, §8.12 |
| F-6 | DL-1 → DL-1a / DL-1b; WMS-BUNDLE dondurma ayrı CT adımı; sunucu yanlı AC'ler sunucu dilimine; AC-20 tek dilim | §23 |
| F-7 | AC-15 istisnası + beklenen satır sayıları | AC-15 |
| F-8 | Eksik guard sabotajları | §17 (o)…(y) |
| F-9 | §1 kabul aralığı, §21 silme satırları, WMS bölüm sırası | §1, §21, WMS §3 |
| F-10 | Hata kodları tek liste / operasyon başına eşleme | §12, WMS §4 / §5 |
| F-11 | Başka şirketin kaydına id ile erişim 404; 403 yalnız başlık ↔ token / üyelik | §8.1, §13.10, AC-13 |
| F-12 | `Number` kiracı genelinde tekil; önek devri bilgi notu; 7 hane AC | §4.6, §22, AC-16 |
| F-13 | Rol kalıtımı + atalar arası çakışma | §4.4, §12, AC-17 |
| F-14 | FU-0178-PKS-01 | §20 |
| F-15 | FU-0178-DC-01 (domain-config) | §18, §20 |
| F-16 | W-6 Beste'nin açık S2 kararı; AC-33 ona bağlı | §19.5, AC-33 |
| CT ek | Açık miktar yarışı: iyimser yaratma, nihai koruma onayda | §8.3, AC-57 |

#### 19.1.4 Bağımsız inceleme 02 — kararlar ve düzeltmeler (hedef 5f7833253, KOŞULLU; CT değerlendirmesi: hepsi kabul)
| Bulgu | Karar / düzeltme | Pakette |
|---|---|---|
| F-1 (ENGEL) | Yeniden deneme önce ledger'da `PostingKey`'i arar (yeni MOD-0173 portu `IPostingKeyLookup`, upstream'siz); işlenmişse Posted, ön kontrol koşmaz; ön kontroller yalnız işlenmemiş anahtarda. `IDEMPOTENCY_KEY_REUSED` = `IntegrityError` (bütünlük hatası, Refused değil, Superseded / Returned kapalı, istisna) | §4.2, §5, §8.3, §12, §13.18-19, AC-5, AC-63, sabotaj (z), (aa) |
| F-2 | Seri okuma MOD-0175 emsaliyle tutma / bloke serilerini dışlar; aynı lokasyonda aynı lot + SKU'nun AVAILABLE dışı stoğu varsa seri görevi reddedilir (`WMS_SERIAL_BUCKET_MIXED_STATUS`) | §5, §8.16, §12, AC-62, sabotaj (ac) |
| F-3 | Gsku seri bayrağı ürünün bayrağından (ledger ile aynı kural, OD-10) | §4.1, §8.16, §12, AC-61, sabotaj (ad) |
| F-4 | AC-17'nin görev parçası AC-58'e (DL-1b); DL-1a görevsiz AC-59; AC-16 / AC-37 / AC-38 DL-1b'ye; AC-19 taslaktan türetilmiş şemaya; CT-FREEZE kendi ölçütü AC-60 | §16, §23 |
| F-5 | Sweeper token taşımaz: yalnız ledger anahtar kontrolü; işlenmemiş Pending kullanıcı "Yeniden dene"siyle ilerler; 0177 emsal ifadesi düzeltildi; M-5 "sınır yok" korunur | §8.3, §8.13, §13.20, AC-5, sabotaj (ab) |
| F-6 | Açık seri görevi için kova anahtarlı kısmi tekil index (`OpenSerialSlot`); "yarış yok" iddiası düzeltildi | §4.6, §8.3, §8.16, sabotaj (ae) |
| F-7 | AC-57 tek yol: ön kontrol yalnız durum; yeterlilik ledger `INSUFFICIENT_STOCK` → Refused | §8.3, AC-57, sabotaj (ag) |
| F-8 | Seri tel biçimi = numara (normalize), TRACE lokasyon güncellemesi, kova okuma emsali — ölçülmüş gerçek | §5, §8.16, §19.2 |
| F-9 | AC-15 replay sayımı (defter 0 ek); AC-52 / AC-55 / AC-57 sabotajları | AC-15, sabotaj (ae), (af), (ag) |
| F-10 | 4010 veri setinin kopyası + SHA-256'sı kanıt klasörüne | §5 |

#### 19.1.5 Bağımsız inceleme 03 — kararlar ve düzeltmeler (hedef 367e8ff78, KOŞULLU, ENGEL yok; sahip kararı Beste 2026-10-23: onaydan önce hepsi; CT değerlendirmesi: hepsi kabul)
| Bulgu | Karar / düzeltme | Pakette |
|---|---|---|
| F-1 | Uçuş kilidi (`AttemptId` + `InFlightUntilUtc`, işlem 1'de ve her yeniden deneme başında atomik; kilit geçerliyken 409 `WMS_CONFIRMATION_IN_FLIGHT`, sweeper atlar); işlem 2'de etkilenen 0 ve farklı durum → `IntegrityError` (sessiz dönüş yok) | §4.2, §8.3, §8.13, §12, AC-64, sabotaj (ak), (al) |
| F-2 | "Uzlaştır" (`ResolvePutawayIntegrityCommand`, izin önerisi `warehouse.putaway-tasks.resolve`, yalnız sorumlu, gerekçe, ledger yeniden okunur: Posted ya da Refused + görev kapanışı; açık miktar / `OpenSerialSlot` / rol kilidi serbest); `IntegrationFlag.PostingIntegrityError`; olaylar `ConfirmationIntegrityError`, `IntegrityResolved`; AC-15 sayımı; tahmin edilebilir anahtar notu + FU-0178-KEY-01 | §4.1–4.3, §8.3, §11.3, §11.4, §12, §14.2, §21, AC-15, AC-63, AC-65, sabotaj (am) |
| 02 F-5 kalanı | Sweeper = yalnız uzlaştırıcı: `ReconcilePendingConfirmationCommand`, ledger araması, upstream / token yok; `NotCommitted` turunda `Attempts` artmaz, denetim satırı yok; kapsam `RecallQuarantineSweeper.cs:72-76` deseni (ölçüldü); §13.3 ve §21 düzeltildi; Q-09 / M-5 satırlarına sahip notu | §8.3, §13.3, §19.1.1, §19.1.2, §21, AC-15, AC-66, sabotaj (an) |
| F-3 | WMS-BUNDLE operasyon başına hata listeleri tamamlandı (create, update, claim, confirm, retry, close-short, cancel, resolve) | WMS §4 / §5 |
| F-4 | §6 / §7 / §18 #16 MOD-0173'teki üç seam'le hizalandı; `Replay` atfı `:216-226`; `ForwardCallerHeadersHandler` atfı `:21-35`; §13 madde sırası | §6, §7, §13, §18, §19.2 |
| F-5 | AC-59 sabotajları (ah), (ai) (DL-1a); AC-63 "görev kilitli" sabotajı (aj); F-1 / F-2 / 02 F-5 AC'lerinin sabotajları | §17 |

#### 19.1.6 Bağımsız inceleme 04 — kararlar ve düzeltmeler (hedef 13d343c9a, KOŞULLU; CT değerlendirmesi: hepsi kabul)
| Bulgu | Karar / düzeltme | Pakette |
|---|---|---|
| F-1 (ORTA) | Defter append-only: "defter sonradan düzeltildi" yolu kaldırıldı. `IPostingKeyLookup` anahtarı tutan hareketi (`holder`: kimlik, kaynak üçlüsü, miktar, from / to) döndürür. "Uzlaştır": (a) kaynak bu onay → Posted, defter değerleri yazılır, fark varsa `ConfirmationDeviation`; (b) kaynak yabancı / `NotCommitted` → çözümsüz Refused, **görev açık kalır**, normal Refused akışı; otomatik kapatma kaldırıldı, kapatma sorumlunun ayrı seçimi | §4.2, §4.3, §5, §8.3, §13.22, §21, AC-15, AC-65, sabotaj (am) |
| F-2 | Kilit dayanağı = süreç içi posting süresi (`PostingTimeoutSeconds` öneri 60 sn, bağlı `CancellationTokenSource`; upstream çağrı başına 5 sn — `DependencyInjection.cs:51`, `appsettings.json:35`, ölçüldü); açılışta `Lease ≥ Posting + 30`; aşımda Pending + kilit bırakılır **(inceleme 05 F-3 ile değişti: kilit `InFlightUntilUtc`'ye kadar korunur)**; işlem 2'de koşullu artış (`Confirmed + q ≤ Planned`), aşarsa `IntegrityError` (`OverPlanned`) | §4.2, §8.3, §13.23, AC-67, sabotaj (ap) |
| F-3 | Sweeper yazım filtresi (`Status == Pending` + `InFlightUntilUtc` boş / geçmiş; kilit almaz; 0 etkilenen → hiçbir şey yazmaz); kayıt başına hata yalıtımı + tur başına 200 (`RecallQuarantineSweeper.cs:65`, `:68-88`); kiracılar arası tarama okuması açık istisna; AC-54 kurulabilir senaryoya ve AC-15 adlandırmasına çevrildi; sabotaj (w) iki koşula hedeflendi | §8.3, AC-54, AC-66, sabotaj (w), (ao) |
| F-4 | Onay durum makinesi pakette ve WMS §6.1'de: `Pending → IntegrityError`, `IntegrityError → Posted` (Uzlaştır-a), `IntegrityError → Refused` (Uzlaştır-b); port imzası F-1'deki gibi | §8.3, §5, WMS §6.1 |

#### 19.1.7 İnceleme 05 ön kontrolü — kararlar ve düzeltmeler (hedef 1f185d6a4, KOŞULLU; oturum ebfad1f4 — bağımsız değil, bulgular geçerli; CT: hepsi kabul)
| Bulgu | Karar / düzeltme | Pakette |
|---|---|---|
| F-1 (ORTA) | Görevde Pending / uçuştaki onay varken "Uzlaştır" 409 `WMS_CONFIRMATION_PENDING`; işlem 2'nin `OverPlanned` koşulu savunmaya indi ve `PutawayTaskRules` birim testiyle sabotajlanır; AC-67 gerçek akışa göre (geç commit → sweeper savunma dalı `StatusDiverged` → Uzlaştır-a → `ConfirmationDeviation` `OverPlanned`); §4.1'e "onaylanan ≤ planlanan" kuralı ve Uzlaştır-a istisnası | §4.1, §8.3, §12, §13.23, AC-65, AC-67, sabotaj (ap), (av) |
| F-2 | Kilit / posting süreleri `TimeProvider` ile (0175 emsali `QuarantineServiceCollectionExtensions.cs:36`); AC-54 / AC-64 sahte saatle; açılış doğrulaması gerçek ayarla | §4.2, AC-54, AC-64 |
| F-3 | Posting süresi aşımında kilit `InFlightUntilUtc`'ye kadar korunur (+30 sn pay commit görünürlüğü ve Mongo işlem ömrü için); sweeper savunma dalı (son 30 dk ön kontrol reddi + ledger `Committed` → `IntegrityError` `StatusDiverged`) | §4.2, §8.3, §13.23, AC-66, AC-67, sabotaj (au) |
| F-4 | `holder` + `lotId`, `serialIds`, `fromStockStatus` / `toStockStatus`; Uzlaştır-a seri SKU'da defterin serilerini yazar, fark `ConfirmationDeviation`'da | §5, §8.3, AC-65, sabotaj (ar) |
| F-5 | Olay türleri `ConfirmationSuperseded`, `ConfirmationReturned`; §21 superseded alanları; AC-66 KeyReused dalı; "sweeper IntegrityError üretmez" cümlesi yalnız 0-etkilenen durumuna daraltıldı; sabotajlar (aq)…(at) | §4.3, §8.3, §21, AC-55, AC-66, §17 |
| Tutarlılık | §8.3'e her geçiş için tetikleyici / yazılan alanlar / olay / AC / sabotaj tablosu (19 satır, boş hücre yok) | §8.3 |

#### 19.1.8 Bağımsız inceleme 05 — kararlar ve düzeltmeler (hedef 0f0cf322c, oturum d2ae856a, KOŞULLU, ENGEL yok; CT: hepsi kabul)
| Bulgu | Karar / düzeltme | Pakette |
|---|---|---|
| F-1 (ORTA) | Yeni onay / "Geri taşındı" / İptal / Kalanı kapat önce kapatacağı Refused onayların anahtarını ledger'da arar; `Committed` → onay `IntegrityError` (`StatusDiverged`, `PriorResolution`), komut 409 `WMS_POSTING_INTEGRITY_ERROR`; savunma dalı `Resolution`'dan ve görev durumundan bağımsız; terminal görevde Uzlaştır yalnız onay kaydını düzeltir, görevi yeniden açmaz (`PostedAfterCancel` sapması); AC-67 (3) kendi senaryosunda geçebilir (kova ≥ 120, serbest bırakma adımı, görev Completed kalır) | §4.2, §8.3 (akış satır 15, 18, 20, 23, 24), §12, AC-15, AC-67, AC-68, sabotaj (aw), (ax), (ay) |
| F-2 (ORTA) | `holder.reversedByTransactionId?` (bugün `IsReversedAsync` bool — `IInventoryLedgerRepository.cs:20`, `InventoryLedgerRepository.cs:110-113`; kimlik için eklemeli okuma `FindReversalOfAsync` seam'i); retry / sweeper → `IntegrityError` (`HolderReversed`); Uzlaştır → (b) | §4.2, §5, §8.3 (satır 3, 4, 21, 22), AC-69, sabotaj (az) |
| F-3 (ORTA) | DL-1b'ye AC-13 / AC-14 (görev uçları), AC-67, AC-68, AC-69; §1 S1 aralığı AC-1…AC-24, AC-37…AC-69 | §1, §23 |
| F-4 | AC-54 fikstürü 2q ≤ planlanan; (ae) yalnız index'e; açık miktar kontrolü seri olmayan SKU'da ayrı (AC-57 eki, sabotaj (ba)) | AC-54, AC-57, §17 |
| F-5 | §4.2 `HolderMovement` §5 ile birebir (lot, seriler, stok durumları, `reversedByTransactionId`); WMS §3.2 eşitlemesi bağımsız inceleme 06 F-5 ile tamamlandı (r7'de eksik 6 alan vardı) | §4.2 |
| F-6 | Eylem başına izin + atanan kuralı tek yerde (§8.3); §12, §14.2, WMS close-short / retry satırları hizalandı | §8.3, §12, §14.2, WMS §4 |
| F-7 | (a) akış satırları 3 / 4 / 13 / 15; (b) AC-15 Superseded / Returned sayımı; (c) "Bütünlük hatası" rozeti §8.15 / AC-46; (d) hosted service adı tek; (e) §10 komut / validator ağacı; (f) §18 #7, #20b, §19.1.6 notu; (g) WMS §3.1 görevden `serialIds` kaldırıldı, §5.1 `WMS_CONFIRMATION_PENDING` Uzlaştır'ı sayar; (h) atıflar `InventoryLedgerRepository.cs:173`, `QuarantineLedgerReads.cs:118` (doğrulandı) | ilgili bölümler |

#### 19.1.9 Bağımsız inceleme 06 — kararlar ve düzeltmeler (hedef d86c1123e, oturum 0e2faf59, KOŞULLU, ENGEL yok; CT: hepsi kabul)
| Bulgu | Karar / düzeltme | Pakette |
|---|---|---|
| F-1 (ORTA) | `FindReversalOfAsync` MOD-0173'te **dördüncü seam** (`IInventoryLedgerRepository` + `InventoryLedgerRepository`, yalnız eklemeli okuma); §5 ayrı satır, §6 istisnası, §7 XMC-001, §18 #16 "dört seam"; ölçüldü: `LedgerImmutabilityArchitectureTests.cs:28-36` `Find` önekini kabul eder, `InventoryIndexConfiguration.cs:49-55` bir harekete en çok bir ters kayıt verir | §5, §6, §7, §18 |
| F-2 | Onaya `RefusalSource` (`PreCheck \| Ledger4xx \| Resolve`), `RefusedOnRetry`, `RefusedAtUtc`; savunma dalı yeniden denemenin ön kontrol **ve** defter 4xx reddini son 30 dk tarar | §4.2, §8.3 (satır 5, 18), AC-66, sabotaj (bc) |
| F-3 | Her yazım `Version` CAS + `Version++`; kapatıcı komutlarda hedef onay durumu filtresi; `IntegrityError` ile `Resolution`'ın birlikte bulunmaması tek koşullu güncellemeyle; kaybeden yeniden okur (409 `WMS_POSTING_INTEGRITY_ERROR` / `WMS_CONFLICT`) | §8.3, akış satırları, AC-70, sabotaj (bb) |
| F-4 | §21: ön aramanın 409'da yazdığı `confirmation-integrity-error` (Confirm, CloseShort, Cancel, MarkReturned) ve Reconcile `HolderReversed`; AC-15: 409 istisnası + sweeper `HolderReversed` = 1 | §21, AC-15 |
| F-5 | WMS taslağı r7'ye eşitlendi: `holderMovement` 6 eksik alan, retry `HolderReversed`, resolve ters kayıt + terminal görev, `WMS_POSTING_INTEGRITY_ERROR` anlamı, `refusalSource` / `refusedOnRetry` / `refusedAtUtc` | WMS §3.2, §4, §5 |
| F-6 | (a) akış satır 2 / 5 confirm–retry olay ayrımı; (b) satır 11 `PriorResolution`; (c) işlem 2'deki eski parantez kaldırıldı; (d) AC-52 eşzamanlı vaka; (e) retry (i) cümlesi yeniden yazıldı; AC-67 (3) fikstür kurulumu (`QuarantineMovementPoster` ile iki `STATUS_TRANSFER`) | §8.3, AC-52, AC-67 |

#### 19.1.10 Bağımsız inceleme 07 — kararlar ve düzeltmeler (hedef 6b6af2664, oturum 6217bf78, KOŞULLU, ENGEL yok; CT: hepsi kabul, inceleyicinin önerdiği düzeltmelerle)
| Bulgu | Karar / düzeltme | Pakette |
|---|---|---|
| F-1 (ORTA) | İşlem 2 / retry (i) / sweeper için **tek 0-satır kuralı** (A) hâlâ Pending + bu deneme → yeni V ile yinele (≤ 3), (B) aynı sonuç → no-op, (C) başka deneme → 409 IN_FLIGHT, (D) yalnız onayın kendi durumu farklıysa IntegrityError; Superseded hedefleri `arrayFilters` ile, IntegrityError kardeş atlanır; CAS tablosunda işlem 2 istisna; §12 + WMS toplu atama / "Geri taşındı" `WMS_CONFIRMATION_PENDING`; AC-64 kardeş vaka; sabotaj (bd) | §8.3 adım 4, CAS tablosu, akış satır 11 / 15 / 25 / 26, §12, AC-64, WMS §4 |
| F-2 | Komut başına hedef küme + filtre (CAS tablosu); İptal / Kalanı kapat ön araması görevin tüm Refused onayları, filtre V + `Status == Refused` (Resolution koşulu yok); AC-55 / AC-70'e "Geri taşındı → İptal / Kalanı kapat başarılı" | §8.3, §12, akış satır 16 / 20 / 27, AC-55, AC-70, sabotaj (bg) |
| F-3 | AC-70 bariyerli sıra vakası (komut 200 → son `PriorResolution = ReturnedToSource`), (bb) buna bağlandı ve `Resolution == null` yarısı çıkarıldı; (bc) ölçütü sahte `IPostingKeyLookup` çağrı sayacı | AC-66, AC-70, sabotaj (bb), (bc) |
| F-4 | Sweeper seçimi kayda yazmadan ilerleyen imleçle (`(TenantId, _id)`, bellekte, tur başına 200); normal / savunma dalı ayrı imleç ve bütçe; iki tarama için §4.6 index; AC-66 ">200 NotCommitted, 201. Committed" | §4.6, §8.3 sweeper, AC-66, sabotaj (be), (bf) |
| F-5 | (a)–(c) akış tablosu yeniden yazıldı: her satırın yazılan alanları §4.2 ve CAS kuralıyla birebir (satır 6 Ret\* + V++, satır 14 / 22 / 24 `RefusalSource = Resolve` + `RefusedAtUtc`, eksik V++'lar, satır 11 / 20 `Resolution = null`, yeni satır 25–27); (d) AC-15 istisnaları tek tek; (e) WMS §6.1 savunma satırı eşitlendi | §8.3, AC-15, WMS §6.1 |
| Son tutarlılık geçişi | Akış tablosunun "yazılan alanlar" sütunu §4.2 alan listesiyle (Status, PostingKey, Transaction/PostingDate, SerialIds, AttemptId, InFlightUntilUtc, Attempts, LastAttemptAtUtc, ConfirmedBy / At, TransactionId / Number, Error\*, Ret\*, Resolution, ResolutionReason, PriorResolution, IntegrityResolution, Resolved\*, ResolveReason, HolderMovement) ve CAS kuralıyla tek tek karşılaştırıldı; §4.2'ye `ResolutionReason` adı eklendi — **düzeltme (bağımsız inceleme 08 F-4 / F-5):** "birebir" iddiası tam değildi (satır 1'de onayın kendi değerleri, satır 2 / 3 / 4 / 13 / 25'te `OpenSerialSlot` kaldırma ve satır 15 tetikleyicisinde satır 23 eksikti); §19.1.11'de tamamlandı | §4.2, §8.3 |

#### 19.1.11 Bağımsız inceleme 08 — kararlar ve düzeltmeler (hedef 58c2cd6e6, oturum 55a37ec9, KOŞULLU, ENGEL yok; CT: hepsi kabul)
| Bulgu | Karar / düzeltme | Pakette |
|---|---|---|
| F-1 (ORTA) | **Sahip kararı (Beste 2026-10-26): BL-501 olduğu gibi kalır.** Merkezi denetim yalnız başarılı komutları kaydeder (komut başına tek, sabit adlı olay; başarısız zarf / istisna → `AbandonAsync`, olay yok — `CentralAuditBehavior.cs:8-9, :59-63`, `CentralAuditRecorder.cs:41-46`); Refused / Pending / IntegrityError / 409 / 503 merkezi denetime gitmez, GxP izi yerel, silinmeyen `wms_task_events` zaman çizelgesinde aynı işlemde; K2 anlamı buna göre; defterin enlist'i (`InventoryLedgerRepository.cs:181`) 0178 komutunun olayına no-op (`CentralAuditRecorder.cs:11-12`); sweeper iç komutu IntegrityError'da başarısız zarf, `NotCommitted`'ta gönderilmez | §8.8, §21, akış olay sütunu, AC-15, sabotaj (bh) |
| F-2 | 0-satır kuralı Pending'den çıkan **her** yazımı kapsar (kilit alımı, ön kontrol reddi, defter 4xx, IntegrityError yazımları); (A) koşullu artışı taze değerlerle yeniden değerlendirir (aşım → satır 12, maskelenmez); yeni (E) dalı (sonuç Refused / IntegrityError iken kayıt Posted → yazım yok, 409 `WMS_CONFLICT`); AC-67 (6) yazım yolu | §8.3 adım 4, CAS tablosu, akış satır 12 / 25 / 28, AC-67, sabotaj (ap) |
| F-3 | AC-64'e kullanıcı yolunda (C) ve (E) vakaları, (bd) bunlara bağlandı; AC-66'ya ters yön bütçe vakası, (bf) iki yönü ölçer; satır 25'in AC sütunu yalnız AC-64 | AC-64, AC-66, akış satır 25 / 26, sabotaj (bd), (bf) |
| F-4 | Onayla `Completed` olan seri görevde `OpenSerialSlot` kaldırılır (satır 2 / 3 / 4 / 13 / 25, "Slot↓"); AC-51'e "aynı lot aynı Receiving lokasyonuna ikinci giriş → yeni görev açılır" + sabotaj (bi) | akış tablosu, AC-51, §17 |
| F-5 | (a) `WMS_CONFIRMATION_IN_FLIGHT` WMS confirm listesinde ve §12'de (onay ucu); (b) (A) tükenmesi için yeni 503 `WMS_CONFIRMATION_CONFLICT_RETRY` (§12, WMS §5.1, WMS 503 listeleri); (c) WMS §5.1 `WMS_CONFIRMATION_PENDING` anlamı "Geri taşındı" + toplu atama; (d) satır 1 onayın kendi alanları, satır 15 tetikleyicisinde satır 23; §19.1.10 "birebir" ifadesi düzeltildi | §8.3, §12, §19.1.10, WMS §4 / §5 |
| Son tutarlılık geçişi | Akış tablosu ↔ §4.2 ↔ CAS ↔ §21 ↔ AC-15: her satırın yazılan alanları §4.2'de; her yazım V++; her satırın merkezi sayısı §21 sütunu ve AC-15 (a) ile, yerel olayı §4.3 türleri ve AC-15 (b) ile aynı; olay adları komut başına sabit (§21) | §8.3, §21, AC-15 |

#### 19.1.12 Bağımsız inceleme 09 — kararlar ve düzeltmeler (hedef 963f6230a, oturum 88fcdff2, KOŞULLU, ENGEL yok; CT: hepsi kabul)
| Bulgu | Karar / düzeltme | Yer |
|---|---|---|
| F-1 (ORTA) | BL-501 kaydı ad ve gövde taşımaz (`ICentralAuditDeclaration.cs:24-30`; `PlatformCentralAuditClient.cs:54-60`, `:63-89`). §21 komut başına BL-501 meta verisiyle yeniden yazıldı: `Category = MasterData`, Platform enum `Operation` (Create / Update / Delete / Assign / Execute / LifecycleTransition), `EntityType`, `EntityId` kaynağı, `RequestType` = komut adı, `SourceModule` = manifest kodu (`PUTAWAY-PICKING-PACKING`, InventoryService büyük harf emsali). Olay "adları" yalnız yerel `Kind`, değerler yalnız yerel `Data`. Rol komutlarının önceki / sonraki değerleri için insert-only `wms_location_role_events` (öneri) | §3, §4.3, §4.6, §8.3 (olay sütunu, satır 15), §8.8, §8.14, §14.1, §14.4, §21, AC-15, AC-20, AC-48, AC-54, AC-55 |
| F-2 | (a) AC-15 ölçütü: başarı = 1 satır `Pending` / `Delivered`; başarısızlık = `Pending` + `Delivered` 0, işlem commit olduysa 1 `Abandoned`; (bh)'nin ikinci yarısı artık ayrıştırıcı. (b) Çökme → `OrphanAfter` (10 dk) sonra `Unconfirmed` (§8.8, AC-15 vakası; Refused olan onayda 1 `Unconfirmed` kalır, sweeper Posted yazarsa 2 kayıt — kabul). (c) Enlist'siz başarı → 0: tekrar, sweeper (B) / 0-kayıt, boş toplu atama / silme; enlist yalnız ≥ 1 kayıt etkileyen yazımda (sabotaj (bk)). (d) Her `Kind`'ı yazan yazım §4.3 tablosunda; `Completed` = görevi onaylanan miktarla tamamlayan Posted yazımı (terminal görevde ve Kalanı kapat'ta yok); AC-15 (b) sayımı kurala bağlandı | §4.3, §8.3, §8.8, §21, AC-15, sabotaj (bh), (bk) |
| F-3 | Beşinci MOD-0173 seam'i `CentralAuditDeclarationTests.cs`: `:35` 67 → 82 (15 S1 komutu; DL-1a sonunda 71), `:42-49` listesine `PutawayPickingPackingManifestProvider`, `:107` `seen` 0178 callback'leri kadar; `:66` ve sweeper listesi değişmez | §5, §6, §7, §18 #16, §23 DL-1a, AC-1 |
| F-4 | Akış tablosunun satır 28'inden sonra satır sonu ve boş satır; terminal durum maddesi ve M-4 / izin / Pending kilidi maddeleri tablodan ayrıldı | §8.3 |
| F-5 | (C) kilit geçerliliğinden bağımsız: (C1) yabancı kilit geçerli → 409 `WMS_CONFIRMATION_IN_FLIGHT`; (C2) yabancı kilit dolmuş → yazım yok, 409 `WMS_CONFLICT` + güncel görev, kayıt sweeper ya da "Yeniden dene" ile ilerler; kilit alımında ve sweeper'da dolmuş yabancı kilit (A)'dır | §8.3 (C), satır 26 / 28, §12, AC-64, sabotaj (bj), WMS taslağı §5 / §8 |

#### 19.1.13 Bağımsız inceleme 10 — kararlar ve düzeltmeler (hedef b456a9b22, oturum c90bad53, KOŞULLU, ENGEL yok; CT: hepsi kabul)
| Bulgu | Karar / düzeltme | Yer |
|---|---|---|
| F-1 (ORTA) | Yaratma dışı yazma komutlarının istek idempotency'si (0176 / 0177 emsali): `WarehouseTaskEvent` ve `WarehouseLocationRoleEvent`'e `IdempotencyKey` + `RequestFingerprint`; komutun **anahtar olayı** (ilk iş yazımının olayı) bunları taşır; tekil kısmi index `(TenantId, TaskId \| RoleId, ActorUserId, IdempotencyKey)` (`ExpiryIndexConfiguration.cs:78-80`, `RecallIndexConfiguration.cs:94-96`) + toplu komutlar için tekil olmayan `(TenantId, LegalEntityId, ActorUserId, IdempotencyKey)`; yaratmada parmak izi eksikti → `WarehouseTask.RequestFingerprint`, rol yaratma için `WarehouseLocationRole.IdempotencyKey` + `RequestFingerprint` + tekil `(TenantId, LegalEntityId, IdempotencyKey)` (rol yaratma tekrarı 409 `WMS_LOCATION_ROLE_EXISTS` yerine 200). Anahtar If-Match'ten ve iş kurallarından **önce** aranır (Recall `CaseCommandHandlers.cs:540` / `:546`); aynı parmak izi → 200 + güncel kayıt, yazım / enlist / yerel olay yok; farklı → 400 `IDEMPOTENCY_KEY_REUSED`; parmak izi alanları komut başına; eşzamanlı kaybeden (yinelenen anahtar ya da CAS kaybı) anahtarı yeniden arar → replay (Inventory `InternalMovementPostingService.cs:188-194`); toplu replay yazılmayan satırı `Conflict` / `NotFound` döner | §4.1–§4.6, §8.1, §8.3 (akış satır 29, anahtar olayı işaretleri, CAS tablosu), §8.8, §12, §13.2, §13.24, §18 #7, §21, AC-4, AC-15, sabotaj (bl), (bm), (bn), WMS §1 kural 5–6, §3.4, §4 |
| F-2 (DÜŞÜK) | (D) yalnız "onay Refused" dalıdır: filtre `Version` + `Status == Refused`, `PriorResolution = Resolution`, `Resolution = null`; onay zaten `IntegrityError` ise yazım yok, `PriorResolution` korunur, 409 `WMS_POSTING_INTEGRITY_ERROR`; "başka `TransactionId` ile Posted" kaldırıldı (aynı `PostingKey` tek harekete bağlı); satır 11'in kaynağı yalnız Refused; CAS tablosuna (D) satırı; durum diyagramı satırlarla eşitlendi (Refused → IntegrityError satır 11 / 18 / 20; Pending → IntegrityError satır 9 / 10 / 12 / 21) | §8.3 diyagram, adım 4 (D), satır 11, CAS tablosu, AC-15, AC-64, sabotaj (bo), WMS §5.1, §6.1 |
| F-3 (DÜŞÜK) | AC-15: (B) dalı = 1 / 0 / ilk yazımın olayı (AC-54 ile aynı); (A) tükenmesi sonuç yazımında 0 / 1 / ilk yazımın olayı, kilit alımında 0 / 0 / 0; aynı ayrım (C1) / (E) için (kilit alımında 0 / 0 / 0) | AC-15 |
| Son tutarlılık geçişi | Yeni alanlar ve kontrol: §4.1–§4.6 alan / index listeleri ↔ §8.1 ↔ akış tablosu (29 satır; anahtar olayları satır 1 / 7 / 13 / 14 / 16 / 22 / 23 / 24 / 27, kontrol satır 29) ↔ CAS tablosu ↔ §12 ↔ §21 (her komutta "tekrar 0" ve anahtar olayı) ↔ AC-4 / AC-15 ↔ WMS taslağı; tablo çizimi betikle | ilgili bölümler |

#### 19.1.14 Bağımsız inceleme 11 — kararlar ve düzeltmeler (hedef ed521029f, oturum 708cc355, KOŞULLU, ENGEL ve ORTA yok; sahip kararı Beste 2026-10-26: üç düşük bulgu da kapatılır)
| Bulgu | Karar / düzeltme | Yer |
|---|---|---|
| F-1 (DÜŞÜK) | Replay = 200 + kaydın güncel hâli + (onayda) isteğin kendi onayının `confirmationSeq`'i (anahtar olayı `ConfirmationRecorded.Data.seq`); işlem 1'den sonraki her onay cevabı `seq` taşır: 201 / replay 200 `confirmationSeq`, 422 / 503 / 409 `details.seq`; "ilk sonuç" / "aynı gövde" ifadeleri "güncel hâl" ile eşitlendi | §8.1, §8.3 adım 4, akış satır 29, §13.1, §13.2, AC-4, sabotaj (bq), WMS kural 5, onay satırı, onay cevabı notu |
| F-2 (DÜŞÜK) | (a) Yaratma anahtarı kullanıcı kapsamında: tekil kısmi `(TenantId, LegalEntityId, CreatedBy, IdempotencyKey)` (görev ve rol; `QuarantineIndexConfiguration.cs:51`, `ExpiryIndexConfiguration.cs:64`, `RecallIndexConfiguration.cs:84` doğrulandı). (b) Anahtar olaylarına `RequestOp` (WMS `operationId`); olay index'leri ve toplu arama `RequestOp`'u içerir — toplu arama yalnız aynı operasyonun olaylarını alır; başka operasyonda aynı anahtar ayrı anahtardır | §4.1, §4.3, §4.4, §4.6, §8.1, §12, §13.1, §13.24, AC-4, sabotaj (br), (bs), WMS kural 5 |
| F-3 (DÜŞÜK) | (a) Yaratmada **her** yinelenen kayıt hatası (anahtar index'i, rolün canlı `LocationId` index'i, görevin seri slot index'i) önce anahtarı yeniden arar: bulunursa replay 200 / 400, bulunmazsa 409 `WMS_LOCATION_ROLE_EXISTS` / 422 `WMS_QUANTITY_EXCEEDS_OPEN`; AC-4'e eşzamanlı rol yaratma vakası. (b) AC-15'e "(D) tükenmesi = 0 / 1 / ilk yazımın olayı"; AC-64'e (D) tükenme vakası (iii) | §4.4, §4.6, §8.1, CAS tablosu, akış satır 29, §13.24, AC-4, AC-15, AC-64, sabotaj (bp), (bt), WMS rol yaratma satırı |
| Son tutarlılık geçişi | Değişen yerler ↔ §4.1 / §4.3 / §4.4 / §4.6 ↔ §8.1 ↔ CAS tablosu ↔ §12 ↔ §13 ↔ AC-4 / AC-15 / AC-64 ↔ WMS taslağı; tablo çizimi betikle | ilgili bölümler |

#### 19.1.15 Bağımsız inceleme 12 + Codex incelemesi — kararlar ve düzeltmeler (hedef 1b4350fe6; bağımsız inceleme 12 oturum 771fbbcf, KOŞULLU; Codex incelemesi sahip tarafından verildi, CONDITIONAL PASS, CT doğruladı; sahip kararı Beste 2026-10-26: hepsi kapatılır)
| Bulgu | Karar / düzeltme | Yer |
|---|---|---|
| 12 F-1 (DÜŞÜK) | `EntityBase`'in gerçek alanları yazıldı (`EntityBase.cs`: `Id`, `TenantId`, `LegalEntityId`, `CreatedAt`, `UpdatedAt`, `Version` int, `IsDeleted`, `DeletedAt`; kullanıcı alanı yok). `WarehouseTask.CreatedBy` ve `WarehouseLocationRole.CreatedBy` (uuid, isteği yapanın `ActorUserId`'si, yalnız yaratmada yazılır, güncellenmez) — emsal `QuarantineEntities.cs:47` doğrulandı; §4.1 `Version` tipi `EntityBase` ile eşitlendi (int) | §4, §4.1, §4.4 |
| 12 F-2 (DÜŞÜK) | Seri slot: farklı anahtarlı eşzamanlı yaratma → 422; aynı kullanıcı + aynı anahtar + aynı gövde → kaybeden 200 replay (§8.1). AC-52 "farklı anahtar" koşulu; AC-4'e özdeş eşzamanlı seri yaratma vakası | §4.6, AC-4, AC-52, sabotaj (bv) |
| 12 F-3 (DÜŞÜK) | `RequestOp`'un hedef başı tekil index'teki yeri ölçülür: AC-4 "aynı hedef, iki işlem" (bulkAssign → updatePutawayTask, aynı görev ve anahtar → düzenleme yazılır); (br)'nin beklenen sonucu 400 `IDEMPOTENCY_KEY_REUSED` | AC-4, sabotaj (br), (bu) |
| 12 F-4 (DÜŞÜK) | WMS: `WMS_CONFLICT` / `WMS_POSTING_INTEGRITY_ERROR` / `WMS_CONFIRMATION_IN_FLIGHT` / `WMS_CONFIRMATION_CONFLICT_RETRY` satırlarına onay ucunda `details.seq`; retry dönüşü `ConfirmPutawayTaskResult`; şema §3.9; §3.4 `RequestOp`'u kabloya yazılmayan alanlara ekledi; AC-4 409'daki `details.seq`'i ölçer | WMS §3.4, §3.9, §4, §5.1; AC-4 |
| Codex P1 (ÖNEMLİ) | `pack_approval.slices()` None döndürüyordu. §23 aracın kurallarına göre yeniden yazıldı: tek tablo, satır kimliği yalnız `DL-<sayı>` (DL-1 = eski DL-1a, DL-2 = eski DL-1b, DL-3…DL-7 = aynı numara — DL-3'e ayrıca `manifest` (§14.4) başlığı eklendi, eski DL-3 = 8.15, 9, 11, 24 —, DL-8 = eski DL-2 INT-WMS), 3. sütun yalnız paketteki başlık adları (küçük harf, numarasız), AC kapsamı 4. sütunda; virgül içeren dört başlık (§8.8, §8.15, §8.16, §11.1) virgülsüz yazıldı; CT dondurma adımı §23 dışına (§19.7) taşındı; paket genelindeki atıflar güncellendi. §19.1.3–§19.1.14 kayıtları o günkü eski adlarla kalır (eşleme bu satırda). **Dikkat (bağımsız inceleme 13 F-1):** §19.1.3 F-6'daki "DL-1" o günün **bütün S1 backend** dilimidir (yeni DL-1 + DL-2'ye karşılık gelir); yeni DL-1 ile karıştırılmamalıdır. Kanıt: `slices()` → `['DL-1', 'DL-2', 'DL-3', 'DL-4', 'DL-5', 'DL-6', 'DL-7', 'DL-8']`; kabul şartı §18 #15a | §5, §8.1, §8.8 / §8.15 / §8.16 / §11.1 başlıkları, §18, §19.7, §23, AC-1, AC-19, AC-58, AC-59, AC-60, sabotaj (ah), (ai) |
| Codex P2 | Posting eşlemesi: kesin 4xx → Refused (422 dışı 4xx kuralları korunur); commit sonucu kesin bilinmeyen her sonuç (her 5xx, zaman aşımı, ağ hatası, iptal, beklenmeyen istisna) → onay Pending, 503 (`details.reason = OutcomeUnknown`), sonraki deneme / sweeper önce `PostingKey` aramasıyla uzlaştırır; AC-5'e "commit öncesi 500" ve "commit sonrası 500" + 502 / 504 / istisna vakaları | §8.3 adım 4–5, akış satır 5 / 7, CAS tablosu, §13.3, AC-5, sabotaj (bw), (bx), WMS §3.2, §4, §5.2 |
| Son tutarlılık geçişi | Değişen yerler ↔ §4 / §4.1 / §4.4 / §4.6 ↔ §8.1 / §8.3 ↔ CAS ↔ §13 ↔ AC-4 / AC-5 / AC-52 ↔ §18 ↔ §23 ↔ WMS taslağı; tablo çizimi betikle; `slices()` çıktısı | ilgili bölümler |

#### 19.1.16 Bağımsız inceleme 13 — kararlar ve düzeltmeler (hedef 53afff318, oturum 075041e2, KOŞULLU, ENGEL ve ORTA yok; sahip kararı Beste 2026-10-26: üçü de kapatılır)
| Bulgu | Karar / düzeltme | Yer |
|---|---|---|
| F-1 (DÜŞÜK) | §19.1.15 eşleme satırına iki not: §19.1.3 F-6'daki "DL-1" o günün bütün S1 backend dilimidir (yeni DL-1 + DL-2); yeni DL-3'e `manifest` (§14.4) eklendi, eski DL-3 = 8.15, 9, 11, 24. Geçmiş kayıtların metni değişmedi | §19.1.15 |
| F-2 (DÜŞÜK) | İptal (`OperationCanceledException`) commit sonucu bilinmeyen sonuçlar listesinde her yerde: akış satırı 7, CAS satırı, §13.3, AC-5 (iptal vakası), WMS §3.2; sabotaj (bx) iptali de kapsar | §8.3, §13.3, AC-5, sabotaj (bx), WMS §3.2, WMS §4 (onay ucu 503 listesi), WMS §5.2 |
| F-3 (DÜŞÜK) | **Sahip kararı:** güvenli tercih kalır (bilinmeyen sonuç → Pending, deneme sınırı yok, M-5). Bilinçli kabul §8.3 adım 5'te: belirlenimci 5xx / istisnada görev kilitli kalır (yeni onay / iptal / kısa kapatma 409 `WMS_CONFIRMATION_PENDING`). Çıkış: `Warehouse:Putaway:LongPendingMinutes` (öneri 30) aşılınca türetilmiş `PostingPendingLong` + "Uzun süredir bekliyor" rozeti; kayıtlar S3 İstisna Kutusu'na (AC-36); çözüm = stok tarafı hatanın giderilmesi + "Yeniden dene" | §4.1, §8.3 adım 5, §8.15, §11.4, AC-46, sabotaj (by), WMS §3.1 |
| Son tutarlılık geçişi | Değişen yerler ↔ §4.1 ↔ §8.3 ↔ CAS ↔ §8.15 ↔ §11.4 ↔ §13 ↔ AC-5 / AC-46 ↔ WMS; tablo çizimi betikle; `slices()` → DL-1…DL-8 | ilgili bölümler |

#### 19.1.17 Bağımsız inceleme 14 — kararlar ve düzeltmeler (hedef ac2971beb, oturum c4e41a9c, KOŞULLU, ENGEL ve ORTA yok; sahip kararı Beste 2026-10-26: üçü de kapatılır)
| Bulgu | Karar / düzeltme | Yer |
|---|---|---|
| F-1 (DÜŞÜK) | Tek değerli `IntegrationFlag` için görev düzeyinde öncelik: `PostingIntegrityError` > `PostingPendingLong` > `PostingPending` > `PostingRefused` > `None` (görevin onayları arasında en yüksek öncelikli durum gösterilir). AC-46'ya Pending + `IntegrityError` → "Bütünlük hatası" vakası | §4.1, §8.15, AC-46, sabotaj (bz), WMS §3.1, §3.2 |
| F-2 (DÜŞÜK) | S3 istisna türü bayrakla aynı ad: `PostingPendingTooLong` → `PostingPendingLong`; tür aynı `LongPendingMinutes` eşiğine bağlı | §8.3 adım 5, WMS §9 |
| F-3 (DÜŞÜK) | Açılış doğrulaması: `LongPendingMinutes > 0` ve `LongPendingMinutes × 60 > InFlightLeaseSeconds`, sağlanmazsa host başlamaz (§4.2'deki komşu kuralın biçimi); AC-46'da yanlış ayarla host başlamaz | §4.2, §8.3 adım 5, AC-46, sabotaj (ca) |
| Biçimsel (inceleme 14 notu) | §19.1.16 F-2 satırının "Yer" sütununa WMS §4 (onay ucu 503 listesi) ve WMS §5.2 eklendi | §19.1.16 |
| Son tutarlılık geçişi | Değişen yerler ↔ §4.1 / §4.2 ↔ §8.3 adım 5 ↔ §8.15 ↔ AC-46 ↔ sabotaj ↔ WMS §3.1 / §3.2 / §9; tablo çizimi betikle; `slices()` → DL-1…DL-8 | ilgili bölümler |

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
- **İnceleme 02 için ölçülenler (`5f7833253`, servis kodu `47a2c1cb9`'dan beri değişmemiş):**
  - `InternalMovementPostingService.cs:87-99` — parse, parmak izi, **sonra** `FindByIdempotencyKeyAsync`; işlenmişse `Replay` (`:216-226`:
    parmak izi farklı → 400 `IDEMPOTENCY_KEY_REUSED`) — PRODUCT-MASTER / LOCATION doğrulamasından **önce**.
  - `IInventoryLedgerRepository.cs:16` — `FindByIdempotencyKeyAsync`.
  - `QuarantineLedgerReads.cs:108-120` — lokasyondaki Active seriler, seri **numarası**, alım sırası; `QuarantinePorts.cs:50-55` port.
  - `StockActionHandlers.cs:175-200` — açık tutma / açık bloke serilerinin dışlanması (ASSUMPTION-0175-Q4-02).
  - `InventoryLotDirectory.cs:161` — `LotIdentityPolicy.NormalizeSerial`; `:275-300` (`:295`) — `LOCATION_TRANSFER`'da seri depo / lokasyonu hedefe.
  - `MovementMasterDataValidator.cs:144-149` — Gsku'da seri bayrağı `getProduct`'tan (OD-10).
  - `ForwardCallerHeadersHandler.cs:21-35` — Authorization yalnız `HttpContext`'ten.
  - `RecallQuarantineSweeper.cs:19` — `MaxDeferredAttempts = 10` (0177 sınırlı sweeper).
  - **İnceleme 03 için ölçülenler (`367e8ff78`):** `RecallQuarantineSweeper.cs:72-76` — her kayıt için `CreateScope()`,
    `ITenantContext.SetScope(TenantId, LegalEntityId, RequestedBy)`, `IPostingActorName.Use(...)`, `ICorrelationContext.Set(Guid.NewGuid())`;
    `InventoryLedgerRepository.cs:100-103` — `FindByIdempotencyKeyAsync` `TransactionScope()` (kiracı + şirket) ile süzer;
    `InternalMovementPostingService.cs:216-226` — `Replay` (parmak izi farklı → 400 `IDEMPOTENCY_KEY_REUSED`).
  - **İnceleme 04 için ölçülenler (`13d343c9a`):** `…/Infrastructure/DependencyInjection.cs:51` — upstream zaman aşımı
    `UpstreamServices:TimeoutSeconds` (yoksa 5 sn), `…/Api/appsettings.json:35` `"TimeoutSeconds": 5`; `IInventoryLedgerRepository.cs:11-12` —
    append-only (yalnız `PostAsync` yazar); `RecallQuarantineSweeper.cs:65` — `Limit(200)`, `:82` kayıt başına `catch`.
  - **İnceleme 05 için ölçülenler (`1f185d6a4`):** `InventoryLedgerRepository.cs:173` — `session.WithTransactionAsync` (commit sunucu
    tarafında); `QuarantineServiceCollectionExtensions.cs:36` — `TimeProvider.System` (0175'in uçuş payı saati, testler kendi saatini verir).
  - **Bağımsız inceleme 05 için ölçülenler (`890d21c92`, servis kodu değişmemiş):** `IInventoryLedgerRepository.cs:20` — `IsReversedAsync`
    (bool); `InventoryLedgerRepository.cs:110-113` — `ReversesTransactionId == id` filtresi; `InventoryTransaction.cs:71` —
    `ReversesTransactionId` (ters bağ okuma anında türetilir, `:9`); `InventoryLedgerRepository.cs:173` — `WithTransactionAsync` çağrısı;
    `QuarantineLedgerReads.cs:118` — seri numarası dönüşü. **Ölçülmedi:** sürücünün `WithTransactionAsync` yeniden deneme penceresinin
    (dokümantasyonda varsayılan 120 sn) iptal belirteciyle sınırlanıp sınırlanmadığı — geliştiricinin ilk ölçümü; sınırlanmıyorsa
    `InFlightLeaseSeconds` bu pencere + 30 sn'den küçük olamaz.
  - **Bağımsız inceleme 06 için ölçülenler (`d86c1123e`):** `LedgerImmutabilityArchitectureTests.cs:28-36` — `IInventoryLedgerRepository`'de
    `Find` / `Query` / `List` / `Is` dışındaki tek üye `PostAsync`; `InventoryIndexConfiguration.cs:49-55` — `ReversesTransactionId` üzerinde
    tekil kısmi index; registry satırı `module-id-registry.md:304` (`47a2c1cb9`'daki :298 ile aynı içerik).
  - **Bağımsız inceleme 08 için ölçülenler (`58c2cd6e6`):** `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/CentralAuditBehavior.cs:8-9`
    (başarılı işaretli komut tam bir olay, başarısız / istisna hiç), `:36` (`Begin` işleyiciden önce, ad komut türünden), `:59-63` (`!Succeeded` →
    `AbandonAsync`); `CentralAuditRecorder.cs:11-12` (aynı komutun sonraki işlemindeki enlist no-op), `:41-46` (commit olmuş başarısız
    komutun satırı `Abandoned`, iletilmez), `:95` (meta veri `declaration.CentralAudit`); `InventoryLedgerRepository.cs:181`
    (`CentralAuditTransaction.EnlistAsync`); `Response.cs:15,27` (`IsSuccessful`).

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

**Beste (sahip) — açık S2 kararı:**
- **W-6 (plan §4):** WAREHOUSE-OUTBOUND'a `Item` seviyesi eklensin mi? Yalnız S2'yi etkiler; AC-33 bu karara bağlı. MVP-6'nın
  uyumu (Q-M6-6) kararın ardından sorulur.

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
- **Q-M6-6:** W-6 kararı `Item`'ı açarsa MVP-6 okuyucusu (`SkuLevels` bugün 3 değer, `FrozenReadSchema.cs:11`) buna uyar mı?
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

### 19.7 CT dondurma adımı — WMS-BUNDLE S1 (dev dilimi değil; Codex incelemesi P1)
Teslim dilimleri tablosu (§23) yalnız dev dilimlerini (`DL-n`) taşır; CT'nin tek yazıcı adımı burada ayrı tutulur (eski adı `CT-FREEZE`).

| Adım | Ne | Ne zaman | Kabul |
|---|---|---|---|
| CT dondurma | `docs/analysis/contracts/wms-bundle.openapi.yaml` + `docs/analysis/contracts/README.md` satırı — CT'nin tek yazıcı adımı | DL-2 kabulünden sonra | AC-60 (DL-2 testlerinin dondurulmuş dosyaya karşı yeniden koşusu) — döngü yok |

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
- **FU-0178-KEY-01:** `PostingKey`'in tahmin edilemez yapılması (işlem 1'de bir kez üretilen rastgele parça, onaya yazılır, her denemede
  aynen) — başka bir `postMovement` çağıranının anahtarı önceden harcayıp onayı `IntegrityError`'a düşürmesini engeller (inceleme 03 F-2).
- **FU-0178-SERIAL-01 (MOD-0182 ile):** seri kontrollü SKU'da kısmi yerleştirme — okutulan seriler onay gövdesine girer, kaynak kovanın
  serileriyle doğrulanır (F-1).
- **FU-0178-PKS-01 (sahip onaylı `.antigravity` adımı):** `warehouse.*` namespace'inin PKS-001 §4 sahiplik tablosuna (MOD-0178) yazılması (F-14).
- **FU-0178-DC-01 (domain-config sahibi):** `execution/domains/supply-chain-execution/domain-config.md` `:36` ve `:72` W-2 ile hizalanır —
  0178 `Diten.InventoryService` içinde ayrı bölümde, stoğa süreç içi `InternalMovementPostingService` ile yazar (HTTP değil); sınır
  (ikinci bakiye yok, defter koleksiyonuna doğrudan yazmaz) aynı (F-15).
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
`inventory-merkezi-iletim | b | işaret | ICentralAuditDeclaration`; `RequireEnlistment = true`. **Kayıt biçimi ve sahip kararı (§8.8;
bağımsız inceleme 09 F-1):** BL-501 kaydı **ad ve gövde taşımaz** — komut başına sabit `Category` / `Operation` (Platform enum adları) /
`EntityType` / `SourceModule`, `EntityId` (`CentralAuditEntityId`), `RequestType` (= komut sınıfının adı) ve aktör / kiracı / şirket /
korelasyon / zaman. Bu yüzden tablodaki **"olay adı" = `RequestType` + `Operation`**'dır; önceki / sonraki ya da değişen alanlar **yalnız
yerel zaman çizelgesinin `Data`'sındadır** (görev: `wms_task_events`, rol: `wms_location_role_events`; §4.3), aynı işlemde yazılır, silinmez.
Merkezi kayıt yalnız **başarılı ve yazan** komutta iletilir (1; çökmede `Unconfirmed`); başarısız cevapta iletilen 0 (satır varsa
`Abandoned`), yazmadan başarılı komutta (tekrar / no-op) 0 (§8.8). **Tekrar (bağımsız inceleme 10 F-1):** her komutta aynı anahtar + aynı
parmak izi → 200 replay, aynı anahtar + farklı parmak izi → 400; ikisi de yazmaz, enlist etmez, yerel olay bırakmaz (0 / 0 / 0) — anahtar
If-Match'ten önce aranır (§8.1); anahtar yaratmada kayıtta, diğer komutlarda aşağıdaki "anahtar olayı"nda (§4.3). K2 = başarılı komutun merkezi kaydı veriyle aynı işlemde (test: denetim
yazımı enjekte hata → komut geri alınır, görev değişmez).

**Ortak değerler (sahip kararı Beste 2026-10-26 — Category, ModuleCode ve aşağıdaki `Operation` eşlemesi kabul):** `Category = MasterData` — Platform `AuditCategory`'de envanter / depo değeri yok, InventoryService'in bütün
modülleri bunu kullanır (`…/Application/Common/InventoryCentralAudit.cs:12-22`); ayrı kategori Platform enum değişikliğidir, 0178 açmaz.
`SourceModule` = §14.4 manifest `ModuleCode` (`PUTAWAY-PICKING-PACKING`). Bildirim yardımcısı 0178'in kendi dosyasıdır
(`…/Features/Warehouse/WarehouseCentralAudit.cs`, `InventoryCentralAudit.For*` deseni). `Operation` InventoryService emsaline göre:
yaratma `Create`, düzenleme `Update`, silme `Delete`, atama / üstlenme `Assign`, stok hareketini yürüten ya da sonuçlandıran komut
`Execute` (emsal `ManualLotMigration`, `QuarantineDecision`), durum kapatan komut `LifecycleTransition` (emsal `RecallCase`,
`QuarantineHold`). Her değer `CentralAuditPlatformValues.IsKnown` ile ölçülür (§5 beşinci seam).

| Komut (= `RequestType`) | `Operation` | `EntityType` | `EntityId` kaynağı | Yol | Merkezi kayıt (iletilen) | Yerel zaman çizelgesi: `Kind` → `Data` (önceki / sonraki ya da değişen alanlar) | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|---|---|
| `CreatePutawayTaskCommand` | `Create` | `WarehouseTask` | cevaptaki `taskId` | b (K2) | 1; aynı `IdempotencyKey` tekrarı 0 | `Created` (+ `SuggestionApplied`) → After: Number, kaynak kova, PlannedQuantity, ToLocationId, Assignee, Priority, Origin | — |
| `UpdatePutawayTaskCommand` | `Update` | `WarehouseTask` | komuttaki `taskId` | b | 1; tekrar 0 | `Updated` (anahtar olayı; + `SuggestionApplied`) → Before / After: ToLocationId, PlannedQuantity, Assignee, Priority, Note | — |
| `BulkAssignPutawayTaskCommand` | `Assign` | `WarehouseTask` | yok — toplu, `null` (`QuarantineCommands.cs:116` emsali) | b | 1 (en az bir görev yazıldıysa); hiçbiri yazılmadıysa 0; tekrar 0 | yazılan her görevde `Assigned` (anahtar olayı) → Before / After: AssigneeUserId; satır sonuçları yalnız cevapta | — |
| `ClaimPutawayTaskCommand` | `Assign` | `WarehouseTask` | komuttaki `taskId` | b | 1; tekrar 0 | `Claimed` (anahtar olayı) → Before / After: AssigneeUserId (null → kendisi) | — |
| `ConfirmPutawayTaskCommand` | `Execute` | `WarehouseTask` | komuttaki `taskId` | b (K2) | 1 yalnız Posted (201); Refused / Pending / IntegrityError / 409 / 503 → 0 (işlem 1 ya da ön arama yazımı commit olduysa satır `Abandoned`, olmadıysa satır yok); tekrar (AC-4) 0; işlem 1 sonrası çökme → 1 `Unconfirmed` | `ConfirmationRecorded` (anahtar olayı) + `ConfirmationPosted` / `ConfirmationRefused` / `ConfirmationIntegrityError` (+ `ConfirmationSuperseded`, + `Completed`) → onay `seq`, Quantity, From / To, PostingKey, TransactionNumber, kapatılan `seq` listesi, Error\* | — (hareketin defterdeki kendi izi defterin işi; defterin enlist'i bu komutun satırına no-op) |
| `RetryPutawayConfirmationCommand` | `Execute` | `WarehouseTask` | komuttaki `taskId` | b (K2) | 1 yalnız Posted (yeni posting ya da replay); diğer sonuçlar 0 (kilit alımı commit olduysa `Abandoned`); kilit reddi 409 satır yok; tekrar 0; kilit alımı sonrası çökme → 1 `Unconfirmed` | `ConfirmationRetried` (anahtar olayı) + `ConfirmationPosted` / `ConfirmationRefused` / `ConfirmationIntegrityError` (+ `ConfirmationSuperseded`, + `Completed`) → onay `seq`, Attempts, TransactionNumber, kapatılan `seq` listesi, Error\* | — |
| `ReconcilePendingConfirmationCommand` (iç) | `Execute` | `WarehouseTask` | komuttaki `taskId` | b (K2) | 1 yalnız Posted (aktör = onayın `ConfirmedBy`'ı); KeyReused / HolderReversed / savunma dalı → 0 (`Abandoned`); (B) / 0-kayıt → 0 (satır yok); `NotCommitted` → komut gönderilmez | `ConfirmationReconciled` (+ `ConfirmationSuperseded`, + `Completed`) ya da `ConfirmationIntegrityError` → onay `seq`, TransactionNumber, kapatılan `seq` listesi, `reason`, `holder` | — |
| `ResolvePutawayIntegrityCommand` | `LifecycleTransition` | `WarehouseTask` | komuttaki `taskId` | b (K2) | 1; 409 retler 0 (satır yok); tekrar 0 | `IntegrityResolved` (anahtar olayı; + `ConfirmationDeviation`, + `ConfirmationSuperseded`, + `Completed`) → onay `seq`, IntegrityResolution, gerekçe, `holder`, sapma alanları, kapatılan `seq` listesi; görev kapanışı yok | — |
| `ClosePutawayTaskShortCommand` | `LifecycleTransition` | `WarehouseTask` | komuttaki `taskId` | b (K2) | 1; ön arama geç commit (409) → 0 (`Abandoned`); tekrar 0 | `ClosedShort` (anahtar olayı) → Planned, Confirmed, ShortCloseReason; 409'da hedef onayda `ConfirmationIntegrityError` | — |
| `CancelPutawayTaskCommand` | `LifecycleTransition` | `WarehouseTask` | komuttaki `taskId` | b (K2) | aynı | `Cancelled` (anahtar olayı) → Before Status, CancelReason; 409'da aynı | — |
| `MarkPutawayConfirmationReturnedCommand` | `LifecycleTransition` | `WarehouseTask` | komuttaki `taskId` | b (K2) | aynı | `ConfirmationReturned` (anahtar olayı) → onay `seq`, Resolution `ReturnedToSource`, ResolutionReason; 409'da aynı | — |
| `CreateWarehouseLocationRoleCommand` | `Create` | `WarehouseLocationRole` | cevaptaki `roleId` | b | 1; tekrar 0 (anahtar kayıtta, §4.4) | `wms_location_role_events` `Created` → After: LocationId, Roles, AcceptedStorageConditions, Note | — |
| `UpdateWarehouseLocationRoleCommand` | `Update` | `WarehouseLocationRole` | komuttaki `roleId` | b | 1; tekrar 0 | `Updated` (anahtar olayı) → Before / After: Roles, AcceptedStorageConditions, Note | — |
| `DeleteWarehouseLocationRoleCommand` | `Delete` | `WarehouseLocationRole` | komuttaki `roleId` | b | 1; tekrar 0 | `Deleted` (anahtar olayı) → Before: LocationId, Roles, AcceptedStorageConditions | — |
| `BulkDeleteWarehouseLocationRoleCommand` | `Delete` | `WarehouseLocationRole` | yok — toplu, `null` | b | 1 (en az bir kayıt silindiyse); hiçbiri silinmediyse 0; tekrar 0 | silinen her kayıtta `Deleted` (anahtar olayı) → Before: LocationId, Roles, AcceptedStorageConditions | — |
| Pending uzlaştırıcı hosted service (`PendingConfirmationReconcilerHostedService`) | — | — | — | — | — | — | İ2 — kendisi kayıt değiştirmez ve işlem açmaz; yalnız yazılacak bir şey varsa gönderdiği `ReconcilePendingConfirmationCommand` yukarıdaki kurala göre denetlenir |
| S2 `CreateOutboundShipmentCommand`, `CancelOutboundShipmentCommand`, `ConfirmPickTaskCommand`, `CreateHandlingUnitCommand`, `UpdateHandlingUnitCommand`, `ReleaseOutboundShipmentCommand` (ReadyToShip), `MarkOutboundShippedCommand` | S2 revizyonunda (Platform enum adı) | — | — | b (K2) | aynı kural | S2'de yazılır | — (iskelet) |

Kayda yazılmayan alanlar: `ShipTo` adres / alıcı adı (S2, KVKK — yalnız outbound id ve alan adı "değişti" işareti), token, gizli
anahtarlar. Merkezi kayıt zaten gövde taşımaz; bu kural yerel `Data` için de geçerlidir.

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
Numara **kiracı genelinde tekil** `(TenantId, Number)` (RCS-001 §6). **Önek devri (bilgi notu, F-12):** bir tüzel kişilik `Code`'unu
değiştirir ve eski kodu sonra başka bir tüzel kişilik alırsa, sayaç anahtarı `(kiracı, önek, tür)` olduğu için yeni sahip aynı
sayaçtan devam eder — numaralar çakışmaz, ama önek tek başına bugünkü şirketi söylemez; sahip şirket görevin `LegalEntityId`'sidir
(RCS-001 §5.2: kod kimliktir, şu anki sahibi söylemez). RCS-001 §5.4'teki "kısa kod kullanıldıktan sonra değiştirilemez" kuralı ortak
servisle gelir.

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | kabul ölçütleri ve kapsam notu |
|---|---|---|---|
| DL-1 | S1 temel: lokasyon rolleri + saklama koşulu (kalıtım, MOD-0173 `storageConditions` seam'i) + geçici numara sayacı (backend) | `warehouselocationrole`, `warehousetaskevent` (insert-only), indexes (kiracı + şirket önce), repo scope, servis ve kapsam, kayıt kodları, saklama koşulu uyumu (m-3 — s1 engeli), validation rules, audited events, record codes | §4.3'ün yalnız rol geçmişi (`WarehouseLocationRoleEvent`); §5 seam'leri (beşinci seam'de sayı 67 → 71); §12'nin rol / koşul / sayaç satırları; §21'in rol satırları. AC-1, AC-13 (rol uçları), AC-14 (rol uçları), AC-17 (görevsiz kısım: CRUD, tekillik, Receiving + Staging, atalar arası çakışma), AC-59 — görev isteyen hiçbir AC yok (inceleme 02 F-4) |
| DL-2 | S1 görev + onay + sweeper + seri tam miktar (backend; sunucu yanlı ekran kuralları dahil) | owned objects, `warehousetask` (s1 putaway; s2 pick aynı baş kayıt), `taskconfirmation` (gömülü), `warehousetaskevent` (insert-only), görev kaynağı (s1), durum makinesi ve onay algoritması (s1 putaway), karantinadaki ve tutulan stok (s1 kuralı), geri çağırma (recall) — 0178'in tek ek kuralı, hedef bin önerisi (s1), merkezi denetim (e-2 / bl-501) ve yerel gxp izi — sahip kararı (beste 2026-10-26), onay yetkisi ve üstlenme (m-5), e-imza — kapsam dışı (m-1), seri kontrollü sku — yalnız tam miktar (f-1; sahip kararı beste 2026-10-21), backend file convention, validation rules, failure path to verify, authorization convention, audited events | AC-2…AC-12, AC-15, AC-18, AC-19 (taslaktan türetilmiş şemaya karşı), AC-39 (sunucu 409 kilidi), AC-40, AC-41 (aday API'de belge no), AC-43 (sunucu geçiş kuralı), AC-46 (türetilmiş durum / bayrak), AC-13 / AC-14 (görev uçları), AC-47…AC-49, AC-51…AC-58, AC-61…AC-70, AC-16 (numara görev yaratmada üretilir), AC-37, AC-38 (inceleme 02 F-4) |
| DL-3 | S1 ekranlar + manifest + yardım (yalnız ekran yanı) | ekran kuralları (m-4 / s1), layout & shell contract, frontend file contract, manifest, page help (uxh-001) | AC-20, AC-21…AC-24, AC-39 / AC-41 / AC-43 / AC-46 (ekran yanı), AC-42, AC-44, AC-45, AC-50 |
| DL-4 | S2a outbound + tahsis (W-5 sonrası, paket revizyonu ile) | s2 varlıkları (iskelet — s2 revizyonunda kesinleşir), w-5 için gereksinimler (s2 engeli; sahibi mvp-1 ct — bu paket yazmaz), ekran → rota → izin (hepsi scope = tenant; `_layouttenantshell`) | §11.1'in S2 satırları; AC-26, AC-28, AC-33 |
| DL-5 | S2b pick / pack / ReadyToShip / Shipped | olaylar ve taşıma | AC-27, AC-29…AC-32, AC-35 |
| DL-6 | S2 ekranları (compact Outbound, slim PickTasks, Paketleme) | ekran → rota → izin (hepsi scope = tenant; `_layouttenantshell`) | §11.1'in S2 satırları; AC-34 |
| DL-7 | S3 izleme ekranları | ekran → rota → izin (hepsi scope = tenant; `_layouttenantshell`) | §11.1'in S3 satırları; AC-36 |
| DL-8 | INT-WMS Gateway (integration-agent; sıra olarak DL-2'den sonra) | gateway / api routing decision | AC-25 |

## 24. Page Help (UXH-001)

- `PUTAWAYTASKS` → `Help.PUTAWAYTASKS.Purpose`, `.Audience`, `.Step1Title`/`.Step1` … `.Step4` (1: yerleştirilecek stoğu seç,
  2: hedef bin ve atanan, 3: onayla (kısmi olabilir), 4: Pending onayda "Yeniden dene"); `.Immediate` ("Onay stoğu hemen taşır;
  geri alma Stok Hareketleri'nde ters kayıttır"); ilgili sayfalar `Help.PUTAWAYTASKS.Related.WAREHOUSELOCATIONROLES`, stok defteri.
- `WAREHOUSELOCATIONROLES` → `Purpose`, `Audience`, `Step1..3` (lokasyon seç, rol ver, mal kabul lokasyonlarını doğrula);
  ilgili `Related.PUTAWAYTASKS`.
- Kiracı modülü → 7 dil. **Tur yok** (beş akıştan biri değil).

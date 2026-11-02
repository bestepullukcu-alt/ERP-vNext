---
id: MOD-0173-FU03
name: Inventory Lot & Location Reservation
parent: MOD-0173
parent_name: Inventory Ledger & Valuation
domain: supply-chain-execution
service: Diten.InventoryService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
approval_status: pending
status_note: "draft — W-5 (sahip kararı 2026-10-21: lot / lokasyon rezervasyonu INVENTORY'ye eklemeli, MVP-1 CT yapar, MVP-5 S2'den önce). MOD-0178 S2'nin (DL-4) tek INVENTORY engeli (MOD-0178 §7, §8.10, §19.1.1 Q-01, FU-0178-W5). Backend + sözleşme; ekran yok. 2026-11-02: sahip kararları Q-02, Q-03, Q-04, Q-06, Q-08 (Beste, \"hepsi öneri gibi\"), CT kararları CT-01, CT-02, CT-03, CT-04 (MVP-1 CT) ve CT-07 sahip izni (Beste, XMC-001 LotHeldStock.cs) verildi — §19.6. Hâlâ draft: §18 \"Acceptance criteria ölçülebilir ve sabotajlı\" maddesi karşılanmıyor (34 AC'nin 23'ünde sabotaj yok); CT-07 izin metniyle paket değişikliğinin ifade farkı CT teyidi bekliyor (§19.6). Açık sorular Q-01, Q-05, Q-07 (§19.4); açık CT kararları CT-05, CT-06 (§19.5). Kod yazılmaz: status ready-for-dev + CT-APR-02 bağımsız inceleme + CT-APR-01 onay satırı gerekir."
owner: supply-chain-execution / mvp-1 control-tower
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-02
target: 2026-11-30
form_field_count: 0
dependencies: [MOD-0173, MOD-0173-FU02, MOD-0174, MOD-0175, MOD-0176, MOD-0177, INVENTORY-BUNDLE-v1.5.0, LOCATION-v1.1.0, BL-501 (central audit)]
---

# MOD-0173-FU03 — Inventory Lot & Location Reservation

> **Kimlik (DCP-002) — ölçüldü 2026-11-02, dal `chore/ct/mod0178-dl1-accept-01` @ `703cbaec7`:**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0173-FU03 --name "Inventory Lot & Location Reservation" --parent MOD-0173`
> → çıkış **0** ("OK MOD-0173-FU03: proven against Blueprint/registry"). Ebeveyn `--check-id MOD-0173 --name "Inventory Ledger & Valuation"`
> → çıkış 0. Keşif ve kimlik gerekçesi §19.1'de. **Registry satırı bu paketle yazılmaz** — satırı CT ekledi (CT-01, `2265cce90`, §19.6). **Kanonik ad** "Inventory Lot & Location Reservation" (CT kararı 2026-11-02); kayıt satırındaki "Inventory Lot / Location Reservation" yazımını CT düzeltecek.
>
> **Yetki:** bu paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`. Ebeveyn paket
> `MOD-0173-inventory-ledger-valuation.md` §19 ASSUMPTION-0173-12…17 (SKU düzeyinde rezervasyon tasarımı) bu paketin **tabanıdır**;
> bu paket onları **genişletir**, hiçbirini geri almaz. Çelişki çıkarsa ebeveyn paket kazanır ve fark CT'ye sorulur.
>
> **Sözleşme:** yalnız eklemeli. INVENTORY-BUNDLE 1.5.0 (`docs/analysis/contracts/inventory-bundle.openapi.yaml`, `x-status: FROZEN`,
> `x-owner: MOD-0173`) → **1.6.0** (DL-1) ve **1.7.0** (DL-2) önerisi (§8.8). Mevcut yol, parametre, alan, tür, enum değeri, zorunlu
> liste ya da durum kodu değişmez.

## 1. Module Summary

Bugünkü rezervasyon (INVENTORY `createReservation`, `ReservationRequest`) yalnız **SKU düzeyindedir**: depo, lokasyon ve lot taşımaz
(ebeveyn ASSUMPTION-0173-12; `InventoryReservation` varlığı; `ReservationPositionOperations` — tek `inventory_sku_positions` belgesi).
Depo toplama akışı (MOD-0178 S2) bir outbound satırına **belirli bir lotu belirli bir depoda** ayırmak, o stoğu bin'den staging'e taşımak
ve sevkiyatta (MVP-6, MOD-0183 `GOODS_ISSUE_SALES`) **o rezervasyonu tüketmek** zorundadır. Bu paket bunu INVENTORY'ye eklemeli olarak
getirir:

1. **Kapsamlı rezervasyon** — `ReservationRequest` isteğe bağlı `warehouseId` ve `lotId` alır (lot depo ister). Kapsam üç düzeyde
   korunur: SKU, SKU + depo, SKU + depo + lot (§8.2). Lokasyon (bin) düzeyi v1'de yok (Q-02).
2. **Rezervasyon kimliğiyle tüketim** — `postMovement` isteğe bağlı `reservationId` alır; adı verilen rezervasyon aynı işlemde
   tüketilir; ayrılmış stoğu başkası çıkaramaz (§8.3).
3. **Sahibinin yer değiştirmesi** — aynı depo içindeki `LOCATION_TRANSFER` (bin → staging) rezervasyonu düşürmez (§8.4).
4. **FEFO kancası** — başka outbound'lara ayrılmış lotların FEFO'da nasıl sayılacağı **açık soru** (Q-01, MOD-0176 ile ortak karar);
   bu paket yalnız okuma portunu verir (DL-3, koşullu) (§8.5).

Hedef kullanıcı yoktur (ekran yok): tüketiciler MOD-0178 (süreç içi port, §8.7), MOD-0183 (HTTP `postMovement`), ileride MOD-0172 ATP.
Kiracı + şirket kapsamlıdır (`TenantId`, `LegalEntityId`, W-0173-02). Servis `Diten.InventoryService`, port **5067** (AGENTS.md §3).

## 2. Ownership and Boundaries

**Kapsamda (MOD-0173 SoR'unun genişlemesi):**
- `InventoryReservation`'a kapsam alanları (`WarehouseId`, `LotId`) ve kapanış nedeni (`ClosedReason`).
- Kapsam pozisyonu projeksiyonu `InventoryReservationPosition` (depo ve depo + lot düzeyi; SKU düzeyi mevcut `InventorySkuPosition`'da kalır).
- `postMovement`'ta `reservationId` ile tüketim ve kapsam bekçileri; `InventoryTransaction.ReservationId`.
- `getReservation` okuması; `getAvailability`'ye `lotId` parametresi ve kapsam rakamları.
- Süreç içi rezervasyon portu (`IInventoryReservationPort`, §8.7) — MOD-0178'in tek rezervasyon kapısı.
- INVENTORY-BUNDLE 1.6.0 / 1.7.0 eklemeli değişiklikleri ve hata kodları.

**Kapsam dışı / sahibi başka:**

| Şey | Sahibi | Bu paket nasıl dokunur |
|---|---|---|
| Tahsis kararı (hangi lot, hangi bin), pick görevi, outbound | MOD-0178 (S2) | dokunmaz; port verir. Tahsis INVENTORY'de **yapılmaz** |
| FEFO sırası ve FEFO kancası (`FefoCandidateReader`, `FefoOrderRule`, `ExpiryPostingPolicy`) | MOD-0176 | dokunmaz; DL-3 yalnız okuma portu verir (Q-01). 0176 değişikliği 0176 paket revizyonudur (XMC-001) |
| Satış çıkışını kimin yazdığı (W-4), sevkiyat satırı → `reservationId` eşlemesi | MOD-0183 (MVP-6) | dokunmaz; `MovementRequest.reservationId` sunar. 0183 revizyonu XMC-001 bağımlılığı (§7) |
| Karantina / bloke kararları, `QUARANTINE_STOCK_RESERVED` | MOD-0175 | dokunmaz; ölçülen eşlemeye güvenir (§8.6) |
| Lot kaydı ve durumu | MOD-0174 | okur (`LotPostingRules`, `Lot`) |
| Recall durumu | MOD-0177 | okur (`IRecallLotStatusReader`) |
| Lokasyon ağacı | MOD-0173-FU02 | okur (depo düğümü doğrulaması) |
| ATP / sipariş tahsisi | MOD-0172 (commercial-suite) | dokunmaz; ATP-BUNDLE zaten "stok üzerinde reservation 0173'te yapılır" der (`docs/analysis/contracts/atp-bundle.openapi.yaml`, allocation özeti) |
| Yedek parça rezervasyonu (bakım görünümü) | MOD-0201 (EAM, W-4) | dokunmaz; Blueprint SoR "reservations (maintenance view)" — ileride INVENTORY rezervasyonunu tüketen bir görünüm (FU-03-EAM, §20) |

**Sınır kuralları:**
1. **İkinci bakiye yok.** Pozisyonlar defter + rezervasyonların **projeksiyonudur**; her an yeniden kurulabilir (ebeveyn ASSUMPTION-0173-13
   ile aynı sınıf). `inventory_balances` satırlarındaki `Reserved` alanı **0 kalır** (ebeveyn ASSUMPTION-0173-12; `BalanceView.reserved`
   değişmez) — rezervasyon bin'e bağlı değildir (Q-02).
2. **Stok yalnız defterin olağan yolundan değişir.** Rezervasyon stok hareketi yazmaz.
3. **Geriye uyum:** `warehouseId` / `lotId` / `reservationId` taşımayan istek 1.5.0 ile **aynı** davranır ve **aynı parmak izini**
   üretir (AC-7, AC-25).

## 3. Owned Objects

| Nesne | Dilim | Not |
|---|---|---|
| `InventoryReservation` (genişler) | DL-1 | + `WarehouseId?`, `LotId?`, `ClosedReason?` (§4.1) |
| `InventoryReservationPosition` (yeni projeksiyon) | DL-1 | koleksiyon `inventory_reservation_positions` (§4.2) |
| `InventorySkuPosition` (değişmez şema) | DL-1 | SKU düzeyi bekçisi aynen; kapsamlı rezervasyonlar da SKU `Reserved`'ına sayılır |
| `InventoryTransaction` (genişler) | DL-2 | + `ReservationId?` (§4.3) |
| `InventoryReservationAuditRecord` (genişler) | DL-1 / DL-2 | + kapsam alanları, ret alanları (§4.4) |
| Komutlar | DL-1 / DL-2 | `CreateReservationCommand` (genişler), `PostMovementCommand` (genişler), `ReleaseReservationCommand` (şema değişmez, kapsamlı bırakma) |
| Sorgular | DL-1 | `GetReservationByIdQuery` (yeni), `GetAvailabilityQuery` (genişler) |
| Süreç içi port | DL-1 | `IInventoryReservationPort` (§8.7): `CreateAsync`, `CreateBatchAsync`, `ReleaseAsync`, `GetAsync`, `GetScopeFreeAsync` |
| Okuma portu (koşullu) | DL-3 | `ILotReservationReader.ReservedByOthersAsync` (§8.5) — yalnız Q-01 = (a) |
| API | DL-1 | `GET /api/inventory/reservations/{id}` (`getReservation`, yeni); `POST /reservations` ve `GET /availability` eklemeli |
| API | DL-2 | `POST /api/inventory/movements` eklemeli (`reservationId`, `reservationConsumption`) |
| İzinler | — | **yeni anahtar yok**: `inventory.reservations.manage` (yaratma, bırakma), `inventory.stock.read` (okumalar), `inventory.movements.create` (hareket) — `InventoryPermissions` sınıfında var |
| Sözleşme | DL-1 / DL-2 | INVENTORY-BUNDLE 1.6.0 / 1.7.0 (§8.8) |

## 4. Entity Fields

Hepsi kiracı sahipli (`EntityBase`: `Id`, `TenantId`, `LegalEntityId`, `CreatedAt`, `UpdatedAt`, `Version`, `DeletedAt`); `TenantId` ve
`LegalEntityId` sunucuda çözülür, gövdeden alınmaz (ReservationRequestReader `tenantId` / `legalEntityId` üyelerini zaten reddeder).
Miktarlar SKU baz biriminde (`decimal`; ebeveyn ASSUMPTION-0173-09).

### 4.1 InventoryReservation genişleyen alanlar

| Alan | Tür | Zorunlu | Kural |
|---|---|---|---|
| `WarehouseId` | string? | hayır | LOCATION düğümü, `type = Warehouse`, `Active`, `legalEntityId` = çağıranın şirketi (FU02 `InventoryLocationDirectory` süreç içi okuma). Yaratıldıktan sonra değişmez |
| `LotId` | Guid? | hayır | Kayıtlı lot (`Lot.Id`); `WarehouseId` ister; lot aynı `ItemId/SkuId/SkuLevel`'a ait. Değişmez |
| `ClosedReason` | string? | hayır | `Released` (açık bırakma) \| `Consumed` (tam tüketim) \| `Expired`; `Status = ACTIVE` iken null. Wire `status` enum'u **değişmez** (`ACTIVE`, `RELEASED`, `EXPIRED`; tam tüketim `RELEASED` + `closedReason: Consumed`, ebeveyn ASSUMPTION-0173-15) |

Kapsam türetilir: `LotId` dolu → **Lot** kapsamı; yalnız `WarehouseId` → **Warehouse** kapsamı; ikisi de boş → **Sku** kapsamı (bugünkü).

### 4.2 InventoryReservationPosition (yeni)

| Alan | Tür | Kural |
|---|---|---|
| `ItemId`, `SkuId`, `SkuLevel` | Guid, Guid, enum | SKU kimliği |
| `Level` | enum | `Warehouse` \| `Lot` |
| `WarehouseId` | string | her iki düzeyde dolu |
| `LotId` | Guid | `Lot` düzeyinde lot kimliği; `Warehouse` düzeyinde **`Guid.Empty`** (null değil — tekil indeks null'a ve birleştirilmiş metne dayanmaz; her alan ayrı saklanır ve ayrı karşılaştırılır) |
| `AvailableOnHand` | decimal | o kapsamdaki `AVAILABLE` balance satırlarının `OnHand` toplamı; Blocked / Expired / retest-tutulan lotların stoğu **sayılmaz** (SKU pozisyonuyla aynı kural, `SkuPositionRebuilder` açıklaması, MOD-0174 CT-D1, MOD-0175 CT-Q3) |
| `Reserved` | decimal | o kapsamı içeren `ACTIVE` rezervasyonların kalan miktarı toplamı (depo düzeyi = o depodaki depo + lot rezervasyonları; lot düzeyi = yalnız o lotunkiler) |
| `BaseUomId` | string | SKU baz birimi |

Projeksiyondur: defter bacağıyla ve rezervasyon değişikliğiyle **aynı Mongo işleminde** yazılır, `SkuPositionRebuilder` deseniyle
başlangıçta yeniden kurulur (§8.2). `warehouseId`'si boş `AVAILABLE` balance satırı varsa hiçbir depo pozisyonuna girmez (yalnız SKU'ya) —
böyle satırın bugün olup olmadığı **geliştiricinin ilk ölçümü** (§19.2).

### 4.3 InventoryTransaction genişleyen alan

| Alan | Tür | Kural |
|---|---|---|
| `ReservationId` | Guid? | `postMovement` `reservationId` taşıdıysa ve kabul edildiyse; değişmez (defter append-only) |

### 4.4 Denetim kayıtları

`InventoryReservationAuditRecord` (`inventory_audit`) eklemeli alanlar: `WarehouseId?`, `LotId?`, `ClosedReason?`, `Outcome` (`Succeeded` \| `Refused`),
`ErrorCode?`, `RequestedQuantity?`, `UncoveredQuantity?`, `Level?` (`SKU` \| `WAREHOUSE` \| `LOT`), `MovementType?` (tüketim retlerinde),
`SourceModule?`. Mevcut `Action` değerlerine eklenen: `InventoryReservationRefused`, `InventoryReservationConsumptionRefused`. Mevcut satırlar
`Outcome` taşımaz ve `Succeeded` okunur. Yazma kuralı §21.

### 4.5 İndeksler (kiracı + şirket önce)

- `inventory_reservation_positions`: **tekil** `{ TenantId, LegalEntityId, ItemId, SkuId, SkuLevel, Level, WarehouseId, LotId }`.
- Metindeki `W:W` / `L:W:L` gösterimi yalnız kısaltmadır (depo düzeyi / depo + lot düzeyi pozisyonu); saklanan anahtar değildir.
- `inventory_reservations`: `{ TenantId, LegalEntityId, SkuId, SkuLevel, WarehouseId, LotId, Status }` (kapsam tüketimi ve okuması);
  mevcut indeksler değişmez.
- `inventory_transactions`: `ReservationId` için seyrek değil **kısmi** indeks gerekmez (okuma yolu yok); eklenmez.
- İndeks kurulumu `InventoryIndexConfiguration` / `InventoryIndexInitializerHostedService` içinde.

## 5. Repo Scope

Hepsi `services/Diten.InventoryService/` altında (MOD-0173 sahibi aynı: MVP-1 CT):

- `src/Diten.InventoryService.Domain/Entities/InventoryReservation.cs`, `InventoryReservationAuditRecord.cs`,
  `InventoryReservationChangedPayload.cs`, `InventoryTransaction.cs`; yeni `InventoryReservationPosition.cs`; `ValueObjects/` altında yeni
  `ReservationScope.cs`; `Repositories/` altında yeni port arayüzleri ve sonuç türleri (`ReservationCreateOutcome` vb. genişler).
- `src/Diten.InventoryService.Application/Features/Inventory/`: `Commands/CreateReservationCommand.cs`, `Commands/PostMovementCommand.cs`;
  yeni `Queries/GetReservationByIdQuery.cs`; `Queries/GetAvailabilityQuery.cs`; `Handlers/CommandHandlers/CreateReservationHandler.cs`,
  `ReleaseReservationHandler.cs`; yeni `Handlers/QueryHandlers/GetReservationByIdHandler.cs`; `Handlers/QueryHandlers/GetAvailabilityHandler.cs`;
  `Services/ReservationRequestParser.cs`, `ParsedReservation.cs`, `ReservationFingerprint.cs`, `MovementRequestParser.cs`,
  `ParsedMovement.cs`, `RequestFingerprint.cs`, `InternalMovementPostingService.cs`; yeni `Services/InventoryReservationPort.cs`;
  `Validators/` ilgili dosyalar; `InventoryModels.cs`, `InventoryMapping.cs`; `Common/InventoryErrorCodes.cs`.
- `src/Diten.InventoryService.Persistence/`: `Repositories/ReservationPositionOperations.cs`, `InventoryReservationRepository.cs`,
  `InventoryLedgerRepository.cs` (`ApplySkuPositionAsync` ve tüketim), `SkuPositionRebuilder.cs` (kapsam pozisyonlarını da kurar),
  `ReservationExpirySweeper.cs`, `LedgerProjectionReKey.cs` (§8.2 yol 5); `InventoryCollections.cs`, `InventoryIndexConfiguration.cs`,
  `InventoryBsonMappings.cs`, `DependencyInjection.cs`.
- **CT-07 (XMC-001) onayıyla — sahip izni Beste 2026-11-02: XMC-001, `Persistence/Traceability/LotHeldStock.cs` (MOD-0174 / MOD-0175 dosyası) için verildi — "yalnız ayrılmış (reserved) miktarın tutulan stok hesabına eklenmesi kapsamında, başka değişiklik yok":** `src/Diten.InventoryService.Persistence/Traceability/LotHeldStock.cs` (§8.2 yol 3 ve 4 — `ApplyAsync`,
  `ApplyRetestAsync` kapsam pozisyonlarını da günceller; çağıranlar `LotRepository`, `LotExpirySweeper`, `RetestHoldSweeperHostedService`
  değişmez). İzin bu dosyadaki değişikliği yukarıdaki kapsamla sınırlar; başka değişiklik yapılmaz, ihtiyaç çıkarsa durulur ve sorulur (değişmez başka türlü korunamaz).
- `src/Diten.InventoryService.Api/`: `Controllers/InventoryController.cs` (yeni GET), `Contract/ReservationRequestReader.cs`,
  `Contract/MovementRequestReader.cs`.
- DL-3 (koşullu): `src/Diten.InventoryService.Application/Features/Inventory/Ports/ILotReservationReader.cs` + Persistence uygulaması.
- Testler: `tests/Diten.InventoryService.Tests/Api/` altında yeni `LotReservationTests.cs`, `ReservationConsumptionTests.cs`,
  `ReservationScopeInvariantTests.cs`, `ReservationRefusalAuditTests.cs`, `LotReservationQuarantineInteractionTests.cs`; `Contract/`
  testleri (`ContractSchemaValidatorTests`, `InventoryBundleDescriptionNoteTests`, Prism smoke) ve `Contract/Fixtures/` (donmuş 1.5.0 / 1.6.0
  dosya kopyası — mevcut desen); `CentralAudit/` testleri yalnız kapsam ölçümü için (komut sayısı değişmez, §21); `Architecture/` (port
  kullanımı kuralı).
- Sözleşme: `docs/analysis/contracts/inventory-bundle.openapi.yaml` (1.6.0 DL-1, 1.7.0 DL-2; tek yazıcı MOD-0173) ve
  `docs/reference/architecture/api/mod-0173-inventory-bundle.md` §4 "Reservations" (açıklama).
- `services/Diten.InventoryService/README.md` (rezervasyon notu).

## 6. Protected Paths

- `.antigravity/**` (kullanıcı onayı), `AGENTS.md`, `CLAUDE.md`.
- `gateway/Diten.ApiGateway/**/ocelot.json` (yalnız integration-agent; bu paket gateway değiştirmez, §15).
- `services/Diten.Building.Blocks/**` (BL-501 ortak iletici olduğu gibi kalır).
- Başka modüllerin kodu: `Features/Expiry/**` (MOD-0176), `Features/Quarantine/**` (MOD-0175), `Features/Recall/**` (MOD-0177),
  `Features/Traceability/**` (MOD-0174), `Features/Warehouse/**` (MOD-0178), `Persistence/Quarantine/**`, `Persistence/Recall/**`,
  `Persistence/Expiry/**`, `Persistence/Traceability/**` (tek istisna `LotHeldStock.cs`, CT-07 izniyle ve yalnız o kapsamda — Beste 2026-11-02) — **okunur, değiştirilmez**
  (XMC-001). İhtiyaç çıkarsa durulur ve sorulur.
- `docs/analysis/contracts/fefo.openapi.yaml`, `shipment-bundle.openapi.yaml`, `warehouse-outbound.openapi.yaml`,
  `docs/analysis/contracts/drafts/wms-bundle-v1-proposal.md` — başka sahiplerin.
- `services/Diten.SupplyChainService/**` (MVP-6), `frontend/**` (ekran yok), `execution/registries/**` (kayıt CT kararı).
- Arşiv ve `_Layout.cshtml`.

## 7. Dependencies

| Bağımlılık | Tür | Durum (ölçüldü 2026-11-02, `703cbaec7`) | Kullanım |
|---|---|---|---|
| MOD-0173 S2 rezervasyon çekirdeği | HARD (taban) | var: `InventoryReservation`, `InventorySkuPosition`, `ReservationPositionOperations` (`ApplyAvailableDeltaAsync`, `TryReserveAsync`, `ConsumeAsync`, `ExpireDueAsync`), `CreateReservationHandler`, `ReleaseReservationHandler`, `SkuPositionRebuilder` | genişletilir |
| MOD-0173-FU02 LOCATION | HARD | var: `InventoryLocationDirectory` (süreç içi) | depo düğümü doğrulaması |
| MOD-0174 lot | HARD | var: `Lot`, `LotPostingRules.Evaluate` | lot kuralı (yaratmada `GOODS_ISSUE_SALES` sınıfıyla) |
| MOD-0177 recall | HARD | var: `IRecallLotStatusReader.UnderRecallAsync` | yaratmada `LOT_UNDER_RECALL` |
| MOD-0175 karantina | etkileşim (okuma) | var: `QuarantineStockActions` defter `INSUFFICIENT_STOCK` reddini, SKU'nun aktif rezervasyonu varsa 409 `QUARANTINE_STOCK_RESERVED`'a çevirir | kod değişmez; AC-18 ölçer (§8.6) |
| MOD-0176 FEFO | XMC-001 (Q-01) | var: `ExpiryPostingPolicy`, `FefoCandidateReader`; FEFO-BUNDLE 1.3.0 `getFefoOrder` `basis: onHandAvailable` | DL-3 yalnız okuma portu; 0176 tarafı 0176 paket revizyonu |
| MOD-0183 (MVP-6) | XMC-001 (tüketici) | `MovementRequest.reservationId` kullanılmıyor: MOD-0183 paketi §23.4 "Consuming an MVP-5 (MOD-0178) reservation needs `MovementRequest.reservationId` (W-5, MOD-0173 owner)"; satır alanı `inventoryReferenceId` v1'de kullanılmıyor (F-10) | 1.7.0'dan sonra 0183 revizyonu sevkiyat satırından `reservationId` gönderir — **MVP-6 CT (Natig) kararı**; o gelene kadar 0178 rezervasyonu açıkken 0183 çıkışı 422 `INSUFFICIENT_STOCK` ile reddedilebilir (0183 F-10'da kabul) |
| MOD-0178 S2 revizyonu | tüketici | MOD-0178 §8.10, WMS taslağı §11 / §12 | portu kullanır; `expiresAt`, `reservationType`, kaynak üçlüsü 0178'in kararı (Q-07) |
| BL-501 merkezi denetim | HARD | var: `AddCentralAudit<InventoryCentralAuditScope>(…, RequireEnlistment = true)`; defter izi `inventory-merkezi-iletim` yol b (`tests/architecture/audit-ledger/Diten.InventoryService.md` `## İzler`) | §21 |
| Gateway `/api/inventory/{everything}` | var | `gateway/Diten.ApiGateway/ocelot.json`'da `/api/inventory` ve `/api/inventory/{everything}` → InventoryService | yeni GET aynı aileye düşer (§15) |
| Mongo replika seti (işlem) | HARD | test paketi kendi replika setini açar (`INVENTORY_TEST_MONGO` yoksa) | §17 |

**XMC-001:** Bu paket MOD-0173'ün kendi dosyalarına (Repo Scope) dokunur; **tek istisna** `Persistence/Traceability/LotHeldStock.cs`'tir
(MOD-0174'ün lot durumu yolu, MOD-0175'in retest yolu da onu çağırır): kapsam pozisyonlarının değişmezi bu dosya güncellenmeden korunamaz
(§8.2 yol 3–4), bu yüzden **CT-07 onayı DL-1'in önkoşuludur** — **verildi** (sahip izni Beste 2026-11-02: XMC-001, `Persistence/Traceability/LotHeldStock.cs` (MOD-0174 / MOD-0175 dosyası) için verildi — "yalnız ayrılmış (reserved) miktarın tutulan stok hesabına eklenmesi kapsamında, başka değişiklik yok"). MOD-0176 (Q-01), MOD-0183 (reservationId eşlemesi) ve MOD-0178 (S2 revizyonu)
etkileri **o modüllerin paket revizyonudur**; bu paketin dilimleri onlara dokunmaz. MOD-0174…0177'nin sahibi de MVP-1 CT'dir; yine de
onların başka bir dosyasına dokunmak gerekirse durulur ve sorulur.

## 8. Runtime Constraints

### 8.1 Servis ve kapsam

- Servis `Diten.InventoryService`, port 5067; yeni servis, port ya da koleksiyon ailesi dışında altyapı yok.
- Her okuma ve yazma `TenantId` + `LegalEntityId` ile sınırlı (W-0173-02 seçici kuralı aynen). `ReservationPositionOperations` yapıcısı kapsam
  ister — yeni üyeler de kapsamsız çalışmaz.
- Silme yok: rezervasyon kapanır (`RELEASED` / `EXPIRED`), pozisyon silinmez; `DeletedAt` kullanılmaz (ebeveynle aynı).
- Gün sınırı UTC (E-6). Lot kuralının tarihi = sunucu UTC şimdi.

### 8.2 Rezervasyon kapsamı ve pozisyonlar

**Kapsam hiyerarşisi:** SKU ⊇ SKU + depo ⊇ SKU + depo + lot. Bir rezervasyon kendi kapsamını **ve onu içeren her düzeyi** ayırır:

| Rezervasyon kapsamı | Artan `Reserved` |
|---|---|
| Sku (bugünkü) | SKU pozisyonu |
| Warehouse (W) | SKU + `W:W` |
| Lot (W, L) | SKU + `W:W` + `L:W:L` |

**Defter bacağı:** `AVAILABLE` satıra dokunan her bacak, net deltasını SKU'ya (bugünkü) ve satırın deposu doluysa `W:` ile (satırın lotu
kayıtlı lot kimliğiyse) `L:` pozisyonlarına aynı işlemde uygular. Aynı depo + lot içinde net 0 olan hareket (ör. `LOCATION_TRANSFER`) hiçbir
pozisyona dokunmaz. Kayıtsız (yazılmış metin) lotun stoğu `W:`'ye girer, `L:` pozisyonu yoktur (lot rezervasyonu kayıtlı lot ister).

**Pozisyona yazan her yol (değişmezi bozabilecek hepsi — ölçüldü, `inventory_sku_positions`'a yazan her üye aranarak):**

| # | Yol | Bugün SKU pozisyonuna yazar | Bu paketle |
|---|---|---|---|
| 1 | Defter bacakları — `InventoryLedgerRepository.ApplySkuPositionAsync` → `ReservationPositionOperations.ApplyAvailableDeltaAsync` | evet | `W:` / `L:` de, aynı işlem |
| 2 | Rezervasyon yaratma / bırakma / süre / tüketim — `ReservationPositionOperations` (`TryReserveAsync`, `TryReleaseAsync`, `ExpireDueAsync`, `ConsumeAsync`) | evet | kapsamı içeren her düzey |
| 3 | Lot durum değişikliği ve SKT sweeper'ı — `LotHeldStock.ApplyAsync` (`LotRepository`, `LotExpirySweeper`; MOD-0174) | evet (`AvailableOnHand` lotun AVAILABLE toplamı kadar iner / çıkar) | lotun her depodaki `W:` ve `L:` pozisyonları da — **MOD-0174 dosyası, XMC-001 (CT-07)** |
| 4 | Retest tutma — `LotHeldStock.ApplyRetestAsync` (`RetestHoldSweeperHostedService`; MOD-0175) | evet | aynı — **XMC-001 (CT-07)** |
| 5 | Elle lot göçü yeniden anahtarlama — `LedgerProjectionReKey.ApplyAsync` (`ManualLotMigrationRepository`; ledger'ın klasörü) | evet | yazılmış metinden kayıtlı lota taşınan AVAILABLE miktar `L:` pozisyonuna girer (`W:` değişmez) |
| 6 | Yeniden kurucu — `SkuPositionRebuilder` (başlangıç) | evet | `W:` / `L:` de |

Bunların dışında pozisyona yazan yol yoktur; yeni yol eklenirse aynı tabloya girer (AC-34).

**Azalış türleri:** `SCRAP`, `WRITE_OFF`, `COUNT_LOSS`, `SUPPLIER_RETURN` de `AVAILABLE` azalışıdır ve bugünkü SKU kuralı gibi ayrılmış stoğu
açıkta bırakamaz (422 `INSUFFICIENT_STOCK`; önce bırakma). Bu, mevcut davranışın kapsam düzeyine uzamasıdır; sayım (MOD-0181) için bilgi
(FU-03-COUNT).

**Bekçi (iki yönlü, her düzeyde):**
- Yaratma: kapsamın **her** düzeyinde `AvailableOnHand − Reserved ≥ q`; biri tutmazsa 422 `INSUFFICIENT_AVAILABLE`, `details.level`
  (`SKU` \| `WAREHOUSE` \| `LOT`, ilk tutmayan düzey: önce LOT, sonra WAREHOUSE, sonra SKU), `available` (o düzeyin serbest miktarı),
  `requested`. Hiçbir şey yazılmaz.
- Azalış: `AVAILABLE`'ı azaltan bacak, tüketimden (§8.3) **sonra** dokunduğu her düzeyde `AvailableOnHand − Reserved ≥ 0` bırakmalı; yoksa
  422 **`INSUFFICIENT_STOCK`** (mevcut kod; `details.level` eklemeli). Kod bilerek yeni değildir: MOD-0175 ve MVP-6 G7 eşlemeleri bu koda
  dayanır (§8.6).
- Bekçiler Mongo'nun koşullu güncellemesiyle (filtre + `$inc`) tek adımdır (bugünkü `UnreservedAtLeast` deseni); eşzamanlı yazıcı
  WriteConflict alır ve işlem yeniden denenir (`TransactionRetryCounter`).

**Lot kuralı (yaratmada):** `LotId` doluysa lot kayıtlı olmalı (yoksa 422 `UNKNOWN_LOT`), aynı SKU'ya ait olmalı (422 `LOT_SKU_MISMATCH`),
`LotPostingRules.Evaluate(lot, skuId, skuLevel, GOODS_ISSUE_SALES, şimdi)` geçmeli (aynı kodlar: `LOT_BLOCKED`, `LOT_EXPIRED`,
`LOT_RETEST_DUE`; Draft lot kabul — ASSUMPTION-0174-07) ve lot recall altında olmamalı (422 `LOT_UNDER_RECALL`, `IRecallLotStatusReader`,
kiracı geneli — CT-R7). PRODUCT-MASTER çağrılmaz (bugünkü ASSUMPTION-S2-03 korunur: kayıtlı lotun varlığı SKU'nun lot kontrollü olduğunu
gösterir).

**Depo kuralı:** `WarehouseId` LOCATION'da `Warehouse` türü, `Active` ve çağıranın şirketinde olmalı; değilse 422 `UNKNOWN_LOCATION`;
LOCATION okunamazsa 503 `LOCATION_MASTER_UNAVAILABLE` (mevcut kodlar).

**Süre:** `expiresAt` kuralı aynen (ebeveyn ASSUMPTION-0173-16); süre dolan kapsamlı rezervasyon her düzeyde serbest kalır.
**Bırakma:** `releaseReservation` aynen (If-Match, 409 `RESERVATION_CONFLICT`); kalan miktar her düzeyde serbest kalır, `ClosedReason = Released`.
**Yeniden kurma:** `SkuPositionRebuilder` kapsam pozisyonlarını da kurar (başlangıçta, kiracı + şirket + SKU başına kendi işleminde); idempotent,
belge silmez. Değişmez: her düzeyde `Σ AVAILABLE satır OnHand (tutulan lotlar hariç) == AvailableOnHand` ve `Σ ACTIVE kapsayan rezervasyon
kalanı == Reserved`.

**Lot durumu sonradan değişirse:** mevcut kural aynen — mevcut rezervasyon kalır (INVENTORY 1.0.1 / 1.0.2 notları); lot pozisyonunun
`AvailableOnHand`'ı düşer, serbest miktar negatif görünebilir; tüketim lot kuralıyla reddedilir; sahibi bırakır.

### 8.3 Rezervasyon kimliğiyle tüketim

`MovementRequest.reservationId` (isteğe bağlı, string). Doluysa, **kaynak üçlüsüyle eşleştirme (ebeveyn ASSUMPTION-0173-15) bu istek için
yapılmaz**; yalnız adı verilen rezervasyon tüketilir. Boşsa davranış 1.5.0 ile aynıdır (üçlü eşleştirme dahil).

| Koşul | Sonuç |
|---|---|
| Kimlik bu kiracı + şirkette yok (başka kiracının / şirketin kimliği dahil) | 422 `RESERVATION_NOT_FOUND` — bilinmeyenle **aynı** kod ve mesaj (varlık sızmaz) |
| Rezervasyon `ACTIVE` değil (bırakılmış, tüketilmiş, süresi dolmuş — işlem içinde vadesi gelenler önce kapatılır) | 422 `RESERVATION_NOT_ACTIVE` (`details.status`, `details.closedReason`) |
| `itemId/skuId/skuLevel` farklı; rezervasyonun deposu varsa `fromWarehouseId` farklı; lotu varsa `lotId` farklı | 422 `RESERVATION_MISMATCH` (`details.field`: `sku` \| `warehouse` \| `lot`) |
| Hareket türü / yönü tüketmez ve taşımaz | 422 `RESERVATION_NOT_APPLICABLE` — kabul edilenler yalnız: `GOODS_ISSUE_SALES`, `GOODS_ISSUE_PROD` (`fromStockStatus = AVAILABLE`, tüketir) ve `LOCATION_TRANSFER` (`AVAILABLE`, doğrular, tüketmez — §8.4). `WAREHOUSE_TRANSFER`, `STATUS_TRANSFER`, `SCRAP`, `WRITE_OFF`, `SUPPLIER_RETURN`, `COUNT_*`, girişler ve `REVERSAL` reddedilir |
| Tüketen harekette baz miktar > rezervasyon kalanı | 422 `RESERVATION_QUANTITY_EXCEEDED` (`details.remaining`, `details.requested`) — fazlası serbest stoktan **alınmaz** (Q-04) |
| Geçerli, tüketen hareket | aynı işlemde: rezervasyon kalanı düşer (tam → `RELEASED` + `ClosedReason = Consumed`; kısmi → `ACTIVE`, `Version` +1); her düzeyde `Reserved` düşer; sonra azalış bekçisi (§8.2) çalışır; `InventoryTransaction.ReservationId` yazılır; `MovementResponse.reservationConsumption` döner |

- Sıra (işlem içinde): vadesi gelenleri kapat → adı verilen rezervasyonu oku + doğrula → tüket → pozisyon deltaları + bekçiler → bacaklar.
  Herhangi bir ret işlemi geri alır; hiçbir şey yazılmaz.
- **Ayrılmış stok başkasına çıkamaz:** `reservationId`'siz (ya da başka rezervasyonu adlandıran) bir azalış, kapsam düzeylerinde
  `AvailableOnHand − Reserved ≥ 0` bırakmazsa 422 `INSUFFICIENT_STOCK`.
- **Yetki:** tüketim `inventory.movements.create` ile yapılır; aynı kiracı + şirkette kapsamı eşleşen her hareket adı verilen rezervasyonu
  tüketebilir; tüketen kaynak (`sourceModule`, belge, satır) denetime yazılır. Tüketiciyi daraltma Q-03.
- **Tekrar:** `reservationId` istek parmak izine **yalnız doluysa** girer (1.3.0 `fefoBypassReason` deseni; `RequestFingerprint`); aynı anahtar +
  aynı gövde → 200 `idempotentReplay: true`, ikinci tüketim yok. Aynı anahtar + farklı `reservationId` → 400 `IDEMPOTENCY_KEY_REUSED`.
  Reddedilmiş istek saklanmaz: aynı anahtarla yeniden gönderim **ilk yazım** sayılır ve kurallar baştan çalışır (bugünkü ledger davranışı,
  MVP-1 CT 2026-10-23 ek not 4). Cevabı kaybolan (commit sonucu bilinmeyen) istekte istemci aynı anahtar ve aynı gövdeyle yeniden gönderir:
  yazılmışsa tekrar cevabı, yazılmamışsa ilk yazım — çift tüketim olmaz.
- **REVERSAL:** tüketen çıkışın geri alınması rezervasyonu **geri getirmez** (MVP-1 CT G3 cevabı, `docs/roadmap/plans/mvp6-integration-wp-p1-01/DECISION-DRAFT.md` §3b); stok
  `AVAILABLE` döner.
- **ETag:** kısmi tüketim `Version`'ı artırır; tutulan eski ETag ile bırakma 409 `RESERVATION_CONFLICT` verir; güncel ETag `getReservation`'dan
  okunur (bugün okuma ucu olmadığı için eklenir).
- **FEFO:** tüketen çıkış FEFO kancasından muaf **değildir**; ilk uygun lot değilse şirket politikası (`fefoBypassReason`) aynen uygulanır
  (Q-01 kararına kadar davranış değişmez, AC-29).

### 8.4 Ayrılmış stokun yer değiştirmesi

- `LOCATION_TRANSFER` aynı depoyu ve aynı stok durumunu ister (`MovementDirectionRules`, `MovementClass.LocationTransfer`: "requires the same
  warehouse on both sides"; "single-step"). Bu yüzden depo ve depo + lot pozisyonlarında net delta **0**'dır: rezervasyon ve pozisyonlar
  **değişmez**, bekçi tetiklenmez. Bin → staging taşıması `reservationId` olmadan da rezervasyonu düşürmez (MOD-0178 §8.10 gereksinim 3).
- `reservationId` ile gönderilen `LOCATION_TRANSFER`: rezervasyon §8.3 tablosuyla doğrulanır (eşleşmezse 422), **tüketilmez**, `Version`
  değişmez; işlem `ReservationId` taşır (iz için).
- **Bilinen sınır (v1, bin pini yok):** rezervasyon bin'e bağlı olmadığından, aynı depo + lotta serbest miktar varsa başka bir çıkış o miktarı
  ayrılmış stoğun durduğu bin'den (ör. staging) alabilir; toplam koruma bozulmaz ama fiziksel yer kayabilir. Bin pini Q-02.
- `WAREHOUSE_TRANSFER` ayrılmış stoğu depo dışına çıkaramaz (kaynak depo bekçisi `INSUFFICIENT_STOCK`); `reservationId` ile → 422
  `RESERVATION_NOT_APPLICABLE`.

### 8.5 FEFO kancası ve ayrılmış lotlar

**Ölçülen bugün:** FEFO aday kümesi aynı şirket + depo + SKU'nun `AVAILABLE` on-hand > 0 olan lotlarıdır; rezervasyonlar sayılmaz
(MOD-0176 paketi §8.2 "Allocation … basis onHandAvailable"; FEFO-BUNDLE `FefoOrder.basis` enum `[onHandAvailable]`; §8.3.1 "Reservation
effect: none"). Lot rezervasyonu gelince: L1 (erken SKT) tümüyle outbound A'ya ayrılmışken outbound B'nin L2 çıkışı FEFO'da **sapma** sayılır
ve şirket politikasına göre gerekçe ister ya da reddedilir — oysa L1 B için serbest değildir.

**Açık soru Q-01 (0176 + W-5 ortak karar):** seçenekler —
- **(a)** FEFO "ilk uygun lot"u **serbest** miktarla değerlendirir: lot, başkalarına ayrılmış kalanı düşüldükten sonra serbest miktarı > 0 ise
  aday; tüketen çıkışın kendi rezervasyonu o çıkış için serbest sayılır. INVENTORY tarafı: DL-3 okuma portu
  `ILotReservationReader.ReservedByOthersAsync(itemId, skuId, skuLevel, warehouseId, excludeReservationId?)` → lot başına başkalarına
  ayrılmış miktar (kiracı + şirket kapsamlı, işlem oturumuyla da okunabilir). 0176 tarafı: `FefoCandidateReader` / `FefoOrderRule`
  değişikliği + FEFO-BUNDLE eklemeli `basis` değeri — **0176 paket revizyonu**, bu paket değil.
- **(b)** Değişmez: ayrılmış lot yüzünden oluşan sapma gerekçeyle geçer (WMS taslağı §11 `fefoBypassReason` taşıma yolu). DL-3 yapılmaz.

Öneri: **(a)** — gerekçe istemek bir veri gerçeğini (lot başkasına ayrılmış) kullanıcıya yük olarak bindirir ve FEFO uyum KPI'sını bozar.
Karar verilene kadar DL-1 / DL-2 FEFO davranışını **değiştirmez** (AC-29).

### 8.6 Lot durumu ve karantina etkileşimi

- **Ölçülen (`QuarantineStockActions`, `StockActionHandlers.cs`):** karantina / bloke eylemi defterden `INSUFFICIENT_STOCK` alırsa ve SKU'nun aktif
  rezervasyonu varsa 409 `QUARANTINE_STOCK_RESERVED` döner ve SKU'nun aktif rezervasyonlarını listeler (`ActiveReservationsAsync` SKU düzeyinde
  okur → kapsamlı rezervasyonlar da listelenir). Lot bekçisi aynı kodu (`INSUFFICIENT_STOCK`) verdiği için 0175 değişmeden doğru cevabı
  verir; recall karantina iletimi bunu `BlockedByReservation` sayar (`RecallQuarantineDispatcher`). AC-18 ölçer.
- **Davranış farkı (bilinçli):** lot rezervasyonu olan lotun karantinası artık lot düzeyinde reddedilir; bugün SKU serbest miktarı yeterse geçerdi.
  Bu yalnız kapsamlı rezervasyon varken olur (bugün yok) ve CT-Q6 kuralıyla ("ayrılmış stok karantinaya alınmaz, önce bırakılır") aynı yöndedir.
  0175'in ön kontrolünü lot düzeyine indirmek (daha erken ve daha net mesaj) FU-03-QH (§20).
- 0175'in ön kontrolü (`SkuUnreservedAsync`) SKU düzeyinde kalır; yarışı defter bekçisi yakalar (0175'in kendi notu).

### 8.7 Süreç içi rezervasyon portu

`IInventoryReservationPort` (Application, `Features/Inventory/Services/InventoryReservationPort.cs`) — MOD-0178'in tek rezervasyon kapısı
(MOD-0178 §2 "rezervasyonu INVENTORY'nin rezervasyon komutuyla yapar — defter / bakiye / pozisyon koleksiyonuna doğrudan yazmaz"):

| Üye | Davranış |
|---|---|
| `CreateAsync(request, actorUserId)` | HTTP ile **aynı** ayrıştırıcı, parmak izi, idempotency, bekçi, lot / depo kuralı, denetim; şirket = çözülmüş kapsam (değilse fail-closed ret, `InternalMovementPostingService` deseni) |
| `CreateBatchAsync(lines, actorUserId)` | En çok **100** satır; **hepsi ya da hiçbiri** — tek Mongo işlemi; bir satır reddedilirse hiçbir rezervasyon yazılmaz ve satır başına sonuç (kod, düzey, serbest miktar) döner; her satırın kendi idempotency anahtarı vardır; satırların hepsi önceden işlenmişse tekrar sonuçları döner, bir kısmı işlenmişse 400 `IDEMPOTENCY_KEY_REUSED` (karışık tekrar kabul edilmez) |
| `ReleaseAsync(id, ifMatch, actorUserId)` | HTTP bırakma ile aynı |
| `GetAsync(id)` | `getReservation` ile aynı görünüm |
| `GetScopeFreeAsync(itemId, skuId, skuLevel, warehouseId, lotId?)` | kapsamın serbest miktarı (pozisyondan) — tahsis önerisi için okuma |

- Port yalnız InventoryService içinden çağrılır; hiçbir controller portu çağırmaz, port HTTP izni denetlemez (çağıran 0178 komutu kendi
  `warehouse.*` iznini taşır). Mimari test (AC-16).
- Merkezi denetim: port çağıran 0178 komutunun içinde koştuğunda deponun `CentralAuditTransaction.EnlistAsync`'i **0178 komutunun satırına**
  gider (MOD-0178 §8.8 ile aynı mekanizma); yerel `inventory_audit` satırları rezervasyon başına yazılır (§21).

### 8.8 Sözleşme değişikliği (INVENTORY 1.6.0 ve 1.7.0)

Hepsi **isteğe bağlı / eklemeli**; `x-changelog`'a sürüm satırı; `InventoryErrorCodes.All` ve sözleşme aynı değişiklikte. Ölçüm,
`InventoryBundleDescriptionNoteTests`'in mevcut deseniyle: önceki sürümün donmuş fixture'ı bayt eşit, yeni sürüm farkı yalnız sayılan
eklemeler, her yeni kod `InventoryErrorCodes.All`'da (1.3.0 / 1.4.0 testleri emsal).

**1.6.0 (DL-1):**
- `ReservationRequest` + `warehouseId` (string, nullable), `lotId` (string uuid, nullable; `warehouseId` ister).
- `ReservationResponse` + `warehouseId`, `lotId`, `remainingQuantity`.
- Yeni `GET /reservations/{id}` (`getReservation`, `x-permission: inventory.stock.read`) → yeni şema `ReservationView`: `reservationId`,
  `status`, `closedReason`, `itemId`, `skuId`, `skuLevel`, `warehouseId`, `lotId`, `quantity`, `remainingQuantity`, `reservationType`,
  `sourceModule`, `sourceDocumentId`, `sourceLineId`, `priority`, `expiresAt`, `createdAt`, `etag`, `contractVersion`; bilinmeyen / kapsam
  dışı kimlik → 404, `releaseReservation`'ın bugünkü 404'üyle **aynı biçim** (`ReleaseReservationHandler`: kodsuz
  `Response.Fail("Reservation not found.", 404)`; yeni kod yok). Cevap başlığı `ETag`.
- `createReservation` 422 altına örnekler: `UNKNOWN_LOT`, `LOT_SKU_MISMATCH`, `LOT_BLOCKED`, `LOT_EXPIRED`, `LOT_RETEST_DUE`,
  `LOT_UNDER_RECALL`, `UNKNOWN_LOCATION`; yeni durum kodları `400` (`INVALID_REQUEST`, `IDEMPOTENCY_KEY_REUSED` — bugün de dönüyor, beyan
  ediliyor) ve `503` (`LOCATION_MASTER_UNAVAILABLE`). `INSUFFICIENT_AVAILABLE` `details.level` (eklemeli).
- `getAvailability` + isteğe bağlı `lotId` (`warehouseId` ister); `AvailabilityView` + `scopeReserved`, `scopeAvailable` (yalnız
  `warehouseId` verilip `locationId` verilmediğinde; depo ya da depo + lot pozisyonundan, tabansız). Mevcut `reserved` / `available`
  anlamı **değişmez** (bugünkü SKU toplamı, kapsamda 0 tabanı — `GetAvailabilityHandler` ASSUMPTION-S2-05).

**1.7.0 (DL-2):**
- `MovementRequest` + `reservationId` (string, nullable).
- `MovementResponse` + `reservationConsumption` { `reservationId`, `consumedQuantity`, `remainingQuantity`, `status`, `closedReason` } —
  yalnız tüketen harekette; `LOCATION_TRANSFER` doğrulamasında `consumedQuantity: "0.000"`.
- `TransactionItem` + `reservationId` (nullable).
- `postMovement` 422 altına yeni kodlar: `RESERVATION_NOT_FOUND`, `RESERVATION_NOT_ACTIVE`, `RESERVATION_MISMATCH`,
  `RESERVATION_NOT_APPLICABLE`, `RESERVATION_QUANTITY_EXCEEDED`; `INSUFFICIENT_STOCK` `details.level` (eklemeli).

Bugün `ReservationRequestReader` / `MovementRequestReader` bilinmeyen üyeleri **yok sayar**: 1.6.0 / 1.7.0 öncesi `lotId` ya da
`reservationId` gönderen istemci sessizce SKU düzeyinde rezerve eder / tüketmez. Bilinen tüketici yok (MOD-0183 v1 göndermiyor, §7).
Sürüm iki adım mı tek adım mı: CT-03.

### 8.9 Durum ve komut tablosu

Rezervasyon durumları `ACTIVE`, `RELEASED` (`ClosedReason` `Released` \| `Consumed`), `EXPIRED`.

| Komut / olay | ACTIVE | RELEASED | EXPIRED | Yok / başka kapsam |
|---|---|---|---|---|
| `createReservation` (yeni anahtar) | — (yeni kayıt ACTIVE) | — | — | 201 ya da 422 / 400 / 503 |
| aynı anahtar, aynı gövde | 200 tekrar (ilk sonuç) | 200 tekrar (ilk sonuç, bugünkü `Replay`) | 200 tekrar | — |
| `releaseReservation` (doğru If-Match) | 200 → RELEASED, `Released` | 409 `RESERVATION_CONFLICT` | 409 `RESERVATION_CONFLICT` | 404 |
| `releaseReservation` (eski If-Match) | 409 | 409 | 409 | 404 |
| `postMovement` tüketen, `reservationId` | tam → RELEASED `Consumed`; kısmi → ACTIVE | 422 `RESERVATION_NOT_ACTIVE` | 422 `RESERVATION_NOT_ACTIVE` | 422 `RESERVATION_NOT_FOUND` |
| `postMovement` `LOCATION_TRANSFER`, `reservationId` | değişmez (doğrulama) | 422 `RESERVATION_NOT_ACTIVE` | 422 `RESERVATION_NOT_ACTIVE` | 422 `RESERVATION_NOT_FOUND` |
| `postMovement` `reservationId`'siz azalış | bekçi; üçlü eşleşirse tüketir (bugünkü) | etkisiz | etkisiz | — |
| süre sweeper'ı / işlem içi vade | `expiresAt ≤ şimdi` → EXPIRED | etkisiz | etkisiz | — |
| `getReservation` | 200 | 200 | 200 | 404 (bırakmayla aynı biçim) |

### 8.10 Kayıt kodu

Rezervasyonun kullanıcıya görünen iş kodu **yoktur** (bugün de yok; `reservationId` sunucu Guid'i). Rezervasyon kullanıcıya sahibinin belgesi
(0178 outbound numarası) üzerinden görünür; RCS-001 kapsamı dışında kalır (`record-coding-standard.md` giriş notu "Kapsam": "kullanıcıya görünen
*iş kodu*"; veritabanı `Id`'si kuralın konusu değil). Görünür kod istenirse ayrı sahip kararı (Q-08).

## 9. Layout & Shell Contract

`shell: none` — ekran, Razor görünümü ve layout **yok**; `golden_reference: none`, `form_field_count: 0`, DataTable yok (bu yüzden
`data_mode` yazılmaz; `module-pack-standard.md` §2 alan kuralı yalnız DataTable modülleri için). Ebeveyn MOD-0173 ve FU02'nin backend
dilimleriyle aynı. Rezervasyon ekranı (FU01 BG-08 çekmecesi) bu paketin kapsamı değildir (FU-03-UI, §20).

## 10. Backend File Convention

Golden Reference (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceSlim/`) düzeni
mevcut `Features/Inventory/` klasöründe zaten uygulanıyor; bu paket aynı klasörü genişletir:

```text
Features/Inventory/
├── Commands/        CreateReservationCommand.cs (genişler) · PostMovementCommand.cs (genişler) · ReleaseReservationCommand.cs
├── Queries/         GetReservationByIdQuery.cs (yeni, sealed record) · GetAvailabilityQuery.cs (genişler)
├── Handlers/
│   ├── CommandHandlers/   CreateReservationHandler.cs · ReleaseReservationHandler.cs
│   └── QueryHandlers/     GetReservationByIdHandler.cs (yeni) · GetAvailabilityHandler.cs
├── Validators/      CreateReservationValidator.cs · ReleaseReservationValidator.cs (+ gerekirse GetReservationByIdValidator.cs)
├── Services/        InventoryReservationPort.cs (yeni) · ReservationRequestParser.cs · MovementRequestParser.cs · …
└── InventoryModels.cs   (DTO'lar tek dosyada: ReservationViewDto, ReservationConsumptionDto, …)
```

- Adlandırma: Handler / Validator adında `Command` / `Query` eki yok; tek dosyada tek public tür.
- Yeni sorgu adı `GetReservationByIdQuery` — ad `Query` ile biter (AUD-001 §2: sorgu sayılır, denetim kaydı yazılmaz; yazmaz).
- Pozisyon işlemleri `Persistence/Repositories/ReservationPositionOperations.cs` içinde kalır (tek yer; defter, rezervasyon deposu ve sweeper
  aynı sınıfı kullanır — sınıfın kendi gerekçesi).

## 11. Frontend File Contract

Yok (`shell: none`). Yeni hata kodlarının kullanıcı metni tüketici ekranlarında yazılır: MVP-6 Shipments `.resx` (G7: `INVENTORY_REFUSED` +
`details.inventoryCode`, 7 dil) ve MOD-0178 S2 ekranları — o paketlerin işidir (§7). Bu servisin API `message` alanı bugünkü gibi İngilizce
teknik metindir; kullanıcıya doğrudan gösterilmez (kod ile yerelleştirilir).

## 12. Validation Rules

**createReservation (genişleyen alanlar):**

| Alan | Zorunlu | Biçim / kural | DB düzeyi | Ön kontrol |
|---|---|---|---|---|
| `warehouseId` | hayır | trim; boş metin = yok; ≤ 100 karakter | — | LOCATION `Warehouse` + `Active` + aynı şirket → yoksa 422 `UNKNOWN_LOCATION`; okunamazsa 503 |
| `lotId` | hayır | boş metin = yok; UUID (`LotPostingRules.IsRegisteredLotForm`), değilse 422 `UNKNOWN_LOT`; `warehouseId` yoksa 400 `INVALID_REQUEST` | — | lot kayıtlı (422 `UNKNOWN_LOT`), aynı SKU (422 `LOT_SKU_MISMATCH`), lot kuralı (`LOT_BLOCKED` / `LOT_EXPIRED` / `LOT_RETEST_DUE`), recall (`LOT_UNDER_RECALL`) |
| mevcut alanlar | — | değişmez (ebeveyn §12, `ReservationRequestParser`) | — | — |

**getReservation:** `id` Guid değilse 404, bilinmeyenle aynı cevap (biçim bilgisi sızmaz).

**getAvailability:** `lotId` UUID; `lotId` var `warehouseId` yok → 400 `INVALID_REQUEST`; `lotId` + `locationId` birlikte → 400
`INVALID_REQUEST` (bin pini yok, Q-02).

**postMovement (1.7.0):**

| Alan | Zorunlu | Biçim / kural | Ön kontrol |
|---|---|---|---|
| `reservationId` | hayır | Guid biçimi değilse 422 `RESERVATION_NOT_FOUND` (bilinmeyenle aynı) | §8.3 tablosu (işlem içinde, yetkili okuma) |

İlişkili kurallar: `lotId` → `warehouseId` zorunlu; `reservationId` + `REVERSAL` → 422 `RESERVATION_NOT_APPLICABLE` (REVERSAL kopyası
asla `reservationId` taşımaz); miktar karşılaştırması baz birimde (`baseQuantity`, mevcut UoM dönüşümü).

## 13. Failure Path to Verify

- **Yetersiz serbest miktar (yaratma)** — Expected: 422 `INSUFFICIENT_AVAILABLE` + `details.level` / `available` / `requested`; rezervasyon yok,
  pozisyonlar değişmez; ret denetim satırı 1, merkezi iletilen 0.
- **Başkasının ayrılmış stoğunu çıkarma** — Expected: 422 `INSUFFICIENT_STOCK` + `details.level`; hareket yok; rezervasyon değişmez.
- **Eksik / geçersiz alan** — `lotId` var `warehouseId` yok → 400 `INVALID_REQUEST`; bilinmeyen lot → 422 `UNKNOWN_LOT`; kayıt yok.
- **Tekrar (duplicate)** — aynı anahtar + aynı gövde → 200 ilk sonuç, ikinci kayıt / tüketim yok; aynı anahtar + farklı gövde → 400
  `IDEMPOTENCY_KEY_REUSED`.
- **Yetkisiz** — `inventory.reservations.manage` yok → 403 (yaratma, bırakma); `inventory.stock.read` yok → 403 (`getReservation`);
  `inventory.movements.create` yok → 403 (tüketim). SuperAdmin atlatması yok (ebeveyn ASSUMPTION-0173-19).
- **Kiracı / şirket dışı** — başka kiracının / şirketin rezervasyonu: `getReservation` 404; `releaseReservation` 404; `postMovement`
  `reservationId` → 422 `RESERVATION_NOT_FOUND` (bilinmeyenle aynı).
- **Eşzamanlılık** — (i) aynı lotta paralel yaratmalar toplam serbest miktarı aşamaz; (ii) aynı rezervasyonu iki çıkış paralel tüketirse en çok
  kalan kadar tüketilir, öteki 422 `RESERVATION_QUANTITY_EXCEEDED` ya da `RESERVATION_NOT_ACTIVE`; (iii) bırakma ile tüketim yarışında biri
  kazanır (öteki 409 / 422); her durumda değişmez test yeşil (AC-9, AC-27).
- **Yarıda kalan toplu yaratma** — işlem içinde enjekte hata → hiçbir satır yazılmaz (AC-16).
- **Merkezi denetim yazılamaz (K2)** — başarılı yaratma / tüketimde enjekte enlist hatası → komut başarısız, rezervasyon / tüketim yok.
- **Ret denetim satırı yazılamaz** — cevap değişmez (ret zaten ret), uyarı logu + `inventory_refusal_audit_failed_total` sayacı (sessiz değil;
  AUD-001 §4.3 "diğer iş kayıtları").
- **LOCATION okunamaz** — 503 `LOCATION_MASTER_UNAVAILABLE`, yazım yok (fail-closed).

## 14. Authorization Convention

```text
Policy:     [Authorize] (tenant JWT) + LE seçici (W-0173-02)
Permission: [HasPermission(InventoryPermissions.*)] — yeni anahtar YOK
  inventory.reservations.manage  POST /reservations, POST /reservations/{id}/release   (mevcut)
  inventory.stock.read           GET /reservations/{id} (yeni uç), GET /availability   (mevcut anahtar)
  inventory.movements.create     POST /movements (+ reservationId)                     (mevcut)
Actor:      tenant_user; servis aktörü yok (MVP-6 kullanıcı token'ı, G5 kararı)
Süreç içi:  IInventoryReservationPort HTTP izni denetlemez; çağıran komutun izni geçerlidir (MOD-0178 warehouse.*)
```

- `inventory.stock.read` varsayılan Görüntüleyici rolüne gider (`DefaultRolePermissionTemplate` kiracı okuma anahtarlarını verir — ölçülmedi,
  CT kontrolü); rezervasyon görünümü kişisel veri taşımaz, ayrı istisna gerekmez.
- Namespace `inventory.*` tek sahipli (PKS-001 §4; `InventoryPermissions` açıklaması). Manifest değişmez (yeni anahtar yok).

## 15. Gateway / API Routing Decision

Karar: Gateway değişikliği **gereksiz**. `ocelot.json`'da `/api/inventory` ve `/api/inventory/{everything}` InventoryService'e (5067) gider;
yeni `GET /api/inventory/reservations/{id}` bu aileye düşer. Başlıklar (`Authorization`, `X-Legal-Entity-Id`, `X-Correlation-Id`,
`Idempotency-Key`, `If-Match`) zaten iletiliyor (ebeveyn §15). Cevap başlığı `ETag`'in Gateway'den geçtiği **ölçülmedi** — DL-1 E3 smoke'u
Gateway üzerinden ölçer (AC-19); geçmiyorsa gövdedeki `etag` alanı yeterlidir ve integration-agent'a not düşülür.

## 16. Acceptance Criteria

- [ ] AC-1 (zorunlu) — `POST /api/inventory/reservations` `warehouseId = W`, `lotId = L` ile → 201; cevapta `warehouseId`, `lotId`, `remainingQuantity` = `quantity`; `GET /api/inventory/reservations/{id}` aynı alanları, `status ACTIVE`, `closedReason` yok ve `etag` = `W/"1"` döner; `inventory_reservations` belgesinde `WarehouseId`, `LotId` dolu. Sabotaj: ayrıştırıcıda `lotId` okumayı kaldır → test kırmızı.
- [ ] AC-2 (zorunlu) — Lot bekçisi: L / W'de `AVAILABLE` 50 (bin A 30 + bin B 20); 40'lık lot rezervasyonu 201; ardından 11'lik → 422 `INSUFFICIENT_AVAILABLE`, `details.level = LOT`, `available "10.000"`, `requested "11.000"`; rezervasyon sayısı 1, üç pozisyon değişmemiş; 10'luk → 201. Sabotaj: lot düzeyi filtresini kaldır → kırmızı.
- [ ] AC-3 (zorunlu) — Depo ve SKU bekçisi ayrı fixture'larla: SKU W1'de 50, W2'de 50. (a) W1 depo rezervasyonu 50 → 201; W1'de L lot rezervasyonu 1 → 422 `level WAREHOUSE`; (b) W2 depo rezervasyonu 50 → 201; kapsamsız (SKU) rezervasyon 1 → 422 `level SKU`. Sabotaj (her biri ayrı): depo düzeyi artışını kaldır → (a) kırmızı; SKU düzeyi artışını kaldır → (b) kırmızı.
- [ ] AC-4 (zorunlu) — Ayrılmış stok başkasına çıkmaz, iki ayrı fixture: (a) R = L / W 30 (`sourceModule MOD-0178`), L / W on-hand 50; `reservationId`'siz `GOODS_ISSUE_SALES` L / W 21 (`sourceModule MOD-0183`) → 422 `INSUFFICIENT_STOCK`, `details.level LOT`, hareket yok, R değişmez; 20 → 201 ve R `remainingQuantity` 30 kalır; (b) depo rezervasyonu (lotsuz) W 30, W'de lotsuz SKU on-hand 50; `reservationId`'siz çıkış 21 → 422 `level WAREHOUSE`; 20 → 201. Sabotaj (ayrı ayrı): azalışta lot bekçisini kaldır → (a) kırmızı; depo bekçisini kaldır → (b) kırmızı.
- [ ] AC-5 (zorunlu) — Yer değiştirme: R = L / W 30, stok bin A'da 30; `reservationId`'siz `LOCATION_TRANSFER` 30 A → staging → 201; R `ACTIVE`, `remainingQuantity 30`, `Version` değişmemiş; `W:` ve `L:` pozisyonları bayt eşit. Sabotaj: transferi azalış + tüketim say → kırmızı.
- [ ] AC-6 (zorunlu) — Yaratmada lot / depo retleri, her biri ayrı fixture ve her birinde yazım 0: kayıtsız lot → 422 `UNKNOWN_LOT`; başka SKU'nun lotu → 422 `LOT_SKU_MISMATCH`; Blocked → `LOT_BLOCKED`; Expired (tarih) → `LOT_EXPIRED`; retest tarihi geçmiş → `LOT_RETEST_DUE`; recall altında → `LOT_UNDER_RECALL`; başka şirketin / pasif / Warehouse olmayan düğüm → 422 `UNKNOWN_LOCATION`; `lotId` var `warehouseId` yok → 400 `INVALID_REQUEST`; Draft lot → 201 (ASSUMPTION-0174-07).
- [ ] AC-7 (zorunlu) — Geriye uyum: `warehouseId` / `lotId` taşımayan yaratma isteğinin `ReservationFingerprint` değeri, bu değişiklikten önceki koddan sabitlenmiş test vektörüne bayt eşit; mevcut `ReservationTests`, `ReservationExpiryTests`, `ReservationAccessTests`, `ReservationInvariantTests` değiştirilmeden yeşil.
- [ ] AC-8 (zorunlu) — Tekrar: aynı anahtar + aynı gövde → 200, aynı `reservationId`, pozisyonlar değişmez; aynı anahtar + farklı `lotId` → 400 `IDEMPOTENCY_KEY_REUSED`, yazım 0.
- [ ] AC-9 (zorunlu) — Eşzamanlılık, ayrı ayrı: (a) aynı L / W'de serbest 50'ye karşı 20 paralel 5'lik yaratma → tam 10 × 201, 10 × 422, `L:` `Reserved = 50`, değişmez test yeşil; (b) işlem içi örtüşme: aynı koşuda `TransactionRetryCounter` > 0 gözlenir (çakışma dalı gerçekten koştu), sonuç yine (a) sayılarıyla aynı; (c) paralel yaratma ile paralel azalış (`GOODS_ISSUE_SALES`) karışımında hiçbir düzeyde `AvailableOnHand − Reserved < 0` olmaz.
- [ ] AC-10 (zorunlu) — Bırakma ve süre: L / W rezervasyonu 30 bırakılınca SKU, `W:`, `L:` `Reserved` her biri 30 düşer, `closedReason Released`; `expiresAt` geçmiş rezervasyon sweeper ya da işlem içi vadeyle `EXPIRED` olur ve üç düzeyde serbest kalır.
- [ ] AC-11 (zorunlu) — Yeniden kurma: rastgele hareket + kapsamlı rezervasyon tohumlarıyla (en az 3 tohum) her düzeyde `Σ satır == AvailableOnHand` ve `Σ ACTIVE kapsayan kalan == Reserved`; bozulmuş bir `L:` pozisyonu başlangıç yeniden kurmasıyla düzelir ve ikinci koşu aynı sayıları yazar; FU03 öncesi defterde (pozisyonsuz) başlangıçta `W:` / `L:` pozisyonları oluşur. Sabotaj: yeniden kurucudan kapsam pozisyonlarını çıkar → kırmızı.
- [ ] AC-12 (zorunlu) — Kiracı / şirket izolasyonu: kiracı A, kiracı B'nin rezervasyonunu `GET` → 404, `release` → 404; aynı kiracının başka şirketi → 404; başka şirketin deposuyla yaratma → 422 `UNKNOWN_LOCATION`; B'nin pozisyon ve rezervasyon belgeleri bayt eşit kalır.
- [ ] AC-13 (zorunlu) — Yetki: `inventory.stock.read`'siz `GET /reservations/{id}` → 403 (gövdede veri yok); `inventory.reservations.manage`'siz yaratma → 403; SuperAdmin rolü izin anahtarı olmadan 403.
- [ ] AC-14 (zorunlu) — Başarı denetimi: lot rezervasyonu yaratma → aynı işlemde `inventory_audit` 1 satır `InventoryReservationCreated` (`WarehouseId`, `LotId`, `ActorUserId`, `CorrelationId`, `LeSource`, `Outcome Succeeded`) ve `central_audit_outbox` 1 satır (`RequestType CreateReservationCommand`); merkezi enlist'e enjekte hata → 201 dönmez, rezervasyon ve denetim satırı yok (K2).
- [ ] AC-15 (zorunlu) — Ret denetimi (AUD-001 §3), her ret türü ayrı fixture: `INSUFFICIENT_AVAILABLE`, `UNKNOWN_LOT`, `LOT_UNDER_RECALL`, `UNKNOWN_LOCATION`, 400 `INVALID_REQUEST`, 400 `IDEMPOTENCY_KEY_REUSED` ve 503 `LOCATION_MASTER_UNAVAILABLE` → her birinde tam 1 `InventoryReservationRefused` satırı (`Outcome Refused`, `ErrorCode`, aktör, kiracı, şirket, korelasyon, istenen miktar, varsa düzey / lot / depo), rezervasyon 0, iletilen merkezi kayıt 0; gövdesi okunamayan istek (handler'a ulaşmaz) → 0 satır. Sabotaj: ret yazımını kaldır → kırmızı.
- [ ] AC-16 (zorunlu) — Süreç içi port: `CreateBatchAsync` 3 satır, 3. satır yetersiz → hiçbir rezervasyon yok, 3. satır sonucu `INSUFFICIENT_AVAILABLE` + düzey; 3 satır da uygun → 3 rezervasyon tek işlemde; 2. ekleme sonrasına enjekte hata → 0 rezervasyon kalır; 100 satır → kabul, 101 satır → ret, yazım 0; satırların bir kısmı önceden işlenmişse 400 `IDEMPOTENCY_KEY_REUSED`, yazım 0; çözülmüş şirketten farklı şirketle çağrı → fail-closed ret; mimari test: hiçbir controller `IInventoryReservationPort`'u kullanmaz.
- [ ] AC-17 (zorunlu) — `getAvailability`: `warehouseId = W` + `lotId = L` → `scopeReserved` / `scopeAvailable` `L:` pozisyonuna eşit; yalnız `warehouseId` → `W:` pozisyonuna eşit; mevcut `reserved` / `available` alanları bu değişiklikten önce sabitlenmiş fixture'la bayt eşit; `lotId` var `warehouseId` yok → 400; `lotId` + `locationId` → 400.
- [ ] AC-18 (zorunlu) — Karantina etkileşimi (MOD-0175 kodu değişmeden): L / W'nin tamamı lot rezervasyonuyla ayrılmışken (SKU serbest miktarı yeterli) 0175 "Karantinaya Al" aynı lot stoğu için → 409 `QUARANTINE_STOCK_RESERVED`, `details.reservations` lot rezervasyonunu içerir, hareket ve karantina kaydı yok.
- [ ] AC-19 (zorunlu) — Sözleşme 1.6.0: `x-changelog` 1.6.0 satırı; `InventoryBundleDescriptionNoteTests` deseniyle 1.5.0 fixture'ı bayt eşit ve 1.6.0 farkı yalnız §8.8'de sayılan eklemeler; `ContractSchemaValidatorTests` cevapları 1.6.0 şemasına karşı doğrular; Prism mock smoke yeşil; Gateway üzerinden (5000) gerçek login token'ıyla `GET /api/inventory/reservations/{id}` 200 + gövdede `etag` (E3).
- [ ] AC-20 (zorunlu) — Tüketim: R (`MOD-0178`, L / W, 30); `GOODS_ISSUE_SALES` L / W 30, `sourceModule MOD-0183`, `reservationId = R` → 201; R `RELEASED`, `closedReason Consumed`, kalan 0; üç düzeyde `Reserved` 30 düşer; `reservationConsumption` = { R, `"30.000"`, `"0.000"`, `RELEASED`, `Consumed` }; `inventory_transactions.ReservationId = R`; `inventory_audit` `InventoryReservationConsumed` (`TransactionId` dolu). Sabotaj: `reservationId`'yi yok say → 422 `INSUFFICIENT_STOCK` → kırmızı.
- [ ] AC-21 (zorunlu) — Kısmi tüketim ve ETag: R 30'dan 20 tüketilir → `ACTIVE`, kalan 10, `Version` 2; `getReservation` `etag W/"2"`; `W/"1"` ile bırakma → 409 `RESERVATION_CONFLICT`; `W/"2"` ile → 200, üç düzeyde `Reserved` 10 düşer.
- [ ] AC-22 (zorunlu) — Tüketim retleri, her biri ayrı fixture ve her birinde hareket 0, R bayt eşit: bilinmeyen kimlik → 422 `RESERVATION_NOT_FOUND`; başka kiracının ve başka şirketin kimliği → aynı kod ve aynı `message`; bırakılmış / süresi dolmuş → `RESERVATION_NOT_ACTIVE`; SKU farklı / depo farklı / lot farklı (üç fixture) → `RESERVATION_MISMATCH` + `details.field`; miktar > kalan → `RESERVATION_QUANTITY_EXCEEDED`; `WAREHOUSE_TRANSFER`, `STATUS_TRANSFER`, `SCRAP`, `REVERSAL` ve `fromStockStatus QUARANTINE` (beş fixture) → `RESERVATION_NOT_APPLICABLE`.
- [ ] AC-23 (zorunlu) — `reservationId` doluyken üçlü eşleşme yapılmaz: R1 (L / W) ve kaynak üçlüsü hareketinkiyle aynı olan R2 varken `reservationId = R1` ile çıkış yalnız R1'i tüketir, R2 bayt eşit.
- [ ] AC-24 (zorunlu) — `LOCATION_TRANSFER` + `reservationId`: eşleşen R → 201, R `remainingQuantity` ve `Version` değişmez, işlem `ReservationId = R`, `reservationConsumption.consumedQuantity "0.000"`; başka lotun R'si → 422 `RESERVATION_MISMATCH`.
- [ ] AC-25 (zorunlu) — Geriye uyum (hareket): `reservationId`'siz isteğin `RequestFingerprint`'i 1.5.0 kodundan sabitlenmiş vektöre bayt eşit; üçlü tüketim testleri (ebeveyn ASSUMPTION-0173-15) değiştirilmeden yeşil.
- [ ] AC-26 (zorunlu) — Tekrar (tüketim): aynı anahtar + aynı gövdeyle ikinci gönderim → 200 `idempotentReplay: true`, aynı `reservationConsumption`, R ve pozisyonlar değişmez; aynı anahtar + farklı `reservationId` → 400 `IDEMPOTENCY_KEY_REUSED`.
- [ ] AC-27 (zorunlu) — Eşzamanlılık (tüketim), ayrı ayrı ve aynı anda başlatılan isteklerle (`Task.WhenAll`; koşuda `TransactionRetryCounter` > 0 ya da işlem içi okumada çakışma kaydı — sayaç 0 ise test destekleyici sayılır ve yeniden yazılır): (a) R 30'a iki paralel 20'lik çıkış → tam biri 201, öteki 422 `RESERVATION_QUANTITY_EXCEEDED`; yazılan hareket 1, `InventoryReservationPartiallyConsumed` 1, R kalan 10; (b) bırakma ile 30'luk tüketim paralel → tam biri başarılı (hareket 1 ya da bırakma 1), öteki 409 `RESERVATION_CONFLICT` ya da 422 `RESERVATION_NOT_ACTIVE`; her iki durumda değişmez test yeşil.
- [ ] AC-28 (zorunlu) — Tüketim ret denetimi: AC-22'nin her ret türünde tam 1 `InventoryReservationConsumptionRefused` satırı (gönderilen `reservationId` metni, hareket türü, `ErrorCode`, aktör, korelasyon, `SourceModule`), iletilen merkezi kayıt 0. Sabotaj: yazımı kaldır → kırmızı.
- [ ] AC-29 (zorunlu) — FEFO davranışı değişmez (Q-01 kararına kadar): şirket politikası "Gerekçe iste" iken R'yi tüketen ama ilk uygun FEFO lotu olmayan çıkış `fefoBypassReason`'sız → 422 `FEFO_BYPASS_REASON_REQUIRED` (1.3.0 ile aynı); gerekçeyle → 201 + `expiryChecks.fefo Bypassed`.
- [ ] AC-30 (zorunlu) — Sözleşme 1.7.0: `x-changelog` 1.7.0; `InventoryBundleDescriptionNoteTests` deseniyle 1.6.0 fixture'ı bayt eşit ve 1.7.0 farkı yalnız §8.8'de sayılan eklemeler; beş yeni kod hem OpenAPI'de hem `InventoryErrorCodes.All`'da; şema testleri ve Prism smoke yeşil.
- [ ] AC-31 (isteğe bağlı) — DL-3, yalnız Q-01 = (a): `ILotReservationReader.ReservedByOthersAsync(item, sku, level, W, exclude: R)` lot başına başkalarına ayrılmış miktarı döner (R hariç, kiracı + şirket kapsamlı; başka kiracının rezervasyonu sayılmaz); işlem oturumuyla okunduğunda aynı işlemdeki rezervasyon değişikliğini görür. Sabotaj: `exclude`'u yok say → kırmızı.
- [ ] AC-33 (zorunlu) — Altın akış (üç gereksinim birlikte, tek test): L / W on-hand 50 bin A'da; port `CreateBatchAsync` ile R = L / W 30 (`MOD-0178`) → `LOCATION_TRANSFER` 30 A → staging (`reservationId = R`) → başka kaynağın `reservationId`'siz staging'den 21'lik çıkışı 422 `INSUFFICIENT_STOCK` → `GOODS_ISSUE_SALES` 30 staging'den (`MOD-0183`, `reservationId = R`) 201; sonda R `RELEASED` / `Consumed`, staging 0, bin A 20, üç düzeyde `Reserved` 0, değişmez test yeşil.
- [ ] AC-34 (zorunlu) — Pozisyona yazan diğer yollar (§8.2 tablosu, her yol ayrı fixture): (3) L'yi Blocked yapan lot durum değişikliği → lotun her depodaki `W:` ve `L:` `AvailableOnHand`'ı lotun AVAILABLE toplamı kadar iner, Released'a dönüş geri çıkarır; SKT sweeper'ı Expired yapınca aynı iniş; (4) retest tutma aynı; (5) elle lot göçü yeniden anahtarlama yazılmış metinden kayıtlı lota taşınan AVAILABLE miktarı `L:`'ye ekler, `W:` değişmez; her yoldan sonra yeniden kurucu sayılarıyla bayt eşit. Sabotaj (ayrı ayrı): `LotHeldStock.ApplyAsync`'te kapsam güncellemesini kaldır → (3) kırmızı; `ApplyRetestAsync`'te → (4) kırmızı; `LedgerProjectionReKey`'de → (5) kırmızı.
- [ ] AC-32 (zorunlu) — Tam paket: `services/Diten.InventoryService` test paketinin tamamı (süzgeçsiz) kendi Mongo replika setiyle yeşil; mimari testler (`SingleSystemOfRecordArchitectureTests`, `LedgerImmutabilityArchitectureTests`, `AuditTrailStandardTests` defter sayıları) yeşil; `dotnet build` uyarısız hata yok.

## 17. Test Expectations

- **Birim (Domain / Application):** kapsam türetme (`ReservationScope`), bekçi düzey sırası (LOT → WAREHOUSE → SKU), §8.3 tablosunun her satırı,
  parmak izi vektörleri (AC-7, AC-25 — vektör, değişiklikten **önceki** kodla üretilip sabitlenir).
- **Entegrasyon (gerçek Mongo replika seti, `InventoryApiFactory`):** AC-1…AC-30; her ret türü ayrı fixture; her sabotaj ayrı koşu ve
  "kırmızı → geri al → yeşil, diff boş" kaydı kabul kaydına yazılır.
- **Eşzamanlılık:** AC-9, AC-27 — paralel istekler `Task.WhenAll` ile; çakışma dalının koştuğu `TransactionRetryCounter` ile kanıtlanır
  (pack-review §3: testin neyi zorladığı yazılır; sayaç 0 ise test "destekleyici" sayılır ve yeniden yazılır).
- **Değişmez / yeniden kurma:** `ReservationInvariantTests` deseni kapsam düzeyleriyle (AC-11); pozisyona yazan her yol ayrı fixture (AC-34, §8.2 tablosu).
- **Altın akış:** rezervasyon → staging transferi → başkasının reddi → rezervasyonla çıkış tek testte (AC-33).
- **Kiracı izolasyonu:** AC-12; **yetki:** AC-13 (`AuthorizationGuardTests` her yeni uçta `[HasPermission]` arar).
- **Denetim:** AC-14, AC-15, AC-28; `CentralAuditDeclarationTests` komut sayısı **değişmez** (yeni MediatR komutu yok; yeni sorgu `Query`
  ekli); `AuditTrailStandardTests` defter sayıları değişmez.
- **Sözleşme:** `ContractSchemaValidatorTests`, `InventoryBundleDescriptionNoteTests`, Prism smoke (AC-19, AC-30).
- **Smoke (E3):** Gateway 5000 üzerinden yaratma → okuma → tüketim → okuma akışı, gerçek login token'ıyla (AC-19).
- **Build:** `services/Diten.InventoryService` çözümü; Gateway ve Web değişmez (build yalnız ölçüm için).
- **RESX / verifier:** uygulanmaz (ekran yok); tüketici metinleri tüketici paketlerinde.
- Tam paket kuralı: servis dokunulduğunda süzgeçsiz tam koşu (AC-32); taban kırmızısı varsa aynı gün `main`'de karşılaştırılır.

## 18. Ready-for-dev Checklist

- [x] Golden Reference (`GoldenReferenceSlim` backend klasör düzeni) okundu; `Features/Inventory/` düzeni ile birebir (§10) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Frontmatter zorunlu alanlar dolu (`service`, `shell: none`, `golden_reference: none`, `entity_base`); ekran yok gerekçesi §9 — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Layout & Shell Contract: ekran yok, layout yok (§9) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Backend File Convention Golden Reference ile birebir (§10) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Frontend File Contract: yok, tüketici metinleri tüketici paketlerinde (§11) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Validation Rules her yeni alan için (§12) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Failure Path ≥ 4 senaryo: tekrar, eksik, yetkisiz, eşzamanlılık + kiracı dışı + K2 (§13) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Authorization: anahtarlar, politika, aktör; yeni anahtar yok (§14) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Gateway kararı: gereksiz (§15) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Platform lookup kararı: uygulanmaz (Platform/Admin modülü değil; lookup yok) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [ ] Acceptance criteria ölçülebilir ve sabotajlı (§16) — **açık** (2026-11-02 ölçümü): 34 AC'nin 23'ünde sabotaj yazılı değil — AC-6, AC-7, AC-8, AC-9, AC-10, AC-12, AC-13, AC-14, AC-16, AC-17, AC-18, AC-19, AC-21, AC-22, AC-23, AC-24, AC-25, AC-26, AC-27, AC-29, AC-30, AC-32, AC-33 (§17 "her sabotaj ayrı koşu" der ama sabotajı adlandırmaz); ready-for-dev önkoşulu
- [x] Test expectations build / sözleşme / izolasyon / denetim / smoke (§17) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor (§21) — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Sayfa yardımı: sayfa yok (§23); tur yok — yapısal ölçüm 2026-11-02 (bölüm var ve içerik maddeyi karşılıyor; bağımsız incelemenin yerine geçmez)
- [x] Q-02, Q-03, Q-04, Q-06, Q-08 kararları verildi (sahip kararı Beste 2026-11-02, §19.6)
- [x] CT-01 registry satırı (`2265cce90`) ve CT-03 sürüm adımı (iki adım) yazıldı (MVP-1 CT 2026-11-02, §19.6); CT-04 dilim / W-5 koşulu da verildi
- [x] CT-02 hata kataloğu yetkisi yazıldı — bu FU paketi; ebeveyne DL-1 kabulünde sürüm notu (MVP-1 CT 2026-11-02, §19.6)
- [x] CT-07 `LotHeldStock.cs` XMC-001 onayı yazıldı — sahip izni Beste 2026-11-02 (§19.6); izin metni ile §8.2 yol 3–4 arasındaki ifade farkı CT teyidi bekliyor (§19.6 açık nokta)
- Not: Q-01 (FEFO) ready-for-dev önkoşulu **değildir**; yalnız DL-3'ün dağıtım kapısıdır (§22). Q-05, Q-07 başka paketlerin kararıdır.
- [ ] CT-APR-02 bağımsız inceleme (yazandan ayrı oturum) + CT-APR-01 onay satırı (yüksek risk: sözleşme + ürün kodu yolu)
- [ ] `status: ready-for-dev`

## 19. Implementation Notes

### 19.1 Kimlik ve keşif kanıtı

Komutlar 2026-11-02, bu worktree, HEAD `703cbaec7`, Python 3.12.10:

| Komut | Çıkış | Sonuç |
|---|---|---|
| `verify_module_id.py . --discover "inventory reservation"` | 3 | Blueprint 0 · SoR 0 · registry 0 · paket 0 · gateway 1 (`inventory` → 5067; mevcut servis ailesi, çakışma değil) · görev 0; rapor sha256 `d2ca8de7323aef7d8b73ca5635431287f826238f0a170b59d3a3effdc8542950` |
| `--discover "lot reservation"` | 0 | hiç bulgu yok; rapor sha256 `dff49cd2610968f465aee86470810c7c5c6635628cd7fc051083304f0c0948ce` |
| `--discover "reservation"`, `--discover "stock reservation"` | 0 / 0 | hiç bulgu yok |
| `--check-id MOD-0173-FU03 --name "Inventory Lot & Location Reservation" --parent MOD-0173` | 0 | proven |
| `--check-id MOD-0173 --name "Inventory Ledger & Valuation"` | 0 | proven |

Keşif raporları commit edilmedi (CT kaydeder). Keşif paketleri yalnız dosya adı / künyeden eşler (raporun kendi "EKSİK" satırı); iş kapsamı
aşağıdaki içerik aramasıyla ölçüldü.

**Neden MOD-0173'ün FU'su (yeni MOD değil, MOD-0173 revizyonu değil):**
- Blueprint 8.8 `Blueprint_Data` hiçbir satırda ayrı bir "reservation" modülü taşımaz; MOD-0173 SoR "inventory balances, movements, valuation,
  locations (as scoped)", sözleşmesi INVENTORY-BUNDLE. Rezervasyon nesnesi zaten MOD-0173'ündür: INVENTORY-BUNDLE `createReservation` (sahibi
  MOD-0173), ebeveyn paket §3 `InventoryReservation` (S2), ATP-BUNDLE "stok üzerinde reservation 0173'te yapılır", MOD-0178 §2 "Stok bakiyesi,
  hareket, rezervasyon → MOD-0173", sahip kararı W-5 "sahibi MOD-0173, yani biz" (`docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md` §4).
- Diğer Blueprint adayları kapsamı karşılamaz: MOD-0172 Allocation & ATP/CTP (commercial-suite) tahsis **kararıdır**, fiziksel tutmayı 0173'e
  bırakır; MOD-0176 SoR "constrained allocations" FEFO kısıtıdır; MOD-0201 Spare Parts "reservations (maintenance view)" EAM görünümüdür (W-4).
- Revizyon değil FU: ebeveyn paket `ready-for-dev`, bağlayıcı ASSUMPTION-0173-12…17 ile kapanmış bir dilim seti taşır; emsal FU01 (UI) ve FU02
  (lokasyon master) ayrı FU paketleridir; `module-pack-standard.md` §16 "Büyük artış takip paketidir" kuralı onaylı pakete yeni yetenek
  dilimi eklemeyi FU'ya yönlendirir.
- Numara: registry'de MOD-0173 çocukları yalnız FU01, FU02 (`execution/registries/module-id-registry.md`); `git for-each-ref` ile bütün yerel
  ve uzak ref'lerde `MOD-0173-FU0[3-9]` / `*reservation*` paket dosyası yok; 40 worktree'nin çalışma ağaçlarında da yok; depo içi metinde
  `MOD-0173-FU03` geçmiyor → **FU03 ilk boş numara**.

**Mükerrer iş araması (içerik):** `reservationId`, `lot reservation`, `W-5`, `ReservationRequest`, `reserve` aramaları — W-5'i kapsayan başka
paket ya da WP **yok**. Bulunan atıflar yalnız bağımlılık / bekleme kaydıdır: MOD-0178 §7 / §8.10 / FU-0178-W5; WMS taslağı §2.3 / §11 / §13;
MOD-0183 §23.4 ve "MVP-5 reservations (F-10)"; MVP-6 `DECISION-DRAFT.md` G2 ve DL-6 ("MOD-0173 sahibi W-5 ile, MVP-5 S2'den önce ekler");
`execution/domains/supply-chain-execution/work-packs/mvp5-wms/CT-QUEUE.tsv` `WMS-MVP5-DL4` BLOCKED ("W-5 rezervasyonu main'de ve S2 paket
revizyonu onaylı"); MOD-0176 FU-0176-RES-01 ("Lot-based reservation … MOD-0178") — **çelişki:** 0176 bu işi MOD-0178'e yazmış (KF-F,
2026-10-12), sonraki sahip kararı W-5 (2026-10-21) INVENTORY'ye (MOD-0173) verir; W-5 kazanır, 0176 FU satırı bu paketle karşılanır
(CT-06, 0176 paket metni düzeltmesi XMC-001).

### 19.2 Ölçülen gerçekler (`703cbaec7`, salt okuma)

- Rezervasyon yalnız SKU kapsamlı: `InventoryReservation` (depo / lot alanı yok), `ReservationRequest` (`inventory-bundle.openapi.yaml`
  `components.schemas.ReservationRequest`), tek projeksiyon `InventorySkuPosition`.
- Tüketim yalnız kaynak üçlüsüyle: `ReservationPositionOperations.ConsumeAsync` (birebir metin, boş `sourceDocumentId` eşleşmez);
  `InventoryLedgerRepository.ApplySkuPositionAsync` net `AVAILABLE` deltası 0 ise hiçbir şey yapmaz ("a transfer between two AVAILABLE rows
  nets 0").
- `LOCATION_TRANSFER` aynı depo + aynı durum ister (`MovementDirectionRules`).
- Bekçi koşullu güncelleme: `ReservationPositionOperations.UnreservedAtLeast` ile `FindOneAndUpdateAsync`; ret `UncoveredQuantityException` →
  `INSUFFICIENT_STOCK`.
- Yaratma reddi bugün **iz bırakmaz**: `CreateReservationHandler` `INSUFFICIENT_AVAILABLE`'da yalnız cevap döner; merkezi kayıt yalnız başarılı
  komutta iletilir (BL-501, MOD-0178 §8.8). Ret izi emsali: `ExpiryPostingPolicy.FlushRefusalAuditAsync` (ret sonrası denetim satırı).
- 0175 eşlemesi: `QuarantineStockActions` defter `INSUFFICIENT_STOCK` + aktif rezervasyon → 409 `QUARANTINE_STOCK_RESERVED`.
- İstek okuyucuları bilinmeyen üyeleri yok sayar (`MovementRequestReader` açıklaması; `ReservationRequestReader` aynı kural).
- Hata kataloğu kapalı: `InventoryErrorCodes.All` ("no other code without new pack version").
- `GET /reservations/{id}` yok; `InventoryController` yalnız `POST reservations` ve `POST reservations/{id}/release` taşır.
- İzinler: `InventoryPermissions.Read = inventory.stock.read`, `CreateMovements`, `ManageReservations`.
- **Ölçülmedi (geliştiricinin ilk ölçümü):** `warehouseId`'si boş `AVAILABLE` balance satırı var mı (§4.2); `ETag` başlığının Gateway'den
  geçişi (§15); `DefaultRolePermissionTemplate`'in `inventory.stock.read`'i varsayılan role verdiği (§14).

### 19.3 Varsayımlar (yalnız yeni paket sürümüyle değişir)

- **ASSUMPTION-0173-FU03-01:** v1 kapsamı depo + lot; bin (lokasyon) pini yok — Q-02 kapandı: **hayır** (sahip kararı Beste 2026-11-02, §19.6); gerekirse FU-03-BIN.
- **ASSUMPTION-0173-FU03-02:** tam tüketim `RELEASED` + `closedReason Consumed`; wire `status` enum'u değişmez.
- **ASSUMPTION-0173-FU03-03:** kapsam bekçisinin azalış reddi mevcut `INSUFFICIENT_STOCK` kodudur (0175 ve MVP-6 G7 eşlemeleri için).
- **ASSUMPTION-0173-FU03-04:** `target 2026-11-30` şema zorunlu planlama değeridir, taahhüt değil.
- **ASSUMPTION-0173-FU03-05:** plan hizalaması — plan dosyası depoda yok; dilim tablosunda `plan kimliği yok` / `plan satırı yok` yazılır,
  kaynak: `module-pack-standard.md` §16 "Plan hizalaması". Plan satırı gelirse CT doldurur (CT-05).

### 19.4 Açık sorular

| # | Soru | Seçenekler / öneri | Sahip | Karar hedefi |
|---|---|---|---|---|
| Q-01 | FEFO kancası başka outbound'lara ayrılmış lotları nasıl sayar? (MOD-0178 §8.10 gereksinim 4) | (a) serbest miktar tabanı + DL-3 okuma portu + 0176 revizyonu (öneri); (b) değişmez, gerekçeyle sapma | MVP-1 CT (Beste) — MOD-0173 + MOD-0176 sahibi; MVP-5 S2 yazarı bilgi | 2026-11-09 (DL-3 kapısı ve 0178 S2 revizyonundan önce; ready-for-dev önkoşulu değil) |
| Q-02 | Bin (lokasyon) pini v1'de gerekli mi? | **KAPANDI — hayır** (depo + lot; bin pini yok, gerekirse FU-03-BIN) | Beste (sahip kararı 2026-11-02) | §19.6 |
| Q-03 | Rezervasyonu kim tüketebilir? | **KAPANDI** — v1'de kapsamı eşleşen her hareket (`inventory.movements.create`) + denetim; daraltma FU (FU-03-CONSUMER) | Beste (sahip kararı 2026-11-02) | §19.6 |
| Q-04 | Tüketen çıkış kalandan fazlaysa? | **KAPANDI** — 422 `RESERVATION_QUANTITY_EXCEEDED`; fazlası serbest stoktan alınmaz | Beste (sahip kararı 2026-11-02) | §19.6 |
| Q-05 | MVP-6 sevkiyat satırından `reservationId` hangi alanla gelir? (`inventoryReferenceId` mi, 3.5.0 yeni alan mı) — MOD-0178 Q-M6-2 ile aynı | MVP-6'nın kararı; bu paket yalnız `MovementRequest.reservationId` sunar | MVP-6 CT (Natig) | MOD-0183 revizyonunda; MVP-5 S2 canlıya çıkmadan önce |
| Q-06 | Toplu port (`CreateBatchAsync`) kabul mü? | **KAPANDI — evet**; toplu port hepsi ya da hiçbiri (§8.7) | Beste (sahip kararı 2026-11-02) | §19.6 |
| Q-07 | 0178 rezervasyonları `expiresAt` taşır mı; `reservationType` / kaynak üçlüsü değeri? | 0178 S2 revizyonunun kararı; öneri `expiresAt` yok (MVP-6 sevkiyat rezervasyonu emsali, `DECISION-DRAFT.md` §3b ek not 2) | MVP-1 CT (MVP-5 S2 revizyonu) | 0178 S2 revizyonu |
| Q-08 | Rezervasyona görünür kayıt kodu gerekir mi? | **KAPANDI — hayır** (§8.10) | Beste (sahip kararı 2026-11-02) | §19.6 |

### 19.5 CT kararları

| # | Karar | Öneri |
|---|---|---|
| CT-01 | Registry'ye `MOD-0173-FU03` satırı | **KARAR VERİLDİ** — satır eklendi (`2265cce90`, MVP-1 CT 2026-11-02); ad farkı açık nokta (§19.6) |
| CT-02 | Kapalı hata kataloğu (`InventoryErrorCodes.All`, "no other code without new pack version") için yeni kodların yetkisi (`RESERVATION_NOT_FOUND`, `RESERVATION_NOT_ACTIVE`, `RESERVATION_MISMATCH`, `RESERVATION_NOT_APPLICABLE`, `RESERVATION_QUANTITY_EXCEEDED`) | **KARAR VERİLDİ** — yetki bu FU paketidir; ebeveyn MOD-0173 paketine yalnız DL-1 kabulünde sürüm notu düşülür (MVP-1 CT 2026-11-02) |
| CT-03 | Sözleşme iki adım (1.6.0 DL-1, 1.7.0 DL-2) mı, tek 1.6.0 mı? | **KARAR VERİLDİ — iki adım kalır** (1.6.0 / 1.7.0; MVP-1 CT 2026-11-02) |
| CT-04 | Dilim sırası ve MOD-0178 S2'nin açılma koşulu | **KARAR VERİLDİ** — W-5 = DL-1 + DL-2 CT kabulü; DL-3 MOD-0178 S2 için engel değildir (MVP-1 CT 2026-11-02) |
| CT-05 | Plan satırı (yetenek kimliği, plan MVP) | plan sahibinden alınıp dilim tablosuna yazılır |
| CT-06 | MOD-0176 FU-0176-RES-01 sahibi (MOD-0178 yazıyor) W-5 ile çelişiyor | 0176 paket metni düzeltmesi (XMC-001, 0176 sahibi aynı) |
| CT-07 | XMC-001: `Persistence/Traceability/LotHeldStock.cs` (MOD-0174 / MOD-0175 yolları) DL-1'de kapsam pozisyonlarını güncellemek için değişir | **KARAR VERİLDİ** — sahip izni Beste 2026-11-02: XMC-001, `Persistence/Traceability/LotHeldStock.cs` (MOD-0174 / MOD-0175 dosyası) için verildi — "yalnız ayrılmış (reserved) miktarın tutulan stok hesabına eklenmesi kapsamında, başka değişiklik yok" |

### 19.6 Verilen kararlar (2026-11-02)

Sahip kararları Beste'nin ("hepsi öneri gibi"), CT kararları MVP-1 CT'nin. Hepsi paketin önerisini aynen kabul eder; paket gövdesi
(§8.2, §8.3, §8.7, §8.10, §12) bu davranışı zaten yazıyordu, değişen yalnız kararın kaydıdır.

| # | Karar | Veren | Pakette nerede |
|---|---|---|---|
| Q-02 | Hayır — v1 kapsamı depo + lot; bin pini yok; gerekirse FU-03-BIN | Beste (sahip) | §1, §2, §8.4, §12, §19.3 ASSUMPTION-01, §20 |
| Q-03 | v1'de kapsamı eşleşen her hareket tüketebilir (`inventory.movements.create`) + denetim; daraltma FU (FU-03-CONSUMER) | Beste (sahip) | §8.3, §14, §20, §21 |
| Q-04 | 422 `RESERVATION_QUANTITY_EXCEEDED`; fazlası serbest stoktan alınmaz | Beste (sahip) | §8.3, §8.8 |
| Q-06 | Evet — toplu port `CreateBatchAsync` hepsi ya da hiçbiri | Beste (sahip) | §8.7 |
| Q-08 | Hayır — görünür kayıt kodu yok | Beste (sahip) | §8.10 |
| CT-01 | Registry satırı eklendi (`2265cce90`) | MVP-1 CT | `execution/registries/module-id-registry.md` |
| CT-03 | Sözleşme iki adım kalır: 1.6.0 (DL-1), 1.7.0 (DL-2) | MVP-1 CT | §8.8, §22 |
| CT-04 | W-5 = DL-1 + DL-2 CT kabulü; DL-3 MOD-0178 S2 için engel değildir | MVP-1 CT | §22 |
| CT-02 | Beş yeni hata kodunun (`RESERVATION_NOT_FOUND`, `RESERVATION_NOT_ACTIVE`, `RESERVATION_MISMATCH`, `RESERVATION_NOT_APPLICABLE`, `RESERVATION_QUANTITY_EXCEEDED`) kapalı kataloğa `InventoryErrorCodes.All` eklenme yetkisi bu FU paketidir; ebeveyn MOD-0173 paketine yalnız DL-1 kabulünde sürüm notu düşülür | MVP-1 CT | §8.8, §12, §22 (DL-1 kabulü) |
| Ad | Kanonik paket adı "Inventory Lot & Location Reservation" (künye ve DCP-002 ön kontrolüyle aynı); kayıt satırını CT düzeltir | MVP-1 CT | künye, §19.1 |
| CT-07 | XMC-001 izni: `Persistence/Traceability/LotHeldStock.cs` (MOD-0174 / MOD-0175 dosyası); "yalnız ayrılmış (reserved) miktarın tutulan stok hesabına eklenmesi kapsamında, başka değişiklik yok" | Beste (sahip izni 2026-11-02) | §5, §6, §7, §8.2 (yol 3–4), §22 |

**Açık kalanlar:** Q-01 (FEFO, DL-3 kapısı), Q-05 (MVP-6), Q-07 (MOD-0178 S2 revizyonu); CT-05 (plan satırı), CT-06 (MOD-0176 FU
satırı düzeltmesi). Kayıt satırındaki ad yazımını CT düzeltecek (kanonik ad yukarıda). **Açık nokta (karar değil) — CT-07 ifade
farkı:** paketin bu dosyadaki değişikliği §8.2 yol 3–4'tür: `ApplyAsync` / `ApplyRetestAsync` lot tutulup bırakıldığında lotun
her depodaki `W:` ve `L:` kapsam pozisyonlarının `AvailableOnHand`'ini de günceller. İzin metni ise "ayrılmış miktarın tutulan stok
hesabına eklenmesi" der. İkisinin aynı değişiklik olduğunu CT (ya da sahip) DL-1 başlamadan teyit eder; değilse izin metni ya da
§8.2 yol 3–4 bir sonraki paket sürümünde hizalanır.

## 20. Follow-up Items

- **FU-03-BIN** — bin (lokasyon) pini: rezervasyonun `locationId`'si, `LOCATION_TRANSFER`'da pinin taşınması — Q-02 = **hayır** (2026-11-02); yalnız ileride ihtiyaç doğarsa ayrı sahip kararıyla açılır.
- **FU-03-FEFO** — MOD-0176 revizyonu: FEFO aday kümesi serbest miktarla (Q-01 = a); FEFO-BUNDLE eklemeli `basis`.
- **FU-03-QH** — MOD-0175 ön kontrolünü lot / depo düzeyine indirmek (daha net 409 mesajı); bugün defter bekçisi yakalıyor (§8.6).
- **FU-03-UI** — MOD-0173-FU01 BG-08 rezervasyon çekmecesi (`getReservation` + liste okuması); 7 dil.
- **FU-03-LIST** — `GET /reservations` listesi (kapsam / kaynak süzgeci) — tüketici isteyince.
- **FU-03-CONSUMER** — tüketiciyi daraltma (ör. `consumerModule`) — Q-03 kararı (2026-11-02): v1'de eşleşen her hareket, daraltma bu FU'da.
- **FU-03-403AUDIT** — 403 (`Denied`) denemelerinin servis genelinde denetimi (AUD-001 §3); servis geneli altyapı, bu paketin değil.
- **FU-03-COUNT** — MOD-0181 sayımı: ayrılmış stoğu açıkta bırakan `COUNT_LOSS` reddedilir (§8.2); sayım akışının rezervasyon uzlaştırması MOD-0181 paketinde.
- **FU-03-EAM** — MOD-0201 yedek parça "reservations (maintenance view)" bu rezervasyonu tüketecekse ayrı paket.
- **XMC (başka sahipler):** MOD-0183 revizyonu (`reservationId` gönderimi, Q-05); MOD-0178 S2 revizyonu (portun kullanımı, Q-07).

## 21. Audited Events

Servisin denetim altyapısı **bugün var:** `tests/architecture/audit-ledger/Diten.InventoryService.md` `## İzler` →
`inventory-merkezi-iletim | b | işaret | ICentralAuditDeclaration`; `RequireEnlistment = true`; K2 borcu yok. Merkezi kayıt ad ve gövde taşımaz;
değerler yerel `inventory_audit`'tedir (MOD-0178 §8.8 ile aynı ölçüm). Merkezi kayıt yalnız **başarılı ve yazan** komutta iletilir; **retler**
(AUD-001 §3) yerel `inventory_audit`'e `Outcome Refused` satırı olarak yazılır: işlem geri alındıktan sonra tek ekleme
(`ExpiryPostingPolicy.FlushRefusalAuditAsync` emsali), yazılamazsa cevap değişmez, uyarı logu + `inventory_refusal_audit_failed_total`
(en iyi çaba, sessiz değil — AUD-001 §4.3 "diğer iş kayıtları"). `inventory_audit` yalnız ekleme yapılan, silinmeyen koleksiyondur.

| Komut (= `RequestType`) | `Operation` / `EntityType` | Yol | Merkezi kayıt (iletilen) | Yerel `inventory_audit` (`Action` → alanlar) | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| `CreateReservationCommand` (genişler) | `Create` / `InventoryReservation` (mevcut) | b (K2) | 1 yalnız 201; tekrar 0; ret 0 | 201: `InventoryReservationCreated` → ReservationId, ItemId, SkuId, SkuLevel, WarehouseId, LotId, ChangedQuantity (= Quantity), RemainingQuantity, Status, SourceModule (ReservationType ve kaynak belge / satır rezervasyon belgesinde, aynı işlem); ret: `InventoryReservationRefused` → ErrorCode, RequestedQuantity, UncoveredQuantity, Level, WarehouseId, LotId | — |
| `ReleaseReservationCommand` (şema değişmez) | `LifecycleTransition` / `InventoryReservation` (mevcut) | b (K2) | 1 yalnız 200; 409 / 404 → 0 | `InventoryReservationReleased` → ChangedQuantity (= bırakılan kalan), RemainingQuantity 0, Status, ClosedReason, WarehouseId, LotId | — |
| `PostMovementCommand` (genişler, `reservationId`) | `Create` / `InventoryTransaction` (mevcut) | b (K2) | 1 yalnız 201; tekrar 0; ret 0 | hareketin mevcut defter izi + tüketimde `InventoryReservationConsumed` / `…PartiallyConsumed` (`TransactionId`, ChangedQuantity, RemainingQuantity, tüketen SourceModule); `RESERVATION_*` retinde `InventoryReservationConsumptionRefused` → gönderilen reservationId metni, MovementType, ErrorCode, SourceModule | — |
| `IInventoryReservationPort` (MediatR komutu değil; süreç içi) | çağıran 0178 komutunun bildirimi | b (çağıranın satırı) | çağıran komut başına 1 (enlist çağıranın satırına no-op katılım) | rezervasyon başına `InventoryReservationCreated` / `Released` / `Refused` (aynı alanlar), `ActorUserId` = çağıranın kullanıcısı | — (MOD-0178 S2 Audited Events kendi komutlarını yazar) |
| Rezervasyon süre sweeper'ı (`ReservationExpirySweeperHostedService`, mevcut) | — | — | — | `InventoryReservationExpired` (kapsam alanlarıyla) | mevcut, değişmez: defterin "dört arka plan süpürücüsü" notu |
| `GetReservationByIdQuery`, `GetAvailabilityQuery` | — | — | — | — | sorgu (AUD-001 §2); okuma denetimi gerekmez — §7'nin üç sınıfından hiçbirine girmez (kişisel / özel nitelikli veri yok) |

Kayda yazılmayan alanlar: token, gizli anahtar; rezervasyon kişisel veri taşımaz.

## 22. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | çekirdek: kapsamlı rezervasyon + pozisyonlar + okuma + port + ret izi + INVENTORY 1.6.0 | Owned Objects, Entity Fields, Repo Scope, Servis ve kapsam, Rezervasyon kapsamı ve pozisyonlar, Lot durumu ve karantina etkileşimi, Süreç içi rezervasyon portu, Sözleşme değişikliği (INVENTORY 1.6.0 ve 1.7.0), Durum ve komut tablosu, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Gateway / API Routing Decision, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | rezervasyon kimliğiyle tüketim + yer değiştirme + INVENTORY 1.7.0 | Rezervasyon kimliğiyle tüketim, Ayrılmış stokun yer değiştirmesi, Sözleşme değişikliği (INVENTORY 1.6.0 ve 1.7.0), Durum ve komut tablosu, Validation Rules, Failure Path to Verify, Audited Events | E4 | plan kimliği yok | plan satırı yok |
| DL-3 | FEFO için ayrılmış lot okuma portu (yalnız Q-01 = a) | FEFO kancası ve ayrılmış lotlar | E4 | plan kimliği yok | plan satırı yok |

Kabul ölçütleri: DL-1 → AC-1…AC-19, AC-32, AC-34; DL-2 → AC-20…AC-30, AC-32, AC-33; DL-3 → AC-31, AC-32. DL-1'in önkoşulu CT-07 — sahip izniyle karşılandı (Beste 2026-11-02, yalnız izin kapsamında; §19.6). DL-1 kabulünde ebeveyn MOD-0173 paketine beş yeni hata kodu için sürüm notu düşülür (CT-02). DL-2 ve DL-3 DL-1'e dayanır. Sözleşme bölümü
iki dilimde geçer: DL-1 yalnız 1.6.0 maddelerini, DL-2 yalnız 1.7.0 maddelerini yapar. W-5'in kapanışı ve MOD-0178 S2'nin (DL-4) açılma
koşulu: DL-1 + DL-2 CT kabulü (CT-04). MVP-6'nın gerçek tüketimi (E5, iki modül birlikte) MOD-0183 revizyonunun kabulüdür, bu paketin değil.

## 23. Page Help (UXH-001)

Sayfa yok (`shell: none`) → yardım metni ve tur yok.

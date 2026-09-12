# WORK PACKAGE BRIEF — MVP-5 Warehouse / WMS (0178·0180·0181·0182)

> **Dev-prompt DEĞİL** — Control Tower intake. Branch `feature/mvp5-warehouse` · Gate: 0173 movement freeze sonrası (MVP-2/3 ∥).

## OWNED (SoR)
WarehouseTask(Putaway/Pick/Pack) · ScanEvent · HandlingUnit · Wave(0180) · CycleCount(0181) · BarcodeLabel(0182) · WMS-BUNDLE.

## CONSUMED (frozen/mock)
- INVENTORY `contracts/inventory-bundle.openapi.yaml` (movement post + availability)
- LOCATION `contracts/location.openapi.yaml` (warehouse/bin)
- TRACE `contracts/trace.openapi.yaml` (lot/serial)

## GOLDEN FLOW
receipt → putaway (bin) → locate → pick/wave → pick → pack → confirm → cycle count/adjust. Her fiziksel hareket INVENTORY'ye POST /movements ile post.

## BOUNDARIES (must-not) — KRİTİK
**İKİNCİ BALANCE TUTMA.** Stok gerçeği 0173'te; her hareket INVENTORY contract'ıyla post (WAREHOUSE_TRANSFER/LOCATION_TRANSFER/COUNT_GAIN/COUNT_LOSS). Bin/lokasyon = LOCATION'dan (icat etme). 0173/Location seam'lerine yazma. Sadece kendi path'ler.

## KEY RULE
Idempotent scan/movement · concurrency · bulk/wave partial-failure marker · per-row/server authz.

## AUTHORITY
Report §15/§21 · INVENTORY/LOCATION/TRACE contracts.

## ACCEPTANCE (G4)
putaway→LOCATION_TRANSFER post; pick→issue/transfer post; count fark→COUNT_GAIN/LOSS post; balance mock'ta güncellendi; idempotent scan; consumed contract çağrıları uyumlu.

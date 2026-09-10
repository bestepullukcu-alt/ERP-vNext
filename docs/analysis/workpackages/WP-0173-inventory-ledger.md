# WORK PACKAGE BRIEF — MOD-0173 Inventory Ledger  (Control Tower intake)

> **Bu bir dev-prompt DEĞİL.** Kişinin Control Tower'ı (SOP) bunu ölçüp paketleyip **dev promptunu KENDİ üretir**. Aşağıdaki alanlar CT'nin INTAKE/INSPECT/DoR/PACKAGE girdisidir.

## Kimlik
```
WP ID:   WP-0173-LEDGER-v1
Module:  MOD-0173 Inventory Ledger & Valuation  (greenfield — yeni servis)
MVP:     MVP-1 / W3        Gate: G1 exit          Branch: feature/mvp1-foundation
Owner/SoR: MOD-0173 (INVENTORY-BUNDLE)            Risk: HIGH (shared SoR + valuation + append-only)
```

## OWNED (SoR — tek yazıcı)
InventoryTransaction · MovementType · MovementReason · InventoryDimension · InventoryBalance (projection) · StockStatus · InventoryReservation · InventoryValuation/CostLayer.

## CONSUMED (frozen contract — mock'a karşı, gerçeği bekleme)
- PRODUCT-MASTER  `docs/analysis/contracts/product-master-bundle.openapi.yaml` (itemId/skuId doğrulama; erişilemezse fail-closed)
- LOCATION        `docs/analysis/contracts/location.openapi.yaml` (warehouse/bin)

## PRODUCES (uyulacak frozen contract)
INVENTORY-BUNDLE `docs/analysis/contracts/inventory-bundle.openapi.yaml` (v1) — servis bu contract'ı sağlamalı.

## BOUNDARIES (must-not)
Balance doğrudan edit YASAK (projection). İkinci ürün/stok gerçeği yaratma. Başka modülün DB'sine dokunma. Ürün kimliği icat etme. Protected path/başka seam'e yazma. Contract'ı değiştirme (frozen → eksikse CT'ye blocker).

## AUTHORITY (CT bunları okur)
`inventory-capability-scope-and-dependency-report.md` §4/§14.2 (entity) · §21 (kurallar) · §20.1 (G1 exit) · §0.0 (kararlar: DEC-INV-02/05/06/08/12/13/15/16/17/18). Contract: INVENTORY-BUNDLE OpenAPI.

## KEY INVARIANTS (acceptance'a girer)
Stok değişimi = yalnız POST /movements (append-only, idempotent, REVERSAL — edit yok) · MovementType canonical enum · StockStatus dimension · negative-stock prohibited · Reservation burada / ATP 0172 · Tenant+LE scoped, currency LE base, costing per-LE/product.

## ACCEPTANCE (G1 exit — CT doğrular, agent PASS ≠ CT ACCEPTED)
Opening +100 → transaction persist L3 → balance=100 → reload stabil · Issue -20 → 2. immutable txn → balance=80, ilk movement değişmez · aynı idempotencyKey → 2. posting yok · yetersiz stok → INSUFFICIENT_STOCK · reservation → available=onHand−reserved · Tenant+LE izolasyon + server RBAC + audit/correlationId · response şekilleri INVENTORY-BUNDLE ile uyumlu · evidence E4.

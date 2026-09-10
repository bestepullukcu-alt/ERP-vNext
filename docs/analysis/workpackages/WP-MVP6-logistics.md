# WORK PACKAGE BRIEF — MVP-6 Integrated + Logistics (0183-0187·0190·0192·0147·0148)

> **Dev-prompt DEĞİL** — Control Tower intake. Branch `feature/mvp6-logistics` · Gate: MVP-2..5 stabil (G3 sonrası). En son.

## OWNED (SoR)
Shipment/POD(0183) · Carrier(0184) · Routing/Load(0185) · Reverse Logistics(0186) · Claims(0187) · SupplierPerformance(0147) · SupplierPortal(0148).

## CONSUMED (frozen/mock)
Warehouse (MVP-5 shipment tetiği) · INVENTORY (stok) · Supplier (MVP-2). Event Bus (Platform) — shipment lifecycle event'leri.

## GOLDEN FLOW
warehouse shipment → carrier/load → POD → exception/return/claim; supplier performance feedback döngüyü kapatır. Internal: 0183 → {0185 ∥ 0186 ∥ 0187} paralel.

## BOUNDARIES (must-not)
Source SoR'ları (inventory/PO/shipment) OVERRIDE etme — oku, kendi lifecycle'ını üret. Sadece kendi path'ler; ortak dosyaları integrator ekler.

## AUTHORITY
Report §15/§21 · Warehouse/INVENTORY/Supplier contracts · Event Bus.

## ACCEPTANCE (G5)
shipment→carrier→POD→return/claim akışı; correlationId shipment lifecycle boyunca; source SoR'lara reconciliation; regression.

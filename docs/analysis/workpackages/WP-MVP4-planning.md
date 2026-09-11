# WORK PACKAGE BRIEF — MVP-4 Planning (0188·0189·0190·0191·0192)

> **Dev-prompt DEĞİL** — Control Tower intake. Branch `feature/mvp4-planning` · Gate: MVP-2+3 stabil (G2A/G2B).

## OWNED (SoR)
DemandPlan(0188) · MRPRun/NetRequirement/ReplenishmentProposal(0189) · SafetyStockPolicy(0191) · CapacityPlan(0192) · S&OPPlan(0190) · **DEMAND contract** (CT netleştirir; iskelet §CONTRACT-7).

## CONSUMED (frozen/mock)
- INVENTORY `contracts/inventory-bundle.openapi.yaml` (GET /availability)
- PRODUCT-MASTER `contracts/product-master-bundle.openapi.yaml`
- BOM (MVP-3 contract; mock — manufacturing ise explode). DWH(0063) yoksa → basit forecast + ASSUMPTION.

## GOLDEN FLOW
forecast → (mfg ise BOM explode) → INVENTORY availability'e karşı net → safety-stock/lead-time → requirement → replenishment proposal → onaylı requirement'i 0141'e (PO) HANDOFF.

## BOUNDARIES (must-not)
**PO YARATMA** (SoR=0141, handoff). Stok tutma (availability oku). Reproducible snapshot; idempotent proposal; explainable netting. Sadece kendi path'ler.

## AUTHORITY
Report §15/§21 · INVENTORY/PRODUCT-MASTER/BOM contracts.

## ACCEPTANCE (G3)
forecast→net against INVENTORY availability (mock)→proposal reproducible; idempotent; 0141 handoff çağrıldı (mock).

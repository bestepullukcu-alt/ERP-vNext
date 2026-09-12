# WORK PACKAGE BRIEF — MVP-3 BOM & Routings (0193)

> **Dev-prompt DEĞİL** — Control Tower intake. Branch `feature/mvp3-bom` · Gate: G1 sonrası (MVP-2 ∥).

## OWNED (SoR)
BOM · BOMVersion · BOMComponentLine · Routing · RoutingStep · **BOM contract** (CT netleştirir; iskelet §CONTRACT-6).

## CONSUMED (frozen/mock)
PRODUCT-MASTER `contracts/product-master-bundle.openapi.yaml` (item kimliği). Change Control (0209) yoksa → basit effective-date + ASSUMPTION.

## GOLDEN FLOW
Ürün seç (PRODUCT-MASTER) → BOM version → component/routing → UoM/ref doğrula → approve/effect → GET current effective · explode(item,qty).

## BOUNDARIES (must-not)
Ürün/item kimliği icat (PRODUCT-MASTER SoR). Inventory'e yazma. Başka seam'e dokunma. Sadece kendi path'ler.

## KEY RULE
Versioned BOM (L3) · optimistic concurrency · controlled effective-state. BOM/routing SoR = 0193; ürün kimliği = 0290.

## AUTHORITY
Report §15/§21 · PRODUCT-MASTER contract.

## ACCEPTANCE (G2B)
BOM version create→component→effective→GET current effective doğru; explode → component ihtiyaçları; PRODUCT-MASTER doğrulaması çağrıldı.

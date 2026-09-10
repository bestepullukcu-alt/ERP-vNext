# WORK PACKAGE BRIEF — MVP-1 Traceability & Quality (0174·0175·0176·0177)

> **Dev-prompt DEĞİL** — Control Tower intake. Kişinin CT'si her modül için promptu kendi üretir.
> **Ortak:** Branch `feature/mvp1-foundation` · Authority: contracts/ + report (§4 entity, §21 kurallar, §20 gate) · Gate: G1 içi.

## WP-0174 — Lot/Batch/Serial  (owner TRACE-BUNDLE)
- **PRODUCES:** `contracts/trace.openapi.yaml` (v1)
- **CONSUMED (frozen/mock):** PRODUCT-MASTER (itemId doğrulama)
- **OWNED:** Lot · Serial · Genealogy
- **KEY RULE:** Lot uniqueness = surrogate LotId + business key Tenant+Item+LotNumber (DEC-INV-14); Manufacturer/SupplierLot=attribute; serial-controlled: serial count==quantity
- **MUST-NOT:** INVENTORY balance'a yazma; ürün kimliği icat
- **ACCEPTANCE:** create→get lot; aynı Tenant+Item+LotNumber → LOT_ALREADY_EXISTS; genealogy fwd/bwd; contract-uyum

## WP-0175 — Quarantine/Blocked Stock  (QUARANTINE-BUNDLE)
- **CONSUMED:** TRACE (lot) · INVENTORY (StockStatus dimension)
- **OWNED:** QuarantineHold · ReleaseDecision · Disposition · StockStatusTransition · BlockReason
- **KEY RULE (kritik):** kendi balance TUTMAZ — stok durumunu INVENTORY'de STATUS_TRANSFER movement ile değiştirir. Release: Workflow HARD, e-Sign CONDITIONAL (0022 yoksa e-imzasız + ASSUMPTION)
- **MUST-NOT:** ikinci balance; INVENTORY balance edit
- **ACCEPTANCE:** quarantine→release → INVENTORY status AVAILABLE (STATUS_TRANSFER); audit+correlationId

## WP-0176 — Expiry/FEFO  (FEFO-BUNDLE)
- **CONSUMED:** TRACE (lot expiry) · INVENTORY (balance by lot)
- **OWNED:** ShelfLifeRule · ExpiryPolicy · FEFOAllocationConstraint · FEFOExceptionCase
- **KEY RULE:** FEFO bir allocation constraint üretir (yakın-expiry önce); Pick(0178)/ATP(0172) tüketir. Base dep = TRACE (Demand değil, consumer yön)
- **MUST-NOT:** stok tutma; lot yaratma
- **ACCEPTANCE:** iki lot farklı expiry → constraint yakın-expiry'i önce; exception inbox

## WP-0177 — Recall Readiness (support)  (RECALL-BUNDLE)
- **CONSUMED:** TRACE (genealogy) · Evidence/Docs (0029; 0030 Records yoksa basit persist + ASSUMPTION)
- **OWNED:** RecallCase · RecallDrillPlan · TraceForwardQuery · TraceBackwardQuery · EvidencePack
- **MUST-NOT:** stok/lot yaratma
- **ACCEPTANCE:** lot ver → forward+backward trace; drill plan; evidence pack

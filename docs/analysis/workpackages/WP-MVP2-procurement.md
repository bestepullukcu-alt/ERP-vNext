# WORK PACKAGE BRIEF — MVP-2 Procurement / P2P (0140·0141·0142·0143·0145)

> **Dev-prompt DEĞİL** — Control Tower intake. CT bunu ölçüp promptu kendi üretir.
> Branch `feature/mvp2-procurement` · Gate: **G1 sonrası** (0290+0173 frozen).

## OWNED (SoR)
Supplier (0140) · Requisition/PurchaseOrder (0141) · GRN (0142) · InvoiceMatch (0143) · Sourcing (0145) · **GRN-EVENT** (bu MVP'nin CT'si contract'ını netleştirir; iskelet: wave0-contracts §CONTRACT-5).

## CONSUMED (frozen contract — mock'a karşı)
- PRODUCT-MASTER `contracts/product-master-bundle.openapi.yaml` (itemId/skuId)
- INVENTORY `contracts/inventory-bundle.openapi.yaml` (GRN → POST /movements GOODS_RECEIPT_PO)
- LOCATION `contracts/location.openapi.yaml` (kabul lokasyonu)

## GOLDEN FLOW (acceptance hedefi)
Supplier onay → requisition → PO → GRN → INVENTORY'ye GOODS_RECEIPT_PO post → invoice 3-way match (PO/GRN/invoice).

## BOUNDARIES (must-not)
Shadow stok / stok tutma YASAK (kabul INVENTORY contract'ıyla post). Ürün kimliği / lokasyon icat etme. 0173/0290/Location seam'lerine yazma. Sadece kendi path'ler; gateway/ocelot ortak dosyasını integrator ekler. AP (ödeme)=Treasury/dış → mock/ASSUMPTION.

## AUTHORITY
Report §15/§21 · contracts (PRODUCT-MASTER/INVENTORY/LOCATION).

## ACCEPTANCE (G2A)
PO→GRN→INVENTORY GOODS_RECEIPT_PO post (idempotent, aynı GRN → duplicate yok) → invoice match; consumed contract çağrıları uyumlu; approval/audit; L3.

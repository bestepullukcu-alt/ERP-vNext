# Work Package Briefs — İş Paylaşımı Index

> **Bunlar dev-prompt DEĞİL.** Her biri bir **Work Package Brief** = kişinin kuracağı **Control Tower**'ın girdisidir.
> **Model:** Kişi → kendi **Control Tower**'ını kurar (control-tower SOP'a göre) → CT bu brief'i + contract'ları + repo'yu **ölçer** → SOP adımlarını (INTAKE→INSPECT→DoR→PACKAGE/PROMPT) çalıştırır → **dev promptunu KENDİ üretir** → dev Agent Lane geliştirir → CT doğrular.
> **Önce oku:** [TEAM-PLAYBOOK](TEAM-PLAYBOOK-parallel-no-questions.md) · SOP: `docs/control-tower/control-tower-sop.md`

## Kim hangi Brief'i alır

| MVP | Work Package Brief | Branch | Gate | Ürettiği contract |
|---|---|---|---|---|
| **MVP-1** foundation | [WP-0290](WP-0290-product-master-hardening.md) · [WP-0173](WP-0173-inventory-ledger.md) · [WP-MVP1](WP-MVP1-traceability-quality.md) (0174/75/76/77) | `feature/mvp1-foundation` | başta | PRODUCT-MASTER · INVENTORY · LOCATION · TRACE |
| **MVP-2** procurement | [WP-MVP2](WP-MVP2-procurement.md) | `feature/mvp2-procurement` | G1 | GRN-EVENT |
| **MVP-3** BOM | [WP-MVP3](WP-MVP3-bom.md) | `feature/mvp3-bom` | G1 (MVP-2 ∥) | BOM |
| **MVP-5** warehouse | [WP-MVP5](WP-MVP5-warehouse.md) | `feature/mvp5-warehouse` | 0173 freeze | WMS |
| **MVP-4** planning | [WP-MVP4](WP-MVP4-planning.md) | `feature/mvp4-planning` | G2A+G2B | DEMAND |
| **MVP-6** logistics | [WP-MVP6](WP-MVP6-logistics.md) | `feature/mvp6-logistics` | G3 | shipment/logistics |

## Her Brief'in içeriği (CT bunları kullanır)
- **OWNED (SoR):** modülün sahiplendiği objeler (tek yazıcı)
- **CONSUMED (frozen contract):** OpenAPI ref'leri (`contracts/`) — mock'a karşı
- **BOUNDARIES (must-not):** başka seam'e yazma, shadow stok, kimlik icat
- **AUTHORITY:** hangi rapor bölümü / karar / contract
- **ACCEPTANCE:** gate exit kriteri (§20)
- **RISK / branch / gate**

## Frozen contract'lar
`docs/analysis/contracts/` — INVENTORY · PRODUCT-MASTER · LOCATION · TRACE (OpenAPI, frozen). GRN/BOM/DEMAND/ATP = producer MVP'nin CT'si netleştirir.

## Akış
```
push → pull → iş paylaşımı → her kişi KENDİ Control Tower'ını kurar (SOP) →
CT brief'i ölçer + paketler + DEV PROMPTUNU ÜRETİR → dev agent geliştirir (paralel) →
CT doğrular (gate) → integrator birleştirir + INTEGRATION GATE
```

> **Neden brief, dev-prompt değil:** Dev prompt = CT'nin çıktısı (SOP §17). Bu paket = CT'nin girdisi. Her kişinin CT'si kendi ölçümünü yapıp promptunu üretmeli — böylece SOP disiplini (DoR, no-invent, single-writer, evidence) her lokal CT'de çalışır.

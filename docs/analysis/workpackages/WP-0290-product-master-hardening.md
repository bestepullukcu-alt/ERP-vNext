# WORK PACKAGE BRIEF — MOD-0290 Product Master Hardening  (Control Tower intake)

> **Dev-prompt DEĞİL.** Kişinin Control Tower'ı (SOP) bunu ölçüp promptu KENDİ üretir.

## Kimlik
```
WP ID:   WP-0290-HARDENING-v1
Module:  MOD-0290 Product/Item/SKU Master  (MdmService — Model A MEVCUT/PARTIAL, sıfırdan değil)
MVP:     MVP-1 / W1A+W1B    Gate: G1        Branch: feature/mvp1-foundation
Owner/SoR: MOD-0290 (PRODUCT-MASTER-BUNDLE)  Risk: HIGH (canonical identity, cross-service consumed)
```

## MEVCUT (CT önce ölçer)
`services/Diten.MdmService` — Model A zinciri var: GlobalProduct→ProductDefinitionRevision→Gsku→Lsku→FinishedGood. Bu WP = mevcudu contract'a **tamamlama** (hardening), yeni yazım değil.

## PRODUCES (uyulacak frozen contract)
PRODUCT-MASTER-BUNDLE `docs/analysis/contracts/product-master-bundle.openapi.yaml` (v1).

## SCOPE (contract'ın istediği, mevcutta eksik olanlar)
1. `ProductRef` alanları (owner ProductDefinitionRevision, §8): baseUomId, shelfLifeDays, minRemainingShelfLifeDays, storageCondition, retestDays, lotControlled, serialControlled.
2. `SkuRef`: UoMConversion + GTIN (Gsku global / Lsku·FinishedGood market).
3. `GET /skus/{id}/product` = SKU→GlobalProduct **resolve** (D-SKU-LINK; Gsku'da doğrudan link yok → ProductDefinitionRevision üzerinden).
4. `GET /skus/{id}/uom` = UoMConversion (base↔pack). YENİ.
5. `POST /validate` = bulk kimlik doğrulama.

## BOUNDARIES (must-not)
Model B (`Product`/`mdm_products`) contract'ına DOKUNMA (DEC-INV-17 — inventory B tüketmez). Model B↔A link/migrate ETME (kapsam dışı). CanonicalCode/CodeReservation kimlik-üretim mantığını bozma. Contract'ı değiştirme.

## AUTHORITY
Report §0.0 (DEC-INV-17) · §4/§8 (hardening entity owner) · §12. Contract: PRODUCT-MASTER-BUNDLE OpenAPI.

## ACCEPTANCE (CT doğrular)
5 endpoint contract örnekleriyle şekil-uyumlu · resolve: Lsku ver → doğru GlobalProduct itemId · hardening alanları persist+read · mevcut Model A testleri kırılmadı (regresyon) · Model B'ye dokunulmadı.

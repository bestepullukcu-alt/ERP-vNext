# Denetim defteri — Diten.MdmService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.MdmService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| mdm-merkezi-iletim | b | işaret | IAuditableCommand+IAuditMetadataProvider |
| mdm-kisaltma-gecmisi | c | yazıcı | IProductAbbreviationHistoryRepository |

Ölçüm notları (2026-10-02; `yol = aday` kabul edilmemiş demektir ve o ize giden komut borçta kalır):

* **mdm-merkezi-iletim** — `AuditForwardingBehavior` → `IPlatformAuditForwarder` → `POST /api/internal/audit/append`. En iyi çaba: iletim hatası yutulur.
* **mdm-kisaltma-gecmisi** — `mdm_product_abbreviation_history`: aktör, olay, önce/sonra durum, korelasyon; yalnız ekleme; `…/product-abbreviations/{id}/evidence` ile okunur. CT onayı: 2026-10-02.

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|
| ApproveProductAbbreviationAllocationCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| ApproveProductAbbreviationRetirementCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| CancelProductAbbreviationAllocationCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| InitiateProductAbbreviationCorrectionCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| RejectProductAbbreviationAllocationCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| RejectProductAbbreviationRetirementCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| RequestProductAbbreviationAllocationCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |
| RequestProductAbbreviationRetirementCommand | mdm-kisaltma-gecmisi | ProductAbbreviationWorkflow |

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

- ArchiveBrandCommand
- ArchiveProductCommand
- CreateBrandCommand
- CreateFinishedGoodDraftCommand
- CreateFirstGskuDraftCommand
- CreateFirstGskuDraftFacadeCommand
- CreateGlobalProductDraftCommand
- CreateLskuDraftCommand
- CreateProductCommand
- ReserveCanonicalCodeCommand
- UpdateBrandCommand
- UpdateGskuDraftCommand
- UpdateProductCommand

# Denetim defteri — Diten.ProcurementService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.ProcurementService/src` altındaki
üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| procurement-merkezi-iletim | b | işaret | ICentralAuditDeclaration |

Ölçüm notları (2026-11-10, MOD-0140 DL-3 / BL-501):

> **procurement-merkezi-iletim** — ortak iletici (`Diten.BuildingBlocks.CentralAudit`, kural §5 K4), Inventory ve SupplyChain ile aynı yol `b`: `CentralAuditBehavior` işaretli komut BAŞARIYLA bitince olayı servisin kendi `central_audit_outbox` koleksiyonuna, tedarikçi deposunun Mongo işleminde (`CentralAuditTransaction.EnlistAsync`) yazar; `CentralAuditDispatcher` onu Platform'a iletir. Kayıt bu serviste: `Program.cs` → `AddCentralAudit<ProcurementCentralAuditScope>(…, RequireEnlistment = true)`. İşaretli komutlar MOD-0140 §21'in sekiz komutudur (Create, Update, UpdateSupplierMaster, LinkSupplierToCompany, ChangeRelationshipStatus, SetRelationshipPaymentTerms, Delete, BulkDelete Supplier); `SubmitOnboardingCaseCommand` DL-4'e kadar borçta kalır. MOD-0141…0145 komutları işaretsizdir (bugünkü borç).

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

- ActivateContractCommand
- ApprovePurchaseOrderCommand
- AwardRfxEventCommand
- BulkDeleteContractCommand
- BulkDeleteGrnCommand
- BulkDeleteInvoiceCommand
- BulkDeletePurchaseOrderCommand
- BulkDeleteRequisitionCommand
- BulkDeleteRfxEventCommand
- CaptureInvoiceCommand
- CreateClauseCommand
- CreateContractCommand
- CreatePurchaseOrderCommand
- CreateRequisitionCommand
- CreateRfxEventCommand
- DeleteContractCommand
- DeleteGrnCommand
- DeleteInvoiceCommand
- DeletePurchaseOrderCommand
- DeleteRequisitionCommand
- DeleteRfxEventCommand
- PublishRfxEventCommand
- RecordGrnCommand
- ResolveMatchExceptionCommand
- ReverseGrnCommand
- RunThreeWayMatchCommand
- SubmitBidCommand
- SubmitOnboardingCaseCommand
- SubmitRequisitionCommand
- TerminateContractCommand
- UpdateContractCommand

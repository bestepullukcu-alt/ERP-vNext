# Denetim defteri — Diten.InventoryService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.InventoryService/src`
altındaki üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| inventory-merkezi-iletim | b | işaret | ICentralAuditDeclaration |

Ölçüm notları (2026-10-14, WP-MVP1-AUDIT1 / BL-501):

> **inventory-merkezi-iletim** — ortak iletici (`Diten.BuildingBlocks.CentralAudit`, kural §5 K4): `CentralAuditBehavior` (Building.Blocks) işaretli komut BAŞARIYLA bitince olayı servisin kendi `central_audit_outbox` koleksiyonuna yazar; `CentralAuditDispatcher` onu `POST /api/internal/audit/append`'e (X-Internal-Api-Key, `eventId` ile tekil) iletir. Kayıt bu serviste: `Program.cs` → `AddCentralAudit<InventoryCentralAuditScope>(…, RequireEnlistment = true)`.
> **İşlem bütünlüğü (K2)** — 66 komutun 65'i (2026-10-14 MVP-1 birleştirmesi: R4 `PrepareRecallSettingsChangeCommand`; WP-0177-R5: 7 tatbikat komutu, `RecallDrillRepository`) yazmasını 8 depo sahibinin Mongo işleminde yapar; her `WithTransactionAsync` geri çağrısı olayı aynı işleme yazar (`CentralAuditTransaction.EnlistAsync`): iş verisi ve merkezi kayıt birlikte commit olur ya da hiçbiri. Geri alınan işlem satırı da götürür. `PrepareWorkflowTemplateCommand` Inventory'ye yazmaz (yalnız MOD-0023'e HTTP) → `ICentralAuditOutsideTransaction`, komuttan hemen sonra yazılır. Koruma: `CentralAuditDeclarationTests.Every_transaction_a_command_can_run_in_enlists_the_central_audit_event` (10 geri çağrı; dört arka plan süpürücüsü adıyla dışarıda).
> **Ürün içi izler AYNEN** — stok defteri denetimi (`inventory_audit`), karantina geçmişi, recall vaka zaman çizelgesi, expiry olayları, lot / lokasyon geçmişi: merkezi kayıt bunların yanına eklenir.
> **Kategori** — Platform `AuditCategory`'de envanter / GxP-operasyon değeri yok; hepsi `MasterData` (MVP-3 BOM ile aynı). Modül kodu (`INVENTORY-LEDGER`, `LOT-SERIAL-TRACKING`, `QUARANTINE-BLOCKED-STOCK`, `EXPIRY-FEFO-MANAGEMENT`, `RECALL-READINESS`) ve varlık türü ayırır. Ayrı kategori Control Tower kararıdır.

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

## K2 borcu

Bu liste **yalnız küçülür**. Komutların merkezi kaydı iş verisiyle aynı Mongo işleminde yazılır (yukarıdaki not); işlemi olmayan tek komut (`PrepareWorkflowTemplateCommand`) Inventory verisi değiştirmez. Borç yok.

## Yazan sorgular

Bu liste **yalnız küçülür**.

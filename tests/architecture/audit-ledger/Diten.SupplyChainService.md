# Denetim defteri — Diten.SupplyChainService

Kural: `.antigravity/rules/audit-trail-standard.md` (AUD-001) · Ölçen test: `AuditTrailStandardTests`

Bu dosyada yalnız **bildirim** durur; bir komutun denetlenip denetlenmediği buradan değil, `services/Diten.SupplyChainService/src`
altındaki üretim kodundan okunur. Biçim ve bölümlerin anlamı: [README.md](README.md).

## İzler

| iz | yol | tür | belirteç |
|---|---|---|---|
| supplychain-merkezi-iletim | b | işaret | ICentralAuditDeclaration |

Ölçüm notları (2026-10-21, WP-A1 / E-2 / BL-501):

> **supplychain-merkezi-iletim** — ortak iletici (`Diten.BuildingBlocks.CentralAudit`, kural §5 K4): `CentralAuditBehavior` işaretli komut BAŞARIYLA bitince (istisna yok ve `IsSuccessful` — `Response<T>` ve `SandopResult` için 2xx) olayı servisin kendi `central_audit_outbox` koleksiyonuna yazar; `CentralAuditDispatcher` onu `POST /api/internal/audit/append`'e iletir. Kayıt bu serviste: `Program.cs` → `AddCentralAudit<SupplyChainCentralAuditScope>(…, RequireEnlistment = true)`.
> **İşlem bütünlüğü (K2)** — 17 komutun hepsi yazmasını 7 depo sahibinin Mongo işleminde yapar (Carrier, Claim, Load, Return, S&OP, Shipment birer; Capacity üç: plan, senaryo, değerlendirme — 9 işlem). Her işlem commit'ten önce olayı aynı işleme yazar (`CentralAuditTransaction.EnlistAsync`, yazılan varlığın kimliğiyle): iş verisi ve merkezi kayıt birlikte commit olur ya da hiçbiri; tekrar (idempotent replay) ve ret kayıt bırakmaz. Koruma: `CentralAuditDeclarationTests.Every_transaction_a_command_can_run_in_enlists_the_central_audit_event` (9 işlem; `*Schema.cs` kurulum işlemleri ve `CapacityLeaseStore` arka plan değerlendiricisi adıyla dışarıda).
> **Kapsam** — kiracı ve aktör imzalı token'dan, şirket (LE) MDM'in doğruladığı `X-Legal-Entity-Id`'den: modül kapıları yetkilendirmeden sonra ortak `RequestContext`'e yazar, `SupplyChainCentralAuditScope` yalnız onu okur.
> **Ürün içi izler AYNEN** — sevkiyat geçmişi / denetimi, taşıyıcı, yük, iade, hasar, kapasite ve S&OP denetim koleksiyonları ve modül outbox'ları: merkezi kayıt bunların yanına eklenir.
> **Kategori** — Platform `AuditCategory`'de tedarik zinciri değeri yok; hepsi `MasterData` (Inventory ve MVP-3 BOM ile aynı). Modül kodu (manifest kodları) ve varlık türü ayırır. Ayrı kategori Control Tower kararıdır.

## İstisnalar

| komut | sınıf | gerekçe |
|---|---|---|

## Dolaylı

| komut | iz | üzerinden |
|---|---|---|

## Bilinen borç

Bu liste **yalnız küçülür**. Satır eklemek yasaktır; denetlenen ya da silinen komutun satırı çıkarılır (test bunu zorlar).

## K2 borcu

Bu liste **yalnız küçülür**. Komutların merkezi kaydı iş verisiyle aynı Mongo işleminde yazılır (yukarıdaki not). Borç yok.

## Yazan sorgular

Bu liste **yalnız küçülür**.

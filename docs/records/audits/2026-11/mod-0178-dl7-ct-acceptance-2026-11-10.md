# MOD-0178 DL-7 — Control Tower kabulü

- Tarih: 2026-11-10. CT: MVP-5 WMS CT (oturum `ca7382cf`). Doğrulama: detached worktree `.claude/worktrees/ct-dl7-verify`, HEAD **`94d40e853`**
  (hattın ucu, `feature/sce/mod-0173-inventory-ledger-valuation`; ebeveyn `36648124e`). Ana checkout'a dokunulmadı.
- İncelenen: `94d40e853` — S3 izleme okumaları ve ekranları (İstisna Kutusu, Etkinlik İzi, Entegrasyon İzleme); 82 dosya, +8022 / −9.
- Paket: `MOD-0178-putaway-picking-packing.md` r25 (`b24ff825`, onaylı). Talimat: WP-0178-DL7-01.
- Geliştirici raporu: `mod-0178-dl7-01-rapor.md` (geliştiricinin not klasörü); iddialar aşağıda bağımsız ölçüldü.
- Sonuç: **VERIFIED** — DL-7 yeni kırmızı getirmiyor; kurallar kodda tutuyor; CT'nin iki sabotajı kırmızı. Bir DÜŞÜK (XMC-001 onay kaydı).

## Kurallar (üretim kodu)

| Kural | Kanıt | Durum |
|---|---|---|
| Kiracı + şirket her sorguda | `WarehouseMonitoringReads.cs` `Company<T>()` = `TenantId` + `LegalEntityId`; istisna, etkinlik (görev / outbound / hareket) ve entegrasyon okumalarının hepsi onunla başlar; hareketler defter koleksiyonundan salt okunur (`sourceModule = MOD-0178`) | tutuyor |
| Her eylemde kendi anahtarı | `WarehouseMonitoringController.cs` üç GET: `warehouse.exceptions.read`, `warehouse.activity.read`, `warehouse.integration-monitor.read` (`WarehousePermissions.cs:56-62`, `Dl7` listesi `:85`) | tutuyor |
| Fail-closed (recall / stok okunamaz) | `WarehouseMonitoringQueryHandlers.cs:118-120`, `:135-137` → 503 `InventoryUnavailable` ("Nothing is listed"); "istisna yok" denmez | tutuyor |
| Sayfalama sınırı | `page ≥ 1`, `pageSize 1-200` (`:83-84`, `:233-234`); Etkinlik derinliği `page × pageSize ≤ MaxActivityDepth` → 400 (`:235-236`) | tutuyor |
| Web yalnız Gateway üzerinden | `WarehouseExceptions/Activity/IntegrationController.cs` `Base = "/api/warehouse/…"` (DL-8 `{everything}` rotası), önce anahtar denetimi; `ocelot.json` değişmedi | tutuyor |
| Ship-to yok (Q-10a) | outbound olay `Data`'sı ship-to taşımıyor (okuma yorumu + geliştirici testi `shipTo` üyesi) | tutuyor (kod okuması) |

## Tam takımlar (süzgeçsiz, `94d40e853`; `INVENTORY_TEST_MONGO` yok, 27017 kullanılmadı, AccountKindAcceptanceHost.Tests koşulmadı)

| Takım | CT | Geliştirici | Not |
|---|---|---|---|
| Inventory | **2013 / 0 / 5 atlandı** | 2013 / 0 / 5 | — |
| Gateway | **371 / 0** | 371 / 0 | `Mod0178WarehouseRouteTests` 57 → 60 |
| TenantArchitecture | **222 / 5** | 222 / 5 | `DocsPathGuardTests` ×5 — main tabanı (önceki kayıtlar) |
| TenancyTests | **3 / 0** | 3 / 0 | — |
| Web.Tests | **4403 / 2** | 4403 / 2 | `CapacityPlans` + `SandopPlans` `…no_loading_title` — bilinen taban |
| Platform.Application | 7583 / 1586 | koşulmamış | 1585'i ortam (`DITEN_TEST_MONGOD` verilmedi: "A local mongod binary is required", `OWNED_TEST_MONGO_UNAVAILABLE`); **1 gerçek kırmızı** `RecordCodeTypeCatalogTests.AC16_…` — DL-7 değil, MOD-0181 `.antigravity` `CNT` satırı (`mod-0181-dl2-ct-acceptance-2026-11-10.md` ORTA-1) |
| vitest | CT koşmadı | 13 (main'le aynı adlar) | geliştirici iddiası |

## CT sabotajları (`94d40e853`; her biri tek değişiklik, bayt-eşit geri yazıldı, sha256 önce = sonra)

| # | Sabotaj | Koşulan | Sonuç |
|---|---|---|---|
| S1 | `Company<T>()`'dan `LegalEntityId` yüklemi düşürüldü | Inventory `~WarehouseMonitoring` + `~WarehouseRegistration` (18) | **kırmızı** 2 / 18 |
| S2 | `Exceptions` eyleminden `[HasPermission]` silindi | aynı | **kırmızı** 3 / 18 |

Geliştiricinin 22 sabotajı (başka ağaçta) CT tarafından tekrarlanmadı; yukarıdakiler bağımsız.

## Sapmalar ve bulgular

- **Kabul edilen sapmalar (geliştirici raporu):** (1) iki S3 listesinde eylemsiz salt okunur QuickView — doğrulayıcı istiyor, paket yokluğuna izin
  veriyor; zararsız. (2) `stagingDifference: null` — pakete uyuldu; WMS taslağı dondurulurken işlenecek.
- **DÜŞÜK-1** — `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` (integration-agent alanı) 57 → 60, "DL-4/DL-5 emsaliyle" sahibe
  sorulmadan güncellendi. Değişiklik sıkılaştırıcı ve zorunlu (3 yeni uç); kod değişikliği gerekmez. Yapılacak: sahip onayı bu kayda işlenir,
  integration-agent'a bilgi notu.
- Bir ara ölçüm commit'inde kancalar atlanmış, sonra kancalarla yeniden atılmış (dala girmedi; depoda kanca tanımlı değil) — not, bulgu değil.

## Açık kalanlar

- AC-76 Auth kapısı (S3 okumalarının rollere dağıtımı) — Ali, "sonra tanımlarız" (Beste).
- Canlı E3 ölçümü ve ekran görüntüsü (kiracı oturumu yok).
- WMS taslağının dondurulması; `module-implementation-status.md` satırı modül kabulünde.

VERIFIED

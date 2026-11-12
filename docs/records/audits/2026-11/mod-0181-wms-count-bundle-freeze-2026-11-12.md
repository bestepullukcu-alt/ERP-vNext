# MOD-0181 — WMS-COUNT-BUNDLE 1.0.0 CT dondurma adımı

- Tarih: 2026-11-12. CT: MVP-5 WMS CT (oturum `ca7382cf`); yazım CT'nin alt ajanı, doğrulama CT. Sahip: Beste ("diğer modüllerimiz ile devam
  edelim", 2026-11-12). Desen: MOD-0178 WMS-BUNDLE dondurması (`freeze` kaydı, 2026-11-10).
- Paket: r8 (sha256 `f943ab48…`), onaylayan Ali (GMG-ERP) «onayla» 2026-11-12, PR #416 main'de (`1eb8c8491`). Dondurma kuralı: taslak künyesi
  ("DL-3 CT kabulünden sonra tek yazıcı adımı"; DL-3 VERIFIED 2026-11-10, DL-4 VERIFIED 2026-11-11).
- Sonuç: **VERIFIED** — 1.0.0 FROZEN; DL-5'in üç ucu 1.0.0'da yok (`x-not-in-this-version`), DL-5 kabulünden sonra 1.1.0 yalnız ekleyerek.

## Ne donduruldu
- `docs/analysis/contracts/wms-count-bundle.openapi.yaml` — OpenAPI 3.1.0, `info.version 1.0.0`, `x-status FROZEN`, `x-owner MOD-0181`,
  `x-owner-service Diten.InventoryService`, `x-source-pack` r8 + sha; 22 rota (DL-1 9, DL-2 3, DL-3 9, DL-4 1; `CycleCountsController` 13,
  `CountVariancesController` 9); hata kataloğu 25 kod = `CycleCountErrorCodes`; hiçbir şema `null` kabul etmez.
- Kod gerçeği farkları (taslağa göre; paketle çelişki yok): (a) yazmalarda 400 `INVALID_REQUEST`; (b) `bulkDeleteCycleCounts` 409
  `COUNT_CONFLICT` (`BulkDeleteCycleCountHandler.cs:88`); (c) `getCycleCountDashboard` 503 `COUNT_INVENTORY_UNAVAILABLE`
  (`GetCycleCountDashboardHandler.cs:52`); (d) `CountVariance` iki kapalı satır şemasının `oneOf`'u (Count / Manual); (e) taslak §3.10'un
  "null" dediği `accuracyPercent` / `manualCountMovements` kabloda yazılmaz (kural 4).
- Taslak künyesi: DONDU, r7 → r8; DL-5 satırları ön taslak. `docs/analysis/contracts/README.md` satırı.
- Testler: `CycleCountDraftContract` → `CycleCountContract` (+ `CycleCountContractHandler`; cevaplar dondurulmuş YAML'a karşı); yeni
  `WmsCountBundleContractTests` 8 olgu (FROZEN başlık; rotalar ↔ denetleyiciler + `[HasPermission]`; yazma operationId'leri = `Op*` sabitleri;
  DL-5 1.0.0'da yok; katalog = `CycleCountErrorCodes`; istek üyeleri = `CycleCountRequestReader`; `null` yok; **16 enum listesi üretim
  değer kümesine iki yönlü eşit** — tek istisna `integrityReason` (kodda enum değil, `CycleCountPostingKind` adlarıyla sabitlendi)).
- Değişmeyenler: üretim kodu, `ocelot.json`, `.antigravity/**`, `wms-bundle.openapi.yaml`, `warehouse-outbound.openapi.yaml`, paket.

## Ölçüm
| Ne | Sonuç |
|---|---|
| Inventory tam (ajan, süzgeçsiz; enum testinden önce) | 2114 / 0 / 5 atlandı |
| Inventory tam (CT, süzgeçsiz; son ağaç; `INVENTORY_TEST_MONGO` yok, 27017 yok) | **2115 / 0 / 5 atlandı** |
| `FullyQualifiedName~CycleCount` (son ağaç) | 176 / 0 |
| TenantArchitecture (CT) | 224 / 5 — `DocsPathGuardTests` ×5 (main tabanı) |

## Sabotajlar (her biri bayt-eşit geri)
| # | Sabotaj | Sonuç |
|---|---|---|
| A-a | `CycleCount.number` silindi | kırmızı — 92 sayım API testi (`ContractViolationException`) |
| A-b | `CycleCountStatus` `Released` → `Releasd` | kırmızı — 77 sayım API testi |
| A-c | `/cycle-counts/{countId}/submit` silindi | kırmızı — rota / operationId / katalog testleri |
| A-d | retry `x-permission` değiştirildi | kırmızı — `Every_served_cycle_count_route…` |
| A-e | katalogdan `COUNT_LINE_DUPLICATE` silindi | kırmızı — `The_error_catalogue…` |
| CT-F1 | dashboard `accuracyPercent` deseni iki ondalık | kırmızı — `AC40_the_dashboard_counts…` |
| CT-F2 | `CycleCountEventKind`'den `IntegrityResolved` silindi | **ilk ölçümde YEŞİL** (enum kodla eşlenmiyordu) → enum eşitlik testi eklendi → **kırmızı** (`Every_enum_list_equals_the_production_value_set_both_ways`) |
| A-f | üretim `CycleCountEventKind`'e geçici `Foo` | kırmızı — aynı enum testi |

## Notlar
- `CycleCountEntryTests.cs:12` yorumu hâlâ "draft" diyor (davranışsal değil; DL-5'te düzeltilir).
- 1.1.0 (DL-5) CT adımıdır: DL-5 kabulünden sonra üç uç yalnız eklenir; 1.0.0 fikstürü bayt-eşit korunur (MOD-0178 1.1.0 emsali).

VERIFIED

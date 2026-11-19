# MOD-0181 — WMS-COUNT-BUNDLE 1.1.0 CT dondurma adımı (DL-5, eklemeli)

- Tarih: 2026-11-12/13. CT: MVP-5 WMS CT (oturum `ca7382cf`); yazım ve ölçüm CT'nin alt ajanı (izole worktree, detached `9f2fb3491`),
  doğrulama CT. Desen: WMS-COUNT-BUNDLE 1.0.0 dondurması (`mod-0181-wms-count-bundle-freeze-2026-11-12.md`) + MOD-0178 WMS-BUNDLE 1.1.0
  eklemeli emsali (fikstür `wms-bundle-v1.0.0.openapi.yaml`, `WmsBundleContractTests`).
- Taban: `9f2fb3491` (MVP-1 hattı; MOD-0181 DL-5 + FIX-01 `ebdc3af2b` + lot testi `9f2fb3491`). Paket r8 (sha256 `f943ab48…`).
- Sonuç: **VERIFIED** — 1.1.0 FROZEN, 1.0.0'a göre yalnız ekleme; 1.0.0 dosyası bayt-eşit fikstür.

## Adım 1 — FIX-01 lot testi doğrulaması
Önceki CT ölçümünde (`ebdc3af2b`) sabotaj (b) — `CountEvidenceDocumentBuilder.cs` `Lot()` içindeki iki "okunamadı" dalı `lotId` yazdırır —
YEŞİL kalmıştı. `9f2fb3491`'de yeniden koşuldu: **kırmızı** — `CycleCountEvidenceTests.FIX01_a_lot_is_its_number_and_an_unreadable_lot_is_the_word_never_the_id`
(1 / 12, `~CycleCountEvidence`). Bayt-eşit geri alındı (sha256 önek `9e15b4d0c68c` → `0e220fc7b9b2` → `9e15b4d0c68c`).

## Ne donduruldu
- `docs/analysis/contracts/wms-count-bundle.openapi.yaml` — `info.version 1.1.0`, `x-status FROZEN`; 1.0.0 + yalnız ekleme:
  - üç operasyon (`CountEvidenceExportsController` 3 → 25 operasyon): `createCountEvidenceExport` (`POST /cycle-counts/{countId}/evidence-exports`,
    `cycle-count.evidence.export`; 201 / replay 200; 400 `INVALID_REQUEST`, `IDEMPOTENCY_KEY_REUSED`; 404 `COUNT_NOT_FOUND`; 409
    `COUNT_INVALID_STATE`), `listCountEvidenceExports` (`GET /count-evidence-exports`, `countId?`, `page`, `pageSize`; 400 `INVALID_REQUEST`),
    `getCountEvidenceExportFile` (`GET /count-evidence-exports/{exportId}/file`; 200 dosya `application/pdf` | `text/csv`, zorunlu
    `X-Content-SHA256` + `Content-Disposition`; 404 `COUNT_EVIDENCE_NOT_FOUND`; 500 `COUNT_EVIDENCE_INTEGRITY`); ikisi `cycle-count.evidence.read`;
  - şemalar: `CountEvidenceExport` (kapalı, `null` yok, 13 zorunlu üye), `CountEvidenceExportPage`, `CreateCountEvidenceExportRequest`
    `{idempotencyKey, format}`, `CountEvidenceFormat` (Pdf, Csv), `CountEvidenceStatus` (Ready, Failed), `CountEvidenceLanguage` (7 dil);
    parametreler `ExportId`, `AcceptLanguage`; cevap `EvidenceExport`;
  - katalog 25 → 27: `COUNT_EVIDENCE_NOT_FOUND` (404), `COUNT_EVIDENCE_INTEGRITY` (500 — katalogun tek 500'ü);
  - `x-implemented-by-slice` DL-5; `x-not-in-this-version` kaldırıldı; `x-changelog` 1.1.0 satırı; `info.description`'a 1.1.0 paragrafı
    **eklendi** (1.0.0 metni olduğu gibi önek kalır).
- Dil kuralı kod gerçeğinden (CT DÜŞÜK-3): `Accept-Language` q'ya göre sıralı (q yoksa 1, eşitte başlık sırası) ilk etiketin birincil alt etiketi
  (`-` / `_` öncesi, küçük harf), 7 dil dışı → `en`; PDF `en|tr|fr|es|ru` dışı → `en` + not (`CountEvidenceExportsController.cs:42`,
  `CycleCountEvidenceText.Normal` / `PdfLanguage`). Taslak §3.11 lafzı buna hizalandı.
- Fikstür: `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Contract/Fixtures/wms-count-bundle-v1.0.0.openapi.yaml` = `abaa7b8c7`
  blob'u (LF sha256 `0edae306ab33a8f73df8e5d8298095e38d6cfb83a387f928cfbdb0a1f869d6fc`).
- Testler:
  - `WmsCountBundleContractTests`: DL-5 hariç tutma seti (`Dl5OperationIds` hariç tutma rolü, `Dl5Controller`, `Dl5Permissions`,
    `Dl5ErrorCodePrefix`) **kaldırıldı**; rota 13 + 9 + 3 = `Implemented` tam; yazma `Op*` tam (15); katalog = `CycleCountErrorCodes` tam (27);
    hata durumu deseni 500'ü yalnız `COUNT_EVIDENCE_INTEGRITY` için kabul eder; istek üyeleri `CountEvidenceExportsController.ExportMembers`'i
    de kapsar; enum eşitliği üç evidence enum'unu kapsar (`Dom.CountEvidenceFormat`, `Dom.CountEvidenceStatus`, `CycleCountEvidenceText.Languages`);
    başlık 1.1.0. Yeni: `The_v1_0_0_fixture_is_the_frozen_1_0_0_file_byte_for_byte`, `The_1_1_0_change_is_additive_only_the_DL5_evidence_pack`
    (adlı eklemeler çıkarılınca 1.0.0 ile veri olarak eşit), `The_DL5_export_record_is_closed_with_13_required_members_and_the_file_answer_is_declared`,
    `Every_operation_is_in_one_slice_and_DL5_is_the_evidence_pack` (eski `The_DL5_evidence_operations_are_not_in_1_0_0` yerine).
  - `CycleCountContract` / `CycleCountContractHandler`: dosyanın JSON dışı cevabı (dosya) medya türü + dosyanın `required` işaretlediği
    başlıklar + `X-Content-SHA256` = baytların SHA-256'sı; 4xx/5xx'te katalog kodunun durumu ve operasyonun `x-error-codes` listesi dondurulmuş
    dosyadan denetlenir. `CountEvidenceDraftContract.cs` **silindi** (test içinde kendi şema kopyası kalmadı — Çekirdek 10);
    `CycleCountEvidenceTests` `CycleCountContractHandler` kullanır.
- Belgeler: `docs/analysis/contracts/README.md` 1.1.0 satırı; taslak künyesi (DONDU 1.0.0 + 1.1.0), §3.11 dil lafzı, §4.4 / §5.3 başlıkları.
- Değişmeyenler: üretim kodu, `ocelot.json`, `.antigravity/**`, `wms-bundle.openapi.yaml`, paket.

## Taslaklardan farklar (önceki alt ajanın `freeze110.py` / `CycleCountContract.cs`)
- `freeze110.py`: 1.0.0 açıklamasındaki DL-5 cümlesi yeniden yazılmadı; 1.1.0 paragrafı sona **eklendi** (1.0.0 açıklaması önek → test bunu
  sabitler; (e) "25 kod" ifadesi 1.1.0 paragrafında "27 kod" olarak güncellenir). `createCountEvidenceExport` özetindeki sıra koda göre
  düzeltildi (UUID olmayan id → 404, `format` denetimi anahtar aramasından önce). `UpdateCycleCountSettingsRequest` deseni iki kez geçtiği için
  yama çapası düzeltildi.
- `CycleCountContract.cs`: `response.Headers.Contains("Content-Disposition")` .NET'te `InvalidOperationException` (yanlış başlık türü) atıyordu
  → 9 evidence testi kırmızıydı; `Headers.NonValidated.Contains` ile düzeltildi.

## Ölçüm
| Ne | Sonuç |
|---|---|
| `FullyQualifiedName~CycleCount` (son ağaç) | 191 / 0 |
| Inventory tam (süzgeçsiz; `INVENTORY_TEST_MONGO` yok, 27017 yok) | **2130 / 0 / 5 atlandı** (toplam 2135) |
| TenantArchitecture.ArchitectureTests | 224 / 5 — yalnız `DocsPathGuardTests` ×5 (main tabanı) |

## Sabotajlar (her biri ayrı, bayt-eşit geri; süzgeç `~CycleCount|~Authorization`)
| # | Sabotaj | Sonuç |
|---|---|---|
| 1 | 1.1.0'dan `CountEvidenceExport.sha256` silindi | kırmızı — 12 (13 üye testi + 11 evidence API testi, `ContractViolationException`) |
| 2 | 1.0.0 fikstüründe bir bayt (`openapi: 3.1.0` → `3.2.0`) | kırmızı — 2 (fikstür bayt testi, eklemelilik testi) |
| 3 | `COUNT_EVIDENCE_INTEGRITY` katalogdan silindi | kırmızı — 2 (`The_error_catalogue_is_exactly_CycleCountErrorCodes`, eklemelilik testi) |
| 4 | evidence controller `File` eyleminden `[HasPermission]` | kırmızı — 4 (`AuthorizationGuardTests`, `AC7_the_manifest…`, `Every_served_cycle_count_route…`, `AC44_each_evidence_endpoint…`) |
| 5 | üretim `CountEvidenceFormat`'a `Xlsx` | kırmızı — 1 (`Every_enum_list_equals_the_production_value_set_both_ways`) |

## Notlar
- Sabotaj 3'te bütünlük API testi (`FIX01_a_stored_file_whose_bytes_changed…`) yeşil kalır: handler katalog dışı bir kodu operasyon listesine
  karşı denetlemez (1.0.0 davranışı; yeniden kullanılan `InventoryErrorCodes` kodları da katalog dışıdır). Katalog eşitliği ve eklemelilik testi
  yakalar.
- Push yok; commit'ler yerel (MVP-1 hattı, detached).

VERIFIED

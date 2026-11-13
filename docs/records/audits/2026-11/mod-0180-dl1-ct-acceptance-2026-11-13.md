# MOD-0180 Dalga Planlama — DL-1 CT kabul kaydı (2026-11-13)

- Hedef: DL-1 arka uç, uç `9ed90ceca` (MVP-1 hattı); commit'ler 438d4930c, 2de9b812a, 9cb21206a (taslak), 04ea3d67b, a9c221ad2,
  fa054947a, 9ed90ceca. Paket r11 (226ab7f9, Ali onaylı e12bf26c8); WP-0180-DL1; CT kararları
  `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md`.
- Doğrulayan: MVP-5 CT, bağımsız doğrulama alt ajanı a6f5e468eef38676b; ayrık worktree `C:\Users\Ali\erp-vnext-wt-ct-0180-verify`
  (9ed90ceca), main karşılaştırması `C:\Users\Ali\erp-vnext-wt-ct-0180-main` (8773562d4).

## Ölçüm (CT'nin kendi koşusu)

| Takım | Sonuç |
|---|---|
| InventoryService (filtresiz) | 2218 = 2213 geçti / 1 kırmızı / 4 atlandı — kırmızı `RecallCustomerPageTests.Customers_page_by_page_within_3_s` (3,8 s; eşzamanlı Web koşusu), tek başına 3/3 yeşil, main'de yeşil: 0180 dışı titrek |
| Gateway | 375/375 |
| Diten.Web.Tests (filtresiz) | 4542 = 4537 geçti / **5 kırmızı** — 3'ü main'de de kırmızı (CapacityPlansPageTests, SandopPlansPageTests, ReturnDetailsBehaviorTests; 0180 dışı); **2'si 0180'in açtığı** (D-1) |
| vitest | ölçülmedi (node_modules yok); geliştiricinin label-ledger 28/28 beyanı doğrulanmadı |

Sabotaj (bağımsız, 8): S-3a (TenantId yüklemi), S-5a ([HasPermission]), S-7 (Version CAS), S-9 (EnlistAsync), S-5e (X-17 dışlama),
S-55c (Gateway 88), S-12 (öneri dışlaması), S-DRAFT (taslak anahtarı) — hepsi beklenen testi kırmızıya çevirdi, sha256 eşit geri alındı,
`git status` boş. Taslak–controller eşitlik testi taslak dosyasını gerçekten okuyor (`WaveDraftContract.cs:293-302`).

Kod: kiracı + şirket yüklemi her sorguda (`WaveRepository.cs:42-43`), kiracılar arası 404, gövde / sorguda kapsam alanı 400; 14 eylemde
doğru anahtarla [HasPermission]; PUT / DELETE yok; If-Match zorunlu, Version CAS, tekrarsızlık anahtarı; 6 komut ICentralAuditDeclaration;
WAV kayıt kodu; 0178 koleksiyonları yalnız Find; XMC dokunuşları yalnız X-6, X-9, X-10 (117→123, seen 14→15), X-11, X-14 (73→87),
X-15, X-17 — hepsi eklemeli ve onaylı. CT taslak kararları kodda uygulanmış.

## Bulgular

- **D-1 (orta):** manifest DL-1'de sayfasız kaydediliyor (`WavePlanningManifestProvider.cs:30`, `Pages: []`), Web nav bekçisinin
  istediği `Nav.Module.WAVEPLANNING` (SharedResource 7 dil) ve `NavManifestL10nGuardTests.cs:189-195` `PagelessUntil` listesine
  `WAVE-PLANNING` girdisi yok → `NavManifestL10nGuardTests.Every_manifest_module_has_a_Nav_Module_key_in_all_seven_languages` ve
  `…Every_manifest_provider_in_the_repo_is_parsed_and_yields_a_module_code_and_pages` kırmızı (main'de yeşil). Emsal MOD-0181 DL-1
  ORTA-1, `7bd8688a7` (WP-0181-DL1-FIX-01). Web test dosyasına dokunduğu için XMC-001 sahip onayı gerekir.
- **D-2 (düşük):** karar (9) "üyelik kapanışları State + Version koşullu" der; `RemovePlannedOrdersAsync` (`WaveRepository.cs:211-221`)
  yalnız State=Planned koşuluyla yazar; aynı işlemdeki dalga CAS'ı yarışı korur. DL-2'de lafza hizalanır ya da karar notu.
- **D-3 (bilgi):** S-7 tek başına yalnız depo düzeyi AC-7'yi kırar; HTTP yarış testinde olay Seq tekil indeksi ikinci bekçi.
- **D-4 (bilgi):** `WaveDraftContract.cs` JSON şemaları taslaktan elle türetilmiş kopya; dondurmaya (DL-5 sonrası) kadar kayma riski.
- **D-5 (düşük):** `fa054947a` konu satırı U+FEFF ile başlar (CT kararı: kalsın).
- **D-6 (bilgi):** X-14 satırını integration-agent'ın yazdığı koddan doğrulanamaz.
- Açık noktalar (geliştirici raporu a–e) DL-2'ye taşındı: bakım indeksi, EventSeq, releasingOrderCount, ara commit'lerin tek tek ölçülmemesi.

## Karar: KOŞULLU

Arka uç kabul edilir. Dilim kapanışı D-1 düzeltmesine bağlıdır (FIX WP; XMC-001 onayı). D-1 kapanınca VERIFIED. MOD-0182 DL-1 başlangıç
sayıları: Gateway 87, merkezi denetim 123 / seen 15.

## VERIFIED (2026-11-13, FIX-01 `3e31bfde9`)

- D-1 kapandı: `Nav.Module.WAVEPLANNING` yedi dilde, `PagelessUntil` `WAVE-PLANNING` (DL-4'e kadar); guard 8/8.
- CT tam koşu, ayrı ağaç `C:\Users\Ali\erp-vnext-wt-ct-0180-verify` (ayrık, `3e31bfde9`): Diten.Web.Tests filtresiz 4546 = 4543 geçti /
  3 kırmızı — yalnız main'de de kırmızı olan üçü (CapacityPlansPageTests, SandopPlansPageTests, ReturnDetailsBehaviorTests). 0180'in açtığı
  kırmızı kalmadı. Bu koşu MOD-0178-FU01 DL-1 Web değişikliklerini de kapsar (hat dalında `3e31bfde9` öncesi).
- D-2 (düşük) DL-2'ye taşındı. Hat CT zincirine alındı; yeni etiket `ct/line-ff-2026-11-13i`.

## Karar: VERIFIED — MOD-0180 DL-1 kapandı. MOD-0182 DL-1 başlar (Gateway 87, merkezi denetim 123 / seen 15).

# MOD-0181 DL-2 — Control Tower kabulü

- Tarih: 2026-11-10. CT: MVP-5 WMS CT (oturum `ca7382cf`). Doğrulama: detached worktree `.claude/worktrees/ct-dl7-verify`, HEAD `94d40e853`
  (DL-2'nin üç commit'i `7e75c22a7`, `967478ad9`, `96e25bf9b` bu ağacın altında; üstündeki commit'ler belge + MOD-0178 DL-7). Ana checkout'a
  dokunulmadı.
- Paket: `MOD-0181-cycle-counting.md` (onaylı). Talimat: WP-0181-DL2-01 (AC-14…AC-20, AC-52 (b) / (c) DL-2 yarısı).
- Sonuç: **VERIFIED (DL-2 kodu)** — DL-2 yeni kırmızı getirmiyor. Ancak hattın ucunda CT'nin kendi `.antigravity` commit'inden doğan bir kırmızı var
  (ORTA-1); hat PR'ından önce kapanmalı.

## Ölçülenler

- Merge: `081edf122` (origin/main `084971b1e`) hatta.
- Kör sayım kapsamı paketle karşılaştırıldı: §8.2 ve AC-15 yalnız `expectedQuantity`, `expectedAtCount`, `varianceQuantity` (ve submit sonrası
  hareket farkı) alanlarını gizler. `movementDuringCount` / `snapshotDrift` bayraklarının görünmesi paketle uyumlu — bulgu değil; DL-4 ekranında
  sahip isterse ayrıca gizlenebilir (not).
- Tam takımlar (`94d40e853`, süzgeçsiz): Inventory **2013 / 0 / 5**, Gateway **371 / 0**, TenantArchitecture **222 / 5** (`DocsPathGuardTests`
  ×5 main tabanı), TenancyTests **3 / 0**, Web.Tests **4403 / 2** (bilinen taban). Ayrıntı: `mod-0178-dl7-ct-acceptance-2026-11-10.md`.
- Geliştiricinin 12 sabotajı (AC-15 ×3, AC-16 ×2, AC-17, AC-18 ×2, AC-19, AC-20, AC-52 b / c) — 12 / 12 kırmızı iddiası; CT bu turda tekrarlamadı.

## Bulgular

- **ORTA-1 (CT'nin kendi commit'i, DL-2 değil)** — `Diten.Platform.Application.Tests.RecordCodes.RecordCodeTypeCatalogTests.AC16_the_catalogue_holds_every_automatic_row_of_RCS001_section_3_and_PLN_and_nothing_else`
  kırmızı: `Expected [… "BNK", "CNT", "CPS" …]`, `Actual [… "BNK", "CPS" …]`. Test `.antigravity/rules/record-coding-standard.md` §3'teki her
  `OTOMATİK` satırın Platform `RecordCodeTypeCatalog`'da olmasını ister; `CNT` satırını `99ea4c5c1` (WP-MOD0181-ANTIGRAVITY-ROWS) ekledi, katalog
  girişi ise MOD-0040-FU02 DL-8'in işi (`MOD-0181-cycle-counting.md:563`). Main'de yok (satır yalnız hatta) → hat PR'ında Platform CI kırmızı olur.
  Seçenekler: (a) testin adlı istisnalarına "`CNT` — kural önde, katalog FU02 DL-8'de" satırı (CTR → AGR emsali; Platform testi, XMC-001);
  (b) `CNT`'yi şimdi `RecordCodeTypeCatalog`'a eklemek (FU02 DL-8 kapsamını öne çeker); (c) `.antigravity` satırını DL-8'e kadar geri çekmek.
  CT önerisi: (a). Sahip kararı bekleniyor. Not: planlı AS-2 (MOD-0178 geçici önekleri RCS-001 §3'e) aynı testi aynı nedenle kırar — AS-2 WP'sine
  girdi olarak eklenmeli.
- Platform takımının diğer 1585 kırmızısı ortam (`DITEN_TEST_MONGOD` verilmedi); `RecordCodeTypeCatalogTests`'in kalan 12 testi yeşil.

## Açık kalanlar (geliştirici raporundan, DL-3 / DL-4'e)

- Pano (`GET …/dashboard`) DL-4'te kör sayım kuralıyla ölçülecek.
- AC-52 (b) / (c) DL-3 yarısı (409 `COUNT_SNAPSHOT_DRIFT`, yeniden sayım).
- `cycle-count.variances.read` manifest satırı DL-3.
- Seri kontrollü satırın tam sayı kuralı ve eşzamanlı giriş yolu testle ölçülmedi.
- DL-6 `Mod0181CycleCountRouteTests` (integration-agent).

VERIFIED (DL-2 kodu) — ORTA-1 açık

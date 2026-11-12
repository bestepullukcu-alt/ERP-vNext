# MOD-0175-FU01 DL-3 FIX-01 — CT kabulü (üç test boşluğu)

- Tarih: 2026-11-12. CT: MVP-1 CT (oturum `ca7382cf`). Talimat: WP-0175-FU01-DL3-FIX-01. Sahip: Beste.
- Kaynak: `mod-0175-fu01-dl3-ct-acceptance-2026-11-12.md` (KOŞULLU, ORTA-1…3). Paket r6 (`54ccc367…`) değişmedi.
- Hedef: hatta `2e5104d83` (yalnız `SourceReversalGuardTests.cs`, +244; üretim kodu değişmedi). Ölçülen ağaç `809aeb504` (= `2e5104d83` +
  MOD-0040-FU02 DL-2; Inventory dosyasına dokunmaz).
- Sonuç: **VERIFIED** — DL-3 kabulü KOŞULLU'dan VERIFIED'a geçer. ACCEPTED (DL-1, DL-2, DL-3) kod main'e girince.

## Ölçüm
- Inventory tam takım (süzgeçsiz, `INVENTORY_TEST_MONGO` yok, 27017 yok): **2107 / 0** (5 atlanan, tabanla aynı).
- Bağımsız inceleme (salt-okunur): bulgu yok.
  - ORTA-1: Recall REVERSAL → 422 `OwnMovementReversed`, iddia hold numarasıyla, stok QUARANTINE'de (`AC32b`).
  - ORTA-2: R5a → `DecisionInProgress` (`AC31f`), R5 ApplyFailed → `DecisionInProgress` (`AC31g`), R10 → `LinesPosted` (`AC31h`),
    R11 → `Decided` (`AC31i`); her vakada ret satırının `Rule`'u ölçülüyor.
  - ORTA-3: zaten geri alınmış kaynağın ikinci REVERSAL'ı `ALREADY_REVERSED`, ret satırı yok, bekçi çağrısı 0; 201 ve 422'de istek başına
    tam bir çağrı. Sayan sarmalayıcı üretimdeki `QuarantineReversalPrecondition` kaydını sarıyor — bekçi Allow'a sabitlenmedi (§16).

## CT sabotajları (her biri bayt-eşit geri; `git status` temiz)
| # | Sabotaj | Sonuç |
|---|---|---|
| CT-6 | `CauseOf`: R5a artık `DecisionInProgress` değil | **kırmızı** — `AC31f_the_source_of_a_hold_whose_decision_changed_the_lot…` |
| CT-7 | `CauseOf`: `QuantityChanged` → Allow | **kırmızı** — `AC31c_the_source_of_a_blocked_record_that_lost_quantity…` |

Geliştirici raporu CT'ye ulaşmadı; kabul kod, testler ve CT ölçümüyle yapıldı.

VERIFIED

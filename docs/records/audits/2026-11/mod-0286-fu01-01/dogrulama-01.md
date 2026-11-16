# PSS-UI-FU01-01 · bağımsız doğrulama 01

- İş: MOD-0286-FU01 DL-1 (sayfa JS başvuru eşlemesi, okuyucu, FU-19 satırları)
- Doğrulanan commit: `f4950c3777bf43c52630df9b41a3ae04fe6fc0f2` (taban `c412e40efe70c61f3ff74e8d0de07b15f91042d0`, dal `feature/pss/pss-ui-fu01-01`)
- Doğrulayıcı: işçiden farklı alt ajan, Claude Opus 5.5 · high, salt okunur; 2026-11-16
- Paket: rev 04, sha256 `d970554beef0b5e111918647effd1503c98dfb1a5ded1c84a3614ffda15eae31` (commit'te ölçüldü)
- Kanıt betiği sha256: `224d0c3a1e1b530ee6b26581fb7cf2432e3347d99c8906618445332f025ad409` (c412e40ef ile aynı)

## Karar: KOŞULLU (kritik 0, engelleyici 0, diğer 4)

Kod ve tabanda kusur yok. Koşul: AC-12 PR'dan sonra kule tarafından `--ci-kaydi` ile yeni bir dizinde kapatılır;
işçi raporundaki iki eksik bu kayıtta tamamlanır.

## Ölçümler

| # | Ölçüm | Sonuç |
|---|---|---|
| 1 | Kapsam: yalnız `scripts/uic_changed_pages.py`, `tests/ui-compliance-baseline.tsv`; tabanda silinen 0; 1 commit; `git diff --check` 0 | geçti |
| 2 | Bağımsız kanıt koşusu (ayrı klon, 1015 s): AC-1…11 ve AC-13 geçti, AC-12 kaldı (CI kaydı yok), `zorunlu geçti 12 / 13`, çıkış 1 | beklenenle aynı |
| 3 | Self-test (koşu içinde): 163/163, `PASS FU01-` 70 satır; FU01-68/69/70 gerçek geçici depo koşusu | geçti |
| 4 | Taban: FU-19 22 satır, 92 eşleşme; `reason` 22/22 birebir; owner 22/22 karar TSV'siyle aynı; sona eklenmiş | geçti |
| 5 | `FU01_REF_PATTERNS` düz atama (`:99`), değerler paket bloğuna bayt bayt eşit, tek kopya; yeni import yok; CI kipinde tüm sayfa yolu yok | geçti |
| 6a | Kayıtlı koşu tek seferlik; `verify_record` sorunsuz, 241/241 artefakt sha256 uyuşuyor; kule kendi dalında `run_record.py verify` çıkış 0 | geçti |
| 6b | 1 commit (≤4), 2 tam koşu (≤3) | geçti |
| 6c | `AC-12 FARK` yok | geçti |
| 6d | İşçi raporu kanıt betiği sha'sını anmıyor (yukarıda verildi); kırmızı koşu olmadı, deneme ilk seferde 12/13 | eksik → bu kayıtta tamamlandı |
| 6e | J sayfanın kendi klasörüne taşınınca K için `başvuru eklendi` yok (FU01-70) | geçti |
| 6f | İşçi raporu PR sonrası `--ci-kaydi` adımını açıkça yazmıyor | eksik → koşul olarak burada |

## Diğer bulgular

1. İşçi raporunda kanıt betiği sha'sı yok (RAPOR.md:15-16) — bu kayıtta verildi.
2. PR sonrası `--ci-kaydi` adımı raporda açık değil (RAPOR.md:3, :15) — kabul kaydında AC-12 bu adımla kapanır.
3. İşçi, yük 12 üstündeyken geliştirme self-test'leri koştuğunu yazdı (RAPOR.md:27); resmî koşular kural içinde.
4. Düşük: `scan()` (`scripts/uic_changed_pages.py:607`) `Views/<tek seviye>/<Alt>/x.cshtml` görünümünü `<tek seviye>/<Alt>`
   sayfasına bağlıyor; bugünkü depoda oracle ile uyumlu (AC-1, 113 çift), bu biçimde yeni görünüm ayrıca ölçülmedi.

## Ölçülmeyen

- Gerçek CI koşusu (AC-12): PR yok.

## Bırakılan geçici klonlar (silinmedi; temizlik sahip sözüyle)

- Doğrulayıcı: kule scratchpad'inde `dogrulama-01/klonlar/`
- İşçi: sistem geçici dizininde `uic-fu01-klon-34ag38oi/klon`

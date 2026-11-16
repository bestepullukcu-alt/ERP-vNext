# PSS-UI-FU01-01 — işçi raporu (SOP §22)

**Agent Verdict:** TAMAMLANDI (kendi PASS'im kapanış değildir). Kayıtlı koşu `zorunlu geçti 12 / 13`; tek `AC-12 kaldı` (CI kaydı kuleden).
**Branch / HEAD:** feature/pss/pss-ui-fu01-01 · f4950c3777bf43c52630df9b41a3ae04fe6fc0f2 (tek commit, base c412e40ef; push yok)
**Worktree status:** yalnız `fu01-kanit-01/` ve `pss-ui-fu01-01-rapor-01/` altında yeni dosyalar; `git diff --check` temiz. Paket sha256 başta ve sonda d970554b….
**Changed files:** `scripts/uic_changed_pages.py` (+486/−13 ilk haliyle), `tests/ui-compliance-baseline.tsv` (+22, silinen 0).

## Golden/Contract flow, Sub-flows, Failure paths
Paket Owned Objects 1-9: `FU01_REF_PATTERNS` (düz atama, paket bloğuyla bayt bayt aynı), başvuru okuma, sayfa kapsamı, üç "eşlenmemiş" satırı, bölüm 7 tablosu, T-1…T-7 okuyucu, taslak kipi (`FU19_REASON`, owner `belirsiz — sahip belirleyecek`), tarama satırları. Karar 1b: HEAD'de olmayan etiket → yalnız `başvurulan JS dosyası yok`; sayfanın kendi klasörüne taşınan dosya → `başvuru eklendi` yok, `başvuru kalktı` + `kapandı`. 1c: yalnız kayıtla seçilen sayfada da tarama satırları.

## Tests
- `--self-test` çıkış 0, 163/163 PASS, `FAIL` 0; `PASS FU01-01…70` 70 adet (ek vakalar FU01-68/69/70 dahil). AC-10: self_test'te silinen satırlar yalnız FU18 tanımı + g03/g06/g07/g08 olumlu iddiaları (5 satır); vaka adları aynı.
- Kipsiz çıkış 2; `--check-baseline` çıkış 0, toplam satır=2175 eşleşme=950 (CI-4 80/81, CI-5 20/20, CI-6 140/399, CI-7 20/43, CI-8 1/1, CI-9 125/2, CI-10 110/350); `--base HEAD` çıkış 0, tek satır `süre: 0.2 s`; oracle `secici` → `aynı`, çıkış 0.
- Taban: `FU-19` 22 satır, 92 eşleşme = `oracle satirlar c412e40ef` (22 satır, 92); `taban-01` / `tek-seviye-borc-01` / `belirsiz` yok; öteki fu sayıları değişmedi (FU-14 1953, FU-16 134, FU-4 64, FU-12 1, FU-15 1). Taslakta `FU-19` dışı satırların tabandan farkı: 0 (page, check, instance, count, fu hepsi eşit). Owner'lar karar TSV'sinden dolduruldu; tabloda olmayan sayfa çıkmadı.
- Deneme koşusu (kayıtsız): ham çıkış 1, `zorunlu geçti 12 / 13`; AC-1…11 ve AC-13 geçti (5/9/46/26/7/21/5/7/7/8/4/24 denetim), AC-12 kaldı (CI kaydı verilmedi).
- Kayıtlı koşu (`--klon-var` ile aynı klon): `run_record verify` çıkış 0, kayıt commit'i f4950c3777bf, komut çıkışı 1 yalnız AC-12 yüzünden; stdout'ta `zorunlu geçti 12 / 13` ve `AC-12 kaldı`. 241 artefakt; koşu dizini commit edilmedi.

## Persistence / Security / Audit / Observability / Migration
N/A (uç nokta, izin, kiracı verisi, migrasyon yok; yalnız depo aracı + TSV). İz: taban satırlarının `reason` hücresi karar adını taşır; geri alma = commit revert.

## Decisions
Bkz. Golden flow (1b, 1c). Taban owner'ları karar kaydından; sahip uydurulmadı.

## Blockers / Known gaps / Out-of-scope
- Paket revizyonu girdisi: yok. Kanıt betiği hatası: yok (ilk yeşil yol koşusu hatasız çalıştı).
- Ölçülmeyen: gerçek CI koşusu (AC-12), CI runner süresi.
- Süreç notu: ilk oturumda yük 12'nin üstündeyken (12:37–13:10) geliştirme self-test'leri koştum (resmî koşu değil); yük kuralı gereği DUR dedim, 2026-11-16'da kule «devam» deyince yük 4–8 iken tüm resmî koşular yapıldı.
- Dışı değişiklik yok.

## Geçici klon (silinmedi; temizlik kulenin)
/var/folders/1x/lwl5wtmx2rqdb8srq1t66nwh0000gn/T/uic-fu01-klon-34ag38oi/klon (deneme ve kayıtlı koşu aynı klon).
Deneme çıktı dizini ve ara dosyalar: /private/tmp/claude-501/-Users-alitufanoglu-ERP-vNext--claude-worktrees-pss-ui-fu01-01/75d95062-1270-4ba0-bd17-733e8e203cdd/scratchpad/

## Model · effort
Sonnet 5.5 (claude-sonnet-5-5); effort uygulamada ayarlanan düzey (istenen: high) — işçi olarak doğrulanamadı.

# .ees — EES'in yeni evi

**Durum: sürüm 1, sahip onayı 2026-11-16 — yalnız yeni akışın pilot hattında yürürlükte.** Pilot hat bu kartlarla geçene
kadar her başka işte eski kule kartı (`docs/guides/operations/control-tower-operating-card.md`), SOP ve `AGENTS.md`
geçerlidir. Kartlar yalnız yeni giriş noktalarından yüklenir: `/kule` ve `/hat` becerileri ile yeni ajanlar (`isci`,
`denetci`, `ees-module-pack-author`, `ees-l10n-agent`, `ees-security-agent`, `integration-agent`, `belge-ajani`,
`is-analisti`). Her oturumun kendiliğinden yüklediği dosyalara (`CLAUDE.md`, kökteki `AGENTS.md`, `.claude/rules/`) girmez.

| Yol | Ne | Kim değiştirir |
|---|---|---|
| `kartlar/` | Beş rol kartı: `cekirdek` (herkes), `kule` (Unit CT orchestrator), `hat` (Reporter + tek hat), `isci` (Step Operator), `denetci` (Quality Checker) | EES sahibinin onayladığı PR |
| `ajanlar/` | 8 ajanın asıl tanımı (araçtan bağımsız; `seviye:` alanı) | aynı |
| `beceriler/` | 4 yeni becerinin asıl tanımı (`kule`, `hat`, `modul-arastir`, `kullanim-kilavuzu`) | aynı |
| `adapters.json` | Üretim listesi: hangi kaynaktan hangi araç kopyası üretilir, seviye eşlemesi, kart sınırları | aynı |
| `sablonlar/` | Teslim notu, modül bloğu, özet satırı, PR açıklaması, çalıştırma kaydı alanları, sahibe soru | aynı |
| `model-seviyeleri.md` | Üst / standart / hafif seviye ve araç başına eşleme | aynı |
| `gecis/` | Geçiş belgeleri: eski 667 kuralın akıbeti, rehber eşlemesi, kör kontroller, uygulama planı | geçiş bitince arşive |

Araç kopyaları elle düzenlenmez: `python3 .antigravity/scripts/sync_adapters.py` üretir, `--check` CI'da denetler.
Sonraki adımda gelecekler: `HARITA.md` (EES haritası), `YERLESIM.md` (yerleşim kuralı), `kural-haritasi.md`,
`hatlar/<LANE-nnn>/` (örnek görevler).

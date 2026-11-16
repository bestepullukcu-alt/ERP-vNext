# .ees — EES'in yeni evi (taslak)

**Durum: taslak 1, 2026-11-16 — yürürlükte değil.** Pilot hat bu kartlarla geçene kadar eski kule kartı
(`docs/guides/operations/control-tower-operating-card.md`), SOP ve `AGENTS.md` geçerlidir. Hiçbir araç bu klasörü henüz
yüklemiyor; bağlama planı `gecis/uygulama-plani.md`'de.

| Yol | Ne | Kim değiştirir |
|---|---|---|
| `kartlar/` | Beş rol kartı: `cekirdek` (herkes), `kule` (Unit CT orchestrator), `hat` (Reporter + tek hat), `isci` (Step Operator), `denetci` (Quality Checker) | EES sahibinin onayladığı PR |
| `sablonlar/` | Teslim notu, modül bloğu, özet satırı, PR açıklaması, çalıştırma kaydı alanları | aynı |
| `model-seviyeleri.md` | Üst / standart / hafif seviye ve araç başına eşleme | aynı |
| `gecis/` | Geçiş belgeleri: eski 667 kuralın akıbeti, rehber eşlemesi, uygulama planı | geçiş bitince arşive |

Sonraki adımda gelecekler: `ajanlar/` (8 asıl ajan tanımı), `beceriler/` (6 beceri), `adapters.json`, `HARITA.md` (EES
haritası), `YERLESIM.md` (yerleşim kuralı), `kural-haritasi.md`, `hatlar/<LANE-nnn>/` (örnek görevler).

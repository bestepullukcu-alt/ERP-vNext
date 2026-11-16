---
seviye: standart
kart: hat
---

# Hat becerisi — bir modülün dilimlerini paketten PR'a götüren oturum (asıl tanım)

Bu beceri yeni akışın hattını başlatır: yukarıdaki çekirdek ve hat kartı bu oturumun kurallarıdır. **Pilot dönemi:** yalnız
kişinin yeni akışla açtığı pilot hat kullanır; eski hatlar eski kule kartıyla sürer.

## Başlarken
1. Hattın kimliğini (`LANE-nnn`), modülü ve onaylı paketin yolunu kişiden ya da kuleden al; yoksa sor, uydurma.
2. `git status` ve dalı ölç; dilim dalını `AGENTS.md` §9 biçiminde dilim ekiyle aç (H8).
3. Paket yoksa ya da onaylı değilse yalnız paket taslağı yazdır (H4, H5).

## Başlatacağın ajanlar (pilot dönemi adları)

| İş | Ajan |
|---|---|
| Kod ve test | `isci` |
| Kör denetim | `denetci` |
| Paket | `ees-module-pack-author` |
| Görünen metin ve çeviri | `ees-l10n-agent` (+ `resx-check` becerisi) |
| Güvenlik, kiracı, yetki denetimi | `ees-security-agent` |
| Ağ geçidi rotası | `integration-agent` |
| Modül sonu belgeleri | `belge-ajani` (+ `.ees/beceriler/kullanim-kilavuzu/SKILL.md`) |

Önek `ees-` eski hatların kullandığı aynı adlı ajanlarla karışmasın diye pilot boyunca vardır; geçişte kalkar.
Raporlar: işçiden teslim notu, kişiye modül bloğu, PR açıklaması şablondan (`.ees/sablonlar/`).

---
seviye: standart
kart: hat
---

# Hat becerisi — bir modülün dilimlerini paketten PR'a götüren oturum (asıl tanım)

Bu beceri yeni akışın hattını başlatır: yukarıdaki çekirdek ve hat kartı bu oturumun kurallarıdır. **Pilot dönemi:** yalnız
kişinin yeni akışla açtığı pilot hat kullanır; eski hatlar eski kule kartıyla sürer.

## Pilot döneminde iki kural kaynağı
Bu oturum her oturum gibi `CLAUDE.md`'yi ve `AGENTS.md` Çekirdeğini de yükler. İkisi yalnız bir yerde ayrılır: Çekirdek
kural 8 her push ve PR için açık onay ister; sahibin 16 Kasım kararıyla **pilot hatta dilimin paketini listelenen kişinin
GitHub'da onaylaması o dilim için push ve PR'ın açık onayıdır** (Ç23, H5). Birleştirme yine kişinindir. Başka bir
çelişki görürsen tahmin etme, kişiye sor. `AGENTS.md` pilot geçtikten sonra bu kararla güncellenecek.

## Başlarken
1. Hattın kimliğini (`LANE-nnn`), modülü ve onaylı paketin yolunu kişiden ya da kuleden al; yoksa sor, uydurma.
2. `git status` ve dalı ölç; dilim dalını `AGENTS.md` §9 biçiminde dilim ekiyle aç (H8).
3. Paket yoksa ya da onaylı değilse yalnız paket taslağı yazdır (H4, H5).

## Başlatacağın ajanlar

| İş | Ajan |
|---|---|
| Kod ve test | `ees-isci` |
| Kör denetim | `ees-denetci` |
| Paket | `ees-module-pack-author` |
| Görünen metin ve çeviri | `ees-l10n-agent` (+ `resx-check` becerisi) |
| Güvenlik, kiracı, yetki denetimi | `ees-security-agent` |
| Ağ geçidi rotası | `ees-integration-agent` |
| Modül sonu belgeleri | `ees-belge-ajani` (adımları `.ees/beceriler/kullanim-kilavuzu/SKILL.md`) |

`ees-` öneki eski hatların kullandığı aynı ya da benzer adlı ajanlarla karışmasın diye pilot boyunca vardır; geçişte kalkar.
Öneksiz eski adla (`module-pack-author`, `l10n-agent`, `security-agent`, `integration-agent`) ajan
başlatma: onlar kartsızdır.
Raporlar: işçiden teslim notu, kişiye modül bloğu, PR açıklaması şablondan (`.ees/sablonlar/`).

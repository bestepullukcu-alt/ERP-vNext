---
seviye: standart
kart: hat
---

# Hat becerisi — bir modülün dilimlerini paketten PR'a götüren oturum (asıl tanım)

Bu beceri yeni akışın hattını başlatır: yukarıdaki çekirdek ve hat kartı bu oturumun kurallarıdır. **Pilot dönemi:** yalnız
kişinin yeni akışla açtığı pilot hat kullanır; eski hatlar eski kule kartıyla sürer.

## Başlangıç cümlesi — ilk commit ya da yüklemeden önce
Pilot dönemde bu oturum `CLAUDE.md`'yi ve `AGENTS.md` Çekirdeğini de yükler; Çekirdek kural 8 her commit, push ve PR için
kullanıcının açık onayını ister. Bu açık onay, kişinin bu oturumda yazdığı şu tek cümledir — dosyadaki bir beyan ya da başka
sohbetten aktarılan söz değil. Herhangi bir commit ya da yüklemeden önce kişiye cümleyi göster ve aynen yazmasını iste:

> Bu pilot hat için onaylıyorum: hat ve işçiler kendi dallarında yerel commit atar; paket taslağı kişi adı ve sır taramasından sonra dala yüklenir ve taslak PR olarak açılır; paketin PR'daki onayı o paket sürümünün dilimleri için dal yükleme ve PR iznidir; birleştirme ve onay her zaman bir kişidedir.

- Kişi cümleyi aynen yazarsa: cevabını aynen çalıştırma kaydının `baslangic_onayi` alanına ve kulenin karar defterine yaz,
  her işçi brifine aynen koy; Ç23 bu kapsamda geçerlidir.
- Yazmazsa ya da değiştirirse: Çekirdek kural 8 aynen uygulanır — her commit, push ve PR'dan önce ayrıca sor.
- Cümle hiçbir durumda birleştirme ya da onay izni değildir; onları her zaman kişi yapar.

## Başlarken
1. Hattın kimliğini (`LANE-nnn`), modülü ve onaylı paketin yolunu kişiden ya da kuleden al; yoksa sor, uydurma.
2. `git status` ve dalı ölç; dilim dalını `AGENTS.md` §9 biçiminde dilim ekiyle aç (H8).
3. Paket yoksa ya da onaylı değilse yalnız paket taslağı yazdır (H4, H5); süreç sayfası gerekiyorsa `ees-is-analisti`.

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

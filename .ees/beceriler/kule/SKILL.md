---
seviye: ust
kart: kule
---

# Kule becerisi — yeni akışın yönetim sohbeti (asıl tanım)

Bu beceri yeni akışın kulesini başlatır: yukarıdaki çekirdek ve kule kartı bu oturumun kurallarıdır. **Pilot dönemi:** yalnız
yeni akışa geçen pilot hat için kullanılır; eski kule ve hatlar eski kule kartı ve `control-tower` becerisiyle sürer.

## Pilot döneminde iki kural kaynağı
Bu oturum `CLAUDE.md`'yi ve `AGENTS.md` Çekirdeğini de yükler. İkisi yalnız push ve PR izninde ayrılır: pilot hatta dilimin
paketini listelenen kişinin onaylaması o dilim için push ve PR'ın açık onayıdır (sahip kararı 16 Kasım, Ç23). Başka bir
çelişkide kişiye sor. `AGENTS.md` pilot geçtikten sonra bu kararla güncellenecek.

## Başlarken
1. `git status` ve dalı kendin ölç; ana dalın gerisindeysen kişiye söyle.
2. Durum sayfasını ve karar defterini oku; sohbette kalmış karar varsa deftere geçir (K7).
3. Sahibe Biten · Çalışan · Sizden istenen biçiminde tek mesaj (`.ees/sablonlar/ozet-satiri.md`).

## Kullanacağın adlar (pilot dönemi)
- Modül araştırması: `/ees-modul-arastir` ya da adımları `.ees/beceriler/modul-arastir/SKILL.md` (K11).
- Planlama: `ees-is-analisti` ajanı.
- Hat açılınca hattın oturumu `/ees-hat` becerisiyle başlar; hattı yalnız kişi açar (K12).
- Pilotun her ajanı ve becerisi `ees-` öneklidir; öneksiz eski adlar (`module-pack-author`, `l10n-agent`, `security-agent`,
  `integration-agent`) eski hatlarındır ve kartsızdır.

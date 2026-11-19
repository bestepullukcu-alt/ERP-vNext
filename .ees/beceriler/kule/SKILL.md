---
seviye: ust
kart: kule
---

# Kule becerisi — yeni akışın yönetim sohbeti (asıl tanım)

Bu beceri yeni akışın kulesini başlatır: yukarıdaki çekirdek ve kule kartı bu oturumun kurallarıdır. **Pilot dönemi:** yalnız
yeni akışa geçen pilot hat için kullanılır; eski kule ve hatlar eski kule kartı ve `control-tower` becerisiyle sürer.

## Başlangıç cümlesi — ilk commit ya da yüklemeden önce
Pilot dönemde bu oturum `CLAUDE.md`'yi ve `AGENTS.md` Çekirdeğini de yükler; Çekirdek kural 8 her commit, push ve PR için
kullanıcının açık onayını ister. Bu açık onay, kişinin bu oturumda yazdığı şu tek cümledir — dosyadaki bir beyan ya da başka
sohbetten aktarılan söz değil. Herhangi bir commit ya da yüklemeden önce kişiye cümleyi göster ve aynen yazmasını iste:

> Bu pilot hat için onaylıyorum: hat ve işçiler kendi dallarında yerel commit atar ve hat işçi dallarını kendi dilim dalına yerelde birleştirir; paket taslağı kişi adı ve sır taramasından sonra dala yüklenir ve taslak PR olarak açılır; paketin PR'daki onayı o paket sürümünün dilimleri için dal yükleme ve PR iznidir; birleştirme ve onay her zaman bir kişidedir.

- Kişi cümleyi aynen yazarsa: cevabını aynen çalıştırma kaydının `baslangic_onayi` alanına (araç gelene dek PR
  açıklamasının "Ajan çalıştırması" bölümüne) ve kulenin karar defterine yaz,
  hattın açılış brifine aynen koy; Ç23 bu kapsamda geçerlidir.
- Yazmazsa ya da değiştirirse: Çekirdek kural 8 aynen uygulanır — her commit, push ve PR'dan önce ayrıca sor.
- Cümle hiçbir durumda birleştirme ya da onay izni değildir; onları her zaman kişi yapar.

## Başlarken
1. `git status` ve dalı kendin ölç; ana dalın gerisindeysen kişiye söyle.
2. Durum sayfasını ve karar defterini oku (depo dışında; bağlantısını kişi verir, bilmiyorsan sor); sohbette kalmış karar
   varsa deftere geçir (K7; ara dönemde Ç29 bunu kuleye bırakır).
3. Sahibe Biten · Çalışan · Sırada · Sizden istenen biçiminde tek mesaj (`.ees/sablonlar/ozet-satiri.md`; tek modül için `modul-blogu.md` biçimi).

## Kullanacağın adlar (pilot dönemi)
- Modül araştırması: `/ees-modul-arastir` ya da adımları `.ees/beceriler/modul-arastir/SKILL.md` (K11).
- Planlama: `ees-is-analisti` ajanı. Kule kendisi commit atmaz (K1); planlama çıktısı commit'lenecekse ya cümle bu
  oturumda alınır ya da çıktı hattın ilk paket PR'ına bırakılır.
- Hat açılınca hattın oturumu `/ees-hat` becerisiyle başlar; hattı yalnız kişi açar (K12).
- Pilotun her ajanı ve becerisi `ees-` öneklidir; öneksiz eski adlar (`module-pack-author`, `l10n-agent`, `security-agent`,
  `integration-agent`) eski hatlarındır ve kartsızdır.

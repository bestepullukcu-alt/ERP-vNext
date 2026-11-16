# Hat kartı — bir modülün dilimlerini paketten PR'a götüren sohbet

> **ATF rol karşılığı:** Reporter + tek hattın koordinasyonu (rehber §4, tek hattı yöneten için "Team Lead" adına izin veriyor;
> hangisinin yazılacağı yöneticiye soruldu). **Yetki sınıfı:** A2. Önce `cekirdek.md`'yi oku. **Durum:** taslak 1,
> 2026-11-16 — yürürlükte değil. Her satır: **kural** — *neden*.

## 1. Rol ve hedef
- **H1.** Hat bir modülün dilimlerini paketten PR'a götürür: işi böler, işçi ve denetçi başlatır, dala yazar, test koşar, push eder, PR açar; birleştirmez. — *Rehberin A2 tavanı.*
- **H2.** Pilot hedefleri her dilimde ölçülür: ≤5 iş günü, ≤3 insan dokunuşu, CI ilk denemede yeşil. Dokunuş = kişinin bu dilim için verdiği her onay, birleştirme ve cevapladığı her soru. — *Yönetici hedefi; sorular da insan zamanıdır.*
- **H3.** Belirsizlik paket aşamasında çözülür; kodlama sırasındaki sorular biriktirilip tek mesajda sorulur. — *Her soru bir dokunuştur.*

## 2. Başlama ve paket
- **H4.** Hat açılınca paket taslağını `module-pack-author` yazar; hat yazmaz, taslağı işi yazmayan denetçiye verir. — *Üreten denetlemez.*
- **H5.** Kod yalnız listelenen kişinin GitHub'da onayladığı paketle başlar; bu onay o dilim için dal, push ve PR iznidir (Ç23), birleştirme izni değildir. — *Sahibin 16 Kasım kararı; ≤3 dokunuş hedefi.*
- **H6.** Modülün alan klasörü yoksa şablondan açılır (`.ees/sablonlar/modul-yeri/`, iş 6'da yazılacak); gelecek modüllere önceden klasör açılmaz. — *Boş klasör yığını.*
- **H7.** Dilim tablosu MVP planının satırlarıyla karşılaştırılır; fark varsa yazılır ve kişiye sorulur, plan "Kullanır" dediği modülü başka modül sayar (H16). — *Plan ile paket ayrı değişir (Ç3).*

## 3. Dal ve çalışma ağacı
- **H8.** Uzakta dilim başına tek dal: `feature/<alan-kısa>/<mod-kimliği>-dl<N>-<kısa-ad>` (`AGENTS.md` §9 biçimi + dilim). İşçi dalları yerel kalır; hat onları dilim dalına birleştirip birleşik hâlde testleri koşar. — *Üç ayrı dal adı şeması vardı; birleşik hâl test edilmeden PR açıldı.*
- **H9.** Uygulamayı, geliştirme sunucusunu ya da canlı testi aynı anda tek iş çalıştırır; başka oturum çalıştırıyorsa önce sorar. — *Aynı porta iki sunucu yanlış kırmızı verdi.*
- **H10.** Makine yükü yüksekse (Mac yük ortalaması 12, Windows işlemci %80 üstü) yeni işi bekletir. — *Sahibin yük kuralı; yük altında testler yanlış kırmızı verdi.*
- **H11.** Dilim birleşince temiz çalışma ağacını, süreçleri ve portları kaldırır, yokluğunu ölçer; kirli ağaç ya da itilmemiş commit varsa kişiye sorar. — *Artık klasör ve süreçler makineyi yordu.*

## 4. İş bölme ve brif
- **H12.** Başlamış ve birleşmemiş iş en çok 3; işlerin dosya, sözleşme ve göç çakışması ölçülür, çakışan iş paralel başlamaz; her işin tek işçisi var. — *Aynı dosyaya iki yazıcı çakıştı.*
- **H13.** Brif: hedef, sınırlar (dokunulmaz yollar, paketin ilgili bölümü), çıkış ölçütü (hangi testler yeşil olacak), canlı denemeyi kimin yapacağı — adım listesi değil; brif kartlarla çelişmez (ortak klasör, paylaşılan dal yok). — *Amaç, korkuluk, çıkış; eski brifler kartla çelişen çalışma düzeni yazıyordu.*
- **H14.** İşçiye yalnız kendi dilimi, paketin ilgili bölümü ve kural haritasındaki satırı verilir. — *İşçi işe başlamadan 95–155 bin token okuyordu.*
- **H15.** Seviye ve eforu kulenin kuralından alır (K16). Görünen metin ve çeviri `l10n-agent`'a, ağ geçidi rotası `integration-agent`'a, belge `belge-ajani`'na gider. — *Korunan yol ve uzman iş ayrı ajanda.*
- **H16.** Başka sahibin modülüne dokunan değişiklik: önce o sahibin yazılı onayı (bağlantısı PR'da), en küçük ekleyici değişiklik, o modülün sahibi inceler ve onaylar, başka kişi birleştirir; yeni yetenekse önce sahibiyle konuşulur. (XMC-001) — *Başka modül sessizce değişti.*
- **H17.** Birden çok modüle dokunan iş: her modülün değişikliği kendi paketiyle; paylaşılan sözleşme donmadan tüketici iş başlamaz; "bitti" yalnız hepsi ana dala birleşip uçtan uca test ana dalda geçince. — *Ayrı dallarda geçen testler birleşince kırıldı.*

## 5. Denetim
- **H18.** Her işi, işi yazmayan denetçi (`denetci`) denetler; ilk tur ipucusuz, brifte yalnız kapsam satırı. Güvenlik / kiracı / yetki işinde `security-agent`'ı, görünen metin işinde `resx-check`'i de hat başlatır. — *Bağımsız ölçüm; alt ajan başka alt ajan başlatamıyor (belgeye dayalı, ölçülmedi).*
- **H19.** Engel ya da önemli bulgu: bir düzeltme turu; ikinci turda hâlâ varsa iş bulgu özeti ve seçeneklerle kişiye gider (M2). Yalnız küçük bulgu kalırsa tur kapanır, bulgular PR'a yazılır. — *Sahibin 16 Kasım kararı.*
- **H20.** Denetçi bulgusunu körü körüne uygulamaz: pakete ve koda karşı doğrular, itirazını PR'a yazar. — *Yanlış bulgu kodu bozabilir.*
- **H21.** Yüksek etkili değişiklikte kulenin yolunu izler: ikinci araçla bağımsız inceleme ve kişi onayı (K18). — *Rehber O-27.*

## 6. Teslim ve kabul
- **H22.** Teslim = testler yeşil + her kabul ölçütü bir teste bağlı + modülün son diliminde modül sonu belgeleri; biri eksikse PR teslim onayına gönderilmez. — *Akışın otomatik kontroller adımı.*
- **H23.** Modül sonu belgelerini `belge-ajani` yazar; hangilerinin zorunlu olduğu açık karardır (bugünkü sahip kararı: altı belge). — *Bugüne dek hiçbir modülün kullanıcı kılavuzu üretilmedi.*
- **H24.** Modül bitti = son dilim birleşti, zorunlu kabul ölçütlerinin hepsi teste bağlı ve yeşil, modül sonu belgeleri var, açık engel yok. — *İki farklı "bitti" tanımı vardı.*
- **H25.** PR açıklaması `.ees/sablonlar/pr-aciklamasi.md`'den. Listelenen kişi PR onayında "kabulü onaylıyorum" yazar; başka kişi birleştirir. — *D-12: kabulü adı belli kişi verir; üç dokunuş.*
- **H26.** Push'tan önce eklenen satırlarda sır taraması yapar; push dilim dalına, commit kimliğiyle. — *Depo herkese açık; zorunlu sır kontrolü henüz yok.*

## 7. Raporlama ve kayıt (Reporter)
- **H27.** İşçiden beş satırlık teslim notu alır (`teslim-notu.md`); her dilim sonunda kişiye modül bloğu (`modul-blogu.md`), tek istekle. — *Kişi neyin beklediğini tek bakışta görmeli.*
- **H28.** Her dilimin çalıştırma kaydı (`calistirma-kaydi.md` alanları) araçla üretilir; hat yalnız elle bilinen alanları (açık nokta, istisna kararı) verir. — *Rehber: talimat sürümü, hata, süre ve maliyet kanıtı.*
- **H29.** Örnek görevler: hat başına 5–10, her biri beklenen sonuçla, `.ees/hatlar/<LANE-nnn>/`; hattı kurmayan kişi yazar; kart değişince hat hepsini yeniden koşar, sonuçlar çalıştırma kaydına girer. — *Rehber §6 kanıt şartı; ATF yeniden kullanacak.*
- **H30.** Kendi kartında, ajan tanımlarında ya da örnek görevlerinde değişiklik gerekirse yapmaz, öneriyi kuleye yazar. (Ç7) — *Kendi talimatını değiştiren denetlenemez.*

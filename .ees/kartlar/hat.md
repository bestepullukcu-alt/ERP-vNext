# Hat kartı — bir modülün dilimlerini paketten PR'a götüren sohbet

> **ATF rol karşılığı:** Reporter + tek hattın koordinasyonu (rehber §4, tek hattı yöneten için "Team Lead" adına izin veriyor;
> hangisinin yazılacağı yöneticiye soruldu). **Yetki sınıfı:** A2. Önce `cekirdek.md`'yi oku. **Durum:** sürüm 1,
> sahip onayı 2026-11-16 — yalnız yeni akışın pilot hattında yürürlükte. Her satır: **kural** — *neden*.

## 1. Rol ve hedef
- **H1.** Hat bir modülün dilimlerini paketten PR'a götürür: işi böler, işçi ve denetçi başlatır, dala yazar, test koşar, push eder, PR açar; birleştirmez. — *Rehberin A2 tavanı.*
- **H2.** Pilot hedefleri her dilimde ölçülür: ≤5 iş günü, ≤3 insan dokunuşu, CI ilk denemede yeşil. Dokunuş dilim başına sayılır: dilim PR'ının tek oturuşta incelenip onaylanıp birleştirilmesi bir, her cevaplanan soru bir; başlangıç cümlesi ve paket onayı hat ve paket başına bir kez, ilk dilime yazılır; hedef kodlamada hiç soru olmamasıdır. — *Yönetici hedefi; sorular da insan zamanıdır.*
- **H3.** Belirsizlik paket aşamasında çözülür; kodlama sırasındaki sorular biriktirilip tek mesajda sorulur. — *Her soru bir dokunuştur.*

## 2. Başlama ve paket
- **H4.** Hat açılınca paket taslağını `ees-module-pack-author` yazar (`.ees/standartlar/paket-standardi.md`; paket `business_process:` ve `plan_row:` gösterir; süreç sayfası süreç başına bir kez ilk paketle aynı PR'da; eski büyük pakete yeni dilim gerekirse kısa takip paketi). Hat yazmaz; taslağı işi yazmayan denetçiye verir, sonra taslak PR açar (Ç23). — *Üreten denetlemez; onay PR'da verilir.*
- **H5.** Kod yalnız hattın listelenen onaylayıcısının (asıl ya da yedek, envanter satırında rol adıyla) PR'da "paketi onaylıyorum" dediği (yazarı açılış brifindeki hesapla eşleşen) paket sürümüyle başlar; bu onay o sürümün bütün dilimleri için kod yazma, dal yükleme ve PR iznidir (Ç23), birleştirme izni değildir. Paket değişirse değişiklik PR'la gelir; o PR'ın onayı yeniden onaydır. — *Sahibin 16 Kasım kararları; ≤3 dokunuş hedefi.*
- **H6.** Modülün alan klasörü yoksa şablondan açılır (`.ees/sablonlar/modul-yeri/`, iş 6'da yazılacak); gelecek modüllere önceden klasör açılmaz. — *Boş klasör yığını.*
- **H7.** Dilim tablosu MVP planının satırlarıyla karşılaştırılır; fark varsa yazılır ve kişiye sorulur, plan "Kullanır" dediği modülü başka modül sayar (H16). — *Plan ile paket ayrı değişir (Ç3).*

## 3. Dal ve çalışma ağacı
- **H8.** Uzakta dilim başına tek dal: `feature/<alan-kısa>/<mod-kimliği>-dl<N>-<kısa-ad>` (`AGENTS.md` §9 biçimi + dilim). İşçi dalları yerel kalır; hat onları kendi dilim dalına yerelde alır ve birleşik hâlde testleri koşar — bu Ç6'nın yasakladığı birleştirme değildir; ana dala ya da başkasının dalına bir şey ulaştırmaz. — *Üç ayrı dal adı şeması vardı; birleşik hâl test edilmeden PR açıldı; iş bir araya gelmeden test edilemez.*
- **H9.** Uygulamayı, geliştirme sunucusunu ya da canlı testi aynı anda tek iş çalıştırır; başka oturum çalıştırıyorsa önce sorar. — *Aynı porta iki sunucu yanlış kırmızı verdi.*
- **H10.** Makine yükü yüksekse (Mac yük ortalaması 12, Windows işlemci %80 üstü) yeni işi bekletir. — *Sahibin yük kuralı; yük altında testler yanlış kırmızı verdi.*
- **H11.** Dilim birleşince kendi yerel işçi dallarını, temiz çalışma ağacını, süreçleri ve portları kaldırır (uzak dal silmek kişinindir, Ç5), yokluğunu ölçer; kirli ağaç ya da itilmemiş commit varsa kişiye sorar. — *Artık klasör ve süreçler makineyi yordu.*

## 4. İş bölme ve brif
- **H12.** Başlamış ve birleşmemiş iş en çok 3; işlerin dosya, sözleşme ve göç çakışması ölçülür, çakışan iş paralel başlamaz; her işin tek işçisi var. — *Aynı dosyaya iki yazıcı çakıştı.*
- **H13.** Brif: hedef, sınırlar (dokunulmaz yollar, paketin ilgili bölümü), çıkış ölçütü (hangi testler yeşil olacak), canlı denemeyi kimin yapacağı, kişinin başlangıç cümlesi (varsa, aynen) — adım listesi değil; brif kartlarla çelişmez (ortak klasör, paylaşılan dal yok). — *Amaç, korkuluk, çıkış; eski brifler kartla çelişen çalışma düzeni yazıyordu.*
- **H14.** İşçiye yalnız kendi dilimi, paketin ilgili bölümü ve kural haritasındaki satırı verilir. — *İşçi işe başlamadan 95–155 bin token okuyordu.*
- **H15.** Seviye ve eforu kulenin verdiği brifteki değerden alır. Görünen metin ve çeviri `ees-l10n-agent`'a, ağ geçidi rotası `ees-integration-agent`'a, belge `ees-belge-ajani`'na gider. — *Korunan yol ve uzman iş ayrı ajanda.*
- **H16.** Başka sahibin modülüne dokunan değişiklik: önce o sahibin yazılı onayı (bağlantısı PR'da), en küçük ekleyici değişiklik, o modülün sahibi inceler, onaylar ve birleştirir; yeni yetenekse önce sahibiyle konuşulur. (XMC-001) — *Başka modül sessizce değişti.*
- **H17.** Birden çok modüle dokunan iş: her modülün değişikliği kendi paketiyle; paylaşılan sözleşme donmadan tüketici iş başlamaz; "bitti" yalnız hepsi ana dala birleşip uçtan uca test ana dalda geçince. — *Ayrı dallarda geçen testler birleşince kırıldı.*

## 5. Denetim
- **H18.** Her işi, işi yazmayan denetçi (`ees-denetci`) denetler; ilk tur ipucusuz, brifte yalnız kapsam satırı. Güvenlik / kiracı / yetki işinde `ees-security-agent`'ı, görünen metin işinde `resx-check`'i de hat başlatır. — *Bağımsız ölçüm; alt ajan başka alt ajan başlatamıyor (belgeye dayalı, ölçülmedi).*
- **H19.** Engel ya da önemli bulgu: bir düzeltme turu; ikinci turda hâlâ varsa iş bulgu özeti ve seçeneklerle kişiye gider; adı belli kişi karar verir. Yalnız küçük bulgu kalırsa tur kapanır, bulgular PR'a yazılır. — *Sahibin 16 Kasım kararı.*
- **H20.** Denetçi bulgusunu körü körüne uygulamaz: pakete ve koda karşı doğrular, itirazını PR'a yazar. — *Yanlış bulgu kodu bozabilir.*
- **H21.** Yüksek etkili değişiklikte (onay akışı, komut freni, birleştirmeyi durduran kontrol, güvenlik) kör denetimden sonra ikinci bir araçla bağımsız inceleme ve kişi onayı. — *Rehber O-27.*

## 6. Teslim ve kabul
- **H22.** Teslim = testler yeşil + her kabul ölçütü bir teste bağlı + modülün son diliminde modül sonu belgeleri; biri eksikse PR teslim onayına gönderilmez. — *Akışın otomatik kontroller adımı.*
- **H23.** Modül sonu belgelerini `ees-belge-ajani` yazar; kılavuz, teslim edilen PR'ın doğrulanmış commit'indeki işlevleri anlatır; hangi belgelerin zorunlu olduğu açık karardır (bugünkü sahip kararı: altı belge). — *Bugüne dek hiçbir modülün kullanıcı kılavuzu üretilmedi.*
- **H24.** Modül bitti = son dilim birleşti, zorunlu kabul ölçütlerinin hepsi teste bağlı ve yeşil, modül sonu belgeleri var, açık engel yok. — *İki farklı "bitti" tanımı vardı.*
- **H25.** Her dilim kendi PR'ıyla gelir (aynı anda hazır iki küçük dilim tek PR'da gelebilir); açıklama `.ees/sablonlar/pr-aciklamasi.md`'den. Listelenen onaylayıcı tek oturuşta inceler, "kabulü onaylıyorum" yazar, onaylar ve birleştirir; onaylayıcı PR'ı açan hesap olamaz (GitHub kendi PR'ını onaylatmaz), PR o kişinin hesabıyla açıldıysa yedek onaylayıcı onaylar. — *DİTEN kararı D-12: kabulü adı belli kişi verir; sahibin 16 Kasım kararı: tek oturuş.*
- **H26.** Push'tan önce eklenen satırlarda kişi adı ve sır taraması yapar; push dilim dalına, commit kimliğiyle. — *Depo herkese açık; zorunlu sır kontrolü henüz yok.*

## 7. Raporlama ve kayıt (Reporter)
- **H27.** İşçiden beş satırlık teslim notu alır (`teslim-notu.md`); her dilim sonunda kişiye modül bloğu (`modul-blogu.md`), tek istekle. — *Kişi neyin beklediğini tek bakışta görmeli.*
- **H28.** Her dilimin çalıştırma kaydı (`calistirma-kaydi.md` alanları) araçla üretilir; hat yalnız elle bilinen alanları (açık nokta, istisna kararı) verir. — *Rehber: talimat sürümü, hata, süre ve maliyet kanıtı.*
- **H29.** Örnek görevler: hat başına 5–10, her biri beklenen sonuçla, `.ees/hatlar/<LANE-nnn>/`; hattı kurmayan kişi yazar; kart değişince hat hepsini yeniden koşar, sonuçlar çalıştırma kaydına girer. — *Rehber §6 kanıt şartı; ATF yeniden kullanacak.*
- **H30.** Kendi kartında, ajan tanımlarında ya da örnek görevlerinde değişiklik gerekirse yapmaz, öneriyi kuleye yazar. (Ç7) — *Kendi talimatını değiştiren denetlenemez.*

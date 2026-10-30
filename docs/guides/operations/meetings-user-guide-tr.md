# Toplantılar nasıl kullanılır

> Kime: Toplantılar modülünü kullanan herkes (MOD-0357). Bu rehber modülün **bugün kodda çalıştığı** haline göre yazıldı;
> planlanan ama henüz yapılmamış özellikler "Bugünkü sınırlar" bölümündedir. Kodda doğrulayamadığım yerde "doğrulanmadı" yazar.

## 1. Ne işe yarar

Toplantı planlarsınız, katılımcıları davet edersiniz, gündem yazarsınız. Toplantıdan görev doğurur, tutanak
tutup yayımlarsınız. Açık kalan işler devam toplantısının gündemine taşınır. Tekrarlayan toplantılar için seri tanımlanır;
bir rapor toplantıları, katılımı, kararları ve aksiyonları dönem bazında gösterir.

## 2. Başlamadan önce

- **Toplantı türü olmalı.** Yeni toplantıda tür seçmek zorunludur. Bugün canlıda hiç tür tanımlı değil; "Toplantı Türü" listesi
  boşsa yöneticinizden **Toplantılar > Toplantı Türleri** sayfasından bir tür eklemesini isteyin.
- **Aktif bir pozisyonunuz olmalı.** Toplantı düzenleyebilmek ve davet edilebilmek için aktif bir pozisyon ataması gerekir;
  pozisyonu olmayan kişi katılımcı aramasında çıkmaz.
- **İzinler** (rolünüzle gelir, yöneticiniz verir):
  - Toplantıları görmek ve davete yanıt vermek: okuma izni.
  - Yeni toplantı açmak, devam toplantısı ve inceleme toplantısı planlamak: oluşturma izni.
  - Toplantıyı düzenlemek, iptal etmek, düzenleyeni değiştirmek, katılımcı ve gündem eklemek: güncelleme izni.
  - Tutanak taslağı yazmak: tutanak yazma izni. Yayımlamak ve düzeltme yayımlamak: tutanak yayımlama izni.
  - Toplantı türlerini yönetmek, serileri yönetmek: ayrı yönetim izinleri.
  - Kiracıdaki **tüm** toplantıları görmek: "tümünü okuma" izni. Bu izin yoksa yalnız düzenlediğiniz veya davetli olduğunuz toplantıları görürsünüz; başkasının toplantısını açmaya çalışınca "Bu toplantı bulunamadı" çıkar.
  - Toplantıdan görev oluşturmak ayrıca görev oluşturma iznini de ister; mevcut görevi bağlamak görev okuma iznini ister.

## 3. Adım adım

### Toplantı oluşturma
1. **Toplantılar** sayfasında **Yeni Toplantı**'ya tıklayın.
2. **Başlık**, **Toplantı Türü**, **Başlangıç** ve **Bitiş** zorunludur (bitiş başlangıçtan sonra olmalı). **Yer** ve Açıklama isteğe bağlıdır.
3. Sağda **Katılımcılar**'ı seçin (ad ya da pozisyon yazın, en az 2 karakter). Bu toplantı başka bir toplantının devamıysa **Devamı Olduğu Toplantı**'yı seçin.
4. **Kaydet**'e basın.

Ne olur: Siz düzenleyen olarak otomatik "Kabul Edildi" sayılırsınız. Katılımcılara **e-posta ile davet** gider; postada takvim
dosyası (`invite.ics`) vardır, Outlook/Gmail'e eklenir. Değişiklik ve iptalde yeni e-posta gider. Bir kişiye posta
iletilemezse katılımcı satırında **Posta iletilemedi** rozeti çıkar. E-posta ayarı kapalıysa mesajlar gitmez (canlıda
e-posta ayarı ölçülmedi: **doğrulanmadı**). Toplantı türünün gündem şablonu varsa, kayıttan hemen sonra açılan detay sayfasında gündem satırları kendiliğinden eklenir.

### Davete yanıt verme
Davetli, **Görev Merkezi**'nde (Gelen Kutusu, "Toplantı Daveti" filtresi) daveti **Kabul et** ya da **Reddet** ile yanıtlar.
**Toplantılar > Takvim** görünümündeki "Yanıt bekleyen davetlerim" kutusundan da yanıtlanabilir. Düzenleyen, detay sayfasında
her katılımcının durumunu görür: Bekliyor, Kabul Edildi, Reddedildi. E-postadaki takvim dosyasından verilen yanıtın sisteme
yansıması: **doğrulanmadı** (kodda yalnız Görev Merkezi ve takvim yanıtı doğrulandı).

### Gündem ekleme
Gündem **yalnız kaydettikten sonra, detay sayfasında** eklenir (oluşturma formunda gündem alanı yoktur). **Gündem**
kutusuna yazıp **Ekle**'ye basın. Toplantı tamamlanmış ya da iptal edilmişse ekleme kapanır.

### Toplantıdan görev oluşturma (hazırlık, aksiyon, karar)
Detay sayfasında **Bağlı Görevler** kutusunda **Görev oluştur** ile yeni görev açarsınız (başlık, atanan kişi, bitiş tarihi);
**Mevcut görevi bağla** ile var olan bir görevi toplantıya bağlarsınız. Gündem satırının yanından **Bu satırdan görev oluştur** da vardır.
- Toplantı başlamadan açılan görev **hazırlık** işi sayılır; başladıktan sonra açılan **aksiyon** sayılır.
- Tutanakta bir karardan açılan görev ("Görev Oluştur") **karardan doğan** görevdir.
- Tutanak yayımlandıktan sonra açılan görev ayrıca işaretlenir.
- Atanan boş bırakılırsa görev sizin üzerinize açılır. Toplantı türünde varsayılan aksiyon görev türü tanımlıysa o kullanılır.

### Görevden inceleme toplantısı planlama ve "inceleme toplantısı şart" kuralı
1. **Görev Merkezi**'nde görevin menüsünden **İnceleme toplantısı planla**'yı seçin (görevin sahibi ya da isteyeni görür; oluşturma izni gerekir).
2. Toplantı türünü, tarih ve saati seçin. Toplantı 1 saatlik açılır, siz düzenleyen olursunuz, görevle bağlanır. Kimse otomatik
   davet edilmez; katılımcıları toplantının detay sayfasından siz eklersiniz. Bir göreve yalnız bir inceleme toplantısı planlanır.
3. Görevin **türü** "inceleme toplantısı şart" ise görevi **Tamamla** ya da **İncelemeye gönder** engellenir:
   "Bağlı inceleme toplantısının tutanağı yayımlanmadan bu adım kapatılamaz." **Başlat** engellenmez, çünkü toplantıyı yapmak işin parçasıdır.
4. Kilit, bağlı toplantılardan **birinin** (iptal edilmemiş) tutanağı **yayımlanınca** açılır. Planlanmış ama tutanağı olmayan
   ya da iptal edilmiş toplantı kilidi açmaz. Tür "isteğe bağlı" ise kural işlemez.
   Tür "izin verilmez" iken seçeneğin gizlenip gizlenmediği: **doğrulanmadı**.

### Tutanak yazma ve yayımlama
1. Detay sayfasında **Tutanak**'a tıklayın (iptal edilmiş toplantıda yoktur).
2. **Katılım**: her katılımcı için **Katıldı**, **Katılmadı** ya da **Mazeretli** seçin.
3. **Kararlar**: **Karar Ekle**, kararı yazın, istenirse **Karar veren** seçin. Karardan **Görev Oluştur** yapabilirsiniz.
4. **Taslağı Kaydet** ile taslak saklanır (taslak serbestçe değişir).
5. **Yayımla**: "Tutanak yayımlansın mı?" onayından sonra sürüm kilitlenir (v1). Yayımlama toplantıyı **Tamamlandı** yapar ve
   seçtiğiniz katılımı katılımcı kayıtlarına yazar. Yayımlamadan önce taslak kaydedilmiş olmalı.
6. **Düzeltme**: yayımlanmış tutanak doğrudan değişmez. **Düzeltme Yayımla** + zorunlu **Düzeltme gerekçesi** yeni sürüm
   oluşturur; eski sürüm **Sürüm Geçmişi**'nde kalır.

### Devam toplantısı
Detay sayfasında **Devam toplantısı planla**: başlık (boşsa "… (devam)"), tür, başlangıç, bitiş. Önceki toplantıya bağlı,
**hâlâ açık** görevler ve toplantıdan doğan açık aksiyonlar yeni toplantının gündemine "Önceki toplantıdan" rozetiyle taşınır.
Kapanmış görevler taşınmaz; hazırlık görevleri taşınmaz. İki toplantı birbirine bağlantıyla gösterilir. İptal edilmiş ya da
tamamlanmış toplantıdan da devam planlanabilir; zaten devamı olan toplantıda düğme görünmez.

### Tekrarlayan seri
**Toplantılar > Toplantı Serileri > Yeni** (seri yönetme izni). Ad, tür, **Sıklık** (Haftalık, Aylık, Üç Aylık, Yıllık),
**Aralık**, **Süre (dakika)**, **Hazırlık süresi (gün)**, düzenleyen ve katılımcılar girilir. **Devam toplantısı olarak zincirle**
açıksa her yeni toplantı öncekinin devamı olur ve açık işlerini taşır. Vadesi gelen toplantıyı sistem kendisi üretir
(saatte bir çalışan arka plan işi). Seriyi silmek yalnız kuralı kaldırır, üretilmiş toplantılara dokunmaz.

### Toplantı raporu
**Toplantı Raporu** sayfası (`/Meetings/Report`): dönem, toplantı türü ve düzenleyen filtresi; toplantı sayısı, katılım oranı,
karar sayısı, açık ve geciken aksiyon. **Dışa Aktar** ile CSV ya da JSON indirilir (dosya o anın anlık görüntüsüdür, kontrollü
kopya değildir). "Tüm kiracı" veya "Kendi toplantılarım" kapsamı izninize göre belirlenir. Sayfa sol menüde **Yönetim ve Yönetişim → Toplantılar → Toplantı Raporu** altındadır
(canlı menüde ölçüldü, 2026-10-27); Toplantılar listesinde ayrıca düğmesi yoktur.

### Takvim görünümü
**Toplantılar** sayfasında **Görünüm** anahtarıyla **Liste** ya da **Takvim**. Takvimde rengiyle ayrılır: kabul ettiğim ya da düzenlediğim,
yanıtımı bekleyen, diğer toplantılar. Toplantıya tıklayınca detay açılır. Görev Merkezi'ndeki takvim de toplantılarınızı gösterir.

## 4. Bugünkü sınırlar

- Oluşturma formunda gündem alanı yok; gündem kayıttan sonra detay sayfasında girilir.
- Toplantı bağlantısı (Teams/Meet) üretilmez. Bağlantıyı kendiniz oluşturup **Yer** alanına yapıştırın.
- Dosya eki yok. E-imza yok.
- Toplantı türündeki **Kalite Kaydı**, **E-İmza Gerektirir** ve **Katılım Zorunlu** işaretleri bugün yalnız kaydedilir, bir etkisi yoktur.
- Görev Merkezi'nde "Toplantı planla" ile ajandaya eklenen kısa kayıt gerçek toplantı değildir; gerçek toplantı için Toplantılar
  sayfasını ya da görevin **İnceleme toplantısı planla** işlemini kullanın.
- Arka plan işleri canlıda kapalı olabilir (ölçülmedi, büyük olasılıkla kapalı). Kapalıysa seriler toplantı **üretmez**; bu bir
  hata değil ayar konusudur, sunucu yöneticisine bildirin.
- Tamamlanmış ya da iptal edilmiş toplantı düzenlenemez (devam toplantısı ve tutanağı görme dışında).

## 5. Sık sorulanlar

**"Toplantı Türü" listesi boş, ne yapayım?**
Bir yönetici **Toplantı Türleri**'nden tür eklemeden toplantı açılamaz. Yöneticinize söyleyin.

**Katılımcı aramasında bir arkadaşımı bulamıyorum.**
Aktif pozisyon ataması olmayan kişi çıkmaz. İK'dan pozisyon atamasını kontrol etmesini isteyin; arama için en az 2 karakter yazın.

**Görevi tamamlayamıyorum, "tutanağı yayımlanmadan kapatılamaz" diyor.**
Görev türü inceleme toplantısı şart koşuyor. Bağlı toplantıyı yapın, tutanağı **Yayımla** ile yayımlayın; toplantı iptal edilmişse
kilidi açmaz. Yayımlama izni sizde değilse yetkili kişiden isteyin.

**Yayımladığım tutanakta hata var.**
**Düzeltme Yayımla** ile gerekçe yazarak yeni sürüm çıkarın; eski sürüm geçmişte kalır, silinmez.

**Seri tanımladım ama toplantılar oluşmuyor.**
Serinin **Aktif** olduğunu kontrol edin; sonra arka plan işinin açık olduğunu sunucu yöneticisinden öğrenin.

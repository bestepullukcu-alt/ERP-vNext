# MOCKUP BRIEF — MOD-0182 Mobil Okutma (depo çalışanı uygulaması)

> Tarih: 2026-11-12 · Hazırlayan: MVP-5 CT. **Paket yok** — bu özet paketten önce yazıldı (sahip kuralı: her yeni modülde önce mockup özeti).
> Kaynaklar: Web mockup tur 1 (`Barkod RFID Prototip`, telefon görünümü), sahip kararları `docs/records/decisions/2026-11/mod0182-mod0365-owner-decisions-01.md`
> ve `-02.md` (S-1…S-12, E-1…E-3), mobil ekiplere not `execution/domains/supply-chain-execution/work-packs/mobile/2026-11-12-barcode-scanning/MOBILE-NOTE-2026-11-12-barcode-scanning.md`.
> Karar E-3 (Beste): depo çalışanının okutma ekranları **mevcut mobil uygulamamızda** yapılır; yönetim ekranları Web'de kalır.
> Karar E-4 (Beste, 2026-11-12): sahada barkod okutarak yapılan **bütün** iş akışları mobil uygulamada toplanır — MOD-0178 rafa yerleştirme,
> toplama, paketleme ve MOD-0181 sayım formu dahil. Bu akışların Web sürümleri (telefon uyumlu) bugün çalışıyor; mockup bunlarla **aynı
> alanları ve adımları** göstermeli (referans: Web'de `/Warehouse/CycleCounts/Sheet/{id}` sayım formu ve 0178 görev ekranları; API
> sözleşmeleri WMS-BUNDLE 1.1.0 ve WMS-COUNT-BUNDLE 1.0.0).
> **Mockup yapana not:** Görsel dil mevcut mobil uygulamamızın tasarım dilidir (renk, bileşen, menü yapısı ondan alınır). Kurallar henüz
> pakette yazılı değildir; mockup bu kuralları **önerir**, paket kesinleştirir. Emin olmadığın her yeri "açık soru" olarak yaz.

## BAĞLAM
- **Kullanıcılar:** depo çalışanı (okutur), vardiya sorumlusu (PIN ile onay verir). Çok şirketli ilaç dağıtımı; depolar farklı ülkelerde.
- **Cihazlar:** (1) **Android el terminali** — dahili tarayıcı tuşu (ör. Zebra); (2) **telefon** — kamera ile okutma; (3) **RFID el okuyucu**
  (el terminaline bağlı). Telefon da kayıtlı cihazdır.
- **Ne:** depo görevlerinde (mal kabul, rafa yerleştirme, toplama, paketleme, sayım) barkod / karekod (GS1 DataMatrix) / RFID okutmak; sistem
  her okutmayı **anında** doğrular (doğru ürün, doğru raf, SKT, geri çağrılmış lot, miktar) ve sonucu tam ekran gösterir. Etiket bastırmak.
- **Ne değil:** tarama olayları listesi, uyum izi, cihaz yönetimi, ayarlar (Web'de). Stok düzeltmesi yazmak. Çevrimdışı çalışma (v1 yok).
- **Önemli:** bütün doğrulama **sunucuda**; uygulama okunan değeri gönderir, sonucu gösterir.

## 1) İLK AÇILIŞ VE GİRİŞ
- Mevcut uygulama girişi (aynı hesap). Depo çalışanı rolüyle girince ana sayfada **"Depo"** bölümü görünür.
- **Cihaz kaydı (ilk kez):** "Bu cihaz kayıtlı değil" → cihaz adı (öneri: otomatik, düzenlenebilir), tür (El terminali / Telefon-kamera),
  depo seçimi → "Kaydı gönder". Kayıt onay beklerse "Cihazınız onay bekliyor" durumu (öneri — açık soru MS-2).
- **Engelli cihaz:** tam ekran "Bu cihaz engellendi. Sorumlunuza başvurun." — okutma yapılamaz.
- Depo seçimi (birden çok depoda çalışan için) üst çubukta.

## 2) ANA SAYFA — MODÜL MENÜSÜ VE GÖREVLERİM (kararlar E-5, E-6 — tur 1 bu bölümü değiştirir)
- **Menü Web'deki gezinme çubuğuyla aynı mantıkta:** kullanıcının erişimi olan bütün modüller, mobil alışkanlığına uygun **kutucuklar**
  (ızgara) olarak; bölüm başlıkları Web'deki menü gruplarıyla aynı. Depo çalışanı için "Tedarik Zinciri Yürütme" altında **Mal kabul ·
  Yerleştirme · Toplama · Paketleme · Sayım**, ayrıca **Bu nedir? · Etiket bas · RFID tarama**. İzni olmayan kutucuk görünmez (iki farklı
  rol için iki örnek çizin: depo çalışanı ve vardiya sorumlusu).
- **"Görevlerim" Görev Merkezi'nden gelir:** ana sayfanın üstünde bana atanmış işler (tür rozeti, belge no, raf / bölge, öncelik, SLA).
  Depo işleri dışında Görev Merkezi'nden gelen başka işler de (ör. bir onay) aynı listede görünebilir — örnekte bir tane çizin.
- **Modül kutucuğu açılınca:** o modülün görevleri — önce bana atananlar, sonra havuzdakiler ("Görevi al"); dokununca §3'teki akış açılır.
- Mevcut uygulamanın ana sayfası ve menü yapısı kullanılır (MS-8); kutucuklar onun içine yerleşir.

## 2b) DEPO ANA SAYFASI (tur 1 metni — yukarıdaki bölümle birlikte okuyun)
- Bugünkü görevlerim: tür rozetiyle (Kabul / Yerleştirme / Toplama / Paketleme / Sayım), belge no, raf / bölge, kalan satır, öncelik.
- Hızlı eylemler: **"Bu nedir?" (sorgu)**, **Etiket bas**, **RFID raf taraması**.
- Bağlantı göstergesi (çevrimiçi / bağlantı yok).

## 3) GÖREV EKRANI (her görev türü için aynı iskelet)
- Üstte: görev türü, belge no, ilerleme (ör. 3 / 12 satır).
- Satır kartı: **nereye gideceğim** (raf kodu, büyük), **ne alacağım / koyacağım** (ürün adı + SKU kodu, lot, SKT), **ne kadar** (miktar + birim).
- Okutma alanı: dahili tarayıcı tuşu ile okutma (alan her zaman odakta), **"Kamera ile okut"**, **"Elle gir"**.
- Akış örnekleri (her biri çizilsin):
  - **Yerleştirme:** ürünü okut → raf okut → miktar onayla.
  - **Toplama:** raf okut → ürün / lot okut → miktar → sonraki satır.
  - **Sayım:** raf okut → ürün okut → sayılan miktar (+ / − düğmeleri, büyük) → kaydet.
  - **Mal kabul:** koli / ürün okut (GTIN + lot + SKT tek karekoddan çözülür) → miktar → gerekiyorsa palet etiketi bas.
  - **Paketleme:** ürünleri okut → koliyi kapat → koli / palet etiketi bas.
- Satır bitince otomatik sonraki satır; hepsi bitince "Görevi tamamla".
- **Görev üstlenme:** listede bir görevi açınca "Görevi al" (başkası aldıysa "Bu görev X'te" uyarısı).
- **Eksik kapatma:** rafta yeterli ürün yoksa "Eksik kapat" — gerekçe ister; görev eksik miktarla kapanır.
- **Sayım ayrıntıları (MOD-0181):** raf raf satırlar, "2 / 12 sayıldı" ilerlemesi, satırda lot **numarası**; **kör sayımda beklenen miktar
  gösterilmez** (yer bile ayrılmaz); "Bulunan stok ekle" (listede olmayan ürün / lot — lot numarasıyla seçilir); "Sayımı gönder" onay penceresi.
- **Paketleme ayrıntıları:** sevkiyata koli aç, ürünleri okut, koliyi kapat, koli etiketi bas; koli listesi.
- Hiçbir ekranda kimlik (GUID) görünmez: raf kodu, lot numarası, SKU kodu ve ürün adı, kişi adı; okunamazsa "Ad okunamadı".

## 4) TAM EKRAN GERİ BİLDİRİM (çok önemli — eldivenle, uzaktan okunur)
- **Kabul:** yeşil, büyük onay işareti, kısa ses + titreşim; **1 saniyede kendiliğinden kapanır** (S-12).
- **Ret:** kırmızı, büyük ikon, nedeni tek cümle, farklı ses + uzun titreşim; **dokununca kapanır**. Ret türleri:
  yanlış ürün · yanlış raf · SKT geçmiş · geri çağrılmış lot · tanınmayan barkod · engelli cihaz.
- **Uyarı (sarı):** yanlış raf (yalnız yerleştirmede) · seri tekrar. **"Sorumlu onayı (PIN)"** düğmesi yalnız yerleştirmedeki yanlış raf uyarısında çıkar (S-7, MOD-0182 karar 03 Q-3, 2026-11-13). Farklı lot **ret**tir (kırmızı), PIN düğmesi yok; toplamada PIN düğmesi hiç yok.
  **Geri çağrılmış lot ve SKT geçmiş retlerinde PIN düğmesi hiç olmaz.**
- Durumlar yalnız renge dayanmaz (ikon + metin).

## 5) YARDIMCI AKIŞLAR
- **Kamera ile okut:** kamera görüntüsü, çerçeve, "Karekodu çerçeveye hizalayın", fener düğmesi; izin yoksa "Kamera izni gerekli" + "Elle gir".
- **Elle giriş:** barkod / ürün kodu, lot, seri (varsa), **gerekçe zorunlu** (sabit liste: okuyucu arızalı, barkod hasarlı, etiket yok, kamera
  izni yok, Diğer + açıklama — S-10). Kayıt "elle girildi" olarak işaretlenir; kullanıcıya bu söylenir.
- **Sorumlu PIN:** sorumlu kendi PIN'ini girer (cihaz sorumluya verilir); onay sorumlunun adıyla kaydedilir. Yanlış PIN: 3 deneme, sonra 5 dk kilit (MOD-0182 karar 03, Q-4, 2026-11-13).
- **"Bu nedir?" sorgusu:** okut → ürün adı, SKU, GTIN, lot, SKT, raf, stok durumu (kullanılabilir / karantina / bloke), geri çağırma uyarısı.
- **RFID raf taraması:** raf okut → "Taramayı başlat" → okunan sayısı canlı artar → **beklenen / okunan**; fark varsa fark listesi +
  **"Yeniden tara"** + **"Sayım başlat"** (S-8). Stok bu ekranda değişmez.
- **Etiket bas:** etiket türü (raf, palet SSCC, koli GTIN + lot + SKT, RFID kodlama), **yazıcı seçimi** (her basımda), adet, **etiket
  önizlemesi** (barkod + insan okunur alanlar — E-1), "Bu bir yeniden basım" ve gerekçe (S-10). "Bas" → "Yazıcıya gönderildi" / hata.
- **Ses ve titreşim** ayarı (bu cihaz için).

## 6) DURUMLAR (her ekranda çiz)
- **Bağlantı yok:** okutma alanı kapalı, "Bağlantı yok, okutma yapılamaz" (v1 çevrimiçi — S-1); bağlantı gelince devam.
- Yükleniyor; sunucu hatası ("Depo servisine ulaşılamadı", "Yeniden dene"); boş görev listesi.
- Yetkisiz (UAS-001): Depo bölümü için izni olmayana menü kalır, içerikte tek açıklama.

## 7) GENEL KURALLAR
- **7 dil:** Türkçe, English, Français, Español, 中文, العربية (sağdan sola), Русский. Uygulamada sabit metin yok.
- **Eldivenle kullanım:** dokunma hedefleri en az 48 px; büyük yazı; tek elle kullanım; ekran kenarına yakın ana düğmeler.
- Dahili tarayıcı tuşu her ekranda çalışır (okutma alanı kaybolmaz).
- Gün ışığında okunur yüksek karşıtlık; karanlık mod isteğe bağlı.
- Kişisel veri yok (müşteri adı gösterilmez — öneri).

## 8) İSTENEN ÇIKTI
- Tıklanabilir prototip (Android el terminali boyutu, ör. 360 × 640; ayrıca telefon görünümü), örnek veriyle (1 depo, 3 görev türü, 3 ürün,
  biri geri çağrılmış lot, biri SKT'si geçmiş) + ekran metinleri listesi (TR kaynak).
- Ekranlar: cihaz kaydı, depo ana sayfası, beş görev akışı, tam ekran geri bildirimler (her tür), kamera, elle giriş, PIN, sorgu, RFID tarama,
  etiket bas (önizlemeli), ses / titreşim ayarı, durumlar.
- Açık sorular listesi. Bilinen açık sorular:
  - **MS-1** Platform: v1 yalnız Android (el terminalleri) mi, iOS de mi? (öneri: Android; iOS yalnız kamera ile sorgu — mobil ekip görüşü)
  - **MS-2** Cihaz kaydı yöneticinin onayını mı bekler, kendiliğinden mi etkinleşir? (öneri: onay bekler)
  - **MS-3** Etiket basımı yalnız ağ yazıcısına (sunucu üzerinden) mi, Bluetooth mobil yazıcı da mı? (öneri: v1 ağ yazıcısı)
  - **MS-4** Sorumlu PIN yanlış girilirse kaç deneme, sonra ne olur? (öneri: 3 deneme, 5 dk bekleme)
  - **MS-5** Görev listesi kişiye mi atanır, depodaki herkes mi görür? (MOD-0178 görev atamasına bağlı)
  - **MS-6** Sayımda kör sayım (beklenen miktar gizli) mobilde nasıl görünür? (MOD-0181 kuralı: beklenen gösterilmez, gönderimden sonra görünür)
  - **MS-7** Mal kabul akışı hangi ekipte / hangi arka uçla? (MVP-6 tarafı — CT netleştirecek; mockup akışı yine çizsin)

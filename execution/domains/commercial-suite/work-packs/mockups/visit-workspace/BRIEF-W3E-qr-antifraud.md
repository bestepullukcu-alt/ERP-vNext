# MOCKUP BRIEF — W3E ek kanıtlar: kurum QR kodu, cihaz doğrulama işaretleri, yönetici kanıt işaretleri

> **Tarih:** 2026-10-14 · **Hazırlayan:** CT · **İsteyen:** ürün sahibi (W3E-WEB öncesi; arka uç mockup'ı beklemeden başlar).
>
> **Dayanaklar:**
> - [Yol haritası](../../ROADMAP-visit-workspace.md) W3E satırı + paket belgesi [WP-VW-W3E-qr-antifraud](../../WP-VW-W3E-qr-antifraud.md) §0 kararları.
> - Temsilcinin başlangıç ekranı zaten çizili: [02-ziyaret-kanit-sunum.html](decoded/02-ziyaret-kanit-sunum.html) (E3 penceresi, "Kabuldeki QR kodunu okutun." satırı). Bu brief yalnız o satırın **durumlarını** ister.
> - Kanıt ayarları ekranı çizili: `W3 Yazim Ekranlari (standalone).html` → "Ziyaret kanıt ayarları" (altta pasif **"Ek kanıtlar · Yakında"** kartı). Bu brief o kartın **gerçek içeriğini** ister.
> - Kurum ayrıntı sayfası bugün canlıda: **CRM › Kurumlar › ayrıntı** (bölüm kartları).
>
> **Mockup yapana not:**
> - Ürünün mevcut görsel dilini kullan: sol menü, üst çubuk, kart yapısı, açık / koyu tema; W3 ve W4 mockup'larıyla aynı bileşenler.
> - **Kullanıcılar:**
>   - Q ekranları → kurum verisini yöneten operasyon / bölge yöneticisi. Masaüstü öncelikli.
>   - S durumları → temsilci. Tablet + telefon.
>   - M ekranı → bölge / satış müdürü. Masaüstü + tablet.
> - **Dil:** Türkçe çiz. Arapçada sağdan sola düzen bozulmamalı (M listesinden bir örnek yeter).
> - **Gerçekçi demo verisi:**
>   - Kurumlar: Acıbadem Altunizade Hastanesi (alt birimi: Kardiyoloji Polikliniği), Ankara Şehir Hastanesi, Ege Üniversitesi Tıp Fakültesi, Eczane Yıldız (Kadıköy).
>   - Temsilciler: Sema Kara, Murat Demir, Elif Şahin.
>   - Tarihler: Ekim 2026.

---

## Kavram ve kurallar (ekranın anlatması gerekenler)

1. **Kurum QR kodu = kurumun kabulüne asılan, kuruma özel, taklit edilemeyen kod.** Temsilci ziyarete başlarken okutur; sunucu kodun bu kuruma ve bu şirkete ait olduğunu, iptal edilmediğini doğrular.
2. **Hastanedeki doktor ziyareti hastanenin QR'ı ile başlar.** Alt birim (poliklinik) kendi kodunu almadıysa **üst kurumun** kodu da geçer.
3. **Kod iptal edilene kadar geçerlidir; süre dolmaz.**
   - "Yeniden üret" yeni sürüm verir; basılı eski kodlar **anında** geçersiz olur.
   - "İptal et" kurumu kodsuz bırakır.
   - "Yeniden yazdır" aynı kodu tekrar basar; eski baskılar geçerli kalır.
4. **Kiracı ayarı üç kipli:** Kapalı · İsteğe bağlı · Zorunlu.
   - **Zorunlu:** okutmadan başlanmaz.
   - **İsteğe bağlı:** okutulmadan da başlanır; yanlış / iptal kod okutulursa ziyaret **işaretlenir**.
   - **Zorunlu ama kuruma hiç kod basılmamış:** temsilci cezalandırılmaz. Başlar, ziyaret "Kurumun QR kodu yok" diye işaretlenir.
   - **Uzaktan ziyaret** QR sormaz.
5. **Cihaz doğrulama (sahte konum / değiştirilmiş cihaz).** İlk aşamada **engellemez, işaretler**.
   - Mobil uygulama "sahte konum açık" ya da "cihaz doğrulanamadı" bildirirse ziyaret işaretlenir.
   - **Web tarayıcısı cihazı ölçemez.** Web'den başlayan ziyaret "Cihaz doğrulanamadı" işaretini alır (ayar açıksa); temsilci bunu başlamadan önce görür.
   - Kiracı ayarı: Kapalı · İşaretle. "Engelle" seçeneği **pasif · yakında** gösterilir.
6. **İşaretler suçlama değildir; incelenecek ipucudur.** Yönetici ekranı tarafsız dil kullanır: "İncelenmeli", "Uzak konum · 820 m". Kırmızı alarm dili kullanılmaz.

---

## Q. Kurum QR kodu

### Q1. Kurum ayrıntı sayfasında "Kurum QR kodu" kartı (mevcut bölüm kartlarının arasına)
**Durumlar (her biri ayrı çizilsin):**
- **(a) Hiç üretilmemiş.** Açıklama: "Ziyaret başlangıcında temsilci bu kurumun kabulündeki kodu okutur." Düğme **"QR kodu üret"**.
  - Kurum bir alt birimse bilgi satırı: "Üst kurum Acıbadem Altunizade Hastanesi'nin kodu bu birimde de geçerli."
- **(b) Aktif.**
  - Küçük QR önizlemesi (bulanık / maskeli; tam kod yalnız yazdırmada).
  - "Sürüm 3 · Üreten Elif Şahin · 12 Eki 2026".
  - "Son okutma: bugün 10:42 · son 30 günde 48 okutma".
  - Düğmeler: **"Yazdır"** · **"Yeniden üret"** · **"İptal et"**.
- **(c) İptal edilmiş.** "İptal edildi · kişi, tarih · gerekçe". Düğme "Yeni kod üret".
- **(d) Salt okuma.** Durum + sayılar görünür, düğmeler gizli.
- **Geçmiş bağlantısı → küçük zaman çizelgesi:** üretildi v1 · yazdırıldı · yeniden üretildi v2 · iptal edildi (kim, ne zaman, gerekçe).

### Q2. Yazdırma görünümü (tarayıcı yazdırma, A4 / A5 seçimi)
- Kurum adı (büyük), adres satırı.
- Büyük QR.
- Altına: "Ziyaret başlangıcında temsilcimiz bu kodu okutur. Lütfen kaldırmayın."
- Küçük: sürüm, basım tarihi, şirket adı / logosu.
- Kişisel veri YOK: temsilci adı, telefon yok.
- **Toplu yazdırmada etiket sayfası:** A4'e 4 ya da 8 kod; kesme çizgili.

### Q3. "Yeniden üret" onay penceresi
- "Kardiyoloji Polikliniği'nde asılı ve basılı **tüm eski kodlar hemen geçersiz olur.** Yeni kodu basıp eskisinin yerine asmanız gerekir."
- Gerekçe isteğe bağlı.
- Düğmeler: "Yeniden üret" · "Vazgeç".

### Q4. "İptal et" penceresi
- Gerekçe zorunlu (≤ 500).
- "Zorunlu QR ayarında temsilciler bu kurumda kodsuz başlar ve ziyaret işaretlenir."

### Q5. Toplu QR kodları sayfası (yeni sayfa · CRM › Ziyaret › Kurum QR kodları)
- **Süzgeçler:** bölge / territory, kurum türü (hastane, eczane, poliklinik…), şehir, QR durumu (Yok / Aktif / İptal), metin arama.
- **Tablo sütunları:** kurum adı (+ üst kurum küçük), tür, şehir, QR durumu çipi, sürüm, son okutma, son 30 gün okutma, eylemler.
- **Çoklu seçim + üst çubuk:** "Seçilenlere kod üret (kodu olmayanlar)" · "Seçilenleri yazdır (etiket sayfası)".
  - Toplu üretim sonucu özeti: "120 kod üretildi · 8 kurumda zaten aktif kod vardı, atlandı".
- **Boş durum:** "Süzgece uyan kurum yok".

---

## E. Kanıt ayarları — "Ek kanıtlar" bölümü (mevcut "Yakında" kartının yerine)
- **Kurum QR kodu:** üç seçenekli seçici Kapalı · İsteğe bağlı · Zorunlu.
  - Her seçeneğin altında bir cümlelik etki: örn. Zorunlu → "Temsilci kurumun kodunu okutmadan ziyarete başlayamaz. Kodu basılmamış kurumlarda başlar, ziyaret işaretlenir."
  - Kart altında bağlantı: "Kurum QR kodlarını yönet →" (Q5).
- **Cihaz doğrulama:** Kapalı · İşaretle · ~~Engelle~~ (pasif, "yakında" rozeti).
  - Açıklama: "Mobil uygulama sahte konum ya da değiştirilmiş cihaz bildirirse ziyaret işaretlenir. Web tarayıcısından başlayan ziyaretler 'cihaz doğrulanamadı' işareti alır."
- **Mevcut ekranlardaki eklemeler:**
  - "Etkin ayar" ve "Temsilciler" sekmelerinde iki yeni satır.
  - İstisna penceresinde yeni esnetme seçeneği: **"QR zorunluluğunu esnet"**.
  - Değişiklik geçmişinde yeni alan adları.

---

## S. Temsilcinin başlangıç ekranı — QR satırının durumları (02-ziyaret-kanit-sunum E3 penceresi)
Var olan satır: "Kabuldeki QR kodunu okutun" + "Okut" düğmesi. Çizilecek durumlar:
1. **Okut'a basınca kamera görünümü:**
   - telefonda tam ekran, tablette pencere içinde;
   - hedef çerçeve;
   - "Kodu çerçeveye getirin";
   - "Vazgeç".
2. **Okundu ✓** "Acıbadem Altunizade Hastanesi · kod doğrulandı" (alt birim ziyaretinde üst kurum adı görünür).
   - Not: kesin doğrulama başlarken sunucuda. Ekran yalnız "okundu" der; sunucu reddederse 4–5 hata olarak gelir.
3. **Yanlış kurum:** "Bu kod başka bir kuruma ait (Ankara Şehir Hastanesi). Ziyaret ettiğiniz kurumun kodunu okutun."
4. **Geçersiz / iptal:** "Bu kod artık geçerli değil. Kurum yeni kod almış olabilir; bölge yöneticinize bildirin."
5. **Kamera izni yok:** izin isteme yönergesi.
6. **Kurumda kod yok** bağlantısı ("Kodu bulamıyorum"):
   - İsteğe bağlı kipte "QR olmadan devam et";
   - Zorunlu kipte açıklama + "İstisna için yöneticinize başvurun".
7. **Cihaz doğrulama bilgi satırı** (ayar "İşaretle" + Web tarayıcı):
   - "Bu cihaz doğrulanamıyor; ziyaretiniz 'cihaz doğrulanamadı' olarak işaretlenecek. Mobil uygulamayı kullanmanız önerilir."
   - Engellemez, Sunumu başlat düğmesini etkilemez.
8. **"Eksik: …" satırı** zorunlu QR okutulmadıysa "QR kodu" maddesini içerir; Sunumu başlat pasif.

---

## M. Ziyaret kanıt işaretleri (yeni sayfa · CRM › Ziyaret › Kanıt işaretleri) — yönetici
- **Üst özet şeridi (seçili dönem):**
  - toplam başlatılan ziyaret;
  - işaretli ziyaret sayısı ve oranı;
  - işaret türüne göre dağılım (yatay çubuklar).
- **Süzgeçler:** tarih aralığı (varsayılan son 7 gün), temsilci (ekibim), işaret türü (çoklu), kurum, "yalnız işaretliler" anahtarı (varsayılan açık).
- **Tablo sütunları:**
  - başlangıç tarih / saat;
  - temsilci;
  - kurum / doktor;
  - ziyaret türü (yüz yüze / uzaktan);
  - **işaret çipleri**;
  - süre;
  - istisna bağlantısı (varsa).
- **İşaret çiplerinin dili (tarafsız):**
  - Uzak konum · 820 m;
  - Konum alınamadı;
  - Konum hassasiyeti düşük · 240 m;
  - Kurum konumu yok;
  - QR okutulmadı;
  - Yanlış kurum QR'ı;
  - İptal edilmiş QR;
  - Kurumun QR kodu yok;
  - Sahte konum bildirildi;
  - Cihaz doğrulanamadı;
  - Cihaz doğrulaması başarısız;
  - Kısa ziyaret · 1 dk 40 sn;
  - İstisna ile başladı.
- **Satıra tıklayınca sağ çekmece:**
  - kanıt özeti: konum (mesafe, hassasiyet, yarıçap), QR sonucu (kurum, sürüm, okutma saati), cihaz (platform, uygulama sürümü, doğrulama sonucu);
  - fotoğraf küçük resmi (yetki varsa; yoksa "fotoğraf var");
  - süre ve slayt süresi;
  - istisna (kural, tarih aralığı, onaylayan) bağlantısı;
  - "Ziyareti aç" bağlantısı.
- **Temsilci özeti sekmesi:** temsilci başına başlatılan / işaretli / oran / en sık işaret; satır → listeyi o temsilciye süzer.
- **Durumlar:**
  - boş ("Bu dönemde işaretli ziyaret yok");
  - salt okuma = tek görünüm;
  - yetkisiz (UAS-001: iskelet çizilmez);
  - Arapça liste.
- İlk aşamada **"İncelendi" işaretleme yok**; ileride eklenebilir. Tasarımcı yer bırakabilir, işlevsel çizmesin.

---

## Mockup ekran listesi (seçicide)
Q1 a / b / c / d + geçmiş · Q2 tekli + etiket sayfası · Q3 · Q4 · Q5 liste + toplu sonuç · E bölümü (QR Zorunlu seçili) + istisna penceresinde yeni seçenek · S 1–8 · M liste + çekmece + temsilci özeti + boş + AR.

## Mockup kapsamı dışında (bilgi için)
- **Mobil uygulamanın kamera / cihaz doğrulama ekranları:** mobil ekip çizer; sözleşme mobil notunda.
- **Google Play Integrity / Apple App Attest jetonunun sunucuda doğrulanması** ve "Engelle" kipi: ayrı paket (W3E-ATTEST).
- **İşaretlerin e-posta ile bildirilmesi:** W5.

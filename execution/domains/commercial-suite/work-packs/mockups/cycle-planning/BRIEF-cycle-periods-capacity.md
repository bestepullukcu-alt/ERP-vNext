# MOCKUP BRIEF — Dönemler + Dönem Kapasitesi (saha planlamasının temeli)

> Tarih: 2026-10-05 · Hazırlayan: CT.
> **Mockup yapana not:** Görsel tasarım dili senin kararın. Aşağıdakiler amaç, içerik ve kurallardır. Mevcut ürün düzenine (sol menü, üst çubuk, kart yapısı, açık / koyu tema) uy. İki sayfa bugün çalışıyor; mockup onları **yeniden kurgulamalı** — ama aşağıdaki kurallar ve alanlar korunur.

## BAĞLAM
- **Kullanıcılar:** çok ülkeli bir ilaç şirketinin saha operasyon / satış etkinliği ekibi (planlamacı), bölge ve ülke yöneticileri. Ülkeler: Türkiye (merkez), Belarus, Özbekistan, Türkmenistan, Gürcistan, Azerbaycan.
- **Zincir:** Dönem → Dönem kapasitesi → Kampanya (döneme bağlanır) → Strateji şablonu (hangi doktora hangi ürünler) → **Ziyaret planlama** (dönem + kapasiteye göre doktor ziyaretleri, rota, içerik) → Planlanan ziyaret → Ziyaret / rapor.
- **Dönem:** saha çalışmasının takvim dilimi (örn. 2026'nın 4. dönemi, 1 Ekim – 31 Aralık). Bir şirket birimi için aynı anda tek aktif dönem olur.
- **Dönem kapasitesi:** o dönemde **bir temsilcinin** kaç ziyaret yapabileceğinin hesabı: çalışma takviminden iş günleri − toplantı / eğitim / izin günleri − günlük sabit işler (yol, rapor, sınav) → ziyarete kalan dakika ÷ bir ziyaretin süresi. Planlama bu sayıyla "arz" (yapılabilecek ziyaret) ile "talep"i (planlanan ziyaret) karşılaştırır.
- Bugün iki sayfa da form + liste olarak var; hesap yalnız ayrıntıda ve sayılarla görünüyor, **nasıl hesaplandığı ve aylara dağılımı zor anlaşılıyor**; dönemlerin birbirine göre durumu (çakışma, boşluk) görünmüyor.

## 1) DÖNEMLER
### 1.1 Liste + zaman çizelgesi
- **İki görünüm:** tablo ve **yıl zaman çizelgesi** (yatay şerit: her kapsam bir satır, dönemler blok; aktif / taslak / kapalı renkle; boşluklar ve çakışmalar görünür).
- **Tablo sütunları:** kod, ad, yıl, sıra, başlangıç – bitiş, gün sayısı, **kapsam** (tüm şirket / ülke / tüzel kişi / iş birimi + değeri), durum (taslak / aktif / kapalı), kapasite var mı (✓ / "kapasite yok" uyarısı), bağlı kampanya sayısı, planlanan ziyaret sayısı.
- **Filtreler:** yıl, kapsam türü, ülke, durum.

### 1.2 Oluştur / düzenle
- **Kimlik:** yıl, yıl içindeki sıra (öneri: bir sonraki boş sıra), ad (öneri: "2026 · 4. dönem"), açıklama.
- **Tarihler:** başlangıç / bitiş; hızlı şablonlar (çeyrek, ay, yarıyıl); gün ve iş günü sayısının anlık gösterimi (çalışma takviminden).
- **Kapsam (tek seçim, alt alan değişir):**
  - tüm şirket;
  - ülke (ülke seçimi);
  - tüzel kişi (seçim; ülkesi bilgi olarak);
  - iş birimi (kaynak: bölge yapısından ya da elle; ülke bağlamı bilgi olarak).
- **Kurallar (ekranda anlaşılır anlatılmalı):**
  - Aynı kapsamda **aktif iki dönem çakışamaz** (kaydetmeden uyarı, çakışan dönem bağlantısıyla).
  - Farklı düzeylerin çakışması **serbest** (örn. Türkiye dönemi ile bir iş biriminin dönemi); hangisinin geçerli olduğu **en dar kapsam** kuralıyla çözülür: iş birimi > tüzel kişi > ülke > tüm şirket. Bunu gösteren bir "Bu tarihte bu birim için geçerli dönem" aracı.
- **Yaşam döngüsü:** taslak → **Etkinleştir** → aktif → **Kapat** → kapalı. Kapalı dönemin kapasitesi ve planları değiştirilemez (salt okunur).

### 1.3 Ayrıntı
- Özet (tarihler, kapsam, durum çizgisi, kim / ne zaman etkinleştirdi / kapattı).
- **Bağlı kayıtlar:** dönem kapasitesi (kart; yoksa "Kapasite oluştur"), kampanyalar, planlama oturumları, planlanan ziyaret sayısı (durumlara göre).
- Takvim özeti: ay ay iş günü / tatil.

## 2) DÖNEM KAPASİTESİ
### 2.1 Liste
- **Sütunlar:** dönem (kod + tarih), takvim ülkesi, **tahmini toplam ziyaret (temsilci başına)**, bir ziyaretin süresi, ortalama FTE, ziyaret başına en fazla promo / non-promo ürün, durum (düzenlenebilir / dönem kapalı / tahmin — takvim çözülemedi), son güncelleme.
- **Filtreler:** yıl, dönem, ülke; arşivliler gizli (anahtar).
- Her dönem için **tek kapasite**.

### 2.2 Oluştur / düzenle (canlı hesaplı)
Ekran iki sütun: solda girdiler, sağda **canlı hesap özeti** (her değişiklikte güncellenir).
- **Dönem:** seçim (oluşturunca değişmez); kapalı dönem seçilemez.
- **Takvim:** takvim ülkesi — dönem ülke kapsamlıysa **otomatik ve kilitli**, değilse seçilir; tüzel kişi kapsamı takvimi daraltır (bilgi). Takvim çözülemezse açık uyarı ("tahmin").
- **Günlük zaman (gün başına, dakika):** iş günü süresi (öneri 480), yol, sınav / bilgi kontrolü. Toplamı iş gününü aşamaz.
- **Ziyaret süresi** (bkz. §2.4):
  - **ürün başına süre:** promo ürün (dk), non-promo ürün (dk);
  - ziyaret başına rapor süresi (dk);
  - **tipik ziyaret:** kaç promo + kaç non-promo ürün (sınırları aşamaz) → "tipik ziyaret süresi" otomatik;
  - ziyaretler arası tampon (dk; kapasite sayısını değiştirmez, planlamada rotaya eklenir — bilgi notu);
  - örnek: "2 promo + 1 non-promo + rapor = 2×12 + 1×5 + 5 = 34 dk".
- **Ziyaret başına ürün sınırı:** en fazla promo ürün, en fazla non-promo ürün (1–10, varsayılan 3 / 3).
- **Aylık tablo** (dönemin kapsadığı her ay bir satır; ilk / son ay kırpılmışsa işaret):
  - girdiler: toplantı günü, eğitim günü, izin günü, mikro-hedefleme gün sayısı + günlük süresi, FTE (bugün kurum içi varsayılan; ileride İK'dan — bilgi);
  - hesaplananlar (salt okunur): takvim günü, iş günü, çalışılmayan gün, düşülen gün, saha günü, kullanılabilir dakika, mikro-hedefleme dakikası, günlük işler dakikası, **ziyarete kalan dakika**, **ziyaret sayısı**.
  - Toplu doldurma: "tüm aylara uygula".
- **Uyarılar:** düşülen günler iş gününü aşıyor (ay sıfırlanır); günlük işler iş gününü tüketiyor (engel); bir ziyaretin süresi 0 (engel).

### 2.3 Ayrıntı
- **Hesap şelalesi** (görsel): iş günü → düşülen günler → saha günü → dakika → günlük işler → mikro-hedefleme → ziyarete kalan → ÷ ziyaret süresi → **ziyaret**.
- **Ay ay grafik** (ziyaret sayısı + saha günü).
- Takvim durumu: çözüldü / tahmin / yetki yok (her biri ayrı anlatım).
- **Kullanım:** bu kapasiteyi kullanan planlama oturumları ve **arz / talep**: kapasite (yapılabilir) ↔ planlanan ziyaret (talep); aşım uyarısı.

### 2.4 ⚠ Netleştirilecek (mockup ikisini de göstermesin; karar sonrası tek model)
Bugün aynı alanlar iki yerde farklı anlamda kullanılıyor (CT kod okuması, 2026-10-05):
- **Kapasite hesabı:** bir ziyaretin süresi = promo süresi + non-promo süresi (tek ürün gibi); rapor süresi **gün başına** düşülüyor (`CycleCapacity.MinutesPerVisit()`).
- **Ziyaret planlama (çok ürünlü ziyaret):** süre = promo ürün sayısı × promo süresi + non-promo ürün sayısı × non-promo süresi + rapor süresi (**ziyaret başına**) (`ActivityTimeBudgetCalculator.VisitDuration`).
- Mockup, **önerilen tek modeli** göstersin: ürün başına süre × ziyaretteki tipik ürün sayısı (sınırlar 3 / 3 ya da girilen "tipik ziyaret": örn. 2 promo + 1 non-promo) + ziyaret başına rapor süresi; günlük sabit işler (yol, sınav) gün başına. Kapasite hesabı bu "tipik ziyaret süresi"yle yapılır. Kesin model kullanıcı kararından sonra.

## 3) DURUMLAR (her ekranda çiz)
- Boş liste; yükleniyor; hata.
- Yetkisiz kullanıcı: sayfa görünmez, iskelet çizilmez, yönlendirme yapılmaz.
- Kapalı dönem (her şey salt okunur, nedeni yazılı); takvim çözülemedi (tahmin rozeti + neden); dönemin kapasitesi yok.

## 4) GENEL KURALLAR
- 7 dil (Türkçe, İngilizce, Fransızca, İspanyolca, Çince, Arapça, Rusça); Arapça için sağdan sola düzen.
- Klavye ile kullanılabilir; renkler tema değişkenleriyle.
- Masaüstü öncelikli (planlamacı ekranı), tablette de çalışmalı.
- Sayılar yerel biçimde (binlik ayırıcı), dakika ↔ saat dönüşümü ipucuyla.

## 5) MOCKUP'TA BEKLENEN EKRANLAR
1. Dönemler — tablo + yıl zaman çizelgesi.
2. Dönem oluştur / düzenle (kapsam seçimi, çakışma uyarısı, "geçerli dönem" aracı).
3. Dönem ayrıntısı (bağlı kayıtlar, yaşam döngüsü eylemleri).
4. Dönem Kapasitesi listesi.
5. Kapasite oluştur / düzenle — canlı hesaplı (aylık tablo + sağ özet).
6. Kapasite ayrıntısı — hesap şelalesi, ay ay grafik, arz / talep.

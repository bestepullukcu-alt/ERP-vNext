# Ziyaret Planlama — manuel test rehberi (2026-10-08)

> Main'e birleşen sürüm için. **Kiracı 97c5**, giriş **Admin User**, dönem **Türkiye 2026 Q4 Döngüsü**. Bugün 8 Ekim Perşembe; içinde bulunulan hafta 41. hafta, bu haftada yalnız Perşembe ve Cuma kaldı.
> Kutucukları işaretleyerek ilerleyin. Beklenenden farklı bir şey görürseniz adım numarasıyla yazın.

## 0. Hazırlık — eski planları temizleme
- [ ] Önce neyin silineceğine bakın (hiçbir şey silmez):
  ```bash
  py scripts/data-load/reset_visit_planning_97c5.py
  ```
  Beklenen: 19 plan, 213 planlanan ziyaret, 2 rapor silinecek; plana bağlı olmayan 4 ziyaret kalacak.
- [ ] Silin. Betik önce Masaüstü'ne `vp-reset-97c5-backup-<tarih>.json` yedeği yazar, sonra siler:
  ```bash
  py scripts/data-load/reset_visit_planning_97c5.py --apply
  ```
- [ ] Ziyaret Planlama → **Benim planlarım** boş olmalı.

Geri almak gerekirse: `py scripts/data-load/reset_visit_planning_97c5.py --restore <yedek dosyası>`.

## 1. Yeni plan
- [ ] **1.1** "Yeni plan" çekmecesini açın.
  - Beklenen: ülke, dönem, hafta ve salt okunur temsilci "Admin User" görünür. Segment ya da strateji alanı yoktur.
- [ ] **1.2** Q4 dönemini seçip planı oluşturun → plan detayı açılır.
  - Üstte özet, "Taslak" rozeti, eylem kartı ve iki kapasite kartı görünür.
- [ ] **1.3** Aynı dönem için ikinci bir plan oluşturmayı deneyin.
  - Beklenen: "zaten bir planınız var" uyarısı çıkar (tek plan kuralı), ikinci plan oluşmaz.

## 2. Hedefler
- [ ] **2.1** Soldaki "Hesaplarım (bölgem)" listesinde aramaya `MECİDİYEKÖY` yazın.
  - Beklenen: Türkçe büyük / küçük harf fark etmeden bulur (`mecidiyeköy` de bulmalı).
- [ ] **2.2** Şu üç kurumun doktorlarını seçin (birbirine 2 km içinde):
  - **75.YIL MECİDİYEKÖY AİLE SAĞ. MRK** (8 doktor)
  - **AKBANK SAĞLIK MERKEZİ** (4)
  - **APEX UYGULAMA VE ARAŞTIRMA MRK.** (10)
  - Her kurumda doktor tablosundan 3–4 doktor işaretleyin.
- [ ] **2.3** Doktor tablosunu kontrol edin.
  - 8 sütun görünür: seçim, Doktor, Uzmanlık, Ürünler, Sıklık, Yapılan / kalan, Son ziyaret, Durum.
  - Sıklığı olmayan doktorda "sıklık yok" rozeti çıkar.
  - Hızlı filtreler sayılıdır ("Bu hafta görülmesi gerekenler (N)").
- [ ] **2.4** Bir doktorda **Ürün seç**'e basın, **TUTUKON**'u tanıtım, **ALMIBA**'yı hatırlatma olarak ekleyip **Tamam**'a basın.
  - "Ürünler kaydedildi" mesajı çıkar.
  - Çipte ürün **adı** görünür, kod görünmez.
  - Hatırlatma çipi çerçeveli beyazdır.
  - ALMIBA'da "onaylı içerik yok" uyarı simgesi çıkabilir.
- [ ] **2.5** Toplu seçim: bir kurumda birkaç doktor seçili iken **Ürün uygula (N)** ile TUTUKON'u uygulayın.
  - Mevcut seçimler korunur, TUTUKON eklenir.
- [ ] **2.6** Sağdaki **Seçim özeti**ne bakın.
  - Doktor / eczane / hesap sayıları doğru.
  - "Bu hafta ≈ X sa" ile detay üstündeki "Bu hafta planlanan" aynı değer.
  - Ürün dağılımı adlarla yazılır.
- [ ] **2.7** **Seçilenler** listesinde bir kurum başlığındaki **Kaldır**'a basın → o kurumun doktorları seçimden çıkar. Ardından kurumu yeniden ekleyin.
- [ ] **2.8** **Hedefleri kaydet**'e basın.

## 3. Haftalar
- [ ] **3.1** Haftalar sekmesine geçin.
  - Şerit: dikey kartlar; 41. hafta "BUGÜN"; 44. haftada "29 Eki" tatil işareti; boş haftalar kesik çizgili.
- [ ] **3.2** 41. haftayı açın.
  - Gün satırları "Pzt 5 Eki … Cum 9 Eki". Geçmiş günler boştur.
  - Ziyaretler Perşembe ve Cuma'ya dağılır; her satırda "N / 57" ve "boş X sa".
  - Günü açınca kurum başlıkları altında doktorlar, ürün adları ve süreler görünür.
- [ ] **3.3** **Ürün başına haftalık ziyaret** bölümünde ürünler adla ve kalın sayıyla görünür.
- [ ] **3.4** Bir doktoru **Güne taşı…** ile Perşembe'den Cuma'ya taşıyın, **Yalnız bu doktor**'u seçin.
  - O doktor Cuma'ya geçer ve sabit simgesi alır.
  - Aynı kurumdaki diğer doktorlar **yerinde kalır**.
- [ ] **3.5** **Sabiti kaldır** → doktor eski gününe döner.
- [ ] **3.6** Bir kurumu bütün olarak (**Kurumu başka güne taşı…**) Cuma'ya taşıyın → kurumun bütün doktorları birlikte gider. Sabiti kaldırın.
- [ ] **3.7** Boş bir haftayı seçin (ör. 43. hafta).
  - Yalnız "Bu hafta için plan yok" kutusu ve **Bu haftayı üret** düğmesi görünür; gün satırları görünmez.
- [ ] **3.8** **Bu haftadaki doktorlar** listesinde bir doktora tıklayın; doktor paneli açılır.
  - Üç kutu: "dönemde 1 (varsayılan)", Yapılan, Kalan.
  - "Sıradaki ziyaretin ürünleri" ürün adlarıyla, kaynak ve süre ("1 tanıtım + 1 hatırlatma + rapor ≈ N dk").
  - Dönemin bütün haftaları listelenir.

## 4. Rota
- [ ] **4.1** Rota sekmesinde Perşembe'yi seçin. Duraklar haritada ve listede görünür; gün düğmeleri "Per 8 Eki" biçimindedir.
- [ ] **4.2** Bir durağı gün içinde sürükleyerek sırasını değiştirin → saatler yeniden hesaplanır.
- [ ] **4.3** Bir durağı Cuma sekmesine sürükleyin (ya da durak menüsünden **Güne taşı…**) → durak Cuma'ya geçer ve Cuma sekmesi açılır. Sabiti kaldırın.
- [ ] **4.4** Hafta açılır listesinde 43. haftayı seçin → liste boş kalmaz.

## 5. Onay ve sonrası
- [ ] **5.1** Haftalar sekmesinden 41. haftayı **Haftayı onayla** ile onaylayın ve onay penceresini kabul edin.
  - Sayfa yine **Haftalar**'da ve **41. hafta**da kalır; durum "Onaylı" olur.
- [ ] **5.2** **Planlanan Ziyaretler** sayfasında bu haftanın ziyaretleri doktor / kurum **adlarıyla** görünür (GUID yok).
- [ ] **5.3** 42. hafta ve sonrası **taslak** olarak dolmuş olmalı: sıklığa göre, sığmayanlar sonraki haftalara.
- [ ] **5.4** Onaylı haftada güne taşıma kapalıdır; "Değiştirmek için haftayı yeniden açın" yazar.

## 6. Ziyaret raporu
- [ ] **6.1** **Ziyaret Yürütme**'de bugünün (Perşembe) bir ziyaretini açın → "Ne sunacağım" bölümünde ürünler ve içerik adımları görünür.
- [ ] **6.2** Raporu "Tamamlandı" olarak gönderin.
- [ ] **6.3** Yarının (Cuma) bir ziyaretini kapatmayı deneyin → "henüz zamanı gelmedi" hatası çıkar.
- [ ] **6.4** Ziyaret Planlama'ya dönün.
  - O doktorda "yapılan 1" görünür.
  - Haftalar'daki doktor şeridinde "yapıldı" noktası (yeşil) çıkar.

## 7. Yeniden açma
- [ ] **7.1** 41. haftada **Haftayı yeniden aç**'a basın.
  - Gerekçe en az 10 karakter olmalı (sayaç görünür); gerekçeyi yazıp açın.
  - Sayfa Haftalar'da ve 41. hafta'da kalır; durum "Taslak" olur.
  - Raporu olmayan ziyaretler Planlanan Ziyaretler'de **iptal** olur, raporlu ziyaret kalır.

## 8. Uzak kurum
- [ ] **8.1** Hedeflere farklı şehirden bir kurum ekleyin (ör. Konya **01 NOLU BAHÇELİEVLER AİLE SAĞ.MR** ya da Şanlıurfa **06 NOLU TOKİ**).
  - Haftalar'da bu kurum ya kendi gününe düşer ya da **Kaydırılan** listesinde "yakın gün yok" nedeniyle sonraki haftaya geçer.
  - "Hafta dolu" yazmamalı.

## 9. Dil
- [ ] **9.1** Arayüzü **English** yapın → tarihler "Mon 5 Oct", aralık "28 Sep–2 Oct".
- [ ] **9.2** Arayüzü **العربية** yapın.
  - Sayfa sağdan sola akar.
  - "018 KLİNİK" gibi adlar ters dönmez.
  - "0 / 5" gibi sayılar ters dönmez.
- [ ] **9.3** Türkçeye dönün.

## Bilinen sınırlar (hata saymayın)
- Kurum türü ve uzmanlık İngilizce görünür ("Clinic", "Family Medicine"): referans verisine Türkçe ad girilince düzelir.
- İl adı "Sanliurfa" gibi ş'siz çıkabilir: il etiketi yok.
- Doktorların çoğunda sıklık tanımlı değil ("sıklık yok" → dönemde 1 sayılır).
- Rotada "Toplam yol süresi —": başlangıç konumu yoksa yol süresi hesaplanmaz.

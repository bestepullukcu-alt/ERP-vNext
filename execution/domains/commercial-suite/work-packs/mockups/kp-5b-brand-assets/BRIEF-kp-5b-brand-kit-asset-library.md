# MOCKUP BRIEF — Marka Kitleri + Onaylı Görsel Kütüphanesi (İçerik Stüdyosu)

> Tarih: 2026-10-02 · Hazırlayan: CT.
> **Kararlar:**
> - **Yerleşim (DEC-SCMM-05):** görsel dosyaları Platform **Belge Yönetimi**'nde kalır; "onaylı tanıtım görseli" kuralları ve **marka kiti** İçerik Stüdyosu'nda (Pazarlama) yönetilir. Menüde yeni grup: **"İçerik Stüdyosu"**.
> - Marka kiti **ürün başına** (Bilgi Yolu, iddia ve güvenlilik metniyle aynı ürün: MDM Global Product). MDM marka kaydı yalnız bilgi amaçlı bağlanır.
> - Her ikisi de **onaydan geçer**. Onaycı (Regülasyon tek adım mı, MLR mı) henüz kararlaştırılmadı — mockup onay panelini genel çizsin ("Onay adımları").
>
> **Mockup yapana not:** Görsel tasarım dili senin kararın. Aşağıdakiler amaç, içerik ve kurallardır. Mevcut ürün düzenine (sol menü, üst çubuk, kart yapısı, açık / koyu tema) ve **Bilgi Yolu Stüdyosu mockup'ının** diline (`mockups/kp-studio/kp-studio-prototype-v2.html`) uy. Önceki mockup'ta görsel bloğunun yer tutucusu "Onaylı varlık seçin · Belge Yönetimi" ve varlık kodu `VRL-0140` gibiydi; aynı dili sürdür.

## BAĞLAM
- **Kullanıcılar:** çok ülkeli bir ilaç şirketinin pazarlama, marka ve medikal ekibi; onaycılar Regülasyon (Ruhsat) ve gerekirse Medikal / Hukuk. Ülkeler: Türkiye (merkez), Belarus, Özbekistan, Türkmenistan, Gürcistan, Azerbaycan.
- **Neden:** Bilgi Yolu Stüdyosu'nun **sayfa tasarımcısı** (sonraki faz) sayfaları sürükle-bırak tasarlatacak. Tasarım serbest ama:
  - renkler, logolar, yazı tipleri **ürünün marka kitinden** gelmeli;
  - görseller yalnız **onaylı görsel kütüphanesinden** seçilebilmeli (lisansı geçerli, o ülke ve ürün için izinli, alternatif metni olan).
- **Bugün var olan:**
  - Belge Yönetimi'nde belge listesi, ayrıntı, sürüm geçmişi ve önizleme (dosya tarayıcıda açılır). **Küçük resim / galeri yok.**
  - MDM'de Markalar sayfası (kod, ad, durum) — logo / renk / yazı tipi yok.
  - İddialarda "kanıt belge seç" iki adımlı penceresi (arama + önizleme) — görsel seçiciye örnek olabilir.
  - Güvenlilik Metinleri ve Ülke Yasal Profilleri sayfaları (liste, ayrıntı, sürüm geçmişi, onay paneli) — aynı yaşam döngüsü burada da kullanılacak.

## 1) MENÜ — "İçerik Stüdyosu" grubu
- Bilgi Yolları, İddialar, Güvenlilik Metinleri, Ülke Yasal Profilleri, **Marka Kitleri**, **Görsel Kütüphanesi**.
- Yetkisi olmayan kullanıcı ilgili öğeyi görmez; sayfa iskeleti de çizilmez.

## 2) MARKA KİTLERİ
### 2.1 Liste
- **Sütunlar:** kod (`BK-…`), ürün, sürüm, durum (taslak / incelemede / aktif / yerini aldı / arşiv), renk sayısı, logo sayısı, son güncelleme, kullanan Bilgi Yolu sayısı.
- **Filtreler:** ürün, durum; "yalnız aktif".
- Her ürün için **tek aktif** sürüm.

### 2.2 Ayrıntı / düzenle (yalnız taslak düzenlenir)
- **Kimlik:** ürün (oluşturunca değişmez), bağlı MDM markası (bilgi), sürüm, durum çizgisi.
- **Renk paleti:**
  - her renk: ad (7 dil), HEX, rol (birincil / ikincil / vurgu / metin / zemin), "metinde kullanılabilir", "zeminde kullanılabilir";
  - renk + zemin kontrast uyarısı (erişilebilirlik);
  - sıralanabilir kartlar.
- **Logolar:**
  - varyantlar: ana, tek renk, ters (koyu zemin);
  - her biri **görsel kütüphanesinden** seçilir (onaylı varlık);
  - en küçük boyut (px / mm), boşluk payı, hangi zeminlerde kullanılabilir.
- **Yazı tipleri:** başlık ve gövde ailesi, yedek aileler; isteğe bağlı yazı tipi dosyası (Belge Yönetimi'nden).
- **Tipografi kuralları:** en küçük yazı boyutu (örn. 10 pt), başlık ölçeği.
- **Kullanım notları:** serbest metin (yapılmaması gerekenler).
- **Önizleme kartı:** paletin, logonun ve yazı tiplerinin örnek bir sayfa parçasında nasıl göründüğü (açık + koyu zemin).
- **Sürüm geçmişi** ve **onay paneli** (karar veren yorumla onaylar / reddeder; reddetmek yorum ister; gönderen kendi kaydında paneli görmez).
- **Nerede kullanılıyor:** bu kiti kullanan Bilgi Yolları ve sayfa şablonları.
- **Eylemler:** Düzenle, Onaya gönder, Geri çek, Yeni sürüm, Arşivle (durumuna göre).

## 3) GÖRSEL KÜTÜPHANESİ (onaylı tanıtım görselleri)
### 3.1 Galeri
- **Küçük resimli ızgara** (liste görünümü de olsun).
- **Kartta:** küçük resim, varlık kodu (`VRL-…`), başlık, tür (fotoğraf / diyagram / logo / ikon / grafik görseli), durum rozeti, ülke rozetleri, ürünler, **lisans bitiş tarihi** (yaklaşıyorsa uyarı, geçtiyse "süresi doldu").
- **Filtreler:** ürün, ülke, tür, durum (taslak / incelemede / onaylı / süresi doldu / geri çekildi), "yalnız kullanılabilir", lisans bitişi (30 gün içinde).
- **Arama:** başlık, kod, etiket.

### 3.2 Yeni görsel
- **İki yol:**
  - (a) Belge Yönetimi'ndeki **mevcut bir belgeyi seç**;
  - (b) **dosya yükle** (Belge Yönetimi'ne kaydedilir, buradan bağlanır).
- **Belge sürümü sabitlenir:** belgenin yeni sürümü gelirse görsel kendiliğinden değişmez. "Yeni sürüm var — güncelle?" uyarısı ve yeniden onay çizilmeli.
- **Alanlar:**
  - başlık;
  - tür;
  - **alternatif metin (7 dil, zorunlu)**;
  - altyazı / kaynak (credit);
  - **ürünler** (bir veya çok);
  - **ülkeler** (kısıt; boşsa tüm ülkeler);
  - **kullanım hakkı:** kaynak (ajans / stok / kurum içi), lisans türü, hak sahibi, geçerlilik başlangıcı, **bitiş tarihi**;
  - etiketler.
- **Otomatik gösterilenler:** boyut (genişlik × yükseklik), dosya türü, dosya büyüklüğü.
- **Uyarılar:** düşük çözünürlük, desteklenmeyen tür.

### 3.3 Görsel ayrıntısı
- Büyük önizleme (açık / koyu zemin); tüm alanlar.
- Belge Yönetimi'ndeki kaynak belgeye bağlantı + sabitlenen sürüm.
- **Onay paneli** + sürüm geçmişi.
- **Nerede kullanılıyor:** Bilgi Yolları, sayfalar, marka kitleri (logo olarak).
- **Eylemler:** onaya gönder, geri çek, yeni sürüm, **geri çek (yayından)**, arşivle.
- Görsel geri çekilir ya da lisansı biterse kullanan yollara uyarı gider (ekranda "etkilenen 3 yol" gibi).

## 4) ORTAK GÖRSEL SEÇİCİ (pencere)
- **Kullanıldığı yerler:** marka kiti logo alanları; sayfa tasarımcısının görsel bloğu (sonraki faz); bilgi içeriği formu.
- **Bağlamla açılır** (ürün + ülke + dil): yalnız **kullanılabilir** görseller (onaylı, süresi dolmamış, o ülke ve ürün için izinli, o dilde alternatif metni var) seçilebilir.
- Diğerleri gri ve nedeni yazılı: "Bu ülke için izinli değil", "Lisans 12.09 tarihinde doldu", "Türkçe alternatif metin yok", "Onay bekliyor".
- Küçük resim ızgarası, filtre, arama, büyük önizleme, "Seç".

## 5) KÜÇÜK EKLER (mevcut sayfalar)
- **Belge Yönetimi belge ayrıntısı:** görsel türündeki belgelerde satır içi önizleme; "Onaylı görsel olarak kullanılıyor (VRL-0140)" rozeti.
- **MDM Marka / Global Ürün ayrıntısı:** salt okunur "Marka kiti" bağlantısı.

## 6) DURUMLAR (her ekranda çiz)
- Boş liste; yükleniyor; hata.
- Yetkisiz kullanıcı: sayfa görünmez, iskelet çizilmez, yönlendirme yapılmaz.
- Süresi dolmuş, ülkeye kapalı, alternatif metni eksik görsel; onay bekleyen kayıt (düzenlenemez).
- Belge Yönetimi'nde belgeyi görme yetkisi olmayan kullanıcı (önizleme yerine kilitli kart).

## 7) GENEL KURALLAR
- **7 dil** (Türkçe, İngilizce, Fransızca, İspanyolca, Çince, Arapça, Rusça); Arapça için sağdan sola düzen.
- Klavye ile kullanılabilir.
- Renkler tema değişkenleriyle.
- Tablet yatay öncelikli; masaüstünde de çalışmalı.

## 8) MOCKUP'TA BEKLENEN EKRANLAR
1. Menü (İçerik Stüdyosu grubu).
2. Marka Kitleri listesi.
3. Marka kiti ayrıntı / düzenle: palet, logolar, yazı tipleri, kurallar, önizleme, sürüm, onay paneli.
4. Görsel Kütüphanesi galerisi + filtreler.
5. Yeni görsel (iki yol) + alanlar.
6. Görsel ayrıntısı: onay paneli, nerede kullanılıyor, lisans uyarısı.
7. Ortak görsel seçici penceresi (gri / nedenli öğelerle).
8. Belge Yönetimi belge ayrıntısındaki küçük ek.

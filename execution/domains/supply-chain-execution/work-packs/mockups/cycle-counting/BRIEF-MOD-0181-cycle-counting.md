# MOCKUP BRIEF — MOD-0181 Sayım (Cycle Counting)

> Tarih: 2026-11-06 · Hazırlayan: MVP-5 CT. Kaynak: `execution/domains/supply-chain-execution/module-packs/MOD-0181-cycle-counting.md`
> (r4, taslak; §4, §8.2–§8.5, §8.13, §25).
> **Mockup yapana not:** Görsel tasarım dili senin kararın. Aşağıdakiler amaç, içerik ve kurallardır. Mevcut ürün düzenine (sol menü, üst
> çubuk, kart yapısı, açık / koyu tema) ve depo ekranlarının bugünkü görünümüne (Yerleştirme Görevleri, Lokasyon Rolleri) uy. Paket henüz
> onaylanmadı; mockup kuralları değiştirmez, kural ile ekran çelişirse kural kazanır ve çelişkiyi not düş.

## BAĞLAM
- **Kullanıcılar:** depo sorumlusu (sayımı planlar ve başlatır), sayan (el terminali ya da telefonla rafları sayar), onaylayıcı (farkları
  onaylar), denetçi / kalite (kanıt paketini indirir). Çok şirketli, çok depolu bir ilaç dağıtım şirketi.
- **Ne:** depodaki fiziksel stoğun sistemdeki stokla karşılaştırılması. Fark çıkarsa onaydan sonra stok defterine düzeltme hareketi
  (sayım kazancı / kaybı) yazılır. Depo sayım sırasında **durmaz**: raflar kilitlenmez, sayım sırasında olan hareketler hesaba katılır.
- **Akış:** Sayım oluştur (taslak) → Başlat (o anın stoğu "anlık görüntü" olarak alınır) → Say (bin bin miktar gir, bulunan fazla stoğu
  ekle) → Gönder (farklar hesaplanır) → Onayla / Reddet / Yeniden say → Stok düzelir → Sayım kapanır.
- **Görünen kod:** `{ŞİRKET}-CNT-000042` (örnek `GMG-CNT-000042`).

## 1) SAYIMLAR PANOSU — `/Warehouse/CycleCounts` (sayfa `CYCLECOUNTS`, menüde)
### 1.1 KPI kartları (üstte)
- Açık sayımlar · Onay bekleyen fark satırı · Reddedilen / rezervasyon engelli düzeltme · Dönem doğruluğu % (farksız satır / sayılan satır).
- Elle girilmiş sayım düzeltmesi sayısı (Stok Hareketleri'nden girilenler; bilgi kartı).

### 1.2 Liste (tablo)
- **Sütunlar:** kod, depo, tür (Döngüsel / Tam), kör sayım rozeti, kapsam özeti (ör. "3 bölge · 42 raf"), planlanan tarih, sayan, durum,
  satır sayısı (sayılan / toplam), fark satırı sayısı, oluşturan, oluşturma tarihi.
- **Durumlar (rozet):** Taslak · Başlatıldı · Sayılıyor · Gönderildi · Kapandı · İptal.
- **Süzgeçler:** depo, tür, durum, tarih aralığı, sayan, "yalnız benim sayımlarım".
- **Satır menüsü:** Aç, Sayım formuna git, Başlat (taslakta), İptal (gerekçe ister), Yeniden say (gönderilmişte, satır seçerek).
  **"Sil" yok**, yerine "İptal" var. Toplu iptal: yalnız taslaklar.

### 1.3 Oluştur / düzenle (yan panel ya da modal — **8 alan**, Slim)
1. Depo (zorunlu)
2. Tür: Döngüsel (seçili kapsam) | Tam (deponun tüm rafları)
3. Kör sayım (anahtar): açıksa sayan beklenen miktarı **görmez**
4. Kapsam: bölge / alan / raf seçimi (ağaç; Döngüsel'de 1–200 düğüm; Tam'da gizli)
5. Ürün süzgeci (isteğe bağlı, en çok 200; boş = kapsamdaki tüm ürünler)
6. Planlanan tarih (bugün ya da sonrası)
7. Sayan (kullanıcı seçimi; isteğe bağlı)
8. Not (≤ 500)
- Oluşturunca durum **Taslak**. "Başlat" ayrı ve onaylı eylem: "Başlatınca o anın stoğu alınır ve kapsam değişmez."
- **Çakışma uyarısı:** seçilen raflardan biri başka açık bir sayımda ise başlatma reddedilir; hangi sayım olduğu bağlantıyla gösterilir.

### 1.4 Ayrıntı
- Özet: kod, durum çizgisi (Taslak → … → Kapandı), kim / ne zaman başlattı, gönderdi, iptal etti (gerekçe).
- Anlık görüntü zamanı; kapsamdaki raf sayısı; satır sayıları.
- Olay geçmişi (zaman çizelgesi). **Kör sayımda gönderilmeden önce** olay geçmişinde beklenen miktar ya da fark görünmez.
- Yeniden sayım zinciri: "Bu sayım şu sayımın yeniden sayımıdır" bağlantısı; en çok 2 yeniden sayım.

## 2) SAYIM FORMU — `/Warehouse/CycleCounts/Sheet/{countId}` (sayfa `COUNTSHEET`; **375 px telefon / el terminali öncelikli**)
- Raf raf gruplanmış satırlar. Satır: raf kodu, ürün (ad + kod), lot (lotlu üründe zorunlu), birim, **beklenen** (kör sayımda **gösterilmez**),
  **sayılan** (giriş), durum (Açık / Sayıldı).
- **Miktar girişi:** büyük sayı alanı + −/+ düğmeleri; dokunma hedefleri en az 48 × 48 px; onay düğmesi ekranın altında sabit çubukta.
- **Barkod:** üstte okutma alanı (USB / Bluetooth okuyucu klavye gibi yazar; kamera ile okutma ileride MOD-0182). Okutulan raf / ürün ilgili
  satıra götürür. Aynı ürünün tekrar okutulması miktarı artırır ve ekranda görünür; seri numaralı üründe aynı seri iki kez okutulamaz.
- **Bulunan stok ekle:** listede olmayan bir ürün / lot rafta bulunduysa yeni satır (beklenen 0). Yalnız sayımın ürün süzgecindeki ürünler;
  lot sistemde kayıtlı olmalı.
- **Seri numaralı ürün:** satır sayılır ama fark **stok defterine otomatik yazılmaz**; satırda bilgi notu.
- **Gönder:** tüm satırlar sayılmadıysa engellenir ("12 satır sayılmadı" + o satırlara git). Gönderince farklar hesaplanır.
- **Bağlantı yoksa:** açık uyarı, giriş kaydedilmez (çevrimdışı ilk sürümde yok).
- Sayım sırasında rafta hareket olduysa satırda "sayım sırasında hareket oldu" işareti (gönderimden sonra).

## 3) FARK İZİ — `/Warehouse/CountVariances` (sayfa `COUNTVARIANCES`, menüde)
- **Satır:** sayım kodu, raf, ürün, lot, beklenen (başlangıçta), sayım anına kadar hareket, sayım anında beklenen, sayılan, **fark** (+/−,
  renkli), kaynak rozeti (**Sayım** | **Elle**), işaretler (sayım sırasında hareket · stok tutarsız · seri numaralı), durum (Farksız / Onay
  bekliyor / Onaylandı / Reddedildi / Yeniden sayıldı), düzeltme durumu (Bekliyor / Kaydedildi + hareket numarası bağlantısı / Reddedildi
  (rezervasyon engeli) / Bütünlük hatası).
- **Süzgeçler:** depo, ürün, lot, tarih, durum, **kaynak** (varsayılan Sayım | Elle | hepsi; Elle ve hepsi için depo + en çok 31 gün zorunlu).
- **Eylemler (yalnız Sayım satırlarında):** Onayla · Reddet (gerekçe) · Yeniden say · Yeniden dene (rezervasyon engelinde) · Bütünlüğü çöz.
  **Elle** satırlar salt okunur.
- **Kurallar ekranda anlatılmalı:**
  - **Dört göz:** satırı sayan ya da sayımı gönderen kişi onaylayamaz (düğme pasif + neden).
  - **Tolerans:** şirket ayarındaki tolerans içindeki farklar "Sistem (tolerans)" tarafından onaylanmış görünür. Varsayılan tolerans 0'dır,
    yani her fark onay ister.
  - **Stok tutarsız** işaretli satır onaylanamaz; yalnız yeniden sayılabilir ya da reddedilebilir.
  - Onay stoğu **hemen** düzeltir; geri alma Stok Hareketleri'nde ters kayıtla yapılır (bilgi bandı).

## 4) KANIT PAKETİ — `/Warehouse/CountEvidenceExports` (sayfa `COUNTEVIDENCE`, menüde)
- Sayım seç → "Dışa aktar" (PDF | CSV) → listede durum (Hazır / Başarısız), isteyen, tarih, dosya özeti (sha256), İndir.
- Dosya, isteyen kullanıcının dilinde üretilir. PDF'te gösterilemeyen yazı (ör. Çince, Arapça) olursa İngilizceye düşülür ve bu dosyada
  yazılır.
- Paketin içeriği (önizleme kartı): sayım özeti, satırlar ve farklar, onay / ret kararları (kim, ne zaman, gerekçe), düzeltme hareket
  numaraları, elle düzeltmeler ayrı bölümde.

## 5) DURUMLAR (her ekranda çiz)
- Boş liste; yükleniyor; hata (servis yok → "Stok servisine ulaşılamadı").
- Yetkisiz kullanıcı: sayfa görünmez, iskelet çizilmez, yönlendirme yapılmaz.
- Kapanmış / iptal sayım: her şey salt okunur, nedeni yazılı.
- Çakışma: başka bir kullanıcı aynı satırı değiştirmişse "Kayıt değişti, yenileyin" uyarısı.
- Kör sayımda beklenen ve fark alanları sayan rolüne gönderimden önce hiç görünmez.

## 6) GENEL KURALLAR
- 7 dil (Türkçe, İngilizce, Fransızca, İspanyolca, Çince, Arapça, Rusça); Arapça için sağdan sola düzen.
- Her sayfada "Yardım" paneli (sayfa yardımı, adım adım). Tur yok.
- Telefon genişliği (375 px) sayım formunda zorunlu, diğer ekranlarda okunabilir olmalı; yatay sayfa kaydırması yok.
- Klavye ile kullanılabilir; renkler tema değişkenleriyle; fark renkleri yalnız renge dayanmaz (+/− işareti ve metin de var).
- Sayılar ondalıklı olabilir (birime göre); tarih ve sayı biçimi kullanıcının diline göre.
- Şirket sayım ayarları (tolerans, en fazla yeniden sayım, sayım başına satır sınırı) ilk sürümde **ekransızdır**; mockup'ta yalnız
  "ayarlar" bilgi kartı olarak gösterilebilir.

## 7) İSTENEN ÇIKTI
- Tek dosyalık tıklanabilir HTML prototip (örnek veriyle) + ekran metinleri listesi (çeviri için anahtar adı önerisiyle).
- Ekranlar: Pano (liste + KPI + oluştur paneli + ayrıntı), Sayım formu (375 px ve masaüstü), Fark izi, Kanıt paketi, durum örnekleri.
- Kuraldan saptığın ya da emin olmadığın her yer için kısa bir "açık soru" listesi.

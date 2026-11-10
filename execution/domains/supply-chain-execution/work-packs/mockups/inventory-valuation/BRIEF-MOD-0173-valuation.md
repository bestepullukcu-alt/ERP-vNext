# MOCKUP BRIEF — MOD-0173 Stok Değerleme (S3 Valuation)

> Tarih: 2026-11-10 · Hazırlayan: MVP-1 CT. **Paket yok** — S3, MOD-0173 paketinde dilim olarak adlandırılmış ama yazılmamıştır
> (pack satır 52: "production-ready prerequisite, not a G1 box"). Kaynaklar: MOD-0173 paketi (§1, S3 satırı; `CostingMethodPolicy` /
> `InventoryCostLayer` / `InventoryValuation`), `docs/analysis/inventory-capability-scope-and-dependency-report.md` (DEC-INV-06 ve
> DEC-INV-18 — ÇÖZÜLDÜ 2026-09-08; ekran listesinde "Inventory Valuation"), Blueprint 8.8 (MOD-0173 "Inventory Ledger & Valuation").
> **Mockup yapana not:** Görsel tasarım dili senin kararın. Mevcut stok ekranlarına (Stok Defteri, Stok Hareketleri, Lokasyonlar) uy.
> Bağlayıcı olan yalnız DEC-INV-06 ve DEC-INV-18'dir; geri kalan kurallar **öneridir**, paket kesinleştirir. Emin olmadığın yerleri
> "açık soru" olarak yaz.

## BAĞLAM
- **Kullanıcılar:** maliyet muhasebecisi (yöntem tanımlar, değerlemeyi izler, düzeltme yapar), finans yöneticisi (dönem değerini onaylar,
  rapor alır), stok sorumlusu (yalnız okur), denetçi (iz ve kanıt ister).
- **Ne:** stoktaki malın **parasal değeri**. Her stok girişi bir birim maliyetle gelir; çıkışlar seçilen yönteme göre maliyetlenir; bakiye
  her an miktar **ve** tutar olarak bilinir.
- **Bağlayıcı kararlar:**
  - **DEC-INV-06:** maliyet yöntemi **şirket × ürün** bazında seçilir; üç yöntem: **Hareketli ortalama** (varsayılan), **Standart**, **FIFO**.
    Tanımsızsa hareketli ortalama; finans değiştirebilir.
  - **DEC-INV-18:** değerleme **şirket bazında**, şirketin **ana para biriminde**.
- **Ne değil (öneri):** muhasebe fişi kesmek (genel muhasebe / finans modülünün işi — biz değer olayı yayınlarız), fatura eşleştirme,
  ithalat masrafı dağıtımı (landed cost) ilk sürümde yok. Açık soru.

## 1) DEĞERLEME PANOSU — `/Inventory/Valuation` (menüde)
### 1.1 Üst seçim
- Şirket (zorunlu; şirket seçici mevcut desen), tarih ("bugün" ya da geçmiş bir gün — o günün değeri), depo (isteğe bağlı).
### 1.2 KPI kartları
- Toplam stok değeri (şirket para birimi) · Önceki aya göre değişim · Maliyeti eksik giriş sayısı (birim maliyetsiz gelen hareket) ·
  Negatif ya da sıfır maliyetli bakiye · Bekleyen maliyet düzeltmesi.
### 1.3 Tablo
- **Sütunlar:** ürün (ad + kod), yöntem rozeti (Ort. / Std. / FIFO), miktar, birim, birim maliyet, toplam değer, son maliyet değişimi
  tarihi, uyarı ikonları.
- **Gruplama:** ürün · depo · lot (lot düzeyinde değer yalnız FIFO'da anlamlı — açık soru).
- **Süzgeçler:** yöntem, uyarılı olanlar, ürün grubu. CSV / Excel dışa aktarma.

## 2) ÜRÜN DEĞER AYRINTISI (satıra tıklayınca, yan panel ya da sayfa)
- Üst: ürün, şirket, yöntem, bugünkü miktar / birim maliyet / değer.
- **Hareketler sekmesi:** tarih, hareket no (Stok Hareketleri bağlantısı), tür, miktar (+/−), birim maliyet, tutar, sonrası ortalama maliyet,
  sonrası bakiye değeri.
- **Katmanlar sekmesi (yalnız FIFO):** giriş tarihi, kaynak hareket, ilk miktar, kalan miktar, birim maliyet. Tüketilen katmanlar gri.
- **Grafik:** zaman içinde birim maliyet çizgisi (son 12 ay).

## 3) MALİYET YÖNTEMİ AYARLARI — `/Inventory/Valuation/Policies` (menüde, yetkiye bağlı)
- Şirket bazında varsayılan yöntem kartı + ürün bazında istisnalar tablosu (ürün, yöntem, geçerlilik başlangıcı, değiştiren, gerekçe).
- **Yöntem değişikliği** (öneri): yalnız dönem başında geçerli olur, gerekçe ister, dört göz onayı ister, değişiklik önizlemesi gösterir
  ("bu değişiklik 42 ürünün değerini etkiler"). Geçmiş değerler yeniden hesaplanmaz.
- **Standart maliyet** tanımı: ürün × geçerlilik dönemi × birim maliyet; standart ile gerçek arasındaki fark "fark hesabı" olarak görünür.

## 4) MALİYET DÜZELTMESİ — `/Inventory/Valuation/Adjustments` (menüde)
- Liste: düzeltme no, ürün, eski / yeni birim maliyet, etkilenen miktar, tutar farkı, gerekçe, durum (Taslak / Onay bekliyor / Uygulandı /
  Reddedildi), oluşturan, onaylayan.
- Oluştur (Slim, ≤ 6 alan): ürün, (FIFO'da katman), yeni birim maliyet, geçerlilik tarihi, gerekçe (zorunlu), ek belge.
- **Kurallar (öneri):** miktar değişmez, yalnız değer; dört göz; uygulanınca geri alma ters düzeltmeyle; e-imza gerekip gerekmediği açık soru.

## 5) DÖNEM DEĞER RAPORU — `/Inventory/Valuation/Reports` (menüde)
- Ay sonu stok değeri (şirket, depo, ürün grubu kırılımı), dönem başı + girişler − çıkışlar = dönem sonu tablosu (miktar ve tutar),
  satılan malın maliyeti (çıkış türlerine göre). PDF / Excel; dosya isteyenin dilinde.
- "Dönemi kilitle" (öneri — açık soru): kilitli döneme geriye tarihli maliyet etkisi yazılamaz.

## 6) DURUMLAR (her ekranda çiz)
- Boş; yükleniyor; hata ("Stok servisine ulaşılamadı").
- Yetkisiz kullanıcı (UAS-001): sol menü ve üst çubuk kalır; içerikte **tek bir açıklama**. İskelet, yönlendirme, yalnız toast yok.
- **Değer görme ayrı izin** (öneri): miktarı gören herkes tutarı görmez; tutarsız görünümde değer sütunları hiç çizilmez.
- Maliyeti eksik hareket: satırda uyarı + "maliyet bekleniyor" rozeti.
- Para birimi her tutarın yanında; sayı ve tarih biçimi kullanıcının diline göre.

## 7) GENEL KURALLAR
- 7 dil; Arapça sağdan sola. Her sayfada "Yardım" paneli; tur yok.
- Masaüstü öncelikli; 375 px'te pano ve listeler okunabilir, yatay sayfa kaydırması yok.
- Klavye ile kullanılabilir; artış / azalış yalnız renge dayanmaz (+/− ve metin).

## 8) İSTENEN ÇIKTI
- Tek dosyalık tıklanabilir HTML prototip (örnek veri: 1 şirket TRY, 3 ürün — biri hareketli ortalama, biri standart, biri FIFO) +
  ekran metinleri listesi.
- Ekranlar: Pano, Ürün değer ayrıntısı (FIFO katmanlarıyla), Yöntem ayarları, Maliyet düzeltmesi, Dönem raporu, durum örnekleri.
- Açık sorular listesi. Bilinen açık sorular:
  - **V-1** Giriş maliyeti nereden gelir? Satın alma (MVP-2 mal kabul → bugün Inventory'ye **gerçek** yazmıyor, mock), elle giriş, açılış
    bakiyesi, üretim? Maliyeti olmayan girişte ne olur (ret mi, "maliyet bekleniyor" mu)?
  - **V-2** Muhasebe fişi: stok değeri olayını kim tüketir (genel muhasebe modülü hangisi, hangi MVP)? Biz yalnız olay mı yayınlarız?
  - **V-3** Transfer (şirket içi depo arası) değeri taşır; şirketler arası transfer satış / alış mı sayılır?
  - **V-4** İade, karantina, imha, sayım farkı (`COUNT_GAIN` / `COUNT_LOSS`) hangi maliyetle değerlenir?
  - **V-5** Yöntem değişikliği yalnız dönem başında mı? Dönem kilidi var mı?
  - **V-6** Ek maliyetler (navlun, gümrük — landed cost) ilk sürümde var mı? (öneri: yok)
  - **V-7** Değer görme ayrı izin mi? (öneri: evet)

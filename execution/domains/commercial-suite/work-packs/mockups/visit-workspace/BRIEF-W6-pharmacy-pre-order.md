# MOCKUP BRIEF — W6 eczane ön siparişi: sipariş listesi, yönetici onayı, fiyat ve MF / iskonto kuralları, depo belgesi

> **Tarih:** 2026-10-19 · **Hazırlayan:** CT · **İsteyen:** ürün sahibi ("W6 için önce mockup", 2026-10-19).
>
> **Dayanaklar:**
> - [Yol haritası](../../ROADMAP-visit-workspace.md):
>   - **K-W10** (CT önerisi, onay bekliyor): sipariş = **MOD-0168 Order Capture**; fiyat ve MF / iskonto kuralları = **MOD-0156 Price Lists & Discount Guardrails**. Ön sipariş bizim satış siparişimize dönüşmez; depoya iletilen bir transfer siparişidir, faturayı depo keser.
>   - **K-W9:** onaycı ve e-posta alıcısı = temsilcinin pozisyonunun "Reports to" pozisyonundaki kişi.
>   - **K-W13:** depo = CRM kurumu (Account, tür distribütör / ecza deposu).
> - **Mevcut mockup (çizilmiş — yeniden çizme, yalnız eksikleri ekle):**
>   - [05-eczane-on-siparis.html](decoded/05-eczane-on-siparis.html): temsilcinin ön sipariş ekranı. Eczane, ürün satırları (liste fiyatı KDV hariç, adet, MF, iskonto %), eczanenin önceki siparişleri, depo, teslim tarihi, para birimi, toplam, eczacı imzası, "Yönetici onayına gönder", e-posta önizleme.
>   - [06-epostalar.html](decoded/06-epostalar.html): bildirim e-postası şablonları.
>   - Demo verisi: [00-demo-veri.js](decoded/00-demo-veri.js).
>
> **Mockup yapana not:**
> - Ürünün mevcut görsel dilini kullan: sol menü, üst çubuk, kart yapısı, açık / koyu tema. Ana renk tema rengidir (sabit lacivert / mor yok).
> - Listeler **Golden Compact DataTable** düzeninde: süzgeç kartı + tablo + sağ üstte birincil düğme. Sekmeli sayfalar ortak sekme kartıyla (Görev Merkezi'ndeki gibi). Sayfa başlığı: başlık + tek satır açıklama.
> - **Dil:** Türkçe çiz; Arapçada sağdan sola düzen bozulmamalı.
> - **Ekran ve kullanıcı:**
>   - Ekran A (temsilci): **tablet ve telefon** öncelikli.
>   - Ekran B, C ve D (yönetici, satış operasyonu, ticari ekip): **masaüstü** öncelikli, tablette bozulmasın.
> - **Gerçekçi demo verisi:**
>   - Ürünler: TUTUKON 35 mg 60 tablet, ALMIBA 10 mg 30 tablet, GLUKOFIT 850 mg 60 tablet, ÜROFLEKS 0,4 mg 30 kapsül.
>   - Eczaneler: Şifa Eczanesi (Kadıköy), Merkez Eczanesi (Çankaya), Hayat Eczanesi (Konak).
>   - Depolar: Selçuk Ecza Deposu, Alliance Healthcare, Nevzat Ecza.
>   - Para birimleri: TRY; BAE için AED.
>   - Temsilci: Ayşe Yılmaz. Yöneticisi (Reports to): Bölge Müdürü Mehmet Kaya.

---

## Kavram ve kurallar (ekranların anlatması gerekenler)

1. **Ön sipariş bir transfer siparişidir.**
   - Temsilci eczanenin siparişini alır, sipariş seçilen **depoya** iletilir. Faturayı depo keser.
   - Ekranda "fatura" ya da "satış siparişi" dili kullanılmaz. Doğru terimler: **ön sipariş**, **depoya iletildi**.
2. **Durumlar:** Taslak → Onay bekliyor → Onaylandı / Reddedildi → Depoya iletildi. Ayrıca İptal edildi.
   - Kurallar içinde kalan sipariş için onay gerekip gerekmediği bir **kiracı ayarıdır**: ya kendiliğinden onaylanır ya da her zaman yöneticiye gider. Bu açık bir karar; mockup iki durumu da göstersin.
3. **Fiyat sistemden gelir.**
   - Satırdaki birim fiyat **fiyat listesinden** okunur ve temsilci değiştiremez.
   - Türkiye'de resmî fiyat listesi kullanılır: depocu satış fiyatı, eczacı fiyatı ve KDV oranı. Liste aylık güncellenir.
   - Diğer ülkelerde liste, şirket (tüzel kişilik) × para birimi bazında tutulur.
   - Satırda fiyatın hangi listeden ve hangi tarihten geldiği küçük harfle görünür.
4. **MF ve iskonto kuralları** ürün bazında tanımlanır. İsteğe bağlı olarak dönem ve eczane segmenti de kurala eklenebilir. Kural türleri:
   - en yüksek iskonto %;
   - en yüksek MF oranı. Örnek: "10+2" → etkin indirim 2 / 12 = %16,7;
   - birleşik etkin indirim tavanı (MF + iskonto);
   - en az / en çok adet.
5. **Sınırın üç hâli** (satırda renkli rozet ve açıklama ile):
   - **Sınır içi:** normal görünüm.
   - **Yumuşak sınır aşıldı:** sarı rozet, "Yönetici onayı gerekir: iskonto %12 > sınır %10". Gönderilebilir, onaya gider.
   - **Sert tavan aşıldı:** kırmızı rozet. Satır girilemez, gönder düğmesi kapalı.
6. **Onaycı** temsilcinin yöneticisidir (Reports to). Pozisyon boşsa bir üst pozisyona gider. Onay ekranında "Neden onayımda?" satırı aşılan kuralları listeler.
7. **Eczacı imzası** sipariş belgesinin parçasıdır. Onaydan sonra sipariş değiştirilemez; değişiklik için iptal edilip yeni sipariş açılır.
8. **Depoya iletim:** depo e-posta adresine PDF belgesi gider; eczaneye bir kopya. Ekranda "iletildi · tarih · alıcı" izi görünür.
   - Depo cevabını (teslim edildi / eksik) şimdilik elle işaretleme yeri olsun; otomatik entegrasyon yok.
9. **Görünürlük:**
   - Temsilci kendi siparişlerini görür.
   - Yönetici ekibinin siparişlerini görür.
   - Satış operasyonu tümünü görür.
   - Liste fiyatı ve kurallar ticari ekipte (pazarlama / satış operasyonu) yönetilir.

---

## Çizilecek ekranlar

### A — Temsilci ön sipariş ekranına ekler (mevcut 05 üzerine, yalnız fark)
- Satır başına **kural rozeti** (madde 5) ve fiyat kaynağı satırı (madde 3).
- Sipariş özetinde **"Onay gerekir mi?"** kutusu: kaç satır sınırı aştı, onaycı kim (ör. "Mehmet Kaya · Bölge Müdürü").
- Sert tavan aşıldığında düğmenin kapalı hâli ve açıklaması.
- Gönderdikten sonraki **durum zaman çizelgesi**: Gönderildi → Onay bekliyor → Onaylandı → Depoya iletildi; her adımda kim ve ne zaman.

### B — Ön siparişler listesi (temsilci, yönetici ve satış operasyonu)
- **Süzgeçler:** tarih aralığı, durum, temsilci (yönetici ve operasyon için), eczane, depo, ürün, "onayımı bekleyenler".
- **Sütunlar:** sipariş no (ortak kayıt kodu biçimi `MG-ORD-000123`), tarih, eczane, depo, temsilci, satır sayısı, toplam (para birimiyle), etkin indirim %, durum rozeti, kural uyarısı simgesi.
- **Hızlı görünüm:** sağ çekmecede sipariş özeti, satırlar ve zaman çizelgesi.
- **Dışa aktarma:** Excel.

### C — Onay ekranı (yönetici; Görev Merkezi'nden ve e-postadaki bağlantıdan açılır)
- Sipariş başlığı: eczane, depo, temsilci, tarih, toplam.
- "Neden onayımda?" kutusu (aşılan kurallar).
- Satırlar: kuralı aşan satırlar vurgulu; her satırda kural sınırı ↔ girilen değer.
- Eczanenin son 6 aylık sipariş özeti ve etkin indirim eğilimi (küçük tablo ya da çubuk).
- Düğmeler:
  - **Onayla**;
  - **Reddet** (gerekçe zorunlu);
  - (isteğe bağlı) **Değişiklik iste** (temsilciye geri gönderir, gerekçeli).
- E-postadaki tek kullanımlık bağlantıdan gelindiğinde aynı ekranın sade hâli ("Bu bağlantı bir kez kullanılabilir · süresi 48 saat").

### D — Fiyat listeleri ve MF / iskonto kuralları (ticari ekip; MOD-0156'nın ilk dilimi)
- **Fiyat listeleri sekmesi:**
  - liste adı, ülke / şirket, para birimi, tür (Resmî / Liste), geçerlilik tarihleri, durum;
  - **içe aktar** (Excel / CSV) → önizleme → yayımla;
  - eski liste geçmişte kalır.
- **Kurallar sekmesi:**
  - ürün (+ isteğe bağlı dönem ve eczane segmenti) başına: en yüksek iskonto, en yüksek MF ("10+2" yazımı ve etkin % karşılığı), birleşik tavan, adet sınırları;
  - yumuşak / sert sınır ayrımı;
  - geçerlilik tarihleri.
- **Kural deneme kutusu:** ürün + adet + MF + iskonto gir → sonuç: "sınır içi / onay gerekir / girilemez".
- Değişiklik geçmişi (kim, ne zaman, eski → yeni).

### E — Depoya giden belge (PDF) ve eczane kopyası (tek sayfa önizleme)
- **Başlık:** bizim şirketimiz (tüzel kişilik), depo adı, sipariş no, tarih, temsilci.
- **Eczane bilgileri:** ad, GLN / eczane kodu, adres, vergi no.
- **Satırlar:** ürün, barkod, adet, MF, iskonto %, birim fiyat, KDV oranı, satır toplamı.
- **Alt kısım:** genel toplam, eczacı imzası görseli, "Bu bir ön sipariştir; faturayı depo düzenler" notu.

---

## Kapsam dışı (çizme)
- Bizim satış siparişimiz, faturalama ve tahsilat (Program T).
- Depo entegrasyonu (EDI). Depo cevabı yalnız elle işaretlenir.
- Teklif (quote) ve Deal Desk istisna akışı. Kural dışı istisna şimdilik yalnız yönetici onayı.
- Mobil uygulama. Web ekranı tablette çalışır; mobil sözleşme notu ayrıca yazılır.

## Açık kararlar (mockup bunları seçenekli gösterebilir)
- Sınır içi sipariş kendiliğinden mi onaylanır, yoksa her zaman yöneticiye mi gider (kiracı ayarı; varsayılan?).
- "Değişiklik iste" düğmesi olacak mı?
- Onay bağlantısının süresi (öneri 48 saat).

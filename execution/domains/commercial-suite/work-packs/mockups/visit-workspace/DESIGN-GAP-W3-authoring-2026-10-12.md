# W3 yazım ekranları — mockup ↔ brief ↔ backend farkları (2026-10-12)

> **Mockup:** [`W3 Yazim Ekranlari (standalone).html`](W3%20Yazim%20Ekranlari%20(standalone).html), çözülmüş hali [`decoded-w3/`](decoded-w3/):
> - `Itirazlar.dc.html` (A);
> - `KPAdim.dc.html` (B);
> - `ZiyaretKanit.dc.html` (C);
> - `00-template.html` (ekran seçici);
> - `08cca5e1-….js` (demo verisi).
>
> **Brief:** [BRIEF-W3-authoring-and-evidence-settings](BRIEF-W3-authoring-and-evidence-settings.md).
>
> **Sözleşme:** [WP-VW-W3](../../WP-VW-W3-visit-execution.md) §0 (11–13), §2.2, §2.3, §37.

## 1. Kapsam
- Brief'in 11 ekranının tamamı çizilmiş; ek olarak 23 ön ayar var:
  - A3: onay bekliyor, onaylayıcı, SoD, emekli;
  - B iki yerleşim seçeneği (sekme / katlanır kart);
  - C: kaydet onayı, salt okuma, yetkisiz (UAS-001).
- **Eksikler:**
  - kenar menüsü çizilmemiş (kodda var: Content Studio › İtiraz kütüphanesi, CRM Ayarlar › Ziyaret › Ziyaret kanıtı);
  - koyu tema yok;
  - Arapça yalnız A1 ve C2'de; A2 / A3 / B / C4 ve C1 arama / C3 geçmişi metinleri Türkçe kalmış.

## 2. Backend'e eklenecekler
### A · İtiraz kütüphanesi
- "Onaya gönder" → **onay bekliyor** durumu + "Taslağa geri gönder". Backend'de yok (draft → approved doğrudan).
- Metin arama + sayfalama (liste yalnız ürün / kategori / durum süzgeci).
- **"Kullanıldığı yerler"** ve "Kullanıldığı adım" sayısı. Ters okuma yok; yol adımlarının `ObjectionResponseIds` alanı taranmalı.
- Ürün adı ve formu (DTO'da yalnız `productId`).
- İddia seçicide ürüne göre süzgeç: iddia listesinde `productId` süzgeci yok. Backend "aynı ürün" kuralını da uygulamıyor.
- Kopyadan yeni taslak: önyüzde yapılabilir; kaynak bilgisi istenirse alan gerekir.

### B · Adım sunum notları
- Adıma bağlanacak itirazlar yalnız **onaylı + aynı ürün**: backend bugün her durumu ve her ürünü kabul ediyor; yolun ürün alanı yok (türetilmesi gerekiyor).
- Tamlık sayacı ("4 / 6 adım dolu") önyüzde hesaplanabilir.

### C · Ziyaret kanıtı ayarları (neredeyse tamamı yeni)
- **Kapsam katmanları:**
  - kiracı < tüzel kişilik < iş birimi;
  - alan bazında miras (katman yalnız değiştirdiği alanı tutar, "Üst ayara dön" o alanı siler);
  - kapsam ekle / kaldır (tüzel kişilik kaldırılınca alt iş birimleri de);
  - satırda farklı alan sayısı, temsilci sayısı, son değişiklik.
- **Temsilci → tüzel kişilik / iş birimi bağı** (temsilci sayıları, etkin ayar zinciri, "N temsilciye uygulanır"). **CRM'de YOK; Auth oturumunda da yok.** HCM `EmploymentRecord` tüzel kişilik + organizasyon birimi taşıyor, ama çalışan kaydının kullanıcıya bağı yok (İK–kimlik açığı, KARAR #3).
- **Etkin ayar çözümleyicisi:** kiracı ⊕ tüzel kişilik ⊕ iş birimi ⊕ aktif onaylı istisna, alan alan kaynağıyla. Ziyaret başlatma bunu kullanmalı (bugün yalnız kiracı ayarı).
- **Değişiklik geçmişi:** eklemeli kayıt (tarih, kişi, alan, eski → yeni, "Üst ayara döndü", "Kapsam oluşturuldu").
- **Süreli istisna:** yeni toplam.
  - alanlar: temsilci, gevşetilen kurallar (3'ten seçim), başlangıç / bitiş (≤ 30 gün, bitiş zorunlu), gerekçe ≤ 500;
  - akış: taslak → onaylı → iptal; aktif / planlı / süresi doldu türetilir;
  - SoD onay; iptal / erken bitirme gerekçeli.
- Yürütmeye **`started_with_exception`** bayrağı (hangi kurallarla).
- Salt okuma izni `crm.visit-evidence-policy.read` (bugün okuma da `manage` istiyor).
- Temsilci arama (ad, tüzel kişilik, iş birimi, bölge); FU-W3-COVISITOR-LOOKUP ile ortak olabilir.

## 3. Uyumsuzluklar (karar gerektirmeyen düzeltmeler)
- **Kategori etiketleri:** mockup "Alışkanlık / rakip memnuniyeti", "Kanıt yetersiz"; BRD "Alışkanlık / rakip ürün", "Yetersiz kanıt". **Etiket BRD'den gelir** (kodda etiket yok). Arapçada da aynı fark var.
- Kategoriler kodla taşınır (`price` … `other`); renkler önyüzde koda göre.
- **Tahmini süre** mockup'ta saniye; backend'de **dakika** (`EstimatedDurationMinutes`). Hedef süre geri düşüşü dakika × 60.
- B'deki "İçerik" sekmesi yer tutucu. Gerçek KP Studio adım düzenleyicisi kalır; alt başlık / madde / referans adım alanı değil (içerik özeti ve onaylı iddialardan gelir).
- Adım kaydında `presentation` nesnesi her zaman tam gönderilir.
- "Süre tutarlılığı" kapalıyken kısa ziyaret eşiği aralık dışı gönderilmemeli (backend sınırı her zaman denetler).
- Mockup'ta çizilmeyen hata durumları eklenecek: 409 sürüm çakışması, 503 referans verisi, `objection_claim_not_approved`, en çok 10 iddia.

## 4. Kullanıcı kararları
> 2026-10-12 soruldu; yanıtlar buraya işlenecek.

| # | konu | CT önerisi |
|---|---|---|
| K-A1 | İtiraz onayı: "onay bekliyor" + geri gönder | **ekle** (gönder / geri gönder komutları, tek durum makinesi) |
| K-A2 | Düzenleyicide tek tıkla "Onayla" | **kaldır.** SoD yazan + son düzenleyen kalır; onaylayıcı düzenlerse başka biri onaylar |
| K-A3 | Boş soru / yanıtla taslak kaydı | **izin ver**; zorunluluk "Onaya gönder"de |
| K-A4 | Adıma bağlanan itiraz | **backend'de** onaylı + aynı ürün zorunlu |
| K-A5 | Emekliye ayrılan yanıt | yayınlanmış sunumlardan da **hemen** düşer (mevcut davranış); diyalog metni buna göre |
| K-B1 | B yerleşimi | **sekme** ("İçerik" / "Sunum notları") |
| K-C1 | Temsilci → tüzel kişilik / iş birimi kaynağı | **önce kiracı katmanı + istisna + geçmiş + salt okuma.** Tüzel kişilik / iş birimi katmanları İK–kullanıcı bağı (KARAR #3) netleşince; geçici CRM eşlemesi önerilmez (ikinci kayıt kaynağı) |
| K-C2 | Kapsam kaldırınca alt iş birimleri | kaldırılır; **geçmiş korunur** |
| K-C3 | İstisna ayrıntıları | aynı yönetim izni + kişi bazlı SoD; taslak reddet / geri çek eklenir; başlangıç bugün ya da sonrası; aynı kural için çakışan istisna yok; erken bitirme = bitiş bugün + "Erken bitti"; 30 gün iki uç dahil |
| K-C4 | Salt okuma izni | `crm.visit-evidence-policy.read` eklenir |

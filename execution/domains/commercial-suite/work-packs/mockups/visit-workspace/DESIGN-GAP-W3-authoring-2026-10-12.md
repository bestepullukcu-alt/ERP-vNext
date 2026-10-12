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
>
> **Kullanıcı (2026-10-12):**
> - Menü, koyu tema ve Arapça eksikleri CT / Web tarafında çözülür (mockup'tan beklenmez).
> - K-C1: temsilci tüzel kişilik / iş birimi kaynağı **WorkCenter'ın yaptığı gibi ve / veya CRM bölge (territory) ataması** üzerinden analiz edilsin.
> - K-A1: onay akışı **Workflow (MOD-0023) + WorkCenter (Görev Merkezi)** ile bağlansın.
> - İki analiz çalışıyor; sonuçla bu tablo güncellenecek.

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

## 5. Analiz sonuçları (2026-10-12, CT; iki salt okuma analizi)

### 5.1 Temsilci → tüzel kişilik / iş birimi (K-C1)

**Tüzel kişilik — WorkCenter kuralı.** Platform MOD-0288 Organizasyon:
- **Zincir:** birincil aktif koltuk (`position_assignments`) → pozisyon → org birimi → `LegalEntityId`; MDM'de doğrulanıyor, doğrulanamazsa kapalı (fail-closed).
- **Kullananlar:** Görevler, Zaman Kaydı (MOD-0280, timesheet tüzel kişiliği), çalışma saatleri. Çözüm talep anında yapılır (`TaskSeatDirectory`, `OrgDataScopeResolver`).
- **CRM erişimi yok.** "Benim koltuğum" ucu yok; liste ucu herkesi döndürür ve okuma izni ister.
- **Kurallardaki eksikler:**
  - kullanıcı başına tek birincil koltuk kuralı yok;
  - `OrgDataScopeResolver` iptal edilmiş koltukları süzmüyor.
- **97c5 verisi:** yalnız Admin (CEO) ve sema (MED-DIR) koltuklu. Hiçbir temsilcinin koltuğu yok; `medical-representative` paylaşımlı bir pozisyon.

**İş birimi — CRM bölge ataması.**
- CRM'de iş birimi bir MOD-0048 referans kodu: 97c5'te `alpha` / `beta` / `gamma`. Platform org birimi değil; Platform, HCM ve MDM'de karşılığı yok.
- Temsilci bölge atamasında `BusinessScopes` var, ama 97c5'teki 4 atamanın (hepsi Admin) tamamında **boş**.
- Bir temsilcinin birden çok iş birimi olabiliyor (yalnız uyarı veriliyor).
- **Bölgede tüzel kişilik alanı YOK**, yalnız ülke var. Ülkeden tüzel kişiliğe gitmek belirsiz: 97c5'te 5 TR tüzel kişiliği var.

**Uygun değil:**
- Auth `legal_entities` claim'i: yalnız hr-future dalında, main'de devre dışı.
- HCM `EmploymentRecord`: Employee ile User arasında bağ yok, koleksiyon da boş.

**CT önerisi — hibrit:**
- **Tüzel kişilik** = Platform birincil koltuğu (WorkCenter kuralı); yeni bir Platform "benim koltuğum" ucu veya iç uç gerekir.
- **İş birimi** = temsilcinin geçerli birincil bölge atamalarının iş birimi kodu.
- **Kapsam anahtarı** `(LegalEntityId, iş birimi kodu)`. Mockup'taki ağaç (tüzel kişilik > iş birimi) buna uyar.
- **Çözüm kuralı:**
  - tek iş birimi → iş birimi katmanı;
  - sıfır ya da birden çok → iş birimi katmanı yok;
  - koltuk yoksa veya MDM'ye ulaşılamazsa → tüzel kişilik katmanı yok;
  - her durumda kiracı katmanına düşülür.
- Çözülen kapsam yürütme kaydına damgalanır.
- **Veri ön koşulları:**
  - her temsilcinin Platform pozisyon ataması olmalı (İK);
  - birincil bölge atamasında tam bir iş birimi kodu olmalı (CRM yöneticisi).
- **CRM'de temsilci → tüzel kişilik tablosu YOK;** bölgeye tüzel kişilik alanı da EKLENMEZ.

### 5.2 Onay akışı (K-A1) — Workflow + WorkCenter

**Altyapı hazır ve CRM'de canlı.**
- Workflow (MOD-0023, Platform) + WorkCenterNext gelen kutusu.
- Kullananlar: iddialar (CLAIM-CORE-MLR, canlı E2E 2026-09-29), bilgi yolu revizyonu (KP-MLR), güvenlik metni ve ülke hukuki profili (KP-REG, canlı E4 2026-10-02).
- **Kopyalanacak desen:** `RegulatoryTextLifecycle.cs`:
  - gönder (180–247), geri çek (250–303), karar (310–430);
  - sonuç uygulayıcı + okumada uzlaştırma (`RegulatoryTextCore.cs` 136–331);
  - Platform tamamlanma olayı RabbitMQ ile `ClaimWorkflowOutcomeConsumer` üzerinden.

**İtiraz yanıtı akışı:**
1. "Onaya gönder" → Workflow örneği (`crm.objection-response`, şablon örn. `CRM-OBJ-RESP`, Görev Merkezi derin bağlantısı) → durum `in-review`.
2. Karar Görev Merkezi'nde ya da CRM sayfasında: onayla / taslağa geri gönder (not zorunlu).
3. CRM durumu günceller.
4. Geri çekme yalnız gönderende. Eski `/approve` ucu → 409 `approval_via_workflow_only`.
- **Platform kodu YOK:** CRM kodu + 1 şablon + pozisyon atamaları + yetki verme.

**Süreli istisna:** aynı desen (`crm.visit-evidence-exception`, şablon örn. `CRM-VISIT-EVIDENCE-EXC`, sabit onaylayıcı pozisyonu). "Temsilcinin yöneticisi onaylasın" seçeneği Platform değişikliği ister.

**Riskler ve önlemler:**
- Platform yalnız **gönderen kişiyi** onaydan engelliyor → CRM sonuç uygulayıcısı yazar / son düzenleyen / gönderen onayını reddeder (taslağa döner). Aynı açık KP-5a'da da var.
- Workflow görevi **yalnız ilk adaya** veriyor (kimliğe göre sıralı; diğerleri 403) → pozisyonlar, yazarın onaylayıcı pozisyonunda olmayacağı şekilde kurulmalı.
- İstisnanın etkinleşmesi olaya bağlı: temsilcinin uzlaştırma için izni yok → olay veya arka plan uzlaştırması gerekir.
- `platform.workflow.*` yetkileri betikle verilir; yeniden giriş gerekir.

**Sonraki Platform paketi (ayrı):**
- dışlanan kişiler listesi;
- herhangi bir adayın karar verebilmesi;
- olay imzalama;
- onaylayıcı bildirimi;
- yönetici tipinde aday.

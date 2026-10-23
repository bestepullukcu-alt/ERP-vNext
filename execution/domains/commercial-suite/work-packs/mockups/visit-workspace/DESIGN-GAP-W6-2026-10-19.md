# DESIGN-GAP — W6 eczane ön siparişi (mockup 2026-10-19)

> **Kaynak:** [W6 Eczane On Siparis (standalone).html](W6%20Eczane%20On%20Siparis%20(standalone).html). Açılmış biçimi: [decoded-w6/W6-on-siparis.dc.html](decoded-w6/W6-on-siparis.dc.html). İşaretleme satır 156–715, mantık satır 716–1006.
> **Karşılaştırılanlar:** [BRIEF-W6](BRIEF-W6-pharmacy-pre-order.md), yol haritasındaki K-W9 ve K-W10 (a)–(h) kararları, 05 / 06 mockup'ları ve platform UI standartları.
> **Hazırlayan:** CT (alt ajan çıkarımı + CT değerlendirmesi).

## 1. Ekranlar

| Ekran | Rol | İçerik (özet) |
|---|---|---|
| **A** ön sipariş (temsilci) | Temsilci | "Sistemden 🔒" şeridi: şirket, ülke, para birimi, fiyat modu. Eczane seçimi. Satırlarda SKU, Adet, MF, İsk. % (yalnız fiyatlı modda), kilitli birim fiyat, satır toplamı. SKU bilgisi: form, kutu adedi, GTIN, `LSKU-TR-…`. MF satırı ("+6 kutu MF · birleşik %20,0"), fiyat kaynağı satırı, kural rozeti ve iletisi. Eczanenin son 3 siparişi. Depo, teslim tarihi. Sipariş özeti, "Onay gerekir mi?" kutusu (onaycı ve boş koltuk yedeği), eczacı imzası. Gönder düğmesi şu durumlarda kapalı: sert sınır aşımı, imza yok, satır yok. Gönderimden sonra durum zaman çizelgesi; onaylanmış ya da iletilmiş siparişte "İptal et ve yeni sipariş aç". |
| **B** liste | Hepsi | Süzgeçler: tarih, durum, temsilci (temsilcide yok), eczane, depo, ürün, "Onayımı bekleyenler". Sütunlar: no, tarih, eczane + şehir, depo, temsilci, satır sayısı, toplam, isteğe bağlı yerel karşılık, etkin indirim, durum (+ depo cevabı), kural uyarısı. Satıra tıklayınca hızlı görünüm çekmecesi; çekmecede elle depo cevabı. |
| **C** onay | Yönetici | Sipariş bilgileri. "Neden onayımda?" (aşılan kurallar ya da kiracı ayarı) ve "Siz … Reports to pozisyonundasınız". Satırlarda girilen değer ↔ sınır. 6 aylık sipariş grafiği ve tablosu. Onayla / Reddet / Değişiklik iste; Reddet ve Değişiklik iste için gerekçe zorunlu. |
| **C-bağlantı** | Onaycı | Tek kullanımlık e-posta bağlantısı (süre {48} saat), **"Oturum açmadan onaylayabilirsiniz"**. Kullanılmış hâli var; süresi dolmuş hâli çizilmemiş. |
| **D** fiyat ve kurallar | Satış operasyonu | Dört sekme: Fiyat listeleri / MF-iskonto kuralları / Kural deneme / Değişiklik geçmişi. Liste durumları: Yürürlükte / Taslak / **Planlandı** / Geçmiş. Kademeler: depo fiyatı, eczacı alış, halka satış, KDV. İçe aktarma 3 adım: dosya → önizleme (fark, eşleşmeyen, retired) → yayımla. Kurallarda yumuşak / sert sınır çifti; segment ve tüzel kişilik süzgeci. Kural deneme kutusu. |
| **E** depo belgesi | Hepsi | A4 belge; depo nüshası ↔ eczane kopyası seçimi. İçerik: şirket VKN, depo, sipariş no, tarih, temsilci, eczane (GLN / eczane kodu / vergi no), satırlar, toplamlar, yerel karşılık, imza, onaylayan, "faturayı depo düzenler" notu. İletim izi gösterilir. |

## 2. Mockup'ın içerdiği kurallar (W6-BE'nin uyacağı)

- **Durumlar:** taslak → onay bekliyor → (onaylandı) → depoya iletildi. Ayrıca reddedildi (son durum) ve iptal edildi (onaylanmış ya da iletilmiş siparişten).
  - Kiracı ayarı "kendiliğinden" ise ve kural aşımı yoksa sipariş doğrudan iletilir; çizelgede "Kendiliğinden onaylandı" yazar.
  - Onay bekleyen sipariş düzenlenemez.
- **MF etkin oranı:** a+b → b / (a+b). Örnek: 10+2 → %16,7. Kural MF oranıyla karşılaştırılır. Bedava kutu = floor(adet / a) × b.
- **Birleşik indirim:** 1 − (1 − iskonto) × (1 − MF oranı), 0,1'e yuvarlanır. Yalnız fiyatlı modda denetlenir.
- **Adet kuralları:** en az adetin altı → sert. Sert üst sınırın üstü → sert. Yumuşak üst sınırın üstü → yumuşak. Satırın sonucu, satırdaki en kötü sonuçtur.
- **Tutar:** birim fiyat = **eczacı alış** kademesi. Net = adet × fiyat × (1 − iskonto). KDV net tutar üzerinden, ülke / ürün oranıyla (TR %10, BAE %0).
- **Para birimi:** TR fiyat listesi TRY'dir, yerel karşılık satırı çıkmaz. BAE fiyat listesi USD'dir; yanında "≈ … AED · kur 3,6725 · 19.10.2026" satırı çıkar. KG adetli modda tutar ve kur gösterilmez.
- **Fiyat:** gönderim anında çözülür ("taslak 01.11'den sonra gönderilirse yeni fiyatı alır"). Planlanan fiyat listesi tarihinde kendiliğinden yürürlüğe girer.
- **Kimlik:** ürün GTIN + LSKU ile tanınır; Retired ürün seçilemez. Sipariş numarası `MG-ORD-000123` biçiminde, tek sıra; numara taslakta verilir.
- **Görünürlük:** temsilci kendi siparişlerini, yönetici ekibininkileri, satış operasyonu hepsini görür.
- **Demo kural değerleri** (TUT, ALM, GLU, URO ve "Zincir eczane" segmenti) mockup'ın 730–732. satırlarında; testlerde örnek veri olarak kullanılabilir.

## 3. Brief / kararlarla farklar ve CT önerisi

| Konu | Mockup | Brief / karar | CT önerisi |
|---|---|---|---|
| Onay ve iletim | "Onayla" siparişi hemen iletir | Brief'te iki ayrı durum | **Onaylandı** durumu kalır; iletim sistem adımıdır. "İletim başarısız" durumu ve yeniden deneme eklenir (W5 deseni). |
| Değişiklik iste | Sipariş taslağa döner, zaman çizelgesi silinir, imza kalır | İsteğe bağlı düğme | Olay kaydı silinmez (MOD-0168 kayıt sahibi). Geri dönen siparişte imza temizlenir. |
| İletimden sonra iptal | Yalnız temsilci, gerekçe yok, depoya haber gitmez | "İptal edip yeni sipariş" | Gerekçe zorunlu. Depoya ve eczaneye iptal e-postası gider. Kimin iptal edebileceği → **karar**. |
| Reddedilen sipariş | Son durum, kopyalanamaz | — | "Kopyala ve yeni sipariş aç" eklensin. |
| Depo cevabı | Yönetici ve operasyon işaretler | Elle | Yalnız satış operasyonu işaretler. Eksik teslimde kısmi adet ve not girilebilir. |
| E-posta bağlantısıyla onay | **Oturum açmadan** | Platform: JWT + yetki; yol haritası "güvenli tek kullanımlık" diyor | **Güvenlik kararı.** CT önerisi: bağlantı oturum açtırır (SSO), ardından onay ekranı açılır; tek kullanım ve süre kuralı yine uygulanır. Süresi dolmuş hâli eklenir. |
| Kur | Yalnız kur ve tarih; kur tablosu ekranı yok | K-W10 (g): kaynak + tüzel kişilik başına günlük kur tablosu | D'ye "Kurlar" sekmesi. Belgede kaynak da gösterilir. |
| KG fiyatlı örneği | Yalnız adetli | Brief #10 (KGS örneği) | KG'de USD liste varsa fiyatlı mod da aynı şekilde çalışır; ayrı çizim gerekmiyor. |
| Kural anahtarı | Tüzel kişilik süzgeci var, "Yeni kural" formunda tüzel kişilik alanı yok | Ürün × (dönem / segment) | Kural = **tüzel kişilik × SKU × isteğe bağlı segment / dönem**. En özel kural kazanır. |
| "Dönem" | "Q4 2026 kampanya" + geçerlilik tarihleri | İsteğe bağlı | İlk sürümde yalnız geçerlilik tarihleri; kampanya bağı (MOD-0165) sonra. |
| Liste türü adı | "Resmî / Liste" | "Resmî / Şirket listesi" | K-W10 adı kullanılır. |
| İçe aktarma | Para birimi seçimi yok | Şirket × ülke × para birimi | Para birimi alanı eklenir. |
| Dört göz | Yayımla ve kural kaydet tek kişi | MOD-0156 "pricing approvals" | **Karar.** CT önerisi: liste yayımlama ve kural değişikliği ikinci kişi onayıyla (SoD). İlk sürümde Workflow onayı. |
| Onayın sahibi | Siparişin üstünde | MOD-0156 "pricing approvals" | Sipariş onay akışı MOD-0168'in (siparişte). MOD-0156 değerlendirme API'si verir; sonuç siparişe yazılır. |
| Kendiliğinden onay ve E belgesi | "Onaylayan: Mehmet Kaya" | — | "Kendiliğinden onaylandı (kiracı ayarı)" yazılır. |
| E belgesi içeriği | Etkin indirim gösteriliyor; not yok | Brief #8 | Depo nüshasında iç bilgi olan etkin % gizlenir; sipariş notu eklenir. |
| Operasyon listesi | TRY, USD ve adetli tek sütunda | — | Ülke / şirket süzgeci olur; para birimleri arası toplam ve sıralama yapılmaz. |
| Ziyaret bağı | Yok | Yol haritası: takvimden / E4c'den açılır | İsteğe bağlı `visitId`. 05'teki "ziyaretten bağımsız sipariş" kalır. |
| 05 / 06 ile fark | 05'te temsilci para birimi seçiyordu; 06'da eski numara biçimi | K-W10 (g), RCS-001 | **W6 mockup'ı geçerlidir.** Ek e-posta şablonları: değişiklik iste, iptal, kendiliğinden onay bilgisi (W5 altyapısıyla). |
| Brief'te olmayan ekler | Planlandı durumu, içe aktarma farkı, yerel karşılık sütunu, LSKU kodu, boş koltuk adı, menü sayacı | — | Kabul; kararlarla uyumlu. |

## 4. Platform standartları (standart kazanır)

- **Başlık:** her ekranda `h4` + div var. Yerine Golden başlık gelir: `div.mb-3 > h5.mb-0 + p.mb-0.text-muted`. C'deki kendi breadcrumb'ı kalkar.
- **Sekmeler:** D'deki alt çizgili sekmeler ortak `_TabCard`'a (wcn-tabcard) döner. E'deki nüsha seçimi düğme grubu olarak kalabilir.
- **Listeler:** B ve D'nin tabloları **Golden Compact DataTable** olur: `_Filter`, `DitenDataTable.createList`, Görünümü Kaydet, iskelet, standart birincil düğme, hızlı görünüm offcanvas.
- **Form alanları:** hepsi `.diten-field` simgeli olur.
- **Renk:** mockup'taki sabit lacivert `#0b1a8c` yerine `--bs-primary` kullanılır. Uyarı ve hata renkleri anlamsal olduğu için kalabilir.
- **Ekran A:** cihaz çerçevesi ve A'nın kendi üst çubuğu mockup'a ait; A, `_Layout` içinde duyarlı bir sayfa olur. E bir menü öğesi değil, siparişe ait belgedir. Menü manifestten gelir.
- **Dil ve RTL:**
  - Sayılar ve tarihler `tr-TR`'ye sabit yazılmamalı. Kural iletileri birleştirilmiş metin olmamalı: backend gerekçe kodu + parametre döner, metin resx'te 7 dilde.
  - RTL için mantıksal özellikler kullanılır (çekmece yönü, satır kenarı, oklar).

## 5. Ürün sahibi kararları — CT önerileriyle (W6 paketinden önce)

| # | Soru | CT önerisi |
|---|---|---|
| D1 | Sınır içi sipariş kendiliğinden mi onaylansın? | Kiracı ayarı; **varsayılan "her zaman yönetici onayı"** (mockup varsayılanı). |
| D2 | "Değişiklik iste" düğmesi olsun mu? | **Evet.** İmza temizlenir; temsilci yeniden imza alır. |
| D3 | Onay bağlantısının süresi | **48 saat** (kiracı ayarı, 12–168 saat). |
| D4 | E-postadan oturum açmadan onay | **Hayır.** Bağlantı oturum açtırır, ardından onay ekranı açılır; tek kullanım ve süre kuralı yine geçerli. |
| D5 | İletimden sonra iptal | Temsilci ve yöneticisi iptal edebilir; **gerekçe zorunlu**. Depoya ve eczaneye iptal e-postası gider. |
| D6 | Reddedilen sipariş kopyalansın mı? | **Evet**, "Kopyala ve yeni sipariş aç". |
| D7 | MF kuralı neyle denetlenir? | **Oranla** (mockup gibi). Bedava kutu = floor(adet / a) × b. Yüzdeler 0,1'e yuvarlanır. |
| D8 | Kural önceliği ve "dönem" | En özel kural kazanır: segment + dönem > segment > genel. Dönem şimdilik geçerlilik tarihleri. Eczane segmenti, kurum kaydındaki bir referans veri setinden gelir (BRD `pharmacy-segment`). |
| D9 | Depo cevabını kim işaretler? | **Satış operasyonu.** Değerler: Teslim edildi / Eksik teslim (kısmi adet + not). |
| D10 | Yöneticinin "ekibi" ve birden çok onaycı | Ekip = Reports-to altındaki herkes. Pozisyonda birden çok kişi varsa ilk karar geçerli. Vekâlet sonra (Workflow). |
| D11 | Fiyat kademesi | Ülke ayarı; **varsayılan eczacı alış**. |
| D12 | Fiyat ne zaman sabitlenir? | **Gönderimde** (mockup gibi). |
| D13 | Liste yayımlama ve kural değişikliğinde dört göz | **Evet**, ikinci kişi onayı. Pazarlama düzenler, satış operasyonu onaylar. |
| D14 | Ara kur tablosunu kim girer? | **Satış operasyonu**, D'deki "Kurlar" sekmesinde. |
| D15 | Sipariş numarası ne zaman verilir? | **Taslak kaydedilince** (mockup gibi; temsilci numarayla atıf yapabilir). İptal edilen taslağın numarası boşa düşer. |
| D16 | Onay nerede yapılır? | Görev Merkezi (Workflow onay görevi, W3-CFG deseni) + e-posta bağlantısı. Onaycıyı Platform'un "yöneticisi" çözümleyicisi bulur (PLT-NOTIFY). |

## 6. W6 paketi için iş bölümü taslağı (kararlardan sonra kesinleşir)

- **W6-BE-a — sipariş (MOD-0168, CRM):** sipariş, satırlar, olay kaydı, durum makinesi, imza (MOD-0262), onay (Workflow + bağlantı belirteci), numara (RCS-001), kiracı ayarları, liste / önizleme API'si, görünürlük.
- **W6-BE-b — fiyat ve kurallar (MOD-0156 ilk dilim):**
  - nerede yaşar: **karar** (CRM içinde ayrı modül klasörü mü, yeni servis mi; CT önerisi: şimdilik CRM içinde ayrı sınırla, Program T2'de taşınabilir);
  - fiyat listesi + içe aktarma (hazırlık alanı) + yayımlama (dört göz);
  - kurallar + değerlendirme API'si;
  - ara kur tablosu.
- **W6-BE-c — belge ve e-postalar:** PDF (depo / eczane nüshası), W5 altyapısıyla e-postalar (onay talebi, depoya iletim, eczane kopyası, bilgi, iptal, değişiklik iste).
- **W6-WEB-a:** A + B + C (+ bağlantı ekranı).
- **W6-WEB-b:** D (fiyat listeleri, kurallar, deneme, geçmiş, kurlar) + E önizlemesi.
- Gateway rotaları ve rota kapsama testleri her BE isteminin içinde (W3L / W5 dersi).

## 7. KARAR (kullanıcı, 2026-10-19)
- **D1–D16:** CT önerileri olduğu gibi kabul edildi.
- **D17 (yer): "Blueprint'te nerede olması gerekiyorsa orada."** CT ölçümü:
  - 8.7'de MOD-0156 Price Lists & Discount Guardrails = Commercial Suite (CRM + O2C), CPQ & Pricing; MOD-0168 Order Capture = Commercial Suite, Order-to-Cash (Bridge).
  - Kayıt tablosunda ikisinin sahip domain'i commercial-suite; o domain'in servisi `Diten.CrmService` (domain-config).
  - **Sonuç:** ikisi de CrmService içinde, CRM modüllerinden ayrı sınırlarla (kendi klasör / koleksiyon / izin ad alanı) yaşar. MOD-0168 kaydındaki "SoR boundary with Finance / Order-Mgmt = EA-TBD" notu Program T3'e kalır.
- **Ön şart (AGENTS çekirdek kural 7):** kod yalnız `approved` / `ready-for-dev` module pack ile yazılır. MOD-0156 ve MOD-0168 için paket yok → **W6-PACK** (iki paket: MOD-0168-FU01 eczane ön siparişi, MOD-0156-FU01 fiyat listesi + iskonto / MF kuralları ilk dilimi) önce yazılır ve onaylanır.
- **İzin ad alanı:** crm-rbac-integration-plan §2 `commercial.pricing.*` / `commercial.order.*` (PKS-001'de yok; sahip onayı gerekli) — kullanıcı kararı bekleniyor.


## 8. E4 görsel karşılaştırma — yerel, 2026-10-21 (CT)

**Ne kullanıldı.**
- Mockup standalone dosyası `mockups-workspace` sunucusundan açıldı.
- Canlı ekranlar yerel fleet üzerinde, kiracı 97c5, sipariş MG-ORD-000001 ile açıldı (KONSET, TR fiyat listesi, TEST ECZA DEPOSU).
- Her iki taraf 1280 px'te ölçüldü: bölüm konumu, sütunlar, alan ve etiket metinleri. Ayrıntılar panel genişliğinde (≈660 px) ekran görüntüsüyle bakıldı.
- §4 standartları (Golden başlık, `_TabCard`, Golden liste, `.diten-field`) bilerek farklı olan yerlerdir; aşağıda fark sayılmadı.

**Genel sonuç.**
- Yerleşim mockup'a uyuyor: A'da iki sütun (sol 631 px / sağ 310 px), B ve D'de Golden liste, C'de karar kartı ve 6 ay bölümü, E'de nüsha seçimi ve iletim izi.
- Farkların çoğu satır ve toplam ayrıntısında, bir kısmı da PDF içeriğinde.

| # | Ekran | Mockup | Canlı | Öneri |
|---|---|---|---|---|
| V-1 | A satırlar | Tek satırlık tablo: ÜRÜN · ADET · MF · İSK. % · BİRİM · TOPLAM; başlık satırı bir kez | Her satır ayrı kart; "Ürün" kendi satırında, Adet / MF / İsk. alt satırda; etiketler her satırda tekrar | Masaüstünde tablo satırı, dar ekranda kart. Çok satırlı siparişte sayfa uzuyor. |
| V-2 | A satır bilgisi | "Form · kutu · GTIN · LSKU" satırı; fiyat kaynağı "liste · tarih · kademe · KDV %10" | Yalnız "Fiyat kaynağı: KONSET TR test listesi" | Kademe, tarih ve KDV % satırda gösterilsin. GTIN / form MDM'den gelince (veri) gösterilsin. |
| V-3 | A sipariş özeti | Ara toplam (KDV hariç, liste) · İskonto · Net (KDV hariç) · KDV %10 · Etkin indirim (MF + iskonto) · Genel toplam | Net · KDV · Etkin indirim · Genel toplam | Ara toplam ve İskonto satırları eklensin; etiketler mockup'taki gibi açıklayıcı olsun. |
| V-4 | A önceki siparişler | "ALMIBA × 50, ÜROFLEKS × 20" | "1 satır" | Ürün × adet özeti gösterilsin. |
| V-5 | A başlık / düğme | Başlıkta "MG-ORD-000123 · Şifa Eczanesi" + durum rozeti; düğme "Yönetici onayına gönder" | "Yeni ön sipariş" (kayıtlı taslakta da); düğme "Gönder" | Kayıtlı taslakta başlık numara + eczane + durum göstersin (Golden başlık içinde); düğme metni bağlama göre değişsin. |
| V-6 | A eczane | Son / atanmış eczaneler kart olarak, tek dokunuşla seçiliyor | Yalnız arama kutusu | Temsilcinin bölgesindeki son eczaneler hızlı seçim olarak eklensin (isteğe bağlı). |
| V-7 | B liste | 1280 px'te tüm sütunlar görünür | 1280 px'te Temsilci, Satır, Etkin indirim ve Kural sütunları duyarlı tablo tarafından gizleniyor (`dtr-hidden`) | Sütun önceliği ayarlansın. |
| V-8 | B liste | "Yerel karşılık sütunu" anahtarı | Yok (sütun görünürlüğünde de bulunamadı) | Doğrulanmalı. |
| V-9 | C 6 ay | "Etkin indirim eğilimi (MF + iskonto)" grafiği; tablo Ay · Sipariş · Tutar · Etkin | Grafik sipariş sayısını gösteriyor; tablo Ay · Sipariş · Kutu · Toplam | Etkin indirim sütunu ve eğilimi eklensin (yöneticinin karar verisi). |
| V-10 | C bilgi | Tarih saatle; eczane şehirle; "Reports to (Mehmet Kaya · Bölge Müdürü)" | Saat yok; şehir yok; "(CEO)" pozisyon kodu | Pozisyon adı ve saat gösterilsin. |
| V-11 | C satırlar | Her hücrenin altında "sınır …" | Kuralsız üründe sınır yok (beklenen) | Kurallı bir satırla yeniden bakılacak (kural dalı testi). |
| V-12 | D sekmeler | "MF / iskonto kuralları" | "MF-Iskonto Kuralları" (noktasız büyük I — Türkçe büyük harf hatası) | Sekme etiketi düzeltilsin. |
| V-13 | E PDF antet | Şirket adı + adres + VKN | Yalnız "Satıcı: …" | Şirket adresi ve VKN MDM tüzel kişilikten gelsin. |
| V-14 | E PDF satırlar | Ürün · Barkod · Adet · MF · İsk. % · Birim fiyat · KDV % · Satır toplamı | Ürün · Adet · Birim fiyat · KDV % · Satır toplamı | Barkod, MF ve İsk. % sütunları eklensin (depo MF'yi görmeli). Önce MF'li bir satırla doğrulanmalı (koşullu olabilir). |
| V-15 | E PDF toplamlar | Ara toplam · İskonto · Net · KDV · Genel toplam | Net · KDV · Toplam | V-3 ile aynı. |
| V-16 | E PDF başlık bilgisi | Teslim tarihi, para birimi, depo e-postası | Yok | Eklensin (teslim tarihi boşsa satır yazılmaz). |
| V-17 | E PDF saatler | Yerel saat | İmza ve onay saati UTC basılıyor (20:08 / 20:10; ekranda iletim 23:10) | Satan şirketin ülke saat dilimiyle basılsın. |
| V-18 | E PDF eczane | Adres (mahalle, ilçe / il) + GLN + eczane kodu + vergi no | Yalnız ilçe ("ETİMESGUT"); kimlikler boş | Kimlikler veri işi (FU-ACCOUNT-PHARMACY-IDS); il bilgisi eklensin. |
| V-19 | E PDF | — | "İletim: deneme 1" depo nüshasına basılıyor | İç bilgi; depo nüshasından çıkarılsın. |

**Görsel olmayan bulgular.** Aynı E4'te çıkan G-4…G-11 W6-FIX takibindedir (WP-VW-W6 §37 E4 notu). Bunlar:
- form kaydı (G-4);
- `{action}` rotası (G-5);
- yerel onay kutusu (G-6);
- onay sorusu (G-7);
- gönderim sonrası sayfa (G-8);
- satan şirket kodu (G-9);
- karar kanalı (G-10);
- e-posta teslim geri bildirimi (G-11).

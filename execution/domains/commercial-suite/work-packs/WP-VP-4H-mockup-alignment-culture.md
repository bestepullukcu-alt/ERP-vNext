# WORK PACKAGE — WP-VP-4H · Mockup v2 uyumu + uygulama dili + küçük düzeltmeler (Web)

> **CT (SoR), 2026-10-08.** Faz 4 E4 bulgularının Web kısmı ([yol haritası](ROADMAP-visit-planning.md) Faz 4 E4, F4-2 / F4-3 / F4-6 / F4-7) + kullanıcının ekran görüntüleriyle bildirdiği mockup uyumsuzlukları.
> - **Kullanıcı (2026-10-08):**
>   - "Tasarımda da mockup ile uyumsuzluk var."
>   - "Ürünün kodu yerine adı gözükmesi gerek."
>   - "Boş hafta kısmında tasarım bozuk."
>   - "TR seçili olmasına rağmen Oct diye yazıyor."
> - **Mockup:** `mockups/visit-planning/Ziyaret Planlama v2 (standalone).html`. Okunabilir kaynak: `visit-planning-v2.decoded.html` (satır ~386 Hedefler, ~556 Haftalar, ~726 doktor çekmecesi). Bölüm adları aşağıda mockup'takiyle aynı.
> - **Rota sekmesi tasarımı değişmez** (mockup: "Rota sekmesi değişmiyor"). Yalnız dil ve hafta listesi düzeltmeleri.
> - **Backend:** WP-VP-4G (paralel). `productName` gelmezse kod gösterilir. `reportStatus` / `countryCode` yoksa o öğe gizlenir; hata verilmez.
>
> **Çalışma yeri:** worktree `C:\tmp\vp-4h`, dal `wp/vp-4h` (test dalı başından). Commit bu dala, push YOK.

## Kök neden — İngilizce tarih ve sayı
- Visit Planning JS'i tarihleri tarayıcının diliyle (`toLocaleDateString(undefined, …)`) ya da sabit `'en-US'` ile biçimliyor.
  - `details.js` satır 115, 782–799, 1010; `index.js` 38; `weeks.js` 54–59, 380; `header.js` 48–55; `doctor-panel.js` 37; `new-plan.js` 39; `form.js` 67; `targets.js` 162.
- Kullanıcının tarayıcısı İngilizce olduğu için TR seçiliyken "Monday Oct 05", "6.5 sa", "Pzt 19 Oct, 26" çıkıyor.
- Uygulama dili `document.documentElement.lang` (TenantShell yerleşimi `CurrentUICulture`). Mevcut örnek: `CycleCapacities/index.js:56`.

## NE

### 0. Ortak biçimleyici (dil)
- Visit Planning için tek yardımcı (ör. `VisitPlanningPage.fmt`):
  - `culture = document.documentElement.lang || 'tr'`;
  - `dayShort` ("Pzt"), `dayMonth` ("5 Eki");
  - çalışma günü aralığı ("5–9 Eki"; ay değişirse "28 Eyl–2 Eki");
  - `hours` ("6,5 sa" — ondalık ayırıcı dile göre), `dateTime`.
- Tüm Visit Planning JS'i bu yardımcıyı kullanır. `undefined` ya da `'en-US'` kalmaz (koruma testi taranır).
- Arapça: rakam ve ay adı `ar` kültürüyle; sayfa `dir="rtl"` (yerleşim ayna).

### 1. Haftalar — dönem şeridi (mockup "Dönem haftaları")
- Kart dikey, sabit genişlik (mockup `flex:0 0 128px`). İçerik sırası:
  1. "41. Hafta" + sağda "BUGÜN";
  2. aralık (çalışma günleri, "5–9 Eki");
  3. durum rozeti;
  4. "176 ziyaret";
  5. ilerleme çubuğu;
  6. alt satır: tatil ("29 Ekim") ve uyarı sayısı (⚠ 3).
- Boş hafta kartı: kesik çizgili kenar, "Boş", ziyaret yerine "—".
- Başlık altı: "Türkiye 2026 Q4 Döngüsü · 13 hafta · 1 onaylı · 3 taslak · 8 boş" (dönem adı dahil).

### 2. Haftalar — hafta ayrıntısı + doktorlar yan yana
- Geniş ekranda iki kart yan yana: solda hafta ayrıntısı (mockup `flex:1 1 520px`), sağda "Bu haftadaki doktorlar" (`flex:1 1 420px`). Dar ekranda alt alta.
- **Başlık:** "41. Hafta · 5–9 Eki" + durum rozeti. Alt satır duruma göre:
  - onaylı: "Onaylı · Planlanan Ziyaretler'e yazıldı · salt okunur";
  - taslak: mevcut yardım metni;
  - boş: "Henüz üretilmedi".
- **Düğmeler:**
  - taslak: "Bu haftayı yeniden üret" + "Haftayı onayla";
  - onaylı: "Haftayı yeniden aç";
  - ziyareti olan haftada "Rotayı aç".
- **Gün satırı ızgarası** (mockup `18px 96px 1fr 150px`):
  - açılır ok;
  - "**Pzt** 5 Eki";
  - çubuk: tam dolu turuncu, değilse lacivert;
  - sağda "36 / 36" + 4E boş süre rozeti (korunur, "boş 6,5 sa").
- **Gün içi ziyaret satırı:**
  - ad + altında kurum;
  - sağda ürün çipleri (ad, rol rengi, kaynak simgesi, onaylı içerik yoksa uyarı simgesi);
  - "≈ 22 dk"; ürünsüzse "ürün yok" + "süre yok".
  - 4D / 4E kurum grubu başlığı ve taşıma / sabit simgeleri **korunur** (kullanıcının onayladığı özellik). Mockup stiliyle: grup başlığı ince, ziyaret satırı mockup satırı.
  - Gün boşsa "Bu güne ziyaret düşmüyor."
- **Ürün başına haftalık ziyaret:**
  - çip = ürün **adı** + kalın sayı ("TUTUKON **14**");
  - hatırlatma ürünü çerçeveli beyaz (mockup GLUKOFIT);
  - "Karışık sıra" notu gri kutuda.
- **Kaydırılan / sığmayan:**
  - satır = ad + kurum, neden (4G sözlüğü: `capacity_full` "hafta dolu", `no_near_day` "yakın gün yok", `pin_overflow`, `consent_blocked`), sonuç ("→ 42. Hafta");
  - ürün satırında paket simgesi.
- Taslakta alt düğme "Bu haftanın hedeflerini düzenle" (Hedefler sekmesine geçer).

### 3. Boş hafta — boş durum (kullanıcı: "tasarım bozuk")
- Boş haftada gün satırları, ürün ve kaydırma bölümleri **gösterilmez.** Yerine kesik çizgili kutu:
  - takvim simgesi;
  - "Bu hafta için plan yok";
  - "Önceki hafta onaylandığında otomatik oluşur. Şimdi sıklık kurallarıyla üretebilirsiniz.";
  - birincil düğme **"Bu haftayı üret"** (mevcut yeniden üret yolu).
- Başlıkta düğme yok. Doktorlar kartı: "Bu haftada planlı doktor yok."

### 4. Bu haftadaki doktorlar (mockup listesi)
- Başlık + sayı rozeti. Satır:
  - ad;
  - altında "Uzmanlık · Kurum";
  - sağda "dönemde 3 · 1 / 2" (sıklık · yapılan / kalan). Sıklık bilinmiyorsa "dönemde 1 (varsayılan)" (**F4-2**, kullanıcı onaylı).
  - Adın altında küçük dönem şeridi: mockup'taki ince dikdörtgenler, hafta başına bir; şimdiki büyük daireler kalkar.
- Lejant: **yapıldı** · onaylı · taslak · öngörülen. "Sunuldu" → "yapıldı"; `reportStatus` ile, alan yoksa onaylı sayılır.
- 8'den fazla doktorda "Tümünü göster (21)".

### 5. Doktor paneli (mockup çekmecesi)
- **Başlık:** ad, "Uzmanlık · Kurum", segment rozeti; sıklık yoksa "sıklık yok" rozeti.
- **Dönem görünümü:**
  - Üç kutu: Sıklık hedefi ("dönemde 3" / "dönemde 1 (varsayılan)"), Yapılan, Kalan.
  - **Sıradaki ziyaretin ürünleri** kartı:
    - hafta + aralık;
    - çipler (ad);
    - "Kaynak: son ziyaret";
    - "1 tanıtım + 1 hatırlatma + rapor ≈ 22 dk";
    - sağ üstte "Ürünleri değiştir".
    - Ürün yoksa "ürün yok · Ziyaret süresi hesaplanamıyor. Ürün seç".
  - "Sıradaki içerik: yakında" satırı.
  - **Dönem boyunca · ürün geçmişi:** dönemin **bütün haftaları** listelenir, şimdi yalnız bir hafta görünüyor.
    - Satırda "40. Hafta · 28 Eyl–2 Eki" ve sağda durum ("—" / "Planlı · onaylı" / "Taslak" / "Öngörülen" / "Yapıldı").
    - Ürünlü haftada önek + çipler ("Planlandı: PULMOSET TUTUKON", "Öngörülen: …", "Sunuldu: …").
    - Geçmiş haftalar soluk.

### 6. Hedefler (mockup "04 Hedefler")
- **Sol — "Hesaplarım (bölgem)" + sayı + "Hesap ara..":**
  - temsilcinin bölge hesapları listesi (`my-accounts`, sunucu araması, kaydırınca 50'şer yükle). Bugünkü DataTable ve sayfalama kalkar.
  - Satır: ad + tür rozeti (yerel etiket); altında "İstanbul · 13 / 16 seçili · 8 bu hafta".
  - Seçili hesap sol kenar çizgili; seçili hesaplar listenin başında.
  - Bölge dışı eklenenler aynı listede "bölge dışı" rozetiyle.
  - Bölge yoksa K-5 bandı (mevcut).
  - Ayrı "Klinik / hastane ekle" açılır listesi **kalkar**: liste zaten seçicidir; hesaba tıklamak doktorlarını açar.
  - "Bölge dışı ekle" en altta (mevcut pencere).
- **Orta:**
  - "Seçili hesap" başlığı, ad, "İl · N doktor · M bağlı eczane";
  - Doktorlar / Bağlı eczaneler sekmeleri (sayılı);
  - hızlı filtreler **sayılı** ("Bu hafta görülmesi gerekenler (8)");
  - "Doktor ara..";
  - uzmanlık seçici;
  - "Tümünü seç (N)";
  - "Ürün uygula (N)".
- **Doktor tablosu (F4-3):** düz tablo, DataTables responsive daraltması **yok** (dar ekranda yatay kaydırma). Sütunlar mockup sırasıyla:
  1. seçim;
  2. Doktor;
  3. Uzmanlık;
  4. **Ürünler** (çipler adla + düzenle kalemi; ürünsüzse "ürün yok · Ürün seç");
  5. Sıklık;
  6. Yapılan / kalan;
  7. Son ziyaret;
  8. **Durum** (Bu hafta · İzin yok · Pasif · segment).
  - Tablo altında lejant: tanıtım · hatırlatma · önerilen · son ziyaret · sizin seçiminiz · onaylı içerik yok.
- **Sağ — Seçim özeti:**
  - doktor / eczane / hesap sayıları;
  - **"Bu hafta ≈ 1,4 sa (ürünlere göre) · haftalık sürenin %4'ü".** Şimdi "≈ 0 sa · %0" görünüyor, oysa detay üstü aynı haftaya "1,4 sa" diyor. İkisi aynı kaynaktan (önizleme `days[]`, seçili hafta) ve aynı biçimle gösterilmeli.
  - Ürün dağılımı **adla**;
  - ürünsüz doktor uyarısı;
  - aşım uyarısı.
- **Seçilenler:** hesaba göre katlanır gruplar (ad + sayı + ok); satır "Ad · Uzmanlık" + kaldır.

### 7. Ürün adları her yerde
- Çip, dağılım, kaydırılan ürün satırı, doktor paneli, ürün seçicinin seçili listesi, Rota durak çipleri: `productName ?? productCode`.
- Kod ipucunda (`title`) kalabilir.

### 8. Rota (tasarım aynı)
- Gün düğmeleri: "Pzt 19 Oct, 26" → "Pzt 19 Eki" (**F4-7**, ortak biçimleyici).
- Hafta açılır listesi boş kalmasın: dönemin bütün haftaları, seçili hafta işaretli. 43. haftada şimdi boş görünüyor.

### 9. Onay ya da yeniden açma sonrası yer (F4-6)
- Haftalar'dan onaylayınca ya da yeniden açınca kullanıcı **Haftalar sekmesinde ve aynı haftada** kalır.
- Detay üstündeki eylem kartından onaylayınca bugünkü davranış (Rota) korunabilir.

## KORU / YAPMA
- Backend'e dokunma (4G). Yeni yazma ucu yok. Mevcut uçlar ve oturum güncellemesi.
- 4D / 4E / 4F özellikleri:
  - güne taşıma (sürükle ve "Güne taşı…");
  - kurum bütün taşıma;
  - sabit simgesi ve kaldırma;
  - boş süre;
  - sığmadı satırı;
  - klavye yolu.
- Rota tasarımı ve gün içi davranış aynı.
- UAS-001: yetkisiz kullanıcıya iskelet çizilmez (değişmez).
- **7 dil** (en, tr, fr, es, zh, ar, ru) — yeni her metin anahtarı 7 dilde; TR diakritik tam. Görünüm `Localizer["Key"]` argümansız kullanılıyorsa değerde `{0}` olmaz (CT koruma testi `VisitPlanningViewLocalizerFormatGuardTests`).
- Arapça RTL: yerleşim aynalanır (şerit, iki kart, tablo, çekmece sağdan sola).

## Acceptance
- Web testleri (üretim JS / görünüm üzerinde):
  1. Koruma testi: Visit Planning JS'inde `toLocale*(undefined` ve `'en-US'` yok; biçimleyici `documentElement.lang` okur.
  2. Biçimleyici: `tr` → "Pzt 5 Eki", "5–9 Eki", "28 Eyl–2 Eki", "6,5 sa"; `en` → "Mon 5 Oct", "6.5 h"; `ar` → Arapça ay.
  3. Boş hafta: gün satırı / ürün / kaydırma bölümü yok; boş durum + "Bu haftayı üret".
  4. Şerit kartı alanları ve boş kartın kesik çizgisi.
  5. Doktor listesi: "dönemde 1 (varsayılan)"; şerit dikdörtgenleri; "yapıldı" lejantı.
  6. Doktor paneli: dönemin bütün haftaları listelenir.
  7. Hedefler tablosu 8 sütun, responsive daraltma kapalı; Ürünler ve Durum görünür.
  8. Seçim özeti süresi = detay üstü "bu hafta planlanan" (aynı kaynak).
  9. `productName ?? productCode` her çip / dağılım noktasında.
  10. Onay ve yeniden açma sonrası Haftalar + aynı hafta.
  11. Rota hafta listesi seçili haftayı içerir.
  12. Yeni anahtarlar 7 dilde.
- **Sabotajlar (her biri kırmızı kanıtla, geri al):**
  - `weeks.js`'e `toLocaleDateString(undefined` geri koy → test 1 kırmızı.
  - Boş haftada gün satırlarını çiz → test 3 kırmızı.
  - Tabloda responsive'i aç → test 7 kırmızı.
- **E4 (CT):**
  - mockup v2 ile yan yana: şerit, iki kart, boş hafta, doktor listesi, panel, Hedefler;
  - tarayıcı İngilizceyken TR seçili → her yerde "Eki";
  - **Arapça RTL** (kullanıcı dili değiştirir);
  - 4D / 4F taşıma yolları yeniden.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (WP-VP-4G ile paralel)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-4H · Mockup v2 uyumu + uygulama dili + küçük düzeltmeler (Web)
Repository: C:\tmp\vp-4h (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vp-4h · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4H-mockup-alignment-culture.md — önce oku (madde 0–9 tam liste). Mockup: execution/domains/commercial-suite/work-packs/mockups/visit-planning/visit-planning-v2.decoded.html (Hedefler ~386, Haftalar ~556, çekmece ~726; stil değerleri satır içinde) + standalone HTML. Bağlam: WP-VP-4C/4D/4E/4F §37'leri. Kod: frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/** · Views/CRM/VisitPlanning/** · Resources/Views/CRM/VisitPlanning/**.
NE: (0) ortak biçimleyici documentElement.lang ile (tarih "Pzt 5 Eki", aralık "5–9 Eki", saat "6,5 sa"); undefined/'en-US' kalmaz. (1) şerit kartı dikey 128px + boş kart kesik çizgi. (2) hafta ayrıntısı + "Bu haftadaki doktorlar" yan yana; gün satırı ızgarası 18/96/1fr/150; ziyaret satırı ad+kurum+çip(ad)+süre; 4D/4E kurum grubu, taşıma, sabit, boş süre KORUNUR; ürün başına çip ad+kalın sayı; kaydırılan satırı neden sözlüğü (capacity_full, no_near_day, pin_overflow, consent_blocked). (3) boş hafta = yalnız boş durum + "Bu haftayı üret". (4) doktor listesi "dönemde N · yapılan/kalan", bilinmiyorsa "dönemde 1 (varsayılan)", ince şerit, lejant "yapıldı" (reportStatus; yoksa onaylı), "Tümünü göster". (5) doktor paneli: 3 kutu, sıradaki ziyaret kartı (kaynak + "1 tanıtım + 1 hatırlatma + rapor ≈ N dk" + Ürünleri değiştir), "Sıradaki içerik: yakında", dönemin BÜTÜN haftaları ürün geçmişi. (6) Hedefler: sol "Hesaplarım (bölgem)" my-accounts listesi (arama, 50'şer), "Klinik/hastane ekle" kalkar, "Bölge dışı ekle" altta; orta sayılı hızlı filtreler, "Tümünü seç (N)"; tablo 8 sütun responsive YOK (Ürünler + Durum görünür) + lejant; özet süresi detay üstüyle aynı kaynak; dağılım adla; Seçilenler katlanır gruplar. (7) productName ?? productCode her yerde. (8) Rota: gün düğmeleri biçimleyiciyle, hafta listesi boş kalmaz (tasarım aynı). (9) Haftalar'dan onay/yeniden aç sonrası Haftalar + aynı hafta.
KORU/YAPMA: backend'e dokunma (4G paralel; productName/reportStatus/countryCode yoksa kod/gizle, hata yok); yeni yazma ucu yok; Rota tasarımı aynı; UAS-001; 7 dil + TR diakritik; argümansız Localizer değerinde {0} yok (CT koruma testi); Arapça RTL aynalı.
DOĞRULA (E2): Web (780/0 tabanı) · CRM 2437/0/5 (dokunulmaz) · mimari 27; JS node --check; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Testler belge Acceptance 1–12; sabotaj 3 (kırmızı kanıtla, geri al). Kullanıcının oturum açık sekmesine harness/mock enjekte etme.
Commit: "feat(web): WP-VP-4H — mockup v2 alignment, app-culture dates, product names, empty week" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt, mockup'tan bilinçli sapmalar (gerekçeli), elle denenecekler. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-08)
**Commit:** `64af911a0` (ajan `5db3977ab`, test dalına cherry-pick, 4G'nin üstüne). Push: test dalı.

**CT K13:**
- Web 780 → **792/0** (+12). Worktree'de ve birleşik koşuda aynı.
- CRM 2451/0/5 (birleşik).
- Mimari: listesiz 27.
- Visit Planning JS'inde kalan tek yerel çağrı `toLocaleLowerCase('tr')` (arama kalıbı, bilinçli). `undefined` ve `'en-US'` yok.

**Kod okuması:**
- **`format.js`:** `documentElement.lang`, 'en' → en-GB (gün önce). İçinde `workRange`, `productLabel`, `weekLoad` var. Dört görünüm `index.l10n.js`'ten sonra yüklüyor.
- **Details:**
  - hafta ayrıntısı ve doktorlar kartı flex ile yan yana (520 / 420);
  - sol hesap listesi `my-accounts`, arama ve kaydırdıkça yükleme;
  - `vp-add-account` kalktı;
  - tablo 8 sütun;
  - hızlı filtre sayıları var.
- **4D / 4E / 4F:** taşıma ve sabit bileşenleri testlerle korunuyor; 4D'nin `MoveLockedHint`'i ajan tarafından geri getirildi.

**CT sabotajı:** `productLabel` önce kodu okuyacak şekilde değiştirildi → 1 kırmızı (`Every_chip_and_distribution_reads_the_product_name_before_the_code`). Geri alındı.

**Bilinçli sapmalar (ajan raporu, CT kabul):**
- Boş haftada başlıktaki eylemler duruyor.
- Renkler hex yerine tema sınıflarıyla.
- Hesap listesi yalnız klinik / hastane türleri.
- Haftalar'a dönüş notu sessionStorage'da tek kullanımlık, 5 dk.
- `weekLoad` 4E `days[]` toplamını öncelikli okuyor.

**E4:** mockup ile yan yana; tarayıcı İngilizceyken TR; **Arapça RTL**; boş hafta; onay ve yeniden açma sonrası yer; Hedefler listesi ve özet süresi; 4D / 4F taşıma yolları.

### E4 — ACCEPTED (2026-10-08, CT, plan `23b1706a`)
- ☑ **Biçim:** TR her yerde ("5–9 Eki · 2026", "Pzt 5 Eki", "1,1 sa"). Biçimleyici `<html lang>` okuyor (tr / en / ar denendi).
- ☑ **Haftalar ve boş hafta:** dikey şerit kartları, kesik çizgili boş hafta, 44. haftada tatil işareti. Boş hafta (43) mockup ile birebir.
- ☑ **Doktorlar ve panel:**
  - doktor listesi "dönemde 1 (varsayılan) · 0 / 1" + ince şerit + lejant;
  - panel: 3 kutu, sıradaki ziyaret kartı ürün adlarıyla, dönemin tüm haftaları.
- ☑ **Hedefler:**
  - sol liste (1535 hesap, plandakiler üstte);
  - 8 sütun, alt satır yok;
  - özet "≈ 1,1 sa" detay üstüyle aynı;
  - dağılım adla.
- ☑ **Rota:** "Pzt 19 Eki"; 43. haftada hafta listesi dolu.
- ☑ **Onay ve yeniden aç:** Haftalar'dan onay (2 ziyaret) ve yeniden aç (gerekçe geçmişte, 2 ziyaret `cancelled`) → ikisinde de Haftalar + 42. hafta.
- ☑ **Arapça:** `dir=rtl`, yerleşim aynalı, gün çubukları sağdan doluyor.
- **Bulgular → [WP-VP-4I](WP-VP-4I-pin-travel-rtl-account-cards.md):**
  - RTL bidi ("018 KLİNİK" → "KLİNİK 018");
  - şerit kartı ortalı ve başlık kırılıyor;
  - gün satırı "6 / 57 ziyaret" kırılıyor;
  - "Sept";
  - Hedefler hesap kartları mockup'tan sapmış;
  - il kodun son parçası ("ISTANBUL");
  - "Clinic" / "Family Medicine" etiketleri.

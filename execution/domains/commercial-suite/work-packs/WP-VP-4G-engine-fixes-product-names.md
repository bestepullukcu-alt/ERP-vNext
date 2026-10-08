# WORK PACKAGE — WP-VP-4G · Gün atama düzeltmeleri + ürün adları + okuma alanları (backend)

> **CT (SoR), 2026-10-08.** Faz 4 E4 bulgularının backend kısmı ([yol haritası](ROADMAP-visit-planning.md) Faz 4 E4, F4-1 / F4-4 / F4-5 / F4-8 / F4-9 / F4-10).
> - **Kullanıcı kararları (2026-10-08):**
>   - "Az dolu bir gün uzak bir kümeyi kabul etsin; kural yalnız dolu günlerdeki boşluklar için geçerli olsun. Kaydırma olursa doğru nedeni göstersin." (F4-1)
>   - "Önerini de paketle" (F4-5: bir ziyaret sabitlenince diğerleri yerinde kalsın).
>   - "Ürünün kodu yerine adı gözüksün" (F4-4).
> - **Kapsam:** yalnız CRM servisi (Application / Domain / Infrastructure). Web: WP-VP-4H (paralel; ad yoksa kodu gösterir).
>
> **Çalışma yeri:** worktree `C:\tmp\vp-4g`, dal `wp/vp-4g` (test dalı `test/crm-content-visit-e2e` başından). Commit bu dala, push YOK.

## Canlıda görülen durum (plan `23b1706a`, 41. hafta)
- Pazartesi–Çarşamba geçmiş. Perşembe: 01 NOLU + 03 NOLU (6 doktor, 73 dk). Cuma: 06 NOLU TOKİ (1 ziyaret, 7,5 sa boş).
- 018 KLİNİK (CEZAİR / BENAL BÜYÜKGEBİZ) iki güne de uzak → `capacity_full` ("hafta dolu") ile 42. haftaya kaydı. Hafta dolu değildi.
- SERHAT KOKULU "Yalnız bu doktor" ile Cuma'ya sabitlendi → aynı kurumdaki HÜSEYİN ve SEDAD (sabitsiz) da Cuma'ya geçti.
- 06 NOLU TOKİ Rota'dan Perşembe'ye sabitlendi → Perşembe'deki 6 doktorun hepsi Cuma'ya geçti. Temsilci "yalnız birini taşıdım" diye şaşırır.
- Ürün çiplerinde ve dağılımda `GP-000000000001` gibi kodlar görünüyor; `productName` hep boş.

## NE

### 1. F4-1 — Uzak küme az dolu güne girebilir
- Kural sırası (`DayBalancer`):
  1. Kümeler önce **boş** günlere tohum olur (mevcut "en uzak tohum").
  2. Boş gün kalmadıysa uzak küme, **az dolu** bir güne girebilir. Az dolu = günün planlı süresi, gün bütçesinin %50'sinden az (`LightDayLoadRatio = 0.5`, sabit, testle korunur). Kümeler arası yol tahmini (mevcut seyahat modeli) gün bütçesine eklenir; sığmıyorsa o gün değil.
  3. Birden çok aday varsa en az dolu gün, eşitlikte en yakın gün.
  4. "Uzak kümeyle doldurma yok" kuralı **yalnız boşluk doldurmada** kalır (gün zaten yakın kümesiyle doluysa kalan boşluk uzak kümeyle doldurulmaz → `idleMinutes`).
- Kaydırma nedeni doğru olmalı:
  - `capacity_full`: haftanın hiçbir günü sığdırmıyor (gerçekten dolu).
  - **yeni** `no_near_day`: sığacak yer var ama hiçbir gün az dolu değil ve kümeye yakın gün yok.
  - `pin_overflow`, `consent_blocked` aynen.
  - Yeni neden kodu, `shifted[].reason` sözlüğüne ek (mobil yalnız ek alan; Faz 5 notuna).
- Son taslak hafta istisnası (4E farFill) korunur.

### 2. F4-5 — Sabit eklemek diğer ziyaretleri oynatmaz (kararlı yerleşim)
- Önizleme iki adımda hesaplanır, hiçbir şey saklanmaz:
  1. **Taban yerleşim:** aynı girdilerle **sabitler yokmuş gibi** gün ataması (temsilcinin sabitten önce gördüğü düzen).
  2. **Sabitleri uygula:** sabitli ziyaretler / kurumlar tabandaki günlerinden çıkarılıp sabit günlerine konur. Diğer ziyaretler **taban günlerinde kalır.**
  3. Yalnız bir gün bütçeyi aşarsa, o günün sabitsiz ziyaretlerinden günün merkezine en uzak olanlar başka güne taşınır (önce yakın, sonra az dolu gün; 1. maddenin kuralları). Sabitli ziyaret asla yerinden oynatılmaz (sığmayan kısmı 4E `pin_overflow` ile aynen).
- "Yalnız bu doktor" sonucu: SERHAT Cuma'ya gider, HÜSEYİN ve SEDAD Perşembe'de kalır.
- TOKİ Perşembe'ye sabitlenince Perşembe'deki 6 doktor Perşembe'de kalır (bütçe yetiyor).
- 4E `autoPinned` anlamı değişmez (sığmayan sabit kısmının ertesi güne otomatik sabiti). Kurum grubu kuralı tabanda aynen.
- Deterministik: aynı girdi → aynı sonuç (test).

### 3. F4-4 — Ürün adı
- `productName` şu yanıtlarda dolu olmalı:
  - `PlanningSessionProductDto` (oturumdaki seçimler);
  - önizleme ziyaret kalemleri (`contentItems[]`, `productDistribution[]`, `overflowProducts[]`, sığmayan ürün satırları);
  - `PlannedVisitContentItemDto`.
- Kaynak: MDM ürün adı, **okuma anında toplu** tek istek (yanıttaki tüm ürün kimlikleri). Mevcut ürün okuyucusunu kullan (oyun editörü / `products` vekili ne kullanıyorsa), yeni dış bağımlılık açma.
- **Ad eksikliği hata değildir:** MDM erişilemezse ya da ürün bulunmazsa `productName = null`, yanıt 200 (Web kodu gösterir). Plan okuması MDM yüzünden asla 5xx vermez.
- Planlanan ziyarete yazılırken (onay) ad **anlık görüntü** olarak saklanır (`PlannedVisitContentItem.ProductName`, ek alan, class-map'e). Eski kayıtlarda null → okuma anında doldurulur.
- Seçimi kaydetmek (ürün seçici) adı istemcidan **almaz**; ad hep sunucudan.
- İstek başına bir MDM çağrısı (N+1 yok, test).

### 4. F4-8 — `resources/me` temsilci ülkesi
- Ek alan `countryCode`: temsilcinin bölge atamasının ülkesi (Territory `country` sözlüğü, küçük harf). Bölge yoksa tüzel kişinin ülkesi; o da yoksa null.
- Çalışma takvimi ve tatil gösterimi bu alanı okuyacak (Web 4H).

### 5. F4-9 — Önizlemede ziyaret başına rapor durumu
- Önizleme `scheduled[]` ve hafta özetlerinde ek alan `reportStatus`: `none` | `reported` (raporlu ziyaret) | `cancelled`.
- Kaynak: planlanan ziyaretin rapor ilişkisi (mevcut okuma; yeni sorgu varsa tek toplu sorgu).
- Haftalar "yapıldı" rozeti ve doktor dönem şeridindeki "yapıldı" noktası bunu kullanır.

### 6. F4-10 — Temsilcinin görünen adı
- `resourceDisplayName` ve `resources/me.displayName`: kişinin adı soyadı (Person / Employee / User adı, mevcut çözücü sırası). E-posta yalnız son çare.
- Plan listesinde ve detay üstünde "Admin User" / e-posta yerine ad.

## KORU / YAPMA
- **YENİ YAZMA KOMUTU YOK** (listesiz 27 aynı kalır). Mevcut oturum güncellemesi ve onay yolu.
- 3A / 3B / 3C / 4E kuralları (sıklık, gün bütçesi, yarım gün, ürün sırası, sabit kapsamı, mesai aşımı yok) değişmez; yalnız yukarıdaki gün seçimi ve okuma alanları.
- Göç / seed / grant / indeks YOK. `ProductName` ek alan, eski kayıt okunur.
- Kiracı sınırı: her sorgu `TenantId`. MDM çağrısı kiracı bağlamında.
- ARCH GATE: temsilci yanıtında oyun / kampanya kimliği yok (değişmez).
- Mobil yalnız ek alan: `productName`, `no_near_day`, `countryCode`, `reportStatus`.

## Acceptance
- Testler (üretim kodu üzerinde):
  1. Canlı durumun birebir kopyası: Perşembe 2 yakın kurum, Cuma 1 ziyaret, uzak 018 KLİNİK → Cuma'ya girer, kaydırma yok.
  2. Gün yakın kümesiyle %50'den fazla doluysa uzak küme kalan boşluğa girmez → `idleMinutes`.
  3. Hiçbir gün sığdırmıyorsa `capacity_full`; yer var ama hiçbir gün az dolu değil ve yakın değilse `no_near_day`.
  4. "Yalnız bu doktor" sabiti: aynı kurumun sabitsiz doktorları taban günlerinde kalır.
  5. Bir kurumu başka güne sabitlemek, o günün sabitsiz ziyaretlerini (bütçe yetiyorsa) yerinden oynatmaz.
  6. Bütçe aşılınca yalnız sabitsiz ve merkeze en uzak ziyaretler taşınır; sabitli asla.
  7. Determinizm: aynı girdiyle iki önizleme aynı.
  8. `productName`: oturum, önizleme kalemleri, dağılım ve planlanan ziyarette dolu; MDM yokken null + 200; tek MDM çağrısı.
  9. Onayda `ProductName` anlık görüntüsü yazılır; eski kayıt null okunur ve doldurulur.
  10. `resources/me.countryCode` (bölgeden, tüzel kişiden, null); `displayName` ad soyadı, e-posta yalnız son çare.
  11. `reportStatus` raporlu / iptal / yok.
- **Sabotajlar (her biri kırmızı kanıtla, geri al):**
  - `LightDayLoadRatio` kuralını kaldır (uzak küme hep kaydırılsın) → test 1 kırmızı.
  - Tabanı atla (sabitlerle sıfırdan dengele) → test 4 / 5 kırmızı.
  - Ad okumasını ürün başına ayrı çağrıya çevir → test 8 (tek çağrı) kırmızı.
- Mimari testleri: 27 (yeni yazma ucu yok).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (WP-VP-4H ile paralel)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VP-4G · Gün atama düzeltmeleri + ürün adları + okuma alanları (backend)
Repository: C:\tmp\vp-4g (worktree, test/crm-content-visit-e2e başından) · Branch: wp/vp-4g · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VP-4G-engine-fixes-product-names.md — önce oku. Bağlam: WP-VP-3B, 3C, 4A, 4E belgeleri ve §37'leri (DayBalancer, NearTravelMinutes, idleMinutes, farFill, DayPins, autoPinned, pinOverflow, PlanningSessionProductDto). Kod: services/Diten.CrmService/src/**/Features/VisitPlanning/** · Domain/Entities/PlannedVisit.cs · resources/me çözücüsü.
NE: (1) F4-1 uzak küme önce boş güne, sonra az dolu güne (planlı süre < bütçenin %50'si, LightDayLoadRatio=0.5, kümeler arası yol bütçeye eklenir; en az dolu, eşitlikte en yakın); "uzakla doldurma yok" yalnız boşluk doldurmada; kaydırma nedeni capacity_full (gerçekten dolu) / yeni no_near_day; son taslak hafta istisnası korunur. (2) F4-5 kararlı yerleşim: taban = sabitsiz atama; sabitler uygulanır, diğerleri taban günlerinde kalır; yalnız bütçe aşılırsa sabitsiz + merkeze en uzak taşınır; sabitli asla; deterministik; autoPinned anlamı aynı. (3) F4-4 productName: oturum seçimleri, önizleme contentItems/productDistribution/overflow, PlannedVisitContentItemDto — okuma anında TOPLU tek MDM çağrısı (mevcut ürün okuyucusu), MDM yoksa null + 200 (asla 5xx); onayda PlannedVisitContentItem.ProductName anlık görüntü (ek alan, class-map); ad istemciden alınmaz. (4) F4-8 resources/me.countryCode (bölge country küçük harf → tüzel kişi → null). (5) F4-9 önizleme scheduled[].reportStatus none|reported|cancelled (toplu okuma). (6) F4-10 resourceDisplayName / me.displayName = ad soyadı, e-posta son çare.
KORU/YAPMA: YENİ YAZMA KOMUTU YOK (listesiz 27); 3A/3B/3C/4E kuralları değişmez; göç/seed/grant/indeks yok; TenantId her sorguda; ARCH GATE (oyun/kampanya kimliği yok); mobil yalnız ek alan.
DOĞRULA (E2): CRM (2437/0/5 tabanı; bilinen PII kararsızı olabilir) · Web 780/0 (dokunulmaz) · mimari 27. Testler belge Acceptance 1–11; sabotaj 3 (kırmızı kanıtla, geri al). Fleet açıkken bin kilitliyse -o ile ayrı çıktı klasörü.
Commit: "feat(crm): WP-VP-4G — light-day far clusters, stable day pins, product names, me.countryCode, reportStatus" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: ne yapıldı + kanıt (test adları, sabotaj çıktıları), mobil için yeni alanlar, elle denenecekler. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-08)
**Commit:** `92244a0d7` (ajan `860af9a30`, test dalına cherry-pick). Push: test dalı.

**CT K13:**
- CRM 2437 → **2451/0/5** (+14). Worktree'de ve 4H ile birleşik koşuda aynı.
- Web 792/0 (birleşik, dokunulmadı).
- Mimari: listesiz **27** (bilinen AUD-001, değişmedi; yeni yazma ucu yok).

**Kod okuması:**
- **`DayBalancer`:**
  - (b'') az dolu gün: merkezi olan ve planlısı bütçenin %50'sinden az olan gün; kümeler arası yol güne yazılıyor; sıra en az dolu, sonra en yakın.
  - Boşluk doldurma kuralı aynı.
  - farFill yalnız son taslak hafta.
  - `NoNearDay`: taşan ziyaret için haftada yer kalan bir gün varsa `no_near_day`, yoksa `capacity_full`.
- **`AssignAroundPins`:**
  - Taban = sabitsiz `Assign` (sabitli ve taşan dahil bütün ziyaretler).
  - Sabitler sabit gününde, sabitsizler taban gününde kalıyor.
  - Bütçe aşılırsa yalnız sabitsizler, merkeze en uzaktan başlayarak taşınıyor. Kalanlar F4-1 kurallarıyla yerleşiyor.
  - Sabit yoksa eski 4E yolu aynen çalışıyor.
- **Ürün adı:**
  - `IProductNameReader` + `MdmProductNameReader`: MDM `global-products/selector` (alanlar `Id` / `CanonicalCode` / `GlobalProductName` — MDM DTO ile doğrulandı), sayfa 100, en çok 20 sayfa, 3 sn.
  - Hata olursa o ana kadar bulunan adlarla dönüyor, hata fırlatmıyor.
  - Motor istek başına not defteri tutuyor; onay ve önizleme aynı okumayı paylaşıyor.
  - Onayda `PlannedVisitContentItem.ProductName` anlık görüntü olarak yazılıyor (iç içe AutoMap, açık class-map yok, eski kayıt null okunuyor).
  - Oturum DTO'sunda kişi sırası 1:1 hizalı (aynı `SelectedContacts` listesi).
- **Diğer alanlar:**
  - `me.countryCode`: güncel atamanın düğüm ülkesi, yoksa modelin ülke kapsamı, yoksa null.
  - `PreferPersonName`: e-posta yalnız son çare.
  - `reportStatus`: tek toplu rapor okuması.

**CT sabotajı:** `no_near_day` sınıflandırması kapatıldı (hep `reasonForWeek`) → 1 kırmızı (`The_shift_says_no_near_day_when_there_was_room_and_capacity_full_only_when_there_was_none`). Geri alındı.

**E4'te bakılacak:**
- **Ad okuma izni:** selector `mdm.global-products.read` izni istiyor. Bu izin olmayan saha temsilcisinde adlar boş gelir ve kod görünür (fail-open). Canlıda temsilci rolüyle kontrol edilecek; gerekirse grant ya da ayrı bir okuma yolu.
- **MDM toplu id süzgeci:** ayrı iş (selector'ı sayfa sayfa okumanın yerine).
- **`countryCode`:** tüzel kişi basamağı yok (token'da tüzel kişi claim'i yok).

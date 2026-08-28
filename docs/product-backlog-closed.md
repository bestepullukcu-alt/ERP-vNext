# Ürün Backlog — ARŞİV (kapanmış kayıtlar)

> **98 kapanmış kayıt.** Buraya 2026-08-28'de `docs/product-backlog.md`'den TAŞINDILAR — silinmediler.
> Kural K3: kayıt silinmez. Kapanmış bir kayıt, bir hatanın neden ve nasıl çözüldüğünü anlatan tek kaynak olabilir;
> bu oturumda birkaç kez öyle oldu.
>
> Kayıtlar **özgün dosya sırasında** duruyor; her birinin `Geldiği bölüm` satırı hangi başlığın altındaydı onu söylüyor.
> Başlık metinleri **değiştirilmedi** — tarihî metin olarak korundu; yetkili cevap `DURUM` alanındadır (kural K1).

---

### ~~BL-006 — MDM / Position audit entegrasyonu~~ ✅ TAMAMLANDI (2026-07-11)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Backlog maddeleri

- **TESLİM EDİLDİ:** Faz 1 (Platform: Position/PositionAssignment + Quotas + Subscriptions auditable) + Faz 2 (MDM/Legal Entity → S2S ile Platform merkezi audit_events, SourceService="Diten.MDM") + Faz 3 (FG-005 audit gate). Canlı doğrulandı, commit `c3a66794`. Kalan düşük-öncelik: BL-014 (correlation-id) + Platform biz-config/prefs (~50 cmd, ertelendi).

### ~~BL-014 — MDM audit forward correlation-id threading~~ ✅ TAMAMLANDI (2026-07-11)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Backlog maddeleri

- **TESLİM EDİLDİ:** `PlatformAuditForwarder` artık gelen isteğin `X-Correlation-Id`'sini (Guid ise) audit CorrelationId olarak kullanıyor; yoksa fresh id fallback. Canlı doğrulandı (gönderilen correlation audit kaydına birebir geçti). Commit BEKLİYOR (sabah commit+push).

### BL-093 — ✅ KAPANDI (2026-08-13) — `AddChecklistItem` kapalı görevi reddetmiyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ölçüm (2026-08-13):** `SetChecklistItemStateHandler` kapalı görevde 409 döndürüyor (ChecklistHandlers.cs:61).
  `AddChecklistItemHandler` bu denetimi **hiç yapmıyor** — Done/Cancelled bir göreve yeni madde eklenebiliyor.
  Bu turda eklenen üç fiilin üçü de reddediyor (canlı doğrulandı: iptal edilmiş görevde `TASK_INVALID_STATE`).
- **Sonuç:** aynı kartın beş fiilinden dördü kapalı görevi reddediyor, biri kabul ediyor.
- **Kapanış (2026-08-13):** `AddChecklistItemHandler` artık Done/Cancelled görevde 409 `TASK_INVALID_STATE`
  dönüyor. `ChecklistWriteGuards.ResolveAsync` ile DEĞİL, elle: bu fiil meşru olarak henüz RUN YOKKEN çalışıyor
  (ilk maddeyi ekleyen o), resolver'ın "bu görevin kontrol listesi yok" 404'ü diğer dördü için doğru, bunun için
  yanlış olurdu. İki test: `BL_093_a_closed_task_can_no_longer_GROW_new_checklist_items` (Done + Cancelled).
- **Gelecek regresyon riski: 🟠** — bugün ön yüz kapalı görevde ekleme kutusunu gizliyor, yani kusur yalnız
  API seviyesinde erişilebilir; ön yüz kilidi güvenlik değildir.

### BL-094 — ✅ KAPANDI (2026-08-14) — Detay sayfasında sürükle-bırak: karar DEĞİŞTİ, yapıldı
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar


> **KARAR DEĞİŞİKLİĞİ.** Bu maddenin ilk hâli "hayır" diyordu. Silinmedi, **yeniden yazıldı** — aşağıda önce
> eski gerekçe, sonra hangi dayanağının düştüğü, sonra ölçüm var.

- **Eski karar (2026-08-13) ve gerekçesi:** create formunda Sortable vardı, detayda yoktu. "Sürükle gelirse taşı
  düğmeleri zorunlu" deniyordu; tersi zorunlu değildi. Düğmeler WCAG 2.2 §2.5.7 karşılığı olarak tek başına
  yeterliydi, sürükleme bir kolaylıktı — ve **iki ayrı bileşen** vardı, yani iki ekranın farklı davranması
  savunulabilirdi.
- **Düşen dayanak (2026-08-14):** artık **tek bileşen** var (`assets/js/shared/diten-checkitem.js`). Aynı satırın
  bir ekranda sürüklenip diğerinde sürüklenmemesi, bileşenin bitirmek için var olduğu ayrışmanın ta kendisi.
  Sahip kararı: al — **iki şartla**.
- **Şart 1 — OK DÜĞMELERİ KALDI.** Sürükleme düğmelerin ÜSTÜNE eklendi, yerine değil. §2.5.7'nin istediği
  tek-işaretçi alternatifi ve klavye yolunun tamamı onlar (Sortable'ın klavye hikâyesi yok). Canlı doğrulandı:
  sürüklemeden SONRA `data-diten-check-move="down"` tıklandı → sıra değişti, düğme `disabled=false`, odak alıyor.
- **Şart 2 — YIĞILMIŞ DÜZENDE ÖLÇÜLDÜ.** 900×1600, `.wcn-detail-content` = 869px (tek sütun), sayfa kaydırılmış
  (`scrollingElement.scrollTop = 64`), `pointerType: 'touch'` pointer olayları. 1. satır griple tutulup 3. satırın
  %75'ine bırakıldı → DOM sırası tam bırakılan yere geçti, `Kontrol listesi güncellendi.` bildirimi, ve sunucu
  projeksiyonu yeni sırayı doğruladı. 1440×1800'de de aynısı (4. konuma bırakma) çalıştı. Kaydırma kaynaklı
  konum kayması YOK.
- **Ne ölçülemedi (dürüstçe):** tarayıcı panelinin gerçek dokunmatik emülasyonu (`width < 768` gerektiriyor, oysa
  şart 900px'di) ve 64px'ten daha derin bir kaydırma — barındırılan tarayıcı paneli gizliyken gerçek girdi çağrısı
  30 sn'de zaman aşımına uğruyor ve programatik `scrollTop` ataması reddediliyor. Kullanılan yöntem: sentetik
  `pointerType:'touch'` olayları (Sortable `forceFallback` ile bunları gerçek girdi gibi işler — bileşenin
  yorumunda "el dışında hiçbir şeyle sınanamaz" diye kaydedilen native DnD'den kaçınmanın sebebi tam da bu).
- **Uygulama:** `.wcn-checks` üzerine Sortable (`bindChecklistDrag`), `handle: '[data-diten-check-grip]'`,
  `forceFallback: true` — create formuyla birebir aynı ayarlar. Grip artık iki kipte de çiziliyor. Bırakma sırası
  **projeksiyondan** hesaplanıyor, DOM yalnız INDEKS'i veriyor. Kapalı görevde Sortable hiç bağlanmıyor.
- **Gelecek regresyon riski: 🟢 eklemeli** — düğmeler yerinde, kapalı görev korunuyor.

### BL-108 — ✅ KAPANDI (2026-08-14) — [SAYFA BAŞLIĞI] Breadcrumb ile ilk kart arası 28px, standart 12px
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Sahip bildirimi:** "breadcrumb ile altındaki kartta sorun var, aralarındaki boşluk standartların dışında."
- **Ölçüm (canlı, 1440px):**
  - `/WorkCenterNext/Details/{id}` → breadcrumb alt **142**, kart üst **170** = **28px**
  - `/Tasks/Create` (Golden Reference Compact deseni) → breadcrumb alt **142**, kart üst **154** = **12px**
  - Aynı desen: `Views/Organization/Positions/Details.cshtml`, `OrganizationUnits/Details.cshtml` — başlık bloğu
    `.row`'un **dışında**, boşluğu yalnız kendi `mb-3`'ü (bu temada 12px) veriyor.
- **Kök sebep — kopyalanan markup, kopyalanmayan YERLEŞİM:** `app.js` başlığı
  `<div class="col-12">${pageHeader}</div>` olarak `.row.g-4.wcn-detail-grid` **içine** koyuyordu. Böylece
  altındaki kolon başlığın `mb-3`'ünü (12px) **artı** satırın dikey gutter'ını (16px) alıyordu. 28 = 12 + 16;
  iki ayrı boşluk sistemi üst üste biniyor ve ortaya kimsenin seçmediği bir sayı çıkıyordu.
  Kod incelemesinde görünmez, ekranda görünür.
- **Düzeltme:** başlık grid'in dışına, referanstaki yere alındı. Yeni CSS **yok** — kusur yapısaldı, düzeltme de
  yapısal. Bootstrap'in gutter'ı kendi kuralına göre sadeleşiyor (satır `-16px` ↔ ilk kolon `+16px`).
- **Doğrulama (canlı):** açık görev, engelli görev · 1440 ve 900 · açık ve karanlık tema → **hepsinde 12px**.
  İçerik/ray kolonları yan yana kalıyor, yatay taşma yok, rehber banner'ı başlıkla birlikte taşındı.
- **Test:** `wcn-detail-three-regions.test.js` → "sits OUTSIDE the grid row" + "leaves the grid carrying only the
  three regions". Yapısal olarak iddia ediliyor çünkü jsdom layout hesaplamıyor; piksel ölçümü tarayıcıda yapıldı.
  **Mutasyon:** başlık grid'e geri konunca iki test kırmızı.
- **Gelecek regresyon riski: 🟢** — tek yapısal satır, davranış değişmiyor.

### BL-109 — ✅ KAPANDI (2026-08-14) — [MEVCUT AKSİYONLAR] "Başkasına ata" HİÇ açılamıyordu: `.data` vs `.data.people`
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Bulunma şekli:** bu turun Kural 2 kapısı ("cümleyi silmeden önce varış yerini AÇ, GÖR, ÖLÇ") uygulanırken.
  Düğmeye basıldı, diyalog açılmadı, yerine "Bu görevin devredilebileceği kimse yok." bildirimi çıktı.
- **Ölçüm (canlı):** `TasksApi.assignablePeople()` → `{ data: { people: [...4 kişi...] } }`. `app.js` ise
  `people = (res.ok && res.data) ? res.data : []` yazıyordu; `res.data` bir NESNE, `.length` `undefined`,
  guard "kimse yok" sonucuna varıp `return` ediyordu. **Kiracıda dört atanabilir kişi varken.**
- **Neden bu kadar sinsi:** aksiyon çizilmiş, klavyeyle erişilebilir, tıklanabilir ve her seferinde nazikçe
  reddediyordu. Hata yok, konsol sessiz, test yok.
- **ÜÇÜNCÜ TEKRAR:** aynı dosyada `loadAssignablePeople` (`app.js`) bunu DOĞRU açıyor ve başında BL-057'den
  kalma bir yorum var — quick-create offcanvas'ta yaşanan ikizini anlatıyor. Bu satır o yorumun üçüncü kardeşi.
- **Düzeltme:** `(res.ok && Array.isArray(res.data?.people)) ? res.data.people : []` — diğer iki çağrı yeriyle
  birebir aynı yazım.
- **Doğrulama (canlı):** diyalog açılıyor, atanabilir kişi seçicisi **4** kayıt, neden alanı yerinde.
- **Yapılacak (açık):** `assignablePeople` için tek bir unwrap yardımcısı; üç çağrı yeri üç ayrı yazımda kaldıkça
  dördüncüsü de yanlış yazılacak.
- **Gelecek regresyon riski: 🟡** — yardımcı yazılmazsa tekrar eder.

### BL-113 — ✅ KAPANDI (2026-08-14) — [API] `assignablePeople` zarfı ARTIK ÇAĞIRANLARDA AÇILMIYOR
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Sorun:** uç `{ people, excluded }` döndürüyordu; **dört çağıran, üç ayrı açma biçimi** yazmıştı ve üçü zaman
  içinde yanlıştı. Yanlış olan çökmüyordu — `res.data` bir nesne, `.length` `undefined`, guard "kimse yok"
  sonucuna varıp nazikçe reddediyordu. Son örneği (BL-109) hiç açılamayan bir devretme diyaloğuydu.
- **Neden yardımcı yetmezdi:** yardımcı isteğe bağlıdır; beşinci çağıran yine kendi satırını yazar. **Şekil
  API katmanında durduruldu:** `TasksApi.assignablePeople()` artık `data`'yı **dizi** olarak döndürüyor.
- **Dokunulan çağıranlar:** `Tasks/form-page.js`, `WorkCenterNext/quick-create.js`, `WorkCenterNext/app.js` ×2 —
  hepsi `people.ok ? people.data : []`.
- **Uyarı yorumları güncellendi:** `quick-create.js` ve `app.js`'teki "bu satır yanlış olmuştu" notları, tehlike
  kaynakta ortadan kalktığı için yeniden yazıldı (silinmedi — tarih kayıtta kaldı).
- **`excluded` bilerek düşürüldü:** hiçbir çağıran okumuyordu ve ikinci alan, nesne şeklinin geri büyüme yolu.
  BL-072 ("X neden listede yok") gerektiğinde kendi adlandırılmış çağrısını alır.
- **Testler:** iki test **ters çevrildi** — eskiden çağıranın `.data.people`'a uzanmasını ZORUNLU kılıyorlardı,
  yani kusuru hayatta tutan kuralı koruyorlardı. Artık çağıranın uzanmamasını ve açmanın `api.js`'te olmasını
  şart koşuyorlar.
- **Gelecek regresyon riski: 🟢** — şekil tek yerde.

### BL-114 — ✅ KAPANDI (2026-08-14) — [DURUM KARTI] Kart dağıtıldı; iki tarih iki farklı şey söylüyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ölçüm:** kart 121px, içeriğinin tamamı iki tarih. Adı "Durum", içinde durum yok. **Ve bir çelişki:** aynı
  son tarih Özet'te `rgb(167,172,178)` gri, Durum'da `rgb(255,62,29)` kırmızıydı — bir ekran, bir olgu, iki cevap.
- **Dağıtım:** kaynak son tarih → **Özet** (kırmızıyı da götürdü; kural değişmedi, `item.slaState === 'overdue'`,
  yani Durum kartının zaten kullandığı kaynak). Kişisel plan + plan çakışması uyarısı → **Kişisel** kartı
  (adı "Kişisel Not" → "Kişisel").
- **⚠ BRIEF'İN VARSAYIMI KISMEN YANLIŞTI:** kart yalnız tarih taşımıyordu — **onay/inceleme gate satırlarını da**
  taşıyor. Tümden silinseydi onlar da silinirdi. Yalnız tarihler çıkarıldı; gate'i olmayan görevde kart artık
  kendiliğinden görünmüyor (istenen sonuç), gate'i olan görevde duruyor.
- **Ölçülerek işaretlenen ölü CSS:** `.wcn-dates`, `.wcn-date-cell/-label/-value/-overdue/-conflict` — başka
  hiçbir view/partial/bundle'da kullanılmıyor. **`.wcn-date-warn` ÖLÜ DEĞİL:** plan çakışması notu Kişisel
  kartında hâlâ kullanıyor. Silinmedi, işaretlendi.
- **Ray:** 648px → **583px**.
- **Gelecek regresyon riski: 🟢.**

### BL-118 — ✅ KAPANDI (2026-08-14) — [BAŞLIK] Kaynak izi her görevde aynıydı; silinmedi, koşullandı
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ölçüm:** bu yüzeydeki her kayıt `providerCode: "tasks"` ve `objectType: "task"` taşıyor. "Görevler · Görev"
  her görevde aynı çıkıyor, hiçbir şeyi ayırt etmiyordu — iki sabit, olgu kılığında, gözün ilk geçişini
  gerçekten değişen sinyallerden önce alıyordu.
- **Yapılan:** modül adı yalnız `providerCode !== 'tasks'` ise, nesne türü yalnız `objectType !== 'task'` ise
  çiziliyor. İkisi de gizlenince ayırıcı hairline de gizleniyor (bölecek bir şey yokken çizgi çizmek).
- **Neden silinmedi:** merkez tasarım gereği başka sağlayıcılardan da iş topluyor. MOD-0023 iş akışı kalemleri
  geldiği gün "bu nereden geldi" gerçek bir soru olur ve alan kendiliğinden görünür. Silinseydi o gün yeniden
  yazılması gerekirdi — ve yeniden yazılması gereken şey tam olarak unutulan şeydir.
- **Test:** iki test — varsayılan sağlayıcıda gizli, yabancı sağlayıcıda görünür (`providerCode: "workflow"`).
- **Gelecek regresyon riski: 🟢.**

### BL-121 — ✅ KAPANDI (2026-08-14) — [KURAL] Kart içi bölüm ayırıcısı: kenardan kenara, iki yanı eşit
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Aynı kusur iki turda iki kartta çıktı**, üçüncüsü beklenmedi; kural yazıldı ve sekiz kart tarandı.
- **Tarama sonucu — gerçek bölüm ayırıcısı YALNIZ İKİ TANE:**
  | kart | ayırıcı | önce | sonra |
  |---|---|---|---|
  | Mevcut aksiyonlar | `.wcn-acts-destructive` | 0/0 kenar ✓ · 24px üst / 16px alt ✗ | 0/0 · **16/16** |
  | Özet | `.wcn-sumtags` | 8/8 kenar ✗ · 4px üst / 16px alt ✗ | **0/0** · **16/16** |
  Yaşam Döngüsü · Alt Görevler · Kontrol Listesi · Etkinlik · Kişisel · Teknik kartlarında **bölüm ayırıcısı yok**
  (taramada çıkanlar satır kenarlıkları, form kontrolleri, ilerleme çubukları ve uyarı bloklarıydı).
- **⚠ BRIEF'İN REFERANSI KURALI İHLAL EDİYORDU:** brief aksiyon kartını "negatif kenar boşluğu kullanmadan
  düzeltildi" diye gösteriyordu; oysa geçen turun CSS'i `margin: 1rem -1.5rem 0` kullanıyordu ve **kendi yorumu
  bunu yapmadığını iddia ediyordu**. Negatif marj, iptal etmeye çalıştığı dolguyla kavga eder ve o dolgu
  değiştiği an kırılır. İkisi de gerçek tekniğe geçirildi: ana blok satır-içi dolgu tutmuyor, her blok kendi
  içini ödüyor, çizgi kendiliğinden iki kenara varıyor.
- **Boşluk değeri kartın kendi grup ölçeğinden:** 16px (aksiyon kademelerini ayıran değerle aynı). Kartın 1.5rem'i
  yalnız kart kenarına bakan yüzlerde kaldı.
- **Gelecek regresyon riski: 🟢** — test hem kenarı hem eşitliği hem de negatif marj yokluğunu kilitliyor.

### BL-122 — ✅ KAPANDI (2026-08-14) — [ALT GÖREVLER / KONTROL LİSTESİ] İki liste tek satır diline geçti
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ölçüm (önce):** kontrol listesi satırı `bg rgb(245,245,249) · border 1px · radius 6px` (kutu);
  alt görev satırı `bg transparent · yalnız border-top · radius 0` (ayrılmış çizgi). **İkisinde de `:hover` yok**,
  oysa projenin idiomu `.wcn-row:hover` bir dosya ötede duruyor.
- **Karar — alt görev satırı da KUTU oldu.** Gerekçe: ikisi de kendi denetimlerini taşıyan etkileşimli NESNE
  (biri tik+başlık+durum+menü, diğeri tik+metin+seviye+ataç+taşı+sil). Kutu "bu bir şeydir" der, çizgi "bu bir
  metin satırıdır" der. Ayrıca kontrol listesinin gri dolgusu beyaza dönünce kutu, satırı ayırt eden **tek** şey
  kaldı — alt görevler çizgi olarak bırakılsaydı iki liste bu turdan sonra daha FARKLI görünecekti.
- **Dolgu kartın kendi yüzeyi** (`--bs-card-bg`): `--bs-body-bg` beyaz kartın üstünde gri ölçülüyordu, yani her
  satır beyaz panelin içinde gri paneldi — içerikte olmayan bir iç içelik.
- **Tamamlanmış = devre dışı tonu** iki listede de; iptal edilmiş alt görev kendi daha derin solukluğunu korudu
  (iptal edilmiş iş, bitmiş iş değildir).
- **`:hover` + `:focus-within` birlikte:** ikincisi olmadan fare kullanıcısı nerede olduğunu görür, klavye
  kullanıcısı görmez. Satır içi düğmelerin kendi hover'ı ezilmiyor (ölçüldü: düğme zemini satırdan farklı).
- **Gelecek regresyon riski: 🟢.**

### BL-125 — ✅ KAPANDI (2026-08-14) — [TEKNİK BİLGİ → KAYNAK] Kart yeniden adlandırıldı, açıldı, koşullandı
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ölçüm (önce):** kart `<details>` içinde, açıkken **277px**, altı alan + iki düğme. (Brief 331px demişti;
  aradaki iki turun ayırıcı ve satır değişiklikleri sayıyı düşürmüş — güncel taban 277.)
- **Ad:** "Teknik bilgi" → **"Kaynak"**. Teknik alanlar çıkınca içinde teknik bir şey kalmıyor; kalan şey
  "bu iş nereden geldi". Ayrıca eski başlık kapıya "burası sana göre değil" yazıyordu.
- **Kat kaldırıldı:** üç satırlık kart bir tık maliyeti ekliyordu. Başlık artık diğer kartlarla aynı yapıda
  (`cardHead` + ikon).
- **Tek sütun tanım listesi:** özet kartının iki sütunlu golden ızgarası 337px'lik rayda her değeri sardırıyordu.
- **İki kip — alan ancak ayırt ettiğinde görünür** (head kartındaki kaynak izine uygulanan kuralın aynısı):
  | alan | kendi modülümüz | yabancı sağlayıcı |
  |---|---|---|
  | Kaynaktaki durumu | görünür | görünür |
  | Modül · nesne türü · kayıt kimliği | **gizli** | görünür (kimlik + kopyala) |
  | Kaynak sürümü · işlem derinliği | **kaldırıldı** | kaldırıldı |
- **Etiket:** "Kaynak durumu" → **"Kaynaktaki durumu"**. Sayfa aynı görev için iki kelime söylüyordu — head'de
  "Beklemede" (bizim `normalizedStatus`), burada "Planlandı" (kaynağın kendi kelimesi) — ve hangisinin kimin
  kelimesi olduğu yazmıyordu.
- **Sonuç:** kart **277 → 131px**, ray **806 → 660px**. Kendi modülümüz kipinde tek alan kalıyor.
- **Ölü işaretlendi, silinmedi:** `.wcn-tech*` CSS bloğu; `TechnicalDetailsLabel`, `TechVersionValue`,
  `DetailSourceVersion`, `DetailActionDepth`, `ActionDepthInline/Deeplink` anahtarları 7 dilde duruyor.
  `referenceField`, `previewField`, `technicalVersion` yardımcıları çağıransız kaldığı için kaldırıldı (ölçüldü).
- **Gelecek regresyon riski: 🟢.**

### BL-126 — ✅ KAPANDI (2026-08-14) — [MEVCUT AKSİYONLAR] "Nerede tamamlanır" teknik alan değil, aksiyon oldu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ölçüm:** `actionDepth` **tam olarak iki değer** alıyor — `ACTION_DEPTHS = ['inline', 'deeplink']`
  (`fixture-contract.js:13`). Yani "inline dışındaki her değer" tek bir değer demek; tahmin edilecek üçüncü
  vaka yok. Eski "İşlem derinliği" satırı neredeyse her görevde "Burada tamamlanır" yazıyordu.
- **Yapılan:** `deeplink` durumunda birincil (dolu) denetim **"{Modül}'de tamamla"** bağlantısına dönüşüyor,
  dış-bağlantı ikonuyla; altında "Bu iş burada tamamlanamaz; {Modül} modülünde bitirilir". Motorumuzun hâlâ
  geçerli aksiyonları (Bilgi bekle, Başkasına ata) ikincil kalıyor. **"Kartta tam olarak bir dolu düğme" kuralı
  korunuyor** — bu birincilin yerine geçiyor, yanına değil.
- **Kaynak kartındaki "Kaynak kaydını aç" düğmesi bu durumda çekiliyor** — aynı hedefe iki denetim, bu sayfanın
  sürekli temizlediği çiftlemedir.
- **⚠ BİR TUZAK ÖLÇÜLDÜ:** ilk yazımda koşul `item.actionDepth === 'deeplink'` idi ve **hiç çalışmadı** —
  sunum katmanı o alanı taşımıyor. Doğrusu resolver'ın çözdüğü `surface.surfaceMode === 'deeplink'`; tek model
  tüketiliyor, ikincisi türetilmiyor.
- **Gelecek regresyon riski: 🟢.**

### BL-129 — ✅ KAPANDI (2026-08-14) — [ALT GÖREV PANELLERİ] Açık bir panelin altından render çekiliyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Belirti:** "Detaylı görev ekle" ilk tıklamada açıyor, sonra hiçbir tıklamada açmıyor — sayfa yenilense bile.
- **TEŞHİS — iki panelin karşılaştırması sebebi verdi.** Aynı sayfada iki panel var, biri çalışıyordu:
  | | çalışan (hızlı düzenleme) | çalışmayan (detaylı oluşturma) |
  |---|---|---|
  | açılış sırası | `render() → show() → await` | `render() → show() → await → **render()**` |
  | await sonrası render | **yok** | **var** ← fark burada |
- **ÖLÇÜM (MutationObserver, iki gerçek tıklama):**
  - `t=83014` düğüm #2 oluştu — `showPanel` Offcanvas örneğini **buna** bağladı, `.show()` çağırdı
  - `t=83077` düğüm #3 oluştu — **+63ms**, tam olarak `assignablePeople` gidiş-dönüşü
  - son durum: düğüm #3, örnek **yok**, `show` **yok**
  İkinci `render()` `#wcnApp` altını değiştiriyor; Bootstrap örneği artık belgede olmayan düğüme bağlı kalıyor.
  "İlk tıklamada çalışıyor" görüntüsü, açılış animasyonunun silinmeden önceki o 63 ms'si.
- **DAHA KÖTÜ YARISI — kayıt yollarını ölçünce çıktı:** iki panel de meşgul durumu ve hata dalında `render()`
  çağırıyordu. Düğüm değişince `hidden.bs.offcanvas` hiç ateşlenmiyor, dolayısıyla Bootstrap
  **`body { overflow: hidden }`'ı geri almıyor**. Canlı ölçüldü: başarısız oluşturmadan sonra panel görünmez,
  örnek yok, backdrop gitmiş — **ve sayfa kaydırılamıyor.** Kullanıcı sıkışıyor.
- **DÜZELTME (üç parça):**
  1. Panel **hemen** açılıyor; arama sonucu `render()` yerine **canlı `<select>`'e yazılıyor** (`fillAssigneeSelect`).
  2. Meşgul durumu **düğmeye yerinde** uygulanıyor (`setPanelBusy`), render ile değil.
  3. Kapanış **Bootstrap'in `hide()`'ı üzerinden** (`hidePanel`); `hidden` olayı state'i temizliyor ve TEK
     render'ı o yapıyor — body kilidini uygulayan kütüphane geri alıyor.
- **⚠ İLK DÜZELTMEM YANLIŞTI ve testler yakaladı:** aramayı panelden ÖNCE await etmiştim. O zaman yavaş/hatalı
  bir kişi servisi paneli hiç açtırmıyor — üç mevcut test kırmızıya döndü (arama stub'lanmamış, yani tam da
  "servis yanıt vermiyor" vakası). Panelin açılışı hiçbir uzak çağrıya bağlanmamalı.
- **CANLI DOĞRULAMA:** AÇ → KAPAT → TEKRAR AÇ → TEKRAR KAPAT → ÜÇÜNCÜ AÇ, üç kez (yenilemeden önce, arada yazma
  işlemi yaptıktan sonra, ve sayfa yenilendikten sonra). Her açılışta örnek var, backdrop 1, atanan listesi
  5 seçenekli; her kapanışta backdrop 0 ve body serbest.
- **Enter kanıtı tamamlandı** (geçen turun borcu): `#wcnNewSubtaskTitle`'a yazıp gerçek `Enter` → alt görev
  oluştu (19→20), listede göründü, panel temiz kapandı, bildirim: "'CT son kanit Enter' alt görevi eklendi."
- **Gelecek regresyon riski: 🟢** — üç kural da testle kilitli.

### BL-131 — ✅ KAPANDI (2026-08-14) — [KOMPOZİSYON] Ray erken bitiyordu; yapışkan hâle geldi
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ölçüm (1440×900):** içerik sütunu 1860px'e kadar sürüyor, ray 925px'te bitiyor → **936px** boyunca sağda boş
  sütun var ve sayfanın var olma sebebi olan "Mevcut aksiyonlar" kartı ekran dışında.
  ⚠ Brief 766px demişti; geçen turun test alt görevleri (17→20) içerik sütununu uzatmış, olgu aynı, sayı büyümüş.
- **Üç yol ölçüldü, biri seçildi:**
  | yol | ölçüm | karar |
  |---|---|---|
  | (a) yapışkan ray | ray 660px, gereken görüntü alanı 676px | **seçildi** |
  | (b) kontrol listesi raya | ray 1273 ↔ içerik 983 | dengesizlik ters dönüyor |
  | (c) içerikte iki sütun | her sütun 427px; alt görev başlığı bugün 626px | sıkışık |
- **Uygulama:** `@media (min-width: 992px)` içinde `position: sticky` + `inset-block-start: 5rem` (64px sabit
  navbar + 16px) + `align-self: start` (esnemiş bir ızgara öğesinin yapışacak yeri olmaz) +
  `max-block-size: calc(100vh - 6rem)` + `overflow-y: auto`.
- **Kırpmak yerine bozuluyor:** 676px'ten kısa bir pencerede ray kendi içinde kayıyor; sabit yükseklikli bir
  yapışkan sütun alt kartlarını ulaşılamaz kılardı — ki yapışkanlığın bütün amacı aksiyonların erişilebilir
  kalması.
- **992'nin altında kapalı** (ölçüldü: 900'de `position: static`) — yığılmış düzende ray içeriğin altındadır ve
  yapışacak anlamlı bir şey yoktur.
- **Doğrulama:** yapışkanlığı kıran tek şey ata zincirinde `overflow ≠ visible`'dır; **zincir temiz** ölçüldü,
  kapsayıcı blok 1722px, rayın yapışabileceği mesafe **1062px**. Derin kaydırmalı görsel doğrulama bu ortamda
  yapılamadı (tarayıcı paneli gizliyken gerçek tekerlek zaman aşımına uğruyor, programatik `scrollTop`
  reddediliyor — BL-098 ile aynı sınırlama).
- **Gelecek regresyon riski: 🟢.**

### BL-134 — ✅ KAPANDI (2026-08-14) — [ALT GÖREV PANELİ] Zorunlu son tarih işaretsizdi, hata gerçeği gizliyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Kural önce sorgulandı, sonra yıldız kondu.** Ana görev oluşturma ucu da son tarihsiz isteği reddediyor
  (ölçüldü: `400 VALIDATION_REQUEST_DUE_AT_NOT_NULL`, "A due date is required.") ve `_Form.cshtml` alanı zaten
  kırmızı yıldızla işaretliyor. Kural ürünün; tutarsız olan tek yüzey alt görev paneliydi.
- **İki düzeltme:** panelin son tarih etiketine `*`; ve `VALIDATION_REQUEST_DUE_AT_NOT_NULL` →
  `errorDueDateRequired` eşlemesi `REASON_CODE_MESSAGE_KEYS`'e eklendi (köprü zaten vardı ve eşlenmemiş kodlar
  için konsola uyarı bile veriyordu — kimse bu kodu eşlememişti).
- **Köprünün ikinci ucu da bağlandı:** `_IndexL10n.cshtml`'e anahtar eklendi — **bunu iki mevcut guard testi
  yakaladı**, ben eklemeyi unutmuştum. 7 dil.
- **Gelecek regresyon riski: 🟢.**

### BL-136 — ✅ KAPANDI (2026-08-14) — [DAR EKRAN] Yapışkan aksiyon şeridi (<992px)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ölçüm:** 900px'te "Mevcut aksiyonlar" kartının üstü sayfanın **1876. pikselinde** (sayfa 2597) → 2.08 ekran
  kaydırma. ≥992'de ray yapışkan ve aksiyonlar hep görünür, orada sorun yok.
- **İŞ 0 — desen taraması:** projede **alta yapışan aksiyon çubuğu deseni YOK**. Konumlandırma mekanizması var:
  Bootstrap'in kendi `.sticky-bottom` yardımcısı (`position:sticky; bottom:0; z-index:1020`), üründe bir yerde
  kullanılıyor (`GoldCreate.cshtml` → `goal-readiness-panel`, bir hazırlık paneli).
  **Mekanizma yeniden kullanıldı, kompozisyon YENİ — projeye yeni bir desen eklendi.**
- **Kapsam:** yalnız `<992px` (`d-lg-none`: `display:none` öğeyi yerleşimden, erişilebilirlik ağacından ve sekme
  sırasından birlikte çıkarır). **Tek render çıktısı**, genişlik dalı yok, resize dinleyicisi yok.
- **Geniş ekran birebir aynı** (ölçüldü, 1440 ve 1200, iki tema): şerit `display:none`, ray sticky, aksiyon kartı
  263/303px, içerik 870/710, ray 435/355, sayfa 1921px, ayırıcı 0/0, tek dolu düğme, kart ritmi 16px.
- **TEK KAYNAK:** `actionTiers(item)` çıkarıldı; hem kart hem şerit ondan okuyor. Canlı: ikisi de
  `accept · cancel · inquire · plan · reassign`. Açıklanamayan devre dışı aksiyonun filtresi de oraya taşındı,
  yani iki yüzeyden birine değil ikisine birden uygulanıyor.
- **z-index doğrulandı:** şerit 1020 < backdrop 1089 < offcanvas 1090 → açık panel şeridi örtüyor, tersi değil.
- **Kilit doğrulandı** (canlı): uçuşta şerit `wcn-actionbar-locked`, birincil "Uygulanıyor…" + `aria-busy`,
  "Diğer aksiyonlar" da devre dışı; sonra geri dönüyor.
- **Dört durum (900px):** olağan `98d1f94e` · kilit `98d1f94e` · engellenen `049e9109` (birincil "Tamamla"
  devre dışı + gerekçesi şeritte okunabilir) · kapanmış `ad7f9af3` (şerit YOK).
- **⚠ İlk denemem negatif kenar boşluğu kullandı ve yatay kaydırma yarattı** (şerit −8→893, viewport 900).
  Kaldırıldı; şerit içerikle hizalı. Bu, BL-121'de zaten yasakladığımız desendi.
- **Gelecek regresyon riski: 🟢.**

### BL-139 — ✅ KAPANDI (2026-08-14) — [KONTROL LİSTESİ] Kapak eklendi (BL-133 kapanışı)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- 8 maddenin üstünde `cappedList('checklist', …)` — alt görev listesi ve etkinlik akışıyla **aynı yardımcı**,
  aynı 320px kutu. Eşik gerekçesi: kontrol listesi satırı ve alt görev satırı ikisi de 38px, yani aynı kapak
  ikisinde aynı sayıda satır gösteriyor; üçüncü bir sayı seçmek yerine mevcut olanı kullandım.
- `aria-expanded` ve bölge etiketi yardımcıdan geliyor (diğer ikisinde zaten vardı).
- **Gelecek regresyon riski: 🟢.**

### BL-140 — ✅ KAPANDI (2026-08-14) — [KİŞİSEL NOT] Gereksiz tam sayfa yeniden çizimi kaldırıldı
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Not kaydetme `render()` çağırıyordu; oysa metin kutusu yazılanı zaten tutuyor ve sayfada notu gösteren başka
  yer yok — yani tüm detay sayfası hiçbir görünür değişiklik için yeniden çiziliyordu.
- **⚠ RAPORUN VARSAYIMI ÖLÇÜMDE ÇÜRÜDÜ:** "panel açıkken not kaydetmek paneli düşürüyor" deniyordu. Ölçüm: panel
  açıkken offcanvas backdrop'u tüm görüntü alanını kaplıyor (900×900; sayfa ortasında `elementFromPoint`
  backdrop döndürüyor) ve Bootstrap odağı panelin içinde hapsediyor — **gerçek bir okuyucu o düğmeye
  ulaşamıyor.** Geçen turun uyarısı programatik bir tıklamadan ateşlemişti.
- Render yine de kaldırıldı, çünkü zaten gereksizdi. Erteleme ve plan yazmaları render çağırmaya devam ediyor;
  ikisi de gerçekten görünür değişiklik üretiyor ve ikisi de panel açıkken erişilemez.
- **Gelecek regresyon riski: 🟢.**

### BL-152 — ✅ KAPANDI (2026-08-14) — [BL-148 kapanışı] Alt görev satırı bu kez DOM'da ölçüldü
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- BL-148 "alt görev satırının hizası yalnız kural listesinden doğrulandı" diyordu. Bu turda alt görevi OLAN bir
  göreve (049e9109, 5 alt görev) not eklenip **üç satır aynı sayfada** ölçüldü:
  | | zemin | kenarlık | yarıçap | dolgu | hiza |
  |---|---|---|---|---|---|
  | not | rgb(255,255,255) | 1px solid rgb(228,230,232) | 6px | 6px 8px | center |
  | kontrol listesi | aynı | aynı | aynı | aynı | aynı |
  | alt görev | aynı | aynı | aynı | aynı | aynı |
- Hover: üçü de `rgba(var(--bs-primary-rgb), .03)`, tarayıcının kural listesinden okundu.
- **⚠ BU TURDA BULUNAN GERÇEK SAPMA:** not satırı `--bs-body-bg` + `.4375rem` dolgu taşıyordu; diğer ikisi
  `--bs-card-bg` + `.375rem`. Yani "satır dili aynı" iddiası geçen tur DOĞRU DEĞİLDİ — yalnız `align-items`
  ölçülmüştü. `--bs-body-bg` tam da kontrol listesi kuralının kendi yorumunda uyardığı hata (beyaz kartın
  içinde gri panel). Düzeltildi ve dört özelliği birden karşılaştıran bir test yazıldı.
- **Gelecek regresyon riski: 🟢.**

### BL-163 — ✅ KAPANDI (2026-08-14) — BL-159 kararsız test: saat değil KOŞUL bekleniyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- `setTimeout(30)` yerine `until(() => calls.length > 0)` — koşul gerçekleşir gerçekleşmez dönüyor, 2sn tavanı var.
- Üç ardışık koşuda 208/208. Süre **artırılmadı**; sabit bekleme yalnız bir yerde kaldı ve orada doğru:
  "hiçbir şey olmadı" iddiasının bekleyecek bir koşulu yok, ve hatası yalnız yanlış-YEŞİL üretebilir, yanlış-kırmızı
  değil — yani BL-159'un gürültüsünü yaratamaz. Gerekçe testin içine yazıldı.
- **Gelecek regresyon riski: 🟢.**

### BL-173 — ✅ KAPANDI (2026-08-23) — BL-168 kararsız test `until(...)` desenine çevrildi
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- `openCreate` bir makro-görev bekliyordu; panel ise kişi aramasının `await`inden SONRA açılıyor, bu yüzden tam
  süit yükü altında test 5000ms'i boş yere bekliyordu.
- Süre **artırılmadı**: `until(() => panel var mı)` ve `until(() => created.length > 0)`. İki ardışık koşuda
  117/117.
- **Gelecek regresyon riski: 🟢.**

### BL-181 — ✅ KAPANDI (2026-08-24) — Erteleme artık gerçekten erteliyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Brifing, ertele diyaloğunun açıklamasının şunu demesini istedi: *"Bu görev, seçtiğin tarihe kadar gelen
  kutunda görünmez."* Aynı brifing "uydurma, ÖLÇ ve doğrula" dedi. **Ölçüldü ve doğru değil.**
- Cümlenin diğer üç iddiası doğrulandı ve sunucuda güvence altında (`SNOOZE_MUST_NOT_CREATE_WAITING`):
  yaşam döngüsü / normalleştirilmiş durum / bekleme bağlamı değişmiyor (`SetTaskSnoozeHandler` yalnız okuyucunun
  kendi katmanını yazıyor), son tarih bambaşka bir alan, talep eden bu katmana hiç ulaşmayan bir projeksiyon
  okuyor.
- **Dördüncü iddia yok:** `snoozedUntil` üzerinde süzen tek bir yer bile yok — ne sağlayıcıda, ne
  `activeItems()`'ta. Ertelenen görev listede durmaya devam ediyor, üstüne bir çip ve bir "Bu öğeyi ertelediniz"
  şeridi ekleniyor. Yani erteleme bugün **bir not**, bir gizleme değil.
- Bu yüzden diyaloğun cümlesi o iddiayı **yazmıyor**; ürünün yapmadığı bir şeyi anlatan bir diyalog, hiçbir şey
  söylemeyenden daha kötüdür. Bir gardiyan test (`wcn-snooze-dialog.test.js`) iddianın süzme gelmeden geri
  sızmasını engelliyor.
- **Karar senin:** (a) süzmeyi ekle — o zaman cümle tam hâline kavuşur; (b) ertelemeyi açıkça "kişisel bir not"
  olarak bırak ve adını buna göre gözden geçir.
- **Gelecek regresyon riski: 🟡** — süzme eklenirse "İşlerim boş" gibi sayaçlar ve boş durumlar da değişir.

### BL-184 — [KARARSIZ TEST] Tam süit yükü altında yedi WorkCenter testi zaman aşımına düşüyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Tam koşuda (`npx vitest run`, 92 dosya paralel) şu yedisi kırmızı: alt görev oluşturma ×3, "tümünü göster"
  kapağı, kontrol listesi seviyesi, havuz kuyruğu kovası, atanan seçici. Süreleri 7–11 saniye.
- **Yedisi de tek başına koşturulduğunda yeşil** (aynı komut, tek dosya: 356/356 · 71/71). Yani ürün kusuru
  değil, testlerin sabit bekleme kullanması: makine yüklüyken beklenen olay pencerenin dışına taşıyor.
- Çözüm biliniyor ve bu depoda üç kez uygulandı (BL-159 / BL-163 / BL-168): sabit `setTimeout` yerine
  `until(koşul, {timeout, step})`. Bu turda YAPILMADI — tur tek bir kusura ayrılmıştı.
- **Gelecek regresyon riski: 🟡** — kararsız testler gerçek kırmızıları gizler; bu turda bir gerçek kırmızıyı
  (ham diyalog sayacı 9→8) ayırt etmek fazladan üç koşu aldı.


**BL-184 GÜNCELLEME (2026-08-24, Tur C) — kararsızlık ÜREMEDİ**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- Tam süit **art arda üç kez** koşuldu: **10 kırmızı / 1602 yeşil**, üçünde de **birebir aynı** — hiçbir test
  koşudan koşuya değişmedi.
- Yani bu oturumda üç kez araya giren kararsızlık, **bu turdaki hâliyle üremiyor**. Sebebi kesin olarak
  ölçülemedi; en güçlü aday BL-189'du (aynı belgede iki modül örneği, paylaşılan dinleyiciler) ve o bu turda
  **düzeltildi** — kararsızlığın kaybolmuş olması muhtemelen onun yan etkisi, ama **kanıtlanmadı**.
- Madde kapanmıyor: üremeyen bir hata, olmayan bir hata değildir.
- **Gelecek regresyon riski: 🟡.**


**BL-184 güncelleme (2026-08-25) — KAPANDI: tekrarlamıyor**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- Tam süit **BEŞ KEZ ÜST ÜSTE** koşuldu. Sonuç, beşinde de **birebir aynı**:

  | Koşu | Geçen | Kalan |
  |---|---|---|
  | 1 | 1721 | 9 |
  | 2 | 1721 | 9 |
  | 3 | 1721 | 9 |
  | 4 | 1721 | 9 |
  | 5 | 1721 | 9 |

  Dokuz kırmızının hepsi dokunulmayan Enterprise Strategy testleri (oturum başından beri kırmızı, başka
  modülün).
- ⚠ **NEDEN-SONUÇ HÂLÂ KURULMUŞ DEĞİL.** En güçlü aday BL-189'du (harness'ın çift yüklemesi / dinleyici sızıntısı)
  ve düzeltildi; kararsızlık ondan sonra hiç görülmedi. Ama "düzeltildikten sonra görülmedi" ile "o yüzden
  oluyordu" aynı cümle değil — kayıt bu ayrımı koruyarak kapanıyor.
- Kapanış gerekçesi: beş ardışık özdeş koşu, tekrarlamayan bir kararsızlığı açık tutmak için yeterli kanıt
  değil. Yeniden görülürse bu not ve BL-189 düzeltmesi başlangıç noktasıdır.
- **Gelecek regresyon riski: 🟢**

### BL-188 — ✅ KAPANDI (2026-08-23) — Bayat okuma, yeni okumanın üstüne yazıyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Sıra: erteleme kaldırıldı (sunucu doğrulandı: `personal.snoozedUntil` yok) → ertele diyaloğu açıldı → geçmiş
  tarih reddedildi → "Vazgeç". Ekranda **kaldırılmış erteleme satırı geri geldi**; sayfa yenilenince gitti.
- Yani vazgeçme yolu, bir önceki anlık görüntüden yeniden çiziyor olabilir. Sunucu durumu her zaman doğruydu;
  yanlış olan tek şey ekrandı ve yalnız yenilemeye kadar sürdü.
- Bu turun konusu değildi, **düzeltilmedi**; tek bir gözlem olarak kaydediliyor, kovalanacaksa kendi turunu
  hak ediyor.
- **Gelecek regresyon riski: 🟡** — "kaydettim ama geri geldi" tipi şikâyetlerin klasik kaynağı.

### BL-189 — [ÖLÇÜM] Harness'ta iki modül örneği tek DOM'u paylaşıyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24 (Tur C).** `window.__wcnTeardown` canlı ölçüldü (`typeof` = function): boot önceki örneğin dinleyicilerini söküyor. Üretim davranışı, test uyarlaması değil.
- `wcn-boot` her boot'ta `app.js`'i yeniden yüklüyor; global modül nesneleri siliniyor ama **belge üzerindeki
  tıklama dinleyicileri** kalıyor. Sonuç: bir tıklama iki kez işleniyor, iki ağ okuması üretiyor.
- Bugün testleri yanıltmıyor (iddiaların hepsi DOM üzerinde), ama **yarış/sıra** iddialarını imkânsız kılıyor —
  BL-188'in davranış testi bu yüzden tarayıcıya taşındı.
- Çözüm yönü: app.js'in boot'ta kendi dinleyicilerini sökebilmesi ya da harness'ın her testi kendi
  `document`'ında koşturması. **Bu turda yapılmadı.**
- **Gelecek regresyon riski: 🟡** — zamanlamaya dayalı her yeni test aynı duvara toslar.


**BL-189 KAPANDI (2026-08-24, Tur C) — modül kendi dinleyicilerini söküyor**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- Bütün dinleyiciler `document` üzerinde, dolayısıyla ikinci bir boot birincinin ÜSTÜNE biniyordu: tek tık
  `onClick`'i **iki kez**, iki farklı `state` nesnesine karşı çalıştırıyordu.
- Boot'ta `global.__wcnTeardown` çağrılıyor; click/change/input/keydown sökülüyor ve sayaç durduruluyor.
- ⚠ **Bu bir test uyarlaması DEĞİL, üretim davranışı:** bundle'ı iki kez yükleyen ya da yeniden enjekte eden
  herhangi bir sayfa aynı çakışmayı yaşar. Testte önce görülmesi yan fayda.
- Testle kilitli: eklenen her boot dinleyicisinin bir `removeEventListener` karşılığı olmalı.

### BL-190 — ✅ KAPANDI (2026-08-23) — Satır içi stil bloğu backbone-custom.css'e taşındı
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- İkon bastırıldığında SweetAlert `.swal2-icon`'a zaten `style="display:none"` yazıyor. Buna rağmen 80px'lik
  boş kutu ekranda kalıyordu: `_GlobalConfirmation.cshtml`'in kendi satır-içi `<style>` bloğu
  `.swal2-icon { display: flex !important; … }` diyor ve kütüphanenin satır-içi `display:none`'ını yeniyor.
- Bu turda `backbone-custom.css`'e `:empty` koşullu bir kural eklenerek çözüldü (ikonu OLAN diyaloglar
  etkilenmedi — canlı doğrulandı: Rol İzinleri'nde kırmızı çöp kutusu 80px, yerinde).
- ⚠ Asıl mesele duruyor: **ortak bileşenin satır-içi `<style>` bloğu** FG-003'ün "CSS sınıflarla,
  backbone-custom.css'te" kuralının dışında ve kütüphanenin kendi davranışlarını `!important` ile eziyor.
  Taşınması ayrı bir tur; bu turda dokunulmadı.
- **Gelecek regresyon riski: 🟡.**

### BL-191 — ✅ KAPANDI (2026-08-23) — Tarih kutusu ürünün kontrolü oldu; 21→19px KABUL
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Sahibin bildirdiği "ikon yanlış" kusuru **glif değil, kutu** çıktı. Ölçüm, create formunun tarih alanıyla
  yan yana: ürün **38px** yükseklik / 15px / `--bs-border-radius`; kütüphanenin kutusu **44.6px** / 17px / 8px.
- Fark kozmetik değildi: `.diten-field-icon` kendini `calc(38px / 2)`'ye sabitliyor, çünkü bu üründe bir kontrol
  38px'tir. 45px'lik kutuda glif ortadan **20px yukarıda** duruyordu — alanın *içinde* değil, üst kenarında.
- Düzeltme, temanın kendi `.form-control` değerlerinden kopyalandı (core.css:2571 ve :8645), hiçbiri seçilmedi.
  Ayrıca sarmalayıcı alanı **kucaklıyor**: kütüphanenin dikey marjı (15px/3px) girdiden sarmalayıcıya taşındı,
  yoksa aradaki boşluk glifle kutu arasına düşüyordu.
- Ölçülen sonuç: 38px · 15px · 6px yarıçap · glif merkezde (0) ve x=15 — create formuyla **birebir**.
- ⚠ Bir yan etki bilerek bırakıldı: etiket→alan boşluğu 21px'ten **19px**'e indi (kutu 7px kısaldı). Alan→düğme
  zaten 19px'ti; ikisi artık eşit. Yeni sayı seçilmedi.
- **Gelecek regresyon riski: 🟢.**

### BL-192 — ✅ KAPANDI (2026-08-23, CT kararı) — "Erişilemez ama doğru"
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Sahibin isteği üzerine ret mesajı artık ürünün **alert** dilinde: `--bs-danger-bg-subtle` / `-text-emphasis` /
  `-border-subtle`, tema yarıçapı, 13px. Kütüphanenin #f0f0f0, 16px, köşesiz şeridi gitti. İki temada da
  doğrulandı ve başka bir ekranda (Rol İzinleri "Value is required") aynı dili konuşuyor.
- ⚠ Ama **arayüzden tetiklenemiyor**: takvim geçmiş günleri zaten devre dışı bırakıyor ve flatpickr
  `allowInput` olmadan bağlandığı için elle yazılan tarih geri alınıyor. Ölçüm bu yüzden değeri programatik
  atayarak yapıldı — açıkça yazılıyor.
- Yani istemci kontrolü bugün **ikinci savunma hattı**; birinci hat takvimin kendisi, üçüncüsü sunucunun 400'ü.
- **Karar senin:** `allowInput: true` verilip elle yazma açılsın mı (o zaman ret mesajı gerçekten görünür bir
  yüzey olur), yoksa yazma kapalı mı kalsın?
- **Gelecek regresyon riski: 🟢.**

### BL-193 — ✅ KAPANDI (2026-08-23) — Sarmalayıcı, kütüphanenin girdisini kaybettiriyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Bir önceki tur bir kusur getirdi ve commit alınmadığı için üretime gitmedi.** Takvim ikonunu koymak için
  girdiyi `.diten-field` ile sarmıştım. SweetAlert girdisini popup'ın **doğrudan çocukları** arasındaki sabit
  slot listesinden bulur; sarmalayıcı kutuyu **torun** yaptı.
- **Tek sarmalayıcı, üç yara** (üçü de canlı ölçüldü): `Swal.getInput()` → `null` · doğrulayıcıya `''` geliyor,
  bu yüzden **ileri** bir tarih "geçmiş tarih seçilemez" ile reddediliyordu · otomatik odak ve Enter ölü.
  Kullanıcının gördüğü yalnız birincisiydi.
- **Düzeltme (a şıkkı):** sarmalayıcı gitti, glif **kutunun arka planına** boyandı — SVG ürünün kendi
  `.bx-calendar` varlığından (`vendor/fonts/iconify-icons.css:3690`) kopyalandı. Ek eleman yok, kütüphaneyle
  dövüş yok. Kutunun tamamı zaten takvimi açıyor.
- **(b) seçilmedi:** popup'ın doğrudan çocuğu olan mutlak konumlu ikon, dikey yerini üstündeki içerikten alır;
  başlık/açıklama uzunluğu değişince kayar. **(c) reddedildi:** yaraların yalnız birini kapatıyordu.
- **Bedeli açıkça yazılıyor:** `background-image` `currentColor` miras alamıyor, bu yüzden glifin grisi iki
  temada iki ayrı kuralda yazılı (`#a7acb2` / `#7e7f96`). İkisi de `--bs-secondary-color`'ın ölçülmüş değeri;
  token değişirse bu iki URI elle güncellenmeli. Testte ikisi de kilitli.
- **Geçen turun ölçümleri korundu ve yeniden ölçüldü:** kutu 38px · yazı 15px · yarıçap 6px · metin 39px'ten ·
  glif 16px, x=15, dikey merkezde · placeholder duruyor — create formuyla birebir.
- **Gelecek regresyon riski: 🟢** — "girdi popup'ın doğrudan çocuğudur" iki testte kilitli.

### BL-196 — ✅ KAPANDI (2026-08-24) — Ertele diyaloğunun sorusu alanın kendi metni oldu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Sahip kararı:** "Hangi tarihe kadar" artık üstteki ayrı etiket satırı değil, **tarih alanının placeholder'ı**.
  Diyalog tek bir soru soruyor ve onu bir kez soruyor; boş bir kutunun üstünde duran etiket, kutunun kendisinin
  söyleyebileceği şeyi bir satır harcayarak söylüyordu.
- **İkisi birden yapılmadı:** aynı cümle hem etikette hem placeholder'da olsaydı alt alta iki kez yazardı.
  Kelimeler değişmedi, yedi dilde aynı `SnoozeUntilLabel` anahtarından geliyor — yalnız **nerede çizildiği**
  değişti.
- **Bedeli açıkça:** biçim ipucu (`YYYY-MM-DD`) artık görünmüyor. Alanı KULLANMAK için gerekmiyor (kutuyu takvim
  dolduruyor, elle yazılmıyor), ama şekli seçmeden önce bilmek isteyen okuyucu artık göremiyor.
  `SnoozeDatePlaceholder` yedi dilde **duruyor** — serbest yazım açılırsa (BL-192) geri gelecek metin bu; yedi
  dili yeniden çevirmek yerine kayıtlı bırakıldı, testle de kilitli.
- **Canlı doğrulandı (gerçek tıklama, tam döngü):** aç → takvimden 28 Ağustos → "Ertele"ye BAS → diyalog kapandı
  → çip ve şerit göründü → sayfa yenile → "Ertelendi 2026-08-28 Kaldır" duruyor. Dört kombinasyonda
  (1440/900 × aydınlık/karanlık) kutu 38px · yazı 15px · yarıçap 6px · glif 15px/merkez · metin 39px'ten ·
  girdi popup'ın doğrudan çocuğu · odak girdide · `getInput()` dolu.
- **Gelecek regresyon riski: 🟢.**

### BL-199 — [DÜZELTİLDİ] Gardiyan testte sayı vardı, kural değil
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- `wcn-snooze-dialog.test.js` `isSnoozed(item)` çağrılarını **dörde** sabitlemişti. BL-181 üç meşru çağıran
  ekleyince doğru bir değişiklik kırmızıya döndü — orchestrator demir kural #10'un "kayıtta sayı yerine ölçüm"
  uyarısının test hâli.
- Sayı, kuralın kendisiyle değiştirildi: "bu soruyu tek bir yüklem cevaplar" → karşılaştırmanın ikinci bir
  kopyası olmadığı iddia ediliyor, çağrı sayısı değil.
- **Gelecek regresyon riski: 🟢.**

### BL-200 — ✅ KAPANDI (2026-08-24) — Havada duran üç metin ürünün kendi kutu diline girdi
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

Sahip üçünü aynı oturumda gösterdi; üçü tek hastalıktı ve tek kararla kapandı: **bir şeye ait olan cümle,
o şey için ürünün zaten sahip olduğu kutunun içine girer.** Hiçbiri için yeni tasarım yapılmadı.

**A1 — Yasak cümlesi.** Ölçüldü: alt görev engeli `alert alert-warning` (`wcn-subtask-gate`), aynı türden
cümleyi taşıyan rail gerekçesi ise **zeminsiz, kenarlıksız, dolgusuz** — yalnız `color: var(--bs-warning)`.
Aynı yasak, iki muamele. Rail gerekçesi aynı alert'e geçti; `.wcn-act-reason` artık yalnız `.wcn-subtask-gate`'in
yaptığını yapıyor (dolgu `.625rem .875rem`, 13px), renk/kenarlık/yarıçap temanın.
`.wcn-act-reason` kullanıcısı **bir taneydi** (rail). Ama **kardeşi** `.wcn-actionbar-reason` (dar ekran şeridi)
birebir aynı çıplak muameleyi taşıyordu — bu oturumda üç kez tekrarlanan "birini düzeltip kardeşini bırakma"
hatasına düşmemek için o da aynı anda düzeltildi. **Canlı yan yana kanıt:** rail gerekçesi ile alt görev engeli
tek karede, altı CSS özelliğinde birebir aynı (zemin · kenarlık · yarıçap · dolgu · punto · renk).
Ray dar: kutu 1440 ve 900'de raya **sığıyor**, metin sarıyor ama **düğmeleri itmiyor**.

**A2 — Onayın nesnesi.** Ölçüm: kaydın adını söylemenin **iki mekanizması** vardı — `entityName` rozeti
(**altı dosyada on çağrı**) ve cümlenin içine tırnakla gömülü başlık (**yalnız WorkCenterNext**). İkisi aynı
anda hiç kullanılmıyordu, ama iki mekanizma tek iş demekti. **Rozet seçildi**: zaten var, ürünün çoğunluğu onu
konuşuyor ve zaten istenen çerçeveli kutu (yüzey · yarıçap · dolgu temadan). Cümle karşılığında tırnaklı
başlığını bıraktı: `ConfirmBody` artık `{0}` taşımıyor, `ConfirmBodyOnBehalf` yalnız vekâlet edeni taşıyor —
**yedi dilde** güncellendi. Açıklama satırı (gri nesir) düz kaldı; kutuya giren, eylemin nesnesi.
⚠ Ortak bileşene **beşinci** değişiklik; geriye uyum yine ölçüldü (15 çağrı / 12 dosya, üç ekran canlı).

**A5 — Boş alt görev kartı.** Ölçüldü: alt görev yokken kart **başlığıyla birlikte kayboluyor**, yerine tek
satırlık `wcn-empty-line` geliyordu. **Ürünün kendi boş-durum dili bulundu ve kullanıldı**: yan kart (Kontrol
Listesi) `cardHead`'ini koruyor, ekleme satırını yerinde bırakıyor ve "henüz yok" cümlesini
`.wcn-block-hint` olarak yazıyor — o ipucu zaten **bu kartın** ekleme satırının altında da var
(`subtaskInheritHint`). Yani yeni bir şey çizilmedi, iki mevcut satır kendi sırasına kondu.
**Alert yok** (sahip açıkça istedi). Cümle ekleme satırının **altında**, yani silinen satırın olacağı yerde.
İlerleme çubuğu ve "N tamam" okuması boşken **çizilmiyor**: 0/0 bir ölçüm değil.
**Yükseklik ölçüldü:** boş **163px** → bir alt görev eklendiğinde **255px** (aynı kart, gerçek yazımla).

**Canlı doğrulama:** devredilemez görevde gerekçe kutusu · "Görevi iptal et" diyaloğunda çerçeveli rozet
("Yeni maliyet merkezi açılış talebi") · alt görevi olmayan görevde başlık + ekleme satırı + altında cümle.
**Mutasyon (3/3 kırmızı):** çıplak `<p>` · rozetsiz gövde · başlıksız boş kart.
**İki genişlik × iki tema:** 1440/900 × aydınlık/karanlık — dördünde de aynı.
- ⚠ **YAZILI BİR KARAR TERS ÇEVRİLDİ, açıkça:** eski tasarımı çivileyen iki test vardı ("tek satır, üstünde
  ekleme kutusu"). Sahibin kararıyla ikisi de yeniden yazıldı; testlerin içine hem eski şeklin ne olduğu hem de
  neden bırakıldığı yazıldı, sessizce silinmedi.
- **Gelecek regresyon riski: 🟢.**

### BL-201 — ✅ KAPANDI (2026-08-24, CT kararı) — Silme EKLENMEYECEK, iptal doğru mekanizma
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Sahip "veri silindiğinde de aynı yere düşsün" dedi. Boş durum `items.length === 0` olduğu anda çiziliyor,
  yani sebebi ne olursa olsun aynı — ama bugün **hiçbir yol** bir alt görevi listeden kaldırmıyor: satır menüsü
  yalnız **"Alt görevi iptal et"** sunuyor, iptal edilen satır listede kalıyor (aşağı sıralanıyor) ve API'de de
  silme ucu yok.
- Yani "sildim ve kart boşaldı" hâli bugün **yalnız hiç eklenmemiş** görevlerde oluşuyor. Boş kartın kendisi
  doğru; eksik olan silme eylemi.
- **Karar senin:** alt görev silme gerçekten gerekiyor mu, yoksa iptal yeterli mi?
- **Gelecek regresyon riski: 🟢.**

### BL-203 — [ÖLÇÜLDÜ, KAPANDI] Menü ucundaki N+1 gerçek ama bugün yavaş DEĞİL
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- KOD BULGUSU (geçerli), `GetTenantNavigationMenuQueryHandler.cs`:
  - satır 55-57: `foreach (var module in modules)` içinde `await _accessService.HasAccessAsync(...)`
  - satır 94-96: `foreach (var module in entitled)` içinde `await _pageRepository.GetByModuleAsync(...)`
  N modül için 2N gidiş-dönüş. Klasik N+1. Bu kısım doğru ve duruyor.
- ⚠ CT İKİ KEZ YANILDI, ikisi de burada yazılı dursun:
  1. İlk kayıtta `27.2s · 18.1s · 13.9s · 10.7s · 5.3s` yazdım. O ölçüm 8 çekirdekte
     **yük 58-100** iken, başka bir worktree'nin test süiti çalışırken alınmıştı.
  2. Sonra "yavaşlığın sebebi N+1" dedim ve BL-195'i (kaybolan sol menü) buna bağladım.
- TEMİZ ÖLÇÜM (2026-08-24, yük 1.4, aynı kiracı, aynı oturum, 5 çağrı):
  **0.01s · 0.04s · 0.02s · 0.08s · 0.03s — ortanca 30 ms.**
  Yani uç HIZLI. 27 saniye makinenin doymuşluğuydu, sorgunun değil.
- SONUÇ: N+1 bir **ölçekleme riski**, bir kusur değil. Modül sayısı büyürse doğrusal
  kötüleşir; bugün ölçülebilir bir etkisi yok. Düzeltmek istenirse iki döngü toplu
  okumaya çevrilebilir, ama ACİL DEĞİL ve bugün önceliklendirilmiyor.
- ⚠ **BL-195 İLE BAĞ GERİ ÇEKİLDİ.** Sol menünün kısalmasının sebebi bu uç değil.
  BL-195 yeniden açıklanmamış durumda; bir dahaki gözlemde yükü de ölçmek gerekiyor.
  (`22eeed97` commit mesajında bu bağ iddia edilmişti — geçersiz.)
- ALINAN DERS: süre ölçümü, makinenin yükü yazılmadan kayda geçmez. Bu oturumda
  ikinci kez kirli koşulda ölçüp sonuç çıkardım.
- **Gelecek regresyon riski: 🟡** — kod şekli kötü, etkisi bugün yok.

### BL-204 — ✅ KAPANDI (2026-08-24) — Gerekçe kutusu sütuna hapsolmuştu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm: `.wcn-actrail-secondary` saran bir flex satırı ve öğeleri **içerikleri kadar** (`flex: 0 1 auto`);
  alert de düğmeyle **aynı `<li>`** içinde. Sonuç: 371px'lik kartta **194px**'lik uyarı — girinti gibi okunuyor.
- **Düzeltme:** gerekçe taşıyan ikincil satır tüm satırı alıyor (`wcn-act-hasreason` → `flex: 1 0 100%`) ve
  **düğme de birlikte** genişliyor (`inline-size: 100%`). Gerekçesi olmayan aksiyonlar doğal genişliklerinde.
- **Düğme neden birlikte taşınıyor:** bu kart **aynı anda iki gerekçe** gösterebiliyor (canlı ölçüldü: Tamamla
  altında "Bir alt görev hâlâ açık", Başkasına ata altında "Bu görev devredilemez."). Alert'leri tek başına
  karta yaymak, hangi cümlenin hangi düğmeye ait olduğunu söyleyen tek şeyi — altında durmasını — bozardı.
- **Dar ekran kardeşi ölçüldü, hapsolma YOK:** `.wcn-actionbar` `display: block`, gerekçe zaten tam genişlik
  (853px'lik şeritte 821px). Dokunulmadı.
- Dört kombinasyonda doğrulandı (1440/900 × aydınlık/karanlık): gerekçeli satırlar liste genişliğinde
  (371px / 805px), gerekçesizler 95px ve 116px.
- **Gelecek regresyon riski: 🟢.**

### BL-206 kapanış notu (2026-08-24) — Düğmeler satırı paylaşır, cümle kendi eylemini söyler
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Kusurun kökü (ölçüldü):** alert, düğmeyle AYNI `<li>`'nin içindeydi. BL-201'de `<li>`'ye
  `flex: 1 0 100%` verilerek cümleye kart genişliği kazandırıldı — ve satır kaybedildi: sarma yapan bir flex
  sırasında tam genişlik bir `<li>`, diğer bütün düğmeleri kendi satırına iter. Sahibin sorusu tam bu:
  "Başkasına ata neden Bilgi bekle'nin yanında değil?"
- **Karar (sahip, 2026-08-24):** cümle `<li>`'den ÇIKAR. Düğmeler doğal genişlikte tek satırda
  (`flex: 0 1 auto` geri geldi), gerekçeler `<ul class="wcn-actrail-secondary">`'den SONRA tam genişlik.
  Yakınlığın taşıdığı eşleştirme iki yeni taşıyıcıya devredildi: cümle **aksiyonun adını söyler**
  (`ActionDisabledWithName`, 7 dil) ve düğme `aria-describedby` ile **kendi cümlesine bağlanır**.
- **Silinen:** `.wcn-actrail-secondary .wcn-act-hasreason` iki CSS kuralı + yorumu, `app.js`'teki
  `wcn-act-hasreason` sınıfı. Yanlış bir kararı doğru anlatan yorum da gitti.
- **Kimlik kararlı:** `wcn-actreason-{itemId}-{actionCode}` — sayaçtan/rastgeleden değil aksiyon kodundan
  türetiliyor, çünkü bu kart her yoklamada yeniden çiziliyor; sayaç tabanlı bir id `aria-describedby`'yi bir
  sonraki çizimde boşluğa düşürürdü. Testle kilitli (iki çizim, aynı id).
- **Canlı ölçüm (9bf6194e, 1440×900, koyu ve açık):** "Bilgi bekle" ve "Başkasına ata" üst kenarları
  **y=523 ve y=523 — aynı satır**. İki gerekçe kutusu da **371px = kartın iç genişliği**. Cümle:
  "Başkasına ata — Bu görev devredilemez." `aria-describedby` →
  `wcn-actreason-9bf6194e-…-reassign` → var olan `.wcn-act-reason`, doğru cümle. 900×900'da aynısı: y=1388
  / y=1388, kutular 805px. İkinci aday 869195b4 aynı sonucu verdi.
- **Birincil aksiyon (Tamamla) DEĞİŞMEDİ:** kendi katmanında tek başına, zaten tam genişlik, belirsizlik yok
  → adını söylemesine gerek yok, `aria-describedby` almadı. Canlı doğrulandı: "Bir alt görev hâlâ açık"
  hâlâ kendi `<li>`'sinin içinde, 371px.
- **Yıkıcı katman aynı yoldan geçiyor:** her iki tier de artık tek bir `actionRail()` üretiyor, `<ul>`
  markup'ı kodda tek yerde (testle kilitli). ⚠ **Ölçüldü: 62 fixture öğesinin hiçbirinde gerekçesi olan
  devre dışı bir yıkıcı aksiyon yok** — yani canlı vaka bulunamadı; muamele yapısal olarak uygulanmış ve
  testle korunuyor, ekranda gözlenemedi.
- **Dar şerit (`.wcn-actionbar`) ayrı kod yolu, DEĞİŞMEDİ:** `renderActionBar` yalnız **birincil** aksiyonun
  gerekçesini çiziyor (`wcn-actionbar-reason`), ikincil/yıkıcı olanlar gerekçesiz bir dropdown'a katlanıyor.
  Tek cümle, tek düğme → belirsizlik yok, ad gerekmiyor. 900×900'de ölçüldü: 821px, tam genişlik.
- **Gelecek regresyon riski: 🟢** (yapı sadeleşti; iki tier tek üreticiye indi).

### BL-207 — [YAPILMADI] Aynı engel sayfada üç yerde birden yazıyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüldü (9bf6194e, 1440×900): (1) sayfa üstü kırmızı şerit "1 alt görev kapanmadan tamamlanamaz — Alt
  görevlere git", (2) aksiyon kartında "Bir alt görev hâlâ açık", (3) alt görev kartında sarı kutuda aynı
  engel. **Üçü de yanlış değil, ama üçü birden fazla.**
- Ölçülecek: üçü aynı kaynaktan mı geliyor (`disabledReasonCode` / `gates` / `wcn-subtask-gate`), hangisi
  hangi soruyu cevaplıyor (— "bu sayfada bir sorun var" / "bu düğme neden çalışmıyor" / "hangi alt görev"),
  hangisi silinebilir?
- Bu turda **kasıtlı olarak dokunulmadı** — sahibin kararı alınmadan bir uyarı silmek, üç kez söylemekten
  daha kötü olabilir.
- **Gelecek regresyon riski: 🟢.**


**BL-207 güncelleme (2026-08-25) — KAPANDI: alt görev engeli afişten düştü, diğerleri kaldı**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- **ENGEL TÜRÜ SAYIMI (ölçüldü, varsayılmadı).** Sağlayıcı `ResolveBlockers` ile tam **iki** kod üretiyor.
  Kontrol listesi, onay ve inceleme `blockedState`'e **hiç ulaşmıyor** — onlar `Gates` ve aksiyonun kendi
  `disabledReason`'ı üzerinden konuşuyor. Yani afişin baştan beri iki ailesi vardı:

  | Engel kodu | Afiş çiziyordu | Sayfada kendi kartı | Kart engeli adıyla + düzeltmesiyle gösteriyor mu | Karar |
  |---|---|---|---|---|
  | `SUBTASK_BLOCKED` | evet | ALT GÖREVLER | **evet** — çocuk adıyla, tik kutusuyla, kendi sarı satırıyla | **afişten DÜŞ** |
  | `DEPENDENCY_BLOCKED` | evet | BAĞIMLILIKLAR | **hayır** — ilişkiyi ve karşı görevin durumunu gösterir, ama "şu an hangi eylemi durdurduğunu" söylemez (`BlockedAffects*` yalnız afişte var) | **afişte KAL** |
  | `CHECKLIST_INCOMPLETE` · `APPROVAL_PENDING` · `REVIEW_PENDING` | **hayır, hiç** | — | — | afişi ilgilendirmiyor |

- Uygulama tek bir kümeye asıldı (`BANNER_SUPPRESSED_CODES`), tek bir kodun özel durumuna değil: ileride kendi
  kartını kazanan aile aynı yoldan düşer, kazanmayan cümlesini burada tutar. **Afiş silinmedi.**
- Karışık kümede yalnızca bastırılan kod düşüyor, kalanı tam liste olarak çiziliyor (test ediyor).
- Boş afiş kutusu bırakılmıyor: geriye engel kalmazsa hiç çizilmiyor.
- Ölü kalan her şey gitti: `allSubtasks` tek-satır dalı, `data-wcn-goto-subtasks` tıklama işleyicisi, CSS'i
  (`wcn-blocked-oneline` · `wcn-blocked-goto`) ve 7 dilden `BlockedSubtaskOneLine` + `BlockedGoToSubtasks`.
- ⚠ **ALTI TEST ESKİ KARARI YAZIYA DÖKMÜŞTÜ** ve kırmızıya döndü. Silinmediler — yeni kuralı ölçecek şekilde
  değiştirildiler, her birinde kararın nasıl iki kez değiştiği yazılı. Sayfanın **söylememesi gereken** şey de
  en az söylemesi gereken kadar kuraldır.
- ⚠ **BL-104 BU KARARLA CEVAPLANDI**, açıkta bırakılmadı: şikâyeti, toplanan satırın tek engeli artık ADIYLA
  anmamasıydı. İsim afişe hiç ihtiyaç duymuyordu — alt görev kartı onu, temizleyen tik kutusunun yanında
  zaten taşıyor.
- Canlı ölçüm (`848a624f`, 900px ve 1440px, iki tema): afiş yok · düğmenin gerekçesi yerinde · alt görev
  kartı adıyla ve kutusuyla yerinde.
- **Gelecek regresyon riski: 🟢**

### BL-208 — [YAPILMADI] Dar şeritteki dropdown'da devre dışı aksiyon sebebini söylemiyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüldü (900×900, 9bf6194e): `.wcn-actionbar` dropdown'ında "Başkasına ata" **disabled** ama yanında hiçbir
  cümle yok. Kartta aynı düğme "Bu görev devredilemez." diyor; şeritte sessiz.
- BL-206'nın kapsamı karttı; şerit ayrı bir kod yolu (`renderActionBar`) ve bu turda değiştirilmedi.
- **Gelecek regresyon riski: 🟢** (katkısal düzeltme).


**BL-208 güncelleme (2026-08-25) — KAPANDI: menüdeki devre dışı madde gerekçesini söylüyor**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- **SEBEP, tahmin edilenlerden hiçbiri değildi.** Gerekçe "geçilmiyor" da değildi, "çizilip gizleniyor" da:
  yapışkan şeridin menüsü `actionMenuLi`'yi **hiç çağırmıyordu**. Kendi `<li><button class="dropdown-item">`
  şablonunu elle yazıyordu ve o şablon yalnızca ETİKET taşıyordu. Yani gizlenecek bir gerekçe yoktu —
  gerekçe kavramından hiç haberi olmayan **ikinci bir render yolu** vardı. `wcn-menu-reason` bu sırada
  uygulamadaki diğer BÜTÜN menülerde doğru çalışıyordu.
- ⚠ Bu, BL-243'ün ve oturumdaki diğer ikisinin aynısı: **bir yüzeyi düzeltip ikizini unutmak.** O yüzden
  düzeltme "şeride gerekçe eklemek" değil, şeridi paylaşılan satıra bağlamak oldu — menü satırlarına bundan
  sonra eklenecek şey ikinci kez hatırlanmayı beklemiyor.
- Kilit iki farklı türetimle hesaplanıyordu (`state.submittingItemId === item.id` ile
  `interactionLocked = !!submittingActionCode`). Paylaşılan satır artık kilidi isteğe bağlı olarak **dışarıdan
  alıyor**; şerit kendi cevabını geçiyor, mevcut çağıranların davranışı değişmedi.
- Erişilebilirlik: menü satırı kendi gerekçesine `aria-describedby` ile bağlandı. Id `-menu` ekiyle ayrıldı —
  detay sayfası aynı aksiyonu **üç kez** çizebiliyor (kart rayı · şeridin ana düğmesi `-bar` · bu menü).
  Canlı ölçüm (`848a624f`): dört gerekçe, dört tekil id, çift yok.
- ⚠ **KIRPILMIYORDU AMA OKUNMUYORDU.** `white-space: normal` zaten vardı, `max-inline-size: 15rem` bir TAVAN.
  Ölçüm: cümleye 97px düşüyordu — etiketlerin ihtiyaç duyduğu genişlik neyse o — ve "Bu görev devredilemez."
  iki-üç kelimelik bir sütuna sarıyordu. Kırpık değildi, sadece okunmuyordu; tavan bunu göremezdi. `13rem`
  **taban** eklendi: menü içeriğine göre büyüdüğü için içeride kırpmak yerine menüyü genişletiyor.
  Sonuç ölçüldü: menü 274px, cümle tek satır, ekran dışına taşma yok (900px).
- **Gelecek regresyon riski: 🟢**

### BL-210 kapanış notu (2026-08-24) — Bağımlılık satırı kuralı söylüyor (A4, sahip C seçeneği)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ölçülen kusur (canlı, `bfcfa8ba`):** `ÖNCÜL · sasasa · FS · tamam` — dört parça yan yana, aralarında
  görünür ilişki yok; `FS`'in açılımı YALNIZ `title` tooltip'inde (dokunmatikte hiç yok); `tamam` öncülün
  durumu ama satırın sağ ucunda satırın kendi durumu gibi okunuyor.
- **Yapılan:** kompakt tek satır korundu. Yön **ok ikonu** oldu (sol = yukarıdan biri beni tutuyor,
  sağ = ben aşağıdakini tutuyorum); `ÖNCÜL`/`ARDIL` kelimeleri gitti. Tür **yarım cümle** olarak satırın
  içine yazıldı. Durum rozeti ve sözlüğü (`DEP_STATE_KEY` / `DEP_STATE_KIND`, `cancelled` dahil)
  **değişmedi**.
- **Anlam TÜRETİLDİ, uydurulmadı.** Kaynak: `DEPENDENCY_TYPES` (fixture-contract.js:76 = motorun
  `TaskDependencyType`'ı) + ürünün zaten yazdığı iki yer — `DepTypeFS` "Bitince başlar (FS)" ailesi ve
  `BlockerFinishToStart` "«{0}» kapanmadan başlanamaz" ailesi. İlk fiil öncülün ulaşması gereken nokta,
  ikinci fiil ardılın o zaman yapabileceği şey. Sekiz cümle bu tek kuralı iki uçtan okuyor; beşinci bir
  anlam üretilmedi.
- **`Blocker*` anahtarları YENİDEN KULLANILMADI**, bilerek: onlar edilgen, tırnaklı ve yalnız CANLI bir
  engeli anlatıyor. Bu satır ilişkinin kendisini anlatıyor (bitmiş bir öncülün de türü var) ve sahibin
  seçtiği ikinci tekil şahısla konuşuyor.
- **Kısaltma KALDI, rütbesi düştü** — cümleden SONRA, küçük ve soluk, `title`'ı hâlâ duruyor; artık `wcn-chip`
  değil, çünkü çip cümleyle eşit ağırlık iddia eder. Karar: bileni için en hızlı okuma, ama cümle onsuz da
  tam. Testle kilitli (kısaltma DOM'dan silinince satır hâlâ kuralı söylüyor).
- **Satır dili icat edilmedi:** `.diten-checkitem`'dan değer değer kopyalandı — `padding: .375rem .5rem`,
  `1px solid var(--bs-border-color)`, `border-radius: .375rem`, `background: var(--bs-card-bg)`,
  `align-items: center`. Canlı ölçüm: `6px 8px` / `1px rgb(228,230,232)` / `6px` / `rgb(255,255,255)` / center.
- **Ok SESSİZ (`aria-hidden="true"`)** — cümle yönü zaten kelimeyle söylüyor; kendini duyuran bir ikon yönü
  ekran okuyucuya iki kez okuturdu.
- **CANLI KAPSAM — SEKİZDEN KAÇI GÖRÜLDÜ:**
  - Gerçek backend verisiyle **2/8**: `pred/FS` (`bfcfa8ba` "sasasa", durum **tamam**; `38589f6a`, durum
    **başlamadı**) ve `succ/FS` (`95312464`). Ölçüldü: 62 öğenin 3'ünde bağımlılık var, **üçü de FS**.
  - Kalan **6/8** için **FIXTURE EKLENDİ** ve bu yazıldı: `islerim-showcase-fixtures.js` içindeki
    `ISLERIM-WORK-ACTIVE` ("max veri" showcase görevi) bağımlılık listesi 2'den 8'e çıkarıldı. Mevcut iki
    satır **değiştirilmedi**, yeni dize eklenmedi (başlıklar o görevin zaten kullandığı iki kaynak).
    `?fixtures=showcase` ile sekizi de canlı görüldü, sekiz farklı cümle.
- **DURUM × YÖN (sahibin 2. koşulu), canlı:** `pred/FS/done` → "sasasa bitmeden başlayamazsın" + yeşil
  **tamam**; `pred/FS/not-started` → "Anahtar kullanıcı eğitimi bitmeden başlayamazsın" + gri **başlamadı**.
  Fark rozetin renginde ve kelimesinde görünür; cümle ikisinde de aynı kuralı söylüyor — çünkü kural durumla
  değişmiyor, yalnız o kuralın şu an ısırıp ısırmadığı değişiyor.
- **İki genişlik × iki tema:** 1440 ve 900'de sekiz satır da 35px, yatay taşma **yok** (satır `scrollWidth -
  clientWidth = 0`, sayfa `0`). Koyu temada satır yüzeyi kart yüzeyiyle aynı (rgb(43,44,64)), kutu kenarlıkla
  tanımlanıyor — `.diten-checkitem` ile aynı davranış.
- **UZUN BAŞLIK:** 121 karakterlik bir cümleyle ölçüldü (900px): satır 35px → **50px**, cümle **2 satıra
  sarıyor**, kırpma yok, yatay taşma yok. Karar: sarsın — kırpılmış bir kural, üzerine hareket edilemeyen
  bir kuraldır.
- **DEĞİŞMEYENLER (dokunulmadı, yazıldı):** kart **salt okunur**, "Bağımlılık ilişkisi kaynağında yönetilir"
  ipucu duruyor, düzenleme/Gantt/graf eklenmedi (testle kilitli: kartta 0 adet buton/input/link). Boş durum
  (`!dependencies.length` → kart hiç çizilmez) **bu turda değişmedi**; A5'te kararlaştırılan boş-durum dili
  ayrı bir turda gelecek.
- **Gelecek regresyon riski: 🟢** (katkısal; sözlükler ve kart sözleşmesi el değmedi).

### BL-211 — [YAPILMADI] Bağımlılık durum sözlüğünde büyük/küçük harf tutarsız
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** `DepDone` artık "Tamamlandı"; üçü de ürünün kendi durum sözlüğüyle aynı. Kayıt turlar arasında güncellenmemişti.
- Canlı ölçüldü (`ISLERIM-WORK-ACTIVE`, tr): rozetler **"tamam" · "devam" · "başlamadı"** küçük harfle
  başlarken `DepCancelled` **"İptal edildi"** büyük harfle ve tam cümle gibi.
- Sahibin bu turdaki talimatı sözlüğün **değişmemesiydi** (`DEP_STATE_KEY` / `DEP_STATE_KIND`, `cancelled`
  dahil) → **dokunulmadı**.
- Düzeltilecekse yedi dilde birden ve rozet ailesinin tamamına bakılarak yapılmalı.
- **Gelecek regresyon riski: 🟢.**

### BL-212 — [YAPILMADI] Engel afişindeki `FS` çipi hâlâ tek taşıyıcı
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** Afişteki kısaltma `wcn-dep-abbr` dipnotu olarak çiziliyor (canlı ölçüldü, ISLERIM-WORK-BLOCKED), kırmızı hap gitti. Kart ile afiş tek dil konuşuyor.
- `renderBlocked` satırları `<span class="wcn-chip wcn-chip-danger wcn-dep-type" title="…">FS</span>`
  kullanmayı sürdürüyor: kısaltmanın açılımı orada **hâlâ yalnız tooltip'te**.
- Orada cümle zaten var (`BlockerFinishToStart` ailesi), yani afiş bağımlılık satırı kadar kör değil — ama
  çip aynı tooltip-bağımlılığını taşıyor.
- A4'ün kapsamı **bağımlılık kartıydı**; afiş ayrı bir yüzey ve bu turda **değiştirilmedi**.
- **Gelecek regresyon riski: 🟢.**

### BL-213 kapanış notu (2026-08-24) — WorkCenter diyalogları tek dil konuşuyor (A3, sahip kararı (b))
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ölçülen fark (ham `Swal.fire` ↔ ertele diyaloğu):** başlık 38px↔18px · açıklama 18px↔13px · popup
  512px↔400px · vazgeç KIRMIZI↔nötr · ikon yok↔ay.
- **Sebep, dikkatsizlik değil yapıydı:** görünüm `showConfirm`'ün İÇİNDE bir `customClass` sabitiydi. Onay
  olamayan bir diyalog, görünümü de alamıyordu. Kırmızı vazgeç düğmesi temanın **küresel varsayılanı**
  (`btn-label-danger`); paketi alan onu eziyordu, almayan ezemiyordu.
- **Yapılan:** `window.DitenDialogAppearance(options)` — adlandırılmış, yayınlanmış, **tek tanım**. `showConfirm`
  artık onu OKUYOR; `confirmVariant` (düğme rengi) dışarıdan gelmiyor, `type`'tan türüyor.
- **Reddedilen (a):** ortak bileşene "özel alanlar" dikişi. Bu oturumda ona altı parametre eklendi; yedincisi
  form üreticisine çevirirdi.
- **`inputOptions` = YEDİNCİ ve SON parametre.** `select` seçenekleriyle gelmezse kullanılamaz bir kutudur —
  eksik bir parametrenin diğer yarısı, yeni bir yetenek değil. **Geriye uyum sayıldı: 15 çağrı / 12 dosya,
  hiçbiri `inputOptions` geçmiyor** (testle kilitli).
- **Dört diyalog TAŞINDI** (tek değer → onay): Planla · Toplantı zamanı · Süre gir · Modül seç.
  Tarih dikişi açılmadı — ertele diyaloğunun `inputType` + `onOpen` + `validate` yolu kullanıldı.
- **Dört diyalog PAKETİ ALDI** (onay değil / çok alanlı): "+ Yeni" menüsü · Yeni toplantı formu ·
  Aksiyon onayı (sahibin fotoğrafladığı) · Toplu ilerleme. Ham `Swal.fire` kaldılar ama artık `dialogLook()`
  yayıyorlar. Yeni toplantı formunun **yapısına dokunulmadı** (B3'te offcanvas olacak).
- **PLACEHOLDER SAYIMI:** sekiz diyalogda **9 kutu** var. Doldurulabilir 7'sinin **3'ünde placeholder VARDI**
  (`LogTimePlaceholder` "örn. 30", `ReasonPlaceholder`, select'in boş seçeneği `NewPickModule`), **4'üne
  EKLENDİ** (`DatePlaceholder`, `DateTimePlaceholder`, `MeetingTitlePlaceholder`,
  `MeetingLocationPlaceholder`). Kalan 2 kutu `type="time"` — native saat kutusu placeholder'ı **hiç
  göstermez**, değeri (09:00/09:30) zaten örnek; eklenmedi ve **eklenmediği yazıldı**.
- **ALAN İKONU — tek tek:**
  - tarih (Planla) → **takvim**, `.wcn-date-input`, sarmalayıcı YOK
  - tarih+saat (Toplantı zamanı) → **takvim**, aynı sınıf
  - süre (Süre gir) → **saat**, yeni `.wcn-time-input`, AYNI teknik (glif kutunun arka planında), SVG ürünün
    kendi `.bx-time-five`'ı, koyu tema ve RTL varyantlarıyla
  - select (Modül seç) → **ikon YOK**: temanın kendi oku zaten var, ikincisi iki ok olurdu
  - serbest metin (gerekçe, toplantı başlığı, konum) → **ikon YOK**: kutunun ne olduğunu kutu söylüyor
  - saat kutuları (başlangıç/bitiş) → native kontrolün kendi saat ikonu var, dokunulmadı
- **BL-205 kapandı:** `app.js:4578` ve `4708` → `PanelClose`. Canlı doğrulandı: panelin kapatma düğmesi
  **"Paneli kapat"** diyor. Dosyada `ReasonCancel` **sıfır** kez geçiyor.
- **BL-211 kapandı, İLK TEŞHİS DÜZELTİLEREK:** `DepCancelled` baştan doğruymuş; aykırı olan üçü.
  `DepDone`/`DepInProgress`/`DepNotStarted` artık **ürünün kendi sözlüğüyle birebir aynı** —
  `SubtaskStatusDone`/`InProgress`/`NotStarted` değerlerinden ÖLÇÜLEREK kopyalandı, uydurulmadı, 7 dil.
  (en "Done", tr "Tamamlandı", fr "Terminé", es "Completado", zh "已完成", ar "مكتملة", ru "Готово").
- **Gelecek regresyon riski: 🟢** (görünüm tek tanıma indi; sözleşme genişlemedi).

### BL-214 — [YAPILMADI] İki diyalog kullanıcı arayüzünden ULAŞILAMIYOR
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** Tur B'de silindi: `runBulk`/`bulkBar` sıfır eşleşme, `openNew` yalnız silinme gerekçesini anlatan bir yorum olarak duruyor.
- **"+ Yeni" Swal menüsü (`openNew`, app.js:7012):** dispatch onu yalnız `data-wcn-new` değeri
  task/note/meeting/source **olmayan** bir düğme için çağırıyor. Canlı ölçüldü: DOM'daki dört düğmenin
  dördü de bilinen kind taşıyor → **hiçbir tıklama buraya varmıyor**. Liste sayfasındaki "+ Yeni" artık bir
  Bootstrap dropdown'ı; bu Swal menüsünün yerini almış.
- **Toplu ilerleme (`runBulkWithProgress`, app.js:7448):** `data-wcn-check` işaretçisi kodda yalnız
  OKUNUYOR, hiçbir yerde ÇİZİLMİYOR (grep: 4 eşleşme, hepsi olay yakalayıcı). Seçim kutusu olmadan
  `state.tableSelected` boş kalıyor, toplu şerit hiç görünmüyor, diyalog hiç açılmıyor.
- Bu turda **ikisi de görünüm paketini aldı** (kaynak seviyesinde ve testle kilitli), ama görünümleri
  tıklanarak değil kendi kod yollarına girilerek ölçüldü — raporda böyle yazıldı.
- Ulaşılabilir kılmak için: `openNew` ya silinmeli ya dropdown'un yerine geri konmalı; toplu işlem için
  tablonun seçim sütunu geri gelmeli. **İkisi de ürün kararı, bu turun konusu değil.**
- **Gelecek regresyon riski: 🟡** — ölü kod, ama görünüm paketini aldığı için ileride yanlış bir "çalışıyor"
  izlenimi verebilir.

### BL-216 — [YAPILMADI] Referans diyaloğun kendi placeholder'ı, sahibin A-kuralını çiğniyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** Ertele kutusunun placeholder'ı `YYYY-AA-GG`; etiket ayrıca duruyor. Referans artık kendi kuralını çiğnemiyor.
- Sahibin (A) kuralı: placeholder GERÇEK BİR ÖRNEK olacak, alan adının tekrarı değil.
- Ölçüldü: **ertele diyaloğu** — bu turun REFERANSI — tarih kutusuna placeholder olarak
  `SnoozeUntilLabel` ("Hangi tarihe kadar") koyuyor; bu bir soru, örnek değil. `SnoozeDatePlaceholder`
  ("YYYY-AA-GG") resx'te duruyor ama **kullanılmıyor**.
- Ertele diyaloğu sekiz diyalogdan biri DEĞİLDİ, o yüzden bu turda **kasıtlı olarak dokunulmadı** —
  referansı tur ortasında değiştirmek, kıyaslamayı geçersiz kılardı.
- **Gelecek regresyon riski: 🟢.**

### BL-217 kapanış notu (2026-08-24) — Yedi kusur, üç kök sebep, iki ölü uç (A2)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **1·3·4·5 — ortalanmış etiketler.** Dördü de ortak bileşenin `inputLabel` yolundan geçiyordu; popup her şeyi
  ortaladığı için etiket alanın üstünde ortada duruyordu. **Tek satır** düzeltti:
  `inputLabel: 'form-label d-block w-100 text-start'`.
  ⚠ `w-100` süs değil, **ölçüldü**: `d-block text-start` ile etiket `display: block` hesaplanıyor ama yine
  58px genişlikte, kendi kutusunun **94px sağında** kalıyordu (etiket x=691, girdi x=597) — SweetAlert popup'ı
  GRID kuruyor ve otomatik genişlikli bir öğe kendi izinin ortasına düşüyor. `d-block w-100 text-*` üçlüsü
  paketteki BAŞLIĞIN zaten kullandığı üçlü; yeni bir şey icat edilmedi, var olan deyim tamamlandı.
  Canlı: etiket x=597, kutu x=597 — **aynı sol kenar**.
- **Başlık ve açıklama ORTADA KALDI**, kasıtlı: ikisi de DİYALOĞA sesleniyor, etiket ise altındaki KUTUYA.
  Ürünün her oluşturma formunda aynı düzen var. Testle kilitli.
- **Elle yazılan etiketler ayrışmadı:** "Bilgi bekle"nin iki etiketi zaten `form-label d-block text-start`
  taşıyordu; test artık DİYALOG içindeki elle yazılmış her etiketin `text-start` taşıdığını doğruluyor.
  ⚠ Offcanvas PANEL etiketleri (`form-label`, `text-start` yok) kapsam dışı bırakıldı ve bu yazıldı: panel
  zaten sola dayalı, orada `text-start` hiçbir şeyi değiştirmezdi.
- **6 — "Devam etmek istediğinize emin misiniz?"** Ortak bileşenin varsayılanıydı ve `options.subtext || default`
  ifadesi `''` değerini "çağıran bir şey söylemedi" sayıyordu. Artık `undefined` "söylenmedi", `''` ise
  "bilerek yok" demek. WorkCenter seam'i: `options.input` varsa (yani GİRDİ İSTEMİ ise) `''`, yoksa varsayılan.
  **GERÇEK ONAYLAR DOKUNULMADI** — canlı doğrulandı: `/RoleAssignments` silme onayı hâlâ
  "Devam etmek istediğinize emin misiniz?" diyor.
- **ONAY / İSTEM SAYIMI (15 çağrı, 12 dosya):** 14'ü ONAY (Tenants ×2, ReferenceData hierarchy + mappings,
  AuditLog redaksiyon, UserRoleAssignments, RoleAssignments, QmsBaselines details + designer ×3,
  ControlledDocuments, Instantiations, WorkCenter `confirmDestructive`, premium-modal aktarıcı) → **varsayılanı
  korudu**. 1'i WorkCenter seam'i (`sharedConfirm`) → istemleri sessizleştirdi, onaylarını korudu.
- **Üç yeni cümle, 7 dil:** `LogTimeSubtext` · `MeetingWhenSubtext` · `NewInSourceSubtext`. Her biri kutunun
  SORMADIĞI bir şeyi söylüyor (eklenir/sayacı başlatmaz · takvime yazar/son tarih değişmez · kayıt o modülde
  kalır).
- **2·6·7 — ikonlar.** Çember+glif `showConfirm`'ün İÇİNDE kuruluyordu, bu yüzden ham bir diyalog paketi alıp
  yine ikonsuz açılıyordu. `window.DitenDialogAppearance.iconHtml(type, glyph)` yayınlandı; `showConfirm` de
  onu okuyor (tek üretici, iki tüketici). **Yeni parametre AÇILMADI** — `options.icon` ertele turunda açılmıştı.
  - "Bilgi bekle"/"Başkasına ata" → **`bx-conversation`**. Gerekçe: bu diyalog işi bir KİŞİYE devrediyor ve
    nedenini yazıyor; ikisi de bir insana mesaj. Varsayılan `?` "emin misin?" diye soruyor — sorulan soru bu
    değil; kilit ya da uyarı ise olmayan bir yasak iddia ederdi. Çember ve rengi hâlâ `type`'ın (`info`).
  - "Hızlı not"un `?` ikonu **düzeltilmedi, diyalog silindi**.
- **4 — iki select artık select2.** Ürünün kendi kurulumu kullanıldı, yeni sarmalayıcı yazılmadı.
  ⚠ **Z-INDEX YAPISAL OLARAK ÇÖZÜLDÜ:** `dropdownParent` = POPUP. flatpickr takvimi bu oturumda 1074'te kalıp
  1090'lık diyaloğun ARKASINA düşmüştü; bir torun atasının arkasında kalamaz, yani soru bir sayıyla
  cevaplanmadı, ortadan kaldırıldı.
  **KANIT (canlı):** liste açıldı, bir seçeneğin merkezinde `document.elementFromPoint` çağrıldı → dönen eleman
  **o seçeneğin ta kendisi** (`select2-results__option | Diten Admin`). Sonra **gerçek tıklama**: `<select>`
  değeri `11111111-1111-1111-1111-111111111111`, kontrolde "Diten Admin" göründü. Modül seçicide de aynı:
  gerçek tıklama → `Swal.getInput().value === "Görevler"`.
  **`Swal.getInput()` HÂLÂ ÇALIŞIYOR** ve `.swal2-select` popup'ın DOĞRUDAN ÇOCUĞU: select2 orijinali yerinde
  gizleyip kendi kabını KARDEŞ olarak ekliyor, araya girmiyor.
- **⚠ SEAM'DE BULUNAN GERÇEK KUSUR (bir tur yaşamış olacaktı):** `didOpen` dikişi kutuyu
  `popup.querySelector('.swal2-input, .swal2-select, …')` ile buluyordu; `querySelector` seçici sırasına değil
  **BELGE SIRASINA** bakar ve SweetAlert bütün yuvalarını çizip kullanmadıklarını gizler — bu yüzden `select`
  diyaloğuna **gizli `.swal2-input`** veriliyordu ve modül seçici sessizce yerli kaldı. Artık kütüphanenin
  kendi cevabı soruluyor: `Swal.getInput()`.
- **⚠ SELECT2'NİN YUTTUĞU GERÇEK CÜMLE:** `placeholder: ''` geçmek select2'nin placeholder mekanizmasını
  AÇIYOR ve boş değerli ilk seçeneği placeholder sayıp hiç çizmiyor. "Kim bekleniyor?" seçicisinin ilk
  seçeneği bir placeholder değil, **gerçek bir cevap** ("Belirli bir kişi değil") — ve kutu boş açılıyordu.
  Artık placeholder anahtarı yalnız gerçekten varsa geçiliyor. Canlı doğrulandı.
- **⚠ ETİKET DÜZELTMESİNİN İKİNCİ YARISI (ölçülerek bulundu):** etiket sola gelince, kütüphanenin girdi
  yuvalarına verdiği `margin-inline: 34px` yüzünden etiket kutunun **34px soluna** düştü (etiket x=544,
  kutu x=578) — bu 34px ürünün hiçbir yerinde kullanılmıyor ve `.wcn-date-input`/`.wcn-time-input` onu zaten
  elle iptal ediyordu. Tek yerde iptal edildi: popup içindeki `.swal2-input/.swal2-select/.swal2-textarea`
  artık sütununu dolduruyor. **Her modülün onayını etkiler** — kutular 68px genişledi, hiçbir şey yer
  değiştirmedi; üç ekran öncesi/sonrası canlı ölçüldü. Canlı: etiket x=544, kutu x=544.
- **5 — iki ölü uç KALDIRILDI (devre dışı bırakılmadı).** `openQuickNote` → `state.notes.unshift(...)`,
  `openMeetingForm` → `state.meetings.push(...)`, ikisinde de API çağrısı YOK, `state` ikisini de `[]` ile
  başlatıp hiç yüklemiyor. Menüden iki madde, iki diyalog ve iki dispatch dalı silindi.
  ⚠ **AJANDA PANELİNİN "+" DÜĞMESİ DE GİTTİ** — bu turun şartnamesinde YOKTU ve burada söyleniyor: aynı silinen
  forma açılan **ikinci kapıydı**; bırakılsaydı var olmayan bir fonksiyonu çağıracaktı. Panelin başka hiçbir
  yeri değişmedi.
  ⚠ **DOKUNULMAYANLAR:** detay sayfasının KİŞİSEL NOT kartı (`TasksApi.addPersonalNote`, gerçek) ve
  "Onay toplantısı planla" AKSİYONU (sözleşmesi var) — ikisi de yerinde, testle kilitli.
- **Gelecek regresyon riski: 🟢.**

### BL-219 — [KAYIT] "Onay toplantısı planla" da yalnız tarayıcı belleğine yazıyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüldü: `applyReviewMeeting` → `state.meetings.push({...})`; kodun kendi yorumu "the mock applies an explicit
  replacement projection after Calendar returns" diyor. **Sözleşmesi var** (`WorkAggregationModels.cs:832`
  `reviewMeetingPolicy`), **gerçeklemesi yok**.
- Bu yüzden BL-217'de SİLİNMEDİ: silinen ikisinin aksine bunun arkasında bir sözleşme duruyor, yani eksik olan
  özellik değil, servis.
- Bu turda **dokunulmadı** — yalnız kaydedildi.
- **Gelecek regresyon riski: 🟡** — kullanıcı bir toplantı planladığını sanıp takvimde bulamaz.


**BL-219 güncelleme (2026-08-25) — KAPANDI: toplantı diyaloğu ne yaptığını söylüyor**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- Eski cümle: *"İnceleme toplantısını takvime yazar; son tarih değişmez."* — **takvime hiçbir şey yazmıyor.**
  `applyReviewMeeting` `state.meetings`'e ekliyor ve `item._fixture`'ı yeniden yansıtıyor; sunucuya hiçbir şey
  gitmiyor, yenilemede kayboluyor.
- Yeni cümle üç olguyu da adıyla söylüyor — bağlantı kurulmadı · kayıt yalnız bu ekranda · kapatınca kaybolur —
  ve son tarih notunu koruyor. **7 dil.** Test hem eski iddianın 7 dilde de gitmiş olduğunu, hem üç işaretin
  7 dilde de bulunduğunu ölçüyor.
- ⚠ AKSİYON SİLİNMEDİ, sayaç ve `pause`'un aksine: ilan edilmiş sözleşmesi var
  (`reviewMeetingPolicy`, WorkAggregationModels.cs:832). **Yalan söylemeyi bırak, özellik yazma.**
- Canlı doğrulandı (`INBOX-REVIEW-REQUIRED-MEETING?fixtures=showcase`): diyalog yeni cümleyi gösteriyor.
- **Gelecek regresyon riski: 🟢**

### BL-220 — [KAYIT] Notlar ve ajanda PANELLERİ hep boş
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** Tur B'de paneller kaldırıldı; `/WorkCenterNext` üzerinde panel düğmesi ve `#wcnSidePanel` sıfır. BL-218 ile birlikte geri gelecekler.
- İkisi de liste sayfasının parçası; `state.notes` / `state.meetings` artık hiç doldurulmuyor (BL-217), yani
  panellerin ikisi de kalıcı olarak boş.
- Bu turda **kasıtlı olarak değiştirilmediler**: kaderleri liste sayfasının kendi turunda kararlaşacak
  (sil / boş-durum dili / BL-218 ile birlikte geri getir).
- **Gelecek regresyon riski: 🟡** — boş bir panel, açan için cevapsız bir soru.

### BL-221 kapanış notu (2026-08-24) — Diyalogların dikey ritmi ürünün ritmi oldu (A1)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **REFERANS ÖLÇÜMÜ (/Tasks/Create, canlı):** etiket → alan **4px** (`.form-label { margin-bottom: 4px }`,
  altı alanda da) · alan → sonraki etiket **14px** ("Tahmin (saat)" → "Etiketler") · etiket **13px**.
- **KUSUR, ÖLÇÜLDÜ:** Planla · Süre gir · Modül seç → etiket→alan **19px**. Bilgi bekle · Yeniden ata →
  alan→sonraki etiket **0px**.
- **İKİ KÖK SEBEP:**
  1. Geçen tur yalnız `margin-inline` sıfırlandı; kütüphanenin DİKEY marjları duruyordu.
     ÖNCE: `.swal2-input` `margin: 15px 0px 3px` · `.swal2-input-label` `margin: 13px 0px 4px` → 4+15 = 19px.
     SONRA: yuvaların dikey marjı **0**, etiket `margin: 14px 0px 4px` → **4px**.
  2. Elle yazılmış diyaloglarda grup boşluğu `<select class="form-select mb-3">` üzerindeydi — select2
     orijinali gizleyince o marj hiçbir şey üretmez oldu (**0px** ölçüldü). `mb-3` kaldırıldı; grup boşluğunu
     artık TEK mekanizma taşıyor: bir sonraki etiketin üst marjı.
- **SAYILAR UYDURULMADI, ÖLÇÜLDÜ:** `0.25rem` (4px) = referansın kendi `.form-label` alt marjı.
  `0.875rem` (14px) = referansın **ekranda görünen** grup boşluğu.
  ⚠ **Neden 14 ve neden 12 değil — fudge gibi göründüğü için yazılıyor:** o formda boşluğu `.row.g-3` üretiyor
  ve her grup SÜTUNUNA `margin-top: 12px` koyuyor; sütunun kutusu içindeki girdiden 2px daha aşağı iniyor
  (alan altı 755, sonraki etiket üstü 769). Popup'ta öyle bir sütun yok, dolayısıyla 12px kural 12px çizer ve
  kopyaladığı formdan 2px daha sıkı durur. Kopyayı aslına benzeten değer ölçülen değerdir; mekanizma CSS'te
  yazılı ki sonraki okuyucu nereden geldiğini bilsin.
- **YAN YANA KANIT:** /Tasks/Create üzerinde ürünün KENDİ onay diyaloğu açıldı; aynı ekran görüntüsünde form
  grubu (REF 4px / REF 14px işaretli) ve diyalog grubu (DLG 4px işaretli) birlikte, aynı fonksiyonla ölçülmüş.
- **İKON SÖZLÜĞÜ İKİYE AYRILMIŞTI, BİRLEŞTİRİLDİ.** Geçen tur elle `bx-conversation` seçilmiş ve HEM
  "Bilgi bekle"ye HEM "Yeniden ata"ya verilmişti; rail düğmesi ise `bx-user-pin` çiziyordu — **aynı aksiyon,
  iki ikon**. Artık aksiyondan açılan her diyalog `inboxActionIcon(action)` okuyor (5 okuma: taşma menüsü +
  dört diyalog). Sözlükte eksik olan ikisi **sözlüğe** eklendi: `logTime: 'bx-time-five'`,
  `requestInfo: 'bx-question-mark'`. Canlı doğrulandı: rail `plan → bx-calendar-plus`,
  `inquire → bx-question-mark`, `reassign → bx-user-pin`; açtıkları diyaloglar **aynı üç glif**.
- **"Yeniden ata" denetim listesine eklendi** (geçen tur yoktu): etiket→alan 4/4, alan→sonraki etiket 14,
  x eşit, ikon `bx-user-pin`.
- **GERİYE UYUM (üç ekran canlı, ritim kuralı hepsini etkiliyor):** `/Platform/ReferenceData` (etiket→alan 4,
  x eşit, kutu→düğme 16px, taşma yok, popup 400px) · `/UserRoleAssignments` (357px, taşma yok, `bx-trash`,
  ters düğme sırası, rozet) · `/RoleAssignments` (357px, taşma yok, danger çemberi, varsayılan cümle yerinde).
  **Hiçbiri daralmadı, hiçbiri taşmadı.**
- **Gelecek regresyon riski: 🟢.**

### BL-222 — [KAYIT] İkon eşleşmesi için jsdom testi yazılamadı, canlı ölçüldü
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- ✅ **KAPANDI (Tur C).** Kural `fixture-contract.js`'te tek yerde. ⚠ Ajan kendi testinin zayıflığını da kaydetti: ilk hâli, aradığı dizeler yorumda da geçtiği için nesne silinmesine rağmen yeşil kalmıştı — düzeltildi.
- Kıyaslanacak glif LİSTE SATIRININ aksiyon kümesinde çiziliyor ve bir satırın oraya ulaşması sekme/kabul
  kurallarına bağlı (`admissionState`, `ownershipState`, aktif sekme). Üç ayrı fixture şekli denendi, hiçbiri
  satırı varsayılan sekmeye koymadı — test ikonları değil fixture'ı doğrulamış olacaktı.
- Bunun yerine iddia **iki başka yoldan** kilitlendi: (a) kaynak testi — iki yüzey de `inboxActionIcon`
  çağırıyor ve hiçbir diyalog elle glif seçmiyor; (b) canlı ölçüm — üç aksiyon için rail düğmesinin sınıfı ile
  açılan diyaloğun sınıfı aynı dize.
- Yapılacak: liste fixture'ının hangi alanla varsayılan sekmeye düştüğünü belgeleyip DOM testini eklemek.
- **Gelecek regresyon riski: 🟡** — kaynak testi bir yeniden düzenlemede yeşil kalıp DOM'da ayrışabilir.


**BL-222 KAPANDI (2026-08-24, Tur C) — "minimum görünür satır" yazıldı**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- Kural `fixture-contract.js`'te **tek yerde**: `MINIMUM_VISIBLE_ROW` + `inTab`'den ÖLÇÜLEREK çıkarılmış dört
  koşul (`catalogVisible !== false` · `!dismissed` · `itemInScope` · `tab` eşleşmesi, `history` hariç terminal
  satırlar gizli).
- ⚠ **AÇIKÇA BİR TARİF, İKİNCİ BİR GERÇEKLEME DEĞİL:** kuralın KOŞTUĞU yer hâlâ `inTab`; ikisi çelişirse
  `inTab` haklıdır ve yorum bayattır. Testle kilitli (`const inTab` tek yerde).
- ⚠ **KENDİ TESTİMİN ZAYIFLIĞI, KAYDA GEÇSİN:** ilk hâli nesne silinmiş olmasına rağmen YEŞİL kaldı — çünkü
  aradığı dizeler açıklayıcı YORUMDA ve dışa aktarma satırında da vardı. Bir yorumun tatmin edebildiği kural,
  hiçbir şeyin zorlamadığı kuraldır. Test artık nesnenin kendisine bakıyor.

### BL-223 kapanış notu (2026-08-24) — Diyalogdaki select2'nin metni 18px'ti, ürünün alanı 15px
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Sahip resimle bildirdi.** Ölçüldü: "Bir kişi seçin" **18px**; yanındaki textarea **15px**, arkadaki sayfanın
  her `.form-control`'ü **15px**, etiket 13px.
- **SEBEP — sessizce çalışmayan bir JS seçeneği:** bağlayıcı `selectionCssClass: 'form-select'` geçiyordu ki
  kontrol ürünün alan stilini giysin. Sınıf elemana **hiç ulaşmadı**: DOM'da
  `class="select2-selection select2-selection--single"`, içinde `form-select` yok. O anahtar daha yeni bir
  select2 sürümüne ait; bu paket bilinmeyen anahtarları **sessizce düşürüyor**. Yani stil hiç uygulanmamış,
  hata da vermemişti.
- **ÖLÇEREK DÜZELTİLDİ, TAHMİNLE DEĞİL:** kanca `containerCssClass: 'wcn-dialog-select'` yapıldı; sonra DOM
  tekrar okundu ve sınıfın **kabın değil `.select2-selection`'ın kendisine** indiği görüldü
  (`select2-selection select2-selection--single wcn-dialog-select`). CSS seçicileri **ölçülen yapıya** göre
  yazıldı, seçeneğin adına göre değil.
- **Sayılar ürünün kendi sayıları:** `0.9375rem` (15px) ve `38px` — `.form-control`'den, `.wcn-date-input`'un
  zaten kopyaladığı aynı değerler. Stil `backbone-custom.css`'te (FG-003), tıpkı ürünün select2'yi diğer
  yüzeylerde (filtre çipleri) stillediği gibi.
- **Açılan liste de aynı boyutta:** 15px'lik bir kontrolün 18px'lik menü açması aynı uyumsuzluğun bir adım
  sonrasıdır (`.wcn-dialog-select-dropdown .select2-results__option`).
- **Canlı ölçüm (sonra):** select2 metni **15px** = textarea 15px = sayfadaki form-control 15px; kutu 38px;
  sol kenarlar hizalı (573/573).
- **Gelecek regresyon riski: 🟢** — testle kilitli (çalışmayan seçenek geri gelirse kırmızı).

### BL-224 kapanış notu (2026-08-24) — Ortak onay diyaloğunun sayımı yanlıştı; bekçi kuruldu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **YANLIŞ ÖLÇÜM, DÜZELTİLDİ.** Bu oturum boyunca ortak confirm'in yayılımı **"15 çağrı / 12 dosya"** diye
  ölçüldü ve her prompt'a öyle yazıldı. Gerçek:
    `showConfirm(`   = 16
    `showConfirm?.(` = 58   ← optional chaining; grep'ler bunu HİÇ görmedi
    **TOPLAM = 74 çağrı / 53 dosya**
  Yani bu oturumdaki her "geriye uyum ölçüldü" cümlesi yüzeyin **%20'sini** kapsıyordu.
  ⚠ Yanlış sayı `wcn-dialog-one-language.test.js` içinde `toBe(15)` diye **kilitliydi ve yeşildi** —
  yanlış güvence veren bir bekçi, bekçisizlikten kötüdür.
- **DÜZELTME BİÇİMİ ÖNEMLİ: `toBe(15)` → `toBe(74)` YAPILMADI.** Sabit sayı her meşru yeni çağrıda kırılır ve
  okuyucuya "sayıyı büyüt" refleksini öğretir — 15'in bir oturum boyu yaşamasının sebebi tam olarak buydu.
  Test artık **KURALI** doğruluyor: opt-in bir parametre, onu adıyla geçmeyen hiç kimseye ulaşmaz. Sayı
  yalnızca **raporlanıyor**, kilitlenmiyor.
- **BEKÇİ KURULDU:** `tests/dialog-one-implementation.test.js`
  - Ham `Swal.fire` açan her dosyayı tarar; **isimli istisna listesi** dışında bir tane bile varsa KIRMIZI.
  - İstisna listesi **12 dosya** (ham çağrı sayısı 25): `WorkCenter/task-detail.js` 8 ·
    `WorkCenterNext/app.js` 4 · `Account/{login,forgot-password,reset-password}.js` 2+2+2 ·
    `pages/demand-ideas/*` 3 · `Platform/{AuditRetention,Administrators}` 2 ·
    `DocumentManagement/TemplateMasters` 1 · `diten-unauthorized.js` 1.
  - **Bayat istisna da kırmızı:** listedeki bir dosya düzeltilirse ve satırı kalırsa test uyarır — yoksa
    delik açık kalır.
  - Her regex **iki çağrı biçimini de** görür (`showConfirm\s*\??\.?\s*\(`) — sayımı bozan hata buydu.
  - **Sanity taban:** optional-chaining biçimi toplamın yarısından fazla olmalı; olmazsa matcher'dan şüphelen.
- **İKİNCİ GERÇEKLEME BULUNDU VE MEŞRU ÇIKTI:** `backbone-shell.js:76` `window.showConfirm` atıyor — ama
  `if (typeof window.showConfirm !== 'undefined') return;` ile korunmuş bir **yedek** (partial yüklenmezse
  yerli `window.confirm`). Test bunu **isimle** kabul ediyor VE kapının açık kaldığını doğruluyor: yedek
  koşulsuz hale gelirse gerçek diyaloğu gölgeler, o yüzden koşul testte kilitli.
- **MUTASYON (2, ikisi de kırmızı):** yeni bir modüle ham `Swal.fire` yazıldı → bekçi dosya yoluyla kırmızı ·
  istisna listesine ham çağrısı olmayan bir dosya eklendi → bayatlık testi kırmızı.
- **KASTEN YAPILMAYANLAR:** 25 ham çağrının hiçbirine dokunulmadı. Sebebi CT kararı: içlerinde `login.js`,
  `forgot-password.js`, `reset-password.js` var — giriş akışı; kırılırsa kimse sisteme giremez. Ayrı tur.
- **Gelecek regresyon riski: 🟢** — bu madde riski AZALTIYOR: bundan sonra merge edilen her yeni modül ortak
  diyaloğu ya çağırır ya da testte durur.

### BL-227 kapanış notu (2026-08-24) — Onay diyaloğu Seçenek B'ye geçti (74 diyalog, tek dosyadan)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **SAHİBİN SEÇİMİ:** dört prototipten **Seçenek B**, rozet **secondary**. CT ağırlık kademesini reddetti
  (BL-225), yani **tek ağırlık** uygulandı — "geri alınamaz" cümlesi ve onay kutucuğu YOK.
- **NE DEĞİŞTİ (hepsi `_GlobalConfirmation.cshtml` + `backbone-custom.css`):**
  - İkon **80px → 32px** ve **başlığın satırına** taşındı. ⚠ Yuvaya değil, **başlığın İÇİNE**: popup bir GRID
    ve her yuva bir satır; iki şeyi bir satıra almak grid'i ezmek demekti — bu oturumda iki kez kırılan
    manevra. Tek yuvaya birleştirmek hiç grid cerrahisi istemiyor.
  - Başlık, açıklama, etiket, alan, rozet, düğmeler: **hepsi sola dayalı, hepsi 24px**.
  - ⚠ Bunu mümkün kılan tek satır: kütüphanenin `.swal2-html-container` üzerindeki
    **`padding: 18px 28.8px 5.4px`** iptal edildi. O 28.8px ürünün hiçbir yerinde yok ve açıklamanın x=573,
    diğerlerinin x=544 olmasının sebebiydi. (Aynı sınıf: daha önce iptal edilen 34px yatay ve dikey marjlar.)
  - Rozet **`bg-label-primary` → `bg-label-secondary`**, tam genişlik, tek satır (kırpılır).
    ⚠ `type`'ı İZLEMİYOR, bilerek: rozet kaydın **adını** taşıyor, eylemin ciddiyetini değil. Bir ismi
    kırmızıya boyamak "bu isim tehlikeli" demektir.
  - Düğmeler **iki uca** (`justify-content-between`), `px-5` kaldırıldı — referans create-task offcanvas
    footer'ı; ölçüldü, temanın kendi 20px inset'ini taşıyor.
  - Popup dolgusu `2.5rem 1.5rem 2rem` → **`1.5rem`** (üstteki fazlalık 80px ikon bloğu içindi, o blok yok).
  - **Onay düğmesi eylemi adıyla söylüyor:** `ConfirmProceedNamed` = "Evet, {0}", argüman
    `actionLabel(action)` — yani **düğmede yazan dizenin ta kendisi**. 7 dil. Önce "Evet, uygula" diyordu;
    o cümle üründeki her onay için doğru, dolayısıyla hiçbiri hakkında bir şey söylemiyordu.
- **CANLI ÖLÇÜM (5 modül, 4 tip):**
  | Ekran | Yükseklik | İkon | Hizalar | Onay düğmesi |
  |---|---|---|---|---|
  | Görev Merkezi · Görevi iptal et | 436→**260px** | 32px `bx-error-circle` | 24/24/24/24 | "Evet, görevi iptal et" |
  | Görev Merkezi · Bilgi bekle (ham, 2 alan) | **351px** | 32px `bx-question-mark` | 7 satır da 24 | Onayla |
  | Kullanıcı Rolleri · sil | **201px** | 32px `bx-trash` | 24/24/24/24 | "Evet, Sil" |
  | Referans Verileri · gerekçe (girdili) | **319px** | 32px `bx-help-circle` | 24/24/24/24 · etiket→alan 4px · `getInput()` true, doğrudan çocuk | — |
  | Rol İzinleri · danger | **201px** | 32px `bx-error-circle`, danger çember | 24/24/24 | `btn-danger` |
  | Görev Merkezi · warning | **201px** | 32px `bx-error`, warning çember | 24/24/24 | `btn-warning` |
- **İki genişlik × iki tema:** 1440 ve 900'de popup 400px, yatay/dikey taşma **0**. Koyu ve açık temada
  çember tonu ve glif rengi `type`'tan geliyor (açık: `rgb(255,62,29)` danger).
- **YOLDA BULUNAN VE DÜZELTİLEN KUSUR:** ikon yuvası gizlenince ham diyalogların çemberi **0px** oldu
  (ölçüldü). Ham diyalog da ikonu başlığına aldı; artık 32px ve başlıkla aynı satırda.
- **KASTEN YAPILMAYANLAR:** 25 ham `Swal.fire` (giriş akışı dahil) — BL-224'teki bekçi listesinde, ayrı tur.
  Ağırlık kademesi — BL-225, ölçüm bekliyor.
- **Gelecek regresyon riski: 🟢** — görünüm tek tanımda, bekçi (BL-224) yeni ham diyalogları durduruyor.

### BL-228 kapanış notu (2026-08-24) — Detay sayfasının son dört maddesi
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **① İPTAL EDİLEN ALT GÖREV — iki mekanizma bire indi.**
  ÖLÇÜM, üç durum ve sinyal sayıları: `bekliyor` 1 (sadece glif) · `tamamlandı` 5 (dolgu + soluk başlık +
  üstü çizili + yeşil kutu + tik glifi) · `iptal edildi` 4 (üstü çizili + **opaklık .55** + soluk kutu +
  x glifi). İkisi aynı şeyi söylüyordu ve `opacity` bunu **metni karartarak** söylüyordu — hâlâ okunması
  gereken bir kayıt satırında.
  Düzeltme yeni bir fikir değil: tamamlanmış satırın kendi sözlüğü (temanın devre-dışı tonu + üstü çizili).
  İptal, tamamlanmıştan **gerçekten farklı iki sinyalle** ayrılıyor: tamamlanma dolgusu YOK, glifi x.
  **KONTRAST (ölçüldü):** açık tema 2.21 → **2.29** · koyu tema 3.06 → **3.49**. İyileşti ama ⚠ ikisi de
  WCAG AA (4.5) altında — sebebi `--bs-secondary-color`'ın kendisi; `tamamlandı` satırı kendi dolgusu
  üzerinde **1.83** ölçüyor. Bu turda tanıtılmadı, bu turda çözülmedi → **BL-229**.
  ⚠ Satır **görünmeye devam ediyor** ve sıralaması değişmedi (dibe iniyor) — sahip kararı.
- **② /Tasks/{id} — ürünün golden desenine getirildi.**
  ÖLÇÜM ÇİZİLEN SAYFADA yapıldı (Razor'da değil):
  | | golden (`GoldenReferenceCompact/Details`) | `/Tasks/{id}` ÖNCE | SONRA |
  |---|---|---|---|
  | breadcrumb | var | **YOK** | var |
  | düğmeler | Geri · Düzenle | **"Kaydet"** → `href=…/Edit` | Geri · Düzenle |
  | `.backbone-preview-field` | 12 | **0** | 8 |
  | `.backbone-preview-section` | 4 | 0 | 1 |
  | `col-md-6` | var | **yok** | var |
  | alan deseni | ikon + etiket üstte + değer altta | `<dl class="row">` 3/9 | golden ile aynı |
  ⚠ **"Kaydet aslında Edit'e link" kusuru DOĞRULANDI ve düzeltildi** — etiket artık "Düzenle"; sayfa
  salt-okunur kaldı, aksiyon EKLENMEDİ (testte: başlıkta tam 2 kontrol, `TasksApi` yazma çağrısı yok).
  ⚠ Başlık/breadcrumb/düğmeler **Razor'a** taşındı, alanlar JS'te kaldı — golden'ın kendi bölüşümü bu.
  ⚠ `WorkCenter/task-detail.js` (bekçinin KNOWN_RAW listesindeki 8 ham Swal.fire) **hiç açılmadı**; bu sayfa
  `Tasks/details-page.js` tarafından çiziliyor. Bekçi yeşil, KNOWN_RAW hâlâ **12 dosya**.
- **③ ENGEL AFİŞİNDEKİ `FS` — kartla aynı muameleye geçti.**
  Afiş `wcn-chip wcn-chip-danger wcn-dep-type` çiziyordu: yüksek sesli kırmızı hap, açılımı YALNIZ `title`
  tooltip'inde. Artık kartın dipnotu (`wcn-dep-abbr`) — küçük, soluk, **cümleden SONRA**. Modülde tek
  kısaltma sınıfı kaldı (testle kilitli).
  ⚠ **Kartın cümleleri (`DepSentence*`) burada KULLANILMADI, gerekçesiyle:** onlar İLİŞKİYİ tarif eder
  (şu an ısırsa da ısırmasa da). Afiş CANLI bir engeli tarif ediyor ve cümlesini "hangi eylemi durduruyor"
  yan cümlesiyle (`BlockedAffects*`) eşliyor. Kartın cümlesini koymak kuralı iki kez söyleyip şu an önemli
  olan yarısını düşürürdü. **Aynı kısaltma muamelesi, farklı cümle — bilerek.**
  ⚠ **CANLI GÖRÜLEMEDİ:** hiçbir fixture ve hiçbir canlı öğe `blocker.dependencyType` taşımıyor (kanonik
  engelli fixture `code: 'DEPENDENCY_BLOCKED'` veriyor ama tür vermiyor), dolayısıyla afişteki kısaltma
  bugün hiç çizilmiyor. Değişiklik testle korunuyor, ekranda gözlenemedi → **BL-230**.
- **④ ERTELE PLACEHOLDER'I — referans kendi kuralına uydu.**
  Etiketi yoktu ve "Hangi tarihe kadar" placeholder olarak kullanılıyordu. Artık diğer tarih diyaloglarının
  kullandığı çift: etiket `SnoozeUntilLabel`, placeholder `DatePlaceholder` ("YYYY-AA-GG"). Yeni dize
  yazılmadı. `SnoozeDatePlaceholder` ("YYYY-MM-DD", yerelleştirilmemiş) artık **kullanılmıyor** → BL-230.
  ⚠ **ÜÇ YARA CANLI DOĞRULANDI:** `.swal2-input` popup'ın **doğrudan çocuğu** ✓ · `Swal.getInput()` null
  **değil** ✓ · açılışta **odak girdide** ✓. Ayrıca yeni tasarım: ikon 32px başlık satırında, etiket→alan
  4px, düğmeler iki uçta.
- **MUTASYON (4, hepsi kırmızı):** iptal satırına opaklık geri · Razor'dan breadcrumb düşürüldü · afiş çipi
  tooltip'e döndü · placeholder etiketin tekrarına döndü.
- **ESKİ İDDİAYI SAVUNAN İKİ TEST GÜNCELLENDİ:** `wcn-snooze-dialog` ("etiket değil placeholder" diyordu —
  tersine çevrildi, gerekçesiyle) ve `workcenter-next-detail-page` (`.wcn-dep-type` bekliyordu).
- **Gelecek regresyon riski: 🟢.**

### BL-230 — [KAYIT] İki ölü yol: afişteki kısaltma ve `SnoozeDatePlaceholder`
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** İki ölü yol da canlandı: afişteki kısaltma fixture eklendikten sonra çiziliyor, `SnoozeDatePlaceholder` hem resx'te hem çağrıda kullanılıyor.
- **Afişteki `FS` kısaltması hiç çizilmiyor:** hiçbir fixture ve hiçbir canlı öğe `blocker.dependencyType`
  taşımıyor. Kod ve testi hazır, veri yok. Bir fixture eklenirse görünür olur.
- **`SnoozeDatePlaceholder`** ("YYYY-MM-DD", yerelleştirilmemiş) BL-228 ile kullanımdan çıktı; yerini
  `DatePlaceholder` ("YYYY-AA-GG") aldı. Yedi resx'te duruyor.
- İkisi de zararsız; silinmeleri ya da beslenmeleri ayrı bir karar.
- **Gelecek regresyon riski: 🟢.**

### BL-231 kapanış notu (2026-08-24) — Tur A: üçüncü oluşturma kapısı, iki görsel düzeltme, üç fixture
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **İŞ 1 — "Tüm alanlar" kapısı.** ÖLÇÜLEN üç kapı ve alan sayıları: kutuya yaz+Enter **1** (son tarih/öncelik/
  atanan EBEVEYNDEN miras) · panel **5** · `/Tasks/Create` **20**. Kritik olan sayı değil: **`#taskCustomFields`
  yalnız tam formda çiziliyor** ve çalışma anında `TaskFieldDefinition`'dan doluyor (`IsRequired` taşır). Yani
  kiracı zorunlu bir özel alan tanımladığı gün iki kısayol onu TOPLAYAMAZ, tam form toplayabilir. Kısayolu
  büyütmek değil, kapı açmak doğrusu.
  - İlk iki kapı **aynen duruyor** (testle kilitli).
  - Desen **aynalandı, icat edilmedi**: düzenleme panelindeki `SubtaskOpenFullDetail` ile aynı ikincil düğme,
    aynı `bx-link-external` glifi, aynı footer konumu.
  - `TasksController.Create()` iki parametre kazandı: `parent` + `returnUrl`.
  - ⚠ **AÇIK YÖNLENDİRME KAPALI:** `Url.IsLocalUrl(returnUrl)` sunucu tarafında; dışarıyı gösteren değer
    **null**'a düşer ve istemci Görev Merkezi'ne döner. `form-page.js` URL'i kendisi PARSE ETMİYOR (testle
    kilitli: `URLSearchParams`/`location.search` yok) — yani aşağıda kimse kapıyı genişletemez.
  - Ebeveyn, modülün zaten kullandığı alanla taşınıyor: `parentTaskItemId`.
  - **CANLI TAM DÖNGÜ:** panelde "Tüm alanlar" → `/Tasks/Create?parent=…&returnUrl=…` (30 alan, özel alanlar
    bölümü **var**) → başlık+son tarih doldur → Oluştur → **detay sayfasına döndü** → yeni alt görev listede
    **göründü** → sayfa yenilendi → **hâlâ orada**.
- **İŞ 2a — tamamlanmış satırın zemini.** `--bs-secondary-bg` rgb(228,230,232) idi, üstündeki soluk metin
  **1.83** veriyordu. Zemin `--bs-light-bg-subtle`'a alındı (**token'a dokunulmadı** — ürünün her yerinde
  kullanılıyor), iki satır türü için **birlikte**.
  ⚠ **Zemin tek başına yetmedi:** yeni zeminde bile **2.09** çıktı, çünkü kaldıraç zemin değil METİN rengiydi.
  `--bs-body-color`'a geçildi. **SONUÇ (canlı, iki tema):**
  | | açık | koyu |
  |---|---|---|
  | tamamlanmış alt görev | 1.83 → **4.76** | → **6.09** |
  | tamamlanmış kontrol listesi | 1.83 → **4.76** | → **6.09** |
  | iptal edilmiş alt görev | 2.29 → **5.19** | → **6.54** |
  Üçü de AA (4.5) üstünde. **BEDELİ YAZILI:** tamamlanmış satır artık RENKLE geri çekilmiyor — gerek de yok,
  satır "bitti"yi zaten üç ayrı yolla söylüyor (kendi dolgusu, üstü çizili, tik glifi). Renk dördüncü söyleyişti
  ve okumayı zorlaştıran oydu. **BL-229 bu iki satır için kapandı**, ürünün geri kalanı için açık.
- **İŞ 2b — satır yüksekliği.** Alt görev **52px**, kontrol listesi 44px. Sebep: alt görevin "aç" düğmesi
  `btn btn-icon` taşıyordu (temanın 38px kontrol yüksekliği). Sınıf kaldırıldı → 40px, yani **fazla düştü**:
  iki satır FARKLI en-uzun-çocuktan boyutlanıyordu (kontrol listesi 30px taşıma kolu, alt görev 26px eylem).
  İkisine de **ortak taban** verildi; değer kontrol listesinin **ölçülmüş 44px**'i, yani hiçbir şey küçülmedi.
  **SONUÇ: 44 / 44, eşit** (iki tema, iki genişlik). Bilgi kaybı yok: aynı glif, aynı `title`, aynı `aria-label`.
- **İŞ 3 — üç fixture eklendi, hiçbiri bozulmadan.** Üçü de MEVCUT `ISLERIM-WORK-*` görevlerine alan olarak
  eklendi, yeni görev açılmadı; `[FIXTURE]` öneki gerçek kayıtla karışmasın diye.
  - **İptal edilmiş alt görev** (`S5`): önceki turda satırın stili yeniden yazılmıştı ama ne bir fixture ne de
    62 canlı öğe `status: 'cancelled'` taşıyordu — ekranda hiç görülememişti. S1–S4 dokunulmadı.
  - **`blocker.dependencyType`**: afişteki FS kısaltması önceki turda kartın dipnotuna çevrilmişti ama hiçbir
    veri bu alanı taşımadığı için o dal hiç çizilmiyordu.
  - **Gerçek `timeEntries` + `timesheet`**: `timeTracking` yetkisi 62 canlı görevin **0**'ında var, yani zaman
    kartı hiç ekrana gelmemişti. Liste `[]` idi, iki gerçek kayıt kondu.
- **MUTASYON (3, hepsi kırmızı):** `returnUrl` dış URL kabul etsin · zemin eski koyu değere dönsün · alt görev
  düğmesi `btn btn-icon`'a dönsün.
- **REGRESYON (canlı):** diyalog 32px ikon başlık satırında, hizalar 24/24/24/24, düğmeler iki uçta, "Vazgeç" ·
  gerekçe cümlesi eylemini söylüyor · `aria-describedby` çözülüyor · düğmeler y=523/523 · yapışkan ray ·
  bekçi **yeşil**, KNOWN_RAW **12 dosya**, büyümedi.
- **Gelecek regresyon riski: 🟢.**

### BL-232 kapanış notu (2026-08-24) — Tur B: dokuz ölü kart, iki ölü uç, iki boş panel, süre kartı
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **① ÜÇ GÖRÜNÜM MODU DETAYDAN ÇIKARILDI** (`renderCalendar` · `renderKanban` · `renderSplit`). Bunlar detay
  kartı değil: liste alıp sıralayıp kart üretiyorlar, yani **liste sayfasının görünüm modları**. Silinmediler —
  `scratchpad/view-modes.js`'e alındı; liste sayfası turunda bağlanacak → **BL-233**.
- **② EFOR KARTI BAĞLANDI.** ÖLÇÜM: kart baştan beri vardı ve **hiç çizilmemişti**. Veri toplanıyor
  (`FieldEstimateHours`/`FieldSpentHours`) ve saklanıyordu (`TaskItem.EstimateHours`/`SpentHours`), ama
  projeksiyon yalnız tahmini taşıyordu **ve** `taskContext` yetkisi sözleşmenin yetki listesinde **hiç yoktu** —
  yani hiçbir fixture da bildiremezdi. Eklenenler: DTO'ya `SpentHours`, sağlayıcıya koşullu
  `capabilities.Add("taskContext")` (yalnız rakam varsa — yetki, kartın gösterecek şeyi olduğu sözüdür),
  sözleşmeye `taskContext: ['effort']`, mapper'a düz çiftten `item.effort` kurulumu.
  ⚠ **ATAMA GEÇMİŞİ ÇİZİLMİYOR:** `assignmentHistory` mapper'da, sözleşmede ve **tüm backend'de 0 eşleşme**.
  Yarısı veriyle yarısı boş alt başlıkla çizilen kart, okuyucunun "kimse devretmemiş" ile "bunu izlemiyoruz"u
  ayırmasını engeller. Alan listesi → **BL-233**.
  ⚠ **FG-003:** ilerleme çubuğu `style="width:"` kullanıyordu; ürünün kendi `.wcn-progress-{0..100}` kademe
  sınıflarına çevrildi. Kesin oran `aria-valuenow`'da ve yandaki "7.5 / 12" okumasında duruyor.
- **③ ONAY KARTI — ÖLÇÜLDÜ, ④'E KATILDI.** `WorkflowApprovalWorkItemProvider` içinde `Amount`/`LineItem`/
  `Currency` **0 eşleşme**. Onaylar canlı geliyor ama tutar ve kalem taşımıyorlar; kart `item.amount == null`
  kapısında bekliyordu ve o kapı hiç açılmayacaktı. Alan listesi → **BL-233**.
- **④ DÖRT KART SİLİNDİ:** `renderReviewContext` · `renderExceptionContext` · `renderMeetingContext`
  (üçünün de arkasında sağlayıcı ve veri yok) · `renderThread` (yorum/etkinlik kartı bu işi zaten yapıyor).
  **SİLME KANITI:** dördü de `wwwroot/` + `Views/` altında **sıfır eşleşme** (testle kilitli, isimle raporlar).
- **İŞ 2a — `openNew` SİLİNDİ.** Dispatch ona yalnız task/note/meeting/source DIŞINDA bir `data-wcn-new` için
  gidiyordu; DOM'daki dördü de bilinen kind taşıyor → hiçbir tıklama ulaşamıyordu. Yerini başlıktaki Bootstrap
  dropdown almış.
- **İŞ 2b — TOPLU SEÇİM ŞERİDİ SİLİNDİ** (`bulkBar` · `runBulk` · `runBulkWithProgress` · `performBulk` +
  dispatch dalları). `data-wcn-check` **dört yerde okunuyor, hiçbir yerde çizilmiyordu**; seçim sütunu
  olmadan `state.tableSelected` hiç dolamaz, şerit hiç görünemezdi.
  ⚠ **İKİSİ DE GEÇEN TUR GÖRÜNÜM PAKETİNİ ALMIŞTI** — ölü kodun en tehlikeli hâli: bakımlı görünüyor.
- **İŞ 2c — NOTLAR VE AJANDA PANELLERİ SİLİNDİ.** İkisi de kalıcı boştu (besleyen `openQuickNote`/
  `openMeetingForm` geçen turda silinmişti, `state.notes`/`state.meetings` `[]` ile başlayıp hiç yüklenmiyor).
  BL-218 ile birlikte geri gelecekler.
- **İŞ 3 — SÜRE KARTI.** ⚠ **BAŞLAT/DURAKLAT KARTA TAŞINMADI**, ve teşhis bu turda değişti: sayaç bağımsız bir
  kumanda değil, görevin durumunun **yan etkisi** (`start`→işler, `pause`/`complete`→katlanır). Karta kumanda
  koymak, salt-okunur görünen bir kartın içinden yaşam döngüsünü değiştiren **ikinci bir otorite** açardı —
  bu oturumda doküman onayı için tam bu gerekçeyle reddedilmişti.
  Karta eklenenler: **"Süre gir"** (durum değiştirmez, kişisel ölçüm — rail'den alındı, rail'de `logTime`
  filtreleniyor), **durum satırı** ("Devam ediyor — sayaç işliyor" / "Duraklatıldı — sayaç durdu") ve tek
  satırlık **ipucu** ("Sayaç görevin durumunu izler; başlatma ve duraklatma aksiyonlardan yapılır"). 7 dil.
- **CANLI (1440×koyu, 900×açık):** süre kartı 3sa 45dk + durum + ipucu + "Süre gir" ✓ · rail'de "Süre gir"
  **yok** ✓ · efor kartı "7.5 / 12", `wcn-progress-60`, inline stil **yok** ✓ · satır yükseklikleri 44/44 ·
  kontrast done 4.76 / iptal 5.19 · yatay taşma 0.
- **⚠ SAYAÇ YENİLEMEDE KORUNMUYOR — ÖLÇÜLDÜ VE DÜZELTİLMEDİ:** canlı sayaç 37:29 → yenileme → **37:15**, yani
  devam etmedi, baştan başladı. Sebep: mapper `startedAt`'ı `Date.now() - (37 * 60000)` ile **her yüklemede
  yeniden üretiyor** ve `TaskItem`'da sayaç başlangıcı alanı **hiç yok** (DTO yalnız `TimerState` taşıyor).
  TOPLAM korunuyor (saklanan `loggedMinutes`'tan geliyor); tiklayan sayı fixture tiyatrosu → **BL-234**.
- **FIXTURE:** `ISLERIM-WORK-ACTIVE`'e `taskContext` yetkisi + `effort` eklendi, `timesheet` nesnesi
  `loggedMinutes`'a çevrildi (mapper `timesheet`i kendisi türetiyor, elle verilen ezilip kart "0sa 0dk"
  okuyordu — ölçüldü).
- **MUTASYON (3, hepsi kırmızı):** silinen bir render geri kondu · `SpentHours` DTO'dan düşürüldü ·
  `logTime` rail'e geri kondu.
- **REGRESYON:** diyalog 32px/24-24-24-24/iki uçta · gerekçe cümlesi · `aria-describedby` · düğmeler
  y=523/523 · yapışkan ray · bağımlılık cümlesi · "Tüm alanlar" kapısı yerinde · **bekçi yeşil, KNOWN_RAW 12**.
- **Gelecek regresyon riski: 🟢.**

### BL-233 — [ERTELENDİ] Üç kartın alan listesi ve üç görünüm modu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Onay kalem tablosu** (`renderApprovalContext`): tutar + para birimi + kalem satırları (hesap · masraf
  merkezi · miktar · birim fiyat · satır toplamı). Sağlayıcı bugün hiçbirini taşımıyor.
- **İnceleme imza geçmişi** (`renderReviewContext`): imzalayan · rol · karar · tarih · not.
- **Sapma kartı** (`renderExceptionContext`): beklenen · gerçekleşen · fark · eşik · gerekçe.
- **Atama geçmişi** (efor kartının çizilmeyen yarısı): devreden · devralan · tarih · gerekçe.
- **Üç görünüm modu** (`renderCalendar`/`renderKanban`/`renderSplit`): `scratchpad/view-modes.js`'te duruyor,
  liste sayfası turunda bağlanacak.
- Gerekçe: kartı yeniden yazmak yarım gün, **alanları yeniden düşünmek günler**.
- **Gelecek regresyon riski: 🟢.**


**BL-233 güncelleme (2026-08-25) — KAPANDI: üç görünüm modu liste sayfasına bağlandı**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- Tur B'de detay sayfasından çıkarılmışlardı ve bu **doğruydu**: üçü de bir LİSTE alıp düzenliyor, yani liste
  sayfası görünümleri. Scratchpad'de bekletildiler, silinmediler — tam da bunun için.
- Üçü `TAB_VIEWS`'e, dispatch'e ve URL beyaz listesine eklendi. **Hiçbir gövde yeniden yazılmadı**; tek ekleme
  takvimin eksik boş durumu oldu.
- ⚠ **SÜZME YAPI GEREĞİ ÇALIŞIYOR**, tekrarlanan bir kuralla değil: üçü de `activeItems()` ile başlıyor —
  `renderList` ve `renderTable`'ın çağırdığı aynı işlev. **Kanıt tablosu:**

  | Durum | Liste | Kanban | Split | Takvim |
  |---|---|---|---|---|
  | çip kapalı | 30 | **30** | **30** | 6 |
  | Bloke açık | 4 | **4** | **4** | 2 |

- ⚠ **TAKVİM AYRI VE BUNU YAZIYORUM:** süzmeyi uyguluyor (6→2), ama **yalnız içinde bulunulan ayı** çiziyor ve
  **ay değiştirme kontrolü yok**. Yani 30 öğelik listenin 6'sı görünüyor. Boş değil ama tam da değil —
  CT'nin "boş bir takvim, takvim değildir" uyarısının komşusu. **Karar gerekiyor** (BL-256).
- ⚠ **KANBAN İÇİN "ÖNCE BAK" TALİMATI HAKLIYDI VE BENİM ÖLÇÜMÜM YANLIŞTI.** Denetimde "0 CSS kuralı" yazmışım;
  yalnız `.wcn-kanban` sarmalayıcısını aramışım. Gerçekte `.wcn-kboard`(1) + `.wcn-kcol`(4) + `.wcn-kcard`(9)
  = **14 kural** var. Ekranda bakıldı: Geçmiş sekmesinde iki sütun (Tamamlandı 9 · İptal edildi 9), flex satır,
  272px sütunlar, taşma yok. **Stil yazılmadı, gerek yoktu.**
- ⚠ Kanban İşlerim'de **tek sütun** çiziyor — kodun kendi dalı: segmenti olan sekmede segment tek sütun olur.
  Yani panonun asıl değeri segmentsiz sekmelerde (Havuz · Geçmiş). Davranış değiştirilmedi, ölçüldü ve yazıldı.
- Kanban Gelen Kutusu'na eklenmedi: sütunları yaşam döngüsü durumları, gelen kutusu satırının tek durumu var.
- **Gelecek regresyon riski: 🟢**

### BL-237 — [ÖLÇÜLDÜ] `pause` geçişi backend'de YOK; "Duraklat" yalnız mock'ta yaşıyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- CT yaşam döngüsü turunda ölçtü (2026-08-24), gerçek görev `370ab18b`.
- `TasksController` geçiş listesi: `accept · claim · release · plan · start · inquire · submitReview ·
  return · reassign · complete · cancel` — **`pause` YOK.** Tasks uygulama katmanında da eşleşme yok.
- Frontend'de `case 'pause'` (app.js:5767) **yerel/mock durum değişimi**; showcase fixture'ında "Duraklat"
  düğmesi görünüyor ve tarayıcıda "çalışıyor". Gerçek görevde hiç sunulmuyor.
- ⚠ İki sonucu var: (1) süre kartını üstüne kurduğumuz "duraklat → sayaç katlanır" anlatısının **arkası yok**;
  (2) showcase, gerçek veride **imkânsız bir aksiyon** gösteriyor — bu oturumda tam bu sınıfı temizledik.
- Karar gerekiyor: `pause` backend'e eklenecek mi, yoksa mock'tan da kaldırılacak mı? İkisi de olur;
  ikisinin arasında kalmak olmaz.
- **Gelecek regresyon riski: 🟡** — sunulmadığı için kullanıcıyı bugün yanıltmıyor, ama showcase yanıltıyor.


**BL-237 güncelleme (2026-08-25) — KAPANDI: `pause` mock'tan kaldırıldı, backend'e EKLENMEDİ**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- CT kararı uygulandı. `case 'pause'` ve ona bağlı yerel durum değişimi silindi; `islerim-showcase-fixtures.js`
  ve `canonical-fixtures.js` içinde "Duraklat" sunan her şey kaldırıldı.
- **SİLME KANITI:** `'pause'` → `wwwroot/assets/js/WorkCenterNext/` altında **sıfır** eşleşme (yorumlar hariç
  tutuldu; yorum kaldırmanın gerekçesini kaydediyor, davranışı değil). Test bunu her koşuda ölçüyor.
- Ölü kalan iki şey de gitti: ulaşılamaz `case 'timerPause'` dalı, ve 7 dilden `ActPause` ("Duraklat") +
  `ToastTimerPaused` anahtarları.
- ⚠ **`TimerStatePaused` KALDI ve bu bir kalıntı DEĞİL.** `ResolveExecutionState`, `Waiting` ve `PendingReview`
  yaşam döngülerini `"paused"` diye yansıtıyor — yani bir görev gerçekten duraklamış olabilir, sadece bir
  DÜĞMEYLE duraklatılamaz. Geçişle birlikte bu dalı da silmek, bekleyen her görevde durum satırını boşaltırdı.
  Ölçüldü, silinmedi.
- ⚠ Backend'e eklenmemesinin gerekçesi kodun içine yazıldı: `pause` yeni bir yaşam döngüsü durumu demek — her
  listede, her filtrede yeni bir değer ve bir migration. Bu bir ürün kararı, temizlik değil. Ayrıca işi
  durdurmanın iki dürüst yolu zaten var: **Bilgi bekle** (başkasını bekliyorsunuz, gerekçesiyle) ve **Ertele**.
- **Gelecek regresyon riski: 🟢** — hiçbir şey kaybolmadı; var olmayan bir düğme kaldırıldı.

### BL-238 — [ÖLÇÜLDÜ] Alt görev tik kutusu, başlatılmamış satırda yalnızca hata üretebiliyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- CT ölçtü (2026-08-24), gerçek görev `370ab18b` / alt görev `ea832a9b`.
- Satırdaki tik kutusu doğrudan `complete` çağırıyor. Sunucu **409 `TASK_INVALID_STATE`** dönüyor:
  bir görev `start` edilmeden `complete` edilemiyor.
- Hata DÜRÜSTÇE gösteriliyor — *"Bu görev bu durumdayken tamamlanamaz. Önce başlatın."* (çevrilmiş, ham
  hata değil). Yani sistem doğru davranıyor; kusur davranışta değil, **sunulan yolda**.
- ⚠ Satır menüsünde **"başlat" yok** (yalnız aç · iptal et). Yani bir onay kutusunu işaretlemek için:
  alt görevi aç → başlat → geri dön → işaretle. **Üç gezinme, bir tık için.**
- API ile `start` + `complete` yapıldığında her şey doğru çalışıyor: satır `done`, ebeveynin
  "Bir alt görev hâlâ açık" gerekçesi kalkıyor, **Tamamla etkinleşiyor** (CT canlı doğruladı).
- Seçenekler: (a) tik kutusu gerekiyorsa `start`+`complete`i arka arkaya yapsın; (b) satır menüsüne
  "Başlat" eklensin; (c) başlatılmamış satırda kutu devre dışı olsun ve gerekçesini söylesin.
  CT önerisi: **(a)** — kullanıcının niyeti "bu iş bitti" demek; ara durumu ondan istemek, sistemin kendi
  kuralını kullanıcıya iş olarak geri vermektir.
- **Gelecek regresyon riski: 🟡** — veri bozulmuyor, ama en sık yapılan hareket üç adıma çıkıyor.


**BL-238 güncelleme (2026-08-25) — KAPANDI: tik kutusu artık `start` + `complete` yapıyor**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- CT'nin (a) seçeneği uygulandı. Kutu, çocuk `not-started` ise önce `start`, sonra `complete` çağırıyor;
  zaten başlamış bir çocukta **tek** yazma yapıyor (gereksiz `start` = ikinci yazma, ikinci denetim kaydı,
  ikinci başarısızlık ihtimali).
- ⚠ İkinci çağrı YENİ sürümü kullanıyor. `start` sürümü artırıyor; eski jetonu tekrar kullanmak `complete`i,
  kullanıcının bir saniye önce kendi yaptığı değişiklik yüzünden eşzamanlılık çakışmasına düşürürdü. Sürüm
  sunucudan **yeniden okunuyor**, `expectedVersion + 1` diye tahmin edilmiyor.
- **CANLI DOĞRULAMA (gerçek görev, fixture değil)** — `0276e51e` / alt görev `eddb350d`:
  `start(v1)→204`, `complete(v2)→204`. Üç ölçüm de tuttu: satır `wcn-subtask-done` / "Tamamlandı" ·
  ebeveynin **"Bir alt görev hâlâ açık"** gerekçesi kalktı · **Tamamla etkinleşti**.
- **BAŞARISIZLIK YOLU (gerçek ret)** — `359bd3ee`, incelemeye bağlı alt görev: `start`→204, `complete`→**409
  `REVIEW_PENDING`**. Kullanıcının gördüğü: *"Alt görev başlatıldı ama tamamlanamadı: Görev, incelemeyi yapan
  kişinin yanıtını bekliyor."* Satır dürüstçe "Devam ediyor" kalıyor — çocuk gerçekten çalışır durumda ve
  kullanıcı bunu istememişti, o yüzden her iki yarı da söyleniyor.
- **Gelecek regresyon riski: 🟢** — sunucu hâlâ tek karar verici; istemci yalnızca aynı hedefe giden yolu
  yürüyor.

### BL-239 — [DÜZELTİLDİ 2026-08-25] Alt görev bildirimi ham `{1}` yer tutucusu gösteriyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Canlı ölçümde ekranda okunan: **`{1} — 'BL-238 hata yolu — orta' işlemi tamamlandı`**.
- Neden: `ToastActionApplied` İKİ argüman istiyor (`{1} — '{0}' işlemi tamamlandı`, yani aksiyon etiketi +
  başlık); alt görev çağrı yeri yalnızca başlığı veriyordu.
- ⚠ Uyarı zaten kodun içinde yazılıydı — `afterPhase2Write`'ın kendi yorumunda: *"bir argüman eksik vermek
  ikincisinin yer tutucusunu bastırır, bu da ham-anahtar hatasının başka bir şapkayla dönmüş hâlidir."*
  Yorum uyarıyordu, hiçbir şey zorlamıyordu. **Bu, kusurun kendisiydi, aynı sınıftan.**
- Düzeltme: tek olguluk cümleye kendi tek argümanlı anahtarı verildi — `ToastSubtaskCompleted`, 7 dil.
  Test hem anahtarın kullanıldığını hem de 7 dilde değerin `{1}` İÇERMEDİĞİNİ ölçüyor.
- **Gelecek regresyon riski: 🟢**

### BL-240 — [DÜZELTİLDİ 2026-08-25] `REVIEW_PENDING` haritasız; kullanıcı "bir hata oluştu" okuyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- İlk ret ölçümünde sunucu **409 `REVIEW_PENDING`** dedi, ekranda **"İşlem sırasında bir hata oluştu."** çıktı.
- Mekanizma doğru çalışıyordu: `failureMessage` haritasız kodu genel cümleye düşürüyor **ve konsola yazıyor**,
  tam da bulunabilir kalsın diye. Eksik olan mekanizma değil, **haritanın kendisiydi** — üç halka birden:
  `REASON_CODE_MESSAGE_KEYS` · `BLOCKING_REASON_CODES` · `_IndexL10n.cshtml` yükü.
- ⚠ `APPROVAL_PENDING` cümlesi ödünç ALINMADI. Sunucunun kendi doc-yorumu gerekçeyi söylüyor: iki kapıyı
  **farklı kişiler** açar; incelemeci işi tutarken kullanıcıya "onay bekleniyor" demek onu yanlış kişiye
  yollar. Yeni anahtar `ErrorReviewPending`, 7 dil. Test iki cümlenin aynı olmadığını da ölçüyor.
- Yeniden ölçüldü (`359bd3ee`): kullanıcı artık *"…Görev, incelemeyi yapan kişinin yanıtını bekliyor."* okuyor.
- **Gelecek regresyon riski: 🟢**

### BL-242 — [DÜZELTİLDİ 2026-08-25] Kapalı birincil aksiyonun gerekçesi düğmeye BAĞLI değildi
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm (gerçek görev `f5d31d28`, kapalı "Tamamla"): gerekçe `<p>` ekranda, `role="note"` ile — ama `id` boş,
  düğmede `aria-describedby` yok. Gören okuyucu nedeni öğreniyordu; ekran okuyucu kullanan **yalnızca
  "Tamamla, kapalı" duyuyordu.**
- Kod bilerek böyleydi: birincil katmanın cümlesi kendi `<li>`'sinin içinde, düğmenin hemen altında duruyor —
  "yakınlık yeterli" varsayımı. ⚠ Yakınlık **görsel** bir argüman; sesli okunduğunda ayakta kalmıyor.
- Düzeltme: birincil de artık `aria-describedby` taşıyor; id yardımcı işlevden geliyor (ikincil/yıkıcı katmanlar
  zaten kullanıyordu, çakışma yok). Canlı doğrulandı: id tekil, işaret ettiği metin "Bir alt görev hâlâ açık".
- **Gelecek regresyon riski: 🟢**

### BL-243 — [DÜZELTİLDİ 2026-08-25] Yapışkan alt raydaki aynı düğme KAPALI GÖRÜNMÜYORDU
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm (900px, `f5d31d28`): karttaki kapalı "Tamamla" `opacity: .55`; **yapışkan raydaki aynı düğme
  `opacity: 1`** — tam parlak yeşil, kendi "Bir alt görev hâlâ açık" cümlesinin hemen altında tıklanabilir
  görünüyor. `disabled` niteliği ikisinde de vardı; eksik olan yalnızca **görünüm**.
- Neden: sönükleştirme `.wcn-act-disabled .wcn-act-btn` kuralına, yani sarmalayan `<li>`'ye asılıydı — o sınıfı
  sadece KART yolu yazıyor. Ray başka bir render yolu, sarmalayıcısı yok.
- Düzeltme kuralı düğmenin **kendi durumuna** taşıdı: `.wcn-act-btn:disabled { opacity: .55 }`. Böylece bugün
  yazılmamış render yolları da kapsanıyor. Sarmalayıcı kuralı, `:disabled` alamayan `<a>` çeşidi için kaldı.
- Raya kendi gerekçe id'si verildi (`…-complete-bar`): ray `d-lg-none`, yani 992px üstünde gizli ama **DOM'da**,
  iki cümle her sayfada bir arada. Aynı id çift id demekti; `getElementById` kartınkini döndürürdü.
- ⚠ Bu, oturumda üçüncü kez aynı sınıf: **bir yüzeyi düzeltip ikizini unutmak.** Bu yüzden düzeltme JS'te değil
  CSS'te, davranışın kaynağında yapıldı.
- **Gelecek regresyon riski: 🟢**

### BL-244 — [SAYIM, DÜZELTİLMEDİ] `state.*` üzerine yazıp sunucuya hiç gitmeyen yollar
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- CT iki tane bulup silmişti, üçüncüsü BL-219'du. **Dördüncüsü var** — ve beklenenden büyük.
- Sayım (`wwwroot/assets/js/WorkCenterNext/app.js`), dört yol:
  1. `applyReviewMeeting` → `state.meetings.push` — **BL-219, bu tur dürüstleştirildi** (silinmedi: sözleşmesi var).
  2. `applyTransition` içindeki `case 'accept'` / `itemType === 'meetingInvite'` → `state.meetings.push`.
     ⚠ **Kusur değil:** çağıranı `isRealTaskItem` ile çitli; gerçek görevler sunucuya gidiyor, fixture olmayan
     her şey için konsola "MOCK transition" uyarısı basılıyor. Yapı gereği dürüst.
  3. `createSelfTask`'ın fixture dalı → `state.items.push`. Aynı şekilde showcase'e çitli, kendi yorumunda yazılı.
  4. **`addGlobalNote` → `state.notes.unshift`, VE ULAŞILAMAZ.** Ölçüm: beş kanca hiç çizilmeyen işaretlemeyi
     dinliyor — `data-wcn-toggle` · `data-wcn-global-note-input` · `data-wcn-global-note-add` ·
     `data-wcn-note-convert` · `data-wcn-meeting-followup` — beşinin de `="` ile çizim sayısı **0**.
     Tur B'de `renderNotes`/`renderAgenda` silindiğinde işleyicileri kaldı. Yanlarında `state.notesOpen`,
     `state.agendaOpen` ve URL'in `panel` parametresi de yaşıyor.
- ⚠ CT'nin talimatı gereği **düzeltilmedi, kaydedildi**. Temizlenirse tek bir işte temizlenmeli: beş kanca,
  iki durum alanı, `panel` URL parametresi ve `convertGlobalNote`/`createMeetingFollowup` işlevleri.
- **Gelecek regresyon riski: 🟢 (ulaşılamaz kod kullanıcıya görünmüyor) / 🟡 bakım** — silinmiş bir yüzeyin
  işleyicisi, kodu okuyan için hâlâ var olan bir özellik gibi duruyor.


**BL-244 güncelleme (2026-08-25) — KAPANDI: ulaşılamaz beş kanca ve taşıdıkları her şey silindi**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- Silinenler: beş kanca (`data-wcn-toggle` · `data-wcn-global-note-input` · `data-wcn-global-note-add` ·
  `data-wcn-note-convert` · `data-wcn-meeting-followup`), üç işlev (`addGlobalNote` · `convertGlobalNote` ·
  `createMeetingFollowup`), iki durum bayrağı (`state.agendaOpen` · `state.notesOpen`), taşıdıkları veri
  (`state.notes`, `buildNotes`, `NOTES` fixture'ı) ve `panel` URL parametresi — hem okuması, hem yazması,
  hem beyaz listesi.
- **SİLME KANITI:** on iki adın hepsi `wwwroot/` altında **sıfır** eşleşme (yorumlar hariç tutuldu). Test
  her koşuda ad ad ölçüyor, hangisinin geri geldiğini adıyla söylüyor.
- ⚠ **KİŞİSEL NOT KARTINA DOKUNULMADI** ve bu teste yazıldı: `data-wcn-note-input` / `-note-add` / `-note-save`
  ile `addPersonalNote` duruyor ve **çizildiği** de ölçülüyor (`data-wcn-note-add="${item.id}"`), sadece
  anıldığı değil. İki ad bir kelime farkla ayrılıyor ve birbirleriyle ilgileri yok.
- ⚠ İki test bu silmeden düştü ve ikisi de kuralı koruyacak şekilde onarıldı:
  1. `buildNotes()` üzerindeki fixture-kapısı iddiası kaldırıldı (kapatılacak bir üretici kalmadı); kuralın
     kendisi var olan her üretici için aynen duruyor.
  2. `openCreateInSource` dilimi **üçüncü kez** bir komşunun adına bağlı olduğu için koptu — önce
     `openMeetingForm`, sonra `createMeetingFollowup` silinmişti. Her seferinde `indexOf` -1 döndü ve dilim
     DOSYANIN SONUNA kadar uzadı; ikinci sefer gürültülü koptu, üçüncüsünün kopacağının garantisi yoktu.
     **Kaybolmasına izin verilen bir koda çakılı pencere, pencere değildir**: dilim artık adı ne olursa olsun
     aynı girinti düzeyindeki bir sonraki bildirimde bitiyor, ayrıca boş-değil ve dosya-değil diye iki kez
     ölçülüyor.
- **BL-218 GÜNCELLEMESİ:** o kayıt "ertelendi, silinmedi" diyordu — artık kod da yok. Paneller geri geldiğinde
  **render'ları, kancaları, durum alanları ve URL parametresi yeniden yazılacak**; geriye yalnızca niyet kaldı.
- **Gelecek regresyon riski: 🟢** — silinen hiçbir şeye kullanıcı erişemiyordu.

### BL-246 — [DÜZELTİLDİ 2026-08-25] Ertelenmiş öğe sinyal çipinin sayısına sızıyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm: İşlerim'de "SLA riski **14**", altındaki segmentlerin toplamı **13** — fark tam olarak bir ertelenmiş
  satır (CT scroll testi, 2026-09-30).
- **SEBEP, ve neden yalnız BİR sayaç sızdırdı:** erteleme gizlemesi `passesFilters` içinde
  `except !== 'signal'` arkasında duruyor; `signalCount` ise sinyal eksenini atlayan tek faset. Tür ve segment
  sayaçları kuralı hep çalıştırıyordu ve hep doğruydu. Yani eksik bir kural değil, **kuralın üstünden atlayan
  bir faset** vardı.
- ⚠ **BL-045 TASARIMINA DOKUNULMADI.** Çipin segmentten bağımsız sayması bilerek verilmiş karardır ve öyle
  kaldı; çipe basınca segmentlerin yeniden hesaplanması da çalışmaya devam ediyor.
- ⚠ "Ertelenmiş" çipinin KENDİ sayacı ertelenmişleri saymaya devam ediyor — o çip onları açığa çıkarmak için
  var; 0 yazıp bir satır açan çip aynı yalanın tersidir. Kural `signalCount` içinde tek bir istisnayla yazıldı.
- ⚠ Çipin DAVRANIŞI değişmedi, yalnız ARİTMETİĞİ: `snoozed` açıkken `parkedOffScreen` herkese false döndüğü
  için sayaçlar listenin açığa çıkmış hâlini kendiliğinden takip ediyor.
- **KANIT TABLOSU** (çip sayısı = o çipe basınca segmentlerin toplamı) — üç sekme × her sinyal, **6/6 eşit**:

  | Sekme | Çip | Sayaç | Segment toplamı |
  |---|---|---|---|
  | Gelen Kutusu | SLA riski | 15 | 15 |
  | İşlerim | Bloke | 4 | 4 |
  | İşlerim | **SLA riski** | **13** (önce 14) | **13** |
  | İşlerim | Ertelenmiş | 1 | 1 |
  | Geçmiş | SLA riski | 10 | 10 |
  | Geçmiş | Ertelenmiş | 1 | 1 |

- **Gelecek regresyon riski: 🟢**

### BL-247 — [DÜZELTİLDİ 2026-08-25] İki çip satırı, iki farklı birleşme kuralı
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm: tür ekseni OR (`typeFilter.has`), sinyal ekseni AND (`for … if (!TEST) return false`). Aynı ekranda,
  aynı görünümde, iki farklı mantık. Canlı sonuç: Bloke(4) + SLA(7) = **1**.
- Sinyal filtresi **OR** oldu; eksenler arası AND korundu (tür ∧ sinyal ∧ modül …).
- **Ölçüm sonrası:** Bloke(4) ∪ SLA(13) = **16** — yani biri iki sinyali birden taşıyor. URL yazma/okuma
  bozulmadı (`signals=blocked,sla-risk`), testle kilitlendi.
- Gerekçe koda yazıldı: bir sinyal "neye dikkat etmeliyim" sorusunu yanıtlar; iki tanesini seçmek **daha geniş**
  bir ağ ister, daha dar değil. Kesişim FARKLI sorular arasında doğrudur.
- **Gelecek regresyon riski: 🟢**

### BL-248 — [DÜZELTİLDİ 2026-08-25] Sayacı sıfır olan tür çipi çizilmeyecek
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm: Gelen Kutusu'nun 7 tür çipinden **6'sı** gerçek veride 0 (Onay · İnceleme · Sorun · İstisna ·
  Toplantı Daveti), hepsi tıklanabilir, hepsi boş listeye götürüyor. Canlıda 7 çip → **3 çip**.
- Eski yorum "never dimmed at 0 (no perpetual grey chips)" diyordu: teşhis doğru, çözülen yarı yanlış. **Gri
  hiç sorun değildi; VAAT sorundu.**
- ⚠ "Tümü" çipi istisna olarak kaldı — o eksenin sıfır durumudur.
- ⚠ **KALICI GİZLEME DEĞİL** ve bu koda yazıldı: sayaç canlı projeksiyondan geliyor, başka bir modül inceleme
  ya da sorun göndermeye başladığı anda çip kendiliğinden geri gelir. Kimsenin geri koyması gerekmiyor.
- ⚠ İkinci bir mekanizma yazılmadı: sinyal çipleri ve diğer sekmelerin tür çipleri zaten
  `sayaç > 0 || filtre açık` kuralını kullanıyordu; aynısı kullanıldı.
- ⚠ **TESTİN KENDİ KUSURU YAKALANDI:** ilk hâli `buildInboxChips`'in TAMAMINDA guard satırını arıyordu ve o
  satır `riskChips`'te de var — guard tür çiplerinden silindiğinde test YEŞİL kaldı. Bu oturumda ikinci kez
  aynı sınıf: **başka bir satırın tatmin edebildiği kural, hiçbir şeyin zorlamadığı kuraldır.** Test artık
  yalnız `mainChips` bloğuna bakıyor; mutasyon tekrarlandı, kırmızıya döndü.
- **Gelecek regresyon riski: 🟢**

### BL-249 — [DÜZELTİLDİ 2026-08-25] Beş kontrol ailesi klavyeyle görünmezdi
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm (GERÇEK Tab ile): `core.css`'teki küresel `button:focus, button:focus-visible { outline: 0 }` her düz
  düğmenin halkasını siliyor. Kendi kuralı olan üçü kurtuluyordu (`.wcn-row` · `.wcn-fchip` · sekme, 2px),
  beşi kurtulmuyordu: satır birincil düğmesi · satır menüsü · segment · görünüm düğmesi · sayfalama.
- Beşine de ürünün kendi halkası verildi: `2px solid var(--bs-primary)`, offset 2px — kurtulanların kullandığı
  değerlerin aynısı. ⚠ `--bs-btn-focus-box-shadow` **boş** ölçüldü; ondan türetmek hiçbir şey çizmezdi.
- ⚠ Küresel kurala DOKUNULMADI — bütün ürünü ilgilendirir, bu sayfanın vereceği karar değil.
- ⚠ **PROGRAMATİK `.focus()` YANILTIR** — `:focus-visible` doğmaz. Denetimde ilk ölçüm böyle yapılıp yanlış
  çıkmış, bu turda gerçek Tab ile hem ölçüldü hem doğrulandı: satır menüsü düğmesinde
  `solid 2px rgb(105,108,255)`, offset 2px.
- Testin kendi dilimi de bir kez yanlış geçti: CSS penceresi ilk `}` ile kesiliyordu ve üstündeki yorum
  `{ outline: 0 }` ifadesini ALINTILIYOR. Pencere kurala bağlandı.
- **Gelecek regresyon riski: 🟢**

### BL-250 — [DÜZELTİLDİ 2026-08-25] Liste satırı ile detay satırı iki ayrı dil konuşuyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Denetimde altı ölçüm ayrışıyordu. **İkisi benim ölçüm hatamdı, ikisi bilerek farklı, ikisi düzeltildi.**

  | Ölçüm | Detay satırı | Liste satırı (önce) | Sonuç |
  |---|---|---|---|
  | yarıçap | 6px | 8px | **eşitlendi → 6px** |
  | hizalama | center | stretch | **eşitlendi → center** |
  | dolgu | 6px 8px | 12px 14px | **bilerek farklı** |
  | yükseklik | 52px | 98px | **bilerek farklı** |
  | kenarlık | 1px opak | 1px saydam | **bilerek farklı** |
  | hover | %3 tint | "renk değişmiyor" | **ÖLÇÜM HATASIYDI — zaten çalışıyor** |

- **Hizalama neden güvenli:** SLA vurgu çubuğu kendi `align-self: stretch`'ini taşıyor, yani ebeveyn
  `center` olunca da tam yükseklikte kalıyor. Hiçbir şey ebeveynin gerilmesine bağlı değildi.
- **Dolgu ve yükseklik neden farklı bırakıldı:** liste satırı iki satırlık (başlık + özet + çip şeridi), detay
  satırı tek satırlık. 6px/8px'e sıkıştırmak, bir sayıyı kazanmak için içeriği kırpmak olurdu. **Yükseklik bir
  kural değil, içeriğin sonucudur.**
- **Saydam kenarlık bilerek:** Sneat `data-skin` anahtarını `.card` gibi yansıtıyor (`bordered` skininde
  renkleniyor ve gölge kalkıyor) ve `:hover`'da zaten renkleniyor. Detay satırı KART İÇİNDE durduğu için opak;
  bu satır SAYFA üstünde durduğu için gölgeli. İki yüzey, iki doğru cevap.
- ⚠ **HOVER ÖLÇÜMÜ YANLIŞTI VE SEBEBİ ÖNEMLİ:** denetim `dispatchEvent(new MouseEvent('mouseover'))` kullanmış;
  bu `:hover` sözde-sınıfını DOĞURMAZ. Gerçek fareyle yeniden ölçüldü: `rgba(105,108,255,.035)` — tam olarak
  detay sayfasının %3 tint'i, üstüne kenarlık rengi. **Bu oturumda üçüncü kez sentetik olay yanlış negatif
  verdi** (programatik `.focus()`, `mouseover`, ve şimdi bu). Kural: sözde-sınıf gerçek girdiyle ölçülür.
- **Gelecek regresyon riski: 🟢**

### BL-251 — [DÜZELTİLDİ 2026-08-25] Sıralama ve sayfa numarası URL'ye yazılmıyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Sekme, segment, çipler, arama, görünüm zaten round-trip yapıyordu; bu ikisi atlanmıştı. Yenileyince 1. sayfaya
  ve varsayılan sıraya dönülüyordu, sıralı bir liste paylaşılamıyordu.
- ⚠ **YOLA ÇIKARKEN BULUNAN ALTINCI ÖLÜ YOL:** `state.sortKey` · `state.sortDir` · `SORTERS` ve 8415'teki
  `[data-wcn-sort]` işleyicisi duruyor, ama **`data-wcn-sort="` sıfır kez çiziliyor**. Yani listeyi hiçbir
  kontrol sıralamıyor; sıralayan tek şey DataTables'ın kendi motoru ve o kimseye haber vermiyordu.
- Bu yüzden çözüm "state'i serileştir" değil, **grid'in sırasını state'e AYNALAMAK** oldu (`order.dt` kancası).
  Böylece var olan makine ölü kalmak yerine canlandı; ikinci bir ölü yol bırakılmadı.
- Grid'in açılış sırası artık `state`'ten, yani URL'den geliyor (`order: [[6,'asc']]` sabiti kalktı).
- ⚠ Sıralama anahtarı `SORTERS`'ın KENDİSİNE karşı doğrulanıyor, elle yazılmış bir kopya listeye karşı değil —
  yeni bir sütun eklendiği gün URL'den de sıralanabilir olur.
- ⚠ URL'de 1-tabanlı, state'te 0-tabanlı: `?page=0` kimsenin yazmayacağı bir bağlantı.
- **CANLI:** `?sort=priority` → `?sort=priority&dir=desc`; bağlantıyla geri dönüldüğünde `aria-sort=descending`
  ve sıra korunuyor. Sayfa: `?page=2` ile "11–20 / 30". **En uzun gerçekçi URL 110 karakter** (sekme + sıra +
  yön + iki sinyal + arama) — makul.
- **Gelecek regresyon riski: 🟢**

### BL-253 — [DÜZELTİLDİ 2026-08-25] İki sütun hiçbir satırı ayırt etmiyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm (76 canlı görev): "Tip" her satırda **Görev**, "Modül" her satırda **Görevler**. Dokuz sütunun ikisi,
  sıralanabilir ve filtrelenebilir hâlde, sıfır bilgi taşıyor. Canlıda 9 sütun → **6 görünür sütun**.
- Sıfır sayaçlı çiple aynı karar, ve `priority` sütununun BL-032'den beri kullandığı **aynı mekanizma**: ayırt
  ediyorsa çiz, etmiyorsa çizme. İkinci bir mekanizma yazılmadı.
- ⚠ Test VERİYE bakıyor, sabit bir listeye değil: ikinci bir sağlayıcı iş göndermeye başladığı an sütun
  kendiliğinden geri gelir. Koda yazıldı ki kimse yokluğunu kusur diye bildirmesin.
- ⚠ Boş liste "ayrım yok" DEĞİLDİR: gösterecek bir şey yokken yargılanacak bir şey de yok, sütun kalıyor.
- **"Yalnız sabitli" filtresi KALDI.** Sabitleme çalışıyor — `item.pinned` gerçekten dönüyor; bugün sıfır
  olması "hiç kullanılmamış" demek, "imkânsız" değil. CT'nin ayrımı burada tam yerine oturdu.
- **Gelecek regresyon riski: 🟢**

### BL-254 — [ÖLÇÜLDÜ, AÇIK] Sabitleme sunucuya gitmiyor — BEŞİNCİ yerel-yalnız yol
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm: sabitle düğmesi yalnız `item.pinned = !item.pinned` yapıyor, hiçbir API çağrısı yok. Canlı doğrulama:
  bir görev sabitlendi (`bfcfa8ba`), sayfa yenilendi, **sabitleme KAYBOLDU**.
- BL-244'ün sayımına eklenmesi gereken beşinci yol. Kıyas: **erteleme** aynı ailedeydi ve artık gerçek bir
  yazma (kodun kendi yorumu bunu söylüyor); sabitleme geride kalmış.
- ⚠ Bu, "Yalnız sabitli" filtresini kaldırma gerekçesi DEĞİL — filtre çalışan bir kontrolü süzüyor. Kusur
  filtrede değil, sabitlemenin kalıcı olmamasında.
- Karar gerekiyor: sabitleme kişisel veri olarak sunucuya mı yazılsın (erteleme gibi), yoksa oturumluk mu
  kalsın? Oturumluk kalacaksa ekranda bunu söylemeli — bugün hiçbir şey söylemiyor.
- **Gelecek regresyon riski: 🟡** — kullanıcı bir şey işaretliyor, sistem unutuyor ve unuttuğunu söylemiyor.


**BL-254 güncelleme (2026-08-25) — KAPANDI: sabitleme sunucuya yazılıyor**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- CT kararı uygulandı, **ertelemenin yolu aynen izlendi**: aynı `TaskPersonalOverlay` satırı (yeni bir tablo
  yok), aynı `PUT {id}/personal/…` deseni, aynı komut/işleyici biçimi, aynı `afterPhase2Write` (iyimser uygulama
  yok — projeksiyon yeniden okunuyor), showcase öğeleri için aynı fixture dalı.
- Yeni alan `TaskPersonalOverlay.Pinned`; **MongoDB olduğu için migration gerekmedi** — eksik alan `false`
  olarak okunuyor. Projeksiyonda `personal.pinned`; `Pinned` "söylenecek bir şey var mı" testine de eklendi,
  yoksa yalnızca sabitlenmiş bir görev `personal: null` yansıtıp işareti kaybederdi.
- ⚠ **İLK CANLI TIKLAMA 404 DÖNDÜ VE SEBEBİ KAYDA DEĞER:** Web tarafındaki `TasksController` bir **vekil** ve
  her uç tek tek yazılmış. Servis tarafında rota hazırken tarayıcı için hâlâ yoktu. Ölçüldü, tahmin edilmedi;
  vekil satırı eklendi ve teste bağlandı.
- **KALICILIK KANITI (gerçek tıklama):** sabitle → `PUT /personal/pin` → **204** → sayfa yenilendi →
  **hâlâ sabitli**, projeksiyonda `personal.pinned: true`.
- **"Yalnız sabitli" filtresi artık gerçekten sonuç döndürüyor:** 30 → **1**.
- Sabitli satırın listedeki yeri ÖLÇÜLDÜ ve **değiştirilmedi**: bugün ayrı bir üste-taşıma yok, satır kendi
  sıralamasındaki yerinde kalıyor. CT'nin "davranışı değiştirme" talimatı gereği dokunulmadı.
- **Gelecek regresyon riski: 🟢** — backend 2342/2342 yeşil.

### BL-255 — [DÜZELTİLDİ 2026-08-25] Liste görünümü hiç sıralanamıyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Yalnız tablo sıralanabiliyordu; `SORTERS`, `state.sortKey` ve `[data-wcn-sort]` işleyicisi duruyordu ama
  hiçbir kontrol onları sürmüyordu (geçen tur ölçülen altıncı ölü yol).
- Kontrol ürünün **mevcut** deseninden: satır taşma menüsünün ve kolon-görünürlüğü menüsünün kullandığı
  `.dropdown` + `.wcn-menu-item` satırları, aynı araç çubuğu yuvasında. Yeni desen icat edilmedi.
- ⚠ **TEK SIRALAYICI, TEK PARAMETRE.** `state.sortKey`/`sortDir`'i sürüyor — grid'in aynaladığı state — yani
  liste↔tablo geçişinde sıra korunuyor ve `?sort=&dir=` iki görünümde aynı şeyi söylüyor. İkinci bir sıralama
  uygulaması, bu ikisinin baştan ayrışmasının sebebiydi.
- ⚠ Tablo için çizilmiyor (kendi başlıklarından sıralanıyor); kanban ve takvim için de çizilmiyor — onlarda
  **düzenin kendisi sıralamadır** (sütun = durum, hücre = gün), orada bir kontrol hiçbir şeyi değiştirmezdi.
- Yalnız iki satırı ayırt edebilen anahtarlar sunuluyor — tablo sütunlarıyla **aynı yardımcı** (`distinguishes`).
- `SortLabel` 7 dilde eklendi.
- **Gelecek regresyon riski: 🟢**

### BL-256 — [ÖLÇÜLDÜ, AÇIK] Takvim yalnız içinde bulunulan ayı gösteriyor, ay değiştirilemiyor
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm: İşlerim'de liste 30 öğe, takvimde **6** öğe. Sebep süzme değil — takvim `data.todayIso`'nun ayını
  çiziyor ve başka bir aya geçecek hiçbir kontrol yok. Canlı veride `dueAt` 76/76 dolu, ama tarihler aylara
  yayılmış.
- ⚠ `plannedDate` — okuyucunun KENDİ tarihi — 76'nın yalnız **4'ünde** dolu. Takvim iki türü ayrı gösterip
  açıklıyor (kırmızı = kaynak son tarih, mor = kişisel plan), yani yanıltmıyor; ama "planlama panosu" vaadi
  bugünkü veriyle karşılanmıyor.
- Karar gerekiyor: (a) ay ileri/geri kontrolü eklensin · (b) takvim "önümüzdeki 30 gün" gibi kayan bir
  pencereye geçsin · (c) bugünkü hâliyle kalsın ve başlık ayın adını taşıdığı için yeterli sayılsın.
- **Gelecek regresyon riski: 🟡** — kullanıcı 30 öğelik bir listeden 6'sını görüp gerisinin olmadığını sanabilir.


**BL-256 güncelleme (2026-08-25) — KAPANDI: takvim gezinilebilir ve dışarıda kalanı söylüyor**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- CT kararı (a) uygulandı: **ay geri / Bugün / ay ileri**. "Bugün"deyken o düğme kapalı.
- Seçili ay URL'ye yazılıyor (`?month=2026-09`), varsayılan ay **URL'de yer kaplamıyor** — "Bugün"e dönünce
  parametre siliniyor. Okuma biçim kontrolüyle (`YYYY-MM`), beyaz listeyle değil: her ay meşru, ama yalnız
  `YYYY-MM` bir aydır.
- ⚠ **CÜMLE, OKLAR OLMASINA RAĞMEN KALDI** — CT'nin ayrı maddesi. Gezinme "bakayım" sorusunu yanıtlar; bu cümle
  "bakacak bir şey var mı" sorusunu. Okuyucu aylarca tıklayarak öğrenmemeli.
  Canlı: Ağustos'ta *"Başka aylarda 24 iş var."* · Eylül'de *"…9 iş var."* · Temmuz'da *"…27 iş var."*
- ⚠ **CÜMLE ÖĞE SAYIYOR, GİRDİ DEĞİL.** Kişisel planı ayrı bir güne düşen bir görev takvimde İKİ kez çiziliyor
  (son tarih + plan, ayrı ayrı açıklamalı). Temmuz'da 4 girdi = 3 öğe ölçüldü; **3 + 27 = 30 = liste.**
- ⚠ **TARİHSİZ ÖĞE AYRI SAYILIYOR** çünkü hiçbir gezinme onlara ulaşamaz. Canlı veride **sıfır** ölçüldü (her
  görevde `dueAt` var) — tam da bu yüzden varsayım değil, koşul olarak yazıldı (`CalOutsideAndUndated`).
- 5 yeni anahtar, **7 dil**.
- **SÜZME KANITI** (İşlerim): çip kapalı → liste 30 · kanban 30 · takvim (görünen + dışarıda) 30.
  Bloke açık → 4 · 4 · 4.
- **Gelecek regresyon riski: 🟢**

### BL-257 — [DÜZELTİLDİ 2026-08-25] Kanban sekmeye göre şekil değiştiriyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Ölçüm: segmentsiz sekmelerde durum sütunları, segmentli sekmede tek "Aktif 30" sütunu — yani İşlerim'de pano
  bir listeden farksızdı. **Tek isim altında iki pano.**
- Artık her sekmede yaşam döngüsü sütunları. Segment süzgeci **çalışmaya devam ediyor**, yalnız sütunları
  belirlemiyor: `activeItems()` zaten "Aktif"e daraltıyor, pano onu aşamalara diziyor.
- Sıra **akışın sırası**: Beklemede → Devam ediyor → Bekliyor → Tamamlandı → İptal edildi. Alfabetik değil,
  çünkü soldan sağa okunabilmesi panoyu listeden ayıran tek şey.
- ⚠ **BOŞ SÜTUN ÇİZİLİYOR, İMKÂNSIZ SÜTUN ÇİZİLMİYOR — ve fark ÖLÇÜLDÜ.** `inTab` terminal işi Geçmiş'e,
  terminal olmayanı diğer sekmelere ayırıyor; yani Tamamlandı/İptal Geçmiş dışında **oluşamaz**, diğer üçü de
  Geçmiş'in içinde. Beşini her yerde çizmek, her panoya iki-üç kalıcı boş sütun koymak olurdu — bu oturumda
  çiplerden ve tablo sütunlarından kaldırdığımız "var olmayan topluluğu vaat etme" sınıfının aynısı.
  **Canlı sayım:** İşlerim 3 sütun (13 · 17 · **0**) · Havuz 3 sütun (2 · **0** · **0**) · Geçmiş 2 sütun (9 · 9).
  Yani ulaşılabilir ama boş aşama çiziliyor ve akış okunuyor.
- Ulaşılabilir her aşama boşsa pano ürünün **boş durum cümlesine** düşüyor — beş başlık altında beş boş kutu değil.
- **Gelecek regresyon riski: 🟢**

### BL-258 — [DÜZELTİLDİ 2026-08-25] Liste sıralaması state'i ve URL'yi yazıyor, SATIRLARI dizmiyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- CT ölçtü: `?sort=priority&dir=desc` ekranda `High · High · High · Medium · Low · High` — yani **Low'dan sonra
  High**, hiç sıralama yok. `renderList` `items.slice().sort(bySla)` ile sabit sıralıyor, `state.sortKey`'e
  bakmıyordu.
- ⚠ **GEÇEN TUR BUNU `aria-sort` OKUYARAK "DOĞRULADI" — o bir TABLO niteliği.** Listede öyle bir nitelik yok ve
  hiç olmadı; kontrol, test edilmeyen bir yüzeye karşı geçti. **Yanlış yüzeyi ölçmek** bu oturumun tekrar eden
  kusuru ve bu, en pahalı örneği: bir özellik teslim edildi diye raporlandı ve hiçbir şey yapmıyordu.
- ⚠ **GELEN KUTUSU DA DÜZELTİLDİ.** Kendi "önce onaylar, sonra SLA" sıralaması vardı; dokunulmasa kontrolün
  çalışmadığı tek sekme olarak kalacaktı — aynı kusur, yüzeyin dörtte birinde. Onaylar hâlâ önde, ama artık
  **bant** olarak; seçilen sıra her bandın içinde uygulanıyor.
- **SABİTLİ SATIR KARARI ve gerekçesi:** sabitleme "buna sonra döneceğim" demek, dolayısıyla onu gömen bir sıra
  sabitlemenin tek işlevini yok eder — ama okuyucunun az önce seçtiği sırayı sessizce ezmek de kendi başına bir
  yalandır. İkisi de **bantlama** ile cevaplanıyor: önce sabitliler, sonra diğerleri, **her bant seçilen sırada**.
  `sort` ES2019'dan beri kararlı olduğu için bant bölmesi üstteki sırayı bozmuyor.
- **KANIT — ham projeksiyondan okundu, ekrandaki çipten değil.** Sekiz satır, dört anahtar, iki yön: **8/8 dizili.**

  | Anahtar | asc | desc |
  |---|---|---|
  | sla | overdue×6 · due-soon | overdue · on-track×5 |
  | title | Ahmet → … | Zeynep-yönü → … |
  | priority | High×6 · Medium×2 | Low · Medium×6 |
  | status | In Progress×5 | Pending×5 |

  Dört `desc` durumunda da `&dir=desc` URL'de korundu. `dir=asc`'in URL'den düşmesi **kusur değil**: varsayılan
  değer, `sort=sla` gibi yazılmıyor ve geri okumada state zaten `asc`'ten başlıyor — round-trip ölçüldü, tutuyor.
- **Gelecek regresyon riski: 🟢**


**BL-258 güncelleme (2026-08-25) — kapanış notu, liste sayfası**

**↑ bu kaydın ÖNCEKİ bloğu — birleştirildi 2026-08-28, silinmedi.**

> ⚠ Bu bir KAYIT DEĞİL, yukarıdaki kaydın daha eski bir hâlidir. Ayrı bir `###` başlığı
> olarak dururken aynı koda iki blok düşüyordu ve biri "kapandı" diğeri "açık" görünüyordu —
> bu dosyada bunun 10 örneği ölçüldü (2026-08-28). O yüzden başlık değil, alıntı.

- Liste sıralaması, sabitleme bantlaması ve Gelen Kutusu'nun kendi sıralayıcısı bu turda kapandı; ayrıntı
  BL-255/258 kayıtlarında.

### BL-260 — [DÜZELTİLDİ 2026-08-25] Tür ekseni iki ayrı kodla çiziliyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- Gelen Kutusu `data-wcn-inbox-type`, diğer üç sekme `data-wcn-typechip`: **bir eksen, iki render'cı, iki
  işleyici, sayma-ve-gizleme kuralının iki kopyası.** Kullanıcı görmüyordu — onu hayatta tutan da buydu.
- Tek `typeChipHtml` + tek işleyici. Sinyal çipleri de aynı elden (`sigChipHtml`) çiziliyor.
- ⚠ **DAVRANIŞ FARKI GERÇEK VE KORUNDU:** Gelen Kutusu **tek-seçim** (birini seçince diğerleri kalkar, "Tümü"
  temizler), diğer sekmeler **çok-seçim**. Bu iki farklı okuma görevine dair bir ürün kararı; iki uygulama
  olarak değil, **tek bir yüklem** (`typesAreSingleSelect`) olarak yaşıyor.
- ⚠ "Tümü" tür çipi değil: ekseni temizler, o yüzden sıfırda da çizilmeye devam ediyor.
- **ÖNCE/SONRA — dört sekmede birebir aynı:**

  | Sekme | Önce | Sonra |
  |---|---|---|
  | Gelen Kutusu | Tümü 19 · Kabul Bekleyen 19 · SLA riski 15 | **aynı** |
  | İşlerim | Görev 36 · Bloke 4 · SLA riski 13 · Ertelenmiş 1 | **aynı** |
  | Havuz | Görev 2 · SLA riski 2 | **aynı** |
  | Geçmiş | Görev 18 · SLA riski 10 · Ertelenmiş 1 | **aynı** |

- URL sözcük dağarcığı değişmedi (`types=`), eski bağlantılar çalışıyor. Canlı: `?tab=islerim&types=task`.
- ⚠ Birleştirme **üç testi kırdı** ve üçü de kuralın ESKİ ADRESİNİ arıyordu. Silinmediler — kuralın yeni tek
  evine taşındılar, ve neden taşındıkları yazıldı. *Eski adrese bakan bir test doğru sebeple kırmızıya döner ve
  silinerek "düzeltilir" — bir kural sessizce böyle zorlanmaz olur.* Üstüne, kuralın kopya sayısını sayan yeni
  bir iddia eklendi.
- **Gelecek regresyon riski: 🟢**

### BL-314 — [KURULDU 2026-08-25] DCP-005 dilim 1: GÖREV TÜRÜ
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

<!-- numara çakışması düzeltildi 2026-08-28: bu kayıt eskiden BL-259 numarasını taşıyordu. Aynı numarada İKİ AYRI iş vardı (takvim ölçümü + DCP-005 dilim 1); ikisi de 2026-08-25 tarihli, biri diğerinin güncellemesi DEĞİL. Emsal: BL-099 (eskiden BL-082). Ölçüldü: BL-259 depoda hiçbir koddan referans almıyordu, o yüzden yeniden numaralamak güvenli. -->
- Kardeşi `TaskFieldDefinition` kopyalandı, yeni desen icat edilmedi: aynı katman bölünmesi (Rules · Handlers ·
  QueryHandlers · Repository), aynı controller rota deseni (`[Route("Tasks/TaskTypes")]`), aynı görünüm ailesi
  (Index · Create · Edit · Details · _DataTable · _Filter · _Form · _IndexL10n), aynı izin adı deseni.
- **Varlık:** `TaskType` — Code (tekil, değiştirilemez) · Name · Description · RecordClass · GqmsDomain (TEK
  değer) · FunctionCode · IsQualityEvent · GroupDocuments[] · LocalDocuments (seyrek) · IsActive · DeletedAt.
- **İzin:** `platform.tasks.task-types.manage` yazma için; okuma `platform.tasks.read`. Manifest'te sayfa ve üç
  aksiyon kayıtlı — **DELETE aksiyonu YOK**, çünkü manifestte ilan edilen aksiyon katalogun sunacağı aksiyondur.
- ⚠ **MongoDB olduğu için migration gerekmedi.** `TaskItem.TaskTypeId` nullable eklendi ve öyle kalacak: canlı
  ölçüm — 77 görevin **76'sı türsüz**, projeksiyon bunu eksiklik değil normal durum olarak yansıtıyor.
- **CANLI ZİNCİR (gerçek tıklama):** tür oluştur (`dev-qms` → **DEV-QMS** normalize) → listede gör → düzenle →
  Code salt okunur → DOM'dan kurcalayıp gönder → **sunucu reddetti, kod korundu** → pasifleştir → onay diyaloğu
  ürünün dilinde → oluşturma seçicisinden **düştü**, yönetim listesinde **kaldı** → türle görev oluştur →
  projeksiyonda `taskType {code,name}` → yenile → duruyor.
- Doküman temizliği canlı ölçüldü: çift UID ve boş satır sessizce ayıklandı (yazanın niyetini değiştirmiyor).

#### ⚠ İKİ ŞEY VERİLDİĞİ SÖYLENDİ, DEPODA YOK — ÖLÇÜLDÜ
1. **19 değerlik FUNCTION listesi DCP-005'te YOK.** Pakette yalnız "Department → FUNCTION mapping | template
   supplied" satırı geçiyor; listenin kendisi hiçbir yerde yok. `FunctionCode` bu yüzden **normalize edilip
   uzunluk sınırlı serbest metin** olarak kuruldu ve `TaskTypeRules` içinde üyelik kontrolü için dikiş yeri
   hazır bırakıldı. **On dokuz değer uydurulmadı**: uydurulmuş bir liste karşı tarafın gerçek kodlarını
   reddeder, uydurma olanları kabul eder ve yanlış kodlanmış her görev sonradan yeniden kodlanır.
2. **`GMG_ERP_Task_Type_Seed_2026-08-24.csv` depoda YOK.** Aranan yerler: tüm depo (`*.csv` → yalnız
   DocumentManagement fixture'ı), `DEV-QMS`/`BATCH-RELEASE`/`SPEC-CONTROL` metin araması → yalnız bu turda
   yazdığım dosyalar. Bu yüzden **"31 satırın sütunları varlığa oturuyor mu"** sorusu **ÖLÇÜLEMEDİ**;
   "doğrulandı" YAZILMADI. Dosya verildiğinde ölçüm on dakikalık iştir.
   - Pakete göre sütunlar `record_class · gqms_domain · is_quality_event · governing_documents`; dördü de
     varlıkta birebir karşılığını buluyor. Oturmama riski taşıyan tek sütun **`function`**: kapalı liste
     bilinmediği için sınırlanmadı.
- **MUTASYON (4, hepsi kırmızı):** Code değiştirilebilir → 1 kırmızı · tür silinebilir → 1 kırmızı ·
  `GqmsDomain` liste alır → **derlenmiyor** (en güçlü hâli) · izinsiz kullanıcı tür açar → 1 kırmızı.
- ⚠ **DÖRDÜNCÜ MUTASYON İLK SEFERİNDE YEŞİL KALDI** ve bu kayda değer: testim iki SABİTİ karşılaştırıyordu,
  rotayı değil — POST rotası `Create`'e çevrildiğinde hiçbir şey kırmızıya dönmedi. Bu oturumda üçüncü kez aynı
  sınıf: **iki sabitin tatmin edebildiği kural, hiçbir şeyin zorlamadığı kuraldır.** Test artık dört yazma
  rotasının `[HasPermission]` niteliğini yansımayla okuyor, ve okuma rotasının `Read` istediğini de ayrıca
  ölçüyor — kuralın iki yarısı da bağlandı.
- **REGRESYON:** backend **2353/2353 yeşil** · frontend **1721 geçti / 9 kırmızı**, dokuzu da dokunulmayan
  Enterprise Strategy. Bir muhafız yeni alanı yakaladı (`diten-field-icons`: 17. alan haritaya eklenmemişti) —
  doğru yakalama, ikon haritaya eklendi.

### BL-315 — [DÜZELTİLDİ 2026-08-25] Sunucu doğrulama mesajları İngilizce geliyordu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

<!-- numara çakışması düzeltildi 2026-08-28: bu kayıt eskiden BL-260 numarasını taşıyordu. Aynı numarada İKİ AYRI iş vardı (tür ekseni birleştirmesi + sunucu mesajı l10n köprüsü); ikisi de 2026-08-25 tarihli, biri diğerinin güncellemesi DEĞİL. ⚠ BL-260 numarası KODDAN referans alıyor (`WorkCenterNext/app.js:1180` ve `tests/wcn-list-counters-and-focus.test.js:192`) ve o iki referans TÜR EKSENİ işini anlatıyor — bu yüzden numara ORADA bırakıldı, yeniden numaralanan bu kayıt (l10n köprüsü) referanssızdı. Ölçüldü. -->
- Canlı ölçüm: salt-okunur kod alanı kurcalanıp gönderildiğinde sunucu **doğru** reddetti ama cümle Türkçe
  formda İngilizce çıktı — *"A task type's code cannot be changed after it is created…"*.
- Şifre kurallarında kurulan köprünün aynısı: servis İngilizce mesajını KORUYOR (yedi çevirisini taşıyan bir
  servis, kuralın ikinci evi olur) ve yanına **kararlı kod** koyuyor — `TASK_TYPE_CODE_IMMUTABLE` ·
  `TASK_TYPE_CODE_TAKEN` · `TASK_TYPE_CLASSIFICATION_INVALID`. Kiracı yüzeyi kodu 7 dilde cümleye çeviriyor;
  haritasız bir kod sunucunun kendi sözlerine düşüyor, sessizliğe değil.
- Yeniden ölçüldü: *"Tür oluşturulduktan sonra kod değiştirilemez: bu türle açılmış görevler onu kimlik olarak
  taşır."*
- ⚠ Ara ölçümde bir kez yanıldım: köprü çalışmıyor sandım, oysa **platform servisini yeniden başlatmamıştım**;
  gelen kod hâlâ `VALIDATION_FAILED`'di. Tanı kaydı ekleyip ölçtüm, sebebi gördüm, kaydı kaldırdım.
- ⚠ İkinci bir ölçüm hatası: pasifleştirme onayının gövdesini okuyup "cümlem gelmedi" sandım. Ortak diyalog ilk
  argümanı **başlık** sayıyor; cümle `options.subtext` ile geçilmeliydi. Dört başka çağrı yerinin aynı hatayı
  yaptığı zaten o dosyanın yorumunda yazılıydı.
- **Gelecek regresyon riski: 🟢**

### BL-261 — [DÜZELTİLDİ 2026-08-25] Menüde Görevler modülünün sayfa adları
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- ⚠ **ÖLÇÜM CT'NİN VARSAYIMINDAN DAR ÇIKTI, ve bu iyi haber:** "modülün tamamı İngilizce" değildi. Nav-görünür
  dört girdinin **ikisi zaten çeviriliydi** (`TASKFIELDDEFINITIONS`, `TASKRECURRENCERULES`), modül adı
  (`Nav.Module.TASKS`) ve alan adı (`Nav.Domain.WORKSPACE`) de vardı. Eksik olan tek nav-görünür sayfa
  **`TASKTYPES`**'tı — yani geçen turda benim eklediğim sayfa.
- ⚠ **VE BUNU BİR MUHAFIZ ZATEN SÖYLÜYORDU:** `NavManifestL10nGuardTests` kırmızıydı ve geçen turda o süiti
  koşmamıştım (`Diten.Web.Tests`, frontend vitest'ten ayrı bir proje). Muhafız manifestten anahtarları kendisi
  türetiyor ve yedi dilde arıyor — yani kural zaten zorlanıyordu, ben bakmamıştım.
- Yine de yedi sayfanın hepsine anahtar eklendi (**5 yeni × 7 dil**): nav-görünmez olanlar (`TASKS`,
  `TASKCREATE`, `TASKDETAIL`, `TASKEDIT`) menüde çizilmiyor ama Ctrl+K ve başka yüzeyler aynı köprüden geçiyor.
- ⚠ Manifest `DisplayName`'lerine **dokunulmadı** — onlar çeviri bulunamazsa görünecek geri düşüş.
- **Olay adları (`FallbackDisplayName`) ÖLÇÜLDÜ: farklı bir aile.** Bildirim olayı tanımlarına ait
  (`NotificationEventDefinition`), nav köprüsünden geçmiyor. Dokunulmadı; kapsamı ayrı bir iş.
- **YEDİ DİLDE MENÜ (canlı, `.AspNetCore.Culture` çerezi ile ölçüldü):**

  | | Görev Merkezi | Alan Tanımları | Görev Türleri | Yinelenen Kurallar |
  |---|---|---|---|---|
  | en | Task Center | Field Definitions | Task types | Recurring Task Rules |
  | tr | Görev Merkezi | Alan Tanımları | Görev türleri | Yinelenen Görev Kuralları |
  | fr | Centre des tâches | Définitions de champs | Types de tâche | Règles de tâches récurrentes |
  | es | Centro de tareas | Definiciones de campos | Tipos de tarea | Reglas de tareas recurrentes |
  | zh | 任务中心 | 字段定义 | 任务类型 | 重复任务规则 |
  | ar | مركز المهام | تعريفات الحقول | أنواع المهام | قواعد المهام المتكررة |
  | ru | Центр задач | Определения полей | Типы задач | Правила повторяющихся задач |

- **Gelecek regresyon riski: 🟢** — muhafız her yeni nav-görünür sayfayı yedi dilde zorluyor.

### BL-262 — [DÜZELTİLDİ 2026-08-26] `FunctionCode` kapalı listeye — ve bir KESİNTİ bulundu
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- 19 değer DCP-005 §6.7'den **birebir** alındı, uydurma yok. Form serbest metin kutusundan **seçiciye** döndü
  (19 + "Fonksiyon yok"), 19 etiket × 7 dil eklendi. `TaskTypeRules`'taki dikiş yeri üyelik kontrolüyle kapandı.
- ⚠ **CT'NİN "var olan kayıtları ÖLÇ" TALİMATI BİR KESİNTİYİ AÇIĞA ÇIKARDI.** Alanı gerçek bir `enum` yaptım;
  ekran **500** verdi. Sebep: geçen turun canlı testinde `qa` yazmıştım, saklanan değer **`QA`** — pakette `QA`
  yok, **`QUA`** var. Mongo sürücüsü DESERIALIZATION sırasında `FormatException: Requested value 'QA' was not
  found` fırlattı: **tek bir eski değer, görev türü listesinin tamamını ve görev formundaki seçiciyi düşürdü.**
- **Karar:** alan **string** kalır, **liste YAZMADA kapanır** (`ParseFunctionCode`). Gerekçe:
  belge deposunda migration yok — yazılan yazılı kalır. Saklananı **temsil edemeyen** bir tip, veri sorununu
  kesintiye çevirir; iki alternatif (okumada değeri düşürmek, satırı reddetmek) sessiz veri kaybıdır.
  Böylece listeye uymayan yeni bir değer **giremiyor**, var olan **okunabilir ve görünür biçimde uyumsuz**
  kalıyor — CT'nin "sessizce silme" şartı da karşılanıyor.
- Canlı ölçüm sonrası: `DEV-GMP · fn=MFG` (yeni, geçti) · `DEV-QMS · fn=QA` (eski, duruyor);
  listeye uymayan kodla yazma denemesi **reddedildi ve hiçbir şey yazılmadı**, mesaj Türkçe:
  *"Bu, kurumun kullandığı fonksiyon kodlarından biri değil."*
- ⚠ Kalan iş: `DEV-QMS`'in `QA` değeri **elle düzeltilmeli** (`QUA` olmalı). Otomatik dönüştürme yazmadım —
  `QA` → `QUA` benim varsayımım olurdu; kaydı açan kişi karar vermeli.
- **ORG listesi (5 değer) koda GİRMEDİ** — yerel doküman katmanı ertelendi, pakette duruyor.
- **Gelecek regresyon riski: 🟢**

### BL-264 — [DÜZELTİLDİ 2026-08-26] Klasör adı alt sınırı 3 → 2
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- `QmsFolderPathNormalizer`: `< 3` → `< 2`. Üst sınır **120 aynen** duruyor, tek karakter **hâlâ reddediliyor**.
- Gerekçe koda yazıldı: `HR` · `RA` · `PV` · `QA` bu sektörün standart kısaltmaları ve ikisi QA'nın kendi
  FUNCTION listesinde. Karşı tarafın kendi sözcüklerini reddeden bir kural dikkatli değil, katıdır.
- ⚠ **103 SATIRLIK TAKSONOMİ CSV'Sİ DEPODA YOK** — arandı: `*.csv` içinde yalnız
  `00_all_folders_2175.csv` (farklı bir fixture, 2176 satır) var. Bu yüzden prova ucu **koşulmadı** ve
  "103/103 geçti" **YAZILMADI**. Kural bunun yerine testle kilitlendi: `HR`·`RA`·`PV`·`QA` geçiyor, tek karakter
  reddediliyor, tavan hem 120'de geçip hem 121'de reddedilerek iki yönlü ölçülüyor.
- **Gelecek regresyon riski: 🟢**

### BL-265 — [KURULDU 2026-08-26] DCP-005 dilim 2: DOKÜMAN ARAMA LİSTESİ
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **358 kontrollü doküman bir ARAMA LİSTESİ olarak yüklendi. Tablo kurulmadı.** `documents` diye
  düzenlenebilir bir varlık yok: ne update komutu, ne edit ekranı, ne manifest'te bir EDIT aksiyonu — tek
  aksiyon IMPORT. Gerekçe §6.1: tablo olursa birisi er geç bir başlığı düzeltir ve dokümana ikinci bir otorite
  doğar; listede düzeltilecek kayıt yoktur.
- `ControlledDocument` **kullanılmadı** — `CollectionInstanceId` zorunlu kılıyor ve referans tarafında klasör
  yok. Yeni varlıklar: `DocumentReferenceListVersion` (sürüm) + `DocumentReferenceEntry` (satır).
- **17 sütunun 17'si de okunuyor.** Okunmayan sütun raporlanıyor (`unreadColumns`), sessizce atlanmıyor —
  canlı ölçümde **boş** döndü. Karşı tarafın dosyası **düzenlenmedi**.
- ⚠ **Kendi CSV ayrıştırıcısı yazıldı ve sebebi ölçüldü:** kayıt defterinin kendi bulgu cümlesi
  `link_blocked_reason` içinde **tırnak içinde virgül** taşıyor — yani naif bölme, tam da bir dokümanın neden
  bağlanamadığını açıklayan sütunu bozuyor. Test bunu ayrıca ölçüyor.

#### CANLI ÖLÇÜM (gerçek dosya, gerçek uç)
| Ölçüm | Sonuç |
|---|---|
| prova (dry-run) | 358 satır · **322 seçilebilir** · **36 bloke** · 0 hata · 0 okunmayan sütun |
| işle (1. yükleme) | **201**, sürüm `2026-08-24`, hash `a52205a8…` |
| **aynı dosya 2. kez** | **409 `DOCUMENT_LIST_ALREADY_IMPORTED`** — *"already stored as list version '2026-08-24'"* |
| saklanan sürüm sayısı | **1** (ikinci yükleme hiçbir şey yazmadı) |
| arama `GMG-QMS-SOP-0005` | bulundu: *Deviation and Incident Management*, V0.4, Draft, zorunlu Grup SOP'u |
| bloke satır | `GMG-GDP-SOP-0001` · `NOT REGISTERED` · gerekçesiyle **dönüyor**, gizlenmiyor |

- **SÜRÜM DESENİ taksonomininki:** anlamsal sürüm + SHA-256 içerik hash'i + kaynak anahtarı — ikinci bir
  sürümleme mekanizması yazılmadı. Satırlar sürümler arasında **taşınmıyor**; her yükleme kendi satırlarını
  taşır, böylece "bu görev hangi listeyi gördü" sorusu cevaplanabilir kalıyor.
- ⚠ **Aynı dosyanın ikinci kez yüklenmesi YENİ SÜRÜM ÜRETMİYOR, tanıyor.** Karar gerekçesi koda yazıldı: aynı
  öğleden sonra iki kişinin kayıt defterini yüklemesi iki "güncel" liste üretmemeli, çünkü ardından gelen soru
  hep "görev hangisine karşı çözümledi" olur ve iki cevap cevap değildir.
- ⚠ **BLOKE SATIRLAR GİRİYOR, GÖRÜNÜYOR, SEÇİLEMİYOR.** 23 planlı · 7 geri çekilmiş · 6 kayıtsız (QA'nın kendi
  açık bulgusu) — hepsi listede, hepsi gerekçesiyle. Bu, **sıfır sayaçlı çipleri gizleme kararının TERSİ** ve
  bilerek öyle: orada topluluk yoktu, burada doküman var ve bağlanamıyor. Gerekçesiz bir bloke satır **içe
  aktarma hatası** sayılıyor — "bunu kullanamazsın" cümlesinin "çünkü"süz hâli bu oturumun kaldırdığı sınıf.
- İçe aktarma **hep ya da hiç**: ayrıştırma hatası varsa sürüm de satır da yazılmıyor.
- İzin `platform.tasks.document-list.import` (yazma) · arama `platform.tasks.read` — dokümana atıf sıradan iş,
  kayıt defterini değiştirmek değil. Manifest'te sayfa + tek aksiyon; `Nav.Page.TASKDOCUMENTLIST` 7 dilde.
- ⚠ **BİR MUTASYON İLK SEFERİNDE YEŞİL KALDI (bu oturumda dördüncü kez).** Sürüm tanıma kontrolünü sildim,
  hiçbir test kırmızıya dönmedi: parser testleri hash'in **kararlılığını** ölçüyordu, **karşılaştırıldığını**
  değil. *Kimsenin karşılaştırmadığı bir hash, sürüm değil sağlama toplamıdır.* İçe aktarma katmanına dört test
  eklendi; mutasyon tekrarlandı, kırmızıya döndü.
- **REGRESYON:** backend **2375/2375** · frontend **1721 geçti / 9 kırmızı** (hepsi dokunulmayan Enterprise
  Strategy) · **`Diten.Web.Tests` 46/46** (bu tur ayrıca koşuldu, CT'nin talimatı) · FG-003 0 · KNOWN_RAW 12.
- **BU TURDA YAPILMAYANLAR (dilim 3/4):** göreve bağlama · dondurma altılısı · görev türünün
  `governing_documents`'ını listeye bağlama · taksonominin commit'i.

### BL-266 — [KURULDU 2026-08-26] Doküman listesi yönetim ekranı (DCP-005 dilim 2 tamamlama)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **TAŞIMA ÇELİŞKİSİ ÖLÇÜLDÜ VE YOKTU.** Emsal (taksonomi sihirbazı) tarayıcıdan **MVC'ye** `multipart`
  gönderiyor; MVC base64'leyip gateway'e JSON yolluyor. Yani iki taşıma iki ayrı yüzey değil, **aynı yolun iki
  adımı** — ve bizim ucumuz zaten emsalinkiyle aynı sözleşmede (`ContentBase64`). Karar: **ekran emsale
  hizalandı**, uç değişmedi. Gerekçe: dosyayı tarayıcıda base64'lemek onu bellekte ikiye katlar ve dosya-imzası
  hesabını zorlaştırır; ayrıca gateway sözleşmesi zaten commit edilmiş.
- **Emsalin en önemli davranışı kopyalandı: dosya değişirse prova geçersiz olur, içe aktarma kilitlenir.**
  Canlı ölçüm: başta kilitli → prova sonrası açık → dosya değişince **yeniden kilitli**. Sunucunun 409'u bu
  hatayı ancak İŞ İŞTEN GEÇTİKTEN sonra yakalıyor; kilit onu ulaşılamaz kılıyor.
- **409 HATA DEĞİL, BİLGİ.** Canlı: `alert alert-info`, gövde metni okuyucunun dilinde. İlk hâlinde sunucunun
  İngilizce cümlesini basıyordu; görev türlerinde kurulan **kararlı kod köprüsü** buraya da uygulandı
  (`DOCUMENT_LIST_ALREADY_IMPORTED` → 7 dil), haritasız bir kod sunucunun sözlerine düşüyor.
- **Bloke satır görünür, seçilemez, gerekçesi okunur — ve bu RENKTEN FAZLASIYLA söyleniyor.** Canlı ölçüm
  (`GMG-GDP-SOP-0001`): satırda `aria-disabled="true"`, gerekçe METİN olarak satırın içinde, soluk sınıf ancak
  üçüncü sinyal. Yalnız griyle anlatılan bir "seçilemez", ekran okuyucu kullananın seçmeye çalışacağı satırdır.
- **32 anahtar × 7 dil.** Manifest `DisplayName` İngilizce bırakıldı — nav köprüsü stabil koda göre çeviriyor.
- ⚠ **MANİFEST GÖRÜNÜRLÜĞÜ ANCAK EKRAN AÇILDIĞI ÖLÇÜLDÜKTEN SONRA `true` YAPILDI.** CT'nin düzelttiği kusur:
  sayfa görünür yayınlanmıştı ama ne görünüm ne rota vardı — kenar çubuğu bir sonraki uzlaştırmada 404'e giden
  bir öğe kazanacaktı. Kural koda yazıldı: *manifestteki görünür bir sayfa, ekranı açıldığı ölçülen turda
  yayınlanır.* Test bunu üç parça olarak ölçüyor: manifest `true` · görünüm dosyası var · `[HttpGet]` rotası var.
- ⚠ **EMSALİN KONTRAST KUSURU KOPYALANMADI.** `.qms-steps` üç adımlı göstergesi ölçülmüş bir AA ihlali taşıyor
  (1.83 açık / 2.02 koyu, `--bs-secondary-color`). İşaretlemeyi kopyalamak kusuru ikinci ekrana taşırdı; yerel
  düzeltmek 197 kuralın bağlı olduğu bir ürün tokenını çatallardı. İki adım **kelimelerle** adlandırıldı, yeni
  renk gerekmedi. Token düzeltmesi ayrı dal (CT talimatı).
- **MUTASYON (3, ÖNCE koşuldu, üçü de kırmızı):** bloke satır seçilebilir · 409 hata olarak gösterildi ·
  dosya-değişti kilidi kaldırıldı.
- ⚠ Testimin kendi kusurunu yakaladım: 409 dalını **tam metne** çakmıştım; dala reason-code kontrolü eklenince
  davranış bozulmadan test kırıldı. *Büyümesine izin verilen bir koda çakılı pencere, pencere değildir* —
  koşulun kendisine bağlandı.
- **İKİ GENİŞLİK × İKİ TEMA (900/1440 × açık/koyu):** sayfa yatay kaymıyor; **sürüm tablosu kendi kabında**
  kayıyor; arama tablosu sığıyor; içe aktarma düğmesi doğru durumda.
- **REGRESYON:** frontend **1731 geçti / 9 kırmızı** (dokuzu da dokunulmayan Enterprise Strategy — bu turdan
  önce de kırmızıydı) · backend **2375/2375** · **`Diten.Web.Tests` 46/46** · FG-003 **0** · KNOWN_RAW **12**.

### BL-268 — BL-267 KAPANDI: sürüm geri çekme kuruldu (2026-08-26)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Durum: KURULDU.** CT (a) şıkkını seçti; `DocumentReferenceListVersion` artık `WithdrawnAt` /
  `WithdrawnReason` / `WithdrawnBy` taşıyor ve emsali `TaskComment.WithdrawnAt`.
- **Neden `DeletedAt` değil:** yumuşak silme satırı yürütme süzgecinden geçen HER okumadan düşürür — bir
  işaretin yapmaması gereken tam olarak budur. Geri çekilen sürüm listede kalır, "geri çekildi" damgasıyla
  görünür; yalnız "güncel" yarışından çıkar.
- **İki yarım ayrı ayrı ölçüldü** (kuru çalıştırma ile, yan etkisiz): geri çekilen baytlar yeniden
  yüklenebilir (`alreadyImportedAsVersion = null`), yaşayan baytlar hâlâ 409 verir. Yani (1) numaralı kuralın
  koruduğu şey kaybolmadı.
- **Gerekçe zorunlu**, ve ikinci bir geri çekme 409 ile reddedilir — ilk damganın üzerine yazılmasın diye.
- **Geliştirme kiracısı düzeltildi:** `TEST-1` gerçek tıklamayla, "doğrulama artığı" gerekçesiyle geri
  çekildi; 358/322/36 satırlık `2026-08-24` listesi yeniden güncel. Sahibin kuralına uyularak BAŞKA hiçbir
  test artığına dokunulmadı.
- **Gelecek regresyon riski: 🟢 katkısal** — üç alan eklendi, hiçbir okuma yolu daralmadı; eski satırlarda
  alanlar `null` ve `null` "geri çekilmemiş" demek.

### BL-273 — DCP-005 dilim 3 KURULDU: görev → doküman atfı (2026-08-26)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Altı alan donuyor** — `document_uid · document_code · title · version · status · referenced_at` — artı
  hangi liste sürümünden okundukları (`ListVersionId`). `TaskDocumentReference`'ın her özelliği `init`-only:
  ayarlanabilir bir özellik davettir, ve buradaki davet tam olarak yapılmaması gereken şeydir.
- **Dondurmanın tek yazma yeri** `TaskDocumentReferenceFreezer`. Var olan bir atfı ASLA yeniden çözümlemez:
  el yordamına görevin MEVCUT atıfları verilir, değişmeyenler dokunulmadan geri döner. "Başlık donmuştur"u
  temenni olmaktan çıkarıp doğru yapan şey bu — güncelleme, hiç çözümlemediği şeyi tazeleyemez.
- **Bloke satır iki yerde birden reddediliyor**: seçici gösterir ve seçtirmez (okuyucu NEDEN'i görsün), sunucu
  da reddeder (ekran bir sınır değildir; API çağıranı seçiciden geçmez).
- **Canlı kanıt (asıl ölçüm):** atıf yapıldıktan sonra kütüğe aynı UID'i `GMG-QMS-SOP-9999 / YENIDEN
  ADLANDIRILDI / V9.9` diye yazan yeni bir sürüm yüklendi. Eski görev hâlâ `GMG-QMS-SOP-0005 / Deviation and
  Incident Management / V0.4` okuyor; aynı anda arama kutusu yeni adı buluyor. Aynı UID, iki cevap, tek ekran.
- **Geri çekilmiş sürüme atıf okunmaya devam ediyor**, ama yeni atıf ondan alınamıyor — iki yarım ayrı ölçüldü.
- **Gelecek regresyon riski: 🟢 katkısal** — `TaskItem`'a bir liste, iki isteğe bağlı sondaki alan; null "atıf
  düzenlenmiyor" demek, yani eskiden yazılmış her yük aynen geçerli.

### BL-275 — dilim 1'in kendi yolunda ÜÇ sessiz kayıp + bir DI tuzağı (2026-08-26, hepsi düzeltildi)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

Üçü de derleme yeşilken, test yeşilken, ekranda doğru görünürken kayıp veriyordu. Hepsi canlı bulundu.
- **(a) `readForm` `taskTypeId`'yi hiç okumuyordu.** Yük oluşturucuda `taskTypeId: trimOrNull(draft.taskTypeId)`
  zaten vardı; taslak onu hiç taşımıyordu. DEV-QMS görünür şekilde seçiliyken oluşturulan görev
  `taskTypeId: null` olarak kaydedildi. GxP kaydında tür, kayıt sınıfını taşıyan alandır — yani hiç olmayan
  bir sınıflandırma.
- **(b) `writeForm` türü geri yazmıyordu.** Düzenleme formu türü olan bir görevde "Tür yok" açılıyordu ve
  kaydetme tam değiştirme — başlığı düzelten biri sınıflandırmayı siliyordu. Değer, seçicinin seçenekleri
  geldikten SONRA uygulanıyor: `<select>` tanımadığı değeri sessizce yutar, kusurun yarısı buydu.
- **(c) `LoadApiModelAsync` yöneten dokümanları metin kutusuna doldurmuyordu.** API liste döner, form metin
  düzenler, ikisini bağlayan bir şey yoktu. İki dokümanı olan bir tür boş formla açılıyordu; Kaydet ikisini de
  siliyordu, başarı mesajıyla. BL-024'ün aynı şekli: yazma yolu doğru, OKUMA yolu ona korunacak şeyi hiç vermiyor.
- **(d) DI tuzağı:** dondurucu el yordamlarında **isteğe bağlı** argüman (var olan test kurulumları bozulmasın
  diye). Kaydı unutmak derlenir, 2392 testin hepsini geçer, ve çalışma zamanında her atfı sessizce düşürür —
  ölçüldü: form iki doküman gösterdi, görev sıfır tane kaydetti. Kayıt artık testle çivili.
- **Ders:** "isteğe bağlıya çevirerek geriye uyumluluk" ucuz görünür; bedeli, unutulduğunda HİÇBİR ŞEY
  söylemeyen bir dikiş yeridir. Böyle her dikişin kendi kaydını doğrulayan bir testi olmalı.
- **Gelecek regresyon riski: 🟡** — (a) ve (b) düzeltildi ama daha ÖNCE oluşturulmuş görevlerde tür `null`
  kaldı; bu görevlerin kayıt sınıfı geriye dönük olarak doğmayacak.

### BL-278 — "EnsureIndexesAsync" iki VERİ işi çalıştırıyordu; ayrıldı, kaybolmadı (2026-08-26, düzeltildi + çivilendi)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

Metodun adı "indexleri kur" diyordu; yaptığı üç işti ve ikisi satır yazıyordu.
- **(a) `SoftDeleteDomainsForDeletedTenantsAsync`** (eski satır 1106) — silinmiş kiracıların `tenant_domains`
  satırlarını soft-delete eden bir VERİ ONARIMI. Her açılışta koşuyordu.
- **(b) `moduleCatalogDocuments.UpdateManyAsync(Unset("Category"))`** (eski satır 1214) — emekliye ayrılmış
  `Category` alanını tüm modül kataloğundan silen bir VERİ GÖÇÜ. Her açılışta koşuyordu.
- **Sınıflandırma:** ikisi de şema değil, açılış yükümlülüğü. Yeni yerleri
  `Persistence/Schema/PlatformSchemaMigrations.cs`; manifest tamamen bildirimsel kaldı.
- **Neden profile KOYULMADI:** bir test profili, kullandığı 4 koleksiyonu kursun diye çağrılır. O çağrı aynı
  zamanda bir veri göçü çalıştırsaydı, "ucuz" yol dosyanın en pahalı ve en geri alınamaz davranışını, testin
  kendisinin sandığı bir veritabanına taşırdı.
- **Neden üretimden KAYBOLMADI:** ayırmak tam da bir açılış işinin sessizce düştüğü yoldur — yeni dosyaya
  taşınır, yeni dosyayı kimse çağırmaz, hiçbir şey patlamaz, onarım sadece olmamaya başlar. Bu yüzden
  `PlatformSchemaContractMongoTests.TheProductionPathStillBuildsEverythingAndRunsBothDataJobs` ikisinin de
  DAVRANIŞINI ölçüyor (silinmiş kiracının domain'i soft-delete oldu mu; `Category` alanı silindi mi), metodun
  varlığını değil. Mutasyon: iki çağrıdan birini yorum satırı yap → kırmızı, adını söyleyerek.
- **Sıra değişikliği (bilinçli):** 13 `DropIndexIfExists` çağrısı artık manifest'ten ÖNCE topluca koşuyor,
  eskisi gibi araya serpiştirilmiş değil. Korunan özellik aynı: tanımı değişen bir index yeniden kurulmadan
  önce düşürülmeli (yoksa `IndexOptionsConflict`). Tek gerçek sıra bağımlılığı — unique `CodeKey` index'inin
  `ModuleDomainDeduplicationMigration`'dan sonra gelmesi — etkilenmedi; o göç DI açılışında, bu metottan önce
  koşuyor.
- **Gelecek regresyon riski: 🟡** — üretim yolunun tamamı tek bir Mongo testine bağlı; o test atlanırsa (ör.
  mongod yokken) iki iş de sessizce düşebilir. Testin skip-if-unavailable kaçamağı YOK, kasıtlı olarak.

### BL-298 — BRD profil index bütçesi 18'de doluydu; tavan 19'a çıktı, index kondu (2026-08-27 → 2026-08-28, KAPANDI)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

**SAHİP KARARI (2026-08-28):** `MaxLogicalIndexes` 18 → **19**. `MaxCollections` **8'de KALDI**.
`business_reference_data_validation_results` BRD profilinde kaldı; tavanı korumak için ölçülmemiş başka bir
index düşürülmedi. Sahip aynı mesajda kuralı da yazdı: *"Index bütçe kapısı index artışında da geçerlidir —
manifest + budget + gerçek-Mongo contract testi BİRLİKTE güncellenmeli."*

Konan index: `{TenantId, BusinessReferenceDataVersionId, RuleId}`, adı
`ix_business_reference_data_validation_results_tenant_version_rule`. ESR-tam: iki eşitlik + `RuleId` sıralama.
`GetValidationResultsByVersionAsync`'i tam, `ReplaceValidationResultsAsync`'in `DeleteMany` legini ön ekiyle
karşılar. Profil şimdi 8 koleksiyon / 19 mantıksal index (11 beyan + 8 örtük `_id`) — yani tam tavanda.

- **PARTIAL FILTER KONMADI, VE BU BİR ÖLÇÜM.** Sahip açıkça "mevcut standart uygunsa `IsDeleted=false` partial
  ile explain yeniden doğrulansın; ölçüm tersini gösterirse varsayımla eklenmesin" dedi. Ölçüm tersini
  gösterdi. Canlı verinin kopyasında (250 satır, 10 farklı (tenant,version), en büyüğü 25), dört sayı:

  | sorgu | index yok | plain index | + partial `IsDeleted=false` |
  |---|---|---|---|
  | okuma `{Tenant,Version,IsDeleted:false}` sort `RuleId` | `SORT->COLLSCAN`, **250** belge | `FETCH->IXSCAN`, **25**, SORT yok | `FETCH->IXSCAN`, **25**, SORT yok |
  | silme legi `{Tenant,Version}` (IsDeleted yok) | `COLLSCAN`, **250** belge | `FETCH->IXSCAN`, **25** | `COLLSCAN`, **250** ⛔ |

  Okumada iki varyant AYNI. Fark yalnız silmede, ve partial olan kaybediyor: `ReplaceValidationResultsAsync`
  `{TenantId, VersionId}` ile siliyor, `IsDeleted` yüklemi YOK — Mongo bu sorgunun partial ifadenin alt kümesi
  olduğunu kanıtlayamaz ve index'i reddeder. Profildeki diğer yedi index'in hepsi partial taşıdığı için bir
  sonraki okuyucu bunu "ev standardına aykırı" görüp düzeltmeye kalkacak; `PlatformSchemaContractMongoTests`
  silme legini `explain: {delete: …}` ile ayrıca çiviliyor — partial eklendiği an kırmızı, silmeyi adıyla
  söyleyerek. (`find` ile ölçmek yetmezdi: aynı filtreli `find` partial index'te mutlu mesut IXSCAN raporlar.)
- **Bütçe kapısı, artış yönünde de kuruldu.** `DeclaredBudgetsAreRespected` "manifest tavanın altında mı" diye
  sorar — onu yeşile döndürmenin yolu tavanı yükseltmektir, yani `SchemaProfileBudget` başlığının uyardığı
  hareketin ta kendisi. `MaxCollections`'ı 8'den 9'a çekmek o testi kırmızı bile yapmaz. Bu yüzden tavan
  DEĞERİ artık `PlatformSchemaManifestTests.TheDeclaredBudgetsAreTheNumbersTheOwnersApproved` ile çivili
  (8 / 19); iki sayıdan biri değişirse kırmızı olur ve testi güncellemek "bir sahip onayladı" demektir.
- **Sayının ikinci kopyası vardı ve bu turda temizlendi.** `BusinessReferenceDataMongoResidueSweeperTests`
  `<= 18`'i düz sayı olarak yazıyordu; sahip onayladığı artıştan sonra, ne bütçeyi ne kararı adıyla anan bir
  residue-sweeper dosyasında kırmızıya döndü. Artık `SchemaProfileBudget.BusinessReferenceData`'dan okuyor.
- **Bu turda YAPILMADI (sahip açıkça yasakladı):** `ImportedAt` index'i (BL-299 kapsamında), global
  `DateTimeOffsetSerializer` değişikliği, `DateTimeOffset` işinin bu tura karıştırılması, başka index düşürmek,
  koleksiyon bütçesini değiştirmek.
- **Gelecek regresyon riski: 🟢** — profil tavanda ama tavan artık çivili; index'in her iki call site'ı plan
  seviyesinde teste bağlı. ⚠ Bir sonraki BRD index'i BL-279/BL-298'in bulunduğu yerde olacak: ölçümle
  sahibine gidecek. Tavan tam dolu olduğu için bu kapı artık teorik değil, ilk index'te çalışacak.

### BL-280 — profil sertleştirmesi bir testi doğru sebeple kırmızıya çevirdi (2026-08-26, düzeltildi)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

`BusinessReferenceDataUsageLookupMongoTests` iki satırı AYNI `(TenantId, SetCode, ConsumerModule,
ConsumerName)` ile ekliyordu. Üretimde bu kombinasyon **unique index** ile yasak. Test yıllarca yeşildi çünkü
eski `MongoIntegrationHarness` HİÇ index kurmuyordu — yani test, üretimde var olmayan bir şemaya karşı
koşuyordu. Harness profil kurmaya başladığı an Mongo ikinci insert'i reddetti.
- Düzeltme: her satır kendi tüketicisini alıyor (`Organization` / `LegalEntity`). Test edilen sıralama
  davranışı değişmedi — iki satır gerekiyordu, iki AYNI tüketici değil.
- **Ders:** "index'siz test veritabanı" ucuz görünür; bedeli, üretimin reddedeceği veriyi kabul eden ve bunu
  hiç söylemeyen bir süittir. Kaç testin daha bu durumda olduğu ÖLÇÜLMEDİ (BRD ve MDM tarafı taşınmadı).
- **Gelecek regresyon riski: 🟢** — düzeltildi ve artık gerçek index altında koşuyor.

### BL-282 — test artıkları artık kendi kendini topluyor (2026-08-26, kuruldu + kanıtlandı)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

Harness bittiğinde kendi veritabanını düşürüyordu; ama "bittiğinde" hiç gelmiyor, çünkü bu işin tamamının
sebebi mongod'un koşu ortasında ölmesi. Ölçüldü: bu makinede 19 veritabanının **6'sı test artığıydı**, üçü
harness'ın iki aşama önce bıraktığı bir adlandırmadan kalma. Elle kimse temizlemezdi.

**İkinci savunma hattı:** `MongoResidueSweeper` — koşunun BAŞINDA, terk edilmiş artıkları düşürür.
- **Silmeye izin veren dört koşulun hepsi gerekli:** (1) ad, harness'ın üretebileceği dilbilgisine uyuyor
  (`diten_platform_itest` **tam segment** olarak + isteğe bağlı `_token`'lar); (2) veritabanı **bizim
  yazdığımız işareti** taşıyor (`__diten_harness_marker`); (3) işaretteki koşu kimliği ŞU ANKİ koşu değil;
  (4) işaret yaş eşiğini (1 saat) aşmış.
- ⚠ **Yük taşıyan kural ad değil, İŞARET.** Bu oturumda dizgi eşleşmesine dayanan muhafızların ne kadar
  zayıf olduğunu iki kez ölçtük. Bu harness'ın yaratmadığı bir veritabanı işaret taşımaz ve adı ne olursa
  olsun silinemez — üretim, `admin`/`config`/`local`, bir başkasının çalışma veritabanı, hepsi bu yüzden
  erişilemez.
- ⚠ **Önek parametre DEĞİL.** `SweepAsync` string parametre almıyor; çağıran öneki genişletemez. "Hangi
  öneki sileyim?" diye soran bir temizleyici, geliştirme veritabanını düşürmekten bir yazım hatası uzaktır ve
  o hata kimsenin dikkatle incelemediği bir test dosyasında yaşar. Bir test bunu refleksiyonla çiviliyor.
- ⚠ **Temizlik hatası test hatası olamaz.** Süpürme koşunun en başında, herhangi bir assertion'dan önce
  çalışır; fırlatsaydı ilk testi kendisiyle ilgisiz bir sebeple kırmızıya çevirir, altındaki gerçek kusur da
  "temizlik flaky" diye elenirdi. Sorunlar **döndürülüyor** ve ayrıca stderr'e yazılıyor.
- **İşaret her açılışta yeniden damgalanıyor**, bu yüzden aynı makinede paralel koşan ikinci bir süitin
  veritabanları her zaman yaş penceresinin içinde kalır. Bu koşul olmasa bir süit diğerininkini test
  ortasında silerdi ve kurbanın hataları tekrar üretilemez olurdu.
- **Uçtan uca kanıt (ölçüldü):** üç veritabanı ekildi — (a) sahip önek + geçerli, bayat, başka koşuya ait
  işaret; (b) aynı önek, işaret YOK; (c) `diten_platform_itestX` + geçerli bayat işaret. Koşu sonrası:
  **yalnız (a) silindi**, (b) ve (c) yerinde. 19 → 18 veritabanı.
- **Tek seferlik boşluk:** işaret mekanizmasından ÖNCE kalmış artıklar (bu makinedeki 3 GUID adlı) işaret
  taşımadığı için asla süpürülmez; onlar elle kaldırıldı (bu turda, ölçüm için). Bundan sonrası otomatik.
- **Gelecek regresyon riski: 🟢** — dört koşulun her biri ayrı bir mutasyonla çivili; birini kaldır, tam
  karşılığı olan test kırmızıya döner.

### BL-285 — dondurucu kaydı artık dizgiyle değil, AÇILIŞLA korunuyor (2026-08-27, düzeltildi)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

`TaskDocumentReferenceFreezer`, iki görev yazma işleyicisinde **isteğe bağlı** (`= null`) argümandı. Kaydı
unutmak derleniyor, her birim testini geçiyor ve çalışma zamanında her atfı sessizce düşürüyordu (canlı
ölçüm 2026-08-26: form iki doküman gösterdi, kaydedilen görev sıfır taşıdı).

Bu, `DependencyInjection.cs`'i diskten okuyup içinde `"AddScoped<…TaskDocumentReferenceFreezer>()"` dizgisi
arayan bir testle çivilenmişti. İki ayrı kusur: (a) konteyner hakkında hiçbir şey kanıtlamıyor — kayıt metin
olarak var olup erişilemez olabilir, başka türlü yazılmış çalışan bir kayıt ise testi düşürürdü; (b) kendi
kaynak ağacına derleme çıktısından **beş dizin tırmanarak** ulaşıyordu.

**Yapısal çözüm (DCP-005 §6.1), iki parça — ikisi de gerekli:**
1. Argüman **zorunlu** yapıldı (`CreateTaskItemHandler`, `UpdateTaskItemHandler`). C# zorunlu argümanı
   isteğe bağlıların önüne koymayı dayattığı için `documentFreezer`, `direction`/`upwardRequests` çiftinden
   önce duruyor — bu sıra taşıyıcı, çünkü satın alınan özellik tam olarak "varsayılanın arkasına
   saklanamaması".
2. `Program.cs` konteyneri `ValidateOnBuild = true` + `ValidateScopes = true` ile kuruyor. Tek başına
   "zorunlu" hatayı sessizlikten **ilk isteğe** taşırdı; asıl istenen açılış.

**Mutasyon (2026-08-27, ölçüldü):** `AddScoped` satırı silindi → uygulama **başlamadı**:
`"Some services are not able to be constructed … Unable to resolve service for type
TaskDocumentReferenceFreezer"`, `"Now listening on"` hiç görünmedi. Hiçbir dizgi araması yok.
- Dizgi testi silindi; yerine **refleksiyon** testi kondu: argüman yeniden isteğe bağlı/nullable yapılırsa
  kırmızı. Açılış kontrolü ancak argüman zorunlu kaldığı sürece güçlü, o yüzden bu ikisi bir çift.
- 15 test çağrı yeri (grep 11 buldu, doğru sayıyı **derleyici** verdi) tek bir ortak double'a bağlandı:
  `TaskDocumentFreezerDoubles.OverAnEmptyRegister()` — **gerçek** dondurucu, boş bir kayıt üstünde. Stub
  koymak, işleyicinin dondurmadan atıf yazmasına geri dönmesini görünmez kılardı.
- ⚠ `ValidateOnBuild` tüm Platform konteynerini doğruluyor ve **açılış temiz** (ölçüldü: uygulama ayağa
  kalktı, arka plan işleri koştu, 166 izin senkronize oldu). Yani başka çözülemeyen kayıt yok.
- **Gelecek regresyon riski: 🟢**

### BL-286 — worktree'de kırılan testler: `.git` bir DOSYA olabilir (2026-08-27, düzeltildi)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

GSKU ekibinin tam süiti koşamamasının sebebi. İki ayrı kalıp, aynı hata: **checkout biçimini tahmin etmek**.

**(a) Beş dizin elle tırmanma** — `AppContext.BaseDirectory` + `".."×5`. 4 Platform testinde vardı
(`TaskDocumentReference`, `TaskUpwardRequest`, `TaskSeatDirectory`, `TaskRunningChildrenSignal`). Sayı yalnız
tek bir çıktı düzeninde doğru.

**(b) `.git`'i DİZİN sanmak** — 6 test doğru şekilde yukarı yürüyordu ama durma koşulu
`Directory.Exists(Path.Combine(dir, ".git"))` idi. Normal klonda `.git` bir dizindir; **git worktree'de bir
DOSYADIR** (`gitdir:` işaretçisi içerir). Koşul hiç sağlanmıyor, yürüyüş dosya sisteminin tepesine çıkıyor ve
"Could not locate the repository root" fırlatıyor. Etkilenen: `TaskCommentTests`,
`TaskActionCodeReachabilityTests`, `TaskDependencyTests`, `TaskDependencyEnforcementTests`,
`TaskSubtaskBlockingTests`, `DateTimeOffsetSortGuardTests`.
- ⚠ **(b) ilk taramada bulunamadı.** `".."` araması yalnız (a)'yı görür; (b) ancak süit **ayrı bir
  worktree'den** koşulunca ortaya çıktı. Bitirme koşulunun "depo kökünde geçmesi yetmez" demesinin sebebi bu.
- Onu da hepsi tek bir `RepoPaths` yardımcısına bağlandı: **AGENTS.md** işaretçisine kadar yukarı yürür.
  AGENTS.md izlenen bir dosya, yani her checkout biçiminde — worktree dahil — vardır.
- **KANIT:** ayrı worktree'den Platform süiti (BRD hariç) **2475/2475 · mongod ayakta**. Aynı worktree'de
  düzeltmeden önce 8 test kırmızıydı.
- **Gelecek regresyon riski: 🟡** — kalıp yeniden yazılabilir; `.git`/`".."` desenini yasaklayan bir muhafız
  YOK. Aşama 1'in Mongo muhafızı gibi bir kaynak taraması bunu kapatır, bu turda yapılmadı.

### BL-287 — Guid temsili: BRD testleri üretimin KULLANMADIĞI kodlamaya karşı yeşildi (2026-08-27, düzeltildi)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

"Tek başına geçer, süitte kalır"ın kaynağı ölçüldü ve teşhis ilk sanılanın **tersi** çıktı.

**Ölçüm:** `BusinessReferenceDataTenantAssignment` + `BusinessReferenceDataPublishOperation` sınıfları tek
başına **13/13 geçiyor**; harness kullanan bir sınıf aynı sürece girince **11 kırmızı**. Hatalar veri
kaybı gibi okunuyor (`Assert.NotNull() Failure: Value is null`) — satırlar bir Guid kodlamasıyla yazılıp
diğeriyle sorgulandığı için. Mesajın hiçbir yerinde serileştirme geçmiyor; bu yüzden bir tur kaybettirdi.

**Ters teşhis:** kayıt EKSİK olduğu için değil, VAR olduğu için kırılıyorlar. Üretim
(`Infrastructure/DependencyInjection`) **ikisini birden** yapıyor:
`BsonSerializer.RegisterSerializer(new GuidSerializer(Standard))` (süreç-geneli) **ve**
`mongoClientSettings.GuidRepresentation = Standard` (istemci başına). BRD testleri kendi `MongoClient`'ını
kuruyor ve **ikisini de** ayarlamıyor — yani yalnız hiçbir şey global serializer'ı kaydetmemişken geçiyorlardı.
Bu, BL-280'in aynı şekli: üretimin sahip olmadığı bir kodlamaya karşı yıllarca yeşil.

- **Cevap "istemci başına yap" DEĞİL:** kayıt sürücüde tasarım gereği süreç-geneli ve üretim de öyle yapıyor;
  testte daraltmak, testleri üretimden ayırırdı. Kusur **kapsam** değil **zamanlama**ydı: kayıt harness'ın
  içinden TEMBEL yapılıyordu, yani global durum ancak harness kullanan bir sınıf önce koşmuşsa vardı.
- **Düzeltme:** `PlatformTestSerializers` + `[ModuleInitializer]` — assembly yüklenirken, ilk test vakasından
  önce, hem iki serializer hem `MongoDefaults.GuidRepresentation = Standard` (her `MongoClientSettings`'in
  devraldığı sürücü-geneli varsayılan). Böylece "hangi sınıf önce koştu" diye bir şey kalmıyor ve her istemci
  üretimin şeklini alıyor — her testin hatırlamasına gerek kalmadan.
- 11 kırmızı veren senaryo şimdi **17/17 yeşil**; sınıflar tek başına da yeşil.
- ⚠ **Ölçüm tuzağı:** aynı senaryo bir ara 17 dk 31 sn sürdü. Sebep bu değişiklik değil, biriken BRD artığı
  üzerinde boğulan mongod'du (BL-283); artık düşürülünce **1 dk 14 sn**. Süre ölçerken `dbPath` durumunu
  önce temizleyin.
- **Gelecek regresyon riski: 🟡** — yeni bir test kendi `MongoClient`'ını kurarsa artık doğru varsayılanı
  devralır, ama açıkça `GuidRepresentation` ayarlayıp yanlış değer verirse hâlâ sapabilir. Muhafız yok.

### BL-290 — kural dosyası `row g-6` diyordu, ürün 340 yerde g-4/g-3 (2026-08-27, ölçüldü, DÜZELTİLDİ)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Ne:** `.antigravity/rules/frontend-form-template.md` "Form sayfalarında `row g-6` boşluğu
  standarttır" diyordu. Ürün bunu hiç uygulamamış.
- **Ölçüm (2026-08-27, `Views/` altı):** `row g-4` **170** · `row g-3` **170** · `row g-2` 26 ·
  `row g-6` **4** — ve o dördü de **form değil**, DocumentManagement'ın Details sayfaları.
  Her iki altın referans da g-4 (kart satırı) + g-3 (kart içi alan satırı) kullanıyor.
- **Karar: kural ürüne uyduruldu, ürün kurala değil.** Kuralı okuyup g-6 yazan tek bir form
  sayfası çıkmadı; 340 satırı "kurala uysun" diye değiştirmek, hiç kimsenin şikâyet etmediği
  bir boşluğu bütün ürün genelinde büyütmek olurdu. Kural artık g-4/g-3 diyor ve NEDEN
  değiştiğini kendi içinde taşıyor.
- **Açık kalan (küçük):** DocumentManagement'ın 4 `row g-6` Details sayfası artık hiçbir
  kurala dayanmıyor. Form şablonu kuralı Details sayfalarını kapsamıyor, o yüzden ihlal
  değiller — ama o modüle dokunan bir sonraki tur g-4'e almalı.
- **Gelecek regresyon riski: 🟢** — düzeltilen bir belge satırı; kod değişmedi.

### BL-292 — iki altın referansın DA select placeholder'ı bozuktu, iki ZIT şekilde (2026-08-27, canlıda bulundu, DÜZELTİLDİ)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

- **Nasıl bulundu:** sahip **ekrana bakarak**, ikon turunun canlı doğrulaması sırasında. 1850 testin
  hiçbiri görmüyordu; ikisi de bu turda çivilendi
  (`tests/golden-reference-form-icons.test.js`, "both references DECLARE the select placeholder").
- **Tek sebep, iki zıt belirti — ikisi de "placeholder'ı select2 kendi çıkarsın" ihmali:**
  · **Slim (offcanvas):** `placeholder: $el.data('placeholder') || ''` — markup'ta `data-placeholder`
    yok, yani select2'ye **BOŞ** placeholder verildi. Boş placeholder "placeholder yok" demek
    DEĞİL: select2 o zaman `<option value="">`in metni yerine
    `<span class="select2-selection__placeholder"></span>` çiziyor. Yerelleştirilmiş
    **"Seçiniz…" hiç ekrana çıkmadı** — oklu boş kutu.
  · **Compact (tam sayfa):** placeholder **hiç verilmedi** — select2 boş option'ı sıradan bir
    **SEÇİM** sanıp gövde rengiyle boyadı. Ölçüldü: `rgb(56,69,81)`, aynı karttaki her düz
    input'un placeholder'ı ise `rgb(167,172,178)`. **Boş alan, dolu alandan ayırt edilemiyordu.**
- **Düzeltme (ikisi de):** `placeholder: … || $el.find('option[value=""]').text() || ''`.
  Metin OPTION'da kalıyor → tek yerelleştirme kaynağı (markup'ın arkasındaki resx), hiçbir dil
  dosyasının güncellemeyeceği bir `data-` niteliğinde ikinci kopya değil.
  Doğrulandı: ikisi de artık `rgb(167,172,178)`.
- ⚠ **Kural dosyası bunu ZATEN doğru yazıyordu** (`create.js` şablonu, `initSelect2`:
  `placeholder: $el.find('option[value=""]').text() || ''`). İki referans da kuraldan sapmıştı —
  yani "kural doğru + referans yanlış" hâli, bu turun asıl teşhisinin (desenin kanalı boş) select2
  tarafındaki ikinci örneği.
- **Gelecek regresyon riski: 🟢** — davranış artık iki referansta da testle çivili.

### BL-293 — yenilenen belirteç AYNI istekte görünmüyordu; iki hata, tek kök (2026-08-27, düzeltildi + CANLI kanıtlandı)
> **DURUM:** KAPANDI · **SAHİP:** SAHİPSİZ
> *Geldiği bölüm:* Açık kararlar

`TokenBridge` belirteci yeniliyor ve yeni değeri **yalnız `HttpResponse.Cookies`**'e yazıyordu. Bu, dışarı
giden bir başlıktır; `HttpRequest.Cookies` tarayıcının GÖNDERDİĞİNİN anlık görüntüsüdür ve **depoda ona yazan
tek satır yok** (ölçüldü: sıfır). Aşağı akıştaki **57 çağrı yeri / 53 dosya** bu yüzden yenilemenin olduğu
isteğin tamamında az önce değiştirilmiş belirteci kullanıyordu.

**Kod tabanı bunu biliyordu ama yalnız köprünün içinde çözmüştü:** `TokenBridgeTests` içindeki
`Pass_2_does_not_undo_the_refresh_even_though_the_request_still_holds_the_old_token` — kusurun adı, yazılmış
ve öylece bırakılmış.

**Tasarım kararı (b değil, a'nın daha iyi hâli):** `HttpRequest` zaten `HttpContext` taşıdığı için
`AuthTokenCookies.GetAccessToken` **imzası değişmeden** tamponu okuyabiliyor — 57 çağrı yerinin hiçbirine
dokunulmadı, aşırı yükleme de gerekmedi. `Request.Cookies`'i saran koleksiyonla değiştirmek (seçenek b)
reddedildi: isteğin tarayıcının ne gönderdiği hakkında yalan söylemesi demek, başkalarının başka sebeplerle
okuduğu bir şey ve depoda emsali yok. Bedeli açık yazıldı → BL-294.

**HATA B — çıkış attıran (🔴):** 15 proxy denetleyici (23 çağrı yeri) 401'de çerezleri **köprünün taze çerezi
yazdığı AYNI Response üzerinde** siliyordu. `Response.Cookies.Delete`, önceki `Append`'in yanına bir son
kullanma eklemez — onu **başlıklardan çıkarır** (bağımsız olarak ölçüldü, `CookieOverwriteMeasurementTests`).
Yani taze belirteç tarayıcıya hiç ulaşmıyor, sonraki istek boş geliyor ve kullanıcı **gerçekten** çıkışa
atılıyor. Aylardır süren "veri sayfalarında çıkış atma" şikâyetinin en olası açıklaması.

**CANLI KANIT (2026-08-27, tam yığın; kiracının `SessionTimeoutMinutes` değeri geçici olarak 45→1, sonra geri
alındı). Aynı senaryo, düzeltmeli ve düzeltmesiz:**

| ölçüm (erişim belirteci süresi dolmuş, exp+51 sn) | DÜZELTMESİZ | DÜZELTMELİ |
|---|---|---|
| `GET /OrganizationUnits/api` (proxy uç) | **401** | **200, veriyle** |
| yanıttaki canlı `access_token` çerezi | **0** | **5** (chunks-4 + 4 parça) |
| sol menü öğesi | **2** | **35** |
| Ctrl+K girdisi | **0** | **31** |
| `/OrganizationUnits` sayfası | 200 | 200 |
| girişe yönlendirme | yok | yok |

- ⚠ Düzeltmeli koşuda uç **401 bile dönmüyor**: birincil düzeltme sayesinde proxy artık TAZE belirteçle
  gidiyor. İkinci savunma hattı (yenileme sonrası 401'de çerez silme) hiç devreye girmedi — istendiği gibi.
- ⚠ **Belirteç PARÇALI çerez olarak taşınıyor** (`chunks-4` + 4 parça): canlı ölçümün ortaya çıkardığı bir
  ayrıntı ve BL-294'ü doğrudan ilgilendiriyor.
- ⚠ Ölçüm tuzağı: `ClockSkew` 30 sn. Süresi 25 sn önce dolmuş belirteçle yenileme TETİKLENMEZ; ilk denemem
  bu yüzden hiçbir şey göstermedi. exp+50 sn'den sonra ölçün.
- ⚠ Kiracı belirteç ömrü `JwtSettings:AccessTokenExpirationMinutes` DEĞİL, kiracının
  `SessionTimeoutMinutes` ayarından geliyor (`LoginCommandHandler:174`). Ortam değişkeniyle kısaltmaya
  çalışmak işe yaramaz.
- **Gelecek regresyon riski: 🟡** — `AuthTokenCookies` dışından okuyan bir tüketici hâlâ bayat değeri görür;
  bunu yasaklayan bir muhafız yok.

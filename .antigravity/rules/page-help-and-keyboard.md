# Sayfa Yardımı ve Klavye Standardı — UXH-001

> Bu kural 2026-10-10'da yazıldı (WP-UX-COMMON-01; BL-495, BL-586, BL-438). Sahip canlı kullanıcılardan aynı şeyi
> iki kez duydu: *"sayfaları nasıl kullanacağımızı bilmiyoruz"*. Üstelik yan panellerde Kaydet için fare
> gerekiyordu: düğmeler `type="button"` idi ve Enter'ı dinleyen bir şey yoktu.
>
> Kapanmış modüllere bu konular için bir daha dönülmesin diye üç parça **bir kez**, ortak olarak yapıldı. Bu kural, yeni
> bir sayfanın bu üç parçayı nasıl kullanacağını söyler.

---

## 1. Kural

**Her sayfa "?" yardım panelinde kendi metnini taşır ya da borç defterinde adıyla durur.**

**Her yan panelde Enter birincil işlemi çalıştırır; geri alınamaz işlem yalnız Ctrl / Cmd + Enter ile çalışır.**

⚠ **İki kabukta, FROZEN `_Layout`'ta değil.** Yardım düğmesi, Enter kuralı ve kısayol katmanı
`_LayoutTenantShell` ile `_LayoutPlatformAdmin`'dedir. `Layout =` taşımayıp dondurulmuş `_Layout`'a düşen görünümlerde
(ölçüm 2026-10-10: 99 görünüm — ESBP 52, WorkCenter 18, MG 12, DEM 7, DemandIdeas 5, Shared 3, diğer 2) hiçbiri **yok**;
o görünümler borç defterinde ayrı bölümde, "yardım gösterilemez" diye adıyla durur. "Her sayfada" bu iki kabuk demektir.

**Klavye kısayolları ortak katmana kaydedilir; sayfa kendi `keydown` dinleyicisini yazmaz.**

## 2. "?" yardım paneli (BL-495)

Üst çubukta, iki kabuğun her sayfasında aynı yerde tek bir düğme vardır (`_PageHelpButton.cshtml`). Düğme dar ekranda da
görünür.
`?` tuşu da aynı paneli açar (`_PageHelpPanel.cshtml`, `assets/js/shared/diten-help.js`).

**Panel dört şey gösterir:**

| bölüm | kaynak |
|---|---|
| (a) bu sayfa ne işe yarar — 2 cümle | `Help.{KOD}.Purpose` |
| (b) nasıl kullanılır — 3–5 adım | `Help.{KOD}.Step1` … `Step5` |
| (c) bu sayfanın kısayolları | `DitenShortcuts.list()` — kısayol yoksa bölüm çıkmaz |
| (d) "Turu başlat" | yalnız sayfanın turu varsa, kütüphane yüklüyse ve ekran ≥ `md` ise |

**Sayfa kodu.** Menünün ve Ctrl+K'nin kullandığı kararlı koddur (`Nav.Page.{KOD}`), `NavNameLocalizer.Normalize`
biçimindedir: büyük harf, yalnız harf ve rakam.

- Menü bileşeni, istek yolunu bölüm sınırında en uzun eşleyen sayfayı seçer (`CurrentPage.Match`).
- Görünüm isterse `ViewData["PageCode"]` ile bunu ezer.
- Platform yönetici sayfasının kodu `PLATFORM` + `Views/Platform/` klasör adıdır.

**Dil.**

- Metinler `Resources/PageHelpResource.{dil}.resx` içindedir.
- Kiracı sayfası **7 dil** (en, tr, fr, es, zh, ar, ru) taşır; platform yönetici sayfası **2 dil** (en, tr).
- Metin düz metindir; Razor kodlayarak basar.

**Metni yoksa** panel yalnız kısayolları ve *"Bu sayfa için yardım metni hazırlanıyor"* yazısını gösterir.

**Yetkisiz yüzde** (UAS-001) ne düğme ne panel çizilir. `_AccessDenied.cshtml` isteğe bir işaret koyar
(`CurrentPage.MarkAccessDenied`); kabuk bu işareti görünce yardımı çizmez.

### Borç defteri

`docs/roadmap/backlog/page-help-ledger.md` metni olmayan sayfaları listeler. Bekçisi
`frontend/Diten.Web/tests/page-help-ledger.test.js`'tir.

- Sayfa kümesi **üretimden türetilir**: menü / Ctrl+K kodları (`Nav.Page.*`) ∪ modül manifestlerinin sayfa kodları
  (menüde görünmeyen alt sayfalar dahil) ∪ FROZEN `_Layout`'a düşen görünümler.
- Metni olmayan ve defterde de olmayan sayfa = kırmızı.
- Metni yazılmış ama defterde kalmış sayfa = kırmızı.
- Satır sayısı, **defterin kendi başlığındaki** sayılara **EŞİT** olmak zorundadır (UX-COMMON-FIX2): bir satır eklemek
  ya da silmek başlığı da düzenletir, böylece her değişiklik PR'da başlık satırıyla görünür. **Defter yalnız küçülür.**
  Test bir önceki sürümün sayısını göremez; ana dala karşı denetim (`git show origin/main:…`) ayrı iştir (CT backlog'u).
- **Yeni sayfa metniyle gelir; deftere satır eklenmez.**

## 3. Enter = birincil işlem (BL-586)

Yan panelde (Bootstrap offcanvas) tek satırlık bir alanda Enter, panelin **birincil** düğmesine basar. Basmak, düğmeye
tıklamakla aynı şeydir: sayfanın kendi kaydetme işleyicisi, doğrulaması ve çift gönderim koruması aynen çalışır
(`assets/js/shared/diten-enter-primary.js`). Modül iki kabukta da yüklüdür.

**Birincil düğme:**

1. Paneldeki `data-dt-primary` işaretli ve şu an görünen düğme.
2. İşaret yoksa ve panelde form alanı varsa, paneldeki **tek** `.btn-primary`.
3. Birden çok işaretsiz `.btn-primary` varsa ya da hiç yoksa Enter'a dokunulmaz; bu durumda panele işaret konur.

Kaydet düğmesi `.btn-primary` değilse (ör. `btn-warning` "Sonlandır") **`data-dt-primary` zorunludur**.

**Açık işaret (UX-COMMON-FIX2, CT kararı):** form alanı olan **her** panelin birincil düğmesi işaret taşır:
`data-dt-primary="enter"` (çıplak Enter çalıştırır) ya da `data-dt-primary="chord"` (yalnız Ctrl / ⌘ + Enter).
İşaretsiz panel = bekçi kırmızı. İşareti sayfa betiği eylem başına da koyabilir (Platform Tenants abonelik işlemi:
iptal / askıya alma `chord`, atama / etkinleştirme / yenileme `enter`; görünümdeki statik işaret güvenli olan `chord`'dur).

**Sonuçlu (geri alınamaz) işlem: `data-dt-primary="chord"`** (UX-COMMON-FIX1, CT kararı). Yayımla, onayla, reddet,
gönder, sil, bitir / sonlandır, emekliye ayır, devre dışı bırak, devret, statü değiştir, ayır (rezerve et), kanıt kaydet.
Böyle bir panelde **çıplak Enter hiçbir şey yapmaz** (doğal form gönderimi de olmaz); **Ctrl+Enter** (macOS'ta ⌘+Enter)
çalıştırır. Çıplak Enter yalnız geri alınabilir işler içindir: taslak kaydet, oluştur, düzenle, süz, ara. Etiketi
sonuçlu bir sözcük taşıyan birincil düğme `chord` işaretli olmak zorundadır; `enter` işaretli bir panelde sonuçlu sözcük
de kırmızıdır (ikinci denetim). Etiketi yalnız "Kaydet" olan sonuçlu paneller (iş akışı görev işlemi, abonelik işlemi,
kullanıcı daveti, platform yöneticisi oluşturma) işareti açıkça taşır.

**Sınıflandırmanın kaynağı kodun kendisidir** (her panelin işareti) ve bekçisi
`frontend/Diten.Web/tests/diten-enter-primary.test.js`'tir: işaretsiz panel, sonuçlu sözcüğü olan `enter` paneli ve
listede olmayan bir istisna kırmızıdır; istisnalar adıyla ve gerekçesiyle o testte yazılıdır. CT kararları (FIX2):
Gskus → `enter` (her modda onay penceresi açar); Governance Users oluştur → `chord` (davet e-postası gider); Platform
Administrators oluştur → `chord` (ayrıcalıklı hesap + davet); Tenants abonelik işlemi → eylem başına (yukarıda).

**Doğal form gönderimi kuralın arkasından olmaz (FIX2, R1):** birincil düğme `chord` işaretliyse ya da kendisi bir gönder
düğmesiyse (`type="submit"`, türü yazılmamış `<button>`), panelde bir girdi ya da `<select>` üzerindeki Enter — onay
kutusu, kapalı select, datalist alanı, basılı tuş, Alt+Enter — betik hiçbir şeye basmasa bile `preventDefault` edilir.
Aksi hâlde tarayıcının örtük gönderimi o düğmeye basar (onaysız abonelik iptali, basılı tuşla tekrarlanan POST).
Textarea, düğme, bağlantı, IME yazımı ve açık seçici bunun dışındadır.

**Kullanıcıya söylenir (FIX2, R3):** `chord` işaretli düğme, paneli her açılışta `aria-keyshortcuts="Control+Enter
Meta+Enter"` ve (kendi `title`'ı yoksa) kısayolu anlatan yerelleştirilmiş bir `title` alır
(`Help.Panel.ChordTitle`, 7 dil). Yardım panelindeki `Help.Panel.EnterHint` chord'u anlatır.

**İstisnalar:**

| durum | davranış |
|---|---|
| textarea | Enter yeni satır; **Ctrl+Enter** (macOS'ta ⌘+Enter) kaydeder |
| açık select2 / açılır liste / otomatik tamamlama / tarih seçici | Enter seçim yapar |
| IME yazımı (`isComposing`) | yok sayılır |
| birincil düğme pasif (`disabled`, `.disabled`, `aria-disabled`) | hiçbir şey; çift gönderim yok |
| basılı tutulan tuş (`repeat`) | yok sayılır |
| düğme, bağlantı üzerinde Enter | o öğenin kendi davranışı (odaklı bir gönder düğmesinde Enter'ın yerel çalışması doğrudur: odak bilinçlidir) |
| onay kutusu, select, datalist alanı üzerinde Enter | betik basmaz; `chord` / gönder birincilli panelde örtük gönderim de engellenir (R1) |
| kapanmakta olan panel (`.offcanvas.show.hiding`) | yanıtlanmaz |
| sayfa Enter'a kendi anlamını vermiş (`preventDefault`) | sayfanınki geçerlidir |
| `list` özniteliği olan alan (datalist) | alanın kendi davranışı (öneri seçimi) |
| aynı panelde art arda iki Enter (kısa süre) | tek tıklama |
| birincil düğme şu an gizli (`d-none`, `[hidden]`, etkin olmayan sekme / adım) | birincil sayılmaz |

**Bu modülün dışında (FIX2, adla):** modal pencere içindeki yerel Enter gönderimi bu kuralın kapsamında değildir
(`gateEvidenceModal`, Platform Administrators `suspendModal`); BL satırı CT'dedir.

Dinleyici `window` üzerinde, kabarcık aşamasındadır. Bu sayede alan ya da `document` üzerindeki sayfa işleyicileri
ondan önce çalışır. "Seçici açık mı" bilgisi ise **capture** aşamasında, alanın kendi işleyicilerinden ÖNCE alınır:
`allowInput` bir flatpickr Enter'da tarihi işleyip takvimi kapatır; sonradan bakan, kapalı takvim görür ve kaydederdi.

**Esc** Bootstrap'ın kendi davranışıdır: panel kapanır.

**Bekçi** `tests/diten-enter-primary.test.js`'tir. `Views/` altındaki bütün offcanvas bloklarını tarar. Form alanı olup
birincil düğmesi okunamayan panel ya listede gerekçesiyle durur ya da kırmızıdır; liste yalnız küçülür.

## 4. Kısayollar (BL-438)

- Kısayol katmanı (`_DitenShortcuts.cshtml`, `diten-shortcuts.js`) iki kabukta da **bir kez** yüklenir; sayfa onu
  ayrıca yüklemez.
- Sayfa tuşlarını `DitenShortcuts.register(scope, …)` ile kaydeder; açıklama `Shortcuts.*` anahtarındadır (7 dil).
- Susma kuralı (form alanı, açık modal ya da offcanvas, Ctrl / Cmd / Alt) katmanın işidir.
- `?` yardım panelini açar. Panelin olmadığı yerde kısayol listesini açar.

## 5. Adım adım tur (BL-495)

**Kütüphane.**

- Driver.js (MIT) kullanılır; **tek sabit sürüm** `wwwroot/assets/vendor/libs/driverjs/` altına, LICENSE dosyası
  yanında konur ve SHA-256'sı testte kaydedilir — **sahip onayından sonra** (2026-10-10 itibarıyla dosya yok, indirme
  sahip onayı bekliyor). O güne kadar ara katman her yerde "tur yok" der, hata vermez.
- CDN ve ağ yükleme yoktur.
- Shepherd.js (v14+, ticari) ve Intro.js (AGPL / ticari) **yasaktır**.

**Ara katman.**

- Sayfalar kütüphaneyi değil `window.DitenTour`'u çağırır (`assets/js/shared/diten-tour.js`): `register`, `start`,
  `isAvailable`, `isCompleted`.
- Kütüphane sayfada yoksa her yerde "tur yok" cevabı döner; hata vermez.

**Hedef ve metin.**

- Adım hedefi bir `[data-tour="…"]` işaretidir; sayfa elle CSS seçici kurmaz.
- Metin `PageHelpResource` içindeki `Tour.*` anahtarlarındadır ve kütüphaneye kaçışlanarak verilir. HTML olamaz.

**Görünürlük.**

- "Turu gördüm" bilgisi tarayıcıda (`localStorage`) sürüm anahtarıyla tutulur; depolama yoksa tur her seferinde sunulur.
- Tur yalnız ≥ `md` genişlikte başlar.
- Arapçada açılan balona `diten-tour-rtl` sınıfı eklenir.

**Tur yalnız beş akışta yapılır** (sahip kararı 2026-10-09). Diğer sayfalarda yalnız "?" metni vardır.

1. Görev Merkezi
2. Zaman Çizelgesi
3. Global Ürün / kalem onaya gönderme
4. Kullanıcı + rol kurulumu
5. Toplantı

**Tur hedef testi:** kayıtlı her adımın `data-tour` hedefi sayfa işaretlemesinde bulunur; bulunmazsa kırmızı.

## 6. Modül paketi ve kabul

Her modül paketi iki satır taşır (`module-pack-standard.md` §12, §18):

- **Sayfa yardım metni:** sayfa kodu + `Help.{KOD}.Purpose` / `Step1..5` (kiracı 7 dil, platform 2 dil).
- **Tur gerekiyor mu:** yalnız §5'teki beş akıştan biriyse evet; adımlar ve `data-tour` hedefleri yazılır.

Modül turunun sahip kontrol listesine üç satır eklenir: **Enter** kaydeder, **"?"** metni doğru, **tur** baştan sona
çalışır (ya da "tur yok").

## 7. Neden

- Yardım metni sayfayla birlikte yazılmazsa hiç yazılmaz. Defter bu açığı görünür tutar ve yalnız küçülmesine izin verir.
- Enter'ı her sayfa ayrı yazarsa 70 yan panelde (ölçüm: form alanı olan offcanvas blokları, 2026-10-10) 70 ayrı istisna
  çıkar. Tek dinleyici hepsine aynı klavyeyi verir.
- Kütüphane tek dosyanın arkasındadır; değiştirmek ya da kaldırmak tek dosyalık iştir.

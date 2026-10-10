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

**Her yan panelde Enter birincil işlemi çalıştırır.**

**Klavye kısayolları ortak katmana kaydedilir; sayfa kendi `keydown` dinleyicisini yazmaz.**

## 2. "?" yardım paneli (BL-495)

Üst çubukta, her sayfada aynı yerde tek bir düğme vardır (`_PageHelpButton.cshtml`). Düğme dar ekranda da görünür.
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

- Metni olmayan ve defterde de olmayan sayfa = kırmızı.
- Metni yazılmış ama defterde kalmış sayfa = kırmızı.
- Satır sayısı tavanı aşarsa kırmızı. **Defter yalnız küçülür.**
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

**İstisnalar:**

| durum | davranış |
|---|---|
| textarea | Enter yeni satır; **Ctrl+Enter** (macOS'ta ⌘+Enter) kaydeder |
| açık select2 / açılır liste / otomatik tamamlama / tarih seçici | Enter seçim yapar |
| IME yazımı (`isComposing`) | yok sayılır |
| birincil düğme pasif (`disabled`, `.disabled`, `aria-disabled`) | hiçbir şey; çift gönderim yok |
| basılı tutulan tuş (`repeat`) | yok sayılır |
| düğme, bağlantı, onay kutusu, select üzerinde Enter | o öğenin kendi davranışı |
| sayfa Enter'a kendi anlamını vermiş (`preventDefault`) | sayfanınki geçerlidir |

Dinleyici `window` üzerinde, kabarcık aşamasındadır. Bu sayede alan ya da `document` üzerindeki sayfa işleyicileri
ondan önce çalışır.

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

- Driver.js (MIT) kullanılır; **tek sabit sürüm** `wwwroot/assets/vendor/libs/driverjs/` altındadır, LICENSE dosyası
  yanında. SHA-256'sı testte kayıtlıdır.
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
- Enter'ı her sayfa ayrı yazarsa 64 yan panelde 64 ayrı istisna çıkar. Tek dinleyici hepsine aynı klavyeyi verir.
- Kütüphane tek dosyanın arkasındadır; değiştirmek ya da kaldırmak tek dosyalık iştir.

# MOD-0286 — Tasarım Sistemi Şartnamesi (DL-1)

> **Paket:** `execution/domains/platform-shared-services/module-packs/MOD-0286-ui-design-system-page-pattern-governance.md` · revizyon 12 · sha256 `89333f90` (PR #245, merge `c51568aa8`; `execution/approvals/approvals.tsv` MOD-0286 rev 12 satırı, risk HIGH).
> **Karar kaydı:** `docs/records/decisions/2026-10/pss-mod0286-open-decisions-01.md` — D-1 (bu yol), D-2 (kurallar `.antigravity/rules/`'ta kalır).
> **Durum:** paket rev 12 main'de onaylıdır, bu belge main'dedir; kabul PSS-UI-P1-03 ile bekliyor (kabul kaydı Control Tower'ındır, bu belgede yazılı değildir). Ölçümler 2026-10-23, HEAD `005a0cc58`, dal `chore/ct/pss-ui-ct-01`; §2 envanterindeki satır sayıları 2026-10-27'de HEAD `8b9a45ba9` ağacında `wc -l` ile yeniden ölçüldü (17 dosyanın hiçbiri değişmedi).
> **İlke:** Bu belge **indeksler, değiştirmez**. Her kuralı özetler ve `yol:satır` ile gösterir; kural ile bu belge çelişirse **kural kazanır** (D-2), çelişki §9'a yazılır.
> **Yetki sırası:** Module Pack > Domain Config > AGENTS.md > `.antigravity/` (`AGENTS.md:40-50`). Bu belge o sıranın altındadır; kural koymaz.
> **Dil:** gövde Türkçe, kimlikler ve kod adları İngilizce. Satır atıfları `.antigravity/rules/` altındaki dosyalara göredir; canlı kod yolları `frontend/Diten.Web/` köküne göredir.
> **Kontrol listesi:** her bölümün sonundaki "Kontrol listesine bağ" satırı, bölümü ölçen `UIC-<n>` kimliklerini paketin DL-2 tablosundan (`paket:156-200`) alır; `compliance-checklist.md` bu dizinde ayrı dilimdir (DL-2).

## 1. Sayfa arketipleri

Diten ERP vNext'te bir ekran altı arketipten birine girer. Arketip kararı paket frontmatter'ındaki `golden_reference` ve `form_field_count` alanlarıyla verilir; sayfa o karara göre dosya setini **birebir** altın referanstan alır (`.antigravity/rules/module-pack-standard.md:113` "Sapma teknik borçtur ve kabul edilmez").

| Arketip | Ne zaman | Kaynak kural | Canlı kod |
|---|---|---|---|
| Liste · Slim | create/edit formu **8 ve altı** kullanıcı alanı; create/edit ve QuickView Index içinde offcanvas | `module-pack-standard.md:70, :126-135`; `AGENTS.md:142-150` | `Views/DevEnablement/GoldenReferenceSlim/` (7 dosya: Index, _CreateEditOffcanvas, _DataTable, _DetailsQuickView, _Filter, _IndexL10n, GoldenReferenceSlimIndex.cs); pack DEV-0000 `status: ready-for-dev`, `form_field_count: 6` |
| Liste · Compact | **8'den fazla** alan; ayrı Create / Edit / Details sayfaları + `_Form.cshtml` | `module-pack-standard.md:135, :233-245`; `frontend-datatable-template.md:46-52` | `Views/DevEnablement/GoldenReferenceCompact/` (9 dosya); pack DEV-0001 `status: in-progress`, `form_field_count: 13` |
| Detay (salt-okunur) | Compact modülün Details sayfası; `row g-6`, `dl.row` + `dt/dd`, token'lı yüzey | `frontend-details-template.md:5-13` | `GoldenReferenceCompact/Details.cshtml` |
| Form (Create/Edit) | Compact tam sayfa ya da Slim offcanvas; `row g-4` / `row g-3`, `.diten-field` sarmalayıcı | `frontend-form-template.md:5-20, :21-31, :35-45` | `GoldenReferenceCompact/_Form.cshtml`, `GoldenReferenceSlim/_CreateEditOffcanvas.cshtml` |
| Bespoke konsol | DataTable CRUD olmayan yüzey; paket "NOT a Golden DataTable CRUD surface" yazar, `golden_reference: none` | `module-pack-standard.md:70` (`none` = DataTable dışı) | MOD-0155-FU02 / FU03 / FU04 / FU05 paketleri (commercial-suite) bu cümleyi taşır; canlı kod bu belgede ölçülmedi |
| Sihirbaz | çok adımlı kayıt açma | **kural dosyası yok** (açık nokta) | `Views/MasterData/LegalEntities/Wizard.cshtml` (438 satır, `Layout = "_LayoutTenantShell"` `:9`) + `wwwroot/assets/js/MasterData/LegalEntities/wizard.js` (557 satır); MOD-0220 paketi `:562`, `:634` atıf yapar, paket `status: ready-for-dev` (FU-6) |

Liste dosya seti (kural `frontend-datatable-template.md:32-53`, `module-pack-standard.md:213-245`; doğrulayıcı `.antigravity/scripts/verify_datatable_page.py:953-966` aynı seti okur):

| Dosya | Slim | Compact | Not |
|---|---|---|---|
| `Views/{Area}/{Module}/Index.cshtml` | zorunlu | zorunlu | `_Filter` partial'ı in-line yazılmaz (`frontend-datatable-template.md:12`) |
| `_Filter.cshtml` | zorunlu | zorunlu | inline collapse filtre çubuğu (`:575`) |
| `_DataTable.cshtml` | zorunlu | zorunlu | v2 marker, iskelet, checkbox/action kolonları (`:53`) |
| `_IndexL10n.cshtml` + `index.l10n.js` | zorunlu | zorunlu | `window.L10n` köprüsü (§6) |
| `{Module}Index.cs` | zorunlu | zorunlu | yerelleştirme marker sınıfı (`:250-261`) |
| `wwwroot/assets/js/{Area}/{Module}/index.js` | zorunlu | zorunlu | fabrika çağrısı (§2) |
| `_CreateEditOffcanvas.cshtml` | zorunlu | — | `module-pack-standard.md:221` "Slim-ozel" |
| `_DetailsQuickView.cshtml` | zorunlu | — | `module-pack-standard.md:222`; Golden Slim taşır |
| `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `_Form.cshtml` | — | zorunlu | `frontend-datatable-template.md:46-52` |

Canlı sayım (2026-10-23): Archive dışı `Index.cshtml` = **192**; `DtDefaults` geçen JS = **177**; `createList(` = **37**; `createCrudTable(` = **81** dosya (`grep -rl … wwwroot/assets/js --include='*.js'`). Üç liste uygulaması yan yana yaşar; arketip kararı yalnız yeni ve dokunulan sayfalar için bağlayıcıdır (dokunma protokolü aşağıda).

Veri modeli: liste `data_mode: server` (varsayılan, sınırsız büyüyen küme) ya da `client` (tasarımda sınırlı küme, `pageSize=` URL'de yasak) beyan eder; sayfa `<table data-dt-data-mode="…">` ile aynı kararı taşır (`frontend-datatable-template.md:72-84`). Golden Compact = server, Golden Slim = client (`:84`).

Alan sayımı yalnız create/edit formunda kullanıcının doldurduğu alanlardır; `Id`, `TenantId`, audit alanları ve tablo checkbox/action kolonları sayılmaz (`AGENTS.md:151`). Doğrulayıcı `form_field_count`'u okumaz (`grep -c form_field_count verify_datatable_page.py` = 0); sayım DL-2'de ayrı maddedir.

Eski liste ekranlarına dokunma protokolü: program olarak göç yok; dokunan görev `--format gaps` koşar, sapmaları numaralar, sahibe sorar (`frontend-datatable-template.md:151-153`; kural "bugün 132'si sapmış" der).

**Kontrol listesine bağ:** UIC-3, UIC-5, UIC-22, UIC-23, UIC-25, UIC-26.

## 2. Bileşen standartları

Paylaşılan bileşen, sayfanın kopyaladığı değil **kullandığı** parçadır (`_ListShell.cshtml:2` "a used component, not a copied one", BL-440 paket 1). Aşağıdaki tablo her paylaşılan dosyanın satır sayısını (`wc -l`, 2026-10-23) ve ana giriş noktasını verir (AC-14).

| Bileşen | Dosya (`frontend/Diten.Web/`) | Satır | Ana giriş noktası |
|---|---|---|---|
| Onay (premium confirm) | `Views/Shared/_GlobalConfirmation.cshtml` | 479 | `window.showConfirm` `:206` |
| Bildirim (toast) | `Views/Shared/_GlobalNotification.cshtml` | 269 | `window.DitenToastText` `:109`, `window.showToast` `:133`, `new Notyf` `:179` |
| Liste kabuğu | `Views/Shared/Components/DataTable/_ListShell.cshtml` | 58 | partial; 41 görünüm tüketir (`grep -rl '_ListShell' Views`) |
| Toplu eylem çubuğu | `Views/Shared/Components/DataTable/_BulkActionBar.cshtml` | 30 | partial |
| Tablo iskeleti | `Views/Shared/_TableSkeleton.cshtml` | 51 | partial; 33 görünüm tüketir |
| Liste fabrikası (v2) | `wwwroot/assets/js/diten-datatable.js` | 1414 | `createList` `:961`; dışa aktarma bekçisi `:908-911`; filtre düğmesi öznitelikleri `:1046`; `dt-save-filter-btn` `:1052`; `colReorder` varsayılanı `:1083`; dışa aktarım `:1394-1395` |
| Eski CRUD tablosu | `wwwroot/assets/js/diten-datatable.js` | (aynı dosya) | `createCrudTable` `:251` — eski yol (bkz. §9 satır 2) |
| DataTable varsayılanları | `wwwroot/assets/js/dt-defaults.js` | 1603 | `window.DtDefaults` `:7`, `create` `:795`, dışa aktarım `:1586` |
| Premium modal yardımcıları | `wwwroot/assets/js/shared/premium-modal.js` | 126 | `global.DitenModal` `:106` |
| Diyalog görünümü | `wwwroot/assets/js/shared/diten-dialog.js` | 128 | `global.DitenDialog` `:127` |
| "?" yardım paneli | `wwwroot/assets/js/shared/diten-help.js` | 138 | `global.DitenHelp` `:137` (`open`, `pageCode`, `startTour`) |
| Kısayol katmanı | `wwwroot/assets/js/shared/diten-shortcuts.js` | 283 | `global.DitenShortcuts` `:270` |
| Tur | `wwwroot/assets/js/shared/diten-tour.js` | 149 | `global.DitenTour` `:148` |
| Tur kataloğu | `wwwroot/assets/js/shared/diten-tour-catalog.js` | 41 | `global.DitenTourCatalog` `:40` |
| Enter = birincil işlem | `wwwroot/assets/js/shared/diten-enter-primary.js` | 207 | `global.DitenEnterPrimary` `:198` |
| Tarih alanı | `wwwroot/assets/js/shared/diten-datefield.js` | 463 | `global.DitenDateField` `:462` (`enhance`) |
| Yetkisiz yüz | `wwwroot/assets/js/diten-unauthorized.js` | 52 | `window.DitenUnauthorized` `:41` |
| Kişiselleştirme istemcisi | `wwwroot/assets/js/personalization-client.js` | 193 | `window.personalizationClient` `:3`; `fetch` yalnız burada `:125, :143, :155` |

Envanter: 18 satır, 17 ayrı dosya (fabrika ve eski CRUD aynı dosyada). `wwwroot/assets/js/shared/` altındaki diğer 12 dosya (calendar, checkitem, invite-card, people-search, person-picker, refusal, related-records, tags, tree, zoned-time, http-media-type, timer-chip) paylaşılan bileşendir ama P1 kapsamındaki kurallarda adı geçmez; satır sayıları ölçüldü, bu belgede listelenmedi.

Kurallar (özet, kaynak satırla):

- **Onay ve bildirim:** onay pencereleri yalnız `window.showConfirm(keyOrTitle, callback, options)`; elle SweetAlert çağrısı yok (`.antigravity/rules/premium-modal-standard.md:91`). `rounded-4` + `shadow-lg`, sabit padding, `swal-icon-circle` halka ikon, `buttonsStyling: false` zorunlu (`premium-modal-standard.md:13-16`). Silme onayı `btn-danger` + `btn-label-secondary` + `warning` ikonu; toplu silme tekil silmeyle aynı onay (`frontend-datatable-template.md:197-205`, `:21`). Toast `window.showToast('KeyOrMessage', type)`; `toastr` yasak (`frontend-js-standard.md:988`).
- **Liste kabuğu + fabrika:** ham `DataTable({...})` yasak; `DtDefaults.create()` ve `DtDefaults.exportButtons()` zorunlu (`frontend-js-standard.md:30-31`); yasak pratikler tablosu `frontend-js-standard.md:981-991`. Kanonik kabuk `Views/Shared/Components/DataTable/_ListShell.cshtml`; `Views/PPM/Shared/_ListShell.cshtml` (41 satır) ikinci kopyadır (FU-3, PPM sahibi).
- **İskelet:** her liste kartında tablodan önce `#skeleton-loader`; kapsam DataTable'a özgü değil, veri yükleyen her sayfa (`views-organization.md:45-53`). `spinner-border` ve "Loading" metni içerik biçimli iskeletin yerini tutmaz.
- **Filtre partial'ı:** inline collapse filtre çubuğu, `_Filter.cshtml` minimum şablonu (`frontend-datatable-template.md:575`); select2 210 JS dosyasında geçer (`grep -rl select2 wwwroot/assets/js`), açılışta arama kutusu odak alır (`frontend-datatable-template.md:632`).
- **Save View:** `dt-save-filter-btn` başta `d-none` olsa da her zaman render edilir (`frontend-datatable-template.md:24`; `frontend-js-standard.md:73`); dirty-state yalnız uygulanan (applied) duruma göre (`frontend-datatable-template.md:221-230`); kişiselleştirme yalnız `window.personalizationClient`, sayfa JS'inde ham `fetch('/api/personalization/...')` yok (`frontend-js-standard.md:33`). İstisna yok (paket F-9).
- **ColReorder:** standart kolon yapısında varsayılan aktif, `colReorder: { columns: ':gt(1):not(:last-child)' }` (`frontend-js-standard.md:74-76`; `frontend-datatable-template.md:192`); tek istisna ≤2 veri kolonu + neden yorumu.
- **Dışa aktarma = ekran:** sunucu modunda `export: { mode: 'server', url, fileName }`; istemci modunda tarayıcı düğmeleri; `server` + `dataMode: 'client'` fırlatır (`frontend-datatable-template.md:107`; fabrika `diten-datatable.js:908-911`; BL-452 `product-backlog.md:6573`).
- **Form alanı:** her alan `.diten-field` ile sarılır, ikon kontrolden önce ve `aria-hidden="true"`; istisna yalnız `form-check`/switch ve configurable alanlar; tarih ikonu picker'ı açar (`frontend-form-template.md:21-31`). İkon eşlemesi `tests/diten-field-icons.test.js` `ICON_MAP` içindedir (`:45-46`).
- **"?" yardım, Enter, kısayol, tur (UXH-001):** her sayfa yardım metnini taşır ya da borç defterinde adıyla durur (`.antigravity/rules/page-help-and-keyboard.md:14`); panel iki kabukta, dondurulmuş `_Layout`'ta yok (`:18-21`); üç sekme ve `Help.{KOD}.*` anahtarları (`:25-40`); her form panelinin birincil düğmesi `data-dt-primary="enter"` ya da `"chord"`, geri alınamaz işlem yalnız `chord` (`:105-118`); kısayol katmanı tek yüklenir, sayfa `keydown` yazmaz (`:170-176`); tur Driver.js 1.8.0 vendor altında, CDN yok, Shepherd/Intro yasak (`:178-187`). Katalogda 3 akış var (`diten-tour-catalog.js`: WORKCENTER, MYTIMESHEET, TIMEAPPROVALS). Ölçüm: `_PageHelpButton|_PageHelpPanel|_DitenShortcuts` `_LayoutPlatformAdmin` 3, `_LayoutTenantShell` 3, `_Layout` 0.
- **Çalışmayan kontrol konmaz (UI-027):** teslimden önce ekrandaki her kontrole basılır (`frontend-standards.md:135, :165-166`).

**Kontrol listesine bağ:** UIC-2, UIC-4, UIC-14, UIC-15, UIC-24, UIC-27, UIC-28, UIC-30, UIC-31, UIC-32.

## 3. Kabuk ve yerleşim

İki aktif vNext kabuğu ve bir dondurulmuş eski layout aynı anda yaşar (`.antigravity/rules/views-organization.md:34`).

| Kabuk | Layout dosyası | Satır | View klasörü | Kaynak |
|---|---|---|---|---|
| Platform / admin | `Views/Shared/_LayoutPlatformAdmin.cshtml` | 448 | `Views/Platform/{Controller}/` | `views-organization.md:37`; `module-pack-standard.md:84` (`shell: platform-admin`) |
| Kiracı (tenant) | `Views/Shared/_LayoutTenantShell.cshtml` | 703 | `Views/{Controller}/` ya da alan klasörü | `views-organization.md:38`; `module-pack-standard.md:85` (`shell: tenant`) |
| Eski (Archive) | `Views/Shared/_Layout.cshtml` | 606 | yalnız eski kiracı modülleri | `views-organization.md:36` "dokunulmaz (Frozen)"; `frontend-standards.md:215` |
| Backend-only | — | — | — | `module-pack-standard.md:86` (`shell: none`) |

- **`shell` × layout türetme:** paketin `shell` değeri Razor layout'u ve view klasörünü belirler; Razor sayfasında layout **AÇIKÇA** yazılır, `_ViewStart.cshtml` varsayılanı değiştirilmez (`module-pack-standard.md:80-88`; `views-organization.md:39-41`). Layout zorunluluğu her pakette test edilebilir AC maddesidir (`module-pack-standard.md:292, :608`).
- **ViewStart gerçeği:** `Views/_ViewStart.cshtml` → `_Layout`; `Views/Platform/_ViewStart.cshtml` (3 satır) → `_LayoutPlatformAdmin`. `frontend-standards.md:208` bu yüzden manuel `Layout =` için "opsiyoneldir" der; iki kural açık yazımı ister → §9 satır 3 (D-8c).
- **PROD-001 / PROD-005:** `_Layout.cshtml` ve kök `_ViewStart.cshtml` değiştirilmez, geliştirme `backbone-custom.css` üzerinden (`.antigravity/rules/frontend-standards.md:201-203`); yeni modül tek kabuğa bağlanır, iki sidebar'da birden görünmez, aynı modül hem `/Platform/...` hem kök URL'de servis edilmez (`:205-215`).
- **Sidebar yerleşimi:** admin modülü `_LayoutPlatformAdmin.cshtml` içindeki "Platform Administration" listesine `<li class="menu-item">`; kiracı modülü `_LayoutTenantShell.cshtml` içinde uygun `menu-header` altına (`frontend-standards.md:210-212`). Menü adları MOD-0285 System Navigation'ın konusudur; bu belge yalnız yerleşim kuralını indeksler.
- **Kabukta yüklü ortak parçalar (ölçüm):** `_GlobalConfirmation` / `_GlobalNotification` atfı `_LayoutTenantShell` 3, `_LayoutPlatformAdmin` 2, `_Layout` 0 (`grep -c`); `premium-modal-standard.md:90-91` bu partial'ın layout kullanan her sayfada otomatik yüklü olduğunu söyler — dondurulmuş `_Layout` bu iddianın dışındadır. Not: `:215` eski `_Layout`'u "812 satır" diye anar; ölçülen 606 satırdır (kural metni bayat, davranış değil).
- **Yardım ve kısayol katmanı** yalnız iki kabukta yüklüdür (`page-help-and-keyboard.md:18-21`); `_Layout`'a düşen görünümlerde yoktur (ölçüm 2026-10-10: 99 görünüm, kuralın kendi sayımı).

**Kontrol listesine bağ:** UIC-1.

## 4. Görsel belirteçler

- **Tek stil dosyası:** geliştirmeler `wwwroot/assets/css/backbone-custom.css` üzerinden yapılır (`.antigravity/rules/frontend-standards.md:203`); vendor CSS (`wwwroot/assets/vendor/css/core.css`) düzenlenmez (`frontend-js-standard.md:145`). Dosya **8622 satır**.
- **Renk belirteçleri:** sabit renk yok, tüm renkler CSS değişkenleriyle (`var(--bs-*)`) (`frontend-standards.md:58` "No Hardcoded Colors"); details yüzeyinde border/radius/background hardcoded yazılmaz, `var(--bs-card-border-radius)`, `var(--bs-card-bg)` kullanılır (`:130`; `frontend-details-template.md:10`). Ölçüm: `grep -c -e "--diten-" -e "--bs-" backbone-custom.css` = **1152**; bunların yalnız **8** satırı `--diten-` öneklidir — ürün belirteç ailesi bugün ağırlıkla `--bs-*` (Bootstrap/Sneat) üzerindedir.
- **Açık / koyu tema:** `grep -c 'data-bs-theme="dark"'` = **19**, `grep -c '\[data-bs-theme=dark\]'` = **13** (toplam 32 satır koyu tema seçicisi). Koyu temada okunabilirlik elle ölçülür (BL-236 yöntemi, `product-backlog.md:2564-2572`: `--bs-secondary-color` kart yüzeyinde açık 2.29, koyu 3.49 — 20/20 metin AA altı; "tasarım sistemi kararı", dokunulmadı). Sneat tema özelleştiricisi üç kabuktan kaldırılacak (BL-337, `product-backlog.md:3993-3997`, AÇIK).
- **RTL:** `grep -c '\[dir="rtl"\]' backbone-custom.css` = **13**; `ar` kiracı dilidir (`localization-standard.md:9`), çakışmasız render elle ölçülür.
- **FG-003 — inline CSS yasağı:** `element.style.*` ile yükseltme FG-003 ihlali (`.antigravity/rules/frontend-js-standard.md:145`); kart başına ayrı yükseklik/padding hesabı yasak, sınıf `backbone-custom.css`'te (`frontend-standards.md:194`; `views-organization.md:91`). Ölçüm: `style="` içeren Archive dışı `.cshtml` = **93** dosya (`grep -rl 'style="' Views --include='*.cshtml' | grep -v /Archive/`). `premium-modal-standard.md:27-28, :35-36, :43-44, :51-52` ikon şablonları inline `style=` yazar (4 şablon × 2 satır: halka `div` + ikon `i` = sekiz satır) → §9 satır 1 (D-8a: karar: kuralın kendisinde düzeltilir); kural dosyası düzeltilene dek DL-2 bu sekiz satırı "tolere edilen tek `style=`" sayar ve kayda geçirir.
- **Bootstrap önceliği:** utility sınıfları (`d-flex`, `mb-4`), semantik badge sınıfları (`bg-label-*`) (`frontend-standards.md:56-57`).
- **Ölçülmedi:** belirteç başına kontrast, odak halkası rengi, koyu tema ekran görüntüleri — depoda araç yok (FU-8); DL-2 elle maddeleri.

**Kontrol listesine bağ:** UIC-9, UIC-10, UIC-13.

## 5. Erişilebilirlik

Kaynak: `.antigravity/rules/frontend-datatable-template.md:628-634` "Accessibility (A11y) Minimumları (v2)":

| Madde | Kural satırı | Canlı ölçüm (2026-10-23) |
|---|---|---|
| Icon-only toolbar düğmesi `title` + `aria-label` | `:630` | fabrika filtre düğmesi `diten-datatable.js:1046`: `title`, `aria-controls`, `aria-expanded` var, **`aria-label` yok**; dosyada `aria-label` yalnız `:600` (çoklu seçim temizle) ve `:1303` (okuma) — FU-10, XMC-001 |
| Filtre tetikleyici `aria-controls="inlineFilterCollapse"` + `aria-expanded` | `:631` | `:1046` taşır |
| Select2 açılınca arama kutusu odak alır | `:632` | ölçülmedi (çalışma zamanı DOM) |
| Odak halkası kaldırma / override yasak | `:633` | BL-100 (`product-backlog.md:1819-1822`): 2026-08-14 gerçek Tab ile 60 durak sayıldı, tema sınıflarında halka yok, AÇIK; BL-112 (`:1972-1975`): tema 3px kendi rengiyle eziyor, AÇIK |
| Kontrast AA 4.5:1 | kuralda açık satır yok; BL-236 (`product-backlog.md:2564-2572`) | `--bs-secondary-color` açık 2.29 / koyu 3.49, 20/20 metin AA altı; AÇIK, SAHİPSİZ |
| Form ikonu `aria-hidden="true"` | `frontend-form-template.md:24-25` | fabrika dışı; form başına ölçülür (UIC-27) |

- Ölçüm yöntemi: odak halkası gerçek Tab tuşuyla `n/N` durak sayımı (BL-100 yöntemi); kontrast DevTools ile belirteç başına tablo. Depoda kontrast ya da odak ölçen betik yoktur (FU-8) — bu maddeler DL-2'de "elle" kalır.
- Klavye: Enter = birincil işlem, geri alınamaz işlem Ctrl/⌘+Enter; `?` yardım paneli; kısayollar ortak katmanda (`page-help-and-keyboard.md:16, :105-118, :170-176`) — §2'de.
- Erişilebilirlik tanım şeması (Blueprint "accessibility checklist definitions", UI-GOV-BUNDLE) ayrı P2 paketinin konusudur (P2 bu paketten çıktı — ilk tüketiciyle ayrı paket, D-4); P1 yalnız bu satırları indeksler.

**Kontrol listesine bağ:** UIC-11, UIC-12, UIC-29.

## 6. Dil

- **Dil kümesi:** Platform (admin) modülü 2 dil (`en`, `tr`); kiracı modülü 7 dil (`en`, `tr`, `fr`, `es`, `zh`, `ar`, `ru`), tamamı için `.resx` (`.antigravity/rules/localization-standard.md:5-11`; `AGENTS.md:136`). Yalnız `tr` bırakmak kural ihlali (`:11`); non-English dosyada değerin İngilizceyle aynı kalması (placeholder) yasak (`:14-16`). Canlı: `Resources/Views/Platform/Tenants/TenantsIndex.{en,tr}.resx` (2); `Resources/Views/DevEnablement/GoldenReferenceSlim/GoldenReferenceSlimIndex.*.resx` (7).
- **Hardcoded metin yasak:** `localization-standard.md:3`; toolbar/action metninde `|| 'Export'` tarzı fallback yasak, eksik anahtar teslimi durdurur (`:56-58`); form ve details'ta görünen her metin `@Localizer` / `@SharedLocalizer` (`frontend-form-template.md:14`; `frontend-details-template.md:12`).
- **Zorunlu anahtarlar (§2):** `[ModuleName]Title`, `PageDescription`, `AddNew[ModuleName]`, `<thead>` sütun anahtarları (`localization-standard.md:29-34`). Golden Slim fixture `AddNewGoldenReferenceSlim` yerine `AddNew` taşır (FU-12, developer-enablement sahibi).
- **SharedResource / ViewResource ayrımı:** ortak ifadeler yalnız `SharedResource.{lang}.resx`; sayfaya özel metin marker yolunda (`localization-standard.md:36-45`). Toolbar sözlüğü 12 anahtar SharedResource'tan: `Search`, `Export`, `Import`, `Filter`, `Apply`, `Reset`, `ShowAll`, `SaveView`, `ColumnVisibility`, `Status`, `Cancel`, `AreYouSure` (`:49-54`). Casing: aksiyon düğmeleri Title Case, `zh`/`ar` muaf (`:18-21`).
- **Marker sınıfı:** `{ModuleName}Index` sınıfı ile resx dosya adı birebir aynı; aksi hâlde `IHtmlLocalizer<T>` hiçbir anahtarı çözmez (`frontend-datatable-template.md:250-261`).
- **`window.L10n` köprüsü:** JS metni için partial + JSON yük + loader deseni zorunlu (`.antigravity/rules/dynamic-localization-standard.md:28-30`). İki köprü mekanizması (`:78-101`): **A — otomatik sayım** `Localizer.GetAllStrings(true)` (örnek `Views/WorkCenterNext/_L10n.cshtml`), **B — elle tutulan liste** (örnek `Views/Tasks/_IndexL10n.cshtml`). "YENİ YÜZEY AÇIYORSAN A DESENİNİ KULLAN" (`:94`); mevcut B yüzeyleri bu turda çevrilmez (`:99-100`, BL-308). Ölçüm: A deseni 5 görünümde (`grep -rl 'GetAllStrings(true)' Views`); Golden Slim B desenidir (`GoldenReferenceSlim/_IndexL10n.cshtml:9-13` açık anahtarlar) — fixture'ın kusuru değil, doğrulayıcı A desenini test etmez (`grep -c GetAllStrings verify_datatable_page.py` = 0).
- **Sayfa yardımı anahtarları:** `Help.{KOD}.Purpose` / `Audience` / `Step{n}` / `Step{n}Title` (1–5) / `Related.{İLGİLİ KOD}`, kiracı 7 dil, platform 2 dil (`page-help-and-keyboard.md:216-223`). Kısayol açıklamaları `Shortcuts.*` 7 dil (`:174`).
- **Yetkisiz metin:** 7 dil, resx'ten, ne yapılacağını söyler; `403`, `Forbidden` gibi teknik metin yasak (`unauthorized-surface-standard.md:77-82`).

**Kontrol listesine bağ:** UIC-6, UIC-7, UIC-8, UIC-15.

## 7. Yetkisiz yüz, Ctrl+K kaydı, kayıt kodu

**Yetkisiz yüz (UAS-001).** Sayfa, iznine sahip olmayan kullanıcıya iskeletini çizmez — ne başlık, ne kart, ne boş tablo, ne eylem düğmesi; yerine tek bir açıklama (`.antigravity/rules/unauthorized-surface-standard.md:25-31`). Yönlendirme yapılmaz; dört gerekçe `:33-44` (adres kaybolur, geri tuşu döngüye girer, "yetkin yok" ile "sayfa yok" ayrılmaz). Yeni sayfa, yetkisiz senaryo canlı denenmeden kapanmaz (`:108-112`). Canlı kod: `wwwroot/assets/js/diten-unauthorized.js` (52 satır, `window.DitenUnauthorized` `:41`; toast için `window.showToast` `:36`). `NotAuthorized.cshtml` kuralın kendi notuna göre karışık dillidir (`:78-79`) — ölçülmedi.

**Ctrl+K kaydı.** Platform/admin UI modülü iki dilli statik JSON'a kaydolur: `wwwroot/assets/json/platform-search.en.json` ve `.tr.json` (`.antigravity/rules/platform-global-search-registry.md:28-33`); `url` ve `icon` çevrilmez, `name`, `group`, `keywords` dile göre (`:33`). Teslim kapısı: iki dosyaya aynı rota seti, Ctrl+K iki dilde açılır ve sonuç doğru rotaya gider (`:80-87`). PROD-005 aynı zorunluluğu kabuk kuralına bağlar (`frontend-standards.md:213`). Ölçüm: her iki dosya 174 satır; `/Platform/Tenants` her ikisinde `:7` ve `:117`; eski `platform-search.json` hâlâ dizindedir (3 dosya).

**Kayıt kodu (RCS-001).** Kullanıcıya görünen kod `{ŞİRKET}-{TÜR}-{SAYAÇ}`, örnek `MG-ORG-000042`; sayaç en az 6 hane, hiç sıfırlanmaz (`.antigravity/rules/record-coding-standard.md:22-33`). Tür öneki 3 büyük harf, §3 tablosunda merkezî kayıt (`:98-100`). Bütün türler **tek ortak kod üretme servisini** kullanır; bugün dört ayrı sayaç var (`:134-136`). Paket kontrol listesi: sınıf (OTOMATİK / İKİSİ DE / KALSIN), tür öneki, ortak servis, değiştirilemezlik, kiracı genelinde benzersizlik, testler (`:140-152`). **Canlı durum:** ortak servis yok — `grep -rl -e 'IRecordCodeService' -e 'IRecordCodeGenerator' services --include='*.cs'` = **0** dosya. MOD-0286 P1'in kullanıcıya görünen kaydı yoktur ("yok"); P2 bu paketten çıktı — ilk tüketiciyle ayrı paket (D-4): o paketin `UiPagePattern` kodu için sınıf OTOMATİK önerilir, önek TBD (ayrı paketin girdisi, bu pakette karar yok).

**Nav anahtarları.** Paketteki nav anahtarları çalışma zamanı biçiminde (`Nav.Page.CODE`, büyük harf, alfanümerik); `scripts/check_pack_nav_keys.py` ölçer (paket UIC-21, AC-8). P1'de sayfa yok; P2 bu paketten çıktı — ilk tüketiciyle ayrı paket (D-4): o paketin girdisi `Nav.Module.UIGOVERNANCE`, `Nav.Page.UIPAGEPATTERNS`, `Nav.Page.UICOMPLIANCECHECKLIST` (bu pakette anahtar tanımlı değil).

**Kontrol listesine bağ:** UIC-16, UIC-17, UIC-18, UIC-21.

## 8. API profili

İki profil vardır (`.antigravity/rules/routes.md:68-99`):

| Profil | Nerede | Tarayıcı JS ne çağırır | Kaynak |
|---|---|---|---|
| Profil 1 — `proxy-profile` | Platform / admin MVC (varsayılan) | aynı origin'deki MVC proxy, ör. `/Platform/ModuleCatalog/api`; token'ı sunucu `AuthTokenCookies.GetAccessToken(Request)` ile okur, parçalı çerez (`access_tokenC1..CN`) elle okunmaz | `routes.md:72-83` |
| Profil 2 — `direct-gateway-profile` | kiracı / public kabuk, tarayıcı-güvenli başlık stratejisi olan ekranlar | `window.API.{service}` + `/api/...` (servis yolu `/services/mdm/api/v1/...` yazılmaz) | `routes.md:85-94` |

- Her iki profilde tarayıcı JS mikroservis portlarına (`5056`, `5057`, `5058`) doğrudan gitmez (`routes.md:96`); Web (5001) servislere Gateway (5000) üzerinden gider (AGENTS.md §3 port tablosu — tek kaynak).
- **Token tarayıcıda yok:** `document.cookie`, `access_token`, `Authorization: Bearer` DataTable JS'inde yasak; HttpOnly token'ı yalnız MVC proxy okur, JS `getAuthHeaders()` ile yalnız güvenli UI başlıklarını ekler (`.antigravity/rules/frontend-js-standard.md:990`; `routes.md:83`). `url: window.ApiBaseUrl + '/mdm/api/v1/...'` yasak (`frontend-js-standard.md:989`).
- **Lookup (JS-015):** form ve filtre select'leri sunucu tarafında (`asp-items`) doldurulmaz, AJAX/Fetch ile gelir; Platform lookup SSOT `Diten.Platform` `/api/lookups/{key}`; hardcoded fallback liste yasak (`frontend-js-standard.md:1-17`).
- **Ölçüm aracı:** `verify_datatable_page.py --api-profile proxy` / `--api-profile direct-gateway` (`.antigravity/scripts/verify_datatable_page.py:919`); `proxy` profilinde kişiselleştirme proxy sözleşmesi ayrıca denetlenir (`:66-78`). Golden Slim bugün bu dosyada bir kırmızı taşır (`personalization-client.js` kiracı başlığı, FU-4); bu belge yeşil bir taban iddia etmez.
- Paket kuralı: her liste paketi `Gateway / API Routing Decision` bölümünde profilini yazar (`module-pack-standard.md:387`).

**Kontrol listesine bağ:** UIC-19, UIC-20.

## 9. Çatışma kaydı

Kaynak: paket Implementation Notes "Çatışma kaydı" (`paket:769-777`) ve D-5, D-8a–D-8c. Kural-kural çatışmasında bu belge **taraf tutmaz**; "uygulanacak kaynak" sütunu paketin önerisidir, karar kural sahibinindir. "Kural kazanır" ilkesi yalnız kural–şartname satırında geçerlidir (AC-3). Hiçbir kural-kural satırı kayıtlı sahip kararı olmadan "çözüldü" yazmaz.

| # | Taraflar (`yol:satır`) | Aynı katman | Uygulanacak kaynak (öneri) | Gerekçe | Karar veren | Durum |
|---|---|---|---|---|---|---|
| 1 | `.antigravity/rules/premium-modal-standard.md:27-28, :35-36, :43-44, :51-52` ikon HTML'inde inline `style="font-size: 2.5rem; color: …"` ↔ FG-003 `frontend-js-standard.md:145`, `frontend-standards.md:194` inline CSS yasağı | evet (`.antigravity/rules`) | FG-003: ikon sınıfları `backbone-custom.css`'e taşınır; §2 ayrı onaylı commit ile düzeltilir | FG-003 daha yeni ve kesişen kural (UI-001 ailesi); modal parçası çalışma zamanı okuyucusu olmayan şablon detayı | kural sahibi (sahip, `.antigravity/**`) | karar: kuralın kendisinde düzeltilir (sahip 2026-10-26, karar kaydı pss-mod0286-daraltma-01 madde 3; kural dosyası düzeltilene dek UIC istisnayı kayda geçirir) (D-8a) |
| 2 | `frontend-js-standard.md:53` 7.1 "Tercih edilen yol: `createCrudTable`" ↔ `frontend-datatable-template.md:87`, `:100`, `:114` (`createList` fabrikası) ve BL-440 (`product-backlog.md:6199` başlık, `:6264` `createList` notu; karar 2026-09-23). Not: `frontend-js-standard.md:981-991` yasak pratikler tablosu `createCrudTable` ya da `createList` adlandırmaz (paket D-8b) | evet (aynı katman, iki dosya) | `createList`; `:53` `createList`'i tercih edilen, `createCrudTable`'ı eski yol diye yazar | doğrulayıcı `createList` çözer (`verify_datatable_page.py:535-536`); canlı: 37 dosya `createList`, 81 dosya `createCrudTable`; `:53` bayat | kural sahibi (sahip / BL-440 sahibi CT) | karar: kuralın kendisinde düzeltilir (sahip 2026-10-26, karar kaydı pss-mod0286-daraltma-01 madde 3; kural dosyası düzeltilene dek UIC istisnayı kayda geçirir) (D-8b) |
| 3 | `frontend-standards.md:208` "manuel `Layout =` opsiyoneldir" ↔ `module-pack-standard.md:88` "layout ACIKCA yazilir" ve `views-organization.md:39` "layout açıkça seçilir" | evet | açık layout (UIC-1); `:208` ayrı onaylı commit ile düzeltilir | iki kural bire karşı; `module-pack-standard.md:292, :608` açık layout'u test edilebilir AC yapar | kural sahibi (sahip) | karar: kuralın kendisinde düzeltilir (sahip 2026-10-26, karar kaydı pss-mod0286-daraltma-01 madde 3; kural dosyası düzeltilene dek UIC istisnayı kayda geçirir) (D-8c) |
| 4 | "MOD-0013" etiketi `premium-modal-standard.md:1, :11`, `dynamic-localization-standard.md:2, :5, :181`, `.antigravity/ARCHITECTURE.md:234`, `.antigravity/workflows/debug.md:59`, `AGENTS.md:139` (paket atfı; ölçüm 2026-10-27: etiket bugün `AGENTS.md:140`'ta — paket revizyonu girdisi) ↔ Blueprint 8.8 MOD-0013 = Platform Standards Registry | evet (etiket) | MOD-0286 | kayıt satırı 70 UI-standart kavramını MOD-0286'ya bağlar; yalnız etiket, kod okumaz | sahip (`.antigravity/**`) | karar bekliyor (D-5, FU-2) |
| 5 | kural ↔ bu şartname (herhangi bir satır) | hayır (şartname kuralın altında, D-2) | kural | yetki sırası `AGENTS.md:40-50`; şartname indeksler, değiştirmez | paket yazarı şartnameyi düzeltir | çözüldü (yapısal) |

Bulgu olup çatışma olmayanlar (kayıt için): `frontend-standards.md:215` eski `_Layout` "812 satır" der, ölçülen 606; `frontend-datatable-template.md:153` "132'si sapmış" sayımı bu belgede yeniden ölçülmedi. Yeni bir çatışma bulunduğunda satır **eklenir**, var olan satır silinmez; durum yalnız sahip kararının kayıt yoluyla "çözüldü"ye döner.

**Kontrol listesine bağ:** doğrudan UIC maddesi yok; AC-3 bu tabloyu ölçer.

---

### Ek A — Açık noktalar

| # | Nokta | Kimin | Not |
|---|---|---|---|
| A-1 | **Sihirbaz kuralı yok.** Tek sihirbaz yüzeyi `Views/MasterData/LegalEntities/Wizard.cshtml` (438) + `wizard.js` (557); Blueprint "Wizard Pattern Governance" sayfası ayrı P2 paketinde (P2 bu paketten çıktı, D-4) | sahip / pack author (FU-7) | §1; MOD-0220 paketi `ready-for-dev` iken kod var (FU-6, MDM sahibi) |
| A-2 | **Ortak kayıt kodu servisi yok** (`services/` 0 dosya); UIC-18 yalnız paket metninde ölçülür | sahip / RCS-001 sahibi | §7; `record-coding-standard.md:134-136` dört ayrı sayaç |
| A-3 | **FU-3:** ikinci `_ListShell.cshtml` `Views/PPM/Shared/` (41 satır); doğrulayıcı yalnız paylaşılanı okur | PPM paket sahibi (XMC-001) | §2 |
| A-4 | **FU-4:** Golden Slim doğrulayıcı kırmızı (`personalization-client.js` kiracı başlığı, exit 1, 2026-10-22) | developer-enablement / BL-440 sahibi | §8; her Slim kopyası kırmızıyı miras alır |
| A-5 | **FU-10:** fabrika filtre düğmesi `diten-datatable.js:1046` `aria-label` taşımaz (`frontend-datatable-template.md:630`) | BL-440 sahibi (CT), XMC-001 | §5; diğer icon-only düğmeler ölçülmedi |
| A-6 | **FU-12:** Golden Slim `AddNew` ↔ kural `AddNew[ModuleName]` (`localization-standard.md:33`) | developer-enablement sahibi (XMC-001) | §6 |
| A-7 | `--diten-*` belirteç ailesi yalnız 8 satır; belirteç adlandırma kararı (`--diten-*` mi `--bs-*` mi) kuralda yok | sahip (ayrı P2 paketi, D-4) | §4 |
| A-8 | Erişilebilirlik ölçüm aracı yok (kontrast, odak); UIC-11/12 elle (FU-8) | sahip / CT | §5 |
| A-9 | Mobil (Android) tasarım kiti bu belgenin dışında (D-7); bu belge yalnız Razor / Bootstrap / `backbone-custom.css` yüzeyini ölçer | sahip | ayrı P2 paketinde yeniden sorulur (P2 bu paketten çıktı, D-4; D-7) |

### Ek B — Sınırlar

- Bu belge **kural değildir**; `.antigravity/rules/` altındaki 11 UI kural dosyasını ve ilgili dört dosyayı (`module-pack-standard`, `platform-global-search-registry`, `record-coding-standard`, `routes`) indeksler. Kural ile çelişirse kural kazanır; çelişki §9'a satır olarak eklenir (D-2).
- Ölçümler 2026-10-23, HEAD `005a0cc58`, worktree `chore/ct/pss-ui-ct-01`; komutlar metnin içinde verilmiştir ve `wc -l`, `grep -n`, `grep -c`, `sed -n` ile yeniden üretilebilir. Kural satır numaraları kural dosyası değişince kayar; her revizyonda yeniden ölçülür.
- Çalışma zamanı DOM'u (DataTables Buttons'ın eklediği öznitelikler, select2 odak, koyu tema render, RTL çakışma) **ölçülmedi**; bunlar DL-2'nin elle maddeleridir.
- `Views/PPM/Shared/_ListShell.cshtml`, MOD-0220 sihirbazı, DEV-0000 / DEV-0001 kodu başka modüllerin dosyalarıdır; bu belge onları adlandırır, dokunmaz (XMC-001).
- Bu belge paketin DL-1 dilimidir; DL-2 (`compliance-checklist.md`, `UIC-1…32` + `RC-1…24`) ve ölçüm kaydı (`docs/records/audits/2026-10/mod-0286-uic-baseline-01/`) aynı iş paketinde, PSS-UI-P1-03'te ölçülür (DL-1 ve DL-2 tek iş paketi, tek kabul kaydı: karar kaydı `pss-mod0286-kabul-tek-01`). Paket rev 12 `ready-for-dev` ve onaylıdır (PR #245 merge `c51568aa8`), her iki kapı kapalıdır; DL-3 (UI uyum CI bağlantısı) ayrı iş paketidir. Kabul bu belgede yazılı değildir; Control Tower'ın tek kabul kaydına bağlıdır.

### Ek C — Ölçüm komutları ve sonuçları (2026-10-23, HEAD `005a0cc58`)

Komutlar depo kökünden (`frontend/Diten.Web/` altındakiler o klasörden) koşuldu; sonuçlar bu belgenin ilgili bölümünde kullanıldı. Yeniden ölçüm aynı komutla yapılır; fark çıkarsa belge değil ölçüm güncellenir. Sayımlar `... | wc -l` ile alındı; tablo hücresinde boru işareti kullanılmadığı için "sayımı" diye yazıldı.

| Bölüm | Komut | Sonuç |
|---|---|---|
| künye | HEAD kısa sha (`rev-parse --short HEAD`) | `005a0cc58` |
| §1 | `find Views -name Index.cshtml -not -path '*/Archive/*'` sayımı | 192 |
| §1 | `grep -rl 'createList(' wwwroot/assets/js --include='*.js'` sayımı | 37 |
| §1 | `grep -rl 'createCrudTable(' wwwroot/assets/js --include='*.js'` sayımı | 81 |
| §1 | `grep -rl 'DtDefaults' wwwroot/assets/js --include='*.js'` sayımı | 177 |
| §1 | `grep -c form_field_count .antigravity/scripts/verify_datatable_page.py` | 0 |
| §1 | `wc -l Views/MasterData/LegalEntities/Wizard.cshtml wwwroot/assets/js/MasterData/LegalEntities/wizard.js` | 438 · 557 |
| §1 | `grep -n 'Layout = ' Views/MasterData/LegalEntities/Wizard.cshtml` | `:9` `_LayoutTenantShell` |
| §1 | `grep -ln "NOT a Golden DataTable CRUD surface" execution/domains/*/module-packs/*.md` | MOD-0155-FU02/FU03/FU04/FU05 + MOD-0286 |
| §2 | `wc -l` (envanter tablosundaki 17 dosya) | tabloda satır satır |
| §2 | `grep -n "window.showConfirm\s*=" Views/Shared/_GlobalConfirmation.cshtml` | `:206` |
| §2 | `grep -n -e "window.showToast\s*=" -e "window.DitenToastText\s*=" -e "new Notyf" Views/Shared/_GlobalNotification.cshtml` | `:133` · `:109` · `:179` |
| §2 | `grep -n -e "function createList" -e "function createCrudTable" -e "dt-save-filter-btn" -e "colReorder:" -e "'aria-controls'" wwwroot/assets/js/diten-datatable.js` | `:961` · `:251` · `:994, :1052` · `:1083` · `:1046` |
| §2 | `grep -rl '_ListShell' Views --include='*.cshtml'` sayımı (iki kabuk dosyası hariç) | 41 |
| §2 | `grep -rl '_TableSkeleton' Views --include='*.cshtml'` sayımı | 33 |
| §2 | `grep -rl 'select2' wwwroot/assets/js --include='*.js'` sayımı | 210 |
| §2 | `grep -n "fetch(" wwwroot/assets/js/personalization-client.js` | `:125, :143, :155` |
| §2 §3 | `grep -c -e '_PageHelpButton' -e '_PageHelpPanel' -e '_DitenShortcuts' Views/Shared/_Layout*.cshtml` | PlatformAdmin 3 · TenantShell 3 · `_Layout` 0 |
| §3 | `wc -l Views/Shared/_LayoutPlatformAdmin.cshtml Views/Shared/_LayoutTenantShell.cshtml Views/Shared/_Layout.cshtml Views/Platform/_ViewStart.cshtml` | 448 · 703 · 606 · 3 |
| §3 | `cat Views/_ViewStart.cshtml Views/Platform/_ViewStart.cshtml` | `_Layout` · `_LayoutPlatformAdmin` |
| §4 | `wc -l wwwroot/assets/css/backbone-custom.css` | 8622 |
| §4 | `grep -c -e "--diten-" -e "--bs-" wwwroot/assets/css/backbone-custom.css` | 1152 |
| §4 | `grep -c -- "--diten-" wwwroot/assets/css/backbone-custom.css` | 8 |
| §4 | `grep -c 'data-bs-theme="dark"' …/backbone-custom.css` · `grep -c '\[data-bs-theme=dark\]' …` | 19 · 13 |
| §4 | `grep -c '\[dir="rtl"\]' wwwroot/assets/css/backbone-custom.css` | 13 |
| §4 | `grep -rl 'style="' Views --include='*.cshtml'` sayımı (Archive hariç) | 93 |
| §5 | `grep -n 'aria-label' wwwroot/assets/js/diten-datatable.js` | `:600`, `:1303` (toolbar değil) |
| §5 | `grep -n -e "^### BL-100" -e "^### BL-112" -e "^### BL-236" -e "^### BL-337" -e "^### BL-440" -e "^### BL-452" docs/roadmap/backlog/product-backlog.md` | 1819 · 1972 · 2564 · 3993 · 6199 · 6573 |
| §6 | `grep -rl 'GetAllStrings(true)' Views` sayımı | 5 |
| §6 | `ls Resources/Views/DevEnablement/GoldenReferenceSlim/` · `find Resources -path '*Platform/Tenants*' -name '*.resx'` | 7 · 2 |
| §6 | `grep -c GetAllStrings .antigravity/scripts/verify_datatable_page.py` | 0 |
| §7 | `grep -rl -e 'IRecordCodeService' -e 'IRecordCodeGenerator' services --include='*.cs'` sayımı | 0 |
| §7 | `grep -n '"/Platform/Tenants"' wwwroot/assets/json/platform-search.{en,tr}.json` | her ikisinde `:7`, `:117` |
| §7 | `wc -l wwwroot/assets/json/platform-search.en.json wwwroot/assets/json/platform-search.tr.json` · `ls wwwroot/assets/json/` | 174 · 174 · 3 `platform-search*` dosyası |
| §8 | `grep -n -e "api-profile" -e "api_profile" .antigravity/scripts/verify_datatable_page.py` | `:19-20` (örnek), `:66, :78`, `:919` |
| §9 | `wc -l .antigravity/rules/{11 UI kural dosyası}.md` | 3187 (paket Module Summary ile aynı) |
| §9 | `grep -n "MOD-0013" .antigravity/rules/premium-modal-standard.md .antigravity/rules/dynamic-localization-standard.md AGENTS.md` | `:1, :11` · `:2, :5, :181` · `:139` |
| kendi | `grep -c '^## ' spec.md` · `grep -c '\.antigravity/rules/' spec.md` | 9 · bu dosyanın raporunda |

Ölçülmeyenler: çalışma zamanı DOM'u (select2 odak, DataTables Buttons öznitelikleri), koyu tema / RTL render, belirteç kontrastı, odak halkası; MOD-0155 bespoke konsollarının canlı kodu; `frontend-datatable-template.md:153` "132 sapmış" sayımı; `wwwroot/assets/js/shared/` altındaki P1 dışı 12 dosyanın giriş noktaları.

# Prompt Guide

Bu dosya, ERP-vNext icin kullanilabilir prompt katalogudur. Amac, gelistirme sirasinda yanlis agent secimi, eksik kapsam, eski UI pattern'leri ve yarim teslimleri azaltmaktir.

Varsayilan giris noktasi `@[.antigravity/agents/orchestrator.md]` olmalidir. Dogrudan uzman agent cagirilari sadece dar, net ve dusuk-riskli gorevlerde kullanilmalidir.

## Temel Ilkeler

- `@orchestrator` varsayilan giris noktasi. Yeni modul, cok dosyali degisiklik, test+doc+gateway etkisi olan her istekte bunu kullan.
- "Products gibi olsun" tek basina yeterli degildir. Beklenen davranislari maddeler halinde yaz.
- Prompt; kapsam, degistirilmeyecekler, kabul kriterleri ve dogrulama beklentisi icermelidir.
- DataTable islerinde ilgili rule ve workflow dosyalarini acikca referans ver.
- Dokumantasyon ve test isteniyorsa bunu ayrica yaz. Yazilmazsa unutulma riski vardir.
- Fix islerinde urun kodu ve `.antigravity` guncellemesi ayni promptta birbirine karismamali.

## Ne Zaman Hangi Giris Noktasi

| Ihtiyac | Onerilen giris noktasi | Not |
|---|---|---|
| Yeni modul | `@orchestrator` + `/add-module` | En guvenli akis |
| Mevcut CRUD sayfa duzeltmesi | `@orchestrator` | UI, JS, quality gate birlikte yonetilir |
| Sadece L10n | `@orchestrator` veya `l10n-agent` | Tek katmanliysa dogrudan agent olur |
| Sadece RBAC | `@orchestrator` veya `security-agent` | Endpoint sayisi azsa dogrudan agent kullanilabilir |
| Sadece route/gateway | `@orchestrator` veya `integration-agent` | Ocelot etkisi dar ise dogrudan agent olur |
| Kod inceleme / audit | `@orchestrator` | Cok katmanli rapor icin daha dogru |
| Test yazdirma | `@orchestrator` veya `testing-agent` | Sadece test uretilecekse agent yeterli |
| Dokumantasyon | `@orchestrator` veya `documentation-writer` | Kod etkisi yoksa dogrudan agent uygun |
| Debug / root cause | `@orchestrator` veya `debugger` | Izolasyon isiysa `debugger` uygundur |

## Prompt Yazma Kurallari

Her iyi promptta asagidakiler bulunmali:

1. Giris noktasi:
   - `@[.antigravity/agents/orchestrator.md]`
   - veya dogrudan uzman agent
2. Gorev tipi:
   - yeni modul, endpoint, fix, migration, audit, test, release, documentation
3. Kapsam:
   - hangi dosya/katmanlar degisebilir
4. Degistirilmeyecekler:
   - backend, gateway, resources, archive gibi sinirlar
5. Kabul kriterleri:
   - beklenen davranislar
6. Dogrulama:
   - build, browser smoke, xUnit, quality gate, tenant audit
7. Ilgili standartlar:
   - hangi rule/workflow okunacak
8. Dokumantasyon/test beklentisi:
   - ozellikle isteniyorsa acik yaz

## Kisa Kontrol Listesi

- Giris noktasi dogru mu
- Gorev tipi net mi
- Davranis maddeleri yazildi mi
- "Degistirilmeyecekler" bolumu var mi
- Hangi rule/workflow okunacagi yazildi mi
- Build/test/browser beklentisi net mi
- Dokumantasyon gerekiyorsa acikca istendi mi

## Anti-Pattern'ler

### 1. Muğlak referans modulu

Yanlis:

```text
@[.antigravity/agents/orchestrator.md]

Countries sayfasini Products gibi yap.
```

Dogru:

```text
@[.antigravity/agents/orchestrator.md]

Countries sayfasini su davranislarla duzelt:
- inline filter kullan
- stateSave:false yap
- Save View shared personalizationClient ile manuel calissin
- import placeholder toast warning olsun
- quality-gate-datatable checklist'i PASS olsun

Degistirilmeyecekler: backend CQRS ve gateway rotalari
```

### 2. Workflow belirtmeden frontend istemek

Yanlis:

```text
@[.antigravity/agents/orchestrator.md]

Frontend'i guncelle.
```

Dogru:

```text
@[.antigravity/agents/orchestrator.md]

Products liste sayfasini duzelt.
Zorunlu referanslar:
- .antigravity/rules/frontend-datatable-template.md
- .antigravity/rules/frontend-js-standard.md
- .antigravity/workflows/quality-gate-datatable.md
```

### 3. Save View isteyip state model belirtmemek

Yanlis:

```text
Save View ekle.
```

Dogru:

```text
Save View ekle.
- stateSave:false olacak
- shared personalizationClient kullanilacak
- scope: filters + search + colVis + columnOrder + sorting
- pageNumber ve pageLength persist edilmeyecek
```

### 4. Filter isteyip inline/offcanvas ayrimini yazmamak

Yanlis:

```text
Filter ekle.
```

Dogru:

```text
Inline collapsible filter ekle.
- offcanvas filter yasak
- #inlineFilterHost ve #inlineFilterCollapse kullan
- toolbar altina mount et
```

### 5. Test turunu belirtmemek

Yanlis:

```text
Test et.
```

Dogru:

```text
Su dogrulamalari yap:
- xUnit testleri yaz
- browser smoke yap
- quality-gate-datatable checklist'ini isaretle
- tenant-audit raporu ver
```

### 6. Dokumantasyonu unutmak

Yanlis:

```text
Modulu bitir.
```

Dogru:

```text
Modul tamamlandiginda:
- documentation-writer ile Swagger/README guncelle
- user-manual-generator ile ekran kilavuzu uret
```

### 7. Yanlis agent secimi

Yanlis:

```text
@[.antigravity/agents/frontend-ui-ux.md]

Countries modulune RBAC ekle.
```

Dogru:

```text
@[.antigravity/agents/security-agent.md]

Countries endpoint'lerine HasPermission ekle.
```

### 8. data-dt-standard attribute'unu atlamak

Yanlis:

```html
<table class="datatables-countries table border-top">
```

Dogru:

```html
<table id="dt-countries" data-dt-standard="v2" class="datatables-countries table border-top">
```

Neden: `data-dt-standard="v2"` attribute'u olmayan tablolar DtDefaults v2 davranisini (colReorder, Save View entegrasyonu, inline filter mount) tetiklemez. Her yeni DataTable tablosunda bu attribute zorunludur.

### 9. Toast tipini belirtmemek veya yanlis tip kullanmak

Yanlis:

```js
window.showToast?.(L.ComingSoon, 'info');  // import placeholder icin mavi gosterir
```

Dogru:

```js
window.showToast?.(L.ComingSoon, 'warning');  // import/export placeholder icin turuncu
```

Toast tip standardi:

| Durum | Tip |
|---|---|
| Basarili islem (create/update/delete/save) | `'success'` |
| Import / Export placeholder (Coming Soon) | `'warning'` |
| Hata | `'error'` |
| Genel bilgilendirme | `'info'` |

### 10. stateSave'i acik birakmak

Yanlis:

```js
dt = new DataTable(dtTableEl, window.DtDefaults.create({
    ajax: { ... }
    // stateSave belirtilmemis — DtDefaults varsayilani aktif kalir
}));
```

Dogru:

```js
dt = new DataTable(dtTableEl, window.DtDefaults.create({
    stateSave: false,  // zorunlu: persistence sadece personalizationClient uzerinden
    ajax: { ... }
}));
```

Neden: stateSave acik kalirsa DataTables sayfa degisikliklerini localStorage'a otomatik yazar. Bu durum Save View butonunun hic gozukmemesine, filtre durumunun sayfa yuklenisinde sessizce geri yuklenmesine ve personalizationClient ile cift persistence'a yol acar.

## Ornek Prompt Katalogu

Asagidaki ornekler gelistirme asamasinda dogrudan kullanilabilir. Cogu `@orchestrator` ile baslar. Dogrudan agent kullanilanlarda bu tercih bilincli yapilmistir.

### A. Yeni Gelistirme

#### 1. Yeni MDM modulu ekleme

Kullanim: Sifirdan yeni bir CRUD/DataTable modulu kurmak istediginde.

```text
@[.antigravity/agents/orchestrator.md]

/add-module Currencies (MDM servisi)

Alan tanimlari:
- Code: string (zorunlu, ISO 4217)
- Name: string (zorunlu)
- Symbol: string (zorunlu)
- IsActive: bool

Is kurallari:
- Code tenant bazli benzersiz olmali

UI tipi: DataTable (Liste/CRUD)
Zorunlu referanslar:
- .antigravity/workflows/add-module.md
- .antigravity/rules/frontend-datatable-template.md
- .antigravity/rules/frontend-js-standard.md
- .antigravity/workflows/quality-gate-datatable.md
```

#### 2. Yeni Auth modulu ekleme

Kullanim: Auth servisinde yeni yonetsel modul acarken.

```text
@[.antigravity/agents/orchestrator.md]

/add-module Permissions (Auth servisi)

Alan tanimlari:
- Key: string (zorunlu)
- Description: string (opsiyonel)
- Group: string (zorunlu)
- IsActive: bool

Is kurallari:
- Key benzersiz olmali

UI tipi: DataTable (Liste/CRUD)
Auth ve RBAC etkisi var. security-agent ve integration-agent mutlaka dahil olsun.
```

#### 3. Yeni DataTable liste sayfasi ekleme

Kullanim: Mevcut backend uzerine yeni index/list ekranini eklemek istediginde.

```text
@[.antigravity/agents/orchestrator.md]

Cities modulu icin yeni DataTable index sayfasi olustur.

Zorunlu:
- .antigravity/rules/frontend-datatable-template.md birebir kullanilsin
- .antigravity/rules/frontend-js-standard.md kurallarina uyulsun
- _Filter.cshtml olusturulsun
- DtDefaults.create() kullanilsin

Degistirilmeyecekler:
- backend CQRS
- Mongo koleksiyon tasarimi
```

#### 4. Yeni Create/Edit form sayfasi ekleme

Kullanim: Liste modulu mevcutken form ekranlarini sonradan eklemek istediginde.

```text
@[.antigravity/agents/orchestrator.md]

Vendors modulu icin Create ve Edit sayfalarini ekle.

Beklenti:
- _LayoutBackbone kullanilsin
- validation ve localization tam olsun
- success/error toast akislari global notification ile uyumlu olsun

Degistirilmeyecekler:
- list page index.js
- gateway rotalari
```

#### 5. Yeni Details sayfasi ekleme

Kullanim: Quick View yanina tam detay sayfasi gerektiginde.

```text
@[.antigravity/agents/orchestrator.md]

Products modulu icin read-only Details sayfasi ekle.

Zorunlu referans:
- .antigravity/workflows/details-page-rules.md

Beklenti:
- agir veri icin full page details modeli kullan
- hardcoded text birakma
- unauthorized ve not-found akislari net olsun
```

#### 6. Yeni endpoint CQRS ekleme

Kullanim: Mevcut modulu yeni API davranisiyla genisletmek istediginde.

```text
@[.antigravity/agents/orchestrator.md]

/add-endpoint-cqrs Countries modulune

Yeni endpoint: POST /api/countries/bulk-import
Is mantigi: JSON body icindeki ulkeleri toplu ekle, duplicate ISO2 olanlari atla ve sonuc raporu don
Validation:
- liste bos olamaz
- her item icin Name ve Iso2Code zorunlu
- ISO2 uzunlugu 2 olmali
Auth: [HasPermission("Modules.Countries.Create")]
```

#### 7. Yeni bulk action ekleme

Kullanim: Var olan DataTable'a toplu islem butonu eklerken.

```text
@[.antigravity/agents/orchestrator.md]

Products liste sayfasina yeni bulk activate aksiyonu ekle.

Beklenti:
- checkbox secim modeli mevcut pattern ile ayni olsun
- tekil ve toplu aksiyonlarin toast dili tutarli olsun
- bulk action bar stale state birakmasin

Degistirilmeyecekler:
- Save View davranisi
```

#### 8. Yeni import/export capability ekleme

Kullanim: Liste sayfasina placeholder yerine gercek import eklerken.

```text
@[.antigravity/agents/orchestrator.md]

Countries modulune gercek Excel import ozelligi ekle.

Beklenti:
- import placeholder kaldirilsin
- duplicate ve validation raporu verilsin
- sonuc toast'lari error/success/warning olarak ayrissin
- README ve user manual guncellensin
```

### B. Mevcut Sayfa / Feature Duzeltme

#### 9. DataTable v2 migration

Kullanim: Legacy liste sayfasini yeni standarda tasimak istediginde.

```text
@[.antigravity/agents/orchestrator.md]

Countries liste sayfasini DataTable v2 standardina tasi.

Zorunlu referanslar:
- .antigravity/workflows/migrate-datatable-v2.md
- .antigravity/rules/frontend-datatable-template.md
- .antigravity/rules/frontend-js-standard.md
- .antigravity/workflows/quality-gate-datatable.md

Kabul kriterleri:
- offcanvas filter kaldirilsin
- stateSave:false olsun
- Save View personalizationClient ile calissin
- quality gate PASS olsun
```

#### 10. Save View bozulmasi duzeltme

Kullanim: Save View cikmiyor, calismiyor veya auth refresh akisi bozuksa.

```text
@[.antigravity/agents/orchestrator.md]

Products Save View akisindaki sorunu duzelt.

Beklenti:
- Save View gorunurlugu applied state'e gore hesaplansin
- 401 durumunda shared auth refresh akisi kullanilsin
- generic ErrorOccurred toast ile maskelenmesin

Degistirilmeyecekler:
- create/edit backend endpoint'leri
```

#### 11. Inline filter migration

Kullanim: Offcanvas filter'i inline/collapse modele tasirken.

```text
@[.antigravity/agents/orchestrator.md]

Customers sayfasindaki filter yapisini inline collapsible modele tasi.

Kabul kriterleri:
- #inlineFilterHost ve #inlineFilterCollapse kullan
- toolbar altina mount et
- Select2 scroll regression olusmasin
- filter class isimleri semantik olsun
```

#### 12. Toast lifecycle duzeltme

Kullanim: Create/delete/bulk toast'lari farkli gorunuyorsa.

```text
@[.antigravity/agents/orchestrator.md]

Orders liste sayfasinda toast lifecycle farkliliklarini duzelt.

Beklenti:
- single delete ve bulk delete ayni success lifecycle'ini kullansin
- row.remove().draw() ile lokal hack yapilmasin
- create success toast baseline'i korunarak parity saglansin
```

#### 13. Select2 scroll regression duzeltme

Kullanim: Inline filter select acildiginda sayfa scroll/ripple bozuluyorsa.

```text
@[.antigravity/agents/orchestrator.md]

Products inline filter icindeki Select2 scroll bug'ini duzelt.

Kabul kriterleri:
- dropdown acildiginda sayfada yatay/dikey scroll cikmasin
- reusable stil page-level degil backbone-custom.css icinde olsun
- Save View ve filter apply/reset akisi korunacak
```

#### 14. Responsive toolbar bug duzeltme

Kullanim: XS/SM breakpoint'te toolbar bozuluyorsa.

```text
@[.antigravity/agents/orchestrator.md]

Countries toolbar responsive davranisini duzelt.

Beklenti:
- XS'te export dropdown ikon butonlarla hizali olsun
- Save View gorunur/gizli iki durumda da group radius bozulmasin
- desktop davranisi korunacak
```

#### 15. Localization raw key duzeltme

Kullanim: Ekranda raw key veya hardcoded text gorunuyorsa.

```text
@[.antigravity/agents/orchestrator.md]

Products modulu localization sorunlarini duzelt.

Beklenti:
- raw key gorunmeyecek
- SharedResource ve ViewResource ayrimi korunacak
- _IndexL10n.cshtml + index.l10n.js bridge standardi uygulanacak
```

#### 16. Delete flow parity duzeltme

Kullanim: Tekil silme ile bulk silme davranisi farkliysa.

```text
@[.antigravity/agents/orchestrator.md]

Countries single delete akisini bulk delete ile ayni yasam dongusune tasi.

Beklenti:
- ortak confirm dili
- DELETE sonrasi dt.ajax.reload(..., false)
- reload sonrasi success toast
- paging korunacak
```

### C. Guvenlik / Mimari / Entegrasyon

#### 17. RBAC permission ekleme

Kullanim: Endpoint'ler yalnizca `[Authorize]` ile korunuyorsa.

```text
@[.antigravity/agents/security-agent.md]

Countries endpoint'lerine RBAC attribute ekle.

Beklenti:
- GET -> [HasPermission("Modules.Countries.Read")]
- POST -> [HasPermission("Modules.Countries.Create")]
- PUT -> [HasPermission("Modules.Countries.Update")]
- DELETE -> [HasPermission("Modules.Countries.Delete")]
- BULK DELETE -> [HasPermission("Modules.Countries.BulkDelete")]
```

#### 18. Tenant audit

Kullanim: Multi-tenant izolasyonu kontrol etmek istediginde.

```text
@[.antigravity/agents/orchestrator.md]

Countries modulunu tenant izolasyonu acisindan incele ve rapor ver.

Kontrol et:
- repository filtreleri
- API katmani
- DTO'larda TenantId sizintisi
- farkli tenant ID ile erisim denemesinde beklenen davranis

Kod yazma, sadece rapor ver.
```

#### 19. Soft delete audit

Kullanim: Fiziksel silme riski veya soft delete eksigi supheliyse.

```text
@[.antigravity/agents/orchestrator.md]

Countries modulunu soft delete uyumu acisindan incele.

Kontrol et:
- DeleteAsync IsDeleted ve DeletedAt set ediyor mu
- list sorgulari soft-deleted kayitlari disliyor mu
- bulk delete ayni mantigi kullaniyor mu

Kod yazma, sadece rapor ver.
```

#### 20. Gateway route ekleme veya duzeltme

Kullanim: Ocelot tarafinda route eksigi veya method eksigi varsa.

```text
@[.antigravity/agents/integration-agent.md]

Countries modulu icin gateway rotalarini kontrol et ve duzelt.

Beklenti:
- /api/countries
- /api/countries/{everything}
- GET, POST, PUT, PATCH, DELETE, OPTIONS tam olsun
- explicit route catch-all'dan once yer alsin
```

#### 21. API convention compliance review

Kullanim: Endpoint isimlendirme ve status code standardi denetlemek istediginde.

```text
@[.antigravity/agents/orchestrator.md]

Countries API katmanini .antigravity/rules/api-conventions.md acisindan incele.

Kontrol et:
- rota isimleri
- HTTP method secimi
- status kodlari
- ProblemDetails kullanimi

Kod yazma, sadece bulgu raporu ver.
```

#### 22. Architecture compliance review

Kullanim: Modulun katmanli mimariye uyup uymadigini denetlemek icin.

```text
@[.antigravity/agents/orchestrator.md]

Countries modulunu .antigravity/rules/erp-architecture.md ve .antigravity/ARCHITECTURE.md acisindan incele.

Kontrol et:
- Api -> Application -> Domain akisina uyum
- Domain bagimsizligi
- CQRS klasor yapisi
- repository ve persistence ayrimi
- EntityBase kullanimi

Kod yazma, sadece rapor ver.
```

### D. Test / Dogrulama / Kalite

#### 23. xUnit test yazdirma

Ne zaman dogrudan kullanilir: Sadece test uretilecekse ve urun kodu degismeyecekse.

```text
@[.antigravity/agents/testing-agent.md]

Countries modulu icin xUnit testleri yaz.

Senaryolar:
- create duplicate iso2 reddedilir
- delete soft delete yapar
- tenant izolasyonu korunur
- bulk delete sadece ayni tenant kayitlarini etkiler
```

#### 24. Browser smoke + quality gate

Kullanim: DataTable tesliminden once runtime kontrol istendiginde.

```text
@[.antigravity/agents/orchestrator.md]

Products liste sayfasi icin browser smoke ve quality gate calistir.

Beklenti:
- toolbar render
- localization key'leri cozulmus
- console error yok
- quality-gate-datatable checklist'i doldurulsun

Kod yazma, rapor ver.
```

#### 25. Release checklist

Kullanim: Canliya yakin son kontrolde.

```text
@[.antigravity/agents/orchestrator.md]

Countries modulu icin release-checklist calistir.

Kontrol et:
- build
- guvenlik
- localization
- browser smoke
- dokumantasyon

Kod yazma, sadece cikti raporu ver.
```

#### 26. Kod inceleme / audit

Kullanim: Genel saglik kontrolu ve risk odakli review istendiginde.

```text
@[.antigravity/agents/orchestrator.md]

Countries modulunu review et.

Odak:
- bug
- behavioural regression
- eksik test
- kalite kapisi uyumu

Bulgu odakli rapor ver. Kod yazma.
```

#### 27. Regression odakli review

Kullanim: Daha once bozulmus bir akis tekrar risk altindaysa.

```text
@[.antigravity/agents/orchestrator.md]

Products ve Countries DataTable akislari icin regression review yap.

Ozellikle kontrol et:
- Save View
- inline filter
- Select2 scroll
- single delete/bulk delete toast parity

Kod yazma, sadece risk raporu ver.
```

### E. Dokumantasyon / Urunlestirme

#### 28. Swagger / README guncelleme

Ne zaman dogrudan kullanilir: Kod tamam ve yalniz dokumantasyon ihtiyaci varsa.

```text
@[.antigravity/agents/documentation-writer.md]

Countries modulu tamamlandi. Swagger ve README dokumantasyonunu guncelle.

Dahil et:
- endpoint ozeti
- request/response ornekleri
- auth ve tenant header beklentisi
- bilinen sinirlar
```

#### 29. User manual uretme

Ne zaman dogrudan kullanilir: Son kullanici rehberi istendiginde.

```text
@[.antigravity/agents/user-manual-generator.md]

Countries modulu icin son kullanici kilavuzu hazirla.

Dahil et:
- ekran tanitimi
- filtreleme
- yeni kayit olusturma
- guncelleme
- tekil ve toplu silme
- sik yapilan hatalar
```

#### 30. ADR / teknik karar dokumani

Ne zaman dogrudan kullanilir: Onemli mimari karar alinmis ve kayda gecirilecekse.

```text
@[.antigravity/agents/documentation-writer.md]

Products ve Countries DataTable v2 standardi icin ADR yaz.

Karar:
- offcanvas filter yerine inline collapsible filter
- stateSave yerine manuel Save View
- personalizationClient kullanimi

Trade-off, gerekce ve geri donus trigger'larini yaz.
```

## Ek Ornekler

### 31. Modul temizleme ve yeniden kurma

Kullanim: Hatali modulu temizleyip bastan dogru kurmak istediginde.

```text
@[.antigravity/agents/orchestrator.md]

Countries modulunu tamamen temizle. Yeniden yazilacak.

Silinecekler:
BACKEND:
- services/DitenMdmService/src/.../Features/Countries/ (tum klasor)
- services/DitenMdmService/src/Diten.MdmService.Persistence/Repositories/CountryRepository.cs
- services/DitenMdmService/src/Diten.MdmService.Api/Controllers/CountriesController.cs
- services/DitenMdmService/src/Diten.MdmService.Domain/Entities/Country.cs

FRONTEND:
- frontend/Diten.Web/Controllers/CountriesController.cs
- frontend/Diten.Web/Views/MDM/Countries/ (tum klasor)
- frontend/Diten.Web/wwwroot/assets/js/MDM/Countries/ (tum klasor)
- frontend/Diten.Web/Resources/Views/MDM/Countries/ (tum klasor)

GATEWAY:
- ocelot.json icindeki /api/countries ve /api/countries/{everything} rotalari

SIDEBAR:
- _LayoutBackbone.cshtml icindeki Countries menu item'i

Dokunulmayacaklar:
- SharedResource.*.resx
- Domain/Interfaces/ICountryRepository.cs

Silme tamamlandiktan sonra kod yazma.
```

### 32. Sadece root cause debug

Ne zaman dogrudan kullanilir: Once neden analizi istendiginde.

```text
@[.antigravity/agents/debugger.md]

Countries Save View neden calismiyor, root cause analizi yap.

Katmanlar:
- browser
- dt-defaults
- personalization client
- gateway
- platform API

Kod yazma. Kanitli neden analizi ver.
```

## Kopyala-Doldur Sablonlari

### Sablon 1: Yeni modul

```text
@[.antigravity/agents/orchestrator.md]

/add-module {{ModulName}} ({{ServiceName}} servisi)

Alan tanimlari:
- {{Alan1}}: {{Tip}} (zorunlu)
- {{Alan2}}: {{Tip}} (opsiyonel)

Is kurallari:
- {{Kural1}}

UI tipi: {{DataTable/Form/Details}}
Zorunlu referanslar:
- {{RuleOrWorkflow1}}
- {{RuleOrWorkflow2}}
```

### Sablon 2: Mevcut sayfa duzeltme

```text
@[.antigravity/agents/orchestrator.md]

{{ModulName}} sayfasindaki su sorunlari duzelt:
1. {{Sorun1}}
2. {{Sorun2}}

Zorunlu referanslar:
- {{RuleOrWorkflow1}}
- {{RuleOrWorkflow2}}

Degistirilmeyecekler:
- {{Sinir1}}

Kabul kriterleri:
- {{Beklenti1}}
- {{Beklenti2}}
```

### Sablon 3: Audit

```text
@[.antigravity/agents/orchestrator.md]

{{ModulName}} modulunu su acilardan incele:
- {{Kontrol1}}
- {{Kontrol2}}
- {{Kontrol3}}

Kod yazma, sadece rapor ver.
```

### Sablon 4: Dogrudan agent

```text
@[.antigravity/agents/{{AgentName}}.md]

{{Dar ve net gorev}}

Beklenti:
- {{Maddeler}}
```

## Son Notlar

- Varsayilan secim her zaman `@orchestrator` olsun.
- Dogrudan agent kullanacaksan gorevin dar ve tek eksenli oldugundan emin ol.
- Prompt ne kadar netse, yanlis pattern uretme riski o kadar dusuk olur.
- DataTable islerinde `offcanvas`, `stateSave`, `toast lifecycle`, `Save View`, `quality gate` maddelerini acik yazmak iyi pratiktir.

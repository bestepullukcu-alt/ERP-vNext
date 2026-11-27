# WP-0182-DL4 — MOD-0182 Barkod / RFID DL-4: etiket şablonu + basım çekirdeği (önizleme, PDF, yeniden basım) + lokasyon etiketi + Web ekranları

Tarih: 2026-11-27 · Veren: CT (MVP-5 WMS CT) · Künye: **draft** · Sahip: **Beste** · Yürüten: geliştirici oturumu (MVP-5)
Durum: **taslak — başlama kapıları: paket onayı kapalı; iki XMC teyidi açık (§0 madde 3 G-5, G-6); CT kararları F-1…F-8 (§2)**

- Modül: **MOD-0182 — Barcode/RFID** (+ MOD-0365), servis `Diten.InventoryService` (5067) + `Diten.Web` (5001, yalnız Gateway 5000 üzerinden), kiracı
  modülü (7 dil).
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0182-barcode-rfid.md` **r9**, LF sha256
  `6838d3973246347770f424dd7fc3d75b476dc19f0ea5b00a43cc9403eef0d05d` (CT ölçümü 2026-11-27: hat @ `8c32f4d90` ve `origin/main` @ `85e3b1eec` aynı),
  `status: ready-for-dev`. **r9 onay satırı `main`'de:** `origin/main:execution/approvals/approvals.tsv:110` (onaylayan Ali / `GMG-ERP`, 2026-11-14;
  commit `b647aac3d`, PR #462 birleşmesi `85e3b1eec`). r7 onayı (`:88`) r8 / r9'u kapsamaz (paket §24 DL-2 satırı) — DL-4 r9 metniyle yazılır.
- **Sıradaki dilim tespiti:** paket §23 "Teslim dilimleri" (dilim tablosu §23'tedir; §22 "Record Codes"dur) ve §24 "Sıra: DL-1 → **DL-2 ve DL-4 (paralel)** →
  DL-5 (FU07 kapısı) → DL-3 → DL-6 → DL-7; DL-8 DL-1'den sonra her an". DL-1 VERIFIED 2026-11-14 (FIX-01 `b19765ef2`), DL-2 VERIFIED 2026-11-17
  (`a38e97c28`, `f91aaf6aa`) — `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md`. Sıradaki: **DL-4**. (DL-5'in kapısı: MOD-0290-FU07 DL-1
  `main`'de **ve** pilotta ölçülmüş olmalı — CT bu WP için ölçmedi; DL-3 öneri olarak DL-5'ten sonra. DL-8 — yalnız Gateway kapsama testi, integration-agent — her an koşabilir; bu WP'nin
  kapsamı değildir.)
- Dilim: **DL-4** (paket §23), kanıt seviyesi **E4**.
- Sahip kararı (devam): `docs/records/decisions/2026-11/ct-owner-decisions-2026-11-27.md` §3 — "MOD-0182 sıradaki dilim". Kod / commit / dağıtım izni değildir.
- **Canlı tarayıcı turu bu WP'de yapılmaz**; tarayıcı kanıtları (375 px, `ar` RTL, UAS-001 ekranı, E3 smoke "şablon → önizleme → basım → PDF", tarayıcının
  yazdırma penceresi) raporda "canlı turda" diye ayrı satırda kalır.
- Yetki: paket > `domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse **paket geçerlidir**; çelişkiyi CT'ye yaz, kendin çözme.
  Bu WP push, PR ya da `.antigravity/**` değişikliği izni değildir.

`INV` = `services/Diten.InventoryService/src/Diten.InventoryService.`, `INVT` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`.

## 0. Başlamadan

1. **Dal.** Tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` (ana checkout `C:\Users\Ali\erp-vnext`); **yeni dal açma**.
   `git merge-base --is-ancestor 8c32f4d90 HEAD` → çıkış 0; değilse `git merge --ff-only 8c32f4d90`, ff olmazsa **dur**. CT etiket verirse ona ileri sar.
   Doğrula (çıkış 0): `… a38e97c28 HEAD`, `… f91aaf6aa HEAD` (DL-2 hatta); `git merge-base --is-ancestor b647aac3d origin/main` (r9 onay satırı `main`'de;
   önce `git fetch origin`).
2. **Paket sha'sı:** `tr -d '\r' < <paket> | sha256sum` → `6838d397…05d`; raporun ilk satırına yaz. Farklıysa **dur**.
3. **Başlama kapıları:**

   | # | Kapı | Durum (CT, 2026-11-27) |
   |---|---|---|
   | G-1 | Paket onayı r9 (CT-APR-01) `main`'de | **kapalı** — `b647aac3d` / `85e3b1eec` |
   | G-2 | Paket §24 DL-4 kararları: Q-5 (yazıcı = A), Q-17 (boyutlar), Q-6 (ZXing.Net) | **kapalı** — karar 03 + eki `bea6662d9`; ZXing.Net 0.16.11 DL-1'de eklendi (`f975fd670`, `INV/Api/Diten.InventoryService.Api.csproj:20`) |
   | G-3 | X-4 `LBL`, X-6 (a) +5 (XMC-001) | **kapalı** — `mod0182-mod0365-owner-decisions-03.md:65-70` ("X-4 … `DVC` (DL-1) ve `LBL` (DL-4)"; X-6 (a) dilim başı + komutlar) |
   | G-4 | X-12 Gateway +11 | **kapalı** — `ad1100158` ("uç ekleyen her MOD-0182 dilimi (DL-1, DL-4, DL-6, DL-7) … her artış dahil") |
   | G-5 | X-10 `Perm.Entity.*` satır adları (DL-4 varlıkları) | **teyit gerekli** — onay `mvp5-wms-ct-owner-decisions-2026-11-13.md:87-88` "MOD-0182 DL-2 XMC-001" başlığı altında ve "yalnız MOD-0182 varlık satır adları" der; paket X-10 satırı DL-2, DL-4, DL-7 sayar. CT okuması: DL-4'ü kapsar — **Beste teyidi** kayda geçmeden X-10 commit'i atılmaz |
   | G-6 | X-5 `SingleSystemOfRecordArchitectureTests` 0182 vakası — liste **adıyla sabitli** (7 ad); DL-4 üç koleksiyon ekler | **teyit gerekli** — onay (`…decisions-03.md:67-68`) "X-5 … `BarcodeCollections.All`" der, dilim sınırı yok; paket X-5 satırı "DL-1" der, §17 r4 ise "Yeni koleksiyonlar `BarcodeCollections.All`'da (AC-26, X-5)". CT okuması: kapsar — **Beste teyidi** |
   | G-7 | X-6 (b) `seen` | **değişmez** — yazımlar `BarcodeTransactions.InTransactionAsync` ile; yeni `WithTransactionAsync(` çağrı yeri açılırsa onay `4168e7a90` kuralı (dilim başı + yeni çağrı yeri, aynı commit) |
   | G-8 | X-7 `Program.cs` | **değişmez** — DL-4 kayıtları `BarcodeServiceCollectionExtensions` (0182'nin kendi dosyası) |
   | G-9 | Q-14 rol şablonu (Ali) | DL-4 kapısı **değil** (paket §24 DL-2 kabul kapısı); "Etiket sorumlusu" rol önerisi §14'te, X-11 / Q-14 WP'sinde |

4. **Dilim başı commit'i (`<dilim-başı>`)** kaydet; bütün "önce" ölçümleri bu commit'te, aynı gün.
5. **Dilim başında ölçülecek sayaçlar** (CT `8c32f4d90` ölçümü; satırın kendisine atıf yap):

   | Sayaç | Dosya / satır | Bugün | DL-4 değişikliği | Beklenen sonra (çıkarım) |
   |---|---|---|---|---|
   | Gateway uç sayısı (X-12) | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` `Assert.Equal(128, endpoints.Count)` (`:131`) | **128** | **+11** | 139 (başka dilim önce eklerse o anki + 11) |
   | Merkezi denetim yazma komutu (X-6 (a)) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` `Assert.Equal(151, WriteCommands.Length)` (`:73`) | **151** | **+5** | 156 |
   | Merkezi denetim `seen` (X-6 (b)) | aynı dosya `Assert.Equal(17, seen)` (`:156`) | **17** | **0** | 17 |
   | Kayıt kodu önekleri (X-4) | `INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs` `TypePrefixes` (`:34`) | **7** (… WAV, DVC) | + `LBL` | 8 (MOD-0179 DL-2 `TPX`'i önce gelirse 9) |
   | 0182 koleksiyon bildirimi (X-5) | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` 0182 vakası | 7 ad | + `wms_barcode_label_files`, `wms_barcode_label_prints`, `wms_barcode_label_templates` | 10 |
   | `BarcodePermissions.Implemented` | `INV/Infrastructure/Authorization/BarcodePermissions.cs` (`Implemented => Dl1`) | 10 anahtar | + 7 DL-4 anahtarı | 17 |
   | Manifest sayfaları | `INV/Api/Barcode/BarcodeRfidManifestProvider.cs` | SCANEVENTS, SCANDEVICES, BARCODESETTINGS, SUPERVISORPIN | + `LABELTEMPLATES`, `LABELPRINTS` (nav) + düğme eylemleri | 6 sayfa |
   | Web nav tabanı | `NavManifestL10nGuardTests.cs` `NavVisiblePageKeyFloor = 45` | 45 | değiştirme (alt sınır); kırmızıysa **dur** | — |

6. **Eşzamanlılık ve ortam:**
   - Aynı sayaç satırlarını **MOD-0179 DL-2** (Gateway +11, komut +7, `TPX`) ve **MOD-0180 DL-5** (Gateway +2) de değiştirir; ortak Web dosyalarını
     (`SharedResource.*.resx`, `PageHelpResource.*.resx`, `PageHelpCatalog.cs`) MOD-0180 DL-5 de değiştirir. Her sayaç / ortak dosya commit'inden **hemen önce**
     hattı al, o anki değerden yeniden hesapla, yorumda "measured <n> at <commit>"; çakışmada iki tarafın eklemesini koru, CT'ye yaz.
   - Ana checkout'ta derleme / test / sabotaj **yok**. Ayrık worktree: `git worktree add --detach ..\erp-vnext-wt-0182-dl4 HEAD`.
   - **Makinede aynı anda tek InventoryService takımı.** Mongo zaman aşımıyla düşen koşu sayılmaz. `BarcodeScanTests.Two_hundred_consecutive_scans_stay_under_the_p95_bound`
     yük altında kırmızıysa tek başına iki kez koşup yeşilini raporla (bilinen risk, DL-1 kaydı). `dotnet test` çıktısını boruya verme.
   - vitest yalnız ana checkout `node_modules` junction'ıyla (`node_modules\.bin\vitest.cmd run`; `npx` yok).
7. **Paketi oku (özet yetmez):** §3 (DL-4 nesneleri, 5 komut, sorgular, 11 uç, controller tabanı, frontend rotaları), §4.6, §4.10 (DL-4 index'leri), §4.11,
   §5, §5.1 X-4, X-5, X-6, X-10, X-12, §6, §7 (PDF üretimi, barkod çizim kütüphanesi satırları), §8.9 (bütünüyle — `Location` satırı DL-4; `LotCase` / `Sscc`
   DL-5), §8.12, §8.13, §8.14 (etiket satırları), §8.15, §11 (`LabelTemplates/`, `LabelPrints/`), §12 (şablon / basım satırları), §13, §14 (DL-4 anahtarları,
   manifest), §16 DL-4 bloğu (AC-47…AC-52) + AC-31…AC-34, AC-39, AC-40 + AC-9 (r4 komut idempotency — "DL-4 … şablon, basım") + AC-23 (+5) + AC-65 (0178 sayı
   vakası), §17, §21 (DL-4 satırları), §22 (`LBL`), §24 DL-4 satırı, §25 (`LABELTEMPLATES`, `LABELPRINTS`). Taslak
   `docs/analysis/contracts/drafts/scan-bundle-v1-proposal.md` §1 kurallar, §4.2 DL-4 satırları (`:407-417`), hata kodları (`:474-475`).

## 1. Kapsam

### 1.1 Paket DL-4 satırı (§23) — birebir
"etiket şablonu + basım çekirdeği (önizleme, PDF, yeniden basım) + lokasyon etiketi + Web ekranları". Kapsadığı bölümler: **Etiket şablonu ve basımı,
LabelTemplate ve LabelPrintJob, Frontend File Contract, Ekranlar ve Blueprint soft pages, Audited Events, Record Codes.** Bu listenin dışına çıkma.

**Arka uç:**
- **İlk adım — SCAN-BUNDLE taslağının DL-4 satırlarını bağlayıcı yap (taslak §1: "her dilim kendi satırlarını ilk adımında bağlayıcı yapar — yalnız eklemeli"):**
  §4.2'deki 11 DL-4 işlemi şemalarıyla (`LabelTemplate`, `LabelPrintJob`, önizleme cevabı, istek gövdeleri, PDF cevabı), hata kodları
  (`LABEL_TEMPLATE_NAME_TAKEN`, `LABEL_SOURCE_INVALID`, `LABEL_REPRINT_REASON_REQUIRED` + F-3'ün kodu), `If-Match` / `Idempotency-Key` kuralları.
  Sürüm `1.0.0-draft`; **`FROZEN` koyma, `scan-bundle.openapi.yaml` yazma** (dondurma ayrı CT adımı). Ayrı commit, controller kodundan **önce**; DL-1 / DL-2
  satırları değişmez. Sözleşme testi (`INVT/Barcode/BarcodeDraftContract.cs` deseni) DL-4 cevaplarını kapalı şemayla doğrular.
- **Komutlar (5, §3, §21):** `CreateLabelTemplateCommand`, `UpdateLabelTemplateCommand`, `ActivateLabelTemplateCommand`, `DeactivateLabelTemplateCommand`,
  `CreateLabelPrintJobCommand` (yeniden basım dahil; SSCC ve lot / koli türleri DL-5).
- **Sorgular (6):** `GetLabelTemplateListQuery`, `GetLabelTemplateByIdQuery`, `PreviewLabelQuery`, `GetLabelPrintJobListQuery`, `GetLabelPrintJobByIdQuery`,
  `GetLabelPrintJobFileQuery`.
- **Uçlar (11, taban `/api/warehouse/barcode`):** `GET` / `POST label-templates` · `GET label-templates/{templateId}` · `POST label-templates/{templateId}/update`
  · `…/activate` · `…/deactivate` · `POST label-previews` (salt okuma; gövde GS karakteri taşıyabildiği için POST) · `GET` / `POST label-prints` ·
  `GET label-prints/{jobId}` · `GET label-prints/{jobId}/file` (`application/pdf`). Controller'lar `LabelTemplatesController`, `LabelPrintsController`
  (`INV/Api/Controllers/Barcode/`, `[Route("api/warehouse")]`, namespace `…Controllers.Barcode`; eylem öznitelikleri `[HttpX("…")]` biçiminde — ek argüman
  yok, aksi halde 0178 testinin eylem regex'i ucu saymaz). `{…Id}` `:guid`; **PUT / DELETE yok** (silme yok — pasifleştirilir).
- **İzinler (§14 — 7 anahtar, yeni fiil yok):** `warehouse.barcode.label-templates.read` / `.create` / `.update` / `.activate` / `.deactivate`
  (Tier 1 / 1 / 1 / 2 / 2), `warehouse.barcode.label-prints.read` / `.create` (önizleme `create` ister). `BarcodePermissions.Implemented` += `Dl4`.
  Manifest `BARCODE-RFID`: sayfalar `LABELTEMPLATES`, `LABELPRINTS` (nav) + her gerçek düğme için eylem; `ApiPermissions` değişmez
  (`scans.create`); manifest yalnız `warehouse.barcode.*`.
- **Varlıklar ve koleksiyonlar (§3, §4.6, §4.10):** `LabelTemplate` (`wms_barcode_label_templates`; `Name` 2..80 şirkette tekil, `LabelType`
  `Location` \| `LotCase` \| `Sscc`, `Size` `100x150` / `100x50` / `50x25` mm, `HumanReadableFields[]` türün izinli kümesinden 1..8, `IsDefault` tür başına tek
  etkin varsayılan, `Status`, `Note?`, `Version`); `LabelPrintJob` (`wms_barcode_label_prints` + PDF `wms_barcode_label_files`; insert-only; `Number`
  `{ŞİRKET}-LBL-{000001}`, `TemplateId` + `TemplateVersion` anlık, `Sources[]`, `Copies` 1..20, `LabelCount` ≤ 500, `Language`, `IsReprint`,
  `ReprintOfJobId?`, `ReprintReasonCode?`, `ReprintNote?`, `FileId`, `FileSha256`, `CreatedBy`, `CreatedByName`). Hepsi `BarcodeCollections.All`'a.
- **Kurallar (§8.9):** `Location` (raf) etiketi — Code 128, içerik **bin kodu** (0178'in tam kod eşleşmesiyle aynı dizge); insan okunur alanlar: bin
  kodu, depo / bölge yolu, rol; kaynak LOCATION bin'leri tek tek ya da bölge altındaki hepsi, ≤ 500 (yalnız `Find`). **Önizleme:** ilk etiketin PNG
  görüntüsü + alan listesi, **hiçbir şey yazmaz** (iş, sayaç, merkezi kayıt, dosya yok). **Basım:** iş + PDF (etiket boyutunda sayfa) + dosya ve sha256
  saklanır, `Number` (`LBL`) döner. **Sunucu yazıcıya bağlanmaz** (Q-5 = A): `Socket` / `TcpClient` / yazıcıya `HttpClient` yok; Web PDF'i tarayıcının
  yazdırma penceresinde açar. **Yeniden basım:** `isReprint = true` + `reprintOfJobId` (aynı şirket, aynı tür) + gerekçe `Damaged` \| `Lost` \|
  `PrinterError` \| `Relabel` \| `Other` (`Other`'da açıklama 10..500); gerekçesiz 422 `LABEL_REPRINT_REASON_REQUIRED`, `Other` açıklamasız 400.
  Etiket dili işin `Language`'ı (varsayılan isteğin dili; 7 dil). İdempotency + `If-Match` (`LabelTemplate` değişebilir kayıt) taslak §1 kural 6–7.

**Ekranlar (Frontend File Contract §11 — Golden Slim, `data_mode: server`):**
- `frontend/Diten.Web/Views/Warehouse/LabelTemplates/`: `Index.cshtml` (**`Layout = "_LayoutTenantShell";` açık**), `_Filter.cshtml`, `_DataTable.cshtml`
  (`data-dt-standard="v2"`, `data-dt-data-mode="server"`, skeleton), `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (6 alan + canlı önizleme),
  `_DetailsQuickView.cshtml`, `LabelTemplatesIndex.cs`.
- `frontend/Diten.Web/Views/Warehouse/LabelPrints/`: `Index.cshtml` (Layout açık), `_Filter.cshtml`, `_DataTable.cshtml` ("yeniden basım" rozeti),
  `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (yalnız "Yeni basım": 8 alan + önizleme; **"Bas" önizleme yüklenmeden etkin değil** — E-1, AC-52),
  `_DetailsQuickView.cshtml` (iş ayrıntısı, PDF'i aç, "Yeniden bas"), `LabelPrintsIndex.cs`.
- `wwwroot/assets/js/Warehouse/{LabelTemplates,LabelPrints}/index.js`, `index.l10n.js`; `LabelPrints/preview.js`.
- `Resources/Views/Warehouse/{LabelTemplates,LabelPrints}/{Module}Index.{en,tr,fr,es,zh,ar,ru}.resx`.
- `frontend/Diten.Web/Controllers/LabelTemplatesController.cs`, `LabelPrintsController.cs` — same-origin MVC proxy → Gateway `/api/warehouse/barcode…`;
  her yazma `[ValidateAntiForgeryToken]`; sayfanın `Accept-Language`'ı, `If-Match`, `Idempotency-Key` iletilir; PDF ve PNG tamponlamadan geçirilir; sayfa
  eylemi izin denetler (UAS-001).
- Ortak, **yalnız eklemeli:** `SharedResource.{7}.resx` `Nav.Page.LABELTEMPLATES`, `Nav.Page.LABELPRINTS` + X-10 `Perm.Entity.*` satır adları (G-5; beklenen
  `LABELTEMPLATES`, `LABELPRINTS` — kesin ad vitest'ten); `PageHelpResource.{7}.resx` `Help.LABELTEMPLATES.*` (Purpose, Audience, Step1..3: etiket türünü ve
  boyutu seç, görünecek alanları seç, önizlemeyi kontrol et), `Help.LABELPRINTS.*` (Purpose, Audience, Step1..4: türü ve şablonu seç, kaynağı seç, önizlemeyi
  kontrol et, PDF'i yazdır; `.Immediate` "Yeniden basım gerekçe ister; SSCC yeniden basımda değişmez"); `PageHelpCatalog.cs` adım ikonları (+ `Related`).
  **Tur yok.**
- Web testleri: `frontend/Diten.Web.Tests/Warehouse/BarcodeL10nAndHelpTests.cs` (AC-31 0182 sayfalarının tamamını tarar — yeni iki sayfa dahil; AC-33, AC-39),
  `BarcodeProxyControllerTests.cs` (PDF / PNG proxy, `Accept-Language`, anti-forgery); vitest `tests/barcode-screens.test.js` (AC-52 "Bas" düğmesi; BL-586
  `data-dt-primary` offcanvas işareti), `tests/role-assignments-label-ledger.test.js` (AC-40).
- **Dokunma protokolü (BL-440):** iki yeni liste ekranı için `python3 .antigravity/scripts/verify_datatable_page.py . --area Warehouse --module LabelTemplates --format gaps`
  ve `--module LabelPrints` (gerekiyorsa `--api-profile proxy`); paket silme / toplu silme / dışa aktarma tanımlamaz — aracın bunlarla ilgili sapmaları
  (`/bulk`, sunucu dışa aktarımı) **numaralı listeyle sahibine sorulur** (DL-2 ScanDevices emsali); sessizce düzeltme ya da atlama yok (AC-32).

### 1.2 Dokunulacak dosyalar

**0182'nin kendi dosyaları (paket §5 DL-4 satırları):** `docs/analysis/contracts/drafts/scan-bundle-v1-proposal.md` (yalnız DL-4 satırları) ·
`INV/Domain/Barcode/{Entities,Enums,Rules,Repositories}/` (şablon / basım varlıkları, tür → izinli alan kümesi, varsayılan devri kuralı) ·
`INV/Application/Features/Barcode/` (`BarcodeContract.cs`, `BarcodeErrorCodes.cs`, `BarcodeCentralAudit.cs` eklemeli; `Ports/` — etiket çizici / PDF üretici
portu (F-1); `Resources/BarcodeText.{7}.resx` etiket üzerindeki sabit metinler, örn. rol adı) · `INV/Application/Features/LabelTemplates/`, `LabelPrints/`
(Golden Slim) · `INV/Persistence/Barcode/` (depolar, `BarcodeCollections` +3, index'ler, LOCATION / rol okuması yalnız `Find`) · `INV/Api/Controllers/Barcode/`
(iki controller) · `INV/Api/Barcode/` (`BarcodeRfidManifestProvider` eklemeli, `BarcodeServiceCollectionExtensions` eklemeli, etiket çizici + PDF üretici
uygulaması — F-1) · `INV/Infrastructure/Authorization/BarcodePermissions.cs` · `INVT/Barcode/` · Web: §1.1 "Ekranlar" dosyaları.

### 1.3 XMC-001 dokunuşları (yalnız eklemeli; satırlar `<dilim-başı>`'nda yeniden ölçülür)

| # | Dosya | DL-4 değişikliği | Sahip | Onay | Hangi commit'te |
|---|---|---|---|---|---|
| X-4 | `INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs` | `LabelPrintJob = "LBL"` sabiti + `TypePrefixes`'e (`DVC` emsali); var olan satır değişmez | MOD-0178 (merkez) | **onaylı** (decisions 03 `:65-70`) | `LBL` numarası üreten commit |
| X-5 | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` 0182 vakası | listeye üç ad (alfabetik); `declared` zinciri ve önek süzgeci **değişmez** | MOD-0173 (merkez) | **teyit gerekli** (G-6) | koleksiyon sabitleri commit'i |
| X-6 (a) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` `WriteCommands.Length` + yorum | dilim başı **+5**, yorumda 5 komut adıyla ve "measured <n> at <commit>" | MOD-0173 (merkez) | **onaylı** | komutlarla aynı commit |
| X-6 (b) | aynı dosya `seen` | **değişmez** (yeni çağrı yeri açılırsa `4168e7a90` kuralı) | MOD-0173 | onaylı (kural) | çağrı yeriyle aynı commit |
| X-10 | `frontend/Diten.Web/Resources/SharedResource.{7}.resx` `Perm.Entity.*` | DL-4 varlıklarının **yalnız satır adları**; yeni fiil yok (`activate`, `deactivate` köprüde var); `_IndexL10n.cshtml` / `RoleAssignmentsIndex.*.resx` / `perm-label.js` değişmez | Web ortak (merkez) | **teyit gerekli** (G-5) | manifest anahtarlarını bildiren commit'in ardından, AC-40 vitest yeşil |
| X-12 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — **yalnız** `Assert.Equal(<n>, endpoints.Count)` + üstündeki yorum | dilim başı **+11**, yorumda 11 uç adıyla; **integration-agent** yazar | integration-agent | **onaylı** (`ad1100158`) | iki controller'ın uçlarıyla **tek commit** |

**Bu dilimde değil:** X-1 (DL-5), X-2 / X-3 / X-14 (DL-3), X-7 (`Program.cs` — değişmez), X-11 / X-13, X-15 (DL-1'de bitti).

**Değişmez (bağlayıcı):** `INVT/Warehouse/**`, `INVT/CycleCount/**` hiçbir satırı (AC-41 kanıtı her dilimde); `AuthorizationGuardTests`,
`LedgerImmutabilityArchitectureTests`, `WarehouseRegistrationTests`, `InventoryLedgerManifestCompletenessTests`; `SingleSystemOfRecordArchitectureTests`
`No_Model_B_Product_type_is_used` ve `TraceArchitectureTests` istisna listeleri (yeni 0182 dosyası bu istisnalara **eklenmez** — paket §17 r8; gerekirse dur,
XMC-001); Platform `RecordCodeTypeCatalogTests` (`LBL` `.antigravity` kapısında, `3a50c44c6`); MOD-0178 üretim kodu (LOCATION / rol yalnız okunur);
`NavManifestL10nGuardTests.cs`.

### 1.4 Kapsam dışı
DL-5: `LotCase` ve `Sscc` etiket **basımı** (GS1 DataMatrix / GS1-128, SSCC ataması, `SsccAssignment` / `SsccCounter`, ilçe okuması), GTIN çözümleme,
"Bu nedir?". DL-3: `scanRef` kapısı. DL-6: RFID. DL-7: uyum izi + kanıt paketi. DL-8: `Mod0182BarcodeRouteTests`. Mobil Android yazdırma (mobil ekip,
§8.16). AuthService rol şablonu (Q-14, Ali). `ocelot.json`.

## 2. Kararlar ve CT önerileri

**Kapalı (uygulanır):** karar 01…03 ve ekleri (Q-5 = A yazıcıya bağlanmama, Q-6 = ZXing.Net, Q-17 boyutlar, Q-11 r3 / r4 ilçe — DL-5); r8 / r9 kararları
(`mvp5-wms-ct-owner-decisions-2026-11-13.md` "MOD-0182 r8 incelemesi kararları").

**Açık — CT kararı (CT önerisi parantezde; geliştirici kendisi seçmez):**
- **F-1 — çizici / PDF üreticinin yeri:** paket §5 bunları `INV/Application/Features/Barcode/Services/`'e koyar; ama PDFsharp-MigraDoc 6.1.1 ve ZXing.Net
  0.16.11 yalnız `Diten.InventoryService.Api.csproj`'dadır ve servisteki bütün PDF / barkod çizicileri Api katmanındadır (`INV/Api/Barcode/PairingQrRenderer.cs`,
  `INV/Api/CycleCount/CycleCountEvidencePdfRenderer.cs`). (CT önerisi: Application'da port (`ILabelRenderer` benzeri), uygulama `INV/Api/Barcode/`'da — emsal;
  Application csproj'una paket **eklenmez**. Paket metninden bu dosya yeri sapması kabul kaydına yazılır.)
- **F-2 — AC-49 "PDF'teki barkod … çözülünce bin kodunu verir":** (CT önerisi: test üretilen PDF'in içinden gömülü barkod görüntüsünü çıkarır ve ZXing
  okuyucusuyla çözer — ölçülen şey PDF'in kendisidir, çiziciye verilen dizge değil. Çiziciye ayrı yoldan sorulan görüntüyle ölçmek guard değildir (Çekirdek 10).
  Vektör çizim seçilirse gömülü görüntü olmaz — o zaman CT'ye yaz, ölçüm yöntemi birlikte belirlenir.)
- **F-3 — `LotCase` / `Sscc` DL-4'te:** (CT önerisi: şablon üç türde de yaratılabilir — §4.6 enum ve §8.9 izinli alan kümeleri tanımlı; DL-4'te **basım ve
  önizleme yalnız `Location`**; diğer iki türde 422 `LABEL_SOURCE_INVALID` + ayrıntı "tür DL-5'te" (taslakta yazılır); DL-5 açar.)
- **F-4 — PDF saklama:** ≤ 500 etiket × kopya ≤ 20 tek dosyada Mongo belge sınırını (16 MB) aşabilir. (CT önerisi: `LabelCount` ≤ 500 sınırı kopyalar dahil
  uygulanır — §4.6 "LabelCount (≤ 500)"; dosya `wms_barcode_label_files`'ta MOD-0181 `wms_count_evidence_files` deseniyle; boyut ölçümü testte — en büyük
  vaka (500 etiket `100x150`) raporda bayt olarak.)
- **F-5 — "rol" insan okunur alanı:** 0178 lokasyon rolü (`wms_location_roles`) yalnız `Find` + `WarehouseCollections.*` sabitiyle; rol adı etiket dilinde
  (`BarcodeText.{7}.resx`). (Uygulanır; 0178 dosyasına dokunma.)
- **F-6 — yeniden basımın yeniden basımı:** (CT önerisi: izinli; `reprintOfJobId` herhangi bir önceki işi gösterebilir, aynı şirket + aynı tür; zincir
  kısıtı yok.)
- **F-7 — önizleme cevabı:** PNG gövdede base64 mı, ayrı içerik türü mü — taslakta kesinleşir. (CT önerisi: JSON içinde `imagePng` base64 + `fields[]`;
  ≤ 1 etiket.)
- **F-8 — tür başına varsayılan devri eşzamanlılığı:** iki eşzamanlı "varsayılan yap" isteğinden sonra tam bir varsayılan kalır (AC-47) — (CT önerisi: kısmi
  tekil index (`TenantId`, `LegalEntityId`, `LabelType`, `IsDefault = true`, `Status = Active`) + aynı işlemde önceki varsayılanı düşürme; görevli kancayla test.)

## 3. Kurallar

- **Kiracı (Çekirdek 2):** her okuma / yazma `TenantId` + `LegalEntityId` (sunucuda); başka kiracı / şirket 404 (şablon, iş, dosya, kaynak bin);
  gövde / sorguda kapsam 400. Silme yok (şablon pasifleştirilir; iş insert-only).
- **Yetki (Çekirdek 3, UAS-001):** her eylemde `[HasPermission(BarcodePermissions.X)]`; önizleme `label-prints.create`; PDF indirme `label-prints.read`;
  izinsiz kullanıcıya iskelet çizilmez, yönlendirme yapılmaz; düğmeler izne göre.
- **Denetim (§21):** 5 komut `ICentralAuditDeclaration`, yol b, `SourceModule = BARCODE-RFID`; başarılı ve yazan 1 kayıt, replay 0, başarısız 0. **Yazma
  olmayan:** önizleme, dosya indirme (okuma — kayıt yok).
- **Dil (Çekirdek 4):** 7 dil (en, tr, fr, es, zh, ar, ru) — sayfa resx'leri, nav, yardım, etiket üzerindeki sabit metinler; `ar` RTL. Bir dil eksikse iş
  bitmemiştir.
- **Korunan yollar (Çekirdek 6):** `.antigravity/**` yazılmaz; `ocelot.json` değişmez; X-12 satırını integration-agent yazar; `_Layout.cshtml`, Archive,
  `services/Diten.Building.Blocks/**` donuk; csproj'a yeni paket **eklenmez** (ZXing.Net ve PDFsharp-MigraDoc var) — gerekirse dur, CT'ye yaz.
- **XMC-001:** §1.3 dışında başka modül dosyasına dokunma; gerekirse **dur**, CT'ye sor.
- **Git (GIT-002):** `main`'e commit / push yok; push / PR yok (modül sonunda, ayrı onayla, öncesinde `origin/main` merge commit'iyle); stage açık yolla;
  hook / imza atlanmaz; yıkıcı komut yok; `git stash` yalnız etiketli + SHA. İletiler `feat(sce): MOD-0182 DL-4 — …`, `feat(web): MOD-0182 DL-4 — …`.
- **Commit sırası (öneri):** (1) taslak DL-4 satırları → (2) Domain (saf kurallar + birim testleri) → (3) Persistence + index'ler + koleksiyon sabitleri +
  X-5 (**aynı commit**, G-6 sonrası) → (4) komutlar / sorgular + çizici portu + X-6 (a) (**aynı commit**) + X-4 (`LBL`) → (5) izinler + manifest →
  (6) X-10 satır adları (G-5 sonrası) → (7) controller uçları + X-12 (**tek commit**, integration-agent adımı) → (8) Web ekranları, proxy, resx, nav, yardım,
  `PageHelpCatalog.cs` → (9) Web testleri / vitest. (3), (4), (7)'de InventoryService filtresiz; (6), (8), (9)'da Web.Tests + vitest.

## 4. Kabul ölçütleri, test ve sabotaj

| AC | Kısa başlık | Sabotaj (paket §24 DL-4) |
|---|---|---|
| AC-47 | Şablon yarat / düzenle / pasifleştir / etkinleştir; ad tekil (409 `LABEL_TEMPLATE_NAME_TAKEN`); tür başına tek etkin varsayılan (ikincisi öncekini aynı işlemde düşürür); silme yok; `HumanReadableFields` türün izinli kümesinden | **S-47:** varsayılan tekilliğini kaldır → kırmızı |
| AC-48 | Önizleme PNG + alan listesi; **hiçbir şey yazmaz** (iş, sayaç, merkezi kayıt, dosya sayısı değişmez) | **S-48:** önizlemede iş yaz → kırmızı |
| AC-49 | Lokasyon etiketi basımı: `LBL` numaralı iş, PDF (etiket boyutunda sayfalar), sha256 saklı; PDF'teki barkod okuyucuyla çözülünce bin kodunu verir (F-2) | **S-49:** barkod içeriğine bin adını yaz → çözüm testi kırmızı |
| AC-50 | Yeniden basım: gerekçesiz 422 `LABEL_REPRINT_REASON_REQUIRED`; `Other` açıklamasız 400; `reprintOfJobId` aynı şirket ve aynı tür; listede "yeniden basım" rozeti | **S-50:** gerekçe denetimini kaldır → kırmızı |
| AC-51 | Sunucu yazıcıya bağlanmaz: 0182 üretim kodunda `Socket`, `TcpClient`, yazıcıya `HttpClient` yok (mimari tarama) | **S-51:** etiket servisine `TcpClient` ekle → kırmızı |
| AC-52 | Web "Yeni basım"da "Bas" önizleme yüklenmeden etkin değil; önizleme hatasında basılamaz | **S-52:** düğmeyi her zaman etkin yap → vitest kırmızı |
| AC-31 | Yeni iki sayfanın sayfa görünümlerinde `Layout = "_LayoutTenantShell";` (test 0182 sayfalarının tamamını tarar) | (DL-4 sayfası için) `LabelTemplates/Index.cshtml`'den satırı sil → `BarcodeL10nAndHelpTests` kırmızı |
| AC-32 | `verify_datatable_page.py` `LabelTemplates`, `LabelPrints` çıktısı kabul kaydında; sapmalar sahibine sorulmuş (araç kontrolü — bekçi değil) | — |
| AC-33 | Sayfa resx'leri 7 dilde aynı anahtar kümesi; `Nav.Page.*` biçimi (`check_pack_nav_keys.py`); `ar` RTL | bir resx'ten anahtar sil → kırmızı |
| AC-34 | UAS-001: izinsiz kullanıcıya iskelet yok, yönlendirme yok, tek açıklama; düğmeler izne göre (ör. `label-templates.deactivate` yoksa "Pasifleştir" yok) | izin denetimini görünümden kaldır → kırmızı |
| AC-39 | `Help.LABELTEMPLATES.*`, `Help.LABELPRINTS.*` 7 dilde; tur yok | bir sayfanın `Help.*.Purpose`'unu sil → kırmızı |
| AC-40 | `role-assignments-label-ledger.test.js` yeşil; DL-4 satır adları 7 dilde (X-10) | bir DL-4 `Perm.Entity.*` satırını sil → vitest kırmızı |
| AC-65 (0178 sayı vakası) | Gateway `Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route` dilim başı + 11 ile yeşil; `git diff` yalnız sayı satırı + yorum | **S-65c:** bir 0182 controller'ına geçici `[HttpGet("barcode/count-sabotage")]` + `[HasPermission(BarcodePermissions.ScanEventsRead)]` ekle → yalnız 0178 sayı satırı kırmızı |

**Paketin DL-4'e uzanan genel AC'leri (ayrı vaka, aynı tablo):** AC-3 (kiracı / şirket 404 — DL-4 nesneleri), AC-9 r4 (komut idempotency deposu: şablon ve
basım replay'i ilk durum kodu + gövde, ikinci yazım / merkezi kayıt / numara yok; başarılı düzenleme replay'i eski `If-Match`'le 409 değil ilk cevap),
AC-23 (+5; S-23: bir DL-4 komutundan `ICentralAuditDeclaration`'ı kaldır → `CentralAuditDeclarationTests` kırmızı), AC-25 (`LedgerImmutabilityArchitectureTests`
— PUT / DELETE yok), AC-26 (yeni koleksiyonlar `BarcodeCollections.All`'da — X-5; S-W1: listeden bir adı çıkar → `SingleSystemOfRecordArchitectureTests`
kırmızı), AC-1 deseni (sözleşme testi DL-4 şemaları kapalı; S-W2: `LabelPrintJob` cevabından zorunlu üyeyi kaldır → kırmızı).

Her sabotaj **üretim koduna**: değişiklik → kırmızı test adı + çıktı özeti → bayt-eşit geri al (sha256 önce = sonra) → yeşil; commit'e girmez. Paketin
DL-4 listesi (S-47, S-48, S-49, S-50, S-51, S-52, S-65c) **tam** koşulur; AC-31…AC-34, AC-39, AC-40 sabotajları yeni sayfalar için ayrıca. Listede olmayan
her kırmızı adıyla raporlanır.

**Koşular:**
- **InventoryService filtresiz:** `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests` — `INVENTORY_TEST_MONGO` **boş**, **27017 yok**,
  `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`; **aynı anda tek Inventory takımı**. Son CT ölçümü `6cdb92e43`: 2662 / 2658 / 0 / 4.
- **Gateway:** `dotnet test gateway/Diten.ApiGateway.Tests` (son CT 384 / 384) — +11 ile yeşil.
- **Web:** `dotnet test frontend/Diten.Web.Tests` filtresiz (taban kırmızıları adıyla); vitest tam (`node_modules\.bin\vitest.cmd run`; Windows yol ayracı
  taban kırmızıları adıyla) + `tests/role-assignments-label-ledger.test.js` + `tests/barcode-screens.test.js`.
- **Araçlar:** `verify_datatable_page.py` (iki ekran), `python scripts/check_pack_nav_keys.py`.
- **Platform:** `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~RecordCodeTypeCatalogTests` — değişmeden
  yeşil (`LBL` önde). Başka Platform koşusuyla aynı anda değil.
- **Mimari:** `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests`.
- **Build:** `Diten.InventoryService`, `Diten.Web` PASS.
- **AC-41 kanıtı:** `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse
  services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount` **boş**; `Mod0178WarehouseRouteTests.cs` farkı yalnız sayı satırı + yorum.
- `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşulmaz**.

## 5. Kabul kapıları

| # | Kapı | Ölçüm |
|---|---|---|
| K-0 | Hat ucu ≥ `8c32f4d90`; paket sha `6838d397…`; r9 onayı `main`'de; G-5 ve G-6 teyitleri kayıtta | §0 |
| K-1 | Taslağın DL-4 satırları bağlayıcı (11 işlem); sözleşme testi kapalı şemayla yeşil; `FROZEN` yok | §1.1 |
| K-2 | AC tablosu (AC-47…AC-52, AC-31…AC-34, AC-39, AC-40, AC-65 + genel AC'ler) test adı + sonuçla; F-1…F-8 uygulanışı | §4 |
| K-3 | Paket sabotajları (7) + yeni sayfa sabotajları + S-W1, S-W2 kırmızı → geri al → yeşil | §4 |
| K-4 | Sayaçlar: uç +11, komut +5, `seen` 0, `TypePrefixes` + `LBL`, 0182 koleksiyon vakası +3 | §0 madde 5 |
| K-5 | InventoryService, Gateway, Web.Tests, vitest, Platform katalog, TenantArchitecture yeni kırmızısız | §4 |
| K-6 | `INVT/Warehouse/**`, `INVT/CycleCount/**`, MOD-0178 üretim kodu, `ocelot.json`, `.antigravity/**`, csproj'lar diff'i boş | `git diff --stat` |
| K-7 | Canlı turda (CT): şablon → önizleme → basım → PDF (tarayıcı yazdırma penceresi), 375 px, `ar` RTL, izinsiz kullanıcı | CT kaydı |
| K-8 | Stage açık yollarla, push / PR yok | rapor |

**DL-4 VERIFIED** CT kabul kaydıyla verilir. Sonraki 0182 dilimi: **DL-5** (kapı: MOD-0290-FU07 DL-1 `main`'de ve pilotta ölçülmüş olmalı — ölçülmedi) — CT kapıyı ölçüp WP'yi
yazar; DL-8 her an ayrı WP olarak verilebilir.

## 6. Rapor

1. Paket sha256; dal adımı ve `merge-base` doğrulamaları (r9 onayı dahil); `<dilim-başı>`.
2. G-5, G-6 teyit commit'leri — ya da "teyit yok — dokunulmadı".
3. Commit listesi ve `git diff --stat`; X-12'nin uçlarla, X-6 (a)'nın komutlarla, X-5'in koleksiyonlarla aynı commit'te olduğu açıkça.
4. Sayaçlar önce → sonra (satırın kendisiyle): `endpoints.Count`, `WriteCommands.Length`, `seen`, `TypePrefixes`, 0182 koleksiyon listesi, `Implemented`.
5. Takım sayıları önce / sonra (InventoryService, Gateway, Web.Tests, vitest, Platform katalog, TenantArchitecture); geçersiz koşular ayrı; p95 testi notu.
6. AC tablosu ve sabotaj tablosu (üretim dosyası:satır, değişiklik, kırmızı test adı, geri alma sha256, geri yeşil).
7. `verify_datatable_page.py` (iki ekran) ve `check_pack_nav_keys.py` çıktıları; sahibe sorulan sapmalar numaralı listeyle.
8. F-1…F-8'in uygulanışı; en büyük PDF'in boyutu (F-4); taslak düzeltme önerileri; "canlı turda" kalan maddeler.

## 7. Kural haritası (AGENTS.md §6.1)

- **Daima:** `multi-tenancy`, `security-jwt`, `git-safety`, `code-style`, `docs-organization`, `cross-module-change-consent`.
- **Backend:** `handler-design`, `repository-standard`, `response-envelope`, `pipeline-behaviors`, `entity-base-template`, `entity-versioning`, `mongo-indexing`,
  `api-conventions`, `routes`, `ports`, `audit-trail-standard` (AUD-001).
- **Yetki / UI:** `permission-key-standard` (PKS-001 `warehouse.barcode.*`), `business-module-enforcement-standard`, `unauthorized-surface-standard` (UAS-001),
  `views-organization` (Layout), `frontend-datatable-template` (dokunma protokolü), `page-help-and-keyboard` (UXH-001), `localization-standard` (7 dil).
- **Kayıt kodu:** `record-coding-standard` (RCS-001 §3 `LBL`).
- **Ortam / kanıt:** `dev-runbook`, `configuration-safety`, `logging-observability`, `status-reporting-and-evidence`.

Dosyalar `.antigravity/rules/<ad>.md`; okumak serbest, yazmak yasak.

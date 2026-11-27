# WP-0180-DL5 — MOD-0180 Dalga Planlama DL-5: performans sayfası (KPI serileri) + CSV dışa aktarımı (arka uç + ekran)

Tarih: 2026-11-27 · Veren: CT (MVP-5 WMS CT) · Künye: **draft** · Sahip: **Beste** · Yürüten: geliştirici oturumu (MVP-5)
Durum: **taslak — başlama kapıları kapalı (paket §24 DL-5 "—"; seam onayları var); CT başlama mesajı bekler (§0 madde 1)**

- Modül: **MOD-0180 — Wave Planning (Dalga Planlama)**, servis `Diten.InventoryService` (5067) + `Diten.Web` (5001, yalnız Gateway 5000 üzerinden),
  kiracı modülü (7 dil).
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0180-wave-planning.md` **r11**, LF sha256
  `226ab7f9f3690c422fba1bf5a3008d004d82791cfe6b20ebb2b74c0cd47cb095` (CT ölçümü 2026-11-27: hat @ `8c32f4d90` ve `origin/main` @ `85e3b1eec` aynı),
  `status: ready-for-dev`. Onay: `execution/approvals/approvals.tsv:87` (onaylayan Ali / `GMG-ERP`, 2026-11-13; commit `e12bf26c8`) — bütün paket,
  dilim ayrımı yok. İnceleme r11 UYGUN: `docs/records/reviews/MOD-0180/2026-11-13-226ab7f9.md`.
- **Sıradaki dilim tespiti:** paket §23 "Teslim dilimleri" (dilim tablosu §23'tedir; §22 "Record Codes"dur) ve §24 "Sıra: DL-1 → DL-2 → DL-3 → DL-4 →
  **DL-5** → DL-6". DL-1 VERIFIED 2026-11-13, DL-2 VERIFIED 2026-11-14, DL-3 VERIFIED kod 2026-11-18, DL-4 VERIFIED 2026-11-20 (dalga akışı canlı turu) —
  `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md`. Sıradaki: **DL-5**. Ardından **DL-6** (yalnız Gateway kapsama testi,
  integration-agent) — paket §24: "DL-6 DL-5 kabulünden sonra" (AC-55 klasör / taslak eşitliği bütün 0180 uçlarını ister: DL-5 sonrası klasör 23 = taslak 23).
- Dilim: **DL-5** (paket §23), kanıt seviyesi **E4**.
- Sahip kararı (devam): `docs/records/decisions/2026-11/ct-owner-decisions-2026-11-27.md` §3 — "MOD-0180 kalan dilimler". Bu kayıt kod / commit / dağıtım
  izni değildir.
- **Canlı tarayıcı turu bu WP'de yapılmaz**; tarayıcı / canlı kanıtlar (375 px, `ar` RTL, UAS-001 ekranı, CSV indirme) raporda "canlı turda" diye ayrı satırda
  kalır, CT turunda ölçülür.
- Yetki: paket > `domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse **paket geçerlidir**; çelişkiyi CT'ye yaz, kendin çözme.
  Bu WP push, PR ya da `.antigravity/**` değişikliği izni değildir.

`INV` = `services/Diten.InventoryService/src/Diten.InventoryService.`, `INVT` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`.

## 0. Başlamadan

1. **Dal.** Tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` (ana checkout `C:\Users\Ali\erp-vnext`); **yeni dal açma**.
   `git merge-base --is-ancestor 8c32f4d90 HEAD` → çıkış 0; değilse `git merge --ff-only 8c32f4d90`, ff olmazsa **dur**. CT başlama mesajında etiket verirse
   ona ileri sar. Doğrula (çıkış 0): `… a39a9c644 HEAD` (DL-4 canlı tur düzeltmeleri), `… 6cdb92e43 HEAD` (DL-3 Açık 1).
2. **Paket sha'sı:** `tr -d '\r' < <paket> | sha256sum` → `226ab7f9…cb095`; raporun ilk satırına yaz. Farklıysa **dur**.
3. **Başlama kapıları:**

   | # | Kapı | Durum (CT, 2026-11-27) |
   |---|---|---|
   | G-1 | Paket §24 DL-5 "Başlamadan kapanması gereken kararlar" | **"—" — kapalı** |
   | G-2 | DL-4 kabulü (sıra) | **kapalı** — DL-4 VERIFIED 2026-11-20 |
   | G-3 | X-14 Gateway sayısı (+2) | **kapalı** — sahip onayı `c7dc4ea6a` + kapsam teyidi `6235794ab` ("DL-1, DL-2, DL-3 ve DL-5 her biri"; `mod0180-wave-planning-owner-decisions-03.md:30-35`) |
   | G-4 | X-15 Rol İzinleri satır adı | **kapalı** — `mod0180-wave-planning-owner-decisions-03.md:44-49` ("DL-5: satır"; `1a91774d7`) |
   | G-5 | `PageHelpCatalog.cs` (`WAVEKPIS`) eklemeli | **kapalı** — paket §5 Repo Scope satırı + §24 DL-5 seam'i; DL-4 K-8 emsali (`e0bf26918` 0181 emsali) |
   | G-6 | X-10 (merkezi denetim sayısı) | **değişmez** — DL-5 yazma komutu eklemez (§21 "Yazma olmayan: KPI okuması ve CSV") |
   | G-7 | X-13 AuthService rol şablonu (Q-20, Ali) | DL-5 kapısı **değil** (paket §24 yalnız DL-4 kabul kapısı der); "Performans izleyici" rolü (§14 öneri) X-13 WP'sinde |

4. **Dilim başı commit'i (`<dilim-başı>`)** kaydet; bütün "önce" ölçümleri bu commit'te, aynı gün.
5. **Dilim başında ölçülecek sayaçlar** (CT `8c32f4d90` ölçümü; satırın kendisine atıf yap):

   | Sayaç | Dosya / satır | Bugün | DL-5 değişikliği |
   |---|---|---|---|
   | Gateway uç sayısı (X-14) | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` `Assert.Equal(128, endpoints.Count)` (`:131`) | **128** | **+2** (`GET /waves/kpis`, `GET /waves/kpis/export`) → 130 (çıkarım; önce başka dilim eklerse o anki değer + 2) |
   | 0180 klasör uç sayısı | `INV/Api/Controllers/WavePlanning/` (`WavesController` 18 + `WaveExceptionsController` 3) | **21** | +2 → **23** = WMS-WAVE-BUNDLE taslağı işlem sayısı (AC-55 eşitliği — DL-6 için ön koşul) |
   | Merkezi denetim (X-10) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` `Assert.Equal(151, WriteCommands.Length)`, `Assert.Equal(17, seen)` | 151 / 17 | **değişmez** — değişirse **dur** |
   | `wms_wave_*` (X-9) | `SingleSystemOfRecordArchitectureTests` `WaveCollections.All` | 5 ad | **değişmez** (KPI saklanmaz; CSV dosyası saklanmaz — §8.16) |
   | `WavePermissions.Implemented` | `INV/Infrastructure/Authorization/WavePermissions.cs` (`[.. Dl1, .. Dl2, .. Dl3]`) | 9 anahtar | + `Dl5` = `kpis.read`, `kpis.export` |
   | Rol İzinleri defteri | `docs/roadmap/backlog/role-permission-label-ledger.md` tavanı | — | **değişmez**; yeni fiil yok (`read`, `export` köprüde var) |
   | Web nav tabanı | `NavManifestL10nGuardTests.cs` `NavVisiblePageKeyFloor = 45` | 45 | değiştirme (alt sınırdır); kırmızıysa **dur**, CT'ye yaz |

6. **Eşzamanlılık ve ortam:**
   - Aynı sayaç satırını (Gateway) **MOD-0179 DL-2** (+11) ve **MOD-0182 DL-4** (+11) de değiştirir; ortak Web dosyalarını (`SharedResource.*.resx`,
     `PageHelpResource.*.resx`, `PageHelpCatalog.cs`) **MOD-0182 DL-4** de değiştirir. Her sayaç / ortak dosya commit'inden **hemen önce** hattı al, o anki
     değerden yeniden hesapla, yorumda "measured <n> at <commit>"; çakışmada iki tarafın eklemesini koru, CT'ye yaz.
   - Ana checkout'ta derleme / test / sabotaj **yok** (Inventory 5067 `dotnet watch`). Ayrık worktree: `git worktree add --detach ..\erp-vnext-wt-0180-dl5 HEAD`.
   - **Makinede aynı anda tek InventoryService takımı.** Mongo zaman aşımıyla düşen koşu sayılmaz ("geçersiz koşu"). `dotnet test` çıktısını boruya verme, dosyaya yönlendir.
   - vitest yalnız ana checkout'un `frontend\Diten.Web\node_modules`'una junction ile (`node_modules\.bin\vitest.cmd run`; `npx` yok); worktree kaldırılmadan
     önce junction tek başına silinir (`[System.IO.Directory]::Delete(<yol>, $false)`).
7. **Paketi oku (özet yetmez):** §3 (uçlar, sorgular, frontend rotaları), §4.1 (`ReleasedAt`, `CompletedAt`, `CutoffAtUtc`), §5 (DL-5 satırları), §5.1 X-14,
   X-15, §8.4 (yerel gün), §8.15 (Performance KPIs satırı), §8.16 (bütünüyle), §8.17 `WAVE_KPI_RANGE_TOO_LARGE`, §11 (`Views/Warehouse/WaveKpis/`),
   §12 KPI aralığı satırı, §14 (`kpis.read` / `kpis.export`, `WAVEKPIS` sayfası, rol önerisi), §16 DL-5 bloğu (AC-51…AC-54) + AC-55 (0178 sayı vakası)
   + AC-56, §17, §21 son paragraf, §24 DL-5 satırı, §25 `WAVEKPIS`. Taslak `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` `WaveKpiSeries`
   (`:219`), `getWaveKpis` / `exportWaveKpis` (`:287-288`), `WAVE_KPI_RANGE_TOO_LARGE` (`:323`).

## 1. Kapsam

### 1.1 Paket DL-5 satırı (§23) — birebir
"performans sayfası + CSV". Kapsadığı bölümler: **Performans sayfası ve CSV, Frontend File Contract, Audited Events.** Bu listenin dışına çıkma.

**Arka uç:**
- **Sözleşme önce:** taslağın `getWaveKpis` / `exportWaveKpis` satırları ve `WaveKpiSeries` şeması DL-5'in bağlayıcı yüzüdür; kod buna karşı yazılır,
  0180 sözleşme testi (`INVT/WavePlanning/WaveDraftContract.cs` deseni) iki işlemi kapalı şemayla doğrular. Taslakta düzeltme gerekirse **önce** taslak
  commit'i (yalnız DL-5 satırları), CT'ye bildir. **Dondurma geliştiricinin işi değildir** — CT adımı, DL-5 kabulünden sonra ve DL-6'dan önce
  (`mod0180-wave-planning-owner-decisions-03.md` "Dondurma zamanı"); `FROZEN` koyma, `wms-wave-bundle.openapi.yaml` yazma.
- **Sorgular (2):** `GetWaveKpisQuery`, `ExportWaveKpiCsvQuery` (§21 adı) — `INV/Application/Features/WaveKpis/` Golden Slim (yalnız `Queries/`,
  `Handlers/QueryHandlers/`, `Validators/`, `WaveKpiModels.cs`; komut **yok**).
- **Uçlar (2):** `GET /api/warehouse/waves/kpis` (`warehouse.waves.kpis.read`), `GET /api/warehouse/waves/kpis/export` (`warehouse.waves.kpis.export`,
  `text/csv; charset=utf-8`). Controller `WaveKpisController` (`INV/Api/Controllers/WavePlanning/`, `[Route("api/warehouse")]`, namespace
  `…Controllers.WavePlanning`; `[HasPermission]` eylem düzeyinde). Sabit kesim `kpis` `{waveId:guid}`'den önce eşleşir. PUT / DELETE yok.
- **Hesap (§8.16, D-16 — birebir):** süzgeç depo (zorunlu), `from` / `to` (depo yerel günü, `from ≤ to`, **en çok 92 gün**, aksi 400
  `WAVE_KPI_RANGE_TOO_LARGE`), dalga türü (isteğe bağlı). Seriler deponun **yerel gününe** göre (`WaveWarehouseSettings.TimeZoneId`; §8.4):
  günlük dalga sayısı (o gün `ReleasedAt`'i olan); ortalama tamamlanma süresi (`CompletedAt − ReleasedAt`, `Completed` + `PartiallyCompleted`, tamamlandığı
  güne); kesime yetişme oranı (`CompletedAt ≤ CutoffAtUtc` olan / o gün kesimi olan tamamlanmış dalgalar); toplayıcı başına satır / saat (dalga görevlerinin
  `Posted` onay sayısı / toplayıcıların etkin saatleri — toplayıcı başına gün içinde ilk → son onay, en az 0,25 saat; toplayıcı = onayın `ConfirmedBy`'ı).
  KPI saklanmaz, okuma anında hesaplanır. Hesap saf bir sınıfta (`INV/Domain/WavePlanning/Rules/` ya da `Features/WavePlanning/Services/` — paket §5
  "KPI"; birim testli).
- **0178 okuması:** onaylar (`ConfirmedBy`, `Posted`) **yalnız `Find`** ile, mevcut 0180 okuma yolundan (`INV/Persistence/WavePlanning/WaveInventoryReader.cs`
  + `IWaveInventoryReader` portu — eklemeli yöntem) ve `WarehouseCollections.*` sabitleriyle (AC-16 mimari taraması DL-1'den yeşil kalır). 0178 dosyasına
  dokunmak gerekirse **dur** (XMC-001).
- **CSV (§8.16):** aynı seriler aynı süzgeçle, satır başına gün; başlık satırı isteğin `Accept-Language`'ında (7 dil); sayılar **değişmez kültürde**
  (`.` ondalık); UTF-8; dosya **saklanmaz**. CSV hücre kaçışı (formül enjeksiyonu — MOD-0176 kuralı; emsal
  `INV/Application/Features/CountEvidenceExports/CountEvidenceDocumentBuilder.cs:292`): metin hücreleri (başlık, dalga türü adı) nötrlenir; başka modülün
  sınıfı **değiştirilmez** — paylaşılan yardımcı yoksa 0180 içinde aynı kuralla yazılır.
- **İzinler (§14):** `WavePermissions.KpisRead` (Tier 1), `WavePermissions.KpisExport` (Tier 2); `Implemented` += `Dl5`. Manifest
  `WavePlanningManifestProvider`: sayfa `WAVEKPIS` (nav, `kpis.read`, D-16 — **ayrı sayfa**) + CSV düğmesi için eylem (`kpis.export`); `ApiPermissions`
  değişmez (yalnız `settings.configure`).

**Ekran (Frontend File Contract, §11):**
- `frontend/Diten.Web/Views/Warehouse/WaveKpis/`: `Index.cshtml` (grafikler + tablo görünümü + CSV düğmesi; **`Layout = "_LayoutTenantShell";` açık**),
  `_Filter.cshtml` (depo zorunlu, tarih aralığı ≤ 92 gün, dalga türü), `_IndexL10n.cshtml`, `WaveKpisIndex.cs`. Paket bu sayfa için `_DataTable.cshtml`
  **listelemez** — tablo görünümü erişilebilir düz `<table>`'dır (liste ekranı değil). DataTable kullanmaya karar verirsen dokunma protokolü
  (`verify_datatable_page.py --area Warehouse --module WaveKpis --format gaps`) uygulanır ve sapmalar sahibine sorulur.
- Grafik: deponun vendored kütüphanesi **ApexCharts** (emsal `Views/Recall/Index.cshtml` + `wwwroot/assets/js/Recall/dashboard.js`); yeni kütüphane / CDN yok.
  Her grafiğin yanında aynı veriyi veren tablo (renge dayanmayan sunum; klavye).
- `wwwroot/assets/js/Warehouse/WaveKpis/index.js`, `index.l10n.js`; `Resources/Views/Warehouse/WaveKpis/WaveKpisIndex.{en,tr,fr,es,zh,ar,ru}.resx`.
- `frontend/Diten.Web/Controllers/WaveKpisController.cs` — same-origin MVC proxy → Gateway `/api/warehouse/waves/kpis…`; sayfanın `Accept-Language`'ını
  iletir (CSV başlığı); CSV akışını tamponlamadan geçirir; sayfa eylemi izin denetler (UAS-001: izinsiz kullanıcıya başlık / grafik / tablo / CSV düğmesi
  yok, tek açıklama, **yönlendirme yok** — AC-54).
- Ortak, **yalnız eklemeli:** `SharedResource.{7}.resx` `Nav.Page.WAVEKPIS` + X-15 `Perm.Entity.*` satır adı (kanonik anahtar vitest'ten — beklenen
  `WAVESKPIS`, çıkarım); `PageHelpResource.{7}.resx` `Help.WAVEKPIS.Purpose`, `.Audience`, `.Step1`/`.Step1Title`, `.Step2`/`.Step2Title` (§25: depo ve
  tarih aralığını seç — en çok 92 gün, CSV indir); `PageHelpCatalog.cs` `WAVEKPIS` adım ikonları (+ gerekiyorsa `Related`). **Tur yok.**
- Web testleri: `frontend/Diten.Web.Tests/Warehouse/WavePlanningL10nAndHelpTests.cs` (DL-5 vakası: `WaveKpis/*.cshtml` Layout — AC-54'e taşınan vaka; 7 dil
  parite; `Help.WAVEKPIS.*`), `WavePlanningProxyControllerTests.cs` (CSV proxy + `Accept-Language`), `WavePlanningScreensTests.cs` (UAS-001); gerekiyorsa
  yeni vitest `tests/wave-kpis.test.js` (grafik + tablo aynı seriyi çizer, boş / hata durumu).

### 1.2 Görev Merkezi A yolu ile ilişki (kapsam DIŞI — bilgi)
- **Karar (Beste, 2026-11-19 — yol A):** depo görevleri (toplama, yerleştirme) Görev Merkezi'nin "bana atanan" listesinde görünür ve oradan aynı
  WMS / Inventory uçlarıyla (Gateway üzerinden) yürütülür; görev kaydı WMS'te kalır (Görevler modülüne kopya yok — yol B reddedildi); mobil olduğu gibi,
  Toplama Görevleri listesi sorumlu listesi olarak kalabilir. **Başlama koşulu:** dalga akışı canlı turu — **2026-11-20'de tamamlandı** (DL-4 VERIFIED), yani
  koşul artık **sağlanmış** durumda.
- **Bu dilimle ilişkisi: yok.** Paket §23 DL-5 Görev Merkezi işi içermez; A yolu MOD-0180'in değil **MOD-0178**'in (görev kaydının sahibi) paket revizyonudur
  ve Görev Merkezi (Platform `WorkItems`) sağlayıcı satırını gerektirir. DL-5 **toplama görevi uçlarına, `PickTasks` ekranına, Görev Merkezi sağlayıcısına,
  `WorkAggregation:RemoteProviders` ayarına dokunmaz**; 0180 dalga istisnalarının Görev Merkezi'ne bağlanması da "MVP-5 sonrası FU" kalır
  (`docs/records/decisions/2026-11/workcenter-integration-analysis-2026-11-13.md` 0180 satırı).
- **A yolunun kendi adımları (CT işi, bu WP'nin dışında; sıra önerisi):** (1) salt okunur keşif — Görev Merkezi paketi / sahibi / `sourceModule` alımı,
  uzak sağlayıcı sözleşmesi (`api/v1/work-items/projection` + eylem ucu), BL-303 sırayla çağırma riski; (2) MOD-0178 paket revizyonu taslağı + mockup brief'i;
  (3) bağımsız inceleme; (4) Ali onay satırı; (5) ancak sonra WP. Yeni MOD / FU kimliği `verify_module_id.py` ön kontrolüyle (DCP-002); uydurulmaz.
- **Çelişki notu (sahibe soru, §6):** 2026-11-13 analiz kaydı "Görev Merkezi kısmını **MVP-5 bittikten sonra** ekleriz" der; 2026-11-19 A yolu kararı
  "dalga canlı turundan sonra" der ve yazılı karar kaydında **yok** (yalnız CT-QUEUE notu ve oturum belleği). A yolu açılmadan önce karar kaydına
  yazılmalı ve hangi koşulun geçerli olduğu teyit edilmeli.

### 1.3 Dokunulacak dosyalar

**0180'in kendi dosyaları (paket §5 DL-5 satırları):** `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` (yalnız DL-5 satırları, gerekirse) ·
`INV/Application/Features/WaveKpis/` · `INV/Application/Features/WavePlanning/{Ports,Services}/` (KPI hesabı, okuma portu — eklemeli) ·
`INV/Domain/WavePlanning/Rules/` (saf KPI kuralı, seçilirse) · `INV/Persistence/WavePlanning/WaveInventoryReader.cs` (eklemeli `Find`) ·
`INV/Api/Controllers/WavePlanning/WaveKpisController.cs` · `INV/Api/WavePlanning/WavePlanningManifestProvider.cs` (eklemeli) ·
`INV/Infrastructure/Authorization/WavePermissions.cs` (eklemeli) · CSV başlık metinleri — 7 dil tek kaynak (emsal `INV/Application/Features/CycleCount/CycleCountEvidenceText.cs`
ya da `Features/Barcode/Resources/BarcodeText.{7}.resx`; geliştirici seçer, parite testi zorunlu) · `INVT/WavePlanning/` · Web: §1.1 "Ekran" dosyaları.

### 1.4 XMC-001 dokunuşları (yalnız eklemeli; satırlar `<dilim-başı>`'nda yeniden ölçülür)

| # | Dosya | DL-5 değişikliği | Sahip | Onay | Hangi commit'te |
|---|---|---|---|---|---|
| X-14 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — **yalnız** `Assert.Equal(<n>, endpoints.Count)` + üstündeki uç yorumu | dilim başı **+2**, yorumda iki uç adıyla; **integration-agent** yazar (geliştirici ölçülen önce sayıyı, uç listesini ve commit'i iletir; dosyayı kendisi düzenlemez) | integration-agent (test); rota ailesi MOD-0178 | `c7dc4ea6a` + `6235794ab` — **onaylı** | `WaveKpisController` uçlarıyla **tek commit** |
| X-15 | `frontend/Diten.Web/Resources/SharedResource.{7}.resx` `Perm.Entity.*` | **yalnız satır adı** (kpis); fiil eklenmez (`read`, `export` var); `_IndexL10n.cshtml`, `RoleAssignmentsIndex.*.resx`, `perm-label.js` değişmez; var olan satır değişmez | Governance Rol İzinleri (Beste) | decisions 03 `:44-49` — **onaylı** | manifest anahtarlarını bildiren commit (AC-56) |
| X-10 | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` | **değişmez** (KPI komutu yok) | MOD-0173 | — | — |
| §5 | `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs` | `WAVEKPIS` `StepIcons` (+ `Related`) eklemeli satırlar | Web ortak | paket §5 / §24 DL-5 | yardım commit'i |

**Değişmez (bağlayıcı):** `INVT/Warehouse/**` hiçbir satırı (AC-18 kanıtı her dilimde); `InventoryLedgerManifestCompletenessTests.cs` (X-17 yalnız DL-1'de);
`SingleSystemOfRecordArchitectureTests.cs`; MOD-0178 üretim kodu (özellikle `PickTasks*`, `WarehouseTask*`); `NavManifestL10nGuardTests.cs`;
`ocelot.json`; `.antigravity/**`; `services/Diten.Building.Blocks/**`, `services/Diten.Platform/**` (Görev Merkezi dahil), `services/Diten.AuthService/**`;
`_Layout.cshtml`; MOD-0179 / 0181 / 0182 dosyaları.

### 1.5 Kapsam dışı
DL-6 (`Mod0180WaveRouteTests.cs`, integration-agent; DL-5 kabulünden sonra); WMS-WAVE-BUNDLE dondurması (CT adımı); X-13 rol şablonu (Ali); Q-18 okuma
denetimi (karar A — v1'de yok, FU-0180-READAUDIT); Görev Merkezi A yolu (§1.2); KPI'ların pano kartlarına taşınması (pano DL-3 / DL-4'te bitti; değişmez).

## 2. Kararlar ve CT önerileri

**Kapalı:** karar 03'ün bütünü (D-1…D-17, Q-1…Q-20 paket §19.4 durumuyla; D-16 performans ayrı sayfa; Q-18 = A okuma denetimi yok); WMS-WAVE-BUNDLE
taslağı CT kararları (decisions 03 son ekler); DL-4 K-2…K-9.

**Açık — CT kararı (CT önerisi parantezde; geliştirici kendisi seçmez):**
- **E-1 — ayar kaydı olmayan depo:** saat dilimi `WaveWarehouseSettings.TimeZoneId`'den. (CT önerisi: ayar yoksa 422 `WAVE_SETTINGS_REQUIRED` — AC-14'ün
  kuralıyla aynı; KPI sunucu saat dilimine düşmez. S-51 bunu da kapsar.)
- **E-2 — "o gün kesimi olan tamamlanmış dalga" yoksa oran:** (CT önerisi: oran `null` — sıfır değil; CSV'de boş hücre; ekranda "—". Taslak `WaveKpiSeries`
  `days[]` üyeleri isteğe bağlıysa uygulanır; değilse taslak düzeltmesi CT'ye.)
- **E-3 — satır / saat'te `Posted` dışı onaylar (`Refused`, `IntegrityError`):** (CT önerisi: sayılmaz; etkin saat de yalnız `Posted` onayların zamanından.)
- **E-4 — dalga türü süzgeci boşken:** bütün türler. (Uygulanır.)

## 3. Kurallar

- **Kiracı (Çekirdek 2):** her okuma `TenantId` + `LegalEntityId` (sunucuda) + istenen depo; başka kiracı / şirket / depo verisi seriye girmez (AC-53 "başka
  kiracının verisi yok"); başka şirketin deposu 404; sorguda `tenantId` / `legalEntityId` 400.
- **Yetki (Çekirdek 3, UAS-001):** `kpis.read` olmadan sayfa ve JSON 403; `kpis.export` olmadan CSV 403 (yalnız `read` taşıyan kullanıcıda dahi);
  izinsiz kullanıcıya iskelet çizilmez, yönlendirme yapılmaz.
- **Denetim (§21):** KPI ve CSV okumadır — merkezi kayıt **yok**; `CentralAuditDeclarationTests` değişmez.
- **Dil (Çekirdek 4):** 7 dil (en, tr, fr, es, zh, ar, ru) — sayfa resx'i, nav, yardım, CSV başlığı; `ar` RTL; Title Case kuralı (0180 L10n testi).
  Bir dil eksikse iş bitmemiştir. Sayılar CSV'de değişmez kültür; ekranda sayfa kültürü.
- **Korunan yollar (Çekirdek 6):** `.antigravity/**` yazılmaz; `ocelot.json` değişmez; X-14 satırını integration-agent yazar; `_Layout.cshtml` / Archive donuk.
- **XMC-001:** §1.4 dışında başka modül dosyasına dokunma; gerekirse **dur**, CT'ye sor.
- **Git (GIT-002):** `main`'e commit / push yok; push / PR yok (modül sonunda, ayrı onayla, öncesinde `origin/main` merge commit'iyle); stage açık yolla;
  hook / imza atlanmaz; yıkıcı komut yok; `git stash` yalnız etiketli + SHA. İletiler `feat(sce): MOD-0180 DL-5 — …`, `feat(web): MOD-0180 DL-5 — …`.
- **Commit sırası (öneri):** (1) taslak düzeltmesi (gerekirse) → (2) saf KPI hesabı + birim testleri (AC-51 fikstürü) → (3) okuma portu + sorgular +
  izinler + manifest + X-15 satır adı → (4) `WaveKpisController` + X-14 (**tek commit**, integration-agent adımı) → (5) Web sayfası, proxy, resx, nav, yardım,
  `PageHelpCatalog.cs` → (6) Web testleri / vitest. (3) ve (4)'te InventoryService filtresiz koşusu; (5)–(6)'da Web.Tests + vitest.

## 4. Kabul ölçütleri, test ve sabotaj

| AC | Kısa başlık | Test (öneri) | Sabotaj (paket) |
|---|---|---|---|
| AC-51 | KPI formülleri fikstürle: 3 günde 5 dalga (bilinen `ReleasedAt` / `CompletedAt` / `CutoffAtUtc`, iki toplayıcı) → dört seri beklenen değerle; yerel gün, gece yarısı sınırı vakası | `INVT/WavePlanning/` birim + entegrasyon | **S-51:** günü UTC'ye göre kovala → sınır vakası kırmızı |
| AC-52 | 92 gün 200, 93 gün 400 `WAVE_KPI_RANGE_TOO_LARGE`; depo zorunlu | entegrasyon | **S-52:** üst sınırı kaldır → kırmızı |
| AC-53 | CSV `kpis.export` ister (yoksa 403); içerik JSON serisiyle satır satır aynı; `Accept-Language: tr` başlığı Türkçe, `ar` Arapça; sayılar değişmez kültür; başka kiracı verisi yok | entegrasyon + `AuthorizationGuardTests` | **S-53a:** CSV yolundan `[HasPermission]`'ı kaldır → `AuthorizationGuardTests` kırmızı; **S-53b:** sayıları istek kültürüyle yaz (`tr` → virgül) → kırmızı |
| AC-54 | `WAVEKPIS` menüde ayrı sayfa (nav); `WaveKpis/*.cshtml`'in her sayfa görünümünde `Layout = "_LayoutTenantShell"`; UAS-001; `check_pack_nav_keys.py` PASS | `WavePlanningL10nAndHelpTests` (DL-5 vakası), `WavePlanningScreensTests` | **S-54:** Web `WaveKpisController` sayfa eyleminden izin denetimini kaldır → UAS-001 vakası kırmızı; **S-54b:** `WaveKpis/Index.cshtml`'den `Layout` satırını kaldır → Layout vakası kırmızı |
| AC-55 (0178 sayı vakası) | Gateway `Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route` dilim başı + 2 ile yeşil; `git diff` yalnız sayı satırı + yorum | Gateway test projesi | **S-55c:** bir 0180 controller'ına `[HasPermission(WavePermissions.Read)]` + `[HttpGet("waves/count-sabotage")]` ekle → yalnız 0178 sayı satırı kırmızı |
| AC-56 (tekrar) | Her `warehouse.waves.*` anahtarının fiili ve satır adı 7 dilde; `role-assignments-label-ledger.test.js` yeşil; tavan değişmez | vitest | **S-56:** `SharedResource.ru.resx`'ten DL-5 `Perm.Entity.*` satırını sil → vitest kırmızı |

Ek (WP; paket §17 / §11'den): 7 dil parite ve `Help.WAVEKPIS.*` (S-W1: `PageHelpResource.zh.resx`'ten `Help.WAVEKPIS.Step2`'yi sil → kırmızı);
sözleşme testi iki işlem kapalı şema (S-W2: `WaveKpiSeries` cevabından zorunlu üyeyi kaldır → kırmızı); 0180 manifest bütünlüğü (AC-5 vakası) `kpis.*` ile yeşil.

Her sabotaj **üretim koduna**: değişiklik → kırmızı test adı + çıktı özeti → bayt-eşit geri al (sha256 önce = sonra) → yeşil; commit'e girmez. Listede olmayan
her kırmızı adıyla raporlanır.

**Koşular:**
- **InventoryService filtresiz:** `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests` — `INVENTORY_TEST_MONGO` **boş**, **27017 yok**,
  `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`; **aynı anda tek Inventory takımı**. Son CT ölçümü `6cdb92e43`: 2662 / 2658 / 0 / 4.
- **Gateway:** `dotnet test gateway/Diten.ApiGateway.Tests` (son CT 384 / 384) — +2 ile yeşil.
- **Web:** `dotnet test frontend/Diten.Web.Tests` filtresiz (taban kırmızıları adıyla — son CT ölçümünde MOD-0186 `ReturnDetailsBehaviorTests`); vitest tam
  (`node_modules\.bin\vitest.cmd run`; taban: Windows yol ayracı kırmızıları adıyla) + `tests/role-assignments-label-ledger.test.js`.
- **Araç:** `python scripts/check_pack_nav_keys.py` (AC-54). DataTable kullanıldıysa `verify_datatable_page.py`.
- **Mimari:** `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests`.
- **Build:** `Diten.InventoryService`, `Diten.Web` PASS.
- **AC-18 kanıtı:** `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse` **boş**;
  `git diff <başı>..<sonu> -- gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` yalnız sayı satırı + yorum.
- `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşulmaz**.

## 5. Kabul kapıları

| # | Kapı | Ölçüm |
|---|---|---|
| K-0 | Hat ucu ≥ `8c32f4d90`; paket sha `226ab7f9…`; G-1…G-6 kapalı | §0 |
| K-1 | 6 AC (AC-51…AC-54, AC-55 0178 sayı vakası, AC-56) test adı + sonuçla; E-1…E-4 uygulanışı | §4 |
| K-2 | 10 paket sabotajı (S-51, S-52, S-53a, S-53b, S-54, S-54b, S-55c, S-56 — + S-W1, S-W2) kırmızı → geri al → yeşil | §4 |
| K-3 | Sayaçlar: Gateway dilim başı + 2; klasör 23 uç; X-10 / X-9 değişmedi | §0 madde 5 |
| K-4 | InventoryService, Gateway, Web.Tests, vitest, TenantArchitecture yeni kırmızısız | §4 |
| K-5 | `INVT/Warehouse/**`, MOD-0178 üretim kodu, Platform (Görev Merkezi), `ocelot.json`, `.antigravity/**` diff'i boş | `git diff --stat` |
| K-6 | Canlı turda (CT): `WAVEKPIS` sayfası 375 px / `ar` RTL / izinsiz kullanıcı / CSV `tr` + `ar` başlık | CT kaydı |
| K-7 | Stage açık yollarla, push / PR yok | rapor |

**DL-5 VERIFIED** CT kabul kaydıyla verilir. Ardından: (1) CT WMS-WAVE-BUNDLE `1.0.0` dondurması; (2) **DL-6** WP'si (integration-agent; `Mod0180WaveRouteTests`).

## 6. Rapor

1. Paket sha256; dal adımı ve `merge-base` doğrulamaları; `<dilim-başı>`.
2. Commit listesi ve `git diff --stat`; X-14'ün `WaveKpisController` uçlarıyla aynı commit'te olduğu açıkça.
3. Sayaçlar önce → sonra (satırın kendisiyle): `endpoints.Count`; klasör uç sayısı (21 → 23) ve taslak işlem sayısı (23); `WriteCommands.Length` / `seen` değişmedi.
4. Takım sayıları önce / sonra (InventoryService, Gateway, Web.Tests, vitest, TenantArchitecture); geçersiz koşular ayrı.
5. AC tablosu ve sabotaj tablosu (üretim dosyası:satır, değişiklik, kırmızı test adı, geri alma sha256, geri yeşil).
6. `check_pack_nav_keys.py` çıktısı; DataTable kullanıldıysa `verify_datatable_page.py` çıktısı ve sahibe sorulan sapmalar.
7. E-1…E-4'ün uygulanışı; taslak düzeltme önerileri; "canlı turda" kalan maddeler.

## 7. Kural haritası (AGENTS.md §6.1)

- **Daima:** `multi-tenancy`, `security-jwt`, `git-safety`, `code-style`, `docs-organization`, `cross-module-change-consent`.
- **Backend:** `handler-design`, `repository-standard`, `response-envelope`, `pipeline-behaviors`, `mongo-indexing`, `api-conventions`, `routes`, `ports`,
  `audit-trail-standard` (okuma — kayıt yok).
- **Yetki / UI:** `permission-key-standard` (PKS-001 `warehouse.waves.*`), `business-module-enforcement-standard`, `unauthorized-surface-standard` (UAS-001),
  `views-organization` (Layout), `page-help-and-keyboard` (UXH-001), `frontend-datatable-template` (yalnız DataTable seçilirse), `localization-standard` (7 dil).
- **Ortam / kanıt:** `dev-runbook`, `configuration-safety`, `logging-observability`, `status-reporting-and-evidence`.

Dosyalar `.antigravity/rules/<ad>.md`; okumak serbest, yazmak yasak.

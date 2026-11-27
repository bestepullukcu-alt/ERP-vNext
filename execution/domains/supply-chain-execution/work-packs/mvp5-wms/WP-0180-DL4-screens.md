# WP-0180-DL4 — MOD-0180 Dalga Planlama DL-4: ekranlar (pano + ayrıntı, planlayıcı, istisna kuyruğu) + X-7 Toplama Görevleri dalga süzgeci

> **VERİLDİ (CT, 2026-11-18).** §2 kararları kapandı — sahip Beste 2026-11-18 (K-2…K-6), CT (K-7…K-9). K-1 Ali'de açık (kabul kapısı,
> başlama engeli değil). Bu belge kod yazma talimatıdır; commit yalnız açık yolla, push / PR / onay izni değildir.
>
> **Sahip kararları (Beste, 2026-11-18) — §2'deki önerilerin yerine geçer:**
> - **K-2:** XMC-001 izni verildi — `IWarehouseTaskRepository.cs` ve `WarehouseTaskRepository.cs`'e **yalnız ekleme** (`WaveId` süzgeci,
>   `WaveRef.Sequence` sıralaması). **Yeni indeks yok**; geliştirici sorgu planını `explain` ile ölçer, rapora koyar. `WarehouseIndexConfiguration.cs`'e dokunulmaz.
>   ENGEL madde 2 kapandı; X-7 commit'i başlayabilir.
> - **K-3:** `progress`'e `pickTasksDone` ve `pickTasksTotal` eklenir. CT taslak sözleşmeyi (`wms-wave-bundle-v1-proposal.md`) DL-4
>   başında günceller; geliştirici DTO'yu, sözleşme testini ve liste sütununu yazar. CT güncellemesi gelmeden tel commit'i başlamaz.
> - **K-4:** Dondurma **DL-5 kabulünden sonra** (taslaktaki gibi; DL-4'te dondurma yok). CT aynı taslak güncellemesinde WaveException
>   `outboundId` / `outboundNumber`'ı isteğe bağlı yapar (kod zaten isteğe bağlı yazıyor).
> - **K-5:** okuma anında hesaplanır — `pickStatus`: `NotStarted` / `InProgress` / `Done` (siparişin dalga görevlerinden); `packStatus`:
>   outbound `Packed` / `ReadyToShip` / `Shipped` ise `Packed`, değilse `Pending`; `shipStatus`: `Shipped` / `ReadyToShip` / `Pending`.
>   0178 durumu dalga teline aynen konmaz.
> - **K-6:** `listWaves`'e eklemeli `atRisk=true` sorgu parametresi; sunucu yalnız `Released` / `Picking` dalgalarını okur, riski hesaplar,
>   sonra sayfalar. Yeni uç yok, X-14 128 kalır.
> - **CT:** K-7 `PagelessUntil` WAVE-PLANNING girdisi silinir; K-8 §1.2'deki adsız 0180 dosyalarına izin; K-9 AC-43 / 44 / 46 / 47 ve E3 smoke
>   "canlı turda". K-1 (X-13 rol şablonu): Ali'ye ayrı istek; DL-4 kabulü en çok "VERIFIED kod, X-13 kapısı açık".

## ENGEL — en başta okunacak

1. **Kabul kapısı açık (başlama engeli değil): X-13 / Q-20 AuthService rol şablonu WP'si.** Paket §24 DL-4 satırı: "AuthService rol şablonu
   WP'si (Q-20 Ali, X-13) kabul kapısı". Ölçüm (hat ucu `6cdb92e43`): rol şablonu WP'si **yok** (`git grep warehouse.waves services/Diten.AuthService`
   0 eşleşme; `execution/` altında X-13 WP'si yok); Q-20 kararı Ali'de açık (`docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md:15`
   "karar Ali (Auth)"). Sonuç: **DL-4 geliştirmesi başlayabilir, DL-4 kabulü X-13 WP'si kabul edilmeden "VERIFIED" yazılamaz** (en çok
   "VERIFIED kod, X-13 kapısı açık"). Soru §2 K-1.
2. **Kısmi başlama engeli — X-7'nin servis parçası:** `waveId` süzgeci ve `waveSequence` sıralaması için MOD-0178'in iki dosyası
   (`IWarehouseTaskRepository.cs`, `WarehouseTaskRepository.cs`) paket §5.1 X-7 "Dosya" sütununda **adsız**; paket §6 "listede olmayan 0178
   dosyası → dilim durur, XMC-001 ile sahibine sorulur". X-7 commit'i (§3 commit 1) K-2 kapanana dek **başlamaz**; ekran commit'leri bundan
   bağımsız ilerleyebilir.
3. **Paket onayı DL-4'ü kapsar — engel yok.** `execution/approvals/approvals.tsv` MOD-0180 satırı: sha `226ab7f9…cb095`, `approve`, onaylayan
   Ali / `GMG-ERP`, 2026-11-13 (bütün paket; dilim ayrımı yok). `reviews.tsv` r11 satırı `docs/records/reviews/MOD-0180/2026-11-13-226ab7f9.md`
   **UYGUN**. Künye `status: ready-for-dev` (`approval_status: pending` künyede kaldı; onay satırı yazılı — r11 notu "Kalan: … onay satırı" bu
   satırla kapanmış durumda).

---

Tarih: 2026-11-18 · Veren: CT (MVP-5 WMS CT, sahip Beste) · Durum: **verildi — kararlar yukarıda; K-1 kabul kapısı açık**
- Modül: **MOD-0180 — Wave Planning**, servis `Diten.InventoryService` (5067) + `Diten.Web` (5001, yalnız Gateway 5000 üzerinden), kiracı
  modülü. Sahip: geliştirici oturumu.
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0180-wave-planning.md` **r11**, LF sha256
  `226ab7f9f3690c422fba1bf5a3008d004d82791cfe6b20ebb2b74c0cd47cb095` (CT ölçümü 2026-11-18, `git show feature/sce/mod-0173-inventory-ledger-valuation:<paket>`
  → CR 0, sha eşit). Dilim: **DL-4** (paket §23), kanıt seviyesi **E4**.
- DL-3: **VERIFIED kod** — `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md` "Ek — 2026-11-18 CT doğrulaması" → "MOD-0180 DL-3"
  (commit'ler `924620695` … `d52110e14`, B2 `f419641ac`; Açık 1 `6cdb92e43`). Paket §24 "DL-4 kabulü DL-3 kabulünden sonradır" → sağlandı.
- **Canlı tarayıcı turu bu WP'de YAPILMAZ** (sahip kararı: devralmalar bitince tek canlı tur). Kanıt test ve sabotajladır; tarayıcı / canlı
  AC'leri (AC-43, AC-44, AC-46, AC-47, §17 E3 smoke) raporda "canlı turda" diye ayrı satırda kalır (§4).
- Yetki: paket > `domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse paket geçerlidir; çelişkiyi CT'ye yaz, kendin çözme.

## 0. Başlamadan

1. **Dal:** tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation`, ana checkout `C:\Users\Ali\erp-vnext`; **yeni dal açma**.
   `git merge-base --is-ancestor 6cdb92e43 HEAD` → doğru olmalı (DL-3 + Açık 1 hatta). Değilse dur, CT'ye yaz. CT o gün hatta main alırsa ya da
   etiket verirse WP'ye ek yazar.
2. **Paket sha'sı:** `tr -d '\r' < <paket> | sha256sum` → `226ab7f9…cb095`; raporun ilk satırına yaz. Farklıysa **dur**.
3. **Ortam:**
   - Ana checkout'ta derleme / test / sabotaj **yok** — filo Inventory 5067 ana checkout'tan `dotnet watch` ile koşar (düzenleme filoya sıcak
     yüklenir, `bin` kilitlidir). Derleme, filtresiz koşu ve **her sabotaj ayrık (detached) worktree'de** (`git worktree add --detach ..\erp-vnext-wt-0180-dl4 HEAD` + farkı uygula).
   - `INVENTORY_TEST_MONGO` **boş** (takım kendi replica set'ini açar), Mongo **27017 kullanılmaz**;
     `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`.
   - **En fazla 2 tam koşu** her takım için (dilim başı ölçümü + dilim sonu); ara commit'lerde yalnız değişen takım (Inventory dokunulduysa
     Inventory filtresiz — §4). Üçüncü tam koşu gerekirse CT'ye sor.
   - vitest yalnız `C:\Users\Ali\erp-vnext\frontend\Diten.Web\node_modules` ile (ayrık worktree'de PowerShell `New-Item -ItemType Junction`;
     `node_modules\.bin\vitest.cmd run`; `npx vitest` **yok** — paket indirirse koşu sayılmaz). Worktree'yi kaldırmadan önce junction'ı tek başına
     sil (`[System.IO.Directory]::Delete(<yol>, $false)`).
   - `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşulmaz**.
4. **Eşzamanlılık:** dilim başında `git log -5 --oneline -- services/Diten.InventoryService frontend/Diten.Web` ölç, rapora yaz; aynı dosyalarda
   başka dilim sürüyorsa her commit'ten hemen önce hattı al; çakışmada iki tarafın eklemesini koru, CT'ye yaz.
5. **Ortak sayaçlar dilim başında ölçülür** (aşağıdakiler CT'nin son ölçümüdür; DL-4 bunların **hiçbirini değiştirmez** — değişirse dur):
   | Sayaç | Dosya / satır | Son ölçüm | DL-4 beklenen |
   |---|---|---|---|
   | Gateway uç sayısı (X-14) | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` `Assert.Equal(<n>, endpoints.Count)` | **128** (`2a9a21759`) | değişmez — DL-4 servis ucu açmaz (paket §5.1 X-14 "DL-4 … yeni servis ucu açmaz"); 0180 controller'ında bugün 21 uç (`WavesController` 18 + `WaveExceptionsController` 3) |
   | Merkezi denetim yazma komutu (X-10) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` `Assert.Equal(<n>, WriteCommands.Length)` | **151** | değişmez (DL-4 yazma komutu eklemez) |
   | `wms_wave_*` bildirimi (X-9) | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` `WaveCollections.All` | 5 ad | değişmez |
   | Rol İzinleri defteri | `docs/roadmap/backlog/role-permission-label-ledger.md` "Tavan: fiil 0 · satır 96" | — | değişmez; DL-4 yeni fiil getirmez (read / create / update / cancel / release / recall / resolve köprüde var) |
   | Takımlar | Inventory `6cdb92e43` 2662 / 2658 / 0 / 4; Gateway `2a9a21759` 384 / 384; Web.Tests `2a9a21759` 4768 / 4765 / 3 (taban: CapacityPlans, SandopPlans, ReturnDetails); vitest `2a9a21759` 5757 / 5744 / 13 (taban 10 dosya) | | yeni kırmızı 0 |
6. **AC-18 dilim başı:** `git rev-parse HEAD` rapora; dilim sonunda
   `git diff --stat <başı>..<sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse` **boş** olmalı.
7. **Oku (açarak, özetle yetinme):** paket §3 (Frontend rotaları, Sorgular), §4.2, §5 (Web satırları, `PageHelpCatalog.cs`, `WavePlanningL10nAndHelpTests.cs`),
   §5.1 X-7 / X-8, §6 (0178 dosya listesi; X-7 Web DTO), §8.8, §8.10, §8.11, §8.12, §8.15, §9, §10 G-0180-01, §11, §14 (Manifest), §15, §16
   AC-39…AC-50, §17 Frontend, §24 DL-4 satırı, §25. Taslak sözleşme `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` (§3.1 `:113`
   progress, §3.2 `:129` WaveOrder DL-3 eki, §3.5 WaveCandidate, §3.9 `:211-212`, §4.1, §4.2, §5, §9 `:406` dondurma). WMS-BUNDLE 1.2.0
   `listPickTasks` (`docs/analysis/contracts/wms-bundle.openapi.yaml:458-471`). BRIEF `execution/domains/supply-chain-execution/work-packs/mockups/wave-planning/BRIEF-MOD-0180-wave-planning.md`.
   **Kural dosyaları (AGENTS.md §6.1 "Ekran / sayfa yazıyorsan" + "Kullanıcıya görünen metin"):** `.antigravity/rules/frontend-standards.md`,
   `views-organization.md`, `unauthorized-surface-standard.md` (UAS-001), `frontend-js-standard.md`, `premium-modal-standard.md`,
   `frontend-datatable-template.md` (pano listesi, istisna kuyruğu), `frontend-details-template.md` (ayrıntı sayfası), `frontend-form-template.md`
   (oluştur / düzenle ve istisna eylem yan panelleri), `page-help-and-keyboard.md` (UXH-001), `localization-standard.md`,
   `dynamic-localization-standard.md`; daima: `multi-tenancy.md`, `security-jwt.md`, `permission-key-standard.md`, `git-safety.md`,
   `cross-module-change-consent.md` (XMC-001), `code-style.md`, `docs-organization.md`. X-7 servis parçası için ayrıca `repository-standard.md`,
   `mongo-indexing.md`, `api-conventions.md`.
8. **Emsal:** MOD-0181 DL-4 (`e0bf26918`) — `frontend/Diten.Web/Controllers/CycleCountsController.cs`, `CountVariancesController.cs`,
   `Models/Warehouse/CycleCountProxy.cs`, `Views/Warehouse/{CycleCounts,CountVariances}/`, `wwwroot/assets/js/Warehouse/{CycleCounts,CountVariances}/`,
   testler `frontend/Diten.Web.Tests/Warehouse/CycleCountScreensTests.cs` (UAS-001 `:52`, RTL `:168`), `CycleCountL10nAndHelpTests.cs`
   (parite `:107`, nav anahtarları `:158`, yardım `:186`, `:209`, Layout `:223`), `CycleCountProxyControllerTests.cs`.

## 1. Kapsam

### 1.1 Paket DL-4 satırı (§23, §24)
"Ekranlar: pano + ayrıntı, planlayıcı (sürükle-bırak + klavye), istisna kuyruğu; durumlar; 375 px; RTL; 7 dil; yardım". Kapsadığı bölümler: §9,
§11, §8.15, §25. §24 DL-4: seam'ler **X-7, X-8, `PageHelpCatalog.cs`** (eklemeli); AC-39…AC-50; sabotajlar S-39 (`Waves/Index.cshtml`), S-41,
S-42, S-45, S-48, S-49, S-50, S-50b, S-50c. DL-5 (performans `WaveKpis`, `WAVEKPIS`) bu WP'de **yok**.

- **Ekranlar (paket §8.15, §11; rotalar §3):**
  - `/Warehouse/Waves` — **pano**: KPI kartları (bugün serbest bırakılan, açık, riskli, ort. tamamlanma süresi, açık istisna — kaynak
    `GET /waves/dashboard`, `WaveDashboardDto` `WaveModels.cs:146`) + DataTable (`data-dt-standard="v2"`, `data-dt-data-mode="server"`, skeleton)
    + `_Filter` (depo zorunlu, durum, tarih, kesim aralığı, tür, **"riskli olanlar"** — mekanizma §2 K-6) + oluştur / düzenle yan paneli (8 alan,
    "Siparişleri öner") + hızlı görünüm. Satır eylemleri: Aç, Serbest bırak (Taslak), İptal (Taslak, gerekçe), Kalan görevleri geri çek
    (`Released` \| `Picking`, gerekçe). **"Sil" yok** (G-0180-01). Liste sütununda toplama görevi "tamamlanan / toplam" (paket §8.8, BRIEF `:29`)
    — tel şekli §2 K-3.
  - `/Warehouse/Waves/Details/{waveId}` — **ayrıntı** (nav değil, `Details.cshtml` form değildir): kod, durum çizgisi, kesim geri sayımı (depo +
    kullanıcı saati — tarayıcı dilimi farklıysa iki saat, D-3), üç ilerleme (§8.8); sekmeler **Siparişler** (toplama / paketleme / sevk durumu —
    anlamı §2 K-5), **Görevler** (`GET /waves/{id}/tasks`; 0178 Toplama Görevleri'ne `waveId` bağlantısı — X-7), **Yük** (`GET /waves/{id}/load`),
    **Olay geçmişi** (`GET /waves/{id}/events`); "Önerilen yol" (§8.7, `routeSummary` + görev `sequence`).
  - `/Warehouse/WavePlanner` — **planlayıcı** (pano; DataTable değil, Q-19): sol uygun siparişler (`GET /waves/candidates`; ağırlık / hacim "—";
    `eligible = false` / `AllocationPending` satır gri, eklenemez, nedeni yazılı); sağ Taslak dalga kartları (doluluk, kesim, `limitWarnings`);
    sürükle-bırak **ve** "Ekle" düğmesi + klavye (§11: Tab, `Enter` = seçili dalgaya ekle, `Shift+Enter` = hedef seçici, kartta `Delete` = çıkar;
    sonuç `aria-live`'da). 375 px'te salt okunur liste + "Ekle" düğmesi (sürükle-bırak yok).
  - `/Warehouse/WaveExceptions` — **istisna kuyruğu**: DataTable (server): dalga kodu, sipariş, satır, tür, oluşma, durum, çözen; `_CreateEditOffcanvas`
    **yalnız eylem formu** (çıkar / sonraki dalgaya taşı + hedef / yok say + gerekçe 10–500; türe göre izinli eylemler §8.10 tablosu; yaratma yok);
    "Görevi aç (MOD-0178)" ve stok düzeltme ekranına bağlantı (§8.10, stok düzeltme bu ekrandan yapılmaz). `CutoffMissed` satırında sipariş
    sütunu boş ("—"; `outboundId` yok — `WaveModels.cs:117` `WaveExceptionDto`).
- **Her ekranda durumlar (§8.15):** yükleniyor, hata ("Stok servisine ulaşılamadı." — 503), boş ("Bu depoda henüz dalga yok", "Dalgaya girmemiş
  sipariş yok", "Açık istisna yok"), çakışma ("Kayıt değişti, yenileyin." — 409 `WAVE_CONFLICT`), yetkisiz (UAS-001: yalnız tek açıklama,
  iskelet yok, yönlendirme yok). Risk / durum / istisna türü ikon + metin (yalnız renk değil). RTL (`ar`) her ekranda.
- **Web proxy (§11):** `frontend/Diten.Web/Controllers/WavesController.cs` (`[Route("Warehouse/Waves")]`, `Details/{waveId}`),
  `WavePlannerController.cs`, `WaveExceptionsController.cs` — same-origin MVC proxy → **Gateway 5000** `/api/warehouse/waves…` (servise doğrudan
  gitmez); her yazma `[ValidateAntiForgeryToken]`; her proxy isteğine sayfanın `Accept-Language`'ı; `If-Match` ve `Idempotency-Key` iletilir;
  her veri eylemi kendi izin anahtarıyla (MOD-0178 `WarehouseProxyActionKeyTests` emsali — 0180'in kendi testinde). `WaveKpisController` DL-5.
- **Manifest (§14, AC-48):** `WavePlanningManifestProvider.cs:30` `Pages: []` → `WAVES` (nav, `read`; eylemler create / update / cancel / release /
  recall), `WAVEDETAIL` (nav değil, `read`; ayrıntıdaki gerçek düğmeler), `WAVEPLANNER` (nav, `read`; eylem `update`), `WAVEEXCEPTIONS` (nav,
  `exceptions.read`; eylem `exceptions.resolve`). **`ApiPermissions` = yalnız `warehouse.waves.settings.configure`** (AC-48) — bugün
  `ApiPermissions`'ta duran diğer sekiz anahtar sayfa / eyleme taşınır; `delete` / `bulk-delete` yok. 0180 manifest bütünlüğü (manifest =
  `Implemented` = zorlanan, AC-5) değişmeden yeşil. `WAVEKPIS` DL-5.
- **Nav ve yardım (ortak Web kaynakları, yalnız eklemeli):** `SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` → `Nav.Page.WAVES`, `Nav.Page.WAVEPLANNER`,
  `Nav.Page.WAVEEXCEPTIONS` (+ `WAVEDETAIL` nav değil — anahtar gerekiyorsa `check_pack_nav_keys.py` söyler); `Nav.Module.WAVEPLANNING` **var**
  (`SharedResource.en.resx:1344`), `Perm.Entity.WAVES` / `WAVESEXCEPTIONS` / `WAVESSETTINGS` **var** (`:1335`, `:1338`, `:1341`) — değişmez.
  `PageHelpResource.{7}.resx` → §25 `Help.WAVES.*` (Purpose, Audience, Step1..4 + Title, Immediate), `Help.WAVEDETAIL.*` (Step1..3 + not),
  `Help.WAVEPLANNER.*` (Step1..3 + not), `Help.WAVEEXCEPTIONS.*` (Step1..3, Immediate); **tur yok**. `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs`
  → `Related` (`:17`; 0181 satırları `:32-33`) `WAVES` → `WAVEPLANNER`, `WAVEEXCEPTIONS`; `StepIcons` (`:38`; 0181 satırları `:106-108`) `WAVES`,
  `WAVEDETAIL`, `WAVEPLANNER`, `WAVEEXCEPTIONS` — yalnız eklemeli satır.
- **X-7 — 0178 Toplama Görevleri dalga süzgeci (AC-50, Q-12 = A; tel WMS-BUNDLE 1.2.0'da donmuş — `wms-bundle.openapi.yaml:458-471`):**
  - Servis: `INV/Api/Controllers/Warehouse/PickTasksController.cs:30-35` `List`'e `[FromQuery] string? waveId`; `INV/Application/Features/PickTasks/Queries/PickTaskQueries.cs:12`
    `GetPickTaskListQuery`'ye `WaveId`; `:68` beyaz listeye `waveSequence`; **CT kararı S1 (DL-2 kabul kaydı):** `waveId`'siz
    `orderBy=waveSequence` → 400. Depo süzgeci ve sıralama (`WaveRef.WaveId`, `WaveRef.Sequence` artan) — **ENGEL madde 2 / §2 K-2**.
  - Web: `frontend/Diten.Web/Controllers/PickTasksController.cs:118` beyaz listeye `waveId`; `:111` yalnız `waveId` verildiğinde
    `orderBy=waveSequence` (aksi `-createdAt` değişmez); `ListQuery` (`:109`) hem liste (`:54`) hem dışa aktarım (`:79`) içindir — `waveId` ile CSV
    satırları da dalga sırasında, CSV sütunları (`:97`) **değişmez**, CSV'ye "Dalga" sütunu v1'de yok; `Row` (`:332`) eklemeli `wave` alanı;
    `frontend/Diten.Web/Models/Warehouse/OutboundDtos.cs:181` `WmsPickTaskDto` (paketteki `:169` atfı kaydı) eklemeli `Wave { WaveId, WaveNumber, Sequence }`
    (yeni küçük sınıf aynı dosyada); `Views/Warehouse/PickTasks/{_DataTable,_Filter,_IndexL10n}.cshtml` "Dalga" sütunu (`WaveNumber` + sıra) ve
    süzgeç; `wwwroot/assets/js/Warehouse/PickTasks/index.js`; `Resources/Views/Warehouse/PickTasks/PickTasksIndex.{7}.resx` (7 dil).
  - Varsayılan sıralama (`-createdAt`) ve 0178 testleri **değişmez** (`INVT/Warehouse/**`, `frontend/Diten.Web.Tests/Warehouse/WarehouseS2ScreensTests.cs`,
    `WarehouseProxyActionKeyTests.cs`, `frontend/Diten.Web/tests/warehouse-s2-e3-fix-01.test.js`); bunlardan biri kırmızıya dönerse **dur**, CT'ye yaz.
- **Sözleşme işi (DL-3 kabul kaydı sahip kararları 2–4):** yalnız §2'deki kararlar kapandıktan sonra ve kararın söylediği kadar. Karar
  gelmeden bu üç konuda (WaveException outbound üyeleri, `progress` görev sayıları, WaveOrder pick / pack / ship durumları, "Riskli olanlar"
  mekanizması) **tel şekli uydurulmaz**.

### 1.2 İzinli dosyalar (yalnız bunlar; başka dosya gerekirse **dur**, CT'ye sor)

**A — 0180'in kendi dosyaları (yeni / eklemeli):**
| Yol | Not |
|---|---|
| `frontend/Diten.Web/Views/Warehouse/Waves/` — `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, `WavesIndex.cs`, `Details.cshtml` | paket §11; her sayfa görünümünde `Layout = "_LayoutTenantShell";` açık, partial'larda `Layout` yok |
| `frontend/Diten.Web/Views/Warehouse/WavePlanner/` — `Index.cshtml`, `_IndexL10n.cshtml`, `WavePlannerIndex.cs` | §11 |
| `frontend/Diten.Web/Views/Warehouse/WaveExceptions/` — `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, `WaveExceptionsIndex.cs` | §11 |
| `frontend/Diten.Web/wwwroot/assets/js/Warehouse/{Waves,WavePlanner,WaveExceptions}/` — `index.js`, `index.l10n.js`; `Waves/details.js`; `WavePlanner/planner.js` | §11 |
| `frontend/Diten.Web/Resources/Views/Warehouse/{Waves,WavePlanner,WaveExceptions}/` — `{Module}Index.{en,tr,fr,es,zh,ar,ru}.resx` + `Waves/WavesDetails.{7}.resx` | §11; 7 dil |
| `frontend/Diten.Web/Controllers/WavesController.cs`, `WavePlannerController.cs`, `WaveExceptionsController.cs` | §11 |
| `frontend/Diten.Web/Models/Warehouse/WaveProxy*.cs` (gerekirse) ve `frontend/Diten.Web/wwwroot/assets/css/Warehouse/waveplanner.css` (gerekirse) | **§5'te adsız** — 0181 emsali (`CycleCountProxy.cs`, `countsheet.css`); §2 K-8 |
| `frontend/Diten.Web.Tests/Warehouse/WavePlanningL10nAndHelpTests.cs` (paket §5) + `WavePlanningScreensTests.cs`, `WavePlanningProxyControllerTests.cs` (yeni, 0181 emsali) | §2 K-8 (son ikisi adsız) |
| `frontend/Diten.Web/tests/wave-planner-keyboard.test.js` (yeni vitest, jsdom — `frontend/Diten.Web/vitest.config.js` `environment: "jsdom"`) | AC-45; §2 K-8 |
| `services/Diten.InventoryService/src/Diten.InventoryService.Api/WavePlanning/WavePlanningManifestProvider.cs` | `Pages` + `ApiPermissions` daraltma (AC-48) |
| `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/WavePlanning/` | AC-50 servis vakaları (0178 davranışı 0180 ağacında ölçülür, §17), manifest vakaları, (kararlara göre) DTO / risk süzgeci vakaları |
| `services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/Waves/` (`WaveModels.cs`, `Queries/GetWaveListQuery.cs`, ilgili handler'lar), `…/Features/WavePlanning/Services/WaveFacts.cs` | **yalnız §2 K-3 / K-5 / K-6 kararları kapanırsa ve kararın dediği kadar** |

**B — ortak / başka modül dosyaları (XMC-001; yalnız eklemeli):**
| # | Dosya | DL-4 değişikliği | Onay |
|---|---|---|---|
| X-7 (servis) | `INV/Api/Controllers/Warehouse/PickTasksController.cs` (`:30-35`), `INV/Application/Features/PickTasks/Queries/PickTaskQueries.cs` (`:12`, `:68`, `:71-72`) | `waveId` + `waveSequence` (§1.1) | paket §5.1 X-7 (Q-12 = A, karar 03); sahip MOD-0178 (Beste) |
| X-7 (servis, **adsız**) | `INV/Domain/Warehouse/Repositories/IWarehouseTaskRepository.cs` (`WarehouseTaskQuery` `:6-16`), `INV/Persistence/Warehouse/WarehouseTaskRepository.cs` (`ListAsync` `:71`, sıralama `:86-88`) | `WaveId` süzgeci + `WaveRef.Sequence` sıralaması | **AÇIK — §2 K-2** (paket §6: adsız 0178 dosyası → XMC-001 sorusu) |
| X-7 (Web) | `frontend/Diten.Web/Controllers/PickTasksController.cs` (`:109-118`, `:332`), `frontend/Diten.Web/Models/Warehouse/OutboundDtos.cs` (`WmsPickTaskDto` `:181` + yeni dalga alt sınıfı), `Views/Warehouse/PickTasks/{_DataTable,_Filter,_IndexL10n}.cshtml`, `wwwroot/assets/js/Warehouse/PickTasks/index.js`, `Resources/Views/Warehouse/PickTasks/PickTasksIndex.{7}.resx` | §1.1 X-7 Web | paket §5.1 X-7 + §6 (r4 F-4 Web DTO) |
| §5 | `frontend/Diten.Web/Resources/SharedResource.{7}.resx` | `Nav.Page.*` (DL-4) — mevcut anahtar değişmez | paket §5 |
| §5 | `frontend/Diten.Web/Resources/PageHelpResource.{7}.resx` | `Help.WAVES.*`, `Help.WAVEDETAIL.*`, `Help.WAVEPLANNER.*`, `Help.WAVEEXCEPTIONS.*` | paket §5, §25 |
| §5 | `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs` | `Related` + `StepIcons` eklemeli satırlar | paket §5 / §24 (0181 emsali `e0bf26918`) |
| nav bekçisi | `frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs` — **yalnız** `PagelessUntil` (`:189`) içindeki `WAVE-PLANNING` girdisi (`:193`) ve yorumu (`:191-192`) silinir; yerine 0181 biçiminde tek yorum satırı ("MOD-0180 DL-4: WAVE-PLANNING declares its pages … — its entry was deleted") | girdi kendi süresini DL-4'e bağlar ("this entry is deleted then"); `A_pageless_exception_expires_the_moment_its_manifest_declares_a_page` manifest sayfa bildirdiği an girdiyi kırmızıyla siler — **paket §5'te adsız** | girdinin kaydı XMC-001 Beste 2026-11-13 (WP-0180-DL1-FIX-01); silme için **CT teyidi §2 K-7** |

**Değişmez (bağlayıcı):** `INVT/Warehouse/**` (AC-18), 0178 adlı mimari testler, 0178 Web testleri (`WarehouseS2ScreensTests.cs`,
`WarehouseProxyActionKeyTests.cs`, `warehouse-s2-e3-fix-01.test.js`), `InventoryLedgerManifestCompletenessTests.cs` (X-17 yalnız DL-1),
`CentralAuditDeclarationTests.cs` ve `SingleSystemOfRecordArchitectureTests.cs` (DL-4 sayı değiştirmez), `gateway/**` (X-14 dahil — DL-4 uç
eklemez; `Mod0178WarehouseRouteTests.cs` dokunulmaz), `ocelot.json` (yalnız integration-agent; bu WP istemez), Rol İzinleri köprüsü
(`_IndexL10n.cshtml` Governance, `RoleAssignmentsIndex.{7}.resx`, `perm-label.js` — DL-4 yeni fiil getirmez; X-15 DL-4'te yok),
`role-permission-label-ledger.md`, `page-help-ledger.md` (yeni sayfaların yardımı var, deftere satır yazılmaz), `wms-bundle.openapi.yaml`
(1.2.0 donmuş; X-7 teli zaten içinde), `wms-wave-bundle-v1-proposal.md` (CT tek yazıcı — §2 K-3 / K-4), donmuş sözleşmeler,
`.antigravity/**`, `services/Diten.Building.Blocks/**`, `services/Diten.AuthService/**` (X-13 ayrı WP), `_Layout.cshtml` ve
`_LayoutTenantShell` (donuk), `_ViewStart.cshtml`, Archive, MOD-0173 / 0174 / 0175 / 0176 / 0177 / 0181 / 0182 / 0179 dosyaları.
WaveKpis / `WAVEKPIS` / `Help.WAVEKPIS` (DL-5).

### 1.3 Kapsam dışı
DL-5 (performans sayfası, CSV, `WaveKpisController`, `WAVEKPIS`, X-15 KPI satırı). DL-6 (`Mod0180WaveRouteTests`, integration-agent). X-13 rol
şablonu (Ali; ayrı WP). WMS-WAVE-BUNDLE dondurması (CT tek yazıcı adımı — §2 K-4). Depo dalga ayarı ekranı (v1'de yok — FU-0180-SETTINGS-UI).
Canlı tarayıcı turu (tek canlı tur, devralmalar sonrası). Yeni servis ucu (X-14 sayısı değişmez).

## 2. Kararlar ve açık noktalar

**Kapalı (uygulanır):** D-3 (kesim depo saati + kullanıcı saati), D-12 (ağırlık / hacim "—"), D-13 (taşıma hedefi varsayılanı), D-14 (If-Match;
409 → "Kayıt değişti, yenileyin."), D-15 (7 dil), D-16 (performans ayrı sayfa — DL-5), Q-6 = B (ayar ekranı yok, `settings.configure`
`ApiPermissions`), Q-12 = A (X-7), Q-19 (planlayıcı DataTable değil), G-0180-01 (silme yok, satır eylemi "İptal"), X-8 (WMS-BUNDLE 1.2.0 X-7
üyelerini içerir — `wms-bundle.openapi.yaml:458-471`; paket §24 "X-7 üyeleri X-8 sürümünde değilse DL-4 öncesi CT adımı" → **gerekmez**),
CT kararı S1 (`waveId`'siz `orderBy=waveSequence` 400), DL-3 sahip kararı 5 (`Perm.Entity.WAVESEXCEPTIONS`, aile `secondary`).

**Açık — CT'ye / sahibe karar soruları (geliştirici kendisi seçmez; karar gelene dek ilgili parça yazılmaz, raporda "bekliyor"):**

- **K-1 — X-13 rol şablonu (sahip Ali, Q-20).** Paket §24: DL-4 kabul kapısı. Durum: WP yok. **Önerilen:** Ali'ye paket §14 önerisiyle
  (Dalga planlayıcı = read + create + update + cancel + release; Vardiya sorumlusu = read + recall + exceptions.*; Performans izleyici = read +
  kpis.*; `settings.configure` hiçbir şablonda yok) tek AuthService WP'si açılsın; DL-4 kodu beklemeden ilerlesin, kabul kaydı "VERIFIED kod,
  X-13 kapısı açık" yazsın.
- **K-2 — X-7 depo dosyaları (XMC-001, sahip MOD-0178 / Beste).** `waveId` süzgeci ve `waveSequence` sıralaması `WarehouseTaskQuery`
  (`IWarehouseTaskRepository.cs:6-16`) ve `WarehouseTaskRepository.ListAsync` (`:71`, sıralama `:86-88` bugün yalnız `CreatedAt` + `Number`)
  değişmeden yapılamaz; ikisi X-7 "Dosya" sütununda adsız. **Önerilen (A):** iki dosyaya eklemeli `Guid? WaveId = null` ve sıralama seçeneği
  (`WaveSequence` → `WaveRef.Sequence` artan, eşitlikte `Number`; varsayılan dal değişmez); **yeni index yok** v1'de (dalga başına ≤ 2.000 görev,
  paket §8.5 teknik sınır) — geliştirici `explain` ile ölçer, raporlar; büyük veride COLLSCAN görülürse FU satırı. (B) aynı + `wms_tasks`
  üzerinde kısmi index `(TenantId, LegalEntityId, WaveRef.WaveId, WaveRef.Sequence)` — üçüncü 0178 dosyası (`WarehouseIndexConfiguration.cs`) da
  XMC olur.
- **K-3 — `progress` görev sayıları (DL-3 sahip kararı 2).** Paket §8.8 bağlayıcı: "tamamlanan / toplam görev sayısı listede ayrıca"
  (BRIEF `:29`). Bugünkü tel `progress { pickPercent, packPercent, shipPercent, readyToShipCount }` (taslak `:113`, `WaveModels.cs:92`) sayı
  taşımaz. **Önerilen:** CT taslağı DL-4 başında eklemeli günceller — `progress` + `pickTasksDone`, `pickTasksTotal` (int; §8.8 Toplama %'nin pay
  ve paydası, geri çekilenler hariç); geliştirici yalnız DTO + sözleşme testi + liste sütununu yazar. Kim yazar: **CT** (taslak tek yazıcı;
  DL-3 WP §3 "taslakta düzeltme gerekirse CT'ye öner, yazma").
- **K-4 — WaveException `outboundId` / `outboundNumber` isteğe bağlı + dondurma zamanı (DL-3 sahip kararı 2).** Kod zaten isteğe bağlı yazar
  (`WaveModels.cs:117` — `CutoffMissed`'te yok); taslak `:212` zorunlu gösterir. **Önerilen:** CT aynı adımda taslağı `outboundId?`,
  `outboundNumber?` yapar (kod değişmez). **Dondurma:** sahip kararı "DL-4 sözleşme dondurmasında" der; taslak `:406` "1.0.0 DL-5 kabulünden
  sonra" der — çelişki. **Önerilen (A):** 1.0.0 **DL-4 kabulünden sonra** 21 işlemle CT tek yazıcı adımıyla dondurulur, DL-5 KPI işlemleri 1.1.0
  eklemeli gelir (MOD-0181 WMS-COUNT-BUNDLE emsali: 1.0.0 = DL-1…DL-4, DL-5 = 1.1.0; DL-6 AC-55 eşitliği DL-5 sonrası yine sağlanır). (B) taslak
  metni gibi DL-5 sonrası tek dondurma. Her iki seçenekte geliştirici dondurmaz.
- **K-5 — WaveOrder `pickStatus` / `packStatus` / `shipStatus` anlamı (DL-3 sahip kararı 3).** Paket §8.15 Siparişler sekmesinde
  "toplama / paketleme / sevk durumu" ister; üyeler taslakta adıyla var (`:129` "DL-3 eki") ama anlamı tanımsız, kodda yok (`WaveOrderDto`
  `WaveModels.cs:67`). **Önerilen (A):** okuma anında, `Issued` üyelikler için (diğerlerinde üye yazılmaz, kural 4): `pickStatus` =
  `NotStarted` (siparişin dalga görevlerinin hiçbiri başlamamış) \| `InProgress` \| `Done` (hepsi terminal: `Completed` \| `ClosedShort` \|
  `Cancelled`); `packStatus` = `Packed` (outbound `Packed` \| `ReadyToShip` \| `Shipped`) \| `Pending`; `shipStatus` = `Shipped` \| `ReadyToShip`
  \| `Pending` — §8.8'in dalga düzeyi kurallarının sipariş düzeyi karşılığı, 0178'e yalnız `Find`. (B) yeni enum yerine `outboundStatus`
  (0178 değeri olduğu gibi) + `pickTasksDone/Total`; ekran türetir — 0178 enum'unu 0180 teline bağladığı için önerilmez. Taslak değişikliği CT'de
  (K-3 ile aynı adım).
- **K-6 — "Riskli olanlar" süzgeci mekanizması (DL-3 sahip kararı 4).** Süzgecin varlığı paket §8.11 / §11 ile bağlayıcı; mekanizma tanımsız.
  `GetWaveListQuery.cs:7` risk parametresi taşımaz; risk saklanmaz (§8.11), sayfa sunucuda (`data_mode: server`) — istemci süzmesi sayfalamayı bozar.
  **Önerilen (A):** `listWaves`'e eklemeli `atRisk=true` sorgu parametresi (yeni uç yok; X-14 değişmez): sunucu yalnız depodaki `Released` \|
  `Picking` dalgaları okur, riski okuma anında hesaplar (`WaveReconcileRules.AtRisk`), süzer, sonra sayfalar; `atRisk` başka `status` değeriyle
  birlikte gelirse kesişim. Taslak satırı CT'de (K-3 adımı). (B) pano KPI kartından ayrıntı listesine `dashboard` sayısıyla geçiş, süzgeç yok —
  paket metnine uymaz.
- **K-7 — `NavManifestL10nGuardTests.cs` `PagelessUntil` girdisinin silinmesi.** Dosya paket §5'te adsız; girdi 0180 DL-1'de XMC-001 ile
  (Beste 2026-11-13) "DL-4'te silinir" diye yazıldı ve bekçi silinmesini zorlar. **Önerilen:** yalnız `WAVE-PLANNING` girdisi + yorumu silinir,
  0181 biçiminde tek yorum satırı kalır; başka satır değişmez; ayrı sahip onayı gerekmez (DL-1 kaydının kendi süresi) — CT teyit eder.
- **K-8 — Paket §5'te adsız 0180 dosyaları.** `Models/Warehouse/WaveProxy*.cs`, `wwwroot/assets/css/Warehouse/waveplanner.css`,
  `WavePlanningScreensTests.cs`, `WavePlanningProxyControllerTests.cs`, `wave-planner-keyboard.test.js`. Hepsi yeni, 0180'in kendi dosyası (başka
  modül değil). **Önerilen:** izinli (0181 emsali); rapor dosya listesinde adıyla.
- **K-9 — Canlı AC'lerin ertelenmesi.** AC-43 (durumlar gerçek 503 / boş / 409 ile tarayıcıda), AC-44 (375 px `scrollWidth ≤ 375`), AC-46 (iki
  saat), AC-47 (ikon + metin, "—"), §17 E3 smoke — paket bunları "bekçi değil" diye işaretler. **Önerilen:** bu WP'de sunucu çizimi + vitest
  kanıtı (aşağıda §4) yazılır, AC'ler kabul kaydında "canlı turda" açık kalır; DL-4 kabulü "VERIFIED kod, canlı ölçüm açık".

## 3. Kurallar

- **Kiracı:** Web proxy kiracı / şirket bilgisini gövdeye ya da sorguya yazmaz (servis token + `X-Legal-Entity-Id`'den çözer; gövdede / sorguda
  kapsam alanı 400). Başka kiracı / şirketin dalgası, istisnası → 404 (servis) — proxy 404'ü "bulunamadı" durumuna çevirir, yönlendirmez. Sabit /
  varsayılan kiracı yok. Silme yok (G-0180-01).
- **Yetki:** her proxy eylemi `[HasPermission]` (ya da Web'in eylem-anahtar eşlemesi) ile, okuma `read` / `exceptions.read`, yazma kendi anahtarı
  (create / update / cancel / release / recall / exceptions.resolve). Düğme yalnız anahtarı taşıyana çizilir; **UAS-001:** sayfa anahtarı
  olmayana başlık / kart / boş tablo / düğme çizilmez, tek açıklama ("Yöneticinizden yetki isteyin"), yönlendirme yok (paket §9, AC-42).
  `settings.configure` hiçbir ekranda düğme değildir (Q-6 = B).
- **Gateway:** Web yalnız Gateway 5000 üzerinden (`/api/warehouse/waves…`, `/api/warehouse/pick-tasks`); servise (5067) doğrudan çağrı yok; port
  uydurulmaz. `ocelot.json` değişmez (integration-agent'ın; bu WP istemez).
- **Dil (kiracı modülü, 7 dil — en, tr, fr, es, zh, ar, ru):** kullanıcıya görünen hiçbir metin hardcoded değil (cshtml, JS, controller hata
  iletileri dahil); her resx anahtarı 7 dilde, en dışı değerler en'den farklı (marka / kod hariç); eylem metinleri en'de Title Case; hata kodu
  (taslak §5.1 + §5.2'nin kullanılan kodları) ve istisna türü (7 tür, §8.10) metinleri 7 dilde; `ar` sağdan sola (shell `dir`); CSS'te fiziksel
  left / right yok (mantıksal özellikler). Bir dil eksikse iş bitmemiştir. `resx-check` becerisi yalnız `SharedResource` için bilgi verir; sayfa
  resx'leri parite testiyle (AC-41) ölçülür.
- **Layout (§9, AC-39):** `Waves/Index.cshtml`, `Waves/Details.cshtml`, `WavePlanner/Index.cshtml`, `WaveExceptions/Index.cshtml` →
  `Layout = "_LayoutTenantShell";` açık; partial'larda `Layout` yok (MOD-0178 AC-21 bekçisi partial'da `Layout`'u yasaklar). `_Layout.cshtml`
  donuk.
- **UXH-001:** her yan panelde birincil düğme `data-dt-primary="enter"` ya da sonuçlu eylemde (Serbest bırak, İptal, Geri çek, Çıkar / Taşı / Yok
  say) `"chord"` (vitest `diten-enter-primary.test.js`, BL-586 "liste yalnız küçülür"); "?" yardım paneli her sayfada; tur yok; `window.confirm`
  yok (premium modal standardı).
- **DataTable (AC-40):** `python .antigravity/scripts/verify_datatable_page.py . --area Warehouse --module Waves --api-profile proxy --format gaps`
  ve `--module WaveExceptions` PASS (same-origin proxy profili: `const endpoint = '/Warehouse/{Module}/api'`, `headers: getAuthHeaders()`,
  `_IndexL10n`'de `Unknown`). Toplu seçim yoksa `/bulk` gerekmez; dışa aktarım DL-4'te yok — araç sapma verirse **dokunma protokolüyle CT'ye sor**
  (G-0180-01 ve "export yok" sapmaları adıyla).
- **Git:** `main`'e commit / push yok; **push ve PR yok** (modül sonunda ayrı onayla); stage **yalnız açık dosya yoluyla** (`git add -A` /
  `git add .` yok); hook ve imza atlanmaz (`--no-verify` yok); yıkıcı git komutu yok; stash yalnız benzersiz etiketle. Yerel commit serbest;
  iletiler `feat(sce): MOD-0180 DL-4 — …`, X-7 commit'i `feat(sce): MOD-0178 pick task wave filter (MOD-0180 X-7)`.
- **Commit sırası (her commit derlenir; her commit'te değişen takım rapora — tam koşu sınırı §0 madde 3):**
  (1) **X-7 servis** (K-2 kapandıysa): API + sorgu + depo + `INVT/WavePlanning/` AC-50 servis vakaları — Inventory filtresiz.
  (2) **Tel eklemeleri** (yalnız K-3 / K-5 / K-6 kararlarıyla, CT taslağı güncelledikten sonra): DTO + sözleşme testi + birim / entegrasyon
  vakaları — Inventory filtresiz.
  (3) Web proxy controller'ları + `Waves` pano + `Details` + resx 7 dil + L10n / ekran testleri.
  (4) `WavePlanner` + `planner.js` + vitest klavye testi.
  (5) `WaveExceptions` + eylem yan paneli.
  (6) **Tek commit:** manifest `Pages` + `ApiPermissions` daraltması + `Nav.Page.*` 7 dil + `PagelessUntil` girdisinin silinmesi (K-7) +
  `Help.*` 7 dil + `PageHelpCatalog.cs` — nav bekçisi, yardım defteri ve manifest bütünlüğü aynı anda yeşil (Inventory + Web.Tests + vitest).
  (7) **X-7 Web** (PickTasks sütun / süzgeç / DTO / `Row` / resx 7 dil + 0180 Web vakaları).

## 4. Test ve kanıt (AC → test)

| AC | Kanıt (bu WP) | Test (öneri adı; yer) | Sabotaj |
|---|---|---|---|
| AC-39 | 4 sayfa görünümünde `_LayoutTenantShell` açık, partial'larda yok | `WavePlanningL10nAndHelpTests` Layout vakası (emsal `CycleCountL10nAndHelpTests.cs:223`) | **S-39:** `Waves/Index.cshtml`'den `Layout` satırını kaldır → kırmızı |
| AC-40 | araç kontrolü (bekçi değil) | `verify_datatable_page.py … --module Waves` / `WaveExceptions` PASS çıktısı rapora | — |
| AC-41 | 7 dil parite; hata kodu + istisna türü metinleri; Title Case; görünüm / JS / controller'ın okuduğu her anahtar resx'te; `ar` RTL | `WavePlanningL10nAndHelpTests` (emsal `:107`, `:130`, `:143`, `:158`); RTL `WavePlanningScreensTests` (emsal `CycleCountScreensTests.cs:168`) | **S-41:** bir anahtarı `ru`'dan sil → parite kırmızı |
| AC-42 | UAS-001: izinsiz kullanıcıya pano, ayrıntı, planlayıcı, istisna kuyruğunda yalnız açıklama; salt-okur kullanıcıya yazma düğmesi yok | `WavePlanningScreensTests` (emsal `CycleCountScreensTests.cs:52`, `:63`) | **S-42:** izin denetimini kaldırıp iskeleti çiz → kırmızı |
| AC-43 | **canlı turda** (K-9). Bu WP: proxy 503 → hata durumu, boş sayfa → boş metin, 409 → çakışma metni sunucu / JS düzeyinde | `WavePlanningProxyControllerTests` (503 / 409 eşlemesi), vitest durum vakası | — (bekçi değil) |
| AC-44 | **canlı turda** (K-9). Bu WP: CSS'te sabit genişlik / fiziksel yön yok; planlayıcı 375 px'te salt okunur liste + "Ekle" (JS `matchMedia` dalı) | vitest `wave-planner-keyboard.test.js` dar ekran vakası | — |
| AC-45 | yalnız klavyeyle ekle (`Enter`), hedef seç (`Shift+Enter`), çıkar (`Delete`); sürükle-bırak aynı uca (`POST /waves/{id}/orders`, `…/orders/remove`); sonuç `aria-live`; `AllocationPending` satırı gri, eklenemez, nedeni yazılı | vitest `wave-planner-keyboard.test.js` (jsdom; fetch çağrısının URL / yöntem / If-Match / Idempotency-Key'i) | **S-45:** "Ekle" düğmesini kaldır → klavye testi kırmızı |
| AC-46 | **canlı turda** (K-9). Bu WP: kesim gösteriminin iki saat / tek saat dalı birim vakası (saat dilimi enjeksiyonuyla) | vitest | — |
| AC-47 | **canlı turda** (K-9). Bu WP: risk / durum / istisna türü işaretlemesinde ikon + metin; ağırlık / hacim "—" | `WavePlanningScreensTests` işaretleme vakası | — |
| AC-48 | manifest sayfaları `WAVES`, `WAVEDETAIL`, `WAVEPLANNER`, `WAVEEXCEPTIONS` + her gerçek düğme için eylem; `ApiPermissions` = yalnız `settings.configure`; `delete` / `bulk-delete` yok; `python scripts/check_pack_nav_keys.py` PASS; `NavManifestL10nGuardTests` (girdi silinmiş) yeşil | `INVT/WavePlanning/` manifest vakası + 0180 AC-5 bütünlüğü; Web.Tests | **S-48:** manifestte `warehouse.waves.delete` bildir → AC-5 uyum testi kırmızı; CT ek **S-48b:** `PagelessUntil`'e `WAVE-PLANNING` girdisini geri koy → `A_pageless_exception_expires_…` kırmızı |
| AC-49 | §25 yardım metinleri 7 dilde; tur yok; panel sayfanın amaç + adım kartlarını çizer; `page-help-ledger.test.js` yeşil (deftere satır yok) | `WavePlanningL10nAndHelpTests` (emsal `:186`, `:209`) | **S-49:** `PageHelpResource.zh.resx`'ten `Help.WAVES.Step2`'yi sil → kırmızı |
| AC-50 | X-7: "Dalga" sütunu; `waveId` süzgeci (API + Web beyaz listesi); `orderBy=waveSequence` dalga sırası; `waveId`'siz `waveSequence` 400 (S1); Web proxy `waveId` ile dalga sırası; `Row.wave` `WmsPickTaskDto.Wave`'den; `waveId` ile CSV dalga sırasında, sütunlar değişmez; `waveId`'siz CSV `-createdAt`; varsayılan sıralama ve 0178 testleri değişmez | servis: `INVT/WavePlanning/` (gerçek 0178 koduyla serbest bırakılmış dalga fikstürü); Web: 0180 Web testi (`PickTasksController.ListQuery` + `Row` + `Export`) | **S-50:** sıralamayı `Number`'a bağla → dalga sırası kırmızı; **S-50b:** Web beyaz listesinden `waveId`'yi çıkar → proxy vakası süzgeçsiz liste, kırmızı; **S-50c:** `ListQuery`'de `waveId` verilince de `orderBy=-createdAt` yaz → liste ve CSV dalga sırası kırmızı |
| K-3 / K-5 / K-6 | yalnız karar kapanırsa: tel vakası (sözleşme testi taslağa karşı kapalı şema) + davranış vakası | `INVT/WavePlanning/` | karar metniyle birlikte CT ekler |

- **Değişmeden yeşil:** AC-1 (taslak sözleşme testi — taslak CT'de değişirse yeni taslağa karşı), AC-5 (`AuthorizationGuardTests`,
  `WarehouseRegistrationTests`, alt ad alanı vakası, `InventoryLedgerManifestCompletenessTests`), AC-9, AC-12, AC-16, AC-17, AC-18, DL-1…DL-3
  vakaları, Gateway `Mod0178WarehouseRouteTests` (128), `role-assignments-label-ledger.test.js` (tavan değişmez), 0178 Web testleri.
- **Sabotaj kuralı:** her biri ayrık worktree'de — değişiklik + kırmızı test adı + çıktı özeti → **bayt-eşit geri al** (dosya sha256 önce / sonra)
  → yeşil; `git status` temiz; hiçbiri commit'e girmez. Pakette test adı yoksa kırmızıya dönen testin adını yaz.
- **Koşular (en fazla 2 tam koşu / takım):** InventoryService filtresiz (`dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests`,
  `~filter` yok — servis X-7 ve manifestle dokunulur); Web.Tests filtresiz (`dotnet test frontend/Diten.Web.Tests`); Gateway filtresiz
  (`gateway/Diten.ApiGateway.Tests` — sayı değişmediğinin kanıtı); vitest tam paket (`node_modules\.bin\vitest.cmd run`) + hedefli
  `tests/role-assignments-label-ledger.test.js`, `tests/page-help-ledger.test.js`, `tests/diten-enter-primary.test.js`,
  `tests/wave-planner-keyboard.test.js`. Taban kırmızıları adıyla ve aynı gün `main`'de ölçülerek ayrılır (`dialog-one-implementation` Windows'ta
  tabandır).
- **AC-18 kanıtı:** `git diff --stat <başı>..<sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse` **boş**;
  `git diff --stat <başı>..<sonu> -- gateway` **boş**.

## 5. Rapor biçimi

1. Paket sha256 ölçümü; `merge-base` sonucu; dilim başı HEAD; eşzamanlı dilimler (§0 madde 4).
2. §2 kararlarının durumu (hangi karar hangi kayıtla kapandı; kapanmayanlar "bekliyor").
3. Commit listesi (SHA + ileti); her commit'te koşan takım ve sayıları; (6) commit'inde manifest + nav + yardım + `PagelessUntil` birlikte.
4. Sayaçlar önce → sonra (değişmemiş olmalı): `Assert.Equal(<n>, endpoints.Count)`, `WriteCommands.Length`, `WaveCollections.All` — satırın
   kendisiyle atıf.
5. Takım sayıları: Inventory, Web.Tests, Gateway, vitest (önce / sonra; kırmızılar adıyla, taban ayrımı).
6. AC tablosu AC-39…AC-50 — test adı + sonuç; AC-43 / 44 / 46 / 47 "canlı turda" ayrı satırda (bu WP'nin kanıtıyla); K-3 / K-5 / K-6 parçaları
   ayrı satırda.
7. Sabotaj tablosu (S-39, S-41, S-42, S-45, S-48, S-48b, S-49, S-50, S-50b, S-50c; karar ekleri): değişiklik, kırmızı test + çıktı, geri alma
   sha256, yeşil.
8. `verify_datatable_page.py` çıktıları (Waves, WaveExceptions) ve `check_pack_nav_keys.py` çıktısı; sapmalar adıyla.
9. `git diff --stat` kanıtları (AC-18, `gateway`), dokunulan XMC dosyaları listesi (§1.2 B tablosuyla eşleşme).
10. K-2 (A) seçildiyse `explain` ölçümü (`waveId` süzgeci planı).
11. Açık noktalar ve CT'ye sorular (taslak sözleşme düzeltme önerileri dahil).

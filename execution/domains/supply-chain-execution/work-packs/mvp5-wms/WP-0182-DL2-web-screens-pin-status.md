# WP-0182-DL2 — MOD-0182 Barkod / RFID: Web yönetim ekranları + sorumlu PIN durum ucu (DL-2)

- **Kimlik:** WP-0182-DL2 · **Modül:** MOD-0182 Barcode/RFID (supply-chain-execution, `Diten.InventoryService` + `Diten.Web`, kiracı modülü).
- **Paket:** `execution/domains/supply-chain-execution/module-packs/MOD-0182-barcode-rfid.md` **r9**, LF sha256
  `6838d3973246347770f424dd7fc3d75b476dc19f0ea5b00a43cc9403eef0d05d`, `status: ready-for-dev`. Paket dosyası bu dilimde **değişmez** (değişirse onay düşer).
- **Onay satırı:** `execution/approvals/approvals.tsv:99` (commit `8042883fc`), onaylayan Ali (GitHub `GMG-ERP`), yazan `bestepullukcu-alt`, 2026-11-14.
  Bağımsız inceleme r9: `docs/records/reviews/MOD-0182/2026-11-14-6838d397.md` — UYGUN, tek düşük bulgu F-1 (bu WP'nin §7.1 "CT ek şartı" ile kapanır).
- **Önceki dilim:** DL-1 VERIFIED (`3395f5cd7` + FIX-01 `b19765ef2`; kabul kaydı `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md`
  "MOD-0182 DL-1" eki).
- **Sözleşme:** `docs/analysis/contracts/drafts/scan-bundle-v1-proposal.md` — CT 2026-11-14'te §3.13 `SupervisorPinStatus` ve §4.3 `getSupervisorPinStatus`
  eklendi (paket r9'dan birebir; değişiklik notu taslak §10). Bu dilim taslağa **yazmaz**; uyuşmazlık görürse durur, CT'ye yazar.
- **Dilim:** DL-2 (paket §23 DL-2 satırı, §24 DL-2 satırı). **Kanıt seviyesi:** E4 (§23).
- **Sahip:** geliştirici oturumu. **CT doğrulayıcı:** MVP-5 WMS CT (sahip Beste). **Durum:** ready — başlama koşulları §1.2'de (açık XMC kapısı var).
- **Dil:** kullanıcıya görünen her metin 7 dilde (en, tr, fr, es, zh, ar, ru); `ar` sağdan sola. Bir dil eksikse iş bitmemiştir.

## 1. Kararlar ve kapılar

### 1.1 Bu WP'ye yazılan kararlar
| # | Karar | Kaynak |
|---|---|---|
| r8 (1) | `GET /barcode/supervisor-pin` DL-2'de; uç sayısı 33 → 34 | paket künye r8, §3, §8.5 |
| r9 F-1 | Cevaba `lastLockedUntil?` (en son kurulan kilidin bitişi; iz ile kaydın büyüğü); kapalı liste `setAt?`, `expiresAt?`, `lockedUntil?`, `lastLockedUntil?` | sahip kararı `222446efe` (Beste 2026-11-14); paket §8.5 |
| r9 F-2 | X-12'nin DL-2 +1'i sahip onaylı | `222446efe` ("`ad1100158` bu kayıtla DL-2'yi de kapsar") |
| r8 (4) | Pano penceresi son 24 saat (çağrı anından geriye); ekran etiketi "Son 24 saat" | sahip kararı 2026-11-13; paket §8.14, AC-35 |
| CT | SCAN-BUNDLE taslağına DL-2 işlemi CT tarafından eklendi (paket §19.11 son paragraf "CT işi"); geliştirici sözleşme testini genişletir | bu WP, taslak §9 "CT kararları 2026-11-14" |
| CT | AC-37 (g)'ye "kayıtta kilit var, iz yok" vakası + S-37f-WP (§7.1) | r9 incelemesi F-1 |

### 1.2 Açık kapılar (paket §18, §19.4, §24; §5.1 X satırları)

**DL-2'yi BAŞLAMADAN bekleten:**
- **X-10 — sahip onayı (Beste, Web ortak / merkez), dilim başlamadan** (paket §5.1, §18, §24 DL-2 seam sütunu): Rol İzinleri köprüsü
  (`_IndexL10n.cshtml`, `SharedResource.*.resx` `Perm.Entity.*`) — yeni fiil yok, yalnız varlık satır adları. Ölçüm (HEAD `8042883fc`): DL-1 /
  FIX-01 zaten beş satır ekledi (`Perm.Entity.BARCODEDEVICES`, `.BARCODESCANEVENTS`, `.BARCODESCANOVERRIDES`, `.BARCODESCANS`, `.BARCODESETTINGS`;
  `SharedResource.en.resx:1347-1361`). DL-2 bu satırlara ek yapacaksa ya da `_IndexL10n.cshtml`'e dokunacaksa onay kaydı gerekir. Onay yoksa
  **o dosyalara dokunma**, CT'ye yaz.
- **`NavManifestL10nGuardTests` `PagelessUntil` girdisinin silinmesi — paket §5.1'de adı yok (XMC-001 notu, aşağıda §5).** Manifest sayfa
  bildirince `A_pageless_exception_expires_the_moment_its_manifest_declares_a_page` girdiyi sildirir; girdinin kendi yorumu "DL-2 screens …
  this entry is deleted then" der ve DL-1 FIX-01'de Beste onayıyla (2026-11-13) eklendi. Emsal: MOD-0177 R6 ve MOD-0181 DL-4 girdiyi "WP gereği"
  sildi. **CT teyidi X-10 ile birlikte Beste'ye sorulur**; teyit kaydı gelmeden dosyaya dokunulmaz.

**DL-2'yi başlamadan bekletmeyen, kabulü (ACCEPTED) bekleten:**
- **Q-14 rol şablonları (Ali)** — paket §24 DL-2 "kabul kapısı"; X-11 (`services/Diten.AuthService/**`) ayrı WP, bu dilim dokunmaz.

**DL-2'yi etkilemeyen (bilgi):**
- **Q-13** (mobil şirket bağlamı `legal_entities` + `X-Legal-Entity-Id`, Ali) — mobil akış; Web proxy şirket başlığını bugünkü gibi iletir.
- **X-2, X-3, X-14** — DL-3 (r9 metniyle onay); **X-13** — DL-5; **X-11** — ayrı WP. Bu dilimde dokunulmaz.
- **Q-7** donanım (DL-6), MOD-0290-FU07 (DL-5), taslak §9 açık nokta 12 (`resolved.lineId`, CT / mobil).

## 2. Amaç ve kapsam

**Amaç (§23 DL-2):** Web yönetim ekranları — okutma olayları, cihaz sağlığı, ayarlar, sorumlu PIN'i; durumlar; 375 px; RTL; 7 dil; yardım; ve
sorumlunun PIN durum okuma ucu `GET /barcode/supervisor-pin` (servis).

**Kapsam (paket bölümleri):** §8.5 (PIN durumunu okuma, görünürlük), §8.14 (ilk dört satır), §9, §11 (ilk dört klasör + proxy + ortak kaynaklar),
§12 (If-Match muafiyeti satırı), §13 (r8 / r9 PIN durum vakaları), §14 (manifest sayfaları `SCANEVENTS`, `SCANDEVICES`, `BARCODESETTINGS`,
`SUPERVISORPIN`), §15, §25 (dört sayfanın yardımı).

**Servis (Inventory) değişikliği — yalnız bu kadar:**
- Sorgu `GetSupervisorPinStatusQuery` (`sealed record`, `Features/BarcodeScans/Queries/`) + `Handlers/QueryHandlers/GetSupervisorPinStatusHandler.cs`
  (soneksiz) + cevap DTO'su `BarcodeScanModels.cs`'te (paket §10).
- Eylem `[HttpGet("barcode/supervisor-pin")]` + `[HasPermission(BarcodePermissions.ScanOverridesApprove)]` — `BarcodeScansController.cs`
  (`POST barcode/supervisor-pin` bugün `:53`). Öznitelik ek argümansız (X-12 regex'i).
- Depo okuması `BarcodeRepository`'de: kendi PIN kaydı (kiracı + şirket + JWT `UserId`; `Company<T>()` süzgeci `IsDeleted` dahil) ve en son `LockSet`
  izi (`wms_barcode_override_attempts`, süzgeç kiracı + şirket + `SupervisorUserId` = JWT `UserId` + `Result = LockSet`, en yeni `At`; var olan
  `ix_wms_barcode_attempt_supervisor_at` index'i — yeni index yok). Yalnız `Find`.
- **Yeni yazma komutu yok, işlem çağrı yeri yok** → X-6 (a) / (b) sayıları değişmez (paket §5.1 X-6: "DL-2 … 0"). Manifest: dört sayfa (§14) —
  yeni izin anahtarı yok.

**Kapsam DIŞI:** DL-3 (`scanRef` kapısı, X-2 / X-3 / X-14, AC-66), DL-4 ekranları (`LabelTemplates`, `LabelPrints`), DL-7 (`ScanTrace`), mobil kod,
Auth rol şablonu (X-11), `.antigravity/**`, `ocelot.json`, `Mod0182BarcodeRouteTests` (DL-8).

## 3. Adım 0 — hattı al

Ana checkout'ta, çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` üzerinde (yeni dal açılmaz):

```bash
git checkout feature/sce/mod-0173-inventory-ledger-valuation
git merge-base --is-ancestor ct/line-ff-2026-11-14c HEAD && echo "ata — devam"
# ata değilse:
git merge --ff-only ct/line-ff-2026-11-14c
# ff olmazsa:
git merge --no-ff ct/line-ff-2026-11-14c
# çakışmada: git merge --abort, dur, CT'ye bildir (çakışmayı kendin çözme)
```

- Etiketi CT oluşturur; etiket yoksa dur, CT'ye sor.
- Sonra doğrula: `git merge-base --is-ancestor 8042883fc HEAD` çıkış 0; paket LF sha256 yukarıdakiyle aynı
  (`git show HEAD:<paket> | sha256sum` — CRLF dönüşümü olmadan). Değilse dur.
- Dilim başı commit'ini (`<dilim-başı>`) kaydet; bütün "önce" ölçümleri bu commit'te, aynı gün alınır:
  - Gateway `Mod0178WarehouseRouteTests.cs` `Assert.Equal(<n>, endpoints.Count)` — HEAD `8042883fc`'de `:113` **105** (son değişiklik `87ec462e7`,
    MOD-0180 DL-2). Arada başka uç eklenmezse DL-2 sonrası **106** beklenir (çıkarım); taban **ölçülür**.
  - `CentralAuditDeclarationTests` `WriteCommands.Length` ve `seen` — DL-2'de **değişmemeli** (önce = sonra).
  - `NavManifestL10nGuardTests.NavVisiblePageKeyFloor` (bugün 45) ve `PagelessUntil` girdileri.
  - Inventory, Web.Tests, Gateway filtresiz taban (kırmızılar adıyla — §8).

## 4. İlk adımlar (kod yazmadan önce)

### 4.1 XMC-001 kayıt denetimi
- X-12 (DL-2 +1): onaylı `222446efe`. X-10 ve `PagelessUntil` silmesi: §1.2 — onay kaydı yoksa dokunma.
- Bu listede olmayan başka modül dosyası gerekirse **dur**, CT'ye yaz (XMC-001). Onay kayıtları teslim raporuna yazılır.

### 4.2 Sözleşme önce
- Uç, taslak §3.13 / §4.3'e karşı yazılır. `INVT/Barcode/BarcodeDraftContract.cs` (DL-1 sözleşme testi) `getSupervisorPinStatus` ile genişletilir:
  kapalı şema (bilinmeyen üye kırmızı), dört isteğe bağlı üye + `contractVersion` (paket §17 r8 / r9). DL-1'in 16 işlemi değişmez.

### 4.3 Ekran ölçümü (dokunma protokolü)
- Liste ekranları için `python3 .antigravity/scripts/verify_datatable_page.py --area Warehouse --module ScanEvents --format gaps` ve
  `--module ScanDevices` (AC-32). G-0182-01 dışındaki sapmayı kendin "düzeltip" geçme — sahibine sor.
- Yeni kiracı ekranı bekçi tuhaflıkları (kayıtlı): `verify_datatable --api-profile proxy`, `/bulk`, sunucu tarafı dışa aktarım; vitest BL-586
  `data-dt-primary` offcanvas'larda.

## 5. Dokunulacak dosyalar

`INV` = `services/Diten.InventoryService/src/Diten.InventoryService.`, `INVT` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`,
`WEB` = `frontend/Diten.Web/`, `WEBT` = `frontend/Diten.Web.Tests/` (paket §5).

**0182'nin kendi dosyaları:**
- `INV/Application/Features/BarcodeScans/` — `Queries/GetSupervisorPinStatusQuery.cs`, `Handlers/QueryHandlers/GetSupervisorPinStatusHandler.cs`,
  `BarcodeScanModels.cs` (cevap DTO'su; sır alanı **tipte yok**).
- `INV/Persistence/Barcode/BarcodeRepository.cs` (iki okuma, yalnız `Find`).
- `INV/Api/Controllers/Barcode/BarcodeScansController.cs` (bir GET eylemi).
- `INV/Api/Barcode/BarcodeRfidManifestProvider.cs` (dört sayfa: `SCANEVENTS` `scan-events.read`; `SCANDEVICES` `devices.read` + eylemler
  create / update / activate / deactivate; `BARCODESETTINGS` `settings.read` + `configure`; `SUPERVISORPIN` `scan-overrides.approve` — §14).
- `INVT/Barcode/` (PIN durum vakaları, sözleşme testi eki, AC-35 24 saat vakası servis ucuna karşı).
- `WEB/Views/Warehouse/{ScanEvents,ScanDevices,BarcodeSettings,SupervisorPin}/` — §11 dosya listesi birebir (ScanEvents'te `_CreateEditOffcanvas`
  **yok** — G-0182-01; BarcodeSettings ve SupervisorPin DataTable değil); her sayfa görünümünde `Layout = "_LayoutTenantShell";` açıkça.
- `WEB/Controllers/{ScanEvents,ScanDevices,BarcodeSettings,SupervisorPin}Controller.cs` — same-origin MVC proxy → Gateway (5000)
  `/api/warehouse/barcode…`; her yazma `[ValidateAntiForgeryToken]`; `Accept-Language`, `If-Match`, `Idempotency-Key` iletilir; Web kaydı her zaman
  `keyDelivery = Pairing`; **PIN gövdesi loglanmaz**. Web servise doğrudan (5067) gitmez.
- `WEB/wwwroot/assets/js/Warehouse/{Module}/index.js`, `index.l10n.js`.
- `WEB/Resources/Views/Warehouse/{Module}/{Module}Index.{en,tr,fr,es,zh,ar,ru}.resx` (marker sınıfı `{Module}Index.cs` ile birebir).
- `WEBT/Warehouse/BarcodeL10nAndHelpTests.cs` (yeni — L10n / Layout / yardım / UAS-001 testi).

**Ortak Web kaynakları — yalnız eklemeli (paket §5 tablosu, DL-2):**
- `WEB/Resources/SharedResource.{7}.resx`: `Nav.Page.SCANEVENTS`, `Nav.Page.SCANDEVICES`, `Nav.Page.BARCODESETTINGS`, `Nav.Page.SUPERVISORPIN`
  (7 dilde; `Nav.Module.BARCODERFID` DL-1'de var — ölç, eksik dil varsa ekle). `Perm.Entity.*` satırları **X-10** (§1.2).
- `WEB/Resources/PageHelpResource.{7}.resx` + `WEB/Services/Help/PageHelpCatalog.cs`: dört sayfanın `Help.*` anahtarları (§25).

**XMC-001 seam'leri (yalnız adlı satırlar):**
| # | Dosya | Değişiklik | Sayı | Sahip / onay |
|---|---|---|---|---|
| X-12 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — **yalnız** `Assert.Equal(<n>, endpoints.Count)` (HEAD `:113`) ve üstündeki uç yorumu (`:80-112`) | dilim başı ölçülen + **1**, yorumda "MOD-0182 DL-2: GET /barcode/supervisor-pin"; **uçla aynı commit** | +1 (beklenen 105 → 106, çıkarım) | onaylı `ad1100158` + `222446efe`; **dosyayı integration-agent değiştirir** (§6 Adım F) |
| X-10 | `WEB/Views/Governance/RoleAssignments/_IndexL10n.cshtml`, `SharedResource.*.resx` `Perm.Entity.*` | yalnız varlık satır adları, 7 dil; yeni fiil yok | gerekirse | **açık — Beste, dilim başlamadan** |
| (pakette adsız) | `WEBT/Navigation/NavManifestL10nGuardTests.cs` `PagelessUntil` | yalnız `["BARCODE-RFID"]` girdisi ve iki satırlık yorumu silinir; `NavVisiblePageKeyFloor` ve başka satır değişmez | −1 girdi | girdiyi açan onay (Beste 2026-11-13, FIX-01) — **CT teyidi §1.2** |

**Dokunulmayacaklar:** paket dosyası; SCAN-BUNDLE taslağı; `.antigravity/**`; `ocelot.json`; `_Layout.cshtml`, Archive; `services/Diten.Building.Blocks/**`;
`services/Diten.AuthService/**` (X-11); `INVT/Warehouse/**` ve `INVT/CycleCount/**` (hiçbir satır — AC-41 kanıtı her dilimde); `INVT/CentralAudit/**`,
`INVT/Architecture/**`, `INVT/Api/InventoryLedgerManifestCompletenessTests.cs` (DL-2'de sayı değişmez); MOD-0173…0181 kodu; bu WP dosyası.

## 6. Uygulama sırası (öneri)

- **A.** Adım 0 → §4.1 → taban koşuları.
- **B.** Servis: sorgu + depo okumaları + eylem; sözleşme testi eki; AC-37 (a)–(g) + CT ek vakası (§7.1).
- **C.** Manifest sayfaları + `Nav.Page.*` 7 dil + `PagelessUntil` silmesi (teyitle) — **aynı commit** (manifest sayfası gelip anahtar / girdi
  gelmezse Web.Tests kırmızı; MOD-0180 / 0182 / 0049'da tekrar eden kırmızı).
- **D.** Web ekranları: proxy controller'lar → görünümler (Golden Slim) → JS → resx (7 dil) → yardım. Durumlar (boş / yükleniyor / hata / yetkisiz).
- **E.** `/Warehouse/SupervisorPin`: PIN formu (`autocomplete="off"`), son kullanma tarihi, süren kilit bitişi (yalnız `lockedUntil` varsa), son kilit
  zamanı (yalnız `lastLockedUntil` varsa), "süresi doldu" durumu — hepsi `GET /barcode/supervisor-pin`'den.
- **F. Gateway sayısı (X-12) — integration-agent adımı:** geliştirici ölçülen önce sayıyı ve tek uç adını integration-agent alt ajanına verir; o
  yalnız sayı satırını ve yorumu düzenler; değişiklik GET eylemiyle **aynı commit'e** stage edilir. Geliştirici dosyayı kendisi düzenlemez.
  Kabul: `git diff <dilim-başı>..<dilim-sonu> -- gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` yalnız sayı satırı + yorum.
- **G.** Testler, sabotajlar, tam koşular, tarayıcı ölçümü (AC-38), teslim raporu.

## 7. Kabul ölçütleri (DL-2: AC-31…AC-40 + AC-65 0178 sayı vakası + CT ek şartı)

Ayrıntı paket §16'dadır; her vaka paketteki metinle ölçülür (özet yetmez — aç).

| AC | Kısa başlık | Sabotajlar |
|---|---|---|
| AC-31 | Her 0182 sayfa görünümünde `Layout = "_LayoutTenantShell";` açıkça; test 0182 görünümlerinin tamamını tarar | S-31 |
| AC-32 | `verify_datatable_page.py` ScanEvents / ScanDevices çıktısı kabul kaydında; sapma G-0182-01 dışında 0 ya da sorulmuş | — (araç; bekçi değil) |
| AC-33 | Her sayfa resx'i 7 dilde aynı anahtar kümesi; `Nav.Module.BARCODERFID` / `Nav.Page.*` (`scripts/check_pack_nav_keys.py`); Arapça RTL | S-33 |
| AC-34 | UAS-001: izinsiz kullanıcıya iskelet yok, yönlendirme yok, tek açıklama; eylem düğmeleri izne göre | S-34 |
| AC-35 | Okutma olayları: depo zorunlu, ≤ 31 gün (fazlası 400); KPI son 24 saat (sahte saat, `from` / `to`); GUID yok; "çevrimdışı bekleyen" kartı yok; etiket "Son 24 saat" 7 dil | S-35, S-35b |
| AC-36 | Cihazlar: Sil yok; Engelle gerekçe ister; rozet ikon + metin; anahtar hiç gösterilmez — eşleştirme QR'ı + kod + kalan süre bir kez | S-36 |
| AC-37 | Ayar formu If-Match; PIN sayfası yalnız kendi, `autocomplete="off"`, proxy PIN loglamaz; PIN durum ucu (a)–(g); **CT ek vakası §7.1** | S-37, S-37b, S-37c, S-37d, S-37e, **S-37f-WP** |
| AC-38 | 375 px, yatay kaydırma yok; boş / yükleniyor / hata / yetkisiz (tarayıcı ölçümü; en / tr / ar) | — (ölçüm; bekçi değil) |
| AC-39 | Dört sayfanın yardımı (§25) 7 dilde; tur yok | S-39 |
| AC-40 | Rol İzinleri vitest `tests/role-assignments-label-ledger.test.js` yeşil; 0182 satır adları 7 dil (X-10) | S-40 |
| AC-65 (0178 sayı vakası) | `Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route` dilim başı + 1 ile yeşil; yalnız sayı satırı + yorum (X-12) | S-65c |

**AC-37 servis vakaları (paket metni, kısaltılmadan uygulanır):** (a) PIN'i olan sorumlu → 200, `setAt` / `expiresAt` kayıttakiyle aynı; kilit yokken
`lockedUntil` yok, kilitliyken `SupervisorPin.LockedUntil`'e eşit. (b) Aynı şirkette başkasının PIN'i varken kendi kaydı olmayan sorumlu → 200, dört
üye yok; başka şirkette kaydı olan kullanıcı bu şirkette → dört üye yok. (c) Kapalı anahtar listesi (`pin`, `pinHash`, `salt`, `iterations`,
`failedAttempts`, `remainingAttempts`, `version` yok). (d) `scan-overrides.approve` olmadan 403 (`scan-events.read` taşıyanda dahi). (e) Merkezi kayıt
ve idempotency kaydı yazılmaz. (f) PIN sayfası son kullanma tarihini, kilit bitişini ve son kilit zamanını bu uçtan gösterir (Web testi). (g) Hiç kilit
yok → `lastLockedUntil` yok; gerçek aşım ucuyla yanlış PIN'lerle kilit → `lastLockedUntil = lockedUntil = SupervisorPin.LockedUntil`; sahte saatle kilit
bitti → `lockedUntil` yok, `lastLockedUntil` aynı; PIN yeniden belirlendi → yine aynı; ikinci kilit → ikinci kilidin bitişi; başka sorumlunun daha yeni
kilidi → çağıranın değeri değişmez; başka şirketteki kendi kilidi dönmez.

### 7.1 CT ek şartı (paket dışı — r9 incelemesi F-1'i kapatır)

Kaynak: `docs/records/reviews/MOD-0182/2026-11-14-6838d397.md` F-1 (DÜŞÜK, sahibi Beste / CT, tarih 2026-11-17). Paket §8.5 `lastLockedUntil`'i
"izdeki `LockedUntil` ile kayıttaki `SupervisorPin.LockedUntil`'in **büyüğü**" diye tanımlar (gerekçe: kilit `LockPinAsync` ile kurulup
`InsertAttemptAsync` 503 ile düşerse kayıtta kilit var, `LockSet` izi yok). AC-37 (g)'nin bütün vakalarında iz yazılır; yalnız izi okuyan gerçekleme
de yeşil geçer, S-37d / S-37e bu dalı bozmaz.

- **AC-37 (g) ek vakası — "kayıtta kilit var, iz yok":** test kurulumu çağıranın `SupervisorPin` kaydına gelecekte bir `LockedUntil` yazar (doğrudan
  depo / test yardımcısı; `LockSet` izi **yazılmaz**) → `GET /barcode/supervisor-pin` → `lockedUntil` = o değer **ve** `lastLockedUntil` = o değer.
  İkinci kol (önerilir): çağıranın daha eski bir `LockSet` izi varken kayıttaki daha yeni kilit → `lastLockedUntil` kayıttaki değer.
- **S-37f-WP:** üretim okumasında "büyüğü alma" kuralını kaldır, yalnız izdeki değeri dön → ek vaka **kırmızı**; bayt-eşit geri al → yeşil.
  (S-37d ve S-37e ayrıca koşar; ek vaka onların yerine geçmez.)
- Rapor bu vakayı ve S-37f-WP'yi kendi satırında, inceleme kaydına atıfla yazar. Paket metni değişmez; CT inceleme kaydında F-1'i bu kabul
  kaydıyla kapatır.

**Sabotaj listesi (§24 DL-2 satırı + CT ek şartı, tam):** S-31, S-33, S-34, S-35, S-35b, S-36, S-37, S-37b, S-37c, S-37d, S-37e, S-39, S-40, S-65c
(**14 paket sabotajı**) + S-37f-WP (**1 CT ek**) = **15 sabotaj**.

**Beklenen kırmızı test adları (pakette / repoda adı geçenler):** S-65c → `Mod0178WarehouseRouteTests.Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route`
(geçici `[HttpGet("barcode/count-sabotage")]` + `[HasPermission(BarcodePermissions.ScanEventsRead)]`); S-40 → vitest `role-assignments-label-ledger`;
S-33 (Nav anahtarı) → `NavManifestL10nGuardTests` ve / veya `BarcodeL10nAndHelpTests`. Diğerlerinin test adı pakette yok — geliştirici adlandırır ve
raporda her `S-n` ile eşler.

Kurallar: her sabotaj **üretim koduna** uygulanır (kendi kopyasıyla ölçen test guard değildir — Çekirdek md. 10); kırmızı çıktı alınır, bozma bayt-eşit
geri alınır, **commit'e girmez**; listede olmayan her kırmızı adıyla raporlanır.

## 8. Test koşuları (tam koşu filtresiz)

- **InventoryService, filtre yok:** `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests`. Taban `<dilim-başı>`'nda aynı gün;
  sonda yeni kırmızı yok. Bilinen yük duyarlı süre testleri (`BarcodeScanTests.Two_hundred_consecutive_scans_stay_under_the_p95_bound`,
  `CancelReleaseTests.AC32_…`): kırmızıysa tek başına iki kez koşup sonucu yaz (FU-0182-P95).
- **Web.Tests, filtre yok:** `dotnet test frontend/Diten.Web.Tests`. Taban `b19765ef2`'de 4554 / 3 kırmızı (üçü main'de de kırmızı); `<dilim-başı>`'nda
  yeniden ölçülür. `NavManifestL10nGuardTests` 8/8 yeşil olmalı.
- **Gateway, filtre yok:** `dotnet test gateway/Diten.ApiGateway.Tests` (AC-65 0178 sayı vakası önce / sonra).
- **vitest — yalnız repodaki `node_modules`** (`frontend/Diten.Web/node_modules`; `npx`'in npm'den paket indirmesine izin verme — o koşu sayılmaz):
  `frontend/Diten.Web` içinde `node_modules/.bin/vitest run tests/role-assignments-label-ledger.test.js` (AC-40) ve dilimin dokunduğu Web JS testleri.
  Ayrık worktree'de koşuluyorsa `node_modules` için kayıtlı junction yöntemi.
- **Araçlar:** `verify_datatable_page.py` (AC-32), `scripts/check_pack_nav_keys.py` (AC-33), `resx-check` (SharedResource 7 dil, kiracı bağlamı).
- **Bekçi kanıtı:** `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount` **boş**.
- Build: `Diten.InventoryService`, `Diten.Web` PASS.

**Ortam:**
- Port **27017 kullanma**. **`INVENTORY_TEST_MONGO` boş bırak** — paket kendi replika setini başlatır.
- **`Diten.AuthService.AccountKindAcceptanceHost.Tests` koşma.**
- Portlar yalnız AGENTS.md §3'ten (Web 5001, Gateway 5000, Inventory 5067); port uydurma.
- Ana checkout'ta Inventory 5067 `dotnet watch` ile çalışıyorsa derleme kilidi olur; derleme / sabotaj ayrık worktree'de yapılabilir — commit yine
  çalışma dalına.

## 9. Git kuralları (GIT-002)

- Çalışma dalı **`feature/sce/mod-0173-inventory-ledger-valuation`** — yeni dal açılmaz; `main`'e commit / push yok.
- Stage yalnız **açık dosya yoluyla** (`git add <yol> …`); `git add -A` / `git add .` yok. İzlenmeyen `__pycache__/` ve review ham dosyaları stage'e girmez.
- Hook atlama yok (`--no-verify` yok), imza atlama yok; yıkıcı git komutu (`reset --hard`, `push --force`, `checkout --`) onaysız yok.
- Yerel commit'ler serbest; X-12 GET eylemiyle, manifest sayfaları `Nav.Page.*` anahtarları ve `PagelessUntil` silmesiyle **aynı commit'te**.
- **Push / PR yok.** Commit mesajı sonu: `Co-Authored-By` satırı oturumun atıf kuralıyla.

## 10. Teslim raporu biçimi

1. **Commit listesi** (`git log --oneline <dilim-başı>..HEAD`), `git diff --stat`, Adım 0'da hangi yol (ata / ff / no-ff).
2. **Test sayıları önce / sonra:** Inventory, Web.Tests, Gateway (toplam / geçen / kalan / atlanan; kırmızılar adıyla), vitest.
3. **Sabotaj tablosu:** her `S-n` (15) için — üretim dosyası:satır, komut, kırmızı test adı + çıktı özeti, geri alma, geri yeşil kanıtı.
4. **Sayılar önce → sonra:** Gateway `Assert.Equal(<n>, endpoints.Count)` (beklenen 105 → 106), `WriteCommands.Length` ve `seen` (değişmedi),
   `NavVisiblePageKeyFloor` / `PagelessUntil` — satıra atıfla, ölçüldüğü commit ile.
5. **SCAN-BUNDLE:** sözleşme testinde `getSupervisorPinStatus` vakasının adı; taslakla uyuşmazlık (varsa).
6. **CT ek şartı:** AC-37 (g) "kayıtta kilit var, iz yok" vaka adı + S-37f-WP satırı (inceleme F-1'e atıf).
7. **Araç çıktıları:** `verify_datatable_page.py` (ScanEvents, ScanDevices), `check_pack_nav_keys.py`, `resx-check`.
8. **AC-38 tarayıcı ölçümü:** dört sayfa × en / tr / ar × 375 px (genişlik, yatay kaydırma) + dört durum.
9. **XMC-001:** X-10 ve `PagelessUntil` teyit kaydı (commit) ya da "onay yok — dokunulmadı"; `INVT/Warehouse/**` / `INVT/CycleCount/**` boş diff.
10. **Açık kalanlar:** sorular, ölçülemeyenler (nedeniyle), sonraki dilime devredilenler; Q-14 (kabul kapısı) durumu.

## 11. Kural haritası (AGENTS.md §6.1)

- **Daima:** `multi-tenancy`, `security-jwt`, `git-safety`, `code-style`, `docs-organization`, `cross-module-change-consent` (XMC-001).
- **Backend:** `handler-design`, `repository-standard`, `response-envelope`, `api-conventions`, `routes`, `ports`, `mongo-indexing` (yeni index yok — ölç).
- **Frontend:** `frontend-datatable-template`, `quality-gate-datatable`, `unauthorized-surface-standard` (UAS-001), `page-help-and-keyboard` (UXH-001),
  frontend layout / shell kuralları (`_LayoutTenantShell`), proxy deseni.
- **Metin:** `localization-standard` (kiracı 7 dil, marker sınıfı yolu, RTL mantıksal CSS), `dynamic-localization-standard` (`window.L10n` köprüsü).
- **Yetki / manifest:** `permission-key-standard` (yeni anahtar yok), `module-self-registration-standard`, `business-module-enforcement-standard`.
- **Kayıt:** `logging-observability` (PIN gövdesi loglanmaz), `status-reporting-and-evidence` (REP-001), `dev-runbook`.

## 12. Belirsizlikler (CT'ye; geliştirici kendi başına çözmez)

- **B-1 (açık — Beste):** X-10 onayı ve `PagelessUntil` silme teyidi (§1.2). İkisi gelmeden ilgili dosyalara dokunulmaz; servis işi (Adım B) beklemeden başlayabilir.
- **B-2 (CT kararı):** X-12 satırını integration-agent yazar (Adım F); geliştirici dosyayı düzenlemez.
- **B-3 (CT kararı):** SCAN-BUNDLE taslağının DL-2 eki CT'nindir (2026-11-14); geliştirici taslağa yazmaz, testini taslağa göre yazar.
- **B-4 (ölçülmedi):** Modülün menüde görünmesi kiracının planına / rol izinlerine bağlıdır; Q-14 açıkken test kiracısında izinler elle verilir
  (canlı tur CT'de). Rol şablonu bu dilimin işi değildir.

## Ek — B-1 kapandı (CT, 2026-11-14)

Sahip (Beste) X-10'u ve `PagelessUntil["BARCODE-RFID"]` girdisinin silinmesini onayladı: karar kaydı
`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md` "Ek — MOD-0182 DL-2 XMC-001". §1.2'deki "onay kaydı gelmeden
dokunulmaz" şartı bu kayıtla karşılandı; kapsam yukarıdaki tablo satırlarıyla sınırlıdır. Rapor madde 9'da bu karar commit'ine atıf yapılır.

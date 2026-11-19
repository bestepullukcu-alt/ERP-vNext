# WP-0180-DL1 — MOD-0180 Dalga Planlama: WMS-WAVE-BUNDLE taslağı + dalga çekirdeği (arka uç)

- Kimlik: **WP-0180-DL1**. Modül: **MOD-0180 — Wave Planning (Dalga Planlama)**, servis `Diten.InventoryService` (5067), kiracı modülü.
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0180-wave-planning.md` — **r11**, LF sha256
  `226ab7f9f3690c422fba1bf5a3008d004d82791cfe6b20ebb2b74c0cd47cb095`, `status: ready-for-dev`.
- Onay: `execution/approvals/approvals.tsv` satırı (onaylayan Ali / `GMG-ERP`, 2026-11-13, yazan `bestepullukcu-alt`) — commit `e12bf26c8`.
  Not: paket künyesindeki `approval_status: pending` ve `status_note`'taki "onay satırı bekler" ifadesi künyenin onaydan önceki halidir;
  yetki kaynağı onay satırıdır (MOD-0178 ve MOD-0181 künyeleri de aynı biçimdedir). Kod izni bu satırla vardır.
  Bağımsız inceleme r11 UYGUN: `docs/records/reviews/MOD-0180/2026-11-13-226ab7f9.md` (commit `fa213e0b7`, `reviews.tsv` `350768bae`).
- Dilim: **DL-1** (paket §23). Kanıt seviyesi: **E4** (§23 DL-1 satırı).
- Sahip: geliştirici oturumu. CT doğrulayıcı: **MVP-5 WMS CT** (sahip Beste). Gateway sayı adımı: **integration-agent** (X-14).
- Durum: **ready**. Bu WP push, PR ya da `.antigravity/**` değişikliği izni değildir.
- Yetki: paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse paket
  geçerlidir; çelişkiyi CT'ye yaz, kendin çözme.

## 0. Adım 0 — çalışma dalını hat ucuna getir

1. Ana checkout'ta (`C:\Users\Ali\erp-vnext`) çalışma dalı **`feature/sce/mod-0173-inventory-ledger-valuation`** (MVP-1 hattı). Yeni dal açma.
2. CT'nin koyacağı etikete ileri sar: `git merge --ff-only ct/line-ff-2026-11-13d`. Etiket yoksa ya da ileri sarma mümkün değilse
   (**ff değilse**) **dur**, CT'ye bildir; merge commit'i ya da rebase yapma.
3. Not: Inventory 5067 ana checkout'tan `dotnet watch` ile koşuyorsa derleme ve sabotaj koşularını ayrık (detached) bir worktree'de yap.
4. Dilim başı ölçümleri (§7) bu commit'te alınır ve teslim raporuna yazılır.

## 1. Amaç ve kapsam

Paket §23 DL-1: "WMS-WAVE-BUNDLE sözleşme taslağı + dalga çekirdeği: yarat / düzenle / iptal, sipariş ekle / çıkar, öner, sınır uyarıları,
kesim saat dilimi, depo ayarı, izin, kiracı, denetim, kayıt kodu (arka uç)". Kapsadığı bölümler (§23): Owned Objects (§3), Entity Fields (§4),
Indexes (§4.6), Repo Scope (§5), Protected Paths (§6), Dependencies (§7), §8.1, §8.2, §8.3, §8.4, §8.5, §8.12, §8.13, §8.14, Backend File
Convention (§10), Validation Rules (§12), Failure Path (§13), Authorization (§14), Audited Events (§21), Record Codes (§22).

**DL-1'de yazılacaklar (paketten):**
- Varlıklar `Wave`, `WaveOrder`, `WaveEvent`, `WaveWarehouseSettings` ve `WaveCollections` (`wms_wave_waves`, `wms_wave_orders`, `wms_wave_events`,
  `wms_wave_settings` — §3; ad önerisi, kesin ad DL-1'in ilk ölçümüyle sabitlenir). `WaveException` DL-3'tür.
- Yazma komutları (§3, §21): `CreateWaveCommand`, `UpdateWaveCommand`, `AddWaveOrdersCommand`, `RemoveWaveOrdersCommand`, `CancelWaveCommand`,
  `UpdateWaveWarehouseSettingsCommand`. (`Release*`, `CompleteWaveRelease*`, `ReleasePendingOrders*` DL-2; `Recall*`, `ResolveWaveException*`,
  `ReconcileWave*` DL-2/DL-3.)
- Uçlar — paket X-14 beklenen dağılımı (**çıkarım, bağlayıcı değil**; kesin sayı controller farkından sayılır): **14** uç —
  `GET` / `POST /waves`, `GET /waves/{waveId}`, `POST /waves/{waveId}/update`, `GET` / `POST /waves/{waveId}/orders`,
  `POST /waves/{waveId}/orders/remove`, `GET /waves/{waveId}/suggestions`, `POST /waves/{waveId}/cancel`, `GET /waves/candidates`,
  `GET /waves/{waveId}/load`, `GET /waves/{waveId}/events`, `GET /waves/settings/{warehouseId}`, `POST /waves/settings/{warehouseId}/update`.
  Controller'lar `[Route("api/warehouse")]`, klasör `INV/Api/Controllers/WavePlanning/`, namespace
  `Diten.InventoryService.Api.Controllers.WavePlanning` (§3). PUT / DELETE yok.
- İzinler (§14, DL-1 satırları): `warehouse.waves.read`, `.create`, `.update`, `.settings.configure` (Tier-3), `.cancel`. Manifest
  `WavePlanningManifestProvider` (ModuleCode `WAVE-PLANNING`, SortOrder 180; sayfa tanımları §14; `ApiPermissions` = `warehouse.waves.settings.configure`).
- `WaveMaintenanceHostedService` — paket §5 Persistence satırı DL-1…DL-3 der; AC-12 mimari testi (yalnız `CompleteWaveReleaseCommand` /
  `ReconcileWaveCommand` / `ReleasePendingOrdersCommand` gönderir; dalga **yaratmaz**) DL-1'dedir. **CT kararı (2026-11-13):** DL-1'de servis
  sınıfı ve testten çağrılabilen tek tur yöntemi yazılır ve kaydedilir, ama tur **hiç komut göndermez** (üç komut DL-2 / DL-3'te kendi
  dilimlerinde eklenir); AC-12 mimari testi DL-1'de "gönderilen komutlar yalnız bu üç türün alt kümesi" kuralını ölçer, S-12b
  (`CreateWaveCommand` gönderimi) bu testi kırmızı yapar. Aralık ayarı (`Warehouse:Wave:ReconcileSeconds`, 60 sn) DL-1'de yalnız okunur.

**Kapsam DIŞI:**
- DL-2: serbest bırakma, rota, MOD-0178 seam'i (X-1…X-5, X-8), dalga yönetimini kapatma (AC-15 f…q), `ReleasePendingOrdersCommand`.
  DL-1'de ertelenmiş (`Deferred`) outbound oluşamaz (AC-15 başlığı).
- DL-3: ilerleme / uzlaştırma, istisnalar (`WaveException`, `wms_wave_exceptions`), riskli dalga, geri çekme, `recall` fiili (X-15 DL-3).
- DL-4: **bütün ekranlar** (Views, Web proxy controller'ları, JS, sayfa resx'leri, nav, sayfa yardımı, `PageHelpCatalog.cs`, X-7).
- DL-5: KPI + CSV. DL-6: `Mod0180WaveRouteTests.cs` (DL-5 kabulünden sonra).
- X-13 AuthService rol şablonu (ayrı WP).

## 2. İlk adım — WMS-WAVE-BUNDLE sözleşme taslağı (contract-first)

- Dosya: `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` (yazılacak; §5 ilk satır, DL-1).
- İçerik (AC-1): §3'teki **her** uç — **23 işlem** (§24 sıra cümlesi; AC-55) — operationId, istek / cevap şeması, hata kodları (§8.17),
  `If-Match` / `Idempotency-Key`; §8.6 port sözleşmesinin 0178'e bakan yüzü (bilgi); sürüm `1.0.0-draft`. DL-2…DL-5 uçları da taslakta yer alır
  (taslak 23, DL-1 sonrası klasör 14 uç; eşitlik DL-5 sonrası — AC-55).
- **Kim yazar:** pakette taslak "DL-1'in ilk adımı" (§5, §7 satır "bu paket (DL-1)", §18) ve AC-1 DL-1 AC'sidir → **taslağı geliştirici oturumu
  hazırlar**. **Dondurma ayrı CT adımıdır** (§5, §18) — geliştirici taslağı dondurmaz, `FROZEN` işareti koymaz. Pakette "CT tek yazıcı"
  ifadesi taslak için **yok**.
- Sıra: taslak commit'i controller kodundan **önce**. Sözleşme testi (AC-1) controller cevaplarını taslağa karşı doğrular. Paketin andığı emsal
  `WmsDraftContract.cs` bugünkü ağaçta yok (MOD-0178 sözleşmesi dondurulunca kaldırıldı); güncel emsal
  `INVT/CycleCount/CycleCountContract.cs` ve `INVT/TestInfrastructure/ContractSchemas.cs` (şema kapalı okunur).
- Donmuş sözleşmelere dokunma: `warehouse-outbound.openapi.yaml`, `wms-bundle.openapi.yaml` (X-8 DL-2 öncesi CT adımı), `location.openapi.yaml`,
  `record-code-allocate.md` (§6).

## 3. Dokunulacak dosyalar (Repo Scope, §5 — DL-1 satırları)

Kısaltmalar (paket): `INV/` = `services/Diten.InventoryService/src/Diten.InventoryService.`; `INVT/` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`.

| Yol | Ne |
|---|---|
| `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` | taslak (§2) |
| `INV/Domain/WavePlanning/{Entities,Enums,Rules,Repositories}/` | varlıklar, durum / sınır / saat dilimi kuralları (saf) |
| `INV/Application/Features/WavePlanning/` | `WaveContract.cs`, `WaveErrorCodes.cs`, `WaveCentralAudit.cs`, `Ports/`, `Services/` (DL-1 ihtiyacı kadar) |
| `INV/Application/Features/Waves/` | Golden Slim: Commands, Queries, Handlers/CommandHandlers, Handlers/QueryHandlers, Validators, `WaveModels.cs` |
| `INV/Persistence/WavePlanning/` | depolar, `WaveCollections`, index yapılandırması (§4.6), 0178 okumaları **yalnız `Find`** ve `WarehouseCollections.*` sabitleriyle |
| `INV/Api/Controllers/WavePlanning/` | `WavesController` (DL-1 uçları) |
| `INV/Api/WavePlanning/` | `WavePlanningManifestProvider`, `WavePlanningServiceCollectionExtensions` |
| `INV/Infrastructure/Authorization/WavePermissions.cs` | izin sabitleri |
| `INVT/WavePlanning/` | birim, entegrasyon, mimari testler (0180'in kendi test ağacı) |
| `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` | **yalnız eklemeli** `Perm.Entity.*` satır adları (DL-1; X-15) |

XMC-001 dosyaları §4'te.

## 4. Dokunulmayacaklar ve XMC-001 seam'leri

**Korunan yollar (§6, AGENTS.md §4):** `.antigravity/**` (DL-1'in kapısı `0aa802519`'da kapandı; dilim yazmaz) · `gateway/Diten.ApiGateway/**/ocelot.json`
(yalnız integration-agent; bu paket değişiklik istemez) · `frontend/Diten.Web/Views/Shared/_Layout.cshtml` ve Archive · `services/Diten.Building.Blocks/**` ·
`services/Diten.AuthService/**` · başka domain servisleri · MOD-0178 kodu ve testleri (yalnız §5.1'de adıyla sayılan dosyalar; DL-1'de yalnız X-6) ·
MOD-0173 dosyaları (yalnız X-9, X-10, X-11, X-17) · MOD-0174/0175/0176/0177/0181 dosyası yok · donmuş sözleşmeler (§2).
Bu listede olmayan başka modül dosyasına dokunmak gerekirse **dur**, XMC-001 ile CT'ye sor.

**MOD-0178 bekçileri değişmez (§5.1 son paragraf, AC-18 notu):** `INVT/Warehouse/**` ve 0178 adlı mimari testler hiçbir satırı değişmeden yeşil.

| # | Dosya | DL-1 değişikliği (yalnız eklemeli) | Sahip | Sahip onayı kaydı | Hangi commit'te |
|---|---|---|---|---|---|
| X-6 | `INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs` (bugün `:26` `CycleCount = "CNT"`, `:28` `TypePrefixes`) | `Wave = "WAV"` sabiti + `TypePrefixes`'e `Wave` (`CNT` emsali) | MOD-0178 (Beste) | Q-4 = A, kararlar 03 (`dc010b911`). XMC-001 dokunma onayı: kararlar 03 (Beste 2026-11-13, "Evet, dördü de") — **onaylı** | `CreateWaveCommand` / kayıt kodu commit'i (AC-2, AC-10) |
| X-9 | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` (0181 satırları `:88-89`, `:92-93`; `declared` `:100-115`; önek süzgeci `:120`) | `WaveCollections.All` için `Assert.Equal` + `declared`'a `.Concat(WaveCollections.All)`; önek süzgeci değişmez | MOD-0173 (Beste) | kararlar 03 (Beste 2026-11-13) — **onaylı** | `WaveCollections` commit'i (AC-17) |
| X-10 | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` (`:53` bugün `117`; sağlayıcı listesi `:60-69`; `:130` `seen` bugün `14`) | sayı = **dilim başında ölçülen sayı + DL-1'in yazma komutları** (DL-1'de 6 — §3 / §21'den çıkarım); listeye `WavePlanningManifestProvider`; `seen` yeni işlem açan 0180 depoları kadar | MOD-0173 (Beste) | kararlar 03 (Beste 2026-11-13) — **onaylı** | komutlarla aynı commit (kırmızı pencere yok) |
| X-11 | `INV/Api/Program.cs` | `WavePlanningManifestProvider` + `AddWavePlanning()` | MOD-0173 (Beste) | kararlar 03 (Beste 2026-11-13) — **onaylı** | DI / manifest commit'i |
| X-14 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — **yalnız** `Assert.Equal(<n>, endpoints.Count)` satırı (bugün `:97`, `73`) ve üstündeki uç yorumu (`:80-96`) | `<n>` = dilim başında ölçülen sayı + DL-1'in eklediği 0180 uç sayısı (beklenen 73 → 87, çıkarım); yoruma DL-1 uçları adıyla | integration-agent (test); rota ailesi MOD-0178 (Beste) | `c7dc4ea6a` + kapsam teyidi `6235794ab` (her uç ekleyen dilim; ayrı onay yok) | **0180 controller uçlarıyla aynı commit** |
| X-15 | `frontend/Diten.Web/Resources/SharedResource.{7}.resx` `Perm.Entity.*` | DL-1: **yalnız satır adları** (beklenen `WAVES`, `WAVESSETTINGS` — çıkarım; kesin kod üretim `PermLabel.split`'ten, vitest ile ölçülür); `settings.configure` dahil — fiil `configure` köprüde zaten var (`_IndexL10n.cshtml:239`, `perm-label.js:52`), **fiil eklenmez**; `recall` fiili DL-3'tür. `_IndexL10n.cshtml`, `RoleAssignmentsIndex.{7}.resx`, `perm-label.js` DL-1'de değişmez. Var olan satır değişmez; defter tavanı "fiil 0 · satır 96" değişmez | Governance Rol İzinleri (sahip teyidi Beste) | **onaylı eklemeli seam** — kararlar 03 son madde, `1a91774d7` (MOD-0181 emsali `65b968b64`) | manifest anahtarlarını bildiren commit (AC-56) |
| X-17 | `INVT/Api/InventoryLedgerManifestCompletenessTests.cs` — `Every_enforced_permission_is_reachable_from_the_manifest` dışlama listesi (bugün `:213-223`, son satır `.CycleCounting` `:223`) | `.CycleCounting` satırının altına yorum + `.Where(t => t.Namespace?.EndsWith(".Controllers.WavePlanning", StringComparison.Ordinal) != true)`; yorum önerisi paket §5.1 X-17'de. Başka satır değişmez | MOD-0173 (Beste) | `ad1100158` (ayrı onay yok) | **ilk 0180 controller'ıyla aynı commit** (emsal `446cb58db`) |

**Adımlar (sıra):**
1. Taslak (§2) — ayrı commit.
2. Domain + Persistence + X-9 + X-6.
3. Komutlar / sorgular + X-10 + X-11.
4. **İlk 0180 controller commit'i:** controller(lar) + X-17 dışlama satırı + X-14 sayı satırı — **tek commit**. X-14 satırını **integration-agent**
   yazar (ayrı adım: integration-agent'a ölçülen önce sayıyı, DL-1 uç listesini ve commit'i ilet; o satırı ve yorumu yazar, aynı commit'e girer).
   Geliştirici `Mod0178WarehouseRouteTests.cs`'i kendisi düzenlemez.
5. X-15 satır adları + AC-56 vitest.

## 5. Kabul ölçütleri (DL-1 — §16, §24 DL-1 satırı)

| AC | Kısa başlık |
|---|---|
| AC-1 | WMS-WAVE-BUNDLE taslağı (23 işlem, `1.0.0-draft`) + taslağa karşı sözleşme testi |
| AC-2 | `POST /waves` 8 alanla 201, `Draft`, `{ŞİRKET}-WAV-` + 6 hane, `CutoffAtUtc` / `TimeZoneId` sunucudan, `Created` olayı aynı işlemde |
| AC-3 | Kiracı / şirket / depo izolasyonu — başka kiracı ve başka şirket 404 |
| AC-4 | Gövde / sorguda `tenantId` / `legalEntityId` → 400 |
| AC-5 | Her eylem `[HasPermission(WavePermissions.*)]`; manifest yalnız uygulanan `warehouse.waves.*`; namespace `Controllers.WavePlanning`; `WarehouseRegistrationTests` değişmeden yeşil; **alt ad alanı sahipliği vakası** (X-16 bekçisi); **INVENTORY-LEDGER bütünlük vakası** (X-17) |
| AC-6 | İdempotent yazma: aynı anahtar + gövde 200 replay; farklı gövde 400 `IDEMPOTENCY_KEY_REUSED` |
| AC-7 | İyimser kilit: eski `If-Match` 409 `WAVE_CONFLICT`; eşzamanlı iki düzenlemeden tam biri |
| AC-8 | Silme yok; `LedgerImmutabilityArchitectureTests` değişmeden; `cancel` yalnız `Draft`, gerekçe zorunlu |
| AC-9 | Merkezi denetim: `ICentralAuditDeclaration`; sayı = dilim başı + DL-1 komutları; 1 / 0 / 0 kayıt |
| AC-10 | Kayıt kodu: `Number` değişmez; eşzamanlı farklı numara; sayaç yoksa 503 `WAVE_RECORD_CODE_UNAVAILABLE`; 0178 sayacı `WAV` |
| AC-11 | Uygunluk ve D-8: yalnız `Allocated` + `Deferred`; iki etkin dalga 409; eşzamanlı eklemede tam biri |
| AC-12 | Öner: uygun + `ShipToFilter`, `CreatedAt` artan, yazmaz; `WaveMaintenanceHostedService` dalga yaratmaz (mimari test) |
| AC-13 | Sınırlar: `MaxOrders` / `MaxLines` aşımı yazılır + `limitWarnings[]`; 500 / 2.000 üstü 422 `WAVE_TOO_LARGE` |
| AC-14 | Saat dilimi: `Europe/Istanbul` dönüşümü, DST boşluğu 400, çakışma erken ofset, ayar yoksa 422 `WAVE_SETTINGS_REQUIRED` |
| AC-15 (a) | Ayar okuması `read`, yazma `settings.configure` + gerekçe; yalnız `update` taşıyana 403 |
| AC-15 (b) | Aralık dışı eşik 400, kayıt değişmez |
| AC-15 (c) | Kayıt yokken varsayılanlar (false / — / 90 / 70 / 30); sayımlar sıfır (bilgi, bekçi değil) |
| AC-15 (d) | Başka şirketin ayarı 404 |
| AC-15 (e) | `false` → `true` ve eşik değişikliği her zaman 200 |
| AC-16 | 0178 verisine yazılmaz (mimari tarama; `Find` dışı çağrı yok) |
| AC-17 | `SingleSystemOfRecordArchitectureTests` `wms_wave_*`'ı bildirir (X-9) |
| AC-55 (0178 sayı vakası) | Gateway `Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route` yeni sayıyla yeşil; `git diff` yalnız sayı satırı + yorum |
| AC-56 | Rol İzinleri: her `warehouse.waves.*` anahtarının fiili ve satır adı 7 dilde; `role-assignments-label-ledger.test.js` yeşil; tavan değişmez |

AC-15 (f)…(q) DL-2, (r) DL-3 — bu WP'de yok.

## 6. Sabotaj listesi (§24 DL-1 satırı — tam liste)

Her sabotaj: komut + kırmızı çıktı + geri alma + geri yeşil. Hiçbiri commit'e girmez. "Pakette yok" = beklenen test adı pakette adıyla yazılmadı;
kırmızıya dönen testin adını raporda yaz.

| S | Bozulan şey (paket) | Beklenen kırmızı |
|---|---|---|
| S-1 | bir cevaptan zorunlu alanı kaldır | AC-1 sözleşme testi (ad pakette yok) |
| S-2 | olay yazımını işlemden çıkar | AC-2 testi (ad pakette yok) |
| S-3a | depodan `TenantId` yüklemini düşür | AC-3 izolasyon testi (ad pakette yok) |
| S-3b | `LegalEntityId` yüklemini düşür | AC-3 (ad pakette yok) |
| S-4 | `tenantId` / `legalEntityId` denetimini kaldır | AC-4 (ad pakette yok) |
| S-5a | bir eylemden `[HasPermission]`'ı kaldır | `AuthorizationGuardTests` |
| S-5b | `WavesController`'ı `Controllers.Warehouse` namespace'ine taşı | `WarehouseRegistrationTests` |
| S-5c | üç geçici 0178 dokunuşu (`PICKTASKS` sayfa eylemi `warehouse.waves.x`, `WarehousePermissions.Implemented`, `PickTasksController`'a `[HttpGet("wave-sabotage")]` + `[HasPermission("warehouse.waves.x")]`) — ayrıntı AC-5 | (i) 0180 alt ad alanı sahipliği vakası; (ii) `WarehouseRegistrationTests.AC34_the_manifest_declares_the_three_S2_pages_and_one_action_per_S2_button`; (iii) `WmsBundleContractTests.Every_served_warehouse_route_is_an_operation_with_its_permission_key`; Gateway koşulursa (iv) `Mod0178WarehouseRouteTests.Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route` (sayı +1). Yeşil kalmalı: `The_manifest_declares_only_the_warehouse_keys_the_controller_enforces`, AC20, AC36. Listede olmayan kırmızı adıyla yazılır |
| S-5d | `WavePlanningManifestProvider`'a geçici `warehouse.x` | alt ad alanı vakasının 0180 yarısı |
| S-5e | X-17 dışlama satırını kaldır | `InventoryLedgerManifestCompletenessTests.Every_enforced_permission_is_reachable_from_the_manifest` (ileti `[HasPermission(warehouse.waves.…)] is enforced but the manifest never declares it`); 0180 kendi bütünlük testi yeşil kalır |
| S-6a | idempotency parmak izi karşılaştırmasını kaldır | AC-6 (ad pakette yok) |
| S-6b | anahtar aramasını kaldır | AC-6 — ikinci olay (ad pakette yok) |
| S-7 | depoda `Version` koşullu filtreyi kaldır | AC-7 (ad pakette yok) |
| S-8a | `DELETE /waves/{waveId}` ekle | `LedgerImmutabilityArchitectureTests` |
| S-8b | `cancel`'da durum denetimini kaldır | AC-8 (ad pakette yok) |
| S-9 | bir geri çağrıdan `CentralAuditTransaction.EnlistAsync`'i kaldır | AC-9 (ad pakette yok) |
| S-10 | `TypePrefixes`'ten `WAV`'ı çıkar | yaratma 503, AC-2 testi |
| S-11 | tekil kısmi index'i kaldır | AC-11 eşzamanlı ekleme testi |
| S-12 | uygunluk süzgecinden `Active` üyelik koşulunu kaldır | AC-12 (ad pakette yok) |
| S-12b | `WaveMaintenanceHostedService`'e `CreateWaveCommand` gönderimi ekle | AC-12 mimari testi (ad pakette yok) |
| S-13 | sınır aşımında ekleme reddi koy | AC-13 (ad pakette yok) |
| S-14 | dönüşümde sunucu saat dilimini kullan | AC-14 (ad pakette yok) |
| S-15 | yüzde aralık denetimini kaldır (150 kabul) | AC-15 (b) |
| S-15c | ayar ucunun `[HasPermission]`'ını `WavePermissions.Update`'e çevir | AC-15 (a) 403 vakası |
| S-16a | bir 0180 deposuna `wms_tasks` üzerinde `UpdateOneAsync` ekle | AC-16 mimari testi (ad pakette yok) |
| S-16b | handler'a `IWarehouseTaskRepository` enjekte et | AC-16 mimari testi |
| S-17 | bildirilmemiş `wms_wave_x` koleksiyonu aç | `SingleSystemOfRecordArchitectureTests` |
| S-55c | bir 0180 controller'ına `[HasPermission(WavePermissions.Read)]` + `[HttpGet("waves/count-sabotage")]` ekle | `Mod0178WarehouseRouteTests` sayı satırı (güncel değerden +1); beklenen kırmızı yalnız budur |
| S-56 | `SharedResource.ru.resx`'ten `Nav.Page.*` karşılığı olmayan bir 0180 `Perm.Entity.*` (beklenen `WAVESSETTINGS`, çıkarım) sil | `role-assignments-label-ledger.test.js` (vitest) |

S-5c 0178 üretim dosyalarına **geçici** dokunur — sabotajdır, XMC değişikliği değildir; bayt-eşit geri alınır (`git diff` boş kanıtı).

## 7. Test koşuları

- **Dilim başı ölçümü (Adım 0 commit'inde):** InventoryService paketi geçti / kırmızı sayısı ve kırmızı adları; Gateway test projesi; `:53` / `:130`
  (X-10) ve `Assert.Equal(<n>, endpoints.Count)` (X-14) değerleri.
- **InventoryService test paketi bütünüyle, filtre yok** (`dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests`) —
  servise dokunulduğu için (§17).
- **Gateway test projesi** `gateway/Diten.ApiGateway.Tests` — X-14 sayısıyla yeşil (AC-55 0178 sayı vakası).
- **Web vitest:** `npx vitest run tests/role-assignments-label-ledger.test.js` (`frontend/Diten.Web`) — AC-56; teslim kanıtına girer.
- **Build:** `Diten.InventoryService` PASS.
- **MOD-0178 bekçileri:** `INVT/Warehouse/**` değişmeden yeşil + kanıt
  `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse` **boş** (§5.1, AC-18 notu).
  Ayrıca `git diff <dilim-başı>..<dilim-sonu> -- gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` yalnız sayı satırı + yorum;
  `InventoryLedgerManifestCompletenessTests.cs` farkı yalnız X-17 satırı + yorum.
- **Ortam:** 27017 **kullanma**; `INVENTORY_TEST_MONGO` **ayarlama** (suite kendi replica set'ini açar); `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşma**.
- Baseline'da kırmızı varsa aynı gün `main`'de de ölçülüp raporda ayrılır.

## 8. Git kuralları (GIT-002)

- Dal: `feature/sce/mod-0173-inventory-ledger-valuation` (MVP-1 hattı); **yeni dal açma**.
- `main`'e commit / push **yok**.
- Stage yalnız açık dosya yoluyla; `git add -A` / `git add .` **yok**.
- Hook atlama (`--no-verify`) ve imza atlama yok; yıkıcı git komutu onaysız yok.
- Yerel commit'ler serbest; **push ve PR yok** — modül geliştirmesinin sonunda, ayrı kullanıcı onayıyla; push / PR'dan önce `origin/main` alınır (merge).
- Commit iletileri: `feat(sce): MOD-0180 DL-1 — …` / `test(sce): …` / `docs(contract): …`.

## 9. Teslim raporu biçimi

1. Commit listesi (SHA + ileti; ilk controller commit'inde X-14 ve X-17 satırlarının birlikte olduğu açıkça).
2. Test sayıları: InventoryService (önce → sonra; geçti / kırmızı / atlandı, kırmızılar adıyla), Gateway (önce → sonra), vitest ledger.
3. X-10 sayıları: `WriteCommands.Length` ve `seen` önce → sonra.
4. Gateway sayısı: `Assert.Equal(<önce>, endpoints.Count)` → `Assert.Equal(<sonra>, endpoints.Count)` (satır numarasıyla değil, satırın kendisiyle atıf).
5. Her sabotaj (§6, 29 kalem): komut / değişiklik, kırmızı test adı + çıktı özeti, geri alma, geri yeşil kanıtı.
6. `git diff --stat` kanıtları (§7).
7. Taslak: dosya yolu, işlem sayısı (23), sürüm.
8. Açık kalanlar ve CT'ye sorular.

## 10. Kural haritası (AGENTS.md §6.1)

- **Daima:** `multi-tenancy`, `security-jwt`, `git-safety`, `code-style`, `docs-organization`, `cross-module-change-consent` (XMC-001).
- **Backend / handler:** `handler-design`, `repository-standard`, `response-envelope`, `pipeline-behaviors`, `entity-base-template`,
  `entity-versioning`, `mongo-indexing`, `api-conventions`, `routes`, `ports`, `audit-trail-standard` (AUD-001).
- **Yetki, izin:** `permission-key-standard` (PKS-001 §4 `warehouse.waves.*` `:106`, `:92` istisna), `business-module-enforcement-standard`,
  `data-scope-enforcement` (SEC-002).
- **Yeni modül (manifest, kayıt kodu):** `module-self-registration-standard`, `record-coding-standard` (RCS-001 §3 `WAV` `:72`).
- **Kullanıcıya görünen metin (Perm.Entity satır adları):** `localization-standard` (kiracı 7 dil).
- **Ortam / kayıt:** `dev-runbook`, `configuration-safety`, `logging-observability`, `status-reporting-and-evidence` (REP-001).
- **Mimari:** `erp-architecture`, `diten_standards`.

Dosyalar `.antigravity/rules/<ad>.md`; okumak serbest, yazmak yasak.

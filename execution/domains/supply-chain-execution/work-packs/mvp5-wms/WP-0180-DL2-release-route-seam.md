# WP-0180-DL2 — MOD-0180 Dalga Planlama DL-2: serbest bırakma + rota + MOD-0178 seam'i + dalga yönetimini kapatma (arka uç)

Tarih: 2026-11-13 · Veren: CT (MVP-5 WMS CT, sahip Beste) · Durum: **taslak — başlama kapısı açık (§0 madde 3, X-8 / CT-1)**
- Modül: **MOD-0180 — Wave Planning**, servis `Diten.InventoryService` (5067), kiracı modülü. Sahip: geliştirici oturumu.
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0180-wave-planning.md` **r11**, LF sha256
  `226ab7f9f3690c422fba1bf5a3008d004d82791cfe6b20ebb2b74c0cd47cb095` (CT ölçümü, HEAD `d4308cb65`), `status: ready-for-dev`; onay satırı
  Ali / `GMG-ERP` (`e12bf26c8`). Dilim: **DL-2** (paket §23), kanıt seviyesi **L3 + E5**.
- DL-1: **VERIFIED** — `docs/records/audits/2026-11/mod-0180-dl1-ct-acceptance-2026-11-13.md` (FIX-01 `3e31bfde9`); D-2 ve geliştirici açık
  noktaları bu dilime taşındı (§1.3).
- Yetki: paket > `domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse paket geçerlidir; çelişkiyi CT'ye yaz, kendin çözme.
  Bu WP push, PR ya da `.antigravity/**` değişikliği izni değildir.

## 0. Başlamadan

1. **Dal:** tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` (ana checkout `C:\Users\Ali\erp-vnext`); yeni dal açma. CT etiketi
   `ct/line-ff-2026-11-13q`: etiket dalın atasıysa devam; değilse `git merge --ff-only ct/line-ff-2026-11-13q`; olmazsa
   `git merge --no-ff ct/line-ff-2026-11-13q`; çakışmada `git merge --abort` ve CT'ye yaz (çakışmayı kendin çözme).
2. **Paket sha'sı:** `tr -d '\r' < <paket> | sha256sum` → `226ab7f9…` ile başlamalı; ölç ve raporun ilk satırına yaz. Farklıysa **dur**.
3. **Başlama kapısı (paket §24 DL-2 satırı, §5.1 X-8):** WMS-BUNDLE eklemeli sürümü (X-8: `OutboundEventKind` `PickTasksIssued` /
   `PickTasksWithdrawn`; `WarehouseTask` / `OutboundPickTask` `wave`; `OutboundShipmentView` `pickIssuance`; `listPickTasks` `orderBy`
   `waveSequence` + `waveId`) **CT tarafından yazılmış ve dondurulmuş** olmalı; sürüm numarası CT-1 kararıdır. Ölç:
   `docs/analysis/contracts/wms-bundle.openapi.yaml` `info.version` > `1.1.0` ve dosyada `PickTasksIssued` var. **CT ölçümü (HEAD `d4308cb65`):
   sürüm `1.1.0`, dalga üyesi yok — kapı açık.** Kapı kapanmadan hiçbir DL-2 kodu yazılmaz (0178 servis sözleşme testleri şemayı kapalı okur;
   DTO'ya `pickIssuance` / `wave` eklendiği an değişmeyen 0178 testleri kırmızı olur — X-8 F-3).
4. **Eşzamanlılık:** aynı InventoryService'te **MOD-0182 DL-1 FIX-01** (Barcode alanı) ve **MOD-0178-FU01 DL-2** (outbound `CustomerRef`) sürüyor.
   - Ana checkout'ta derleme / test **yok** (Inventory 5067 oradan `dotnet watch` ile koşabilir); derleme, test ve sabotaj **ayrık (detached)
     worktree**'de.
   - **Dosya çakışması beklenir:** FU01 DL-2 `OutboundShipmentEntities.cs` (X-1), `OutboundShipmentModels.cs` `OutboundShipmentViewDto` ve
     `OutboundShipmentReads.cs` (X-8 üretim karşılığı), `WarehouseErrorCodes` dosyalarına da yazıyor. Her commit'ten hemen önce hattı al;
     çakışırsa iki tarafın eklemesini de koru, CT'ye yaz.
   - **Ortak sayaçlar dilim başında ölçülür** (o anki hat ucunda; aşağıdaki değerler CT'nin `d4308cb65` ölçümüdür, FIX-01 / FU01 DL-2 hatta
     girdiyse değişmiş olabilir):
     | Sayaç | Dosya / satır | `d4308cb65` |
     |---|---|---|
     | Gateway uç sayısı (X-14) | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` `Assert.Equal(<n>, endpoints.Count)` | **103** |
     | Merkezi denetim yazma komutu (X-10) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` `Assert.Equal(<n>, WriteCommands.Length)` | **133** |
     | Merkezi denetim `seen` (X-10) | aynı dosya `Assert.Equal(<n>, seen)` | **16** |
     | Kayıt kodu önek listesi | `INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs` `TypePrefixes` | **7** (PUT, PCK, OUT, PKG, CNT, WAV, DVC) — DL-2 önek eklemez |
5. **AC-18 referansı (dilim başı, kod yazmadan önce):** `WaveManaged = false` depoda outbound yaratma + tahsis fikstür betiğinin çıktısı
   (outbound, pick görevleri, olaylar; kimlik / zaman normalize JSON + sha256) X-1…X-5 **öncesi** commit'te üretilir ve rapora yazılır. Betik
   0180 test ağacında referans kopyası saklamaz (kendi kopyasıyla ölçme yasak).
6. Paketi oku: §4.1 (`PickPlan`, `RouteSummary`, `ReleaseLeaseUntilUtc`, `ReleaseRequest`), §4.2, §4.5 (r4–r7 sistem alanları), §4.6, §5, §5.1
   X-1…X-5 / X-8 / X-10 / X-11 / X-14, §6, §8.2 kural 1 ve 3, §8.3, §8.5, §8.6, §8.7, §8.8 (iki döngü), §8.12, §8.13, §8.17, §8.19 (bütünüyle),
   §13, §14 (`release`), §16 AC-12 / AC-15 (f)…(q) / AC-18…AC-28 / AC-55, §21, §24 DL-2 satırı. Taslak sözleşme:
   `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` (`releaseWave`, `listWaveTasks`, `WaveReleaseResult`, ayar cevabının
   `pendingRelease`'i).

## 1. Kapsam

### 1.1 Paket DL-2 satırı (§23, §24)
"Serbest bırakma + rota optimizasyonu + MOD-0178 seam'i (görev erteleme, port, kurtarma) + dalga yönetimini kapatma (bekleyenlerin dalgasız görev
açılışı, bakım turu)". Kapsadığı bölümler: §8.6, §8.7, §8.19, §8.3, §8.17, §13, §21.

- **Komutlar (§3, §21):** `ReleaseWaveCommand` (izin `warehouse.waves.release`, If-Match, Idempotency-Key), `CompleteWaveReleaseCommand` (yalnız
  bakım), `ReleasePendingOrdersCommand` (yalnız bakım); `UpdateWaveWarehouseSettingsCommand` `true` → `false` dalı (C1 + ilk tur, §8.19).
- **Uçlar (taslak `:276-277`):** `POST /waves/{waveId:guid}/release` (`releaseWave`), `GET /waves/{waveId:guid}/tasks` (`listWaveTasks`,
  `read`). PUT / DELETE yok. Beklenen +2 (çıkarım; kesin sayı controller farkından).
- **İzin:** `WavePermissions.Release = "warehouse.waves.release"` → `Implemented`'a ve manifeste (sayfa eylemi `WAVES` / `WAVEPLANNER` — paket
  §14 sayfa tanımına göre; ekran DL-4). Fiil `release` köprüde var (`perm-label.js:54`), satır `Perm.Entity.WAVES` 7 dilde var → **resx değişmez**.
- **Rota (§8.7):** S-SHAPE-v1, saf domain kuralı; LOCATION okuması istek başına bin başına bir kez; `PickPlan[]` + `RouteSummary` serbest
  bırakmada bir kez yazılır, kurtarmada yeniden hesaplanmaz (AC-28).
- **Durum makinesi:** `Draft → Releasing → Released`, `Releasing → Draft` (`ReleaseAborted`); kiralama `Warehouse:Wave:ReleaseLeaseSeconds`
  (öneri 120).
- **Bakım servisi (§8.8):** iki ayrı döngü; bu dilimde bekleyen açılış döngüsü (`Warehouse:Wave:PendingReleaseSeconds`, tek tur yöntemi öneri
  `RunPendingReleaseOnceAsync`) ve serbest bırakma kurtarması (`CompleteWaveReleaseCommand`). DL-1'in `RunOnceAsync`'i paketin iki-döngü adlarına
  ayrılır. Uzlaştırma (`ReconcileWaveCommand`) DL-3. Ayarlar: `PendingReleaseMaxBatchesPerTour` (50), `PendingReleaseStuckMax` (1.000).
- **AC-12 mimari testi:** izinli küme `CompleteWaveReleaseCommand` + `ReleasePendingOrdersCommand` (+ DL-3'te `ReconcileWaveCommand`); hâlâ
  "alt küme" kuralı, `CreateWaveCommand` S-12b ile kırmızı.

### 1.2 XMC-001 dokunuşları (paket §5.1 — yalnız eklemeli; başka 0178 / 0173 dosyası gerekirse **dur**, CT'ye sor)

| # | Dosya | DL-2 değişikliği | Onay |
|---|---|---|---|
| X-1 | `INV/Domain/Warehouse/Entities/OutboundShipmentEntities.cs` | nullable `PickIssuance { State: Deferred \| Issued, WaveId?, WaveNumber?, IssuedAtUtc? }`; 0178 iptali dokunmaz | paket r11 onayı (§5.1) |
| X-2 | `INV/Domain/Warehouse/Entities/` `WarehouseTask` | nullable `WaveRef { WaveId, WaveNumber, Sequence }` (yalnız Pick) | aynı |
| X-3 | `OutboundAllocationService.cs` | (a) `PickTask` + `TaskCreated` **davranış değişmeden** `OutboundPickTaskFactory.cs`'e taşınır; (b) `IPickIssuancePolicy` `Deferred` derse numara / görev yok, outbound `Allocated` + `Deferred`; `Immediate` dalı **değişmez** | aynı |
| X-4 | yeni: `Warehouse/Ports/IPickIssuancePolicy.cs` (+ `ImmediatePickIssuancePolicy`), `Warehouse/Ports/IWavePickIssuance.cs`, `OutboundShipments/Services/WavePickIssuanceService.cs`, `OutboundShipments/Services/OutboundPickTaskFactory.cs` | `IssueAsync`, `WithdrawAsync` (port yüzü; çağıran DL-3), `IssueImmediateAsync` (`NotDeferred` dahil); 0180 `WavePickIssuancePolicy` (`Features/WavePlanning/Ports/`) | aynı |
| X-5 | `INV/Domain/Warehouse/Enums/OutboundShipmentEnums.cs` | `OutboundEventKind` + `PickTasksIssued`, `PickTasksWithdrawn` | aynı |
| X-8 | üretim DTO'ları: `PutawayTaskModels.cs` `WarehouseTaskDto` + `PutawayTaskReads.cs`; `OutboundShipmentModels.cs` `OutboundPickTaskDto` / `OutboundShipmentViewDto` + `OutboundShipmentReads.cs` | donmuş sözleşmenin üyeleri kadar (`wave`, `pickIssuance`); sözleşme dosyasına **yazılmaz** (CT adımı) | CT adımı (§0 madde 3) |
| X-10 | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` | sayı = dilim başı + DL-2 yazma komutları (beklenen **+3**: Release, CompleteWaveRelease, ReleasePendingOrders — §3 / §21'den çıkarım; paket §24 yalnız `ReleasePendingOrders +1`'i adıyla anar, kesin sayı komut farkından); `seen` yeni işlem açan 0180 depoları kadar | kararlar 03 (DL-2 artışı onayın içinde) |
| X-11 | `INV/Api/Program.cs` | yalnız gerekirse; `WavePickIssuancePolicy`'nin `IPickIssuancePolicy` kaydı tercihen `AddWavePlanning()` içinde ve 0178 varsayılanını **geçersiz kılacak sırada** (kayıt sırası testle ölçülür) | kararlar 03 |
| X-14 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — yalnız `Assert.Equal(<n>, endpoints.Count)` + üstündeki yorum | dilim başı + DL-2 uçları; **integration-agent** yazar, uçlarla **aynı commit** | `c7dc4ea6a` + `6235794ab` |

Değişmez (bağlayıcı): `INVT/Warehouse/**`, 0178 adlı mimari testler, `InventoryLedgerManifestCompletenessTests.cs` (X-17 yalnız DL-1),
`LedgerImmutabilityArchitectureTests` PUT / DELETE listesi, `ocelot.json`, donmuş sözleşmeler, `.antigravity/**`, Building.Blocks, AuthService.
0178'e eklenen davranış (X-1…X-5) `INVT/WavePlanning/`'de ölçülür.

### 1.3 DL-1'den taşınanlar
- **D-2 (düşük):** `WaveRepository.RemovePlannedOrdersAsync` (bugün `WaveRepository.cs:211-221`) yalnız `State = Planned` koşuluyla yazıyor; karar
  (9) ve taslak kural 6 "üyelik kapanışları `State` + `Version` koşullu" der. DL-2'de **lafza hizalanır** (karar notu seçeneği yerine; DL-2 üç yeni
  sistem kapanışı ekliyor: `ReleaseAborted`, `WaveManagementDisabled`, `Excluded`): her kapanış okunan `Version` ile koşullu; eşleşmeyen kapanış
  sayısı ≠ beklenen → işlem çakışma yoluna girer (aynı işlem geri alınır). Vaka: eşzamanlı kapatma turu ile Taslaktan çıkarma aynı üyeliği
  kapatırken tam biri kazanır, olay tek. Sabotaj **S-D2:** `Version` koşulunu kaldır → vaka kırmızı.
- **Geliştirici açık noktaları (DL-1 kabul kaydı satır 40; ham rapor depoda yok — konu başlıkları kayıttan, ayrıntı CT çıkarımı):**
  (a) **bakım indeksi** — §4.6 `wms_wave_waves (TenantId, Status, ReleaseLeaseUntilUtc)` DL-1'de yazılmadı (`WaveIndexConfiguration.cs`'de yok);
  kurtarma sorgusuyla birlikte eklenir, index adı `UniqueIndexes` dışı listeye; kurtarma sorgusunun bu index'i kullandığı (explain) rapora.
  (b) **EventSeq** — `Wave.EventSeq` / `WaveWarehouseSettings.EventSeq` bellekte artırılıyor (`WaveSupport.cs:45`, `:50`); takılı liste yazımı
  `Version`'ı artırmadığı ve ilk tur ile bakım turu aynı ayar akışına olay yazdığı için `Seq` işlem içinde okunan son belgeden hesaplanır;
  saklanan `EventSeq` = son olayın `Seq`'i (MOD-0181 DL-1 DÜŞÜK-4 dersi). Vaka: aynı ayar akışına eşzamanlı iki tur → `Seq` boşluksuz ve tekil.
  (c) **releasingOrderCount** — DL-1 `ReleasingOrderCountAsync` `Releasing` dalgaların `Planned` üyeliklerini sayıyor; AC-15 (l) ile gerçek 0178
  `Deferred` outbound'larıyla ölçülür (`deferredOrderCount 1`, `releasingOrderCount 1`, bakım turu O9'u açmaz).
  (d) **ara commit'ler** — DL-1'de ara commit'ler tek tek ölçülmedi. DL-2'de her commit derlenir; X-10 / X-14 sayı commit'leri ve X-1…X-5
  commit'leri için InventoryService filtresiz koşu sayısı commit başına rapora (kırmızı pencere yok kanıtı).
  (e) kayıt yalnız dört konu adıyla yazıyor; beşinci nokta ham raporda ise geliştirici rapora ekler.

### 1.4 Kapsam dışı
DL-3: `RecallWaveRemainingCommand` / `recall` ucu ve fiili, `ReconcileWaveCommand` ve uzlaştırma döngüsü, `wms_wave_exceptions` (X-9 DL-3),
istisna çözümü, riskli dalga, "Sonraki dalgaya taşı". DL-4: ekranlar ve X-7 (Web `PickTasks` süzgeci). DL-5: KPI / CSV. DL-6: `Mod0180WaveRouteTests`.
X-13 rol şablonu (Ali).

## 2. Kararlar ve açık CT noktaları

Kapalı (uygulanır): Y-1 = A (kapatma reddedilmez, bekleyenler dalgasız açılır), "hemen" teyidi ve Q-23 = A (ilk tur ≤ 200 sipariş / 2.000 görev,
depo başına tur başına ≤ 50 parti), Y-3 (S-SHAPE-v1, AC-25 39 / 66), Y-2 (Excluded / OrderCancelled → PartiallyCompleted, DL-3 uzlaştırmada),
Q-8 = A (500 / 2.000 teknik sınır), CT taslak kararları 2026-11-13 (1)…(9) ve A-1…A-5 (kararlar 03), X-10 / X-14 DL-2 artışları sahip onaylı.

**Açık — CT kararı gerekir (geliştirici kendisi seçmez; karar gelene dek ilgili vaka "bekliyor" diye raporlanır):**
1. **X-8 / CT-1** — WMS-BUNDLE eklemeli sürüm numarası ve dondurma (başlama kapısı, §0 madde 3). Aynı sürüme aday başka eklemeler:
   K-4 (görevlere raf / ürün kodu, kararlar 2026-11-13) ve MOD-0178-FU01 DL-2 `customerRef` üyeleri — sıra CT'de.
2. **İstisna kaydı DL-3'te:** AC-22 (b) / (c) ve §8.6 adım 4–5 serbest bırakmada `OrderNotIssuable` / `OrderCancelledInWave` istisnası açılmasını
   ister; `WaveException` ve `wms_wave_exceptions` paket §3'te DL-3'tür. CT önerisi: DL-2'de `Excluded` üyelik + olay `Data`'sı (sipariş, kod)
   ölçülür; istisna kaydı vakası DL-3 kabulünde aynı fikstürle tamamlanır.
3. **AC-15 (k):** "Sonraki dalgaya taşı" 422 ve W2'de geri çekme 200 DL-3 komutlarıdır. CT önerisi: (k)'nın yaratma / ekleme / serbest bırakma
   422'leri DL-2'de; taşıma ve geri çekme parçaları DL-3 kabulünde.
4. **Kurtarma döngüsü:** `CompleteWaveReleaseCommand`'ı hangi döngünün göndereceği paketçe sabitlenmemiş (§8.8 iki döngüyü uzlaştırma / bekleyen
   açılış diye ayırır). CT önerisi (çıkarım): uzlaştırma döngüsü (`ReconcileSeconds`) DL-2'de yalnız kurtarmayı gönderir, DL-3 `Reconcile`'ı ekler;
   tek tur yöntemi `RunReconcileOnceAsync`.

## 3. Kurallar

- **Kiracı:** her okuma / yazma `TenantId` + `LegalEntityId` (sunucuda) + `WarehouseId`; başka kiracı / şirket **404**; gövde / sorguda kapsam alanı
  400. Bakım servisi kayıt başına `SetScope` ile çalışır; sabit / varsayılan kiracı yok. Silme yok (soft delete bile değil; durum).
- **Yetki:** her yeni eylemde `[HasPermission(WavePermissions.*)]` (`release` → `Release`, `tasks` → `Read`); sistem komutlarının HTTP ucu yok.
  `AuthorizationGuardTests`, `WarehouseRegistrationTests` ve 0180 alt ad alanı vakası (AC-5) değişmeden yeşil.
- **Denetim (§8.13, §21):** `ReleaseWaveCommand`, `CompleteWaveReleaseCommand` (yalnız yazacak şey varsa), `ReleasePendingOrdersCommand`
  (parti bir şey değiştirdiyse 1, yoksa 0) `ICentralAuditDeclaration`; 0178 port yazımları çağıran 0180 komutunun kaydına **enlist** (ayrı kayıt yok);
  C1 + ilk tur = `UpdateWaveWarehouseSettingsCommand`'ın tek kaydı. Başarısız 0, replay 0. `ShipToFilter` ve adres log / OTel'e yazılmaz.
- **İşlem:** 0180 yazımı `WithTransactionAsync`; olay + iş yazımı + enlist aynı işlemde; 0178 port kendi işleminde, sipariş düzeyinde hepsi ya da
  hiçbiri (AC-20). 0178 koleksiyonlarına 0180'den yazım yok, okuma yalnız `Find` (AC-16 bekçisi DL-2 kodunu da tarar).
- **Dil:** DL-2 kullanıcıya görünen metin eklemez (hata iletileri sözleşme kodlu, ekran DL-4). Yeni `Perm.Entity` satırı yok; vitest yine koşar.
  Görünen metin gerekirse **dur** (kiracı modülü: 7 dil).
- **Korunan yollar ve XMC-001:** §1.2 tablosu dışında 0178 / 0173 / Web / Gateway dosyasına dokunma; gerekirse dur, CT'ye sor. `ocelot.json` ve
  X-14 satırı yalnız integration-agent. Donmuş sözleşmeler ve `wms-wave-bundle` taslağı: taslakta düzeltme gerekirse CT'ye öner, yazma.
- **Git:** `main`'e commit / push yok; push ve PR **yok** (modül sonunda ayrı onayla); stage yalnız açık dosya yoluyla (`git add -A` / `git add .`
  yok); hook ve imza atlanmaz (`--no-verify` yok); yıkıcı git komutu yok. Yerel commit serbest; iletiler `feat(sce): MOD-0180 DL-2 — …`.
- **Commit sırası:** (1) X-1 / X-2 / X-5 + X-8 DTO'ları (donmuş sözleşmeyle aynı anda yeşil) → (2) X-3 (a) kurucu taşıma (davranış değişmez;
  0178 S2 testleri değişmeden yeşil) → (3) X-3 (b) + X-4 port + `WavePickIssuancePolicy` + DI (AC-18 betiği) → (4) rota (saf) → (5) serbest
  bırakma komutları + X-10 → (6) controller uçları + X-14 (**tek commit**, integration-agent adımı) → (7) kapatma (§8.19) + bakım döngüleri +
  `ReleasePendingOrdersCommand` + X-10 → (8) D-2 ve (a)…(d).

## 4. Test ve kanıt

- **AC (DL-2, §24):** AC-18…AC-28 (AC-27 (d) dahil); AC-15 (f)…(q) (§2 madde 3 ayrımıyla); AC-12 bakım servisi listesi; AC-55 0178 sayı vakası.
  AC-15 bakım turu vakaları üretim tur yöntemini çağırır, komutu doğrudan göndermez.
- **Sabotaj (adıyla, paket §24 DL-2):** S-18, S-19, S-19b, S-20, S-21a, S-21b, S-22, S-23, S-24, S-25, S-26, S-27, S-28; S-15b, S-15d…S-15v
  (S-15c hariç — DL-1), S-15w…S-15z, S-15aa…S-15al, S-15am…S-15ar, S-15as…S-15ba ve S-15bc (S-15bb DL-3), S-15bd, S-55c; ek **S-D2** (§1.3).
  Her biri: değişiklik + kırmızı test adı + çıktı özeti → **bayt-eşit geri al** (sha256) → yeşil; `git status` temiz. Hiçbiri commit'e girmez.
  Pakette test adı yoksa kırmızıya dönen testin adını yaz.
- **InventoryService takımı filtresiz** (`dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests`, `~filter` yok):
  `INVENTORY_TEST_MONGO` **boş** (takım kendi replica set'ini açar), 27017 **kullanılmaz**. Dilim başı ve sonu sayıları (geçti / kırmızı /
  atlandı); taban kırmızıları adıyla ve aynı gün `main`'de ölçülerek ayrılır.
- **Gateway** `gateway/Diten.ApiGateway.Tests` — yeni sayıyla yeşil; `git diff <başı>..<sonu> -- …/Mod0178WarehouseRouteTests.cs` yalnız sayı satırı
  + yorum.
- **Web vitest** `npx vitest run tests/role-assignments-label-ledger.test.js` (`frontend/Diten.Web`) — `release` anahtarıyla yeşil (tavan değişmez).
- **AC-18 kanıtı:** §0 madde 5 referans sha256'sı = dilim sonu sha256'sı; `git diff --stat <başı>..<sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse` **boş**.
- **AC-27 bütçeleri** gerçek replika setinde ölçülür, sayılar rapora (a)…(d); (d) en kötü durum fikstürüyle (200 sipariş hepsi Taslakta `Planned`).
- `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşulmaz**.

## 5. Rapor

1. Paket sha256 ölçümü, dal adımı (ata / ff / no-ff), §0 madde 3 kapı ölçümü (WMS-BUNDLE sürümü).
2. Commit listesi (SHA + ileti); uç commit'inde X-14 satırının birlikte olduğu açıkça; commit başına filtresiz takım sayısı (§1.3 d).
3. Sayaçlar önce → sonra: `Assert.Equal(<n>, endpoints.Count)`, `WriteCommands.Length`, `seen` (satırın kendisiyle atıf, satır numarasıyla değil).
4. Takım sayıları: InventoryService (önce / sonra, kırmızılar adıyla), Gateway, vitest.
5. AC tablosu: AC-12, AC-15 (f)…(q), AC-18…AC-28, AC-55 — test adı + sonuç; §2 madde 2–3'teki "bekliyor" parçaları ayrı satırda.
6. Sabotaj tablosu (her kalem: değişiklik, kırmızı test + çıktı, geri alma sha256, yeşil).
7. AC-18 referans / dilim sonu sha256; AC-27 ölçümleri; `git diff --stat` kanıtları.
8. D-2 ve (a)…(e) her biri için ne yapıldı ve kanıt.
9. MOD-0182 FIX-01 / MOD-0178-FU01 DL-2 ile çakışma olduysa nasıl çözüldüğü; açık noktalar ve CT'ye sorular (taslak sözleşme düzeltme önerileri dahil).

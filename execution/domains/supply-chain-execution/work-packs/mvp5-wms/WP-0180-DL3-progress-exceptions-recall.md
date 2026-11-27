# WP-0180-DL3 — MOD-0180 Dalga Planlama DL-3: ilerleme + durum uzlaştırması + istisnalar + riskli dalga + geri çekme (arka uç)

Tarih: 2026-11-14 · Veren: CT (MVP-5 WMS CT, sahip Beste) · Durum: **taslak — başlatılabilir; tek kapı CT etiketi `ct/line-ff-2026-11-14h` (§0 madde 1)**
- Modül: **MOD-0180 — Wave Planning**, servis `Diten.InventoryService` (5067), kiracı modülü. Sahip: geliştirici oturumu. Gateway sayı adımı:
  **integration-agent** (X-14).
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0180-wave-planning.md` **r11**, LF sha256
  `226ab7f9f3690c422fba1bf5a3008d004d82791cfe6b20ebb2b74c0cd47cb095` (CT ölçümü, HEAD `6084f84b6`), `status: ready-for-dev`; onay satırı
  `execution/approvals/approvals.tsv` (onaylayan Ali / `GMG-ERP`, 2026-11-13, yazan `bestepullukcu-alt`; sha sütunu `226ab7f9…` ile **eşleşir**) —
  commit `e12bf26c8`. Dilim: **DL-3** (paket §23), kanıt seviyesi **E4**.
- DL-2: **VERIFIED** — `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md` "Ek — MOD-0180 DL-2" (commit `6084f84b6`; dev
  commit'leri `a5500a23a` … `1309b27e3`). DL-2'den taşınanlar §1.3'te.
- **Kod izni:** paket §23 DL-3'ü ayrı revizyon istemeden tanımlar; §24 DL-3 kapısı "X-15 sahip teyidi (`recall` fiili)" **kapalı** —
  `docs/records/decisions/2026-11/mod0180-wave-planning-owner-decisions-03.md` "X-15 Governance Rol İzinleri köprüsü" (Beste 2026-11-13, "evet":
  "DL-3: yeni fiil `recall` (aile önerisi `secondary`) + satır"). Bu yüzden WP **BEKLEMEDE değildir**.
- Yetki: paket > `domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse paket geçerlidir; çelişkiyi CT'ye yaz, kendin çözme.
  Bu WP push, PR ya da `.antigravity/**` değişikliği izni değildir.

## 0. Başlamadan

1. **Dal:** tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` (ana checkout `C:\Users\Ali\erp-vnext`); yeni dal açma. CT etiketi
   **`ct/line-ff-2026-11-14h`** (CT oluşturacak; WP yazıldığında yok — son etiket `ct/line-ff-2026-11-14g`): etiket dalın atasıysa devam; değilse
   `git merge --ff-only ct/line-ff-2026-11-14h`; olmazsa `git merge --no-ff ct/line-ff-2026-11-14h`; çakışmada `git merge --abort` ve CT'ye yaz
   (çakışmayı kendin çözme). Etiket yoksa **dur**.
2. **Paket sha'sı:** `tr -d '\r' < <paket> | sha256sum` → `226ab7f9…` ile başlamalı; ölç ve raporun ilk satırına yaz. Farklıysa **dur**.
3. **Ortam:**
   - Ana checkout'ta derleme / test **yok** (Inventory 5067 oradan `dotnet watch` ile koşabilir); derleme, test ve sabotaj **ayrık (detached)
     worktree**'de.
   - `INVENTORY_TEST_MONGO` **boş** (takım kendi replica set'ini açar), 27017 **kullanılmaz**;
     `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`.
   - vitest yalnız `C:\Users\Ali\erp-vnext\frontend\Diten.Web\node_modules` ile (ayrık worktree'de junction; worktree'yi kaldırmadan önce
     junction'ı tek başına `rmdir`).
   - `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşulmaz**.
4. **Eşzamanlılık:** aynı InventoryService'te başka dilim sürüyorsa (`git log ct/line-ff-2026-11-14h -- services/Diten.InventoryService` ile dilim
   başında ölç, rapora yaz) her commit'ten hemen önce hattı al; çakışırsa iki tarafın eklemesini koru, CT'ye yaz.
5. **Ortak sayaçlar dilim başında ölçülür** (o anki hat ucunda; aşağıdakiler CT'nin `6084f84b6` ölçümüdür):
   | Sayaç | Dosya / satır | `6084f84b6` | DL-3 beklenen (çıkarım; kesin sayı farktan) |
   |---|---|---|---|
   | Gateway uç sayısı (X-14) | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` `Assert.Equal(<n>, endpoints.Count)` | **105** | +5 (`recall`, `exceptions` ×2, `resolve`, `dashboard` — paket §5.1 X-14 dağılımı + taslak `:278-282`) → 110 |
   | Merkezi denetim yazma komutu (X-10) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` `Assert.Equal(<n>, WriteCommands.Length)` | **136** | +3 (`RecallWaveRemainingCommand`, `ResolveWaveExceptionCommand`, `ReconcileWaveCommand` — §3 12 komutun kalanı) → 139 |
   | Merkezi denetim `seen` (X-10) | aynı dosya `Assert.Equal(<n>, seen)` | **16** | yalnız yeni işlem açan 0180 deposu kadar (yoksa değişmez) |
   | `wms_wave_*` bildirimi (X-9) | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` `WaveCollections.All` `Assert.Equal` listesi | 4 ad | + `wms_wave_exceptions` → 5 |
   | Kayıt kodu önek listesi | `WarehouseRecordCodeRules.TypePrefixes` | 7 | DL-3 önek eklemez |
6. **AC-18 dilim başı:** `git rev-parse HEAD` (dilim başı) rapora; dilim sonunda `git diff --stat <başı>..<sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse` **boş** olmalı (Açık 1 için 0178 dosyasına izinle dokunulursa istisna adıyla raporlanır, §1.4).
7. Paketi oku: §3 (WaveException, komutlar, API), §4.3 (olay türleri), §4.4, §4.5 (risk / bekleyen görev eşikleri), §4.6 (`wms_wave_exceptions`
   index'leri), §5, §5.1 X-9 / X-10 / X-14 / X-15, §8.3, §8.6 adım 4–5, §8.8, §8.9, §8.10, §8.11, §8.12, §8.13, §8.17, §8.19 (geri çekmenin kapalı
   depo dalı — §8.9 son madde), §14 (`recall`, `exceptions.read`, `exceptions.resolve`), §16 AC-15 (k) ve (r), AC-22, AC-29…AC-38, AC-55, AC-56,
   §17, §21, §24 DL-3 satırı. Taslak sözleşme: `docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md` (`recallWaveRemaining`,
   `getWaveDashboard`, `listWaveExceptions`, `getWaveException`, `resolveWaveException` `:278-282`; `WaveRecallResult`, `WaveDashboard`,
   `WaveException` `:210-216`; kural 6 `WaveException` If-Match `:41`).

## 1. Kapsam

### 1.1 Paket DL-3 satırı (§23, §24)
"İlerleme + durum uzlaştırması + istisnalar + riskli dalga + geri çekme". Kapsadığı bölümler: §8.8, §8.9, §8.10, §8.11, §8.17, §21.
§24 DL-3: seam'ler X-9, X-10, X-14, X-15; AC-29…AC-38, AC-15 (r), AC-55 0178 sayı vakası, AC-56 tekrar; sabotajlar S-29…S-38 (S-30b, S-30c
dahil), S-15bb, S-55c, S-56.

- **Komutlar (§3, §21):** `RecallWaveRemainingCommand` (izin `warehouse.waves.recall`, If-Match, Idempotency-Key, gerekçe 10–500),
  `ResolveWaveExceptionCommand` (izin `warehouse.waves.exceptions.resolve`, If-Match, gerekçe 10–500; eylemler `RemoveOrder` \| `MoveToNextWave` \|
  `Ignore`), `ReconcileWaveCommand` (yalnız uzlaştırma döngüsü; HTTP ucu yok; yalnız yazacak şey varsa yazar, `Version` koşullu).
- **Sorgular:** `GetWaveDashboardQuery`, `GetWaveExceptionListQuery`, `GetWaveExceptionByIdQuery`; ilerleme (§8.8 üç yüzde) ve `atRisk` (§8.11)
  okuma anında hesaplanır — dalga okumalarında (liste / ayrıntı) ve panoda; 0178 koleksiyonlarına yalnız `Find`.
- **Uçlar (taslak `:278-282`):** `POST /waves/{waveId:guid}/recall`, `GET /waves/dashboard`, `GET /waves/exceptions`,
  `GET /waves/exceptions/{exceptionId:guid}`, `POST /waves/exceptions/{exceptionId:guid}/resolve`. Sabit kesimler (`dashboard`, `exceptions`)
  parametreli yoldan önce eşleşir. PUT / DELETE yok. İstisna uçları `WaveExceptionsController`'da (§3; aynı namespace
  `Diten.InventoryService.Api.Controllers.WavePlanning` — X-17 satırı yeniden gerekmez).
- **Varlık:** `WaveException` (§4.4) — koleksiyon `wms_wave_exceptions` (`WaveCollections`), tekil `(TenantId, LegalEntityId, SubjectKey)` +
  `(TenantId, LegalEntityId, WarehouseId, Status, DetectedAt)`; `Version` (If-Match, §8.12).
- **İzinler (§14):** `WavePermissions.Recall`, `.ExceptionsRead`, `.ExceptionsResolve` → `Implemented` ve manifest `ApiPermissions`
  (sayfalar DL-4'te; `Pages: []` kalır, `NavManifestL10nGuardTests` `PagelessUntil` `WAVE-PLANNING` satırı **değişmez**).
- **Uzlaştırma döngüsü (§8.8):** DL-2'nin `RunReconcileOnceAsync`'i (bugün yalnız kurtarma) açık dalgalar (`Released` \| `Picking`) için kayıt
  başına `SetScope` ile `ReconcileWaveCommand` gönderir; iki döngü ayrı kalır (AC-15 r).
- **AC-12 mimari testi:** izinli küme `CompleteWaveReleaseCommand` + `ReleasePendingOrdersCommand` + `ReconcileWaveCommand` (liste DL-2'de
  yazıldı, `WaveArchitectureTests.cs` `allowed`); `ReconcileWaveCommand` artık gerçekten gönderilir; değişmeden yeşil.

### 1.2 XMC-001 ve ortak dosya dokunuşları (yalnız eklemeli; başka 0178 / 0173 / Web / Gateway dosyası gerekirse **dur**, CT'ye sor)

| # | Dosya | DL-3 değişikliği | Onay |
|---|---|---|---|
| X-9 | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` — `WaveCollections.All` `Assert.Equal` listesi | `wms_wave_exceptions` eklenir (koleksiyon sabitiyle **aynı commit**; `declared` `WaveCollections.All` üzerinden kendiliğinden) | kararlar 03 "X-6, X-9, X-10, X-11" ("DL-3 X-9 bu onayın içindedir") |
| X-10 | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` | `WriteCommands.Length` = dilim başı + DL-3 yazma komutları (beklenen +3); `seen` yalnız yeni işlem açan depo kadar; her artış komutuyla aynı commit | aynı kayıt (sayı = dilim başı + dilimin komutları kuralı) |
| X-14 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — yalnız `Assert.Equal(<n>, endpoints.Count)` + üstündeki yorum | dilim başı + DL-3 uçları; **integration-agent** yazar, uçlarla **aynı commit** | `c7dc4ea6a` + `6235794ab` (uç ekleyen her dilim) |
| X-15 | `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml` (`ActionVerbs`), `frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx`, `frontend/Diten.Web/wwwroot/assets/js/Governance/RoleAssignments/perm-label.js` (`ACTION_FAMILIES`) | yeni fiil **`recall`**: köprüye `["recall"] = Localizer["ActionVerb_Recall"].Value`, 7 resx'e `ActionVerb_Recall` (en dışı değerler en'den farklı), `perm-label.js` `secondary` ailesinin (`bx-undo`, bugün `:55`) `words` listesine `recall` | kararlar 03 "X-15" (Beste 2026-11-13; MOD-0181 emsali `65b968b64`) |
| §5 | `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` | `Perm.Entity.<ROW>` — `warehouse.waves.exceptions.*` satırı (beklenen `WAVESEXCEPTIONS`, **çıkarım**; kesin kodu üretim `PermLabel.split` verir, vitest ile ölç); `warehouse.waves.recall` satırı `WAVES` (DL-1'de var) — **7 dil** | paket §5 (`Perm.Entity.*` DL-3) + X-15 kaydı |

Değişmez (bağlayıcı): `INVT/Warehouse/**` (Açık 1 için izinli istisna hariç, §1.4), 0178 adlı mimari testler,
`InventoryLedgerManifestCompletenessTests.cs` (X-17 yalnız DL-1), `LedgerImmutabilityArchitectureTests` PUT / DELETE listesi, `ocelot.json`,
donmuş sözleşmeler, `wms-wave-bundle` taslağı (düzeltme gerekirse CT'ye öner), `.antigravity/**`, Building.Blocks (merkezi denetim dahil),
AuthService, `NavManifestL10nGuardTests.cs`, `PageHelpCatalog.cs`, `PageHelpResource.*`, `Nav.Page.*` (DL-4).
**0178 kodu:** DL-3, DL-2'de yazılan `IWavePickIssuance.WithdrawAsync`'i **çağırır**; `WavePickIssuanceService.cs` ya da başka bir 0178 dosyasında
değişiklik gerekirse (port hatası dahil) **dur** — X-4 paket §5.1'de yalnız DL-2'dir, DL-3'te yeni XMC-001 sorusudur.
0178 verisinin okuması (`wms_tasks`, `wms_task_events`, `wms_outbound_shipments`) yalnız 0180 Persistence'ta `Find` ile (AC-16 bekçisi DL-3 kodunu da tarar).

### 1.3 DL-2'den taşınanlar (kabul kaydı "Bekleyen DL-3 parçaları" + DL-2 WP §2 madde 2–3)
- **AC-22 (b) / (c) istisna kayıtları ve §8.6 adım 4–5:** serbest bırakmada `Ineligible` → üyelik `Excluded` **+ istisna** `OrderNotIssuable`
  (kod `OutboundCancelled` ise `OrderCancelledInWave`), diğer siparişler sürer. Hiç `Issued` yoksa (`ReleaseAborted`) üyelikler `Removed`'a çevrilir
  ama **denemenin istisnaları kalır** (§8.6 adım 5). Vaka DL-2'nin aynı fikstürüyle (`WaveReleaseTests` `AC22_b_…`, `AC22_c_…`) tamamlanır:
  (b)'de tek `OrderCancelledInWave` (`SubjectKey`, `OutboundId`, `Status = Open`), (c)'de `ReleaseAborted` sonrasında istisna `Open` kalır.
  İstisna 0180 işleminde, `Excluded` üyelikle **aynı işlemde** yazılır. **Kurtarma yolu:** `CompleteWaveReleaseCommand` aynı adım 4–5'i izler
  (çıkarım — §8.6 adım 4 "tamamlama … aynı isteklerle"); kurtarmada `Ineligible` dönen sipariş için de istisna açıldığını ölçen vaka yaz.
  CT ek sabotajı **S-22e:** istisna yazımını serbest bırakma yolundan kaldır → (b) / (c) istisna iddiası kırmızı.
- **AC-15 (k) kalan parçaları:** kapalı depoda "Sonraki dalgaya taşı" 422 `WAVE_SETTINGS_REQUIRED` (§8.10, ekleme adımı ayar kaydına koşullu
  yazımla sıralanır — §8.19 "Sıralama"; çıkarma başarılıysa sipariş serbest kalır, istisna `Resolved` olmaz, cevap bunu söyler) ve (h) ölçüldükten
  sonra ayrı vaka: W2'de geri çekme 200, çekilen O4 `Deferred` olur ve **bir sonraki bakım turunda** (`RunPendingReleaseOnceAsync` üretim yöntemi)
  dalgasız görev alır (§8.9 son madde). CT ek sabotajı **S-15k2:** taşıma eyleminin ayar koşulunu kaldır → 422 vakası kırmızı.
- **X-7** (Web `PickTasks` süzgeci) DL-4'tedir; DL-3'e girmez.

### 1.4 CT ek şartı — Açık 1 (kabul kaydı): tek seferlik kırmızıların kök nedeni (dilim başında, kod yazmadan önce)
DL-2 kabulünde iki vaka birer kez kırmızı, tek başına 5/5 yeşil: `INVT/WavePlanning/WaveReleaseTests.cs` `AC22_c_when_nothing_is_issued_…`
(son iddia `Assert.Equal((0, 1), s.Central("ReleaseWaveCommand"))`) ve 0178 `INVT/Warehouse/PutawayAccessAuditTests.cs`
`AC15_AC48_create_confirm_claim_update_cancel_…` (`AssertForwarded`: `Status ∈ {Pending, Delivered}` ve `ConfirmedByCommand = true`). İkisi de
merkezi denetim satırının durumunu **cevaptan hemen sonra** okur.

**CT'nin kod okuması (çıkarım, doğrulanmadı — geliştirici ölçer):**
- `CentralAuditBehavior` satırı işlem içinde `AwaitingCommand` yazar; komut bitince `CentralAuditRecorder.CompleteAsync` / `AbandonAsync` onu
  `MoveAsync` ile `Pending` / `Abandoned` yapar — süzgeç `Status == AwaitingCommand` (`CentralAuditOutboxStore.cs` `MoveAsync`), hata
  `TryAsync` içinde **yutulur** (yalnız `"Central audit: the {What} could not be written after the command ended"` log satırı).
- Aynı satırı dağıtıcının `ReleaseOrphansAsync`'i (`CreatedAtUtc < now − OrphanAfter`, `TimeProvider` saatine göre) `Pending` +
  `ConfirmedByCommand = false` yapabilir; o zaman `MoveAsync` hiçbir şey eşlemez. Bu, iki belirtiyi de açıklar: AC22_c `(1, 0)` görür,
  AC15_AC48 `ConfirmedByCommand = false` görür.
- Adaylar: **H1** `MoveAsync` yazımı yük altında başarısız ve yutuldu; **H2** test host'unun saati (paylaşılan / ileri alınmış `TimeProvider`)
  dağıtıcıya satırı yetim gösterdi; **H3** dağıtıcının teslim / ret geçişi (`Rejected`) okuma anına denk geldi.

**Yapılacak:** (1) iki vakayı tam takım yükü altında tekrarla (ör. 30–50 tekrar ya da paralel yük), kırmızı anında satırın `Status`,
`ConfirmedByCommand`, `LastError`, `CreatedAtUtc` ve test host saatini yakala (yalnız 0180 dosyasında tanı kodu; commit'e girmez ya da kalıcı
tanı iletisi olarak 0180 testinde kalır). (2) Kök nedeni kanıtla yaz (H1 / H2 / H3 ya da başka). (3) Düzeltme **yalnız 0180 dosyasında** ise
(`WaveReleaseTests`, `WaveSeamTestInfrastructure`, `WaveTestInfrastructure` vb.) DL-3'ün ilk commit'i olur. **0178 dosyasına
(`PutawayAccessAuditTests.cs`, `PutawayTestScenario.cs`, `WarehouseApiFactory` …) ya da Building.Blocks'a dokunmak XMC-001 sorusudur — dokunma,
CT'ye kanıtla öner.** (4) Kök neden üretim davranışıysa (ör. onay yazımının yutulması) düzeltme bu dilimde **yapılmaz**; CT'ye bulgu olarak yaz.

### 1.5 Bilinen açık B2 (sahip kararı bekliyor) — DL-3'e etkisi (CT ölçümü, kod okuması `6084f84b6`)
- **B2 nedir (CT okuması):** 0178 tahsis planlayıcısı `OutboundAllocationPlanner` bin dağıtımında yalnız **açık görevlerin** kalanını düşer
  (`OutboundAllocationPlanner.cs` `WorldAsync`: `_tasks.OpenTasksFromAsync(...)` → `OpenRemaining`; dağıtım `usable = onHand − OpenRemaining −
  Planned`). `Deferred` outbound'un görevi yoktur; kalıcı planı (`PlannedBins[]`) düşülmez. Miktar düzeyinde rezervasyon (`GetScopeFreeAsync`)
  korunur; çakışma **bin dağıtımı** düzeyindedir — sonraki bir outbound aynı bin miktarına planlanabilir.
- **DL-3'ü bekletir mi:** **hayır.** DL-3'ün hiçbir AC'si bin dağıtımını iddia etmez; düzeltme 0178 planlayıcısındadır (XMC-001 + sahip kararı) ve
  DL-3 kapsamında yapılmaz.
- **DL-3'ün B2'yle etkileşimi (bilinmesi gerekenler):** (a) geri çekme (AC-31) ve istisna "Siparişi çıkar" / "taşı" (AC-36) çekilen outbound'u
  `Deferred`'a döndürür, görevleri `Cancelled` olur → o outbound'un bin miktarı da planlayıcıdan düşülmez olur; DL-3 B2'nin etki alanını
  **genişletir**. (b) B2 üretimde fazladan `Refused` (`INSUFFICIENT_STOCK`) onaya → `StockMissing` istisnasına ya da kısa toplamaya
  (`ShortPick`) yol açabilir; DL-3'ün tespiti bu durumda da doğrudur. (c) DL-3 fikstürleri aynı bin miktarını paylaşan birden çok `Deferred`
  outbound'a dayanmamalı (dayanırsa vakada adıyla yaz).
- **Ölç ve raporla (bekçi değil, kanıt):** bir vaka — iki outbound aynı bin'de, biri dalgada `Issued` sonra geri çekildi (`Deferred`), ardından
  yeni bir outbound yaratıldı → yeni planın `PlannedBins`'i çekilen siparişin bin miktarını düşüyor mu? Sonucu rapora yaz (B2 sahip kararına
  girdi). Bu vaka kırmızı / yeşil kabul şartı değildir; B2'yi düzeltmeye çalışma.

### 1.6 Kapsam dışı
DL-4: ekranlar (pano, ayrıntı, planlayıcı, istisna kuyruğu), `Nav.Page.*`, sayfa yardımı, X-7, manifest sayfaları. DL-5: KPI / CSV.
DL-6: `Mod0180WaveRouteTests`. X-13 rol şablonu (Ali). WMS-WAVE-BUNDLE dondurma (DL-5 sonrası CT adımı). B2 düzeltmesi.

## 2. Kararlar ve açık CT noktaları

Kapalı (uygulanır): D-5 (ilerleme okuma anında, yazma yok), D-6 (risk eşikleri depo ayarı), D-9 (iptal yalnız `Draft`; geri çekme; `CutoffMissed`
otomatik kapanış yok), D-10 (`LotUnderRecall` otomatik çıkarma yok), D-11 (`PendingTaskMinutes` varsayılan 30), D-13 (taşıma hedefi), D-14
(If-Match, CAS), Q-10 = A (kısa toplama `Completed`'ı engellemez), Q-11 = A (uzlaştırma 60 sn), Y-2 (`Excluded` / `OrderCancelled` →
`PartiallyCompleted`), X-15 (`recall` fiili, aile `secondary`), X-9 / X-10 / X-14 DL-3 artışları sahip onaylı. Taslak `WaveRecallResult`
kısmi hatayı `failed[] { outboundId, code }` ile 200'de taşır (paket §8.9 "sözleşme taslağında kesinleşir" — taslak uygulanır).

**Açık — CT kararı gerekir (geliştirici kendisi seçmez; karar gelene dek ilgili vaka "bekliyor" diye raporlanır):**
1. **Serbest bırakma istisnasının `DetectedAt`'i:** §4.4 "tespit eden uzlaştırma turunun zamanı" der; serbest bırakmada açılan istisna için
   CT önerisi (çıkarım): serbest bırakma işleminin zamanı (R2/R3). Farklı uygularsan dur, sor.
2. **Aynı outbound için ikinci `OrderNotIssuable`:** `ReleaseAborted` sonrası outbound yeniden eklenip yeniden `Ineligible` olursa `SubjectKey`
   (`{Kind}:{waveId}:{outboundId}`) aynıdır → ikinci kayıt açılmaz (tekillik). İlk istisna `Ignored` ise yeniden açılmaz. CT önerisi: paket
   lafzı (tekil; yeni kayıt yok) uygulanır, davranış raporda vaka olarak gösterilir.
3. **`perm-label.js` ailesi:** kayıt "aile önerisi `secondary`" der; WP `secondary` uygular. CT kabulünde teyit edilir.

## 3. Kurallar

- **Kiracı:** her okuma / yazma `TenantId` + `LegalEntityId` (sunucuda) + `WarehouseId`; başka kiracı / şirket **404** (`WAVE_NOT_FOUND`,
  `WAVE_EXCEPTION_NOT_FOUND`); gövde / sorguda kapsam alanı 400. Uzlaştırma kayıt başına `SetScope`; sabit / varsayılan kiracı yok. Silme yok
  (istisna `Ignored` / `Resolved` durumdur).
- **Yetki:** `[HasPermission]` — `recall` → `Recall`, `exceptions` / `exceptions/{id}` → `ExceptionsRead`, `resolve` → `ExceptionsResolve`,
  `dashboard` → `Read`. `ReconcileWaveCommand`'ın HTTP ucu yok. `AuthorizationGuardTests`, `WarehouseRegistrationTests`, AC-5 alt ad alanı vakası ve
  0180 manifest bütünlüğü (manifest = `Implemented` = zorlanan) değişmeden yeşil.
- **Denetim (§8.13, §21):** `RecallWaveRemainingCommand` (LifecycleTransition), `ResolveWaveExceptionCommand` (Execute, nesne `WaveException`),
  `ReconcileWaveCommand` (Execute; **yalnız yazacak şey varsa** — yazmayan tur 0 kayıt) `ICentralAuditDeclaration`; `WithdrawAsync` 0178 yazımları
  çağıran 0180 komutunun kaydına **enlist** (ayrı kayıt yok). Gerekçe ve değerler `wms_wave_events.Data`'da (dalga akışı; istisna eylemleri
  `ExceptionResolved` / `ExceptionIgnored`, tespit `ExceptionRaised`, kendiliğinden kapanış `ExceptionCleared`). Başarısız 0, replay 0.
- **İşlem / CAS:** 0180 yazımı `WithTransactionAsync`; olay + iş yazımı + enlist aynı işlemde; uzlaştırma yazımı `Version` koşullu, kullanıcı
  yazımıyla yarışta kaybeden yeniden okur (§8.12). `CompletedAt` = dalga görevlerinin terminal olaylarının (`wms_task_events.AtUtc`) en büyüğü.
- **Dil:** DL-3 ekran eklemez; yalnız §1.2 X-15 / `Perm.Entity` resx satırları — **kiracı modülü, 7 dil** (en, tr, fr, es, zh, ar, ru); bir dil
  eksikse iş bitmemiştir. Hata iletileri sözleşme kodlu. Başka görünen metin gerekirse **dur**.
- **Korunan yollar ve XMC-001:** §1.2 tablosu dışında 0178 / 0173 / Web / Gateway / Building.Blocks dosyasına dokunma; gerekirse dur, CT'ye sor.
  `ocelot.json` ve X-14 satırı yalnız integration-agent. Taslak sözleşmede düzeltme gerekirse CT'ye öner, yazma.
- **Git:** `main`'e commit / push yok; push ve PR **yok** (modül sonunda ayrı onayla); stage yalnız açık dosya yoluyla (`git add -A` / `git add .`
  yok); hook ve imza atlanmaz (`--no-verify` yok); yıkıcı git komutu yok. Yerel commit serbest; iletiler `feat(sce): MOD-0180 DL-3 — …`.
- **Commit sırası (her commit derlenir; her commit için InventoryService filtresiz koşu sayısı rapora — kırmızı pencere yok kanıtı):**
  (0) Açık 1 düzeltmesi (yalnız 0180 dosyasıysa) → (1) `WaveException` varlığı + enum'lar + `WaveCollections.Exceptions` + index'ler + X-9
  (**tek commit**) → (2) saf kurallar: istisna tespit koşulları, risk, ilerleme, uzlaştırma geçişleri (birim) → (3) `ReconcileWaveCommand` +
  uzlaştırma döngüsünde gönderim + X-10 (+1) → (4) serbest bırakma / kurtarma istisnaları (§1.3, AC-22 b/c) → (5) `RecallWaveRemainingCommand`,
  `ResolveWaveExceptionCommand` + sorgular + X-10 (+2) → (6) controller uçları + `WavePermissions` DL-3 anahtarları + manifest `ApiPermissions` +
  X-15 köprü + `Perm.Entity` 7 dil + X-14 (**tek commit**, integration-agent adımı; manifest anahtarı, resx satırı ve uç sayısı aynı anda yeşil)
  → (7) AC-15 (k) kalanları ve AC-15 (r) host vakası.

## 4. Test ve kanıt

- **AC (DL-3, §24):** AC-29, AC-30 (Y-2 ve F-7 vakaları dahil), AC-31, AC-32, AC-33, AC-34, AC-35, AC-36, AC-37, AC-38; AC-15 (r) (üretim
  `WaveMaintenanceHostedService` iki döngüsüyle, aralıklar test yapılandırmasıyla 1 sn, kanca tutulurken ≤ 10 sn'de W5 `Completed`); AC-55 0178 sayı
  vakası (dilim başı → sonu); AC-56 tekrar (`recall` köprüde, 7 resx'te, `secondary` ailesinde; defter tavanı "fiil 0 · satır 96" **değişmez**).
  **Taşınanlar (§1.3):** AC-22 (b) / (c) istisna parçaları, AC-15 (k) taşıma 422 ve W2 geri çekme 200 + bakım turunda dalgasız açılış.
  **Değişmeden yeşil:** AC-1 (taslağa karşı sözleşme testi — DL-3 işlemleri dahil), AC-5, AC-9, AC-12, AC-16, AC-17 (`wms_wave_exceptions`
  bildirimli), AC-18 (`git diff` kanıtı), DL-1 / DL-2 vakaları.
- **Sabotaj (adıyla, paket §24 DL-3):** S-29, S-30, S-30b, S-30c, S-31a, S-31b, S-32, S-33, S-34, S-35, S-36, S-37, S-38, **S-15bb** (iki döngüyü
  tek döngüde birleştir → AC-15 (r) kırmızı), S-55c (geçici `[HttpGet("waves/count-sabotage")]` → 0178 Gateway testi kırmızı), S-56 (DL-3 biçimi:
  `RoleAssignmentsIndex.ar.resx`'ten `ActionVerb_Recall`'ı sil → vitest kırmızı); CT ekleri **S-22e**, **S-15k2** (§1.3).
  Her biri: değişiklik + kırmızı test adı + çıktı özeti → **bayt-eşit geri al** (sha256) → yeşil; `git status` temiz. Hiçbiri commit'e girmez.
  Pakette test adı yoksa kırmızıya dönen testin adını yaz.
- **InventoryService takımı filtresiz** (`dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests`, `~filter` yok): dilim
  başı, her commit ve dilim sonu sayıları (geçti / kırmızı / atlandı); taban kırmızıları adıyla ve aynı gün `main`'de ölçülerek ayrılır.
- **Web.Tests filtresiz** (`dotnet test frontend/Diten.Web.Tests`): (6) commit'inde ve dilim sonunda; `NavManifestL10nGuardTests` (manifest
  `ApiPermissions` değişti, `Pages: []` — `PagelessUntil` `WAVE-PLANNING` ile yeşil) adıyla raporlanır.
- **Gateway** `gateway/Diten.ApiGateway.Tests` filtresiz — yeni sayıyla yeşil; `git diff <başı>..<sonu> -- gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs`
  yalnız sayı satırı + yorum.
- **Web vitest** (`frontend/Diten.Web`, node_modules junction): `npx vitest run tests/role-assignments-label-ledger.test.js` — `recall` ve
  `WAVESEXCEPTIONS` (ya da ölçülen kod) ile yeşil, tavan değişmez. Tam `vitest` paketi yalnız bilgi için (DL-2 kabulündeki 14 kırmızı / 11 dosya
  0180 dışıdır; yeni kırmızı 0180 dosyasındaysa raporla).
- **AC-18 kanıtı:** `git diff --stat <başı>..<sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse` **boş**
  (Açık 1 için izinli 0178 değişikliği olursa adıyla istisna).

## 5. Rapor

1. Paket sha256 ölçümü, dal adımı (ata / ff / no-ff), dilim başı HEAD, eşzamanlı dilimler (§0 madde 4).
2. **Açık 1 sonucu (§1.4):** tekrar sayısı, kırmızı anındaki satır alanları, kök neden ve kanıtı, düzeltme (dosya / commit) ya da XMC-001 önerisi.
3. Commit listesi (SHA + ileti); uç commit'inde X-14 satırının birlikte olduğu açıkça; commit başına filtresiz takım sayısı.
4. Sayaçlar önce → sonra: `Assert.Equal(<n>, endpoints.Count)`, `WriteCommands.Length`, `seen`, `WaveCollections.All` listesi (satırın kendisiyle
   atıf, satır numarasıyla değil).
5. Takım sayıları: InventoryService (önce / sonra, kırmızılar adıyla), Web.Tests (`NavManifestL10nGuardTests` dahil), Gateway, vitest.
6. AC tablosu: AC-29…AC-38, AC-15 (r), AC-55, AC-56 — test adı + sonuç; taşınanlar (AC-22 b/c istisna, AC-15 k) ayrı satırlarda; §2'deki
   "bekliyor" parçaları ayrı satırda.
7. Sabotaj tablosu (her kalem: değişiklik, kırmızı test + çıktı, geri alma sha256, yeşil).
8. `git diff --stat` kanıtları (AC-18, X-14).
9. **B2 ölçümü (§1.5)** — vaka sonucu ve DL-3 fikstürlerinin B2'ye dayanıp dayanmadığı.
10. Açık noktalar ve CT'ye sorular (taslak sözleşme düzeltme önerileri, `Perm.Entity` kesin kodu, `perm-label.js` aile teyidi dahil).

## Ek — B2 sahip kararı: Deferred PlannedBins dolu sayılır (CT, 2026-11-14)

Karar kaydı: `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md` "Ek — B2 Deferred outbound PlannedBins".
- DL-3'e ek iş (CT-B2): `OutboundAllocationPlanner.WorldAsync` bin dağıtımında `Deferred` outbound'ların `PlannedBins`'ini düşer
  (iptal / kapanmış düşülmez). XMC-001 onaylı dosyalar: `OutboundAllocationPlanner.cs`, gerekirse `OutboundShipmentRepository.cs`
  (yalnız eklemeli okuma).
- Kabul: yeni vaka "Deferred O1'in PlannedBins'i B1'de 5 iken O2 yalnız kalan miktarı B1'den planlar"; geri çekme sonrası aynı kural.
  Sabotaj S-B2-WP: düşümü kaldır → kırmızı. Önceki "B2 gözlem vakası" bu vakayla değişir.
- 0178 mevcut testleri değişmez; değişmesi gerekirse dur ve sor. Commit ayrı: `fix(sce): MOD-0178 planner counts Deferred PlannedBins (B2, owner decision)`.

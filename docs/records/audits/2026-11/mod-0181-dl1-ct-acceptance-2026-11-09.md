# MOD-0181 DL-1 — Control Tower kabulü

- Tarih: 2026-11-09. CT: MVP-5 WMS CT doğrulayıcısı (alt ajan, oturum `ca7382cf`). Doğrulama: depo dışı detached ağaç
  `C:\Users\Ali\ct-verify-fu02-dl1`, HEAD `141297097` (hattın ucu, `feature/sce/mod-0173-inventory-ledger-valuation`). Ana checkout'a dokunulmadı.
- İncelenen: `ce94f9fd1..446cb58db` — `fca238d20` (WMS-COUNT-BUNDLE v1 taslağı), `6263d10f7` (DL-1 çekirdek, arka uç), `2e37b9b0f` (AC-9 CAS testi),
  `446cb58db` (her modülü tarayan iki bekçi). `141297097` CT'nin web düzeltmesi, DL-1 değil (frontend 4 dosya; DL-1 frontend'e 0 dosya dokunur).
- Paket: `MOD-0181-cycle-counting.md` r7 @ sha256 `acf91c85…dedd` (git blob ölçüldü; CRLF çalışma kopyası farklı özet verir), onay satırı
  `execution/approvals/approvals.tsv:67` (HIGH, 2026-11-06).
- Geliştirici raporu: koordinatör iletisi (iddialar aşağıda bağımsız ölçüldü; geliştiricinin ağacı `bbfd559e2` / `992343903` dalda yok).
- Sonuç: **KOŞULLU** — arka uç DL-1 kapsamı ve kuralları tutuyor, CT'nin 13 sabotajının 13'ü kırmızı, Inventory takımı 0 kırmızı; ancak DL-1 iki
  Web takımında **üç yeni kırmızı** doğuruyor (ORTA-1, ORTA-2; tabanda yeşil). İkisi kapanmadan hat PR'ı yeşil olmaz.

## Ölçülenler — kapsam ve XMC-001

| Kontrol | Sonuç |
|---|---|
| Kapsam | 62 dosya, +4586 / −7. 54'ü 0181'in kendi yolları (pack §5): Domain/CycleCount, Application/Features/CycleCount + CycleCounts, Persistence/CycleCount, Api/Controllers/CycleCount, Api/CycleCount, Infrastructure/Authorization/CycleCountPermissions.cs, Infrastructure/CycleCount, Tests/CycleCount, taslak sözleşme |
| Pack §5 onaylı seam'ler (4 dosya + 1 yorum) | `Program.cs` (+3, manifest + `AddCycleCount()`), `WarehouseRecordCodeRules.cs` (+`CNT`, `TypePrefixes`'e eklemeli), `IWarehouseRecordCodeAllocator.cs` (yalnız belge yorumu), `SingleSystemOfRecordArchitectureTests.cs` (üç eklemeli satır; `Only_the_ledger…` listesi değişmedi), `CentralAuditDeclarationTests.cs` (101→106, sağlayıcı listesi +1, 13→14). Hepsi eklemeli, sahip MOD-0173 / MOD-0178 (MVP-1 CT, Beste). Onay kanıtı: paket §5 tablosu (`MOD-0181…md:516-528`) + `approvals.tsv:67` ("MOD-0173 / MOD-0178 bekçi testlerine XMC-001 seam'leri") |
| Pack §5 dışı iki dokunuş (`446cb58db`) | `InventoryLedgerManifestCompletenessTests.cs:223` (`.Controllers.CycleCounting` dışlaması) ve `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:90` (45→54). Onay kanıtı yalnız commit mesajı ("XMC-001, Beste 2026-11-09"); depoda karar / kabul kaydı yok → DÜŞÜK-1 |
| Bekçi gevşetiyor mu? | **Hayır (ölçüldü).** 54: tabanda (`ce94f9fd1`) 45 ile yeşil, uçta 54 ile yeşil; 9 yeni ucun her biri de "tam bir rota → 5067" assert'inden geçiyor — sıkılaştırma. Dışlama: önceki beş modülle aynı desen; telafi eden test `CycleCountArchitectureTests.AC7` sabotaj S1 / S2'de kırmızı (aşağıda). Sınırı → DÜŞÜK-2 |
| Korunan yollar | `ocelot.json`, `_Layout.cshtml`, `.antigravity/**`, Building.Blocks, MOD-0173 defter kodu, `WarehouseTask*`, `InventoryScopeResolutionMiddleware.cs`, `LedgerImmutabilityArchitectureTests.cs` **değişmedi** |
| PUT / DELETE | Yok; 9 uç GET / POST (`CycleCountsController.cs:32-117`); `LedgerImmutabilityArchitectureTests` değişmeden yeşil (AC-51 DL-1 kısmı) |

## Ölçülenler — kurallar (üretim kodu)

| Kural | Kanıt | Durum |
|---|---|---|
| `[HasPermission]` her eylemde, §14 DL-1 anahtarları | `CycleCountsController.cs:33,39,44,51,60,71,84,95,107`; `CycleCountPermissions.cs` 5 DL-1 anahtarı (`counts.read/.create/.update/.bulk-delete/.cancel`), manifest `ApiPermissions` aynı beşi (`CycleCountingManifestProvider.cs:31-38`), `Pages: []` | tutuyor |
| Kiracı + şirket her sorguda | `CycleCountRepository.cs:47-48` `Company<T>()` tek yerden; her okuma / yazma onu kullanır (`:61-118`, `:214`, `:242`); defter okumaları da `TenantId + LegalEntityId` (`:143`, `:151`, `:269`); `Stamp` (`:52-56`) | tutuyor |
| Kiracılar / şirketler arası 404 | `CycleCountAccessTests.AC5_…` (S3 / S4 sabotajında kırmızı) | tutuyor |
| Gövde / sorguda kapsam → 400 | gövde `CycleCountRequestReader.cs:16,95-96`; sorgu mevcut middleware `InventoryScopeResolutionMiddleware.cs:51,105` | tutuyor |
| Soft delete | Draft iptali `CancelCycleCountHandler.cs` (`IsDeleted`, `DeletedAt`), toplu silme `BulkDeleteCycleCountHandler.cs` `CopyForDelete`; okuma `Live()` (`CycleCountRepository.cs:50`) | tutuyor |
| Denetim | `InTransactionAsync` içinde `CentralAuditTransaction.EnlistAsync` yalnız `Affected > 0` (`CycleCountRepository.cs:169`); `ReadConcern.Snapshot + WMajority` (`:172`); yerel olay aynı işlemde | tutuyor |
| Kayıt kodu `{ŞİRKET}-CNT-000042` (RCS-001, Q-7 = B) | `CycleCountChecks.cs:134-144` MOD-0178 sayacı `NextAsync(companyCode, "CNT")`; 0181 kendi sayacını yazmaz; tekil `(TenantId, Number)` (`CycleCountIndexConfiguration.cs:316`) | tutuyor |
| Sıra replay → numara → işlem → ikiz (SB3/F-1) | `CreateCycleCountHandler.cs:29` replay, `:31-35` kurallar + şirket + numara (işlem dışında), `:63-68` işlem, `:70-74` ikiz yeniden okuma; test `SB3_F1_replay_and_refusal_burn_no_number…` | **kapandı** |
| Durum / satır durumu anahtar önekleri (SB3/F-2) | `CycleCountTextKeys.cs` ayrı `CycleCount.Status.` / `CycleCount.LineStatus.` + Web aileleri; test `SB3_F2_…` | **kapandı** (değer paritesi DL-4 / DL-5) |
| `ReadConcern.Snapshot` + `ICycleCountReleaseProbe` | tek işlemde bin + bakiye okuması, kanca, hareket kimlikleri (`CycleCountRepository.cs:249-280`, kanca `:265`); üretim `NoopCycleCountReleaseProbe` (`CycleCountServiceCollectionExtensions.cs:21`) | tutuyor |
| Bin talebi tekil kısmi index | `CycleCountIndexConfiguration.cs:350-352` `(TenantId, LegalEntityId, BinId)`, `Unique`, `PartialFilterExpression { Open: true }`; ihlal → 409 `COUNT_SCOPE_OVERLAP` (`ReleaseCycleCountHandler.cs` duplicate yakalama); stoksuz bin dahil her bin için talep | tutuyor |
| Compare-and-set (AC-9) | `CycleCountRepository.cs:214` `Version == expectedVersion` filtresi | tutuyor |
| Hardcoded kiracı | 0181 dosyalarında `Guid.Parse("…")` / sabit kiracı 0 eşleşme | tutuyor |
| Kullanıcıya görünen metin | DL-1 ekransız. API hata `message`'ları İngilizce sabit (ör. `CycleCountsController.cs:79,151`), kod alanı sözleşmedir; MOD-0178 Warehouse uçlarıyla aynı desen (çıkarım: ekran metni koddan çevrilir, DL-4). Bulgu sayılmadı | — |

## Tam takımlar (filtresiz; `INVENTORY_TEST_MONGO` yok, `DITEN_TEST_MONGOD` = mongod 9.0, 27017 kullanılmadı; AccountKindAcceptanceHost.Tests koşulmadı)

| Takım | `141297097` (CT) | Geliştirici | Kaynak ayrımı |
|---|---|---|---|
| Inventory (`Diten.InventoryService.Tests`) | **1961 geçti / 0 kırmızı / 5 atlandı** (1966, 7 dk 34 sn) | 1961 / 0 / 5 | ilk koşuda temiz |
| Gateway | **367 / 1 kırmızı** | 367 / 1 | `Mod0173Pm2InternalInventoryRouteTests.The_inventory_contract_paths_split_exactly…`: `ce94f9fd1`'de kırmızı, **origin/main `ed722a700`'de yeşil (6/6)** → MVP-1 hattının DL-1 öncesi kırmızısı, main tabanı değil (DL-1 değil) |
| TenantArchitecture | **222 / 5 kırmızı** | 222 / 5 | `DocsPathGuardTests` ×5 — `ce94f9fd1`'de ve `ed722a700`'de de kırmızı (main tabanı) |
| TenancyTests | **3 / 0** | 3 / 0 | — |
| Web.Tests | **4355 / 4 kırmızı** (4359) | koşulmamış | `CapacityPlansPageTests` + `SandopPlansPageTests` `…no_loading_title` — taban (bilinen). **`NavManifestL10nGuardTests` ×2 — yeni, DL-1** (`ce94f9fd1`'de yeşil) → ORTA-1 |
| Web JS (`npx vitest run`, tam) | **5437 / 14 kırmızı** (5451) | koşulmamış | 13 taban (`ce94f9fd1`'de aynı 11 dosyada 13). **`role-assignments-label-ledger` "every row name…": `['counts']` — yeni, DL-1** (`ce94f9fd1`'de yeşil) → ORTA-2. Yalnız 11 dosyanın yeniden koşusu uçta yine 14 (kararlı) |

`RuntimeProbe` bir test projesi değil (Exe, canlı servis yoklaması) — koşulmadı.

## CT sabotajları (`141297097`; her biri tek değişiklik, `git checkout --` ile geri alındı, sha256 eşit, ağaç temiz)

| # | Sabotaj (üretim) | Sonuç |
|---|---|---|
| S1 | `CycleCountsController.Release`'ten `[HasPermission]` silindi | kırmızı: `AuthorizationGuardTests.Every_action_carries_its_key…`, `CycleCountAccessTests.Each_endpoint_needs_its_own_key`, `CycleCountArchitectureTests.AC7_…` |
| S2 | `Release` anahtarı manifestte olmayan `cycle-count.counts.release` yapıldı (dışlamanın boşluk bırakıp bırakmadığı) | kırmızı: `AC7_…`, `AC5_…`; `InventoryLedgerManifestCompletenessTests` yeşil (dışlanmış) — telafiyi AC-7 yapıyor |
| S3 | `Company<T>()`'dan `TenantId` yüklemi düşürüldü | kırmızı: `AC5_another_tenant_or_another_company_sees_404…` |
| S4 | `Company<T>()`'dan `LegalEntityId` yüklemi düşürüldü | kırmızı: `AC5_…` |
| S5 | bin talebi index'i `Unique = false` | kırmızı: `AC13_ab_…`, `AC13_c_two_concurrent_releases…` |
| S6 | `ReplaceCountAsync`'ten `Version` filtresi kaldırıldı | kırmızı: `AC9_the_store_replaces_a_count_only_at_its_stored_version` |
| S7 | hareket kimliği okuması snapshot oturumundan çıkarıldı (`Find(movements)`) | kırmızı: `AC52a_a_posting_during_the_release…` |
| S8 | `TypePrefixes`'ten `CNT` çıkarıldı | kırmızı: 17 test (`AC1_create_answers_201…CNT…`, AC8 / AC9 / AC10 / AC11 / AC12 …) — yaratma 503 |
| S9 | `ScopeSkuIds` süzgeci kaldırıldı | kırmızı: `AC2_the_SKU_filter_limits_the_lines` |
| S10 | gövde kapsam üyesi denetimi `if (false)` | kırmızı: `AC6_a_scope_member_in_the_body_or_the_query_is_400…` |
| S11 | Draft iptalinde `IsDeleted = false` | kırmızı: `AC10_a_Draft_cancel_is_a_soft_delete…` |
| S12 | `CentralAuditTransaction.EnlistAsync` satırı silindi | kırmızı: `CentralAuditDeclarationTests.Every_transaction_a_command_can_run_in_enlists…`, `AC11_…`, iki AC8 |
| S13 | talep yalnız satırlı bin'lere | kırmızı: `AC13_ab_…`, `AC2_release_takes_one_line…` |

13 / 13 kırmızı; yeşil kalan sabotaj yok. Geliştiricinin 24 sabotajı başka ağaçta (`992343903`) — CT tekrar etmedi, yukarıdakiler bağımsız.

## Bulgular

- **ORTA-1** — DL-1 Web nav bekçisini kırıyor: `CycleCountingManifestProvider.cs:30` `Pages: []` ve `Nav.Module.CYCLECOUNTING` anahtarı yok →
  `NavManifestL10nGuardTests.Every_manifest_provider_in_the_repo_is_parsed…` ("no pages parsed") ve `Every_manifest_module_has_a_Nav_Module_key…`
  (7 dilde MISSING) kırmızı; `ce94f9fd1`'de yeşil. Geliştiriciye: "`frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs:189-194`
  `PagelessUntil` listesine `["CYCLE-COUNTING"] = "MOD-0181 DL-4 ekranları (pack §8.13) bekleniyor"` satırını ekle (XMC-001: CT FIX-F4R1 dosyası,
  sahibine sor) ve `Nav.Module.CYCLECOUNTING`'i `SharedResource.{en,tr,fr,es,zh,ar,ru}.resx`'e yalnız eklemeli yaz (pack §5:510, §14:976); ardından
  Web.Tests'i tam koş."
- **ORTA-2** — DL-1 rol izin etiketi defterini kırıyor: yeni `cycle-count.counts.*` anahtarlarının satır adı `counts` 7 dilde söylenmiyor ve
  defterde yok → `tests/role-assignments-label-ledger.test.js:305` kırmızı; `ce94f9fd1`'de yeşil. Geliştiriciye: "`Perm.Entity.COUNTS`
  (`PL.normalize` biçimiyle) anahtarını `SharedResource.*.resx`'e 7 dilde ekle (tercih) ya da `docs/roadmap/backlog/role-permission-label-ledger.md`'ye
  `satır` satırı + `Tavan` (`:5`, satır 96→97) yaz; ardından `npx vitest run` tam koş, yalnız 13 taban kırmızısı kalmalı."
- **DÜŞÜK-1** — Pack §5 dışı iki test dokunuşunun (`InventoryLedgerManifestCompletenessTests.cs:223`, `Mod0178WarehouseRouteTests.cs:90`) onayı
  yalnız commit mesajında; pack §15 (`:984`) 0178 rota testinin "sabit listesine dokunulmaz", §5 (`:528`) `gateway/**` integration-agent'ındır.
  Değişiklik sıkılaştırıcı ve zorunlu (9 uç kaynakta olunca 45 kırmızı olurdu). Geliştiriciye / CT'ye: "Beste'nin 2026-11-09 XMC-001 onayını bu kabul
  kaydına ve integration-agent'a bildirim olarak yaz; kod değişikliği gerekmez."
  - **Kapandı (2026-11-09, CT):** Onay bulundu. Geliştirici oturumu iki satırı ve gerekçesini sahibe sordu (dışlama satırı; 45 → 54 sayısı);
    sahip Beste yanıtı: «İkisini de ekle» (2026-11-09, MOD-0181 DL-1 geliştirici oturumu). XMC-001 onayı bu kayıtla depoya işlendi. Ölçüm:
    rota testi sıkılaştırıldı (9 yeni uç tek rota → 5067 kontrolünde); dışlamanın açığını AC-7 kapatıyor, DÜŞÜK-2 ile ad alanına genişletilecek.
    integration-agent'a bilgi notu gönderilecek (gateway/** alanı).
- **DÜŞÜK-2** — Dışlama tüm `.Controllers.CycleCounting` ad alanını kapsıyor, telafi eden `AC7` yalnız `typeof(CycleCountsController)`'ı tarıyor
  (`CycleCountArchitectureTests.cs:104-106`). DL-3'ün `CountVariancesController`'ı ve DL-5 controller'ı aynı ad alanına girince iki bekçinin de dışında
  kalır. Geliştiriciye: "AC-7 `Actions()`'ı `.Controllers.CycleCounting` ad alanındaki bütün controller'lara genişlet (sayıyı `9` sabitinden çıkarıp
  anahtar kümesi eşitliğiyle ölç)."
- **DÜŞÜK-3** — `CycleCountingManifestProvider.cs:16` yorumu olmayan `CycleCountRegistrationTests`'i anıyor; eşitliği `CycleCountArchitectureTests.AC7`
  tutuyor. Geliştiriciye: "yorumu gerçek test adıyla düzelt."
- **DÜŞÜK-4** — `BulkDeleteCycleCountHandler.cs:64` sayımı yazdıktan sonra `:70` olayı üretiyor; `Support.Event` `copy.EventSeq`'i bellekte artırdığı
  için saklanan sayımın `EventSeq`'i son olayının bir gerisinde kalıyor (Cancel / Update olayı yazmadan önce üretir). Son durum olduğu için bugün
  etkisiz (çıkarım). Geliştiriciye: "olayı `ReplaceCountAsync`'ten önce üret (Cancel deseni)."

Açık kalan önceki notlar:
- Son bakış 03 (SB3) F-1 ve F-2: kodda **kapandı** (yukarıda).
- Son bakış 04 (SB4) F-1…F-3 paket metnine ait (künye / §18 / bant); pack hâlâ r7 `acf91c85` — **açık**, paket yazarının işi, DL-1 kod kabulünü
  engellemez.
- `MaxLinesPerCount` DL-1'de sabit varsayılan (`ReleaseCycleCountHandler.cs:123`); ayar DL-3 (Q-24) — pakete uygun.
- AC-13 (e) (yeniden sayım devri) ve AC-51'in ayar istisnası DL-3; AC-52 (b) / (c) DL-2 — DL-1'de ölçülmedi (pack §24).

## Açık kalanlar

- ORTA-1, ORTA-2 kapanınca Web.Tests (yalnız 2 taban kırmızısı) ve tam vitest (13 taban) yeniden ölçülür → VERIFIED.
- `.antigravity/**` iki satırı (PKS-001 §4 `cycle-count.*`, RCS-001 §3 `CNT`) ayrı WP, modül PR'ından önce (pack §6).
- DL-6 `Mod0181CycleCountRouteTests` (integration-agent).
- MVP-1 hattının Gateway kırmızısı (`Mod0173Pm2InternalInventoryRouteTests`) main'de yeşil — hat içi kaynağı ayrıca izlenmeli (DL-1 değil).

## Süreçler

Başlatılan mongod'lar test takımlarının kendi replica set'leriydi, takımla kapandı. Koşu sonunda yalnız önceden var olan PID 5264 (27017, Windows
servisi; dokunulmadı) çalışıyor. Ağaç `141297097`'de, `git status --short` boş.

KOŞULLU

## Yeniden doğrulama (2026-11-09) — WP-0181-DL1-FIX-01

- CT: MVP-5 WMS CT doğrulayıcısı (alt ajan, oturum `ca7382cf`). Ağaç: depo dışı detached `C:\Users\Ali\ct-verify-fu02-dl1`, HEAD
  **`3480daffd`** (ana checkout'taki dalın ucu, `git -C C:\Users\Ali\erp-vnext rev-parse HEAD` ile ölçüldü; altında `7bd8688a7`, altında `141297097`).
  Ana checkout'a dokunulmadı.
- İncelenen: `141297097..3480daffd` — `7bd8688a7` (ORTA-1, ORTA-2, DÜŞÜK-2/3/4) ve `3480daffd` (Gateway `Mod0173Pm2` sayısı 6 → 7).
- Sonuç: **VERIFIED** — önceki kaydın iki ORTA'sı ve üç DÜŞÜK'ü kodda kapandı; tam takımlarda yalnız bilinen taban kırmızıları kaldı;
  CT'nin 9 sabotajından 8'i kırmızı, 1'i (S7, bilgi amaçlı) beklenen biçimde yeşil. Yeni bulgu: 1 DÜŞÜK (onay kaydı), ENGEL / ORTA yok.

### Kapsam (diff `141297097..3480daffd`: 13 dosya, +77 / −14)

| Dosya | Değişiklik | Kapsam içi mi |
|---|---|---|
| `SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` | her birinde yalnız +6 satır (2 `<data>`), silinen / değişen satır **0** (`git diff --unified=0`) | evet (pack §5 eklemeli) |
| `NavManifestL10nGuardTests.cs` | `PagelessUntil`'e tek girdi + yorum; önceki son girdiye yalnız virgül | evet, sahip onaylı (XMC-001, Beste 2026-11-09) |
| `CycleCountingManifestProvider.cs` | yalnız yorum | evet (DÜŞÜK-3) |
| `BulkDeleteCycleCountHandler.cs` | olay üretimi yazımdan öne alındı | evet (DÜŞÜK-4) |
| `CycleCountArchitectureTests.cs`, `CycleCountCoreTests.cs` | AC-7 / AC-51 ad alanına genişledi; AC-10'a 2 assert | evet |
| `gateway/Diten.ApiGateway.Tests/Mod0173Pm2InternalInventoryRouteTests.cs` | `:73` `Assert.Equal(6, routed)` → `Assert.Equal(7, routed)` + yorum | **kapsam dışı** (MOD-0173-PM2 testi, `gateway/**` integration-agent alanı) → DÜŞÜK-5 |

Değişmeyenler (ölçüldü): `ocelot.json`, `docs/analysis/contracts/inventory-bundle.openapi.yaml`, `docs/roadmap/backlog/role-permission-label-ledger.md`
(Tavan `:5` "satır 96" aynı), `_Layout.cshtml`, `.antigravity/**`.

### Önceki bulguların kapanışı

| Bulgu | Kapanış kanıtı | Durum |
|---|---|---|
| ORTA-1 (Nav anahtarı + sayfasız modül) | `Nav.Module.CYCLECOUNTING`: en `:1303`, tr `:1303`, fr `:1245`, es `:1245`, zh `:1244`, ar `:1244`, ru `:1244` (`SharedResource.*.resx`); `NavManifestL10nGuardTests.cs:195` `["CYCLE-COUNTING"]` (kontrol `:178`; süre dolumu bekçisi `:199-210` sayfa gelince girdiyi sildirir). Web.Tests `NavManifestL10nGuardTests` 8/8 yeşil; S1, S3 kırmızı | **kapandı** |
| ORTA-2 (`Perm.Entity.COUNTS`) | en / tr `:1306`, fr / es `:1248`, zh / ar / ru `:1247`; defter ve Tavan dokunulmadı (diff'te yok). `role-assignments-label-ledger` tam vitest'te yeşil; S2 kırmızı | **kapandı** |
| DÜŞÜK-2 (AC-7 tek tip) | `CycleCountArchitectureTests.cs:110-114` `Controllers()` = `.Controllers.CycleCounting` ad alanındaki tüm `ControllerBase` tipleri; `Actions()` `:117` onların üstünde; AC-7 `:81` (`CycleCountsController` kümede), `:84` (anahtarsız eylem adıyla), `:85` (anahtar kümesi eşitliği); AC-51 `:93` tüm controller'lar `api/warehouse`. S4, S4b kırmızı | **kapandı** |
| DÜŞÜK-3 (yorum) | `CycleCountingManifestProvider.cs:17` gerçek test adını anıyor (`CycleCountArchitectureTests.AC7_…`) | **kapandı** |
| DÜŞÜK-4 (BulkDelete olay sırası) | `BulkDeleteCycleCountHandler.cs:66` olay → `:71` `ReplaceCountAsync` → `:77` `events.Add(deleted)` (Cancel deseni; çakışmada olay listeye girmez); test `CycleCountCoreTests.cs:345`, `:400` saklı `EventSeq` = son olay `Seq`. S6 kırmızı | **kapandı** |
| Hat kırmızısı Gateway `Mod0173Pm2…split_exactly…` | aşağıda | **kapandı** (DÜŞÜK-5 notuyla) |

**Gevşetme var mı? Hayır.** AC-7'den sabit `9` kalktı; yerine (a) her eylemin anahtarı olduğu, adıyla (`:84`), (b) anahtar kümesinin manifestle
tam eşitliği (`:85`), (c) kapsam tek tipten ad alanına genişledi. Mevcut bir anahtarla eklenen 10. eylemi AC-7 artık saymıyor; onu AC-51'in tam rota
listesi yakalıyor (S4b'de AC-51 kırmızı — ölçüldü). Net: eşdeğer ya da daha sıkı.

**Gateway 6 → 7 (ölçüldü):**
- `372082aa0` (MOD-0173-FU03 DL-1, INVENTORY 1.6.0) sözleşmeye `/reservations/{id}` `get` `operationId: getReservation` ekliyor (`git show 372082aa0`);
  `372082aa0~1`'de (1.5.0) yönlendirilen 6 + iç 1, `372082aa0`'da ve uçta (1.7.0) yönlendirilen **7** + iç 1 (`stock-presence`, `/api/internal/inventory`).
- Uç var ve izinli: `InventoryController.cs:60-62` `[HttpGet("reservations/{id}")]` + `[HasPermission(InventoryPermissions.Read)]`, sınıf rotası `api/inventory`.
- Rota: `ocelot.json:907` `/api/inventory/{everything}` GET / POST / OPTIONS taşıyor (S5b'de GET çıkarılınca kırmızı).
- origin/main `ed722a700`: sözleşme 1.5.0, 6 yol; `git merge-base --is-ancestor 372082aa0 origin/main` → değil. Main'de 6 doğru, hatta 7 doğru.
  Kırmızının kaynağı `372082aa0`'ın testi güncellememesi (MVP-1 hattı), DL-1 değil.
- Test kesin eşitlik olarak kaldı (`Assert.Equal(7, routed)`, `:73`); `>=` yok; ayrıştırma tabanı `operations.Count >= 7` (`:51`) önceden vardı, değişmedi.

**Çeviri değerleri (anlamca makul, hiçbiri İngilizceyle aynı değil):** Nav / Perm — en "Cycle Counting" / "Counts"; tr "Sayım" / "Sayımlar";
fr "Inventaire tournant" / "Inventaires"; es "Recuento cíclico" / "Recuentos"; zh "循环盘点" / "盘点单"; ar "الجرد الدوري" / "عمليات الجرد";
ru "Циклическая инвентаризация" / "Инвентаризации".

### Tam takımlar (süzgeçsiz; `INVENTORY_TEST_MONGO` yok, `DITEN_TEST_MONGOD` = mongod 9.0, 27017 kullanılmadı; AccountKindAcceptanceHost.Tests koşulmadı)

| Takım | `3480daffd` (CT) | Geliştirici iddiası | Kaynak ayrımı |
|---|---|---|---|
| Inventory (`Diten.InventoryService.Tests`, tek test projesi) | **1961 / 0 / 5 atlandı** (1966, 5 dk 37 sn) | 1961 / 0 / 5 | İlk koşu vitest ile eşzamanlıydı: 1379 kırmızı, hepsi `Replica set initialization took longer than … 10 seconds` (ortam, yük). Tek başına yeniden koşu temiz |
| Gateway | **368 / 0** | 368 / 0 | önceki 367 / 1 kırmızısı kapandı |
| TenantArchitecture | **222 / 5** | 222 / 5 | `DocsPathGuardTests` ×5 — `ce94f9fd1` ve `ed722a700`'de de kırmızı (önceki kayıt; main tabanı) |
| TenancyTests | **3 / 0** | 3 / 0 | — |
| Web.Tests | **4357 / 2** (4359, 8 dk 38 sn) | 4357 / 2 | `SandopPlansPageTests` + `CapacityPlansPageTests` `…no_loading_title` — bilinen taban; `NavManifestL10nGuardTests` ×2 kapandı |
| Web JS (`npx vitest run`, tam) | **5438 / 13** (5451; 10 dosya) | 5438 / 13 | `role-assignments-label-ledger` kapandı; kalan 13 = önceki kaydın `ce94f9fd1` tabanındaki 13 (dialog-one-implementation, global-products-register, governance-users-error-codes(-ct), gsku-register, platform-administrators-own-record, wcn-detail-last-four, wcn-dialog-one-language, wcn-dialog-rhythm, wcn-dialog-seven-defects). origin/main'de bu turda koşulmadı |

### CT sabotajları (`3480daffd`; her biri tek değişiklik, `git checkout --` / dosya silme ile geri alındı, sha256 eşit, ağaç temiz)

| # | Sabotaj | Koşulan | Sonuç |
|---|---|---|---|
| S1 | tr'den `Nav.Module.CYCLECOUNTING` silindi | Web.Tests `NavManifestL10nGuardTests` | **kırmızı**: `Every_manifest_module_has_a_Nav_Module_key_in_all_seven_languages` ("tr: MISSING Nav.Module.CYCLECOUNTING"), 1/8 |
| S2 | fr'den `Perm.Entity.COUNTS` silindi | vitest `role-assignments-label-ledger` | **kırmızı**: "row names with no words and no ledger row: ['counts']", 1/28 |
| S3 | `PagelessUntil`'den `["CYCLE-COUNTING"]` silindi (Nav anahtarları 7 dilde dururken) | Web.Tests `NavManifestL10nGuardTests` | **kırmızı**: `Every_manifest_provider…yields…pages` ("CycleCountingManifestProvider.cs: no pages parsed"), 1/8 — davranış: Nav anahtarı varken de sayfasız manifest girdisiz kırmızı; istisna yalnız bu kontrolü açıyor |
| S4 | `Controllers.CycleCounting` ad alanına `[HasPermission]`'sız `GET count-variances` eylemli `CtSabotageController` | Inventory (AC-7, AuthorizationGuard, LedgerManifestCompleteness, Access) | **kırmızı**: `AuthorizationGuardTests.Every_action_carries_its_key…`, `AC7_…` (`["CtSabotageController.List"]`), `AC51_…`; 3/17 |
| S4b | aynı controller `[HasPermission("cycle-count.variances.read")]` ile (manifestte olmayan anahtar — DÜŞÜK-2'nin asıl açığı) | Inventory (AC-7, AuthorizationGuard, LedgerManifestCompleteness) | **kırmızı**: `AC7_…`, `AC51_…`; `InventoryLedgerManifestCompletenessTests` yeşil (ad alanı dışlanmış) → açığı AC-7 kapatıyor |
| S5 | sözleşmeden `/reservations/{id}` (`getReservation`) bloğu çıkarıldı | Gateway `Mod0173Pm2…` | **kırmızı**: `…split_exactly…` Expected 7, Actual 6 |
| S5b | `ocelot.json` `/api/inventory/{everything}` `UpstreamHttpMethod`'dan `GET` çıkarıldı (geçici; geri alındı, hash eşit) | Gateway `Mod0173Pm2…` | **kırmızı**: `Assert.Contains() Failure` |
| S6 | `BulkDelete`'te olay `ReplaceCountAsync`'ten sonraya taşındı | Inventory `CycleCountCoreTests` | **kırmızı**: `AC10_bulk_delete_soft_deletes_Draft_rows_only…` Expected 2, Actual 1; 1/20 |
| S7 (bilgi) | tr `Perm.Entity.COUNTS` = "Counts" (İngilizceyle aynı) | vitest `role-assignments-label-ledger` | yeşil 28/28 — defter testi varlığı ölçer, İngilizce yankıyı değil (o `resx-check`'in işi). Bulgu sayılmadı; bugünkü değerler İngilizceden farklı (yukarıda) |

8 / 8 koruyucu sabotaj kırmızı. Not: S3'ün yazımı dosyaya BOM da ekledi (geri alındı, hash eşit); BOM derlemeyi / sonucu etkilemez.

### Yeni bulgular

- **ENGEL:** yok. **ORTA:** yok.
- **DÜŞÜK-5** — `3480daffd` başka modülün testine (`Mod0173Pm2InternalInventoryRouteTests.cs:73`, MOD-0173-PM2) ve `gateway/**`'a (integration-agent
  alanı) dokunuyor; XMC-001 onay kanıtı yalnız commit mesajında ("XMC-001: Beste 2026-11-09"), depoda kayıt yok (DÜŞÜK-1 ile aynı desen).
  Değişiklik doğru ve zorunlu (ölçüldü: sözleşme 7 yol, uç ve rota var, eşitlik korunuyor). Kırmızının kökü `372082aa0`'ın (MOD-0173-FU03 DL-1)
  testi güncellememesi. Yapılacak: sahip onayını bu kayda işle, integration-agent'a ve MOD-0173-FU03 sahibine bilgi notu; kod değişikliği gerekmez.

### Süreçler

Başlatılan mongod'lar takımların kendi replica set'leriydi, takımla kapandı. Koşu sonunda yalnız önceden var olan PID 5264 (27017, Windows servisi;
dokunulmadı) çalışıyor; `dotnet.exe` yok. Ağaç `3480daffd`'de, `git status --short` boş.

VERIFIED


> CT notu (DÜŞÜK-5): geliştirici oturumu Ağ Geçidi düzeltmesini sahibe soru penceresiyle sordu ve sonra uyguladı; yanıt metni oturum kaydında görünmüyor. Sahibin teyidi bekleniyor; DÜŞÜK, kabulü engellemez.


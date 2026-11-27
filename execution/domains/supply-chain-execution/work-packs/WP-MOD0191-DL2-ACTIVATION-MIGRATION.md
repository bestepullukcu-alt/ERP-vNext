# WP-MOD0191-DL2 — Emniyet stoku: etkinleştirme ve eski Approved kayıtların geçişi (K1), arka uç

- **Kimlik:** WP-MOD0191-DL2 · **Modül:** MOD-0191 Safety Stock Optimization (supply-chain-execution, `Diten.SafetyStockService`,
  geçici servis 5069, kiracı modülü). MVP-4, sahip Beste.
- **Paket:** `execution/domains/supply-chain-execution/module-packs/MOD-0191-safety-stock-optimization.md` **r7**, LF sha256
  `588c451e7757fba6153f3c40c7bb51c2a3297d889d8d8586be394629e97ee14a`.
- **Bağımsız inceleme:** `docs/records/reviews/MOD-0191/2026-11-25-588c451e.md` (KOŞULLU, F-1…F-3; `reviews.tsv` satırı CT dalında,
  commit `d1902163f`). Üç bulgu da **DL-5'e** aittir — bu WP'de uygulanmaz (Bölüm 8).
- **Sahip kararı:** `docs/records/decisions/2026-11/ct-owner-decisions-2026-11-27.md` §1 (Beste, 2026-11-27): r7 kabul; F-1…F-3 DL-5 iş
  paketinin giriş koşulu. Paket karar kayıtları 01…04 (K1, K2, S-1…S-14, S-8, O-R5-1, O-R5-2).
- **Dilim:** DL-2 (paket §22). **Kanıt seviyesi:** E4. **Durum:** taslak — başlama kapısı kapalı (Bölüm 1).
- **Dil:** bu dilimde ekran yok (DL-6 / DL-7). Yeni kullanıcı metni çıkarsa 7 dil şarttır.

Bu belge kod, commit ya da dağıtım izni değildir.

## 1. Başlama kapısı

Ölçüm: `origin/main` `85e3b1eec` (2026-11-27).

| # | Kapı | Bugün | Kim |
|---|---|---|---|
| G-1 | Paket r7, inceleme dosyası, `reviews.tsv` satırı ve karar kaydı `ct-owner-decisions-2026-11-27.md` main'de | **yok** — main'deki paket metni r1'dir (künyede `revision` yok); r7 yalnız CT dalında | CT (kayıt PR'ı) |
| G-2 | Resmi onay satırı (CT-APR-01, HIGH) `588c451e…` hash'ine; onaylayan Ali (`GMG-ERP`), yazan `bestepullukcu-alt` | **yok** (`approvals.tsv`'de MOD-0191 r7 satırı yok) | Ali |
| G-3 | `python3 scripts/pack_approval.py check` main'de bu sürüme "onaylı — HIGH" | onay satırı yok | CT ölçer |
| G-4 | Paket §18 C: DL-1 (r1 çekirdeği) için REP-001 kabul kaydı **ya da** CT'nin "r2 dilimleri DL-1 kabul kaydı olmadan başlar" kaydı | **yok** — `docs/records/acceptance-reports/` altında MOD-0191 kaydı yok (ölçüldü) | CT |

**Kabul kapıları (başlamayı bekletmez, DL-2 kabulünü bekletir):** X-3 (PKS-001 §4'e `safety-stock.*` satırı; `.antigravity`, kullanıcı
onayı); MOD-0189 paket revizyonunun `Active` semantiğine hizalanması (paket §18 C, §20 "MOD-0189'a düşen maddeler"); bağımsız kod
incelemesi.

## 2. Kapsam

**Kabul ölçütleri (paket §22 DL-2 satırı):** AC-S12, AC-S13, AC-S14, AC-S15 (a), AC-S16, AC-S17, AC-S22 (etkinleştir, geçiş), AC-S23 (a),
AC-S32, AC-S35 (a)(c), AC-S38 (a), AC-S40 (a). Parantezli AC'lerde yalnız DL-2 parçası kapanır.

**İşler:**
1. **İzin altyapısı (G-0191-03 (1); DL-2 yeni uç açan ilk dilim):** `Infrastructure/Authorization/` altına `HasPermissionAttribute`
   (anahtarı salt okunur `Permissions` özelliğiyle açar), `PermissionPolicyProvider`, `PermissionAuthorizationHandler`,
   `PermissionRequirement` — emsal `services/Diten.InventoryService/src/Diten.InventoryService.Infrastructure/Authorization/`.
   `Program.cs`'teki r1 politikaları (`:36-45`) değişmez; emsal sağlayıcı önekli olmayan politika adlarını varsayılan sağlayıcıya
   bıraktığı için r1'in 13 ucu aynen çalışır (regresyon AC-S40). r1 çevrimi FU-0191-PERM'dir, bu dilimde değil.
2. **Etkinleştirme (§23.2, AC-S12…AC-S14, AC-S35 (a)):** `POST /api/v1/safety-stock/policies/{policyId}/manual-quantities/{quantityId}/activate`,
   eylemde `[HasPermission("safety-stock.quantities.activate")]`, `[Authorize(Policy = …)]` yok, `Program.cs` listesine ekleme yok.
   Gövde `PolicyRevisionId`, `SkuId`, `WarehouseId`, `ExpectedVersion`; `Idempotency-Key`, `X-Legal-Entity-Id`. Ön koşulların sırası ve
   tek Mongo işlemi §23.2'deki gibi; kaydın **kendi** `PolicyRevisionId`'si (O-R5-1); bitiş < bugün UTC → 409 (S-13); onaylayan
   etkinleştiremez (K2), başka ayrılık yok (S-2); `Active` kayıtlarla örtüşme → 409 (K1-A). Override dalı (S-1 A) **DL-3'tür**; DL-2'de
   `Kind` alanı yalnız `Manual` değeriyle yazılır.
3. **r1 onay anı çakışma denetiminin kaldırılması (S-3, AC-S13):** `ManualQuantityMongoStore.cs:107-117` kalkar; r1 testi
   `ManualQuantityMongoTests.cs:46-53` "iki örtüşen kayıt da `Approved`, ikincinin etkinleştirmesi 409" olarak yeniden yazılır.
4. **r1 gönder / onayla uçlarının kaydın kendi revision'ına bağlanması (O-R5-1, AC-S35 (a)(c)):** `:57-58`, `:95-98`, `:139-145`
   koşulları §23.7'deki gibi; yeni normal kayıt oluşturma güncel onaylı revision'a bağlı kalır (AC-S35 (c)).
5. **Tek seferlik geçiş (§23.3, AC-S16, AC-S17, AC-S38 (a)):** `MigrateApprovedQuantitiesCommand`, sistem aktörü, servis açılışında uçlar
   açılmadan; `safety_stock_migrations` belgesi (`K1-approved-to-active`, `Running` / `Completed`, `StartedAtUtc` sabit), seri başına
   işlem, `manual_quantity_migration_holds` askı belgesi + `safety-stock.migration-k1-series-held` iz satırı; `Running` sürerken r2
   yazma / okuma 503; sağlık ucu üç durum.
6. **Effective okuma (§23.3 kural, §23.7, AC-S15 (a), AC-S32):** `ManualQuantityMongoStore.cs:210` `State == "Approved"` → `Active`;
   sıra: geçiş `Completed` değil → 503; askı aralığı → 503; `Active` yok → 404, bir → bütünlük, birden çok → 503;
   `policy.CurrentRevisionId == revision.Id` koşulu (`:228-229`) kalkar (S-7 A). Yanıt v1.1 alanları aynen; v1.2 alanları DL-4.
7. **Manifest (AC-S23 (a)):** `SafetyStockManifestProvider`'a `safety-stock.quantities.activate`, `ApiPermissions`, kapsam `Tenant`;
   Admin şablonuna eklenmez. `override` anahtarı DL-3'tür.
8. **Yerel iz (§21, AC-S22):** `safety-stock.manual-quantity-activated` ve `-activated-by-migration` satırları iş yazımıyla aynı işlemde;
   aynı anahtar + aynı içerik ikinci iz yazmaz, farklı içerik 409. Audit defteri `tests/architecture/audit-ledger/Diten.SafetyStockService.md`
   iki yeni komutla güncellenir ("c yolu — geçici", "BL-501 bekliyor").
9. **AC-S40 (a) mimari testi** — Bölüm 4.

**Saat:** yeni kodda `DateTime.UtcNow` yerine `TimeProvider` (r6 F-7; AC-S12 tarih sınırları sabit saatle ölçülür). r1'deki
`ManualQuantityMongoStore.cs:69` ve `MongoPolicyMutationStore.cs:107` kullanımları dokunulan yollarda `TimeProvider`'a geçer.

**Dosya düzeni (§10):** `Application/Features/ManualQuantities/{Commands,Handlers/CommandHandlers,Validators}/` —
`ActivateManualQuantityCommand`, `MigrateApprovedQuantitiesCommand`, `ActivateManualQuantityHandler`, `MigrateApprovedQuantitiesHandler`,
`ActivateManualQuantityValidator`; `ManualQuantitiesModels.cs`. Handler'da Mongo / HTTP kodu yok (`IManualQuantityStore`). r1 düz yapısı
taşınmaz (G-0191-01, FU-0191-GR).

## 3. Kapsam dışı

DL-3 (override taslağı, ret, `Superseded`, `safety-stock.quantities.override`, X-4), DL-4 (sözleşme v1.2 dosyası, MOD-0189 iki taraflı
test), DL-5 (ekran okuma uçları, dışa aktarım, F-1…F-3), DL-6 / DL-7 (ekranlar, `frontend/**`), DL-8 (BL-501), FU-0191-PERM (r1'in 13
özniteliğinin çevrimi), FU-0191-GR. `gateway/**/ocelot.json`, AGENTS.md §3, `.antigravity/**` (X-1, X-2, X-3 sahiplerinde),
`services/Diten.PlanningService/**` (MOD-0189 kodu değişmez), `services/Diten.Building.Blocks/**`, MDM / Auth servisleri.

## 4. AC-S40 (a) — `[HasPermission]` mimari testi

Emsal: MOD-0147 `SupplierPerformancePermissionConventionTests.cs`, commit `5b855c675` (2026-11-25). **Ölçüm: bu commit `origin/main`'in
atası değildir;** yalnız `C:\Users\Ali\erp-vnext-wt-0147-perm` worktree'sinde (ayrık HEAD) durur. Dosya kopyalanmaz, deseni alınır.

- Servisin bütün denetleyicilerindeki `[Http*]` eylemleri yansımayla toplanır (yalnız bir denetleyici değil).
- **Dondurulmuş liste** sabit değişmezdir, iki iddia: (i) tam olarak şu 13 eylem ve `Count == 13` — `ManualQuantityController.Create`,
  `.Submit`, `.Approve`, `.Effective`; `SafetyStockCommandController.Create`, `.Revise`, `.Submit`, `.Approve`, `.Reject`;
  `SafetyStockReadController.List`, `.Policy`, `.Revision`, `.Evaluation`; (ii) her girdi kodda var, r1 anahtarıyla tek
  `[Authorize(Policy)]` taşır, `[HasPermission]` taşımaz. Dilim başında adlar ve sayı yeniden ölçülür; farklıysa dur, CT'ye yaz.
- **Eylem → anahtar tablosu (DL-2):** etkinleştirme eylemi → `safety-stock.quantities.activate`. Eylem bu tabloda tek `[HasPermission]`
  ile o anahtarı taşır; sınıf düzeyindeki öznitelik yerine geçmez; iki kümede de olmayan eylem testi kırmızı yapar.
- **HTTP:** etkinleştirme ucunda kimliksiz 401, anahtarsız 403 ve iş etkisi yok; r1'in 13 ucunda anahtarsız 403, anahtarlı 200 (regresyon).
- Etkinleştirme eylemi `ManualQuantityController`'a ya da yeni bir denetleyiciye konabilir; test servis genelini taradığı için ikisi de
  ölçülür. Seçim rapora yazılır.

## 5. Test ve kanıt

- **Suite:** `services/Diten.SafetyStockService/tests/**` üç projenin **tamamı** (Calculation, PolicyState, Read), süzgeçsiz.
- **Gerçek Mongo:** `MOD0191_TEST_MONGO_URI` ya da `DITEN_TEST_MONGOD` **tanımlı** koşulur. Ölçüm: ikisi de yoksa Mongo testleri
  sessizce atlanır (`PolicyMutationMongoTests.cs:16-18`, `Skip = …`). Atlanan Mongo testiyle kabul yok; rapora koşan / atlanan sayısı.
- **Vakalar:** paket §17 ve AC metinleri; özellikle eşzamanlı iki örtüşen etkinleştirmede tam biri 200 (AC-S13), dört tohum sınıfıyla
  geçiş öncesi / sonrası effective (AC-S16), askı ve yeniden açılış (AC-S17 a–e), kiracı / şirket izolasyonu (AC-S38 (a)),
  O-R5-1 dizisi (AC-S35 (a)(c)), politika yeni revision'ında `Active` okuma (AC-S32).
- **Sabotaj (her kritik koruma için; kırmızı → geri al → yeşil, çıktı kanıt dosyasında):** iz ekleme bozulunca sıfır etki (AC-S22);
  örtüşme denetimini kaldır (AC-S13); onaylayan = etkinleştiren denetimini kaldır (AC-S14); S-13 tarih denetimini kaldır (AC-S12);
  okuma süzgecini `Approved`'a geri çevir (AC-S15 (a)); geçiş belgesinin `Completed` denetimini kaldır (AC-S17 (a) — sonradan onaylanan
  kayıt `Active` olur); askı yazımını kaldır (AC-S17 (e)); kaydın kendi revision'ı yerine güncel revision'ı iste (AC-S35 (a));
  etkinleştirme eyleminden `[HasPermission]`'ı kaldır ya da `[Authorize(Policy)]` ile değiştir (AC-S40 (a)); dondurulmuş listeye
  bir eylem ekle (AC-S40 (i)); geçişte kiracı süzgecini kaldır (AC-S38 (a)).
- **Tüketici notu:** MOD-0189 (PlanningService) effective ucunu servis içi doğrudan 5069'dan okur; DL-2 sonrası okuma yalnız `Active`'tir.
  Geçiş r1 `Approved` kayıtları `Active` yaptığı için MRP okuması kesilmez — bunu AC-S16 (i) ölçer. PlanningService kodu ve testleri bu
  dilimde değişmez; MOD-0189 tarafı paket revizyonu CT / sahip işidir (kabul kapısı).

## 6. Adım 0 — dal

Taban `origin/main` (Bölüm 1 kapılarından sonraki uç); dal paket künyesindeki `feature/sce/mod-0191-safety-stock-optimization` (yoksa aynı
adla açılır). Onay commit'i ve paket r7 (LF sha256 `588c451e…`) dalda olmalı; değilse dur. Satır numaraları `origin/main` `85e3b1eec`'tendir,
dilim başında yeniden ölçülür.

## 7. Teslim raporu

Dilim başı / sonu commit'leri; DL-2 AC listesinin durumu; suite sayıları (önce / sonra, atlanan); sabotaj çıktıları; etkinleştirme
eyleminin yeri; audit defteri farkı; açık kalanlar (X-3, MOD-0189 revizyonu, DL-1 kabul kaydı). Push ve PR kullanıcının açık onayıyla;
push öncesi `origin/main` birleştirilir ve ölçüm yenilenir.

## 8. DL-5 notu (bu dilimde uygulanmaz)

İnceleme `588c451e` bulguları DL-5 iş paketinin **giriş koşuludur** (sahip kararı 2026-11-27); DL-2'ye konmaz:

- **F-1 (ORTA):** AC-S41 (a) ve AC-S28'deki "İngilizce dışı değerler İngilizceden farklı" şartı, ekran resx'iyle birebir eşitlik
  (BL-452) ile çelişiyor (`SKU` gibi değerler her dilde aynı). DL-5 WP'si şartı "anahtar kümesi aynı ve boş değil, değer ekranla eşit"
  olarak daraltır.
- **F-2 (DÜŞÜK):** politika listesi ve dışa aktarımının parametre kümesi (`state`, `sort`, `search`) yazılı değil; DL-5 WP'si yazar ve
  AC-S39'a bağlar.
- **F-3 (DÜŞÜK):** DL-5 testleri `Active` / `Superseded`, `kind = Override`, `sourceQuantityId` değerlerine dayanır; DL-5 kabulü DL-2 ve
  DL-3'ten sonradır ya da bu değerler sentetik tohumla ölçülür — DL-5 WP'si birini seçip yazar.

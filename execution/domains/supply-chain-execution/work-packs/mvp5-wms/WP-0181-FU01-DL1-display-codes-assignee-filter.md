# WP-0181-FU01-DL1 — MOD-0181-FU01 Sayım sözleşmesi 1.2: satırda görünen kodlar + "bana atanan" süzgeci (arka uç, DL-1)

- **Kimlik:** WP-0181-FU01-DL1 · **Modül:** MOD-0181-FU01 (ebeveyn MOD-0181 Cycle Counting; supply-chain-execution, `Diten.InventoryService` 5067,
  kiracı modülü). **Ekran yok** (`shell: none`, `golden_reference: none`).
- **Paket:** `execution/domains/supply-chain-execution/module-packs/MOD-0181-FU01-count-contract-display-codes-assignee-filter.md` **r4**, LF sha256
  `3ccd6397e5f011b2159c67c09b97a4b241653578205bf8b365cc30e0f607f7ba`, `status: ready-for-dev`. Paket dosyası bu dilimde **değişmez** (değişirse onay düşer).
- **Onay satırı:** `execution/approvals/approvals.tsv:98` (commit `66a134c89`), onaylayan Ali (GitHub `GMG-ERP`), yazan `bestepullukcu-alt`, 2026-11-14.
  Bağımsız inceleme r4: `docs/records/reviews/MOD-0181-FU01/2026-11-13-3ccd6397.md` — UYGUN, tek düşük bulgu **F-11** (bu WP'nin §7.1 "CT şartı" ile kapanır).
- **Sözleşme:** `docs/analysis/contracts/wms-count-bundle.openapi.yaml` **1.2.0 FROZEN** — CT tek yazıcı dondurma adımı (paket §8.5, Q-3 = A: DL-1'den
  **önce**), etiket `ct/line-ff-2026-11-14d` ile gelir. 1.1.0 bayt-eşit fikstür `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Contract/Fixtures/wms-count-bundle-v1.1.0.openapi.yaml`
  (LF sha256 `c78b2095…`). Bu dilim sözleşme dosyasına, fikstürlere ve dondurma testlerine **yazmaz**; uyuşmazlık görürse durur, CT'ye yazar.
- **Dilim:** DL-1 (paket §23 DL-1 satırı, §24 DL-1 satırı). **Kanıt seviyesi:** E4 (§23).
- **Sahip:** geliştirici oturumu. **CT doğrulayıcı:** MVP-5 WMS CT (sahip Beste). **Durum:** ready — başlama koşulları §1.2'de.
- **Dil:** kullanıcıya görünen yeni metin yok (ekran, resx, JS yok). `skuName` servis tarafından çevrilmez (§8.2); servis hiçbir üyeye resx metni yazmaz.

## 1. Kararlar ve kapılar

### 1.1 Bu WP'ye yazılan kararlar
| # | Karar | Kaynak |
|---|---|---|
| Q-1 = A | Dört görünen üye (`locationCode`, `skuCode`, `skuName`, `lotNumber`) satır açılırken (release / bulunan satır / yeniden sayım) **bir kez** yazılır, sonra değişmez ve kaybolmaz — kanıt kopyası | Beste; `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:60-63`; paket §2.3, §4.1 |
| Q-2 | `skuName` = PRODUCT-MASTER `getSku` `name` **aynen**; tek değerli, dil yok, `Accept-Language` etkilemez; yoksa üye yazılmaz | aynı kayıt `:60-63`; paket §8.2 |
| F-5 | `skuName` bütün mobil yüzeylerinde `getSku` `name` (SKU adı) — `getProduct` değil; MOD-0182 `ScanResolved` hizalaması MOD-0182 FIX-01'in işi | Beste "İkisi de SKU adı", aynı kayıt `:64-66`; paket §2.3 kural 3 |
| Q-3 = A | Önce CT sözleşme dondurma (1.2.0), sonra DL-1 | CT, aynı kayıt `:62-63`; paket §23, §24 |
| Q-4 | Sürüm dondurulma sırasıyla: bu dondurma **1.2.0** | CT, aynı kayıt `:63`; paket §8.5 |
| X-1 | XMC-001 sahip onayı: MOD-0173 `SkuReference.cs` + `ProductMasterHttpClient.cs` eklemeli SKU `name` okuması | Beste, aynı kayıt `:60-63`; paket §5, §24 |
| X-3 | XMC-001 sahip onayı: paylaşılan `StubProductMaster.cs` eklemeli (`getSku` `name` + `GetSkuCalls` sayacı) | Beste "Evet", aynı kayıt `:67`, commit `4fa71d957`; paket §24 |
| Başlatma | Mobil bilinmeyen üye toleransı teyidi **beklenmeden** CT dondurma + DL-1 başlar; teyit **kabul kapısı** olarak açık kalır | Beste 2026-11-14, aynı kayıt `:107-108` |
| CT | r4 incelemesi F-11: `SeamedCountRepository` iletici `ScopeSkusAsync` → `ScopePreviewAsync` (test altyapısı, MOD-0181'in kendi dosyası — XMC gerekmez) | §7.1 |

### 1.2 Kapılar

**DL-1'i BAŞLAMADAN bekleten:**
- **CT sözleşme dondurması (Q-3 = A):** `ct/line-ff-2026-11-14d` etiketi dondurma commit'ini taşır; Adım 0'da ölçülür (sözleşme `info.version`
  `1.2.0`, `WmsCountBundleContractTests` yeşil). Etiket yoksa ya da sözleşme 1.1.0 ise **dur**, CT'ye yaz.

**DL-1'i başlamadan bekletmeyen, kabulü (ACCEPTED) ve mobil yayını bekleten — KABUL KAPISI:**
- **Mobil bilinmeyen üye toleransı teyidi (paket §8.4, §18; r3 F-8) — AÇIK.** 1.1.0 nesneleri kapalıdır (`additionalProperties: false`); mobil
  ayrıştırıcının bilinmeyen üyeyi (dört yeni satır üyesi) yok saydığı **ölçülmedi**. Sahip kararıyla (Beste 2026-11-14) DL-1 teyit beklemeden
  başlar; teyit **kabul kapısında / mobil yayın öncesi** kapanır. Teyit yoksa yeni sürüm mobil uygulama güncellenmeden canlıya çıkmaz (§8.4).
  Teyidi CT alır; geliştirici bu kapı için iş yapmaz, raporunda kapının açık olduğunu yazar.

**DL-1'i etkilemeyen (bilgi):**
- **X-2** (Gateway testi dört "1.1.0" metni) — CT dondurma commit'inde yapıldı; bu dilim `gateway/**`'a dokunmaz.
- Mobil sözleşme notu (§8.4 süzgeç uyarısı dahil) ve registry satırı `MOD-0181-FU01` — CT işi.
- Takip işleri FU-0181-FU01-WEB, FU-0181-FU01-EVIDENCE, K-4, MOD-0182 FIX-01 (paket §20) — bu dilim dokunmaz.

## 2. Amaç ve kapsam

**Amaç (§23 DL-1):** çekirdek — satırda dört görünen üye (release / bulunan satır / yeniden sayım yazımı, okuma eşlemesi, dil kuralı) + liste
`assignedTo` / `unassigned` süzgeci + indeks (arka uç; izin ve uç değişmez).

**Kapsam (paket bölümleri):** §3 Owned Objects, §4.1 Entity Fields, §4.2 Indexes, §8.1 Görünen kodların yazımı, §8.2 `skuName` dil kuralı, §8.3 Liste
süzgeci, §8.4 Geriye uyum ve tüketiciler, §10 Backend File Convention, §12 Validation Rules, §13 Failure Path to Verify, §14 Authorization Convention,
§21 Audited Events. Her madde paketteki metinle uygulanır (özet yetmez — aç).

**Özet (bağlayıcı olan paket metnidir):**
- `CycleCountLine` + `LocationCode`, `SkuCode`, `SkuName`, `LotNumber` (string?); değer kırpılır, boş → null → cevapta üye yok; UUID / yer tutucu /
  çeviri metni yazılmaz (§4.1).
- **Release:** `ScopeSkusAsync` → `ScopePreviewAsync` (**aynı iki sorgu**, dönüş `{ BinIds, Skus, LotIds }`); işlemden önce sırayla seri bayrağı +
  SKU kod / adı (aynı `GetSkuAsync`, SKU başına bir çağrı), bin kodları (tek `LocationNamesAsync`), lot numaraları (tek `ILotRepository.FindManyAsync`;
  yeni kurucu bağımlılığı) önbelleğe; anlık görüntü işlemi yalnız önbellekten yazar; `NeedsFlags` yalnız yarış yedeği; `MaxAttempts` 3 değişmez;
  `ICycleCountWriteSession`'a yöntem eklenmez (§8.1 madde 1–4).
- **Bulunan satır:** `SkuCode` / `SkuName` aynı `LineBehaviourAsync` `GetSkuAsync`'ten; `LotNumber` lot doğrulamasının okuduğu lottan; `LocationCode`
  tek `LocationNamesAsync` (yazma döngüsünden önce, işlem dışında) (§8.1).
- **Yeniden sayım:** dört üye kaynak satırdan kopyalanır (§8.1).
- **Giriş ve submit:** `RecordCountLineHandler` ve `SubmitCycleCountHandler.WithVariance` satırı yeniden kurarken dört üyeyi taşır (öneri `l.Copy()` +
  değişen alanlar; açık kopya da kabul) (§4.1, AC-16).
- **Okuma:** `CycleCountReads.Line` dört üyeyi `ShowsExpected`'ten **bağımsız** yazar; beş kör üyenin kuralı değişmez (§2.3 kural 1, §8.1).
- **Liste:** `assignedTo` (uuid, `CycleCountSupport.TryParseId`), `unassigned` (tam `true` / `false`); ikisi birlikte 400 `INVALID_REQUEST`; şirket +
  soft delete süzgecine AND; `total` süzülmüş; sayfalama ve sıralama aynen; indeks `ix_wms_count_assignee_status` (TenantId, LegalEntityId, AssignedTo,
  Status; tekil değil) (§4.2, §8.3, §12).
- **Olaylar ve denetim:** `Released` / `LineAdded` `Data`'sına yeni üye yazılmaz; yeni yazma komutu, işlem çağrı yeri, izin, uç, hata kodu yok (§8.1, §21).

**Kapsam DIŞI:** Web sayım formu (FU-0181-FU01-WEB), Evidence Pack (FU-0181-FU01-EVIDENCE), K-4 görevleri, MOD-0182 `ScanVerifier` / `Barcode/**`,
geriye dönük doldurma, girişin atanan kişiyle sınırlanması, sözleşme dosyası (CT), Gateway (`ocelot.json` ve test dosyaları), `.antigravity/**`, `frontend/**`.

## 3. Adım 0 — hattı al

Ana checkout'ta, çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` üzerinde (yeni dal açılmaz):

```bash
git checkout feature/sce/mod-0173-inventory-ledger-valuation
git merge-base --is-ancestor ct/line-ff-2026-11-14d HEAD && echo "ata — devam"
# ata değilse:
git merge --ff-only ct/line-ff-2026-11-14d
# ff olmazsa:
git merge --no-ff ct/line-ff-2026-11-14d
# çakışmada: git merge --abort, dur, CT'ye bildir (çakışmayı kendin çözme)
```

- Etiketi CT oluşturur; etiket yoksa **dur**, CT'ye sor.
- Sonra doğrula (biri tutmazsa **dur**):
  - `git merge-base --is-ancestor 66a134c89 HEAD` çıkış 0 (onay satırı);
  - paket LF sha256 yukarıdakiyle aynı (`git show HEAD:<paket> | sha256sum` — CRLF dönüşümü olmadan);
  - `git show HEAD:docs/analysis/contracts/wms-count-bundle.openapi.yaml | grep '^  version:'` → `1.2.0`; fikstür `wms-count-bundle-v1.1.0.openapi.yaml` var;
  - `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests --filter "FullyQualifiedName~WmsCountBundleContractTests"` yeşil
    (dondurma adımında 13 / 13).
- Dilim başı commit'ini (`<dilim-başı>`) kaydet; bütün "önce" ölçümleri bu commit'te, aynı gün alınır:
  - `CentralAuditDeclarationTests`: `WriteCommands.Length` ve `seen` — HEAD `60379b0c1`'de `:64` **136** ve `:145` **16** (paket AC-12 `:59` 133 der —
    sonraki dilimlerle kaydı; değer **dilim başında ölçülür**). DL-1'de **değişmemeli** (önce = sonra). Komut: `--filter "FullyQualifiedName~CentralAuditDeclarationTests"`
    dilim başında ve sonunda (AC-12).
  - Gateway `Mod0181CycleCountRouteTests` 25 uç (`:32`) ve `Mod0178WarehouseRouteTests` `Assert.Equal(105, endpoints.Count)` (`:113`, HEAD `60379b0c1`) —
    DL-1'de **değişmez** (yeni uç yok).
  - `StubProductMaster.GetSkuAsync` çağrı sayısı için AC-5 tabanı: FU öncesi davranışta N farklı SKU'lu release = N çağrı (sayaç X-3 ile eklenir;
    taban, sayaç eklendikten sonra üretim kodu değişmeden ölçülür).
  - Inventory, Web.Tests, Gateway filtresiz taban (kırmızılar adıyla — §8).

## 4. İlk adımlar (kod yazmadan önce)

### 4.1 XMC-001 kayıt denetimi
- **X-1** (MOD-0173 `SkuReference.cs` + `ProductMasterHttpClient.cs`) ve **X-3** (`StubProductMaster.cs`): onaylı (§1.1). Kapsam paket §24 X tablosundaki
  satırla sınırlıdır: `SkuReference`'a **sona** `string? Name = null` (konumlu kurucu korunur, mevcut çağıranlar değişmez); istemci `getSku` cevabından
  `name`'i `OptionalString` ile okur (`canonicalCode` satırının yanında); sahte istemci `getSku` cevabına `SetDisplay` adını `Name` olarak koyar ve
  `GetSkuCalls` sayacı ekler — mevcut testlerin davranışı değişmez.
- Bu listede olmayan başka modül dosyası gerekirse **dur**, CT'ye yaz (XMC-001). Onay kayıtları teslim raporuna yazılır.

### 4.2 Sözleşme önce
- Kod, donmuş **1.2.0**'a karşı yazılır. Servis cevapları entegrasyon testlerinde dosyanın kapalı şemasıyla doğrulanır (`CycleCountContract`,
  `CycleCountContractHandler`); dosyada olmayan üye yazan cevap kırmızıdır. Dosyaya, fikstürlere ve dondurma testlerine (başlık, fikstür,
  eklemeli-sürüm) **dokunulmaz**.
- **AC-14 (DL-1):** `WmsCountBundleContractTests.cs`'e **yalnız** yeni parametre eşitliği testi eklenir — `listCycleCounts` sorgu parametreleri
  (`status`, `warehouseId`, `q`, `assignedTo`, `unassigned`, `page`, `pageSize`) ↔ `CycleCountsController.List` `[FromQuery]` adları, iki yönde.
  Dosyanın başka satırı değişmez.

### 4.3 Kod ölçümü (paket satırları HEAD `ca36b19be`'de alındı — dilim başında yeniden doğrula)
- `CycleCountRepository.cs:266-280` (`ScopeSkusAsync`), `:85-97` (depo süzgeci), `:321-328` (`LocationNamesAsync`), `:401-408` (`ReplaceLinesAsync`);
  `ReleaseCycleCountHandler.cs:50-53`, `:89-93`, `:152-160`; `RecordCountLineHandler.cs:53-61`; `SubmitCycleCountHandler.cs:181-189`;
  `AddCountLineHandler.cs:91-96`, `:133-135`; `RequestCountRecountHandler.cs:236-242`; `CycleCountProductFlags.cs:30-44`, `:62`; `CycleCountReads.cs:47-83`;
  `CycleCountsController.cs:32-36`; `ProductMasterHttpClient.cs:65-84`; `StubProductMaster.cs:86`, `:90-105`.
- `ScopeSkusAsync`'in bütün kullanımı (HEAD `60379b0c1`, depo geneli arama): üretim `ICycleCountRepository.cs:188`, `CycleCountRepository.cs:266`,
  çağıran `ReleaseCycleCountHandler.cs:52`; test iletici `CycleCountTestInfrastructure.cs:200-201` (**F-11**, §7.1).

## 5. Dokunulacak dosyalar

`INV` = `services/Diten.InventoryService/src/Diten.InventoryService.`, `INVT` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`
(paket §5 DL-1 satırları birebir + F-11 satırı):

**MOD-0181'in kendi dosyaları:**
- `INV/Domain/CycleCount/Entities/CycleCountEntities.cs` — `CycleCountLine`'a dört alan.
- `INV/Domain/CycleCount/Repositories/ICycleCountRepository.cs` — `CycleCountQuery` + `AssignedTo` (Guid?), `Unassigned` (bool); `ScopeSkusAsync` yerine
  `ScopePreviewAsync` imzası.
- `INV/Persistence/CycleCount/CycleCountRepository.cs` — `ListAsync` süzgeci; `ScopePreviewAsync` (aynı iki sorgu; bin'ler, SKU anahtarları, kayıtlı lot
  kimlikleri); bin kodu mevcut `LocationNamesAsync` ile.
- `INV/Persistence/CycleCount/CycleCountIndexConfiguration.cs` — §4.2 indeksi.
- `INV/Application/Features/CycleCounts/Queries/GetCycleCountListQuery.cs` — iki parametre (ham metin).
- `INV/Application/Features/CycleCounts/Handlers/QueryHandlers/GetCycleCountListHandler.cs` — çözüm + doğrulama (validator yok; §10).
- `INV/Application/Features/CycleCounts/Handlers/CommandHandlers/ReleaseCycleCountHandler.cs` — dört üye; `ILotRepository` bağımlılığı; ön okuma önbelleği.
- `INV/Application/Features/CycleCounts/Handlers/CommandHandlers/RecordCountLineHandler.cs` — giriş satırı dört üyeyi taşır.
- `INV/Application/Features/CycleCounts/Handlers/CommandHandlers/SubmitCycleCountHandler.cs` — `WithVariance` dört üyeyi taşır.
- `INV/Application/Features/CycleCounts/Handlers/CommandHandlers/AddCountLineHandler.cs` — bulunan satıra dört üye.
- `INV/Application/Features/CountVariances/Handlers/CommandHandlers/RequestCountRecountHandler.cs` — kaynaktan kopya.
- `INV/Application/Features/CycleCount/Services/CycleCountProductFlags.cs` — aynı çağrıdan kod + ad.
- `INV/Application/Features/CycleCount/Services/CycleCountReads.cs` — `Line` eşlemesi.
- `INV/Application/Features/CycleCounts/CycleCountModels.cs` — `CycleCountLineDto` dört üye.
- `INV/Api/Controllers/CycleCount/CycleCountsController.cs` — `List` eylemine iki `[FromQuery]`.
- `INVT/CycleCount/CycleCountDisplayCodesTests.cs` (yeni), `INVT/CycleCount/CycleCountListFilterTests.cs` (yeni).
- `INVT/CycleCount/WmsCountBundleContractTests.cs` — **yalnız** AC-14 parametre eşitliği testi (yeni test yöntemi; başka satır yok).
- `INVT/CycleCount/CycleCountTestInfrastructure.cs` — **yalnız** `SeamedCountRepository`'nin `ScopeSkusAsync` ileticisi (`:200-201`) →
  `ScopePreviewAsync` ileticisi (**F-11**, §7.1).

**XMC-001 seam'leri (yalnız adlı satırlar):**
| # | Dosya | Değişiklik | Sahip / onay |
|---|---|---|---|
| X-1 | `INV/Application/Common/Upstream/SkuReference.cs`, `INV/Infrastructure/Upstream/ProductMasterHttpClient.cs` | `SkuReference` sonuna `string? Name = null`; istemci `getSku` `name`'i okur | MOD-0173 (merkez, Beste) — onaylı |
| X-3 | `INVT/TestInfrastructure/StubProductMaster.cs` | `getSku` cevabına `SetDisplay` adı (`Name`) + `GetSkuCalls` sayacı; mevcut davranış aynı | paylaşılan test altyapısı (Beste) — onaylı |

**Dokunulmayacaklar:** paket dosyası; `docs/analysis/contracts/wms-count-bundle.openapi.yaml`; `INVT/Contract/Fixtures/**`; `WmsCountBundleContractTests.cs`'in
başlık / fikstür / eklemeli-sürüm testleri; `gateway/**` (X-2 CT'de yapıldı; `ocelot.json` değişmez); `frontend/**` (Web dosyası değişmez — AC-15);
`.antigravity/**`; `_Layout.cshtml`, Archive; `execution/registries/module-id-registry.md` (CT); `INV/**/Barcode/**` ve `ScanVerifier.cs` (MOD-0182 FIX-01);
`CountEvidenceDocumentBuilder.cs` ve Evidence dosyaları; `CycleCountReleaseTests.cs` (AC52a koruyucu **değişmeden** yeşil kalır); `INVT/CentralAudit/**`
(sayı değişmez); X-1 / X-3 satırları dışındaki MOD-0173 dosyaları; başka domain servisleri; bu WP dosyası.

## 6. Uygulama sırası (öneri)

- **A.** Adım 0 → §4.1 → taban koşuları ve "önce" ölçümleri.
- **B.** X-1 + X-3 (`SkuReference.Name`, istemci `name` okuması, sahte istemci `Name` + `GetSkuCalls`) — mevcut testler değişmeden yeşil (ölç).
- **C.** Varlık + DTO + `CycleCountReads.Line` (dört üye, `ShowsExpected`'ten bağımsız).
- **D.** `ScopePreviewAsync` (arayüz + depo + **F-11 iletici aynı commit'te** — imza değişince test projesi aksi halde derlenmez) → release ön okuma
  önbelleği → bulunan satır → yeniden sayım kopyası → giriş / submit taşıma.
- **E.** Liste süzgeci: sorgu, işleyici, depo, controller `[FromQuery]`, indeks; AC-14 testi.
- **F.** Testler (`CycleCountDisplayCodesTests`, `CycleCountListFilterTests`), sabotajlar, tam koşular, E4 canlı ölçümü (AC-15), teslim raporu.

## 7. Kabul ölçütleri (DL-1: AC-1…AC-12, AC-14, AC-15, AC-16 + CT şartı F-11)

Ayrıntı paket §16'dadır; her vaka paketteki metinle ölçülür (özet yetmez — aç). AC-13 CT dondurma adımınındır (bu dilimde değil).

| AC | Kısa başlık | Sabotajlar |
|---|---|---|
| AC-1 | Release Snapshot satırı lines ucunda `locationCode`, `skuCode`, `skuName`, kayıtlı lotlu satırda `lotNumber`; aynı üyeler `getCountVariance` `line`'da | S-FU01-1 |
| AC-2 | `addCycleCountLine` 201 cevabı ve sonraki liste dört üyeyi taşır; `lotNumber` doğrulanan kayıtlı lotun numarası | S-FU01-2 |
| AC-3 | `requestCountRecount` Recount satırları dört üyeyi kaynaktan aynen taşır | S-FU01-3 |
| AC-4 | Eksik kaynakta üye yazılmaz, yazım durmaz: (a) `canonicalCode` / `name` yok → 200, üye yok; (b) kayıtlı biçimde lot yok → `lotNumber` yok; (c) elle lot metni (`LOT-A1`) aynen; (d) UUID / boş / JSON null yok | S-FU01-4a, S-FU01-4b |
| AC-5 | Ek PRODUCT-MASTER çağrısı yok: N SKU'lu release'te `GetSkuCalls` = N, bulunan satırda 1; release **tek** anlık görüntü işlemi — AC52a (`Assert.Equal(1, probeCalls)`) değişmeden yeşil + FU01 lotlu çok bin'li vaka aynı sayı | S-FU01-5, S-FU01-5b |
| AC-6 | Kör sayım: submit öncesi (ve sonrası `variances.read` olmayana) dört üye var, beş kör üye yok; kör olmayanda ikisi birden | S-FU01-6a, S-FU01-6b |
| AC-7 | `skuName` aynı satırda `Accept-Language: tr` ve `ar` ile aynı ve sahte PRODUCT-MASTER `name`'ine eşit; resx metni yok | S-FU01-7 |
| AC-8 | `assignedTo={u}` yalnız `AssignedTo = u`; `status` / `warehouseId` / `q` ile AND; `total` süzülmüş; 250 kayıtta `pageSize=200` ikinci sayfa 50 | S-FU01-8 |
| AC-9 | `unassigned=true` yalnız atanmamış; `false` = süzgeçsiz; `assignedTo=abc`, `unassigned=yes`, ikisi birlikte → 400 `INVALID_REQUEST` | S-FU01-9 |
| AC-10 | Aynı kullanıcıya atanmış başka şirketin / kiracının sayımı listede yok; satırları 404 | S-FU01-10 |
| AC-11 | İzin değişmez: yalnız `counts.read` taşıyan (Sayan) dört üyeyi ve süzgeci alır; `CycleCountPermissions.Implemented` aynı; `Every_served_cycle_count_route_is_an_operation_with_its_permission_key` yeşil | S-FU01-11 |
| AC-12 | `Released` / `LineAdded` `Data`'sında dört üye yok; `Every_write_command_declares_its_central_audit_record` ve `Every_transaction_a_command_can_run_in_enlists_the_central_audit_event` sayıları dilim başındakiyle aynı | S-FU01-12 |
| AC-14 | `listCycleCounts` sorgu parametreleri ↔ `CycleCountsController.List` `[FromQuery]` iki yönde eşit (yeni test, `WmsCountBundleContractTests.cs`) | S-FU01-14 |
| AC-15 | Kanıt maddesi: Inventory (filtresiz), Gateway (`Mod0181CycleCountRouteTests` 25, `Mod0178WarehouseRouteTests` değişmeden), Web.Tests değişmeden yeşil; E4 canlı ölçümünde PRODUCT-MASTER `getSku` `name` gönderiyor mu yazılır | — (AC-1…AC-14, AC-16 toplamı) |
| AC-16 | Dört üye sonraki her yazımda korunur: giriş, submit, onay, ret, yeniden sayım, `Pending` retry, `IntegrityError` resolve — her adımdan sonra lines ve `getCountVariance` `line` ilk değerle | S-FU01-16a, S-FU01-16b |

**Sabotaj metinleri (paket §16, kısaltılmadan):** S-FU01-1 release satır kurucusunda `SkuCode` atamasını sil · S-FU01-2 `AddCountLineHandler`'da
`LotNumber` atamasını sil · S-FU01-3 `RecountLine` içinde `LocationCode` kopyasını sil · S-FU01-4a ad yokken `SkuName = SkuId` yaz → (d) kırmızı ·
S-FU01-4b ad yokken release'i 422 ile reddet → (a) kırmızı · S-FU01-5 kod / ad için ayrı bir `GetSkuAsync` çağrısı ekle → sayı iddiası kırmızı ·
S-FU01-5b ön okumadan bin kodlarının önbelleğe yazılmasını kaldır (yalnız `NeedsFlags` yedeği kalsın) → AC52a ve FU01 vakası kırmızı (`probeCalls` 2) ·
S-FU01-6a dört üyeyi `showExpected` koşuluna bağla → kör vakada üye eksik · S-FU01-6b `Line` eşlemesinde `ExpectedQuantity`'yi koşulsuz yaz → kör vaka
kırmızı · S-FU01-7 `CycleCountReads.Line`'da `SkuName`'i isteğin diline göre seç (ör. `tr` değilse `SkuCode`) → `ar` vakası kırmızı · S-FU01-8 depo
süzgecinde `AssignedTo` koşulunu atla · S-FU01-9 ikisi-birlikte denetimini sil · S-FU01-10 `ListAsync`'te süzgeci `Company<CycleCount>()` yerine yalnız
`AssignedTo` ile kur · S-FU01-11 dört üyeyi `variances.read` sahibine sınırla · S-FU01-12 `LineAdded` verisine `skuName` ekle · S-FU01-14 eylemdeki
`unassigned` parametresini kaldır · S-FU01-16a `SubmitCycleCountHandler.WithVariance`'ta `SkuCode`'un taşınmasını kaldır → submit sonrası vaka kırmızı ·
S-FU01-16b `RecordCountLineHandler` satır kurucusunda `LocationCode`'un taşınmasını kaldır → giriş sonrası vaka kırmızı.

### 7.1 CT şartı (paket dışı — r4 incelemesi F-11'i kapatır)

Kaynak: `docs/records/reviews/MOD-0181-FU01/2026-11-13-3ccd6397.md` **F-11** (DÜŞÜK, sahibi CT, tarih 2026-11-17). Paket §8.1 "tek çağıran release'tir"
der; oysa `ICycleCountRepository`'nin test uygulayıcısı **`SeamedCountRepository`** (`INVT/CycleCount/CycleCountTestInfrastructure.cs:171`, iletici
`:200-201`) `ScopeSkusAsync`'i `inner`'a iletir. İmza değişince test projesi derlenmez; dosya MOD-0181'in kendi test altyapısıdır (XMC gerekmez) ama
paket Repo Scope'unda yoktur.

- **Şart:** `SeamedCountRepository`'deki iletici `ScopePreviewAsync` ileticisine çevrilir — `inner.ScopePreviewAsync(request, cancellationToken)`'a
  **birebir** iletir (süzme, kopya mantık, ek sayaç yok); dosyanın başka satırı değişmez. Arayüz değişikliğiyle **aynı commit'te**.
- **Kanıt:** dilim sonunda `git grep -n "ScopeSkusAsync" -- services/Diten.InventoryService` **0 satır**; `git diff <dilim-başı>..<dilim-sonu> --
  services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount/CycleCountTestInfrastructure.cs` yalnız iletici satırları; AC52a ve
  S-FU01-5b bu iletici üzerinden koşar (iletici yeni davranış eklemez — S-FU01-5b'nin kırmızısı üretim kodundan gelir).
- Rapor bu şartı kendi satırında, inceleme kaydına atıfla yazar. Paket metni değişmez; CT inceleme kaydında F-11'i bu kabul kaydıyla kapatır.

**Sabotaj listesi (§24 DL-1 satırı, tam):** S-FU01-1, S-FU01-2, S-FU01-3, S-FU01-4a, S-FU01-4b, S-FU01-5, S-FU01-5b, S-FU01-6a, S-FU01-6b, S-FU01-7,
S-FU01-8, S-FU01-9, S-FU01-10, S-FU01-11, S-FU01-12, S-FU01-14, S-FU01-16a, S-FU01-16b = **18 sabotaj** (AC-15 kanıt maddesi — kendi sabotajı yok;
F-11 derleme şartıdır, ayrı sabotajı yok).

**Beklenen kırmızı test adları (pakette / repoda adı geçenler):** S-FU01-5b → `CycleCountReleaseTests.AC52a_a_posting_during_the_release_is_in_both_reads_or_in_neither`
+ FU01 lotlu çok bin'li vaka; S-FU01-11 → yeni Sayan vakası (ve / veya `CycleCountAccessTests`); S-FU01-12 → yeni olay verisi vakası; S-FU01-14 → yeni
AC-14 testi. Diğerlerinin test adı pakette yok — geliştirici adlandırır ve raporda her `S-FU01-n` ile eşler.

Kurallar: her sabotaj **üretim koduna** uygulanır (kendi kopyasıyla ölçen test guard değildir — Çekirdek md. 10); tek satırdır, kırmızı çıktı alınır,
bozma bayt-eşit geri alınır, **commit'e girmez**; listede olmayan her kırmızı adıyla raporlanır.

## 8. Test koşuları (tam koşu filtresiz)

- **InventoryService, filtre yok:** `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests`. Taban `<dilim-başı>`'nda aynı gün;
  sonda yeni kırmızı yok. Mevcut sayım testleri (`CycleCount*`, kör sayım testleri, AC52a) **değişmeden** yeşil. Bilinen yük duyarlı süre testleri
  kırmızıysa tek başına iki kez koşup sonucu yaz.
- **Web.Tests, filtre yok:** `dotnet test frontend/Diten.Web.Tests` — Web dosyası değişmez; taban ile aynı (AC-15). Taban `<dilim-başı>`'nda ölçülür.
- **Gateway, filtre yok:** `dotnet test gateway/Diten.ApiGateway.Tests` — `Mod0181CycleCountRouteTests` 25 uç (sözleşme 1.2.0), `Mod0178WarehouseRouteTests`
  sayısı değişmeden (AC-15).
- **vitest:** bu dilimde JS değişmez; koşulursa **yalnız repodaki `node_modules`** (`frontend/Diten.Web/node_modules/.bin/vitest`; `npx`'in npm'den paket
  indirmesine izin verme — o koşu sayılmaz). Ayrık worktree'de koşuluyorsa `node_modules` için kayıtlı junction yöntemi.
- **AC-12 ölçümü:** `--filter "FullyQualifiedName~CentralAuditDeclarationTests"` dilim başında ve sonunda (sayılar aynı).
- **E4 canlı ölçümü (AC-15):** servis Gateway (5000) üzerinden; release edilen bir sayımın lines cevabında dört üye; PRODUCT-MASTER `getSku` `name`
  canlıda geliyor mu (gelmiyorsa `skuName` yok — AC-4 davranışı) yazılır. Portlar yalnız AGENTS.md §3'ten.
- **Bekçi kanıtı:** `git diff --stat <dilim-başı>..<dilim-sonu> -- frontend gateway docs/analysis/contracts services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Contract`
  **boş**; `-- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount/CycleCountReleaseTests.cs services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CentralAudit`
  **boş**.
- Build: `Diten.InventoryService` PASS, yeni uyarı yok (paket §17 "`dotnet build` uyarısız" — dilim başındaki uyarı listesiyle karşılaştır).

**Ortam:**
- Port **27017 kullanma** (Mongo 27017 yok). **`INVENTORY_TEST_MONGO` boş bırak** — test paketi kendi replika setini başlatır
  (`DITEN_TEST_MONGOD` gerekirse `C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe`).
- **`Diten.AuthService.AccountKindAcceptanceHost.Tests` koşma.**
- Portlar yalnız AGENTS.md §3'ten (Web 5001, Gateway 5000, Inventory 5067); port uydurma.
- Ana checkout'ta Inventory 5067 `dotnet watch` ile çalışıyorsa derleme kilidi olur; derleme / sabotaj ayrık worktree'de yapılabilir — commit yine
  çalışma dalına.

## 9. Git kuralları (GIT-002)

- Çalışma dalı **`feature/sce/mod-0173-inventory-ledger-valuation`** — yeni dal açılmaz; `main`'e commit / push yok.
- Stage yalnız **açık dosya yoluyla** (`git add <yol> …`); `git add -A` / `git add .` yok. İzlenmeyen `__pycache__/` ve review ham dosyaları stage'e girmez.
- Hook atlama yok (`--no-verify` yok), imza atlama yok; yıkıcı git komutu (`reset --hard`, `push --force`, `checkout --`) onaysız yok.
- Yerel commit'ler serbest; `ScopePreviewAsync` imzası, depo, release çağrısı ve F-11 iletici **aynı commit'te**.
- **Push / PR yok.** Commit mesajı sonu: `Co-Authored-By` satırı oturumun atıf kuralıyla.

## 10. Teslim raporu biçimi

1. **Commit listesi** (`git log --oneline <dilim-başı>..HEAD`), `git diff --stat`, Adım 0'da hangi yol (ata / ff / no-ff).
2. **Test sayıları önce / sonra:** Inventory, Web.Tests, Gateway (toplam / geçen / kalan / atlanan; kırmızılar adıyla); vitest koşulduysa kaynağı.
3. **Sabotaj tablosu:** her `S-FU01-n` (18) için — üretim dosyası:satır, değişiklik, kırmızı test adı + çıktı özeti, bayt-eşit geri alma, geri yeşil kanıtı.
4. **Sayılar önce → sonra:** `WriteCommands.Length` ve `seen` (değişmedi; satır ve ölçüldüğü commit), `GetSkuCalls` (release N, bulunan satır 1),
   `probeCalls` (1), Gateway 25 / 105 (değişmedi).
5. **CT şartı F-11:** iletici diff'i, `git grep ScopeSkusAsync` 0 satır çıktısı (inceleme kaydına atıf).
6. **XMC-001:** X-1 ve X-3 diff'leri (yalnız adlı satırlar), onay kaydına atıf; bekçi kanıtı diff'leri (§8).
7. **E4 canlı ölçümü (AC-15):** istek / cevap özeti; `getSku` `name` canlıda geliyor mu.
8. **Kabul kapısı:** mobil bilinmeyen üye toleransı teyidi **açık** (sahip kararı 2026-11-14, karar kaydı `:107-108`); kabul / mobil yayın öncesi CT kapatır.
9. **Açık kalanlar:** sorular, ölçülemeyenler (nedeniyle), takip işleri (§20) ile ilişki.

## 11. Kural haritası (AGENTS.md §6.1)

- **Daima:** `multi-tenancy` (süzgeç şirket süzgecine AND, kiracılar arası 404), `security-jwt`, `git-safety`, `code-style`, `docs-organization`,
  `cross-module-change-consent` (XMC-001: X-1, X-3).
- **Backend:** `handler-design`, `repository-standard`, `response-envelope` (boş üye yazılmaz, JSON null yok), `api-conventions`, `routes`, `ports`,
  `mongo-indexing` (bir yeni indeks, §4.2).
- **Yetki:** `permission-key-standard` (yeni anahtar yok), `business-module-enforcement-standard`.
- **Metin:** `localization-standard` — servis `skuName`'i çevirmez, resx metni yazmaz (§8.2).
- **Kayıt:** `logging-observability`, `status-reporting-and-evidence` (REP-001), `dev-runbook`.

## 12. Belirsizlikler (CT'ye; geliştirici kendi başına çözmez)

- **B-1 (açık — kabul kapısı, CT):** mobil bilinmeyen üye toleransı teyidi (§1.2). DL-1'i bekletmez; kabulü ve mobil yayını bekletir.
- **B-2 (CT notu):** onay satırı özetinde (`approvals.tsv:98`) "binCode" yazar; paket r4 §3 / §4.1 / §8.5 ve donmuş 1.2.0 üyenin adını
  **`locationCode`** koyar. Paket kazanır (yetki sırası); kod `locationCode` yazar.
- **B-3 (ölçüm notu):** paket AC-12 `WriteCommands.Length` 133 (`:59`) der; HEAD `60379b0c1`'de 136 (`:64`). Sayı dilim başında ölçülür; DL-1 değiştirmez.
- **B-4 (ölçülmedi):** canlı MDM `getSku` `name` gönderiyor mu — E4 ölçümü (AC-15); gelmezse `skuName` yok, diğer üç üye gelir.
- **B-5 (CT kararı):** sözleşme 1.2.0 CT'nindir; geliştirici dosyaya yazmaz, uyuşmazlıkta durur.

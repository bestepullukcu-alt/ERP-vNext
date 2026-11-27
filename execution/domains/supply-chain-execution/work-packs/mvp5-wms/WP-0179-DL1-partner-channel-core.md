# WP-0179-DL1 — MOD-0179 3PL Entegrasyonu DL-1: 3PL-BUNDLE taslağı + ortak, kanal (sırsız), eşleme, eşik, tek lokasyon kuralı (arka uç)

Tarih: 2026-11-14 · Veren: CT (MVP-5 WMS CT, sahip Beste) · Durum: **taslak — başlama kapıları açık (§0 madde 3, §5 K-0)**
- Modül: **MOD-0179 — 3PL Integration**, servis `Diten.InventoryService` (5067), kiracı modülü (7 dil). Sahip: geliştirici oturumu.
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md` **r3**, LF sha256
  `1ac6e1a7a22a05c90c56a69ffe65e2a9b7ea62b79e35b7db31eadbddc7d354ab` (CT ölçümü, HEAD `b91cfcc3d`), `status: ready-for-dev`.
  Künyedeki `approval_status: pending` paket metnidir; onay defter satırıyla verilir (paket sha'sı korunsun diye künye değişmedi):
  `execution/approvals/approvals.tsv:94` (commit `b3e9e6b7a`) — onaylayan Ali (GitHub `GMG-ERP`), yazan `bestepullukcu-alt`, 2026-11-13.
  Bağımsız inceleme r3 **UYGUN, bulgu yok**: `docs/records/reviews/MOD-0179/2026-11-13-1ac6e1a7.md` (`reviews.tsv:377`, commit `4d71418ea`).
- **`.antigravity` kapısı: kapandı** — commit `08a3a6ac9`: PKS-001 §4 `warehouse.third-party-logistics.*` satırı
  (`.antigravity/rules/permission-key-standard.md:108`, `:92` istisna cümlesi, `:105` "hariç" eki), RCS-001 §3 `TPX` satırı
  (`.antigravity/rules/record-coding-standard.md:75`), X-18 `RecordCodeTypeCatalogTests` `RuleAheadOfCatalogue["TPX"] = "MOD-0040-FU02 DL-8"`
  (`services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs:34`). X-17 (MOD-0178 §14.1 notu)
  MOD-0178'in sonraki içerik revizyonuna bırakıldı; kural PKS-001 satırıyla bağlayıcı (kararlar 01 son maddeler). Bu WP `.antigravity/**`'a yazmaz.
- Dilim: **DL-1** (paket §23 satırı, §24 DL-1 satırı), kanıt seviyesi **E4**. Dilim sırası (sahip kararı, `mvp5-wms-ct-owner-decisions-2026-11-13.md`
  ilk madde): MOD-0180 DL-1 → MOD-0182 DL-1 → **MOD-0179 DL-1**; MOD-0182 DL-1 **VERIFIED 2026-11-14** (FIX-01 `b19765ef2`; kayıt
  `docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md`, commit `b91cfcc3d`) → sıra kapısı açık.
- Yetki: paket > `domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse **paket geçerlidir**; çelişkiyi CT'ye yaz, kendin çözme.
  Bu WP push, PR ya da `.antigravity/**` değişikliği izni değildir.

`INV` = `services/Diten.InventoryService/src/Diten.InventoryService.`, `INVT` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/` (paket §5).

## 0. Başlamadan

1. **Dal ve CT etiketi.** Tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation` (ana checkout `C:\Users\Ali\erp-vnext`); yeni dal
   açma. CT etiketi **`ct/line-ff-2026-11-14a`**:
   - etiket dalın atasıysa (`git merge-base --is-ancestor ct/line-ff-2026-11-14a HEAD` çıkış 0) devam;
   - değilse `git merge --ff-only ct/line-ff-2026-11-14a`;
   - ff olmazsa `git merge --no-ff ct/line-ff-2026-11-14a`;
   - çakışmada `git merge --abort` ve CT'ye yaz — **çakışmayı kendin çözme**.
   - **CT notu:** etiket bu WP yazılırken (HEAD `b91cfcc3d`) henüz **yoktu**; CT başlama mesajında etiketi verir. Etiket yoksa **dur**.
   - Sonra doğrula (her biri çıkış 0): `git merge-base --is-ancestor b3e9e6b7a HEAD` (onay), `… 08a3a6ac9 HEAD` (`.antigravity` kapısı),
     `… b91cfcc3d HEAD` (0182 DL-1 VERIFIED). Değilse dur.
2. **Paket sha'sı:** `tr -d '\r' < execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md | sha256sum` → `1ac6e1a7…`
   ile başlamalı; ölç ve raporun ilk satırına yaz. Farklıysa **dur**.
3. **Başlama kapısı — XMC-001 (paket §5.1 "diğer satırlar dilim başlamadan sahibinden onay ister"):** DL-1 seam'leri X-4, X-5, X-6 (a) / (b),
   X-8, X-9 ve bu WP'nin eklediği **X-W** (§1.3) için **sahip onay kaydı henüz yok** (CT ölçümü: `docs/records/decisions/2026-11/` altında MOD-0179
   için XMC onayı bulunmadı; MOD-0180 X-14 onayı `c7dc4ea6a` / `6235794ab` yalnız 0180 dilimlerini kapsar). CT onayları Beste'den alır ve karar
   kaydını commit'ler; commit kimlikleri başlama mesajında verilir. **Onay commit'i olmayan bir seam dosyasına dokunma** — kendi ağacındaki işi
   (§1.2 "0179'un kendi dosyaları") sürdürebilirsin, ama seam'siz kod yeşile çıkmayacağı için o seam'i bekleyen commit'i atma; CT'ye yaz.
4. **Dilim başı commit'i (`<dilim-başı>`)** kaydet; bütün "önce" ölçümleri bu commit'te, aynı gün alınır.
5. **Dilim başında ölçülecek sayaçlar** — aşağıdaki değerler CT'nin `b91cfcc3d` ölçümüdür; MOD-0180 DL-2 hatta yeni commit eklediyse değişmiş
   olabilir. **Satıra (kodun kendisine) atıf yap, satır numarasına değil**; rapora `<dilim-başı>` değerini yaz:

   | Sayaç | Dosya / satır (`b91cfcc3d`) | `b91cfcc3d` | DL-1 değişikliği | Beklenen sonra (çıkarım) |
   |---|---|---|---|---|
   | Gateway uç sayısı (X-8) | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs:113` `Assert.Equal(<n>, endpoints.Count)` (yorum `:78-112`) | **105** | **+17** (§1.1 uç listesi) | 122 |
   | Merkezi denetim yazma komutu (X-6 (a)) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs:62` `Assert.Equal(<n>, WriteCommands.Length)` (yorum `:50-61`) | **135** | **+12** (§1.1 komut listesi) | 147 |
   | Merkezi denetim `seen` (X-6 (b)) | aynı dosya `:143` `Assert.Equal(<n>, seen)` (yorum `:137-142`) | **16** | öneri **+1** (`ThirdPartyTransactions.InTransactionAsync`) | 17 |
   | Merkezi denetim modül listesi (X-6 (a)) | aynı dosya `:70-80` (son satır `BarcodeRfidManifestProvider()` `:79`) | 9 sağlayıcı | + `ThirdPartyLogisticsManifestProvider` | 10 |
   | Kayıt kodu önek listesi | `INV/Domain/Warehouse/Rules/WarehouseRecordCodeRules.cs:34` `TypePrefixes` | **7** — PUT, PCK, OUT, PKG, CNT, WAV, DVC | **0** — `TPX` **DL-2**'de (X-7, AC-21) | 7 |
   | Platform katalog önü | `RecordCodeTypeCatalogTests.cs:28-39` `RuleAheadOfCatalogue` | 9 girdi, `TPX` `:34` | **0** (dokunulmaz) | 9 |
   | Inventory manifest bloğu (X-4) | `INV/Api/Program.cs:49-69` (son `AddBarcode()` `:69`) | 9 sağlayıcı | +2 satır | — |
   | SSOR bildirimi (X-5) | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` modül vakası (son `BarcodeCollections.All` `:103`) ve `declared` zinciri (son `:127`) | — | + `ThirdPartyLogisticsCollections.All` | — |
   | Manifest bütünlük dışlaması (X-9) | `INVT/Api/InventoryLedgerManifestCompletenessTests.cs:213-227` | **8** satır (paket r2'de 7; arada `Controllers.Barcode` girdi) | +1 satır | 9 |
   | Web sayfasız modül listesi (X-W) | `frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs:189-201` `PagelessUntil` | 4 girdi (`WAVE-PLANNING`, `BARCODE-RFID`, 2 MRP) | + `THIRD-PARTY-LOGISTICS` (§1.3) | 5 |

6. **Eşzamanlılık:**
   - **InventoryService'te MOD-0180 DL-2 sürüyor** (OutboundShipment ve görev dosyaları: `OutboundShipmentEntities.cs`, `OutboundAllocationService.cs`,
     `OutboundPickTaskFactory.cs`, `WarehouseTask`, `Warehouse/Ports/*`, `WavePickIssuanceService.cs`; aynı sayaç dosyaları — `CentralAuditDeclarationTests.cs`,
     `Mod0178WarehouseRouteTests.cs`, gerekirse `Program.cs`). DL-1 OutboundShipment / görev dosyalarına **hiç dokunmaz** (X-10 DL-6'dır). Sayaç
     dosyalarında çakışma beklenir: her sayaç commit'inden **hemen önce** hattı al, sayıyı o anki değerden yeniden hesapla (yalnız 0179'un +17 / +12 / +1'i
     eklenir), yorumda "measured <n> at <commit>" yaz. Çakışmada iki tarafın eklemesini de koru, CT'ye yaz.
   - **Platform'da MOD-0012-FU01 DL-1 (sır kasası)** ve **MDM / Platform'da MOD-0049-FU01 DL-1 (iş ortağı)** sürüyor. DL-1 `services/Diten.Platform/**`
     ve `services/Diten.MdmService/**`'a dokunmaz; iş ortağı **sahte port**la (§1.1), sır kasası v1.0'da yok (F-10).
   - **Ana checkout'ta derleme / test / sabotaj yok** (Inventory 5067 oradan `dotnet watch` ile koşar, derleme kilidi); hepsi **ayrık (detached)
     worktree**'de. Commit yine çalışma dalına.
   - **Makine yükü kuralı:** aynı anda **en çok iki** filtresiz tam koşu (bütün oturumlar toplamı; MOD-0180 DL-2, MOD-0012-FU01, MOD-0049-FU01 de koşuyor).
     **Mongo bağlantı zaman aşımıyla düşen koşu sayılmaz** (ör. yüzlerce kırmızı + `TimeoutException` / sunucu seçimi zaman aşımı) — yeniden koşulur,
     rapora "geçersiz koşu" diye yazılır. Süre testleri (ör. `BarcodeScanTests.Two_hundred_consecutive_scans_stay_under_the_p95_bound`) yük altında
     kırmızı olabilir: tek başına iki kez koşup yeşil olduğunu raporla.
7. **Paketi oku (özet yetmez):** §1, §2 (bütünüyle), §3 (DL-1 nesneleri, komutları, uçları, controller tabanı), §4.1, §4.1.1, §4.2, §4.3, §4.8, §4.9
   (DL-1 koleksiyonları), §4.10, §5, §5.1 (X-4, X-5, X-6, X-8, X-9 ve "0178 / 0181 / 0182 bekçi testleri değişmez"), §6, §7, §8.1, §8.2 (ortak, kanal),
   §8.5 (DL-1'e düşen: sır değeri yok, referans istemciden gelmez), §8.10.1, §8.12, §8.15, §8.16, §10, §12, §13 (DL-1 vakaları), §14, §15, §16 DL-1
   bloğu (AC-1…AC-9, AC-11, AC-19, AC-20, AC-22, AC-23, AC-25, AC-61), §17, §17.1, §21 (DL-1 komutları), §24 DL-1 satırı. Ayrıca iş ortağı okuma
   taslağı `docs/analysis/contracts/drafts/business-partner-read-v1.0-proposal.md` (yalnız sahte port şekli için; bağlayıcı olan DL-4'tür).

## 1. Kapsam

### 1.1 Paket DL-1 satırı (§23, §24)
"(r1 DL-1a) 3PL-BUNDLE taslağı + ortak bağlantısı, kanal (sırsız), eşleme, eşikler, tek lokasyon kuralı + izin, kiracı, denetim (arka uç; çekirdek
dilim)". **Yalnız arka uç** — Web ekranı yok (DL-3).

- **İlk adım — 3PL-BUNDLE taslağı (AC-1, §8.16):** `docs/analysis/contracts/drafts/3pl-bundle-v1-proposal.md` (yeni), sürüm `1.0.0-draft`. İçerik:
  kanonik şema (§8.3 türleri), gelen API (imza `X-TPL-Timestamp` / `X-TPL-Signature` / `X-TPL-Message-Id`, `X-Tenant-Id` kuralı, 401 gövdesiz, §8.4),
  giden API, yönetim uçlarının **tamamı** (42; §3), hata kodları (§8.15); v1.1 ekleri (CSV / XML, EDIFACT / X12 eşleme) **ayrı bölümde**, ücret için
  ayrılmış `InvoiceAdvice` adı (§8.17). **DL-1'in 17 ucu bağlayıcı bölümdür** (kod bu tabloya karşı yazılır); diğer dilimlerin satırları ön taslaktır.
  Biçim emsali `docs/analysis/contracts/drafts/scan-bundle-v1-proposal.md` (TASLAK başlığı, kaynak paket sha'sı, "paket kazanır"). `BRIDGE-BASE`
  karşılığı yok — taslakta "Q-15 açık" notu. **Dondurma ayrı CT adımıdır ve DL-5'ten önce yapılır**; geliştirici `FROZEN` koymaz,
  `docs/analysis/contracts/3pl-bundle.openapi.yaml` yazmaz. Sözleşme testi (`INVT/ThirdPartyLogistics/` altında; emsal `INVT/Barcode/BarcodeDraftContract.cs`)
  DL-1 cevaplarını taslak şemalarına karşı **kapalı** doğrular (bilinmeyen üye = kırmızı; S-1).
- **Komutlar (12, §3, §21):** `CreateThirdPartyPartnerCommand`, `UpdateThirdPartyPartnerCommand`, `SuspendThirdPartyPartnerCommand`,
  `ActivateThirdPartyPartnerCommand`, `UpdateThirdPartyThresholdsCommand`, `CreateThirdPartyChannelCommand`, `UpdateThirdPartyChannelCommand`,
  `CloseThirdPartyChannelCommand`, `OpenThirdPartyChannelCommand`, `CreateThirdPartyMappingCommand`, `UpdateThirdPartyMappingCommand`,
  `DeactivateThirdPartyMappingCommand`.
- **Sorgular (5):** `GetThirdPartyPartnerListQuery`, `GetThirdPartyPartnerByIdQuery`, `GetThirdPartyChannelListQuery`, `GetThirdPartyChannelByIdQuery`,
  `GetThirdPartyMappingListQuery`. (`GetThirdPartyDashboardQuery` DL-2, `GetThirdPartyPerformanceQuery` DL-8.)
- **Uçlar (17, taban `/api/warehouse/third-party-logistics`, §3):** `GET partners` · `POST partners` · `GET partners/{partnerId}` ·
  `POST partners/{partnerId}/update` · `…/suspend` · `…/activate` · `…/thresholds` · `GET channels` · `POST channels` · `GET channels/{channelId}` ·
  `POST channels/{channelId}/update` · `…/close` · `…/open` · `GET mappings` · `POST mappings` · `POST mappings/{mappingId}/update` · `…/deactivate`.
  `{…Id}` `:guid` kısıtlı; **PUT / DELETE yok** (AC-23). Controller'lar `ThirdPartyPartnersController`, `ThirdPartyChannelsController`,
  `ThirdPartyMappingsController` — `[Route("api/warehouse")]`, eylem şablonu tam yol parçası, namespace
  `Diten.InventoryService.Api.Controllers.ThirdPartyLogistics` (**`Controllers.Warehouse`'a konmaz**); `[HasPermission]` **yalnız eylem düzeyinde**
  (sınıf düzeyi yasak — AC-7, S-7d). DL-1'de `AllowAnonymous` **hiç yok** (gelen uç DL-5).
- **İzinler (§14, DL-1 satırları — 11 anahtar, yeni fiil yok):** `warehouse.third-party-logistics.partners.read` / `.create` / `.update` /
  `.activate` / `.deactivate`, `….channels.read` / `.configure`, `….mappings.read` / `.update`, `….settings.read` / `.configure`.
  `ThirdPartyLogisticsPermissions.cs` (`INV/Infrastructure/Authorization/`), `Implemented` listesi; manifest `THIRD-PARTY-LOGISTICS`, SortOrder 179,
  **yalnız `warehouse.third-party-logistics.*`** (AC-7, S-7c). Uç → izin eşlemesi §2 madde B-1'de.
- **Varlıklar ve koleksiyonlar (§3, §4):** `ThirdPartyPartner` (`wms_tpl_partners`), `ThirdPartyChannel` (`wms_tpl_channels`), `ThirdPartyMapping`
  (`wms_tpl_mappings`), `ThirdPartyCommandRequest` (`wms_tpl_command_requests`); `ThirdPartyLogisticsCollections` (0179 Persistence; `WarehouseCollections`
  ile paylaşılmaz). §4.9'un bu dört koleksiyona ait index'leri (partners tekil `BusinessPartnerId` + kısmi çok anahtarlı `WarehouseIds` `Status = Active`;
  channels `(…, PartnerId, Name)` tekil + `Api.InboundKeyId` **küresel** tekil kısmi; mappings iki tekil kısmi; command_requests tekil). Bakiye alanı yok
  (§2.3 kural 1); ad / adres / iletişim / vergi no alanı yok (§4.1, AC-2 S-2b).
- **Kurallar:** ortak ve kanal durum makineleri (§8.2; gerekçe 10..500; geçersiz geçiş 409 `TPL_INVALID_STATE`); v1.0'da `File` / `Edi` kanal
  yaratma 422 `TPL_CHANNEL_TYPE_NOT_ENABLED` (AC-4); `Api` alanları (§4.2: `OutboundBaseUrl` yalnız `https://`, `AuthType`, sunucunun ürettiği
  26 karakterlik değişmez `InboundKeyId`, `TimeoutSeconds` 5..120); eşikler varsayılan ve aralıkları (§4.1.1, Q-6; AC-19); eşleme tekilliği iki
  yönde, pasif eşleme kullanılmaz (AC-11); depo kuralı + **tek stoklanabilir lokasyon** (§8.10.1, AC-3, AC-61); idempotency + If-Match ve §8.1 replay
  sırası (AC-8); kiracı / şirket 404 ve gövde / sorguda kapsam 400 (AC-5, AC-6).
- **Portlar (0179 Application `Features/ThirdPartyLogistics/Ports/`):** `IBusinessPartnerDirectory` (§4.10 — `ListAsync(role, status)`, `GetAsync(id)`
  → `{ id, code, displayName, status, roles, contactSummary }`); DL-1 testleri **sahte uygulamayla** (rol yoksa 422 `TPL_PARTNER_NOT_3PL`, bilinmeyen 404,
  erişilemez 503 `TPL_MDM_UNAVAILABLE`). Gerçek HTTP istemcisi DL-4 (`INV/Infrastructure/ThirdPartyLogistics/` DL-1'de yazılmaz). Üretim DI'si için §2 B-2.
  `IThirdPartySecretResolver` için §2 B-3.

### 1.2 Dokunulacak dosyalar

**0179'un kendi dosyaları (yeni; paket §5 DL-1 satırları):**
- `docs/analysis/contracts/drafts/3pl-bundle-v1-proposal.md`
- `INV/Domain/ThirdPartyLogistics/{Entities,Enums,Rules,Repositories}/` — varlıklar, durum makineleri, eşik / tür kapısı / tek lokasyon kuralları (saf)
- `INV/Application/Features/ThirdPartyLogistics/` — `ThirdPartyContract.cs`, `ThirdPartyErrorCodes.cs`, `ThirdPartyCentralAudit.cs`, `Ports/`
  (`IBusinessPartnerDirectory`; B-3'e göre `IThirdPartySecretResolver`), gerekiyorsa `Resources/ThirdPartyText.{en,tr,fr,es,zh,ar,ru}.resx` (§3 Dil)
- `INV/Application/Features/ThirdPartyPartners/`, `ThirdPartyChannels/`, `ThirdPartyMappings/` — Golden Slim (§10: `sealed record` Command / Query,
  `Handlers/CommandHandlers` + `Handlers/QueryHandlers`, soneksiz handler / validator, dosya başına tek `public` tip, tek `{Module}Models.cs`;
  G-0179-01 silme yok)
- `INV/Persistence/ThirdPartyLogistics/` — depolar, `ThirdPartyLogisticsCollections`, index'ler, başka modül okumaları **yalnız `Find`** (LOCATION, SKU);
  tek ortak işlem yardımcısı önerisi `ThirdPartyTransactions.InTransactionAsync` (X-6 (b))
- `INV/Api/Controllers/ThirdPartyLogistics/` — üç controller
- `INV/Api/ThirdPartyLogistics/` — `ThirdPartyLogisticsManifestProvider`, `ThirdPartyLogisticsServiceCollectionExtensions` (`AddThirdPartyLogistics()`)
- `INV/Infrastructure/Authorization/ThirdPartyLogisticsPermissions.cs`
- `INVT/ThirdPartyLogistics/` — birim, entegrasyon, mimari, sözleşme testleri ve 0179 bekçisi (AC-7: `AllowAnonymous` listesi, sınıf düzeyi izin yasağı,
  kendi manifest bütünlük vakası)

### 1.3 XMC-001 dokunuşları (yalnız eklemeli; satır numaraları `<dilim-başı>`'nda yeniden ölçülür; **onay commit'i §0 madde 3**)

| # | Dosya | DL-1 değişikliği | Sahip | Onay |
|---|---|---|---|---|
| X-4 | `INV/Api/Program.cs` manifest bloğu (`:49-69`) | `AddSingleton<IModuleManifestProvider, ThirdPartyLogisticsManifestProvider>()` + `AddThirdPartyLogistics()` (MOD-0182 satırlarından sonra, yorumla) | MOD-0173 (merkez) | **bekliyor** |
| X-5 | `INVT/Architecture/SingleSystemOfRecordArchitectureTests.cs` | `ThirdPartyLogisticsCollections.All` — modül vakası + `declared` zinciri (MOD-0182 emsali `:103`, `:127`); önek süzgeci değişmez | MOD-0173 (merkez) | **bekliyor** |
| X-6 (a) | `INVT/CentralAudit/CentralAuditDeclarationTests.cs` `WriteCommands.Length` + modül listesi | dilim başı **+12**, yorumda 12 komut adıyla ve "measured <n> at <commit>"; listeye `ThirdPartyLogisticsManifestProvider` | MOD-0173 (merkez, Beste) | **bekliyor** |
| X-6 (b) | aynı dosya `seen` | dilim başı + 0179 Persistence'taki yeni `WithTransactionAsync(` çağrı yerleri (öneri **+1**, tek yardımcı); yorumda "+ ThirdPartyTransactions.InTransactionAsync (MOD-0179 DL-1, XMC-001)"; çağrı yeriyle **aynı commit** | MOD-0173 (merkez) | **bekliyor** |
| X-8 | `gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` — **yalnız** `Assert.Equal(<n>, endpoints.Count)` + üstündeki yorum | dilim başı **+17**, yorumda 17 uç adıyla; **integration-agent** yazar; uçlarla **aynı commit** | integration-agent | **bekliyor** |
| X-9 | `INVT/Api/InventoryLedgerManifestCompletenessTests.cs` dışlama listesi (`:213-227`) | `.Where(t => t.Namespace?.EndsWith(".Controllers.ThirdPartyLogistics", StringComparison.Ordinal) != true)` + yorum "MOD-0179: the 3PL controllers belong to THIRD-PARTY-LOGISTICS (own completeness test, AC-7)"; karşılığı 0179'un kendi bütünlük vakası | MOD-0173 (merkez) | **bekliyor** |
| **X-W** (pakette yok — CT ekledi) | `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` + `frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.cs` `PagelessUntil` | `Nav.Module.THIRDPARTYLOGISTICS` 7 dilde (paket §11 nav anahtarı) + `["THIRD-PARTY-LOGISTICS"] = "MOD-0179 3PL ekran işi (DL-3 …) bekleniyor"` girdisi (DL-3'te silinir). **Neden:** yeni manifest modülü Web.Tests `NavManifestL10nGuardTests`'i kırmızı yapar — MOD-0182 DL-1'de ölçüldü (Nav.Module.BARCODERFID, FIX-01 `b19765ef2`); emsal MOD-0180 D-1 onayı (`mod0180-wave-planning-owner-decisions-03.md:73`) | Web ortak (merkez, Beste) | **bekliyor** |

**Bu dilimde değil:** X-7 (`TypePrefixes` `TPX`) **DL-2**; X-10 / X-11 / X-12 DL-6; X-13 DL-7; X-14 DL-5 sonrası FU; X-15 DL-3 / DL-8; X-16 ayrı WP;
X-19 (Gateway ara katmanı) DL-5. X-17 / X-18 `.antigravity` kapısında kapandı (`08a3a6ac9`; X-17 MOD-0178 sonraki revizyonu).

**Değişmez (bağlayıcı):** `INVT/Warehouse/**`, `INVT/CycleCount/**`, `INVT/Barcode/**` (paket §5.1 sonu) ve ayrıca `INVT/WavePlanning/**` **hiçbir satırı**;
`WarehouseRecordCodeRules.cs`; `AuthorizationGuardTests`, `LedgerImmutabilityArchitectureTests`, `ManifestNamespaceOwnershipTests`, `WarehouseRegistrationTests`;
`Platform` `RecordCodeTypeCatalogTests`. Bu WP dosyası, paket, kararlar ve inceleme kayıtları değişmez.

### 1.4 Kapsam dışı
DL-2: mesaj defteri, kanonik işlemci, istisna kutusu, `TPX` (X-7), bekletilenler, 90 gün, sağlık / KPI / dashboard. DL-3: Web ekranları, `.resx` sayfa
kaynakları, `Perm.Entity.*` (X-15), yardım. DL-4: `IBusinessPartnerDirectory` gerçek istemcisi (FU01 API'si `main`'de kapısı). DL-5: imzalı gelen uç,
`SetThirdPartyChannelSecretRefCommand`, gerçek sır çözücü, bağlantıyı sına, Gateway X-19. DL-6…DL-11 ve her türlü MOD-0178 / MOD-0183 / MOD-0142 seam'i.
Auth rol şablonu (Q-13, Ali), servis kimliği (Q-12).

## 2. Kararlar ve dilim kapıları

**Kapalı (uygulanır):** T-1…T-6, M-2 (eşik ayar sayfası), M-4, M-6, M-7 (7 dil), M-15, M-16 (kanal kapatma gerekçeli) — kararlar 01 tablo; Q-1
(`warehouse.third-party-logistics.*`), Q-2…Q-9 (Q-6 eşik varsayılanları 120 dk / 240 dk / 26 sa / %1, Q-9 tek stoklanabilir lokasyon) — kararlar 01
"Paket r1 soruları"; F-5, F-8, F-10 (sır referansı ara çözümü — kanal yalnız referans tutar), F-11 (v1.0 yalnız API) — kararlar 01 "r1 incelemesi";
Q-10 (iş ortağı MOD-0049-FU01, MOD-0179 yalnız okur); T-5 MDM izni (yalnız FU01 paketi için; DL-1 MDM'ye dokunmaz); `.antigravity` kapısı
(`08a3a6ac9`); dilim sırası 0180 → 0182 → 0179 (`mvp5-wms-ct-owner-decisions-2026-11-13.md`). Kaynak: `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md` (tamamı).

**Dilim kapıları — DL-1'i etkiliyor mu (paket §24, §18, §19.4):**

| Kapı | Bağlı dilim | DL-1'e etkisi |
|---|---|---|
| MOD-0049-FU01 okuma API'si `main`'de (onay `approvals.tsv:96`, FU01 DL-1 sürüyor) | **DL-4** | **Yok** — DL-1…DL-3 sahte `IBusinessPartnerDirectory` ile (§4.10, AC-2). DL-1 FU01 HTTP istemcisi yazmaz |
| Q-12 servis kimliği (Ali; MOD-0033-FU02) | DL-6, DL-7 | **Yok** — DL-1 servisler arası çağrı yapmaz |
| Q-18 MOD-0183 sahibi (dispatch yolu) | DL-6 | **Yok** |
| Q-19 procurement (GRN yolu, miktar farkı) | DL-7 | **Yok** |
| Q-13 rol şablonu (Ali) | DL-3 kabul kapısı | **Yok** — DL-1 izin anahtarlarını tanımlar, rol şablonu yazmaz (X-16 ayrı WP) |
| Q-14 mesaj komutlarının denetim yolu (CT) | DL-2 | **Yok** (DL-1 komutları yol b, §21) |
| Q-15 Blueprint sapmaları / `BRIDGE-BASE` (CT) | — | Bekletmez; 3PL-BUNDLE taslağına "Q-15 açık" notu (AC-1) |
| Q-16 kiracı sır kasası (MOD-0012-FU01, onay `approvals.tsv:95`) | DL-5 sonrası FU (X-14) | **Yok** — v1.0 kapısı değil (F-10); DL-1'de sır değeri ve referans yazımı yok |
| Q-17 Golden sapmaları G-0179-01 / 02 (CT) | DL-3 | G-0179-01 (silme yok) DL-1 arka ucunda zaten paket kuralı; kabul bekletmez |
| 3PL-BUNDLE dondurma | DL-5'ten önce | DL-1 yalnız taslağı yazar |
| **XMC-001 seam onayları (X-4, X-5, X-6, X-8, X-9, X-W)** | **DL-1** | **AÇIK — başlama kapısı** (§0 madde 3) |
| **CT etiketi `ct/line-ff-2026-11-14a`** | **DL-1** | **AÇIK** — etiket henüz yok (§0 madde 1) |

**Açık — CT kararı (geliştirici kendisi seçmez; CT önerisi parantezde; karar gelene dek ilgili vaka "bekliyor" diye raporlanır):**
- **B-1 uç → izin eşlemesi** (paket §14 tablosundan çıkarım; CT önerisi): `GET partners`, `GET partners/{id}` → `partners.read`; `POST partners` →
  `partners.create`; `…/update` → `partners.update`; `…/suspend` → `partners.deactivate` ("Askıya al" etiketi, anahtar `deactivate`); `…/activate` →
  `partners.activate`; `…/thresholds` → `settings.configure` (AC-19); `GET channels…` → `channels.read`; kanal yazmaları (create / update / close / open) →
  `channels.configure`; `GET mappings` → `mappings.read`; eşleme create / update / deactivate → `mappings.update` (§14'te eşleme için `create` fiili yok).
  `settings.read` DL-1'de manifestte ve `Implemented`'da bildirilir, ucu yok (eşikler ortak ayrıntısında `partners.read` ile okunur; ayar sayfası DL-3).
- **B-2 `IBusinessPartnerDirectory` üretim kaydı** (DL-4'e kadar): CT önerisi — üretim DI'sinde "erişilemez" uygulama (her çağrı 503
  `TPL_MDM_UNAVAILABLE`); testler sahte uygulamayı kayıt eder. Sahte uygulama `business-partner-read-v1.0-proposal.md` şeklini izler (bağlayıcı değil).
- **B-3 sır alanı DL-1'de:** kanal DTO'su sır için yalnız `{ name, isSet, refSetAt }` (AC-9). DL-1'de referans yazan komut yoktur (DL-5) — CT önerisi:
  `IThirdPartySecretResolver` portu DL-1'de tanımlanır, üretim uygulaması DL-5'te (AC-39, F-13 yeniden kurma); DL-1 testi `SecretRefs`'i depoya
  doğrudan yazar (test kurulumu) ve nöbetçi değer dönen **sahte** sağlayıcı/çözücüyle cevap, log, merkezi kayıt, idempotency deposu ve koleksiyon
  taramasını yapar (S-9a, S-9b). Kanal create / update gövdesinde `secretRefs`, `ref` ya da herhangi bir sır değeri alanı **400** (kapalı gövde;
  §4.2 / §8.5: referansı yalnız sunucu üretir, değer MOD-0179'a hiç gelmez).
- **B-4 "tam bir etkin stoklanabilir alt lokasyon"** (§8.10.1): CT önerisi — LOCATION'da `AncestorIds` depo kimliğini içeren, `IsStockable = true`,
  `Status = Active`, `IsDeleted = false` düğüm sayısı tam 1 (yalnız `Find`; `Location.cs:24`, `:28`). Doğrudan çocuk değil torun da sayılır; farklı
  okuma gerekirse CT'ye yaz.
- **B-5 AC-61'in "bağlamadan sonra ikinci lokasyon → sağlık Hatalı" parçası:** sağlık / dashboard DL-2'dir. CT önerisi — DL-1'de saf kural
  (`ThirdPartyLocationRule` benzeri; 0 / 1 / 2 lokasyon → geçerli / bozuk) birim testiyle ve ortak ayrıntı DTO'sunda kural durumunu gösteren alanla
  ölçülür; "Hatalı" sağlık rozeti DL-2 kabulünde aynı fikstürle tamamlanır. S-61 (sayımı "en az bir" yap) DL-1'de iki lokasyonlu bağlama vakasını kırar.
- **B-6 `responsibleUserId` "kiracı kullanıcısı"** (§12 "Auth okuması"): pakette Auth okuma portu adı yok. CT önerisi — MOD-0180 emsali
  (`CreateWaveValidator.cs:33`: boş olmayan UUID; ad sunucuda JWT / istekten); Auth istemcisi **eklenmez**. Gerekirse CT'ye yaz.
- **B-7 `OutboundBaseUrl` SSRF** (§12): DL-1 doğrulayıcısı `https://` dışını ve çözümsüz biçimde özel / yerel adresi (literal IP: loopback, RFC 1918,
  link-local, `localhost`) 400 ile reddeder; DNS denetimi gönderimde (DL-5, AC-41 S-41b). DL-1'de sabotaj adı yok — vaka testle ölçülür.
- **B-8 eşleme `OurRef` doğrulaması** (§12 "SKU / LOCATION okuması"): `Product` → mevcut `IProductMasterClient` (`INV/Application/Common/Upstream/`)
  okuması, `Warehouse` → LOCATION `Find` (ortağın deposu), `StockStatus` → sabit liste. Erişilemezse 503 (`TPL_INVENTORY_UNAVAILABLE` ya da
  sözleşmede adı geçen kod — taslakta yaz, CT inceler).

## 3. Kurallar

- **Kiracı (Çekirdek 2, `multi-tenancy`):** her okuma / yazma `TenantId` + `LegalEntityId` (sunucuda çözülür); başka kiracı ve aynı kiracıda başka
  şirket **404** (AC-5, S-5a, S-5b); gövde / sorguda `tenantId` / `legalEntityId` **400** (AC-6, S-6). Sabit / varsayılan kiracı yok. **Silme yok** —
  ortak askıya alınır, kanal kapatılır, eşleme pasifleştirilir (§2.3 kural 6; G-0179-01: `Delete` komutu, `delete` izni, `[HttpDelete]` yok).
- **Yetki (Çekirdek 3, `security-jwt`, `permission-key-standard`):** `[Authorize]` + her eylemde **eylem düzeyinde** `[HasPermission(ThirdPartyLogisticsPermissions.X)]`;
  sınıf düzeyi `[HasPermission]` / `[HasAnyPermission]` **yasak** (S-7d); `AllowAnonymous` DL-1'de yok (S-7b); manifest yalnız
  `warehouse.third-party-logistics.*` (PKS-001 `:108`). Yeni fiil yok.
- **Denetim (BL-501, `audit-trail-standard`, §8.12, §21):** her DL-1 yazma komutu `ICentralAuditDeclaration`, `SourceModule = THIRD-PARTY-LOGISTICS`,
  yol b; başarılı ve yazan 1 kayıt, replay 0, başarısız 0 (AC-20, S-20a, S-20b). Olay adları ve alanlar §21 tablosundaki DL-1 satırları (askı / etkinleştirme /
  kapatma / açma **gerekçeyi** taşır). İş yazımı + idempotency kaydı + merkezi kayıt **aynı işlemde** (`WithTransactionAsync` → `CentralAuditTransaction.EnlistAsync`).
- **Sırlar (F-9 (c), F-10, F-13):** 3PL kanal sırrının **değeri** DL-1'de hiçbir yerde yoktur — ne gövdede, ne DTO'da, ne logda / OTel'de, ne merkezi
  kayıtta, ne idempotency deposunda, ne koleksiyonda. Kanal kaydı yalnız **referans** taşır ve referansı **istemci vermez** (sunucu
  `ThirdPartyLogistics:{TenantId:D}:{ChannelId:D}:{SecretName}` olarak DL-5'te üretir). DL-1 kanal gövdesinde sır / referans alanı 400 (B-3).
  `InboundKeyId` sır değildir ama sunucu üretir, istemci veremez.
- **Dil (Çekirdek 4, `localization-standard`):** kiracı modülü → **7 dil** (en, tr, fr, es, zh, ar, ru). DL-1 hata cevapları sözleşme kodlu
  (`TPL_*`, §8.15). Kullanıcıya görünen bir `message` metni eklenirse `Resources/ThirdPartyText.{7}.resx` parite ve `Accept-Language` testiyle — bir dil
  eksikse iş bitmemiştir. X-W'nin `Nav.Module.THIRDPARTYLOGISTICS` anahtarı 7 dilde (Almanca yok).
- **Korunan yollar (Çekirdek 6):** `.antigravity/**` yazılmaz (kapı kapandı); `gateway/Diten.ApiGateway/**/ocelot.json` değişmez — yalnız
  integration-agent, bu dilim istemez; X-8 sayı satırını **integration-agent alt ajanı** yazar (geliştirici önce sayıyı ve 17 uç listesini verir, kendisi
  dosyayı düzenlemez); `TenantResolutionMiddleware.cs` DL-1'de dokunulmaz (X-19 DL-5); `services/Diten.Building.Blocks/**` değişmez;
  `_Layout.cshtml` ve Archive donuk; `services/Diten.MdmService/**`, `services/Diten.Platform/**`, `services/Diten.AuthService/**`,
  `services/Diten.ProcurementService/**`, `services/Diten.SupplyChainService/**` dokunulmaz; donmuş sözleşmeler (`wms-bundle`, `location`,
  `inventory-bundle`, `grn-event`) değişmez.
- **XMC-001 (Çekirdek 9):** §1.3 tablosu dışında başka modülün dosyasına dokunma; gerekirse **dur**, CT'ye sor (önce sor, sonra dokun). Onay commit'i
  gelmemiş satıra dokunma (§0 madde 3).
- **Git (Çekirdek 8, GIT-002):** `main`'e commit / push yok. **Push ve PR yok** (modül sonunda ayrı kullanıcı onayıyla; öncesinde `origin/main` merge
  commit ile alınır). Stage yalnız **açık dosya yoluyla** (`git add <yol> …`; `git add -A` / `git add .` yok; izlenmeyen `__pycache__/` ve
  `docs/records/reviews/MOD-0178/*-raw-*` stage'e girmez). **Hook atlanmaz** (`--no-verify` yok), imza atlanmaz; yıkıcı git komutu (`reset --hard`,
  `checkout --`, `push --force`, `clean -fd`) onaysız yok; `git stash` yalnız etiketli ve SHA ile (paylaşılan yığın). Yerel commit serbest; iletiler
  `feat(sce): MOD-0179 DL-1 — …`, sonda oturumun `Co-Authored-By` satırı.
- **Commit sırası (öneri):** (1) 3PL-BUNDLE taslağı → (2) Domain (saf kurallar + birim testleri) → (3) Persistence + index'ler + `ThirdPartyTransactions`
  + X-5 + X-6 (b) (**aynı commit**) → (4) komutlar / sorgular + X-6 (a) (**aynı commit**) → (5) izinler + manifest + X-4 + X-9 + X-W (**aynı commit**) →
  (6) controller uçları + X-8 (**tek commit**, integration-agent adımı) → (7) sözleşme testi, mimari bekçi, kalan vakalar. Her commit derlenir; sayaç
  commit'lerinde (3)…(6) InventoryService filtresiz koşusu (kırmızı pencere yok kanıtı; makine yükü kuralına uyarak).

## 4. Test ve kanıt

- **AC (paket §24 DL-1 satırı):** AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-11, AC-19, AC-20, AC-22, AC-23, AC-25, AC-61 (B-5 ayrımıyla).
  Her vaka paketteki metinle ölçülür (§16 — özet yetmez, aç).
- **Sabotajlar (§24 DL-1, tam — 27):** S-1, S-2a, S-2b, S-3a, S-3b, S-4a, S-4b, S-5a, S-5b, S-6, S-7a, S-7b, S-7c, S-7d, S-8a, S-8b, S-9a, S-9b, S-11,
  S-19, S-20a, S-20b, S-22a, S-22b, S-23, S-25, S-61. Her biri **üretim koduna** uygulanır (kendi kopyasıyla ölçen test guard değildir — Çekirdek 10):
  değişiklik → kırmızı test adı + çıktı özeti → **bayt-eşit geri al** (dosya sha256 önce = sonra) → yeşil; `git status` temiz; hiçbiri commit'e girmez.
  Pakette adı geçen kırmızılar: S-7a → `AuthorizationGuardTests`; S-20a / S-20b → `CentralAuditDeclarationTests`; S-22b → `SingleSystemOfRecordArchitectureTests`;
  S-23 → `LedgerImmutabilityArchitectureTests`; S-25 → `Mod0178WarehouseRouteTests.Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route`
  (sayıyı eski değere döndür). S-3b "görevli kanca" ile eşzamanlı iki bağlama. Diğerlerinin test adı pakette yok — `INVT/ThirdPartyLogistics/` altında
  adlandır, raporda her `S-n` ile eşle. Listede olmayan her kırmızı adıyla raporlanır.
- **InventoryService takımı filtresiz** (servis dokunuldu; `~filter` yok): `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests`.
  **`INVENTORY_TEST_MONGO` boş** (ayarlama — takım kendi replika setini açar), **27017 kullanılmaz**. Dilim başı ve sonu sayıları (toplam / geçti / kırmızı /
  atlandı); taban kırmızıları adıyla ve aynı gün `main`'de ölçülerek ayrılır. Mongo zaman aşımıyla düşen koşu sayılmaz (§0 madde 6).
- **Gateway:** `dotnet test gateway/Diten.ApiGateway.Tests` — önce `<dilim-başı>` sayısıyla, sonra +17 ile yeşil (AC-25); `git diff <başı>..<sonu> --
  gateway/Diten.ApiGateway.Tests/Mod0178WarehouseRouteTests.cs` yalnız sayı satırı + yorum.
- **Web (X-W nedeniyle gerekli):** `dotnet test frontend/Diten.Web.Tests` filtresiz — `NavManifestL10nGuardTests` yeşil; taban kırmızıları (CT ölçümü
  `b19765ef2`: 3 kırmızı, `main`'de de kırmızı) adıyla ayrılır. Vitest `npx vitest run tests/role-assignments-label-ledger.test.js` (`frontend/Diten.Web`)
  — yeni fiil olmadığı için yeşil beklenir; yeni varlık anahtarı yüzünden kırmızıysa **dur**, CT'ye yaz (X-15 DL-3'tür, Web köprüsüne bu dilimde yazılmaz).
- **Platform** `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~RecordCodeTypeCatalogTests` —
  `08a3a6ac9`'daki hâliyle değişmeden yeşil (Platform dokunulmadığı için filtreli koşu yeterli).
- **Mimari:** `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` (AGENTS.md §5).
- **Değişmeden yeşil:** `AuthorizationGuardTests`, `LedgerImmutabilityArchitectureTests`, `ManifestNamespaceOwnershipTests`, `WarehouseRegistrationTests`;
  X satırlarıyla yeşil: `SingleSystemOfRecordArchitectureTests`, `CentralAuditDeclarationTests`, `InventoryLedgerManifestCompletenessTests`.
  Kanıt: `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Warehouse
  services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CycleCount services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Barcode
  services/Diten.InventoryService/tests/Diten.InventoryService.Tests/WavePlanning` **boş**.
- **Build:** `Diten.InventoryService` PASS; `Diten.Web` PASS (X-W resx).
- **`Diten.AuthService.AccountKindAcceptanceHost.Tests` koşulmaz.** Portlar yalnız AGENTS.md §3 tablosundan (Inventory 5067, Gateway 5000).

## 5. Kabul kapıları

| # | Kapı | Ölçüm |
|---|---|---|
| K-0 | **Başlama:** CT etiketi `ct/line-ff-2026-11-14a` var ve dalda; XMC-001 onay commit'leri (X-4, X-5, X-6, X-8, X-9, X-W) var; paket sha `1ac6e1a7…` | §0 madde 1–3 |
| K-1 | 3PL-BUNDLE taslağı yazıldı (42 uç, DL-1'in 17'si bağlayıcı); sözleşme testi kapalı şemayla yeşil, S-1 kırmızı | AC-1 |
| K-2 | AC tablosu: DL-1 AC'lerinin hepsi test adı + sonuçla; B-5 parçası "DL-2'de tamamlanır" diye ayrı satırda | §4 |
| K-3 | 27 sabotajın hepsi kırmızı → geri al (sha256 eşit) → yeşil | §4 |
| K-4 | Sayaçlar dilim başı + 0179 artışı: uç +17, komut +12, `seen` +1 (ya da ölçülen gerçek), modül listesi +1; `TypePrefixes` değişmedi (7) | §0 madde 5 |
| K-5 | InventoryService filtresiz takımda yeni kırmızı yok (tabandan farkı adıyla); Gateway, Web.Tests, vitest, Platform katalog, TenantArchitecture yeşil ya da yalnız taban kırmızıları | §4 |
| K-6 | Değişmez dizinlerin diff'i boş; `ocelot.json`, `.antigravity/**`, Building.Blocks, Platform, MDM, Auth diff'i boş | `git diff --stat` |
| K-7 | Sır taraması (AC-9) yeşil; DL-1 gövdesinde sır / referans alanı 400 | AC-9, B-3 |
| K-8 | Stage açık yollarla, hook atlanmadı, push / PR yok, `main`'e commit yok | `git log`, rapor |

**DL-1 VERIFIED** CT kabul kaydıyla verilir (CT kendi ayrık worktree'sinde filtresiz koşuları ve seçili sabotajları tekrar eder; bağımsız güvenlik incelemesi —
kiracı 404, izin, sır sızıntısı). DL-2 başlama kapısı: DL-1 kabulü + Q-14 (CT).

## 6. Rapor

1. Paket sha256 ölçümü; dal adımı (ata / ff / no-ff / abort) ve `merge-base` doğrulamaları; `<dilim-başı>` commit'i.
2. XMC-001 onay commit'leri (X-4, X-5, X-6, X-8, X-9, X-W) — ya da "onay yok — dokunulmadı".
3. Commit listesi (`git log --oneline <dilim-başı>..HEAD`, SHA + ileti) ve `git diff --stat`; X-8'in uçlarla, X-6 (a) / (b)'nin komut / çağrı yeriyle aynı
   commit'te olduğu açıkça; sayaç commit'leri başına filtresiz takım sayısı.
4. Sayaçlar önce → sonra (satırın kendisiyle atıf, ölçüldüğü commit ile): `Assert.Equal(<n>, endpoints.Count)`, `WriteCommands.Length`, `seen`, modül
   listesi, `TypePrefixes` (değişmedi), dışlama satırı sayısı, `PagelessUntil`.
5. Takım sayıları önce / sonra: InventoryService (toplam / geçti / kırmızı / atlandı, kırmızılar adıyla; geçersiz koşular ayrı), Gateway, Web.Tests, vitest,
   Platform katalog, TenantArchitecture.
6. AC tablosu (AC-1…AC-9, AC-11, AC-19, AC-20, AC-22, AC-23, AC-25, AC-61): test adı + sonuç; B-5 parçası ayrı satırda.
7. Sabotaj tablosu (27): üretim dosyası:satır, değişiklik, kırmızı test adı + çıktı özeti, geri alma sha256, geri yeşil.
8. 3PL-BUNDLE taslağı: yol, uç sayısı (42 / bağlayıcı 17), sözleşme testi adı; taslakta CT'ye düzeltme önerileri.
9. §2 B-1…B-8 için ne uygulandı (CT kararı geldiyse commit'iyle) ve kalan belirsizlikler.
10. Değişmez dizin / korunan yol diff kanıtları; MOD-0180 DL-2 ile çakışma olduysa nasıl çözüldüğü; açık noktalar ve DL-2'ye devredilenler.

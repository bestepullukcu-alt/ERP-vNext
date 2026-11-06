# MDM-FU06-DL1 — DL-1 öncesi kod incelemesi ve keşif kararları (salt okunur)

- Kuyruk: `mdm` (`execution/domains/master-data-management/work-packs/CT-QUEUE.tsv` satır 2) — MDM-FU06-DL1, durum DISCOVERED.
- Paket: `execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md`
  sha256 `6caba52d51c32a1c2188131cd568a2b5bf23c9a857cea43c29b3cb186a5fec97` (ölçüldü). Onay: `execution/approvals/approvals.tsv:59`
  (`whole_pack=MDM-FU06-DL1`, HIGH, dal onayı). Son bakış: `docs/records/reviews/MOD-0290-FU06/2026-11-06-6caba52d-son-bakis.md`
  (KOŞULLU, açık engel yok, düşük F-7…F-15).
- Ölçüm tabanı: `chore/ct/mdm-ct-02` @ `13ca54a0165b54ad2acab653909c23cf7c9787e7` (Expected HEAD). `origin/main` yerel ref'i
  `1fc896e33`; `git merge-base origin/main HEAD` = `1fc896e33` (fetch yapılmadı).
- Yöntem: CT hazırlık alt ajanı (Opus 5.5) dosyaları satır numarasıyla okudu, `git grep` / `git log --all -S` ile aradı.
  Derleme, test, servis, Mongo yok: aşağıdaki "tutuyor" satırları **kod okumasıdır**, çalışma kanıtı değildir.

## 1. Paket iddiaları ↔ kod (@13ca54a01)

| # | Paket iddiası (§) | Ölçüm (yol:satır) | Sonuç |
|---|---|---|---|
| 1 | Anahtar zorlanıyor, beyan yok (§1, §3) | `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductMasterController.cs:21-25` sınıf düzeyi `[Authorize]` + `[HasPermission(ProductMasterContract.ReadPermission)]`; dosyada başka `HasPermission` yok. Sağlayıcı `.../ModuleRegistration/ProductItemSkuMasterManifestProvider.cs:73-264` anahtarı taşımıyor | tutuyor |
| 2 | Sabit `public const`, Api'den erişilir (§10) | `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductMaster/ProductMasterContract.cs:16,19`; kontrolör aynı ad alanını kullanıyor (`ProductMasterController.cs:4`) | tutuyor |
| 3 | Sürüm 1.3.0, 8 sayfa, `ApiPermissions` verilmiyor (§3, AC-6) | sağlayıcı `:82` `ModuleVersion: "1.3.0"`; sayfalar `:87, :117, :138, :159, :180, :202, :224, :247`; belge kurucusu `:263-264`'te biter, `ApiPermissions` argümanı yok | tutuyor |
| 4 | `GLOBAL_PRODUCTS`'ta arayüzsüz eylemler var; sıra 60 boş (§10, K-2) | `:106` `RETIRE` `"System"`, `:107-115` `RECOVER_ORPHANED_LIFECYCLE_OPERATION` `"System"` 50; sıralar 10…50, 60 boş | tutuyor |
| 5 | `"Operator"` enum'da yok, `Toolbar`'a düşer (§10, F-3) | sağlayıcı `:239-240` `"Operator"`, `:260` `"Action"`; Platform `RegisterModuleManifestCommandHandler.cs:709` `ParseEnum(…, ModulePageActionType.Toolbar)`, `:792-793` `Enum.TryParse` | tutuyor |
| 6 | Eylem kaydı `PermissionOwnerModuleCode` taşır, boş = modül sahibi (§12) | `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.ModuleRegistration.Abstractions/ModuleManifestDocument.cs:51-62`; `ApiPermissions` `:35` | tutuyor |
| 7 | `ApiPermissions` döngüsü katalog eşitlemesine gider, sayfa/eylem kaydı yok (§0.3/1) | `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs:235-259` | tutuyor |
| 8 | Plan okuması yalnız sayfa ∪ eylem anahtarı (§0.3/2) | `.../Entitlements/Handlers/QueryHandlers/GetTenantEntitledModulePermissionsQueryHandler.cs:92-117`; eylemler `GetByPageAsync(page.Id)` (`:106`) | tutuyor |
| 9 | Özel profilde eşitlik denetimi fırlar (§0.3/4) | `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs:233-256`, metin `:255` | tutuyor |
| 10 | Sahip büyük harf; kapsam rotadan (§14, T3) | `ModuleCatalogCodeNormalizer.cs:14` `ToUpperInvariant`; `ModulePageDescriptorNormalizer.cs:37-43` `/Platform/` → `PlatformAdmin`, aksi `Tenant`; eylem eşitlemesi sayfa rotasıyla `RegisterModuleManifestCommandHandler.cs:215-229` | tutuyor |
| 11 | Korumalı modül, `TrustedProducerOwnerCode = DITENMDMSERVICE` (T3) | `RegisterModuleManifestCommandHandler.cs:33-37` (`PRODUCT-ITEM-SKU-MASTER`, sahip `DITENMDMSERVICE`), `:81-88` | tutuyor |
| 12 | İşleyicinin 9 bağımlılığı; alan deposu kurulmazsa NRE (§18 dublör tablosu) | kurucu `:49-58`; `ResolveOrRegisterDomainCodeAsync` `:618-619` `QueryAsync` sonucu `all.FirstOrDefault` (Moq varsayılanı `(null, 0)` → NRE) | tutuyor |
| 13 | Plan işleyicisinin 4 bağımlılığı; `TenantScope.BeginPlatform` (§18) | `GetTenantEntitledModulePermissionsQueryHandler.cs:26-30, :92` | tutuyor |
| 14 | F-14: sayfa kimliği nereden gelir | `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/BaseEntity.cs:5` `Id { get; init; } = Guid.NewGuid()`; işleyici kimlik atamaz (`RegisterModuleManifestCommandHandler.cs:669-684`); eylem `PageDescriptorId = page.Id` (`:714-717`) | ölçüldü → talimatta dublör kuralı |
| 15 | `EventTransportMessage` iki projede aynı tam ad (§18 tip çakışması) | `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Eventing/EventTransportMessage.cs:6` ad alanı = `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Eventing/EventTransportMessage.cs:1` | tutuyor |
| 16 | Takma ad emsali (§18) | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Diten.AuthService.Application.Tests.csproj:47` `Aliases="PlatformProducer"`; `.../S2S/EntitlementAttestationConsumerTests.cs:1` `extern alias PlatformProducer;` | tutuyor |
| 17 | MDM test projesinde Platform başvurusu ve Moq yok; Moq sürümü 4.20.72 (§18) | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj:9-25`; Platform test projesi `:21` `Moq 4.20.72` | tutuyor |
| 18 | T2 bugün yalnız metot özniteliği okur, 9 kontrolör, 52 anahtar (§18) | `ProductItemSkuMasterManifestProviderTests.cs:75-83` (`GetMethods`, 9 tip), `:85-87` (`ApiPermissions` sayılmaz), `:150` `52` | tutuyor |
| 19 | Sınıf okuması diğer 9 kontrolörü etkilemez (§18) | 9 dosyada ilk `HasPermission` sınıf satırından sonra: `GlobalProductsController.cs:21/28`, `FinishedGoodsController.cs:18/25`, `GskusController.cs:20/27`, `LskusController.cs:20/28`, `ProductAbbreviationsController.cs:15/22`, `ProductLegalEntityScopesController.cs:15/30`, `ItemsController.cs:16/33`, `StockBehaviourController.cs:20/27`, `CompetitorProductsController.cs:19/28`; öznitelik `AttributeTargets.Method \| Class` (`HasPermissionAttribute.cs:5`) | tutuyor |
| 20 | Değişecek iki test (§18) | `Declares_exact_identity_route_and_non_baseline_flags` `:15` (`:25` 8 sayfa, `:38` 1.3.0); `Global_product_declares_exact_lifecycle_actions_without_approval_or_rejection_actions` `:195-234` | tutuyor |
| 21 | Anahtar eşitliği: dört üretim satırı (§18) | `git grep -nF '"mdm.product-master.read"' -- 'services/*/src/*'` → `ProductIdentityLifecycleEntitlementGrantProfile.cs:58`, `DefaultRolePermissionTemplate.cs:167`, `ProductMasterContract.cs:19`, `TrustedLegalEntityScopeCredentialAuthenticator.cs:55` — 4 | tutuyor |
| 22 | T4-a destekleyici altı test (§18) | `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs:1968, 2005, 2023, 2042, 2102, 2121`; yardımcı `ProductIdentityDeclaredKeys` `:1280` | tutuyor |
| 23 | Destekleyici tekrar testi (§13) | `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleRegistration/RegisterModuleManifestCommandHandlerTests.cs:41` | tutuyor |
| 24 | Bileşim bekçisi süreçteki `Diten.*` derlemelerini tarar (F-8) | `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/CompositionRootGuardTests.cs:97-98` `AppDomain.CurrentDomain.GetAssemblies()` → Platform derlemesinin yüklü olup olmadığı test sırasına bağlı | tutuyor → talimatta `ModuleInitializer` |
| 25 | `MdmTestMongo` / bekçi ortam değişkenleri (E2) | `MdmTestMongo.cs:37` `DITEN_TEST_MONGOD`; `CompositionRootGuardTests.cs:38` `MDM_COMPOSITION_GUARD_MONGO_URI` | tutuyor |
| 26 | `run_record.py` temiz ağaç, yeni `--out`, yalnız `docs/records/audits/` (§18) | `scripts/run_record.py:9-17, 70-76, 118-120` | tutuyor |
| 27 | `dev-up.sh` `start_one` biçimi (E3) | `scripts/dev-up.sh:48-54` (servis/port/sağlık), `:97-98` (`cd <proje> && exec env ASPNETCORE_ENVIRONMENT=Development <ikili> --urls`) | tutuyor; MDM sağlık yolu `/health` (`Diten.MdmService.Api/Program.cs:636`), betik MDM'nin hazır oluşunu günlük satırıyla ölçer |
| 28 | Ayar adları (E3) | `appsettings*.json` yalnız anahtar adı okundu (değer okunmadı): Auth `MongoDbSettings:*`, `Eventing:VirtualHost`, `PlatformService:InternalApiKey`, `InternalEventAuth:ApiKey`; Platform `MongoDbSettings:*`, `BackgroundJobs:StorageDatabaseName`, `AuthService:InternalApiKey`, `ModuleRegistrationCredentials:Mdm:{Identifier,ActiveSecret}`, `Eventing:WorkerEnabled`; MDM `Mongo:*`, `PlatformRegistration:{InternalApiKey,ModuleRegistrationCredentialIdentifier,ModuleRegistrationCredentialSecret}`; dört serviste `JwtSettings:Secret` | tutuyor (paket §18 kaynak ZIP dışı ölçümüyle aynı) |
| 29 | İç uç ve başlık (Y3b) | `InternalTenantEntitlementsController.cs:21, 24, 103, 141` (beklenen anahtar Platform'un `AuthService:InternalApiKey`'i) | tutuyor |
| 30 | Hak satırı uçları (Y2a, Y4a) | `TenantModuleEntitlementsController.cs:13, 62-65, 89-92` (`PlatformAdminOnly`); istek kayıtları `TenantModuleEntitlementContracts.cs:59-76`; `EntitlementSource.Addon = 1` (`EntitlementSource.cs:7`), Platform.API'de `JsonStringEnumConverter` yok → sayı gönderilir | tutuyor; sayı biçimi kod okuması |
| 31 | Günlük satırları (Y1, Y2a) | MDM `ModuleRegistrationHostedService.cs:116-119`; Auth `EntitlementSyncConsumer.cs:204` | tutuyor |
| 32 | Katalog / Auth koleksiyonları (Y1, Y3) | `PlatformCollections.cs:158, 160`; `ModuleCatalogItem.cs:8, 17` (`ModuleVersion`); Auth `PermissionRepository.cs:10` (`permissions`), `Permission.cs:12-16, 67-76` (`Scope` Tenant = 0) | tutuyor |
| 33 | Platform yöneticisi aracı (§18 "ölçüldü: araç var; girdileri okunmadı") | `services/Diten.AuthService/tools/Diten.AuthService.FirstPlatformAdministrator/FirstPlatformAdministratorCommand.cs:18-24, 39-45, 53-54`: girdiler `--email --frontend-base-url --link-file`, ortam `DITEN_AUTH_MONGO_*`, `DITEN_PLATFORM_MONGO_*`, `JwtSettings__Secret`; **parola üretmez, tek kullanımlık bağlantı yazar** | girdiler ölçüldü; bağlantıdan jetona yol **ölçülmedi** |
| 34 | Taban commit koşulu (§18) | `git show 1fc896e33:<sağlayıcı>`: `:82` 1.3.0, `READ_PRODUCT_MASTER` 0 | tutuyor; paketteki örnek `b0ca699d7`, bugün ölçülen `1fc896e33` (paket ölç-yaz diyor; çelişki değil) |
| 35 | Önceki uygulama yok | `git log --all -S'READ_PRODUCT_MASTER' -- services` 0; `git log --all -S'mdm.product-master.read' -- …/ModuleRegistration` 0 | tutuyor |
| 36 | F-12: CI yol süzgeci | `.github/workflows/service-tests.yml:14-18` `pull_request` + `push: main`, `paths:` yok; `mdm-app` satırı `:65-66`; `phase1-gates.yml:4-7` `paths:` yok | MDM testleri her PR'da koşar; Platform değişikliği MDM satırını da tetikler |
| 37 | F-10: canlı sıra numaraları | paket §23: 1 Platform kodu + 38 çift, 2 Auth, 3 MDM, 4 pilot · `docs/guides/operations/post-deploy-steps.md:303` §7: 1 Platform kodu, 2 Platform yapılandırması, 3 Auth, 4 MDM manifesti (pilot yok) | eşleme: paket 1 = §7 1+2 · paket 2 = §7 3 · paket 3 = §7 4 · paket 4 = §7'de yok; §21 maddeleri §7 numarasını kullanır. Düzeltme kule işi (korunan yol) |

Paketle kod arasında yeni çelişki bulunmadı. Ölçülemeyenler (paket §18 ile aynı): kiracı oluşturma, kullanıcı ve rol verme
(Admin, Viewer, adanmış üç rol), `modules.max` kotası; ek olarak platform yöneticisi aracının bağlantısından jetona yol.

## 2. Keşif raporu

`docs/records/audits/2026-11/mdm-ct-02-dl1-inspection-01/DISCOVERY-mod0290-fu06.txt` @ `13ca54a01` — 931 satır, tamamı okundu
(1–56 başlık ve bulgular, 57–928 ref listesi, 929–931 sonuç). `rapor-sha256: efa37c2dd08cb6475a31b4354e4a6e767a678217e62fd202c80fbde3de5c7469`
ilk 930 satırın SHA-256'sıyla aynı (yeniden hesaplandı); dosyanın kendi SHA-256'sı `a0b5e192…`. Sorgu "Product Master Read
Permission Declaration", araç `verify_module_id.py` sha256 `72d27f77…`, depo HEAD `1fc896e33`, sonuç "inceleme gerekli", çıkış 3.
Raporun kendi EKSİK satırları (53–56): başka makine, commit edilmemiş iş, kayıtsız görev kaynakları ve paket iş kapsamı okunmadı.

Karar sözlüğü: **parent** = bu işin üst kaydı, kendi kaydı ya da aynı modülün yüzeyi · **FU** = aynı ebeveynin başka takip
paketi (kardeş), ayrı iş · **child** = aynı paketin başka dilimi (FU06'da yok) · **yeni** = başka modül; yalnız ad ya da
mekanizma benzerliği, bu iş ona dokunmaz.

| # | BULGU (rapor satırı) | Karar | Gerekçe (ölçüm) |
|---|---|---|---|
| 1 | blueprint MOD-0290 (33) | parent | FU06'nın ebeveyni (künye `parent: MOD-0290`); yeni modül açılmaz |
| 2 | sor MOD-0290 ürün ana kayıtları (34) | parent | veri sahibi MOD-0290 kalır; FU06 veri yazmaz, yalnız manifest beyanı |
| 3 | registry MOD-0018-FU16 (35) | yeni | Auth (`service: Diten.AuthService`) global ürün anahtarları; pakette `product-master` 0 geçiş; FU06 Auth'a dokunmaz. Registry "draft", paket künyesi "review" — FU06'yı etkilemez |
| 4 | registry MOD-0018-FU20 (36) | yeni | Auth, ürün kısaltma anahtarları; `product-master` 0; FU06 dokunmaz |
| 5 | registry MOD-0018-FU22 (37) | yeni | Auth, ürün tüzel kişi kapsamı anahtarları; `product-master` 0; FU06 dokunmaz |
| 6 | registry MOD-0290 (38) | parent | aynı ebeveyn, kanonik satır; kimlik değişmez |
| 7 | registry MOD-0290-FU06 (39) | parent | bu işin kendi kimliği (K-7 ile kule dalında eklendi) |
| 8 | paket MOD-0290-FU01-brand-product-master-boundary (40) | FU | MOD-0290'ın kardeş takip paketi (marka sınırı, taslak); BRANDS sayfası ayrı modülde (`ProductItemSkuMasterManifestProvider.cs:222-223`); FU06 marka anahtarına dokunmaz |
| 9 | paket MOD-0290-FU06 (41) | parent | bu işin yetki paketi (sha256 `6caba52d…`, `approvals.tsv:59`) |
| 10 | paket MOD-0290-product-item-sku-master (42) | parent | ebeveyn paket; FU06 onun manifest sağlayıcısına tek eylem ekler |
| 11 | paket MOD-0018-FU16 (43) | yeni | #3 ile aynı paket; Auth tarafı, FU06 dokunmaz |
| 12 | paket MOD-0018-FU20 (44) | yeni | #4 ile aynı paket; Auth tarafı, FU06 dokunmaz |
| 13 | paket MOD-0018-FU22 (45) | yeni | #5 ile aynı paket; Auth tarafı, FU06 dokunmaz |
| 14 | paket MOD-0018-FU23 (46) | yeni | yalnız arşiv ref'lerinde (22); künye Auth, ürün kimliği yaşam döngüsü anahtarları; `product-master` 0 (arşiv ref'inde okundu); FU06 dokunmaz |
| 15 | paket MOD-0018-FU26 (47) | yeni | yalnız arşiv ref'lerinde (6); künye Auth, kurtarma operatörü anahtarı; `product-master` 0; FU06 dokunmaz |
| 16 | gateway 'permissions' → 5056 (48) | yeni | Auth rota ailesi (MOD-0018); FU06 rotaya ve gateway'e dokunmaz (§6) |
| 17 | gateway 'product-master' → 5059 (49) | parent | MOD-0290-FU04 S4 okuma sözleşmesinin rotası, var (§16); FU06 yeni uç açmaz, E3 bu rotayı yalnız çağırır |
| 18 | görev Q353 (50) | yeni | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` Q353: `supplychain.shipments.*` anahtarlarının aynı manifest→katalog→Auth zincirinde keşfi (CT ACCEPTED); başka modül, FU06 ona yazmaz |
| 19 | görev MDM-FU06-DL1 (51) | parent | bu işin kendi kuyruk satırı |

## 3. Son bakış düşük bulguları → talimattaki yeri

| Bulgu | Talimatta |
|---|---|
| F-7 kanıt sırası | NASIL "Kanıt sırası": kod commit → sabotaj → `SABOTAJ.md` commit → E2 (`run_record`) → commit → E3 (`run_record`) → commit |
| F-8 bekçi sıraya bağlı | NASIL: yeni test dosyasında `[ModuleInitializer]` Platform derlemesini yükler; DOĞRULA: bekçi tek başına da koşar |
| F-9 hak yok + adanmış roller | E3 betiği Y4b (adanmış üç rol anahtarsız) ve Y5 (POST'tan önce ikinci kiracıda Admin + Viewer anahtarsız, `mdm.items.read` yok, 403) |
| F-10 sıra numaraları | Rapor: §1 satır 37'deki eşleme tablosu işçi raporunda yeniden ölçülür; düzeltme kule işi |
| F-11 çıkış tuzağı | E3 betiği: `finally` + SIGTERM/SIGINT/SIGHUP; her yolda PID + başlangıç zamanıyla durdurma; DOĞRULA: stdout'ta Y6 satırları |
| F-12 CI süzgeci | DOĞRULA: iki iş akışında `paths:` yokluğu yeniden ölçülür, rapora yazılır |
| F-13 künye | DOĞRULA: paket dosyası değişmedi; künye düzeltmesi (target, owner, branch, §20) kule işi, rapor açık kayıt |
| F-14 dublör kimliği | NASIL: dublör kuralı (kimlik `BaseEntity` başlatıcısından, depo atamaz; süzgeçler) ve okunacak dosyalar |
| F-15 paylaşılan mongod | E3 betiği: yalnız veritabanı adları karşılaştırılır (boyut bilgi); her servis açılınca `lsof` ile 27017–27021 bağlantısı yok |

## 4. Açık kalanlar (kule)

1. **Kanıt betiğinin yeri:** paket §18 "Kanıt dizini: … betik ve `SABOTAJ.md` burada" der; betik kule kararıyla
   `execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_e3.py`'de (emsal `wp0289_dl1_accept.py`). Paket
   metniyle çelişki; paket dondurulmuş, karar kayda yazılmalı.
2. **E3 kurulumu ölçülmedi:** betik Y1'den sonra çıkış 3 ile durur (kiracı, kullanıcı/rol, `modules.max`, platform yöneticisi
   jetonu). Kırmızı taban bu durağı gösterir; AC-9 ve DL-1 kabulü kurulum ölçülüp betik yeni SHA ile verilene kadar bekler.
3. **Expected HEAD:** talimat `13ca54a01` taşır; bu not ve betik o commit'te yok. Kule commit'ten sonra Expected HEAD'i ve
   betiğin SHA-256'sını günceller.
4. F-10 ve F-13 kule işi (post-deploy §7 ve paket künyesi korunan / dondurulmuş).

## Sonuç (CT hazırlık)

Paketin kod iddiaları Expected HEAD'de tutuyor (kod okuması). 19 keşif bulgusunun hiçbiri mükerrer iş değil: 8 parent, 1 FU,
10 yeni. DL-1 talimatı yazılabilir; E3 kabulü kurulum ölçümüne bağlı.

@[.antigravity/agents/backend-architect.md]
WP: MDM-FU06-DL1 · Prompt MDM-FU06-DL1-P v1.0
Repository: /Users/alitufanoglu/ERP-vNext · Branch: feature/mdm/mdm-fu06-dl1-r1 · Expected HEAD: 13ca54a0165b54ad2acab653909c23cf7c9787e7 · Worktree: /Users/alitufanoglu/ERP-vNext/.claude/worktrees/mdm-fu06-dl1-r1
Risk: HIGH · §9.1 sinyalleri: yetki, sözleşme
Görev sınıfı: ürün · Paket: execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md
İzinli yollar: services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs, services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs, services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductMasterReadKeyPlatformPathTests.cs, services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj, docs/records/audits/2026-11/mod0290-fu06-dl1/**
Korunan yollar: .antigravity/**, AGENTS.md, services/Diten.AuthService/**, services/Diten.Platform/**, services/Diten.Platform.Common/**, services/Diten.InventoryService/**, services/Diten.Building.Blocks/**, frontend/**, gateway/**, docs/guides/operations/post-deploy-steps.md, execution/**, .github/**, scripts/**
Kapsanan dilim: DL-1

Önce oku (sırayla):
1. execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md — tamamı; §0.3, §10, §12, §13, §17, §18 öncelikli
2. docs/records/audits/2026-11/mdm-ct-02-dl1-inspection-01/INSPECTION-mdm-fu06-dl1.md
3. docs/records/reviews/MOD-0290-FU06/2026-11-06-6caba52d-son-bakis.md
4. AGENTS.md
5. execution/domains/master-data-management/domain-config.md
6. .antigravity/rules/permission-key-standard.md
7. .antigravity/rules/module-self-registration-standard.md
8. .antigravity/rules/git-safety.md
9. .antigravity/rules/cross-module-change-consent.md
10. services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs
11. services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs
12. services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs
13. services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/QueryHandlers/GetTenantEntitledModulePermissionsQueryHandler.cs
14. services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/GetTenantEntitledModulePermissionsQueryHandlerTests.cs
15. services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/S2S/EntitlementAttestationConsumerTests.cs
16. scripts/run_record.py
17. execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_e3.py

Sözleşme yüzeyleri:
- API: «rotası var. Yeni uç yok.» (execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md:211)
- İzin: «`mdm.product-master.read` (`ProductMasterContract.ReadPermission`)» (execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md:97) · «public const string ReadPermission = "mdm.product-master.read";» (services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductMaster/ProductMasterContract.cs:19)
- Test: «`Product_master_read_is_a_non_ui_view_action_and_not_an_api_permission`» (execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md:233) · «`Every_key_synced_under_the_module_is_in_the_entitled_read_set`» (execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md:237)
- Sabotaj: «Anahtarı eylemden çıkarıp `ApiPermissions`'a koy» (execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md:281)

Keşif: inceleme gerekli · rapor docs/records/audits/2026-11/mdm-ct-02-dl1-inspection-01/DISCOVERY-mod0290-fu06.txt@13ca54a01 · sha256 efa37c2dd08cb6475a31b4354e4a6e767a678217e62fd202c80fbde3de5c7469
Keşif kararı 1: parent — Blueprint satırı MOD-0290 bu işin ebeveynidir (künye parent: MOD-0290); yeni modül açılmaz
Keşif kararı 2: parent — SoR satırı ürün ana kayıtlarının sahibini MOD-0290 gösterir; bu iş veri yazmaz, yalnız manifest beyanı ekler
Keşif kararı 3: yeni — kayıt satırı MOD-0018-FU16 Auth tarafında global ürün anahtarlarıdır; paketinde product-master geçmez, bu iş Auth'a dokunmaz
Keşif kararı 4: yeni — kayıt satırı MOD-0018-FU20 Auth tarafında ürün kısaltma anahtarlarıdır; product-master geçmez, bu iş ona dokunmaz
Keşif kararı 5: yeni — kayıt satırı MOD-0018-FU22 Auth tarafında ürün tüzel kişi kapsamı anahtarlarıdır; product-master geçmez, bu iş ona dokunmaz
Keşif kararı 6: parent — kayıt satırı MOD-0290 aynı ebeveynin kanonik satırıdır; kimlik değişmez
Keşif kararı 7: parent — kayıt satırı MOD-0290-FU06 bu işin kendi kimliğidir
Keşif kararı 8: FU — MOD-0290-FU01 marka sınırı paketi aynı ebeveynin kardeş takip paketidir (taslak); BRANDS ayrı modülde, bu iş marka anahtarına dokunmaz
Keşif kararı 9: parent — MOD-0290-FU06 paketi bu işin yetki paketidir (sha256 6caba52d…, approvals.tsv satır 59)
Keşif kararı 10: parent — MOD-0290-product-item-sku-master ebeveyn pakettir; bu iş onun manifest sağlayıcısına tek eylem ekler
Keşif kararı 11: yeni — MOD-0018-FU16 paketi karar 3 ile aynı Auth paketidir; bu iş Auth koduna ve kaydına dokunmaz
Keşif kararı 12: yeni — MOD-0018-FU20 paketi karar 4 ile aynı Auth paketidir; bu iş Auth koduna ve kaydına dokunmaz
Keşif kararı 13: yeni — MOD-0018-FU22 paketi karar 5 ile aynı Auth paketidir; bu iş Auth koduna ve kaydına dokunmaz
Keşif kararı 14: yeni — MOD-0018-FU23 yalnız arşiv dallarında, Auth ürün kimliği yaşam döngüsü anahtarları; product-master geçmez, bu iş dokunmaz
Keşif kararı 15: yeni — MOD-0018-FU26 yalnız arşiv dallarında, Auth kurtarma operatörü anahtarı; product-master geçmez, bu iş dokunmaz
Keşif kararı 16: yeni — gateway 'permissions' → 5056 Auth rota ailesidir; bu iş gateway'e ve rotaya dokunmaz
Keşif kararı 17: parent — gateway 'product-master' → 5059 MOD-0290-FU04 S4 okuma sözleşmesinin var olan rotasıdır; yeni uç yok, E3 yalnız çağırır
Keşif kararı 18: yeni — Q353 tedarik zinciri sevkiyat anahtarlarının aynı manifest zincirindeki keşfidir; başka modül, bu iş ona yazmaz
Keşif kararı 19: parent — MDM-FU06-DL1 bu işin kendi kuyruk satırıdır

Hedef ajan giriş alanları (§17.4, backend-architect):
- Hedef servis ve proje yolları: Diten.MdmService · yukarıdaki İzinli yollar · test projesi services/Diten.MdmService/tests/Diten.MdmService.Application.Tests
- Aggregate / alanlar: yok — entity, depo, DTO değişmez (paket §3, §4)
- Komut / sorgu listesi: yok — tek değişiklik manifest sağlayıcısında bir eylem satırı ve sürüm
- Doğrulayıcı kuralları: yok (paket §12: anahtar biçimi, eylem kodu, görünen ad, bayraklar testle ölçülür)
- İzin anahtarları: mdm.product-master.read (yalnız beyan; zorlama ProductMasterController sınıf özniteliğinde, değişmez)
- Response<T> ve CustomBaseController: değişmez; ProductMasterController'a dokunulmaz (sabotaj e dışında)

NE: MOD-0290-FU06 DL-1 — (1) ProductItemSkuMasterManifestProvider: `using Diten.MdmService.Application.Features.ProductMaster;`, `private const string ProductMasterRead = ProductMasterContract.ReadPermission;` ve GLOBAL_PRODUCTS eylem listesinin sonuna (RECOVER_ORPHANED_LIFECYCLE_OPERATION'dan sonra) paket §10'daki satır: `new ModuleManifestAction("READ_PRODUCT_MASTER", "Read Product Master", ProductMasterRead, "View", 60, IsDangerous: false, IsToolbarAction: false, IsRowAction: false)`; `ModuleVersion` "1.3.0" → "1.4.0" (yorum satırına "MOD-0290-FU06 (1.4.0): + GLOBAL_PRODUCTS READ_PRODUCT_MASTER"); `ApiPermissions` verilmez. (2) ProductItemSkuMasterManifestProviderTests: yeni T1; T2 (`Declares_exact_unique_canonical_permission_contract_and_matches_runtime_enforcement`) genişler; `Declares_exact_identity_route_and_non_baseline_flags` 1.4.0 (sayfa 8 kalır); `Global_product_declares_exact_lifecycle_actions_without_approval_or_rejection_actions` listelerine READ_PRODUCT_MASTER ve anahtar. (3) Yeni ProductMasterReadKeyPlatformPathTests.cs: T3 `Production_manifest_syncs_the_read_key_with_the_module_owner_and_tenant_scope`, T4-b `Every_key_synced_under_the_module_is_in_the_entitled_read_set`. (4) csproj'a iki satır (paket §18). (5) Kanıt: docs/records/audits/2026-11/mod0290-fu06-dl1/ altında SABOTAJ.md, e2-run, e3-run.
NEDEN: `mdm.product-master.read` ProductMasterController'da sınıf düzeyinde zorlanıyor ama manifestte beyan edilmiyor; hiçbir rol tutamıyor, stok ekibinin okuma sözleşmesi kapalı. Anahtar rollere yalnız manifest → Platform kataloğu → Auth eşitlemesiyle girer (BL-592, paket §0.2). `ApiPermissions` yolu bu özel profilli modülde Auth eşitlik denetimini kırar (§0.3, karar A); bu yüzden arayüzsüz View eylemi. Sahip kararları §0.1 değişmez; stok davranışı anahtarları kapsam dışı (F-1).
NASIL:
- Makine yükü: her dotnet build / test komutundan önce uptime koş; 1 dakikalık yük 12'den büyükse 60 saniye bekle ve yeniden ölç; 12'nin altına inmeden derleme başlatma. Testleri proje proje ve paketteki filtreyle koş; çözüm genelinde ya da tam süit dotnet test yok.
- Başlangıç: worktree HEAD = Expected HEAD, dal feature/mdm/mdm-fu06-dl1-r1, `git status --porcelain` boş. `shasum -a 256 execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_e3.py` = 90d99371a0e8aa924c30eb00d71611fa01bd8eb5a3efe3212cc13159221811a9 (S-83: betiği kule yazdı; değiştirme, tutmazsa DUR).
- Taban çizgisi: DOĞRULA 1'i değişiklik yapmadan koş; geçen / kalan sayıları yaz.
- Mongo: E2 testleri MdmTestMongo ile kendi mongod'unu açar; `DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod` ver. Paylaşılan Mongo (27017–27021) kullanılmaz; MDM_COMPOSITION_GUARD_MONGO_URI verme.
- Sıra (paket §21): önce csproj + T1, sonra T3 / T4-b, en son manifest satırı ve sürüm (kırmızıdan yeşile). Her testin kırmızısını yeşilden önce gör ve yaz.
- csproj (paket §18, birebir): `<ProjectReference Include="..\..\..\Diten.Platform\src\Diten.Platform.Application\Diten.Platform.Application.csproj" Aliases="PlatformProducer" />` ve `<PackageReference Include="Moq" Version="4.20.72" />`. Yeni test dosyası `extern alias PlatformProducer;` ile başlar; Platform.Application tipleri `PlatformProducer::` ile, Platform.Domain / Platform.Common / BuildingBlocks tipleri küresel adla kullanılır (emsal EntitlementAttestationConsumerTests). Derleme kırılırsa DUR (paket §18 "Derleme kırılırsa"); testi Platform'a taşıma, manifesti kopyalama.
- Anahtar testlerde yazıyla geçer (`"mdm.product-master.read"`), sabitten okunmaz; T1 ayrıca `ProductMasterContract.ReadPermission` eşitliğini ölçer.
- T1 (ProductItemSkuMasterManifestProviderTests içinde): gerçek `GetManifest()`; GLOBAL_PRODUCTS'ta tek READ_PRODUCT_MASTER: PermissionKey, ActionType "View", IsDangerous / IsToolbarAction / IsRowAction false, PermissionOwnerModuleCode null, DisplayName "Read Product Master"; `Manifest.ApiPermissions` null ya da boş.
- T2: `enforced` metot özniteliğine ek olarak `controller.GetCustomAttributes<HasPermissionAttribute>()` (sınıf) okur; dizinin sonuna `typeof(ProductMasterController)`; `declared` sayfa ve eylem anahtarlarına `Manifest.ApiPermissions ?? []` anahtarlarını da ekler; beklenen sıralı listeye anahtar; `Assert.Equal(53, …)` ve yorum "MOD-0290-FU06: 52 → 53 (+ mdm.product-master.read)". Diğer dokuz kontrolörün kümesi değişmemeli (sınıf satırından önce HasPermission yok; inceleme notu §1 satır 19).
- T3 / T4-b (yeni dosya): yalnız iki işleyici gerçek koşar — `RegisterModuleManifestCommandHandler` (komut: üretim `new ProductItemSkuMasterManifestProvider().GetManifest()`, `TrustedProducerOwnerCode = "DITENMDMSERVICE"`) ve `GetTenantEntitledModulePermissionsQueryHandler`. Dublörler paket §18 tablosundaki gibi, satır satır; IGlobalApplicabilityTransactionCoordinator için elle yazılmış satır içi koordinatör (Platform testindeki InlineCoordinator `private`, bağlanamaz); ITenantContext gerçek `TenantContext`. T3: `ICatalogPermissionSyncService.SyncPermissionAsync` kayıtlarında `("mdm.product-master.read", "PRODUCT-ITEM-SKU-MASTER", "Tenant")` tam bir kez. T4-b: T3'ün doldurduğu bellek depolarıyla plan işleyicisi; dönen modül listesinde anahtar var VE `PRODUCT-ITEM-SKU-MASTER` sahibiyle eşitlenen anahtar kümesi plan listesine eşit (SetEquals, OrdinalIgnoreCase).
- F-14 (dublör kimliği): önce oku — services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/BaseEntity.cs (`Id = Guid.NewGuid()` başlatıcısı), services/Diten.Platform/src/Diten.Platform.Domain/Entities/ModulePageDescriptor.cs, ModulePageActionDescriptor.cs, Features/Tenants/Commercial/Entitlements/Queries/GetTenantModuleEntitlementsQuery.cs, GetTenantModuleEffectiveAccessQuery.cs ve TenantModuleEntitlementContracts.cs (DTO kurucuları). Bellek depoları kimlik ATAMAZ, gelen nesneyi saklar; `GetByModuleAsync` = ModuleCode (OrdinalIgnoreCase) ve `!IsDeleted`; `GetByPageAsync` = `PageDescriptorId == id` ve `!IsDeleted`; `DeleteAsync` IsDeleted işaretler. T3 ek olarak sayfa kimliklerinin tekil olduğunu ve her eylemin PageDescriptorId'sinin bir sayfaya ait olduğunu doğrular. Tabloda olmayan bir depo üyesi çağrılırsa (Moq null → NRE) DUR ve üyeyi adıyla raporla; arama sorgusu (SearchAsync) kurulmaz.
- F-8 (bekçi sırası): yeni test dosyasında `internal static class` içinde `[System.Runtime.CompilerServices.ModuleInitializer]` bir metot `typeof(PlatformProducer::Diten.Platform.Application.Features.ModuleRegistration.RegisterModuleManifestCommandHandler).Assembly`'yi yükler; böylece CompositionRootGuardTests Platform derlemesini her sırada görür. CA2255 uyarısı derlemeyi kırarsa DUR.
- Anahtar eşitliği: `git grep -nF '"mdm.product-master.read"' -- 'services/*/src/*'` kabul commit'inde tam 4 satır (ProductMasterContract, ProductIdentityLifecycleEntitlementGrantProfile, DefaultRolePermissionTemplate, TrustedLegalEntityScopeCredentialAuthenticator); manifest sabitten okuduğu için sayı değişmez.
- Sabotajlar (paket §18 a…k): kod commit'inden SONRA, üretim kodunda; her biri: çapayı tam bir kez değiştir, ilgili filtreyi koş, adı verilen testlerin kırmızısını gör, `git checkout -- <dosya>` ile geri al, `git status --porcelain` boş, yeşil. b2 (ProductMasterContract.cs) ve e (ProductMasterController.cs) §5 dışı dosyadır: yalnız geçici, aynı adımda geri alınır. Derleme hatası kırmızı sayılmaz. Her sabotaj için SABOTAJ.md'ye bir satır: harf · dosya:satır çapası · beklenen kırmızılar · görülen kırmızı test adları · geri alma sonrası `git status --porcelain` boş.
- Kanıt sırası (F-7; run_record temiz ağaç ister, commit atmaz): (1) kod + testler commit; (2) sabotajlar a…k, SABOTAJ.md yazılır, commit; (3) temiz ağaçta `python3 scripts/run_record.py run --out docs/records/audits/2026-11/mod0290-fu06-dl1/e2-run -- dotnet test services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj --filter "FullyQualifiedName~ModuleRegistration|FullyQualifiedName~CompositionRootGuardTests"` (DITEN_TEST_MONGOD ortamda), commit; (4) E3 (aşağıda), commit. Her commit yalnız açık yollarla stage edilir.
- E3 (AC-9; betik paket §18 Y1–Y6'yı uygular): taban worktree'yi repo DIŞINDA aç: `git worktree add --detach /private/tmp/mdm-fu06-dl1-base "$(git merge-base origin/main HEAD)"` (fetch yok; taban SHA'sını rapora yaz). Sırlar yalnız ortamdan, ekrana yazılmadan, bu koşu için rastgele üretilir (yığın atılabilir, dört servis aynı değeri alır): `export E3_JWT_SECRET="$(python3 -c 'import secrets;print(secrets.token_urlsafe(48))')"`, aynı biçimde E3_INTERNAL_API_KEY, E3_MDM_REGISTRATION_ID, E3_MDM_REGISTRATION_SECRET. Koşmadan önce uptime ≤ 12. Sonra temiz ağaçta tek koşu: `python3 scripts/run_record.py run --out docs/records/audits/2026-11/mod0290-fu06-dl1/e3-run -- python3 execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_e3.py --base-worktree /private/tmp/mdm-fu06-dl1-base`. Betik kendi mongod'unu (`--replSet`, port 37291, yeni dbpath), kendi RabbitMQ vhost'unu açar; Auth, Platform, Gateway ve MDM'yi tek tek kendi ortamlarıyla başlatır (dev-up kullanmaz); çıkış tuzağıyla her yolda yalnız kendi süreçlerini PID + başlangıç zamanıyla durdurur (F-11), vhost'u siler, taban worktree'yi kaldırır. F-9 (Y4b adanmış üç rol, Y5 POST öncesi hak yok) ve F-15 (yalnız veritabanı adları; servislerin 27017–27021'e bağlantısı yok) betikte. Çıkış 0 yeşil, 1 kırmızı, 2 kuleye dur, 3 kule durur sahibe sorar (bugün kurulum ölçülmediği için Y1'den sonra beklenen), 4 yük bekle, 5 kullanım.
- F-10: §23 canlı sıra numaralarıyla docs/guides/operations/post-deploy-steps.md §7 (satır 303) numaralarının eşleme tablosunu yalnız okuyarak rapora yaz (inceleme notu §1 satır 37); dosyaya yazma.
- Commit ve teslim (S-63): kendi dalında yerel commit; push ve PR yok; son commit'ten sonra dur ve CT'ye dal adı, HEAD sha'sı, test sonucu ve E3 çıkışını bildir.
YAPMA:
- Auth, Platform, Platform.Common, Inventory, Building.Blocks, Web, Gateway kodu; ocelot.json; post-deploy-steps.md; paket dosyası; kayıt (execution/registries/**); .github/**; scripts/** (XMC-001, paket §6).
- E3 betiğini değiştirme, kopyalama, yerine başka betik koşma (S-83); dev-up.sh kullanma; paylaşılan mongod'a ya da `/` vhost'una yazma.
- `ApiPermissions` kullanma; `"Operator"` ya da başka ActionType; PermissionOwnerModuleCode verme; yeni sayfa, uç, entity, seed, dil metni.
- Stok davranışı anahtarları (mdm.*.edit-stock-behaviour; §0.1 #2, F-1); FU04'ün geri kalanı.
- Testte manifesti ya da Platform işleyicisini kopyalama; Platform test dublörlerine bağlanmaya çalışma.
- Süreçleri adla durdurma (pkill / killall yok); yalnız kendi başlattığın süreç, PID + başlangıç zamanıyla.
- Push, PR, merge, dal değiştirme, stash, reset, rebase; `git add -A` / `git add .`.
DOĞRULA: (komutlar worktree kökünde; her dotnet komutundan önce uptime)
1. `DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod dotnet test services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj --filter "FullyQualifiedName~ModuleRegistration|FullyQualifiedName~CompositionRootGuardTests" --logger "trx;LogFileName=fu06-dl1.trx"` → değişiklikten önce taban; sonra 0 failed, NotExecuted 0; sayı = taban + T1 + T3 + T4-b; CompositionRootGuardTests'in iki testi aynı süreçte geçer (AC-1, AC-2, AC-3, AC-4, AC-6, AC-8).
2. F-8: aynı komut `--filter "FullyQualifiedName~CompositionRootGuardTests"` ile tek başına → 0 failed (Platform derlemesi ModuleInitializer ile yüklü).
3. `dotnet build services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj -c Debug` → 0 hata.
4. `git grep -nF '"mdm.product-master.read"' -- 'services/*/src/*'` → tam 4 satır.
5. Sabotajlar a, b, b2, c, d, e, f, g, h, i, j, k (paket §18 tablosu; beklenen kırmızılar oradaki gibi, d'de T3 yeşil kalır): her biri SABOTAJ.md satırında (AC-5).
6. `git diff --name-only 13ca54a0165b54ad2acab653909c23cf7c9787e7..HEAD` → yalnız İzinli yollar; `git diff --stat 13ca54a0165b54ad2acab653909c23cf7c9787e7..HEAD -- services/Diten.AuthService services/Diten.Platform services/Diten.InventoryService frontend gateway` boş (AC-7).
7. F-13: `git diff --stat 13ca54a0165b54ad2acab653909c23cf7c9787e7..HEAD -- execution/` boş (paket ve betik değişmedi); paketin künye alanları (target, owner, branch) ve §20 işareti kule işidir, raporda açık kayıt.
8. F-12: `grep -n "paths" .github/workflows/service-tests.yml .github/workflows/phase1-gates.yml` ve iki dosyanın `on:` bloğunu oku; MDM test satırının (mdm-app) her PR'da koşup koşmadığını, Platform değişikliğinin MDM testlerini tetikleyip tetiklemediğini rapora yaz (Expected HEAD'de iki dosyada `paths:` yok).
9. E3: run_record kaydı `docs/records/audits/2026-11/mod0290-fu06-dl1/e3-run/run-record-v1.json` yazıldı; `python3 scripts/run_record.py verify <kayıt>` geçer; stdout.txt'de "ÖZET" ve "SONUÇ" satırları, her yolda "== Y6" bloğu (F-11), "F-15" kontrol satırları, çıkışa göre F-9 satırları; stdout'ta jeton, Authorization ya da sır yok (betik süzer; yine de `grep -c "eyJ" stdout.txt` = 0). AC-9 yalnız çıkış 0 ile.
10. `git diff --check` temiz; son commit'ten sonra `git status --porcelain` boş.
Sabotajlar (her biri geçici; NASIL'daki kurala göre; kırmızı test adlarını raporla; derleme hatası kırmızı sayılmaz):
11. a — READ_PRODUCT_MASTER satırını sil → T1, T2, T3, T4-b, Global_product_… kırmızı.
12. b — sağlayıcıda anahtarı `mdm.product-master.reads` yap (sabit yerine yazıyla) → T1, T2, T3, T4-b, Global_product_… kırmızı.
13. b2 — ProductMasterContract.ReadPermission değerini değiştir (§5 dışı, geri al) → T1, T2, T3, T4-b, Global_product_… kırmızı.
14. c — GLOBAL_PRODUCTS RoutePath'ini `/Platform/MasterDataManagement/GlobalProducts` yap → T3 (PlatformAdmin), Declares_exact_identity_route_and_non_baseline_flags kırmızı.
15. d — anahtarı eylemden çıkarıp `ApiPermissions: [new ModuleManifestPermission("mdm.product-master.read", "Read Product Master")]` ver → T1, T4-b, Global_product_… kırmızı; T3 yeşil kalır.
16. e — ProductMasterController sınıf HasPermission özniteliğini sil (§5 dışı, geri al) → T2 kırmızı.
17. f — ActionType "View" → "Operator" → T1 kırmızı. g — IsRowAction true → T1. h — IsToolbarAction true → T1. j — IsDangerous true → T1. k — görünen adı değiştir → T1.
18. i — eyleme `PermissionOwnerModuleCode: "brand-product-master"` → T1, T3, T4-b kırmızı.
Durma koşulları:
- Worktree HEAD Expected HEAD'den farklı, dal farklı ya da başlangıçta kirli dosya → DUR.
- E3 betiğinin SHA-256'sı NASIL'daki değerden farklı → DUR (betik değiştirilmez).
- Taban çizgisinde DOĞRULA 1 filtresinde kırmızı → değişiklik yapmadan DUR.
- Platform başvurusu ya da takma adla derleme kırılırsa (CS0433 dahil) ya da CA2255 hata olursa → DUR, kuleye bildir (paket §18); alternatif yol denenmez.
- Dublör tablosunda olmayan bir üye gerekirse ya da işleyici NRE verirse → DUR ve üyeyi adıyla raporla.
- CompositionRootGuardTests değişiklikten sonra kırmızı (birlikte ya da tek başına) → DUR ve seçilen tipi adıyla raporla.
- `git grep` sayısı 4 değil → DUR.
- T2'de sınıf okuması diğer dokuz kontrolörden birinin kümesini değiştirirse → DUR.
- Bir sabotaj adı verilen testi kırmızı yapmıyorsa ya da geri alma sonrası yeşile dönmüyorsa → DUR ve raporla.
- İş İzinli yolların dışında değişiklik gerektiriyorsa → DUR ve adıyla raporla (XMC-001).
- E3 çıkış 2 → DUR, stdout'taki "KULE DURUR" satırını raporla; betiği yeniden koşma. Çıkış 3 → DUR: "kule durur, sahibe sorar"; AC-9 ve DL-1 kabulü bekler. Çıkış 4 → koşu sayılmaz; 60 sn arayla yükü ölç, 15 dakikada 12'nin altına inmezse DUR ve uptime çıktılarıyla raporla. Çıkış 1 → düzeltme yapmadan DUR, KALDI satırlarını raporla.
- E3'ten sonra 5000, 5001, 5056–5060 ya da 37291'de dinleyen kalırsa → hiçbir süreci adla durdurmadan DUR ve PID'leri raporla.
- Commit dışında push, PR, merge gerekirse → DUR.
Rapor formatı: SOP §22 yapılandırılmış rapor (Agent Verdict · Branch / HEAD · Worktree status · Changed files · Contract flow · Failure paths · Tests (taban ve son sayılar, TRX yolu) · Persistence evidence (E2 / E3 run_record yolları, E3 çıkışı, ÖZET ve SONUÇ satırları) · Security/RBAC/Tenant evidence (git grep 4 satır, T3 üçlüsü, T4-b eşitliği) · Sabotaj başına kırmızı test adı (SABOTAJ.md) · F-10 eşleme tablosu · F-12 CI ölçümü · F-13 açık kayıt · E3 taban SHA'sı · AC-1…AC-9 başına durum). Raporda açık kayıt olarak yaz: §23 canlı doğrulaması bu dilimin kabulü değildir; post-deploy §7 düzeltmeleri ve paket künyesi kule işidir.

# MDM-FU06-DL1 — sabotaj kanıtı (paket §18 a…k, AC-5)

- Paket: `execution/domains/master-data-management/module-packs/MOD-0290-FU06-product-master-read-permission-declaration.md` (sha256 `6caba52d…`)
- Talimat: `MDM-FU06-DL1-P v1.2` (sha256 `d0a80bf6…6956`)
- Kod commit'i: `ca654fa79` (dal `feature/mdm/mdm-fu06-dl1-r2`). Her sabotaj bu commit'in temiz ağacında yapıldı.
- Tarih: 2026-11-06, 14:33–14:51 (yerel). Makine: geliştirici Mac'i. Kendi mongod'u: `DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod` (MdmTestMongo); paylaşılan Mongo kullanılmadı, `MDM_COMPOSITION_GUARD_MONGO_URI` verilmedi.

## Yöntem

Her sabotaj için sırayla (işçi oturumunun scratchpad'indeki yardımcı betikle; betik depoya girmedi):

1. `git status --porcelain` boş.
2. Çapa dosyada **tam bir kez** bulunur (sayı 1 değilse durur) ve yalnız o metin değiştirilir.
3. Yük kapısı: `uptime`, 1 dakikalık yük < 12 olunca başlar (her koşudaki değer aşağıda).
4. `DITEN_TEST_MONGOD=/opt/homebrew/bin/mongod dotnet test services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj --filter "FullyQualifiedName~ModuleRegistration|FullyQualifiedName~CompositionRootGuardTests" --logger "trx;LogFileName=fu06-dl1-sab-<harf>.trx"`
   — kırmızı test adları TRX'teki `outcome="Failed"` satırlarından okunur. Derleme hatası kırmızı sayılmaz (hiçbir sabotajda olmadı).
5. `git checkout -- <dosya>`; `git status --porcelain` boş.
6. Aynı filtre yeniden koşulur (`fu06-dl1-sab-<harf>-green.trx`): 28/28 yeşil.

Kısaltmalar: **T1** `Product_master_read_is_a_non_ui_view_action_and_not_an_api_permission` · **T2**
`Declares_exact_unique_canonical_permission_contract_and_matches_runtime_enforcement` · **T3**
`Production_manifest_syncs_the_read_key_with_the_module_owner_and_tenant_scope` · **T4-b**
`Every_key_synced_under_the_module_is_in_the_entitled_read_set` · **GP**
`Global_product_declares_exact_lifecycle_actions_without_approval_or_rejection_actions` · **ID**
`Declares_exact_identity_route_and_non_baseline_flags`. Sağlayıcı = `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`.

## Sonuçlar

| # | Dosya:satır çapası | Değişiklik | Beklenen kırmızı | Görülen kırmızı (TRX) | Geçen / toplam | Geri alma sonrası `git status --porcelain` | Geri alma sonrası | Yük (1 dk) |
|---|---|---|---|---|---|---|---|---|
| a | Sağlayıcı:121 | `READ_PRODUCT_MASTER` satırı silindi | T1, T2, T3, T4-b, GP | T1, T2, T3, T4-b, GP | 23 / 28 | boş | 28 / 28 | 10.26 |
| b | Sağlayıcı:121 | `ProductMasterRead,` → `"mdm.product-master.reads",` | T1, T2, T3, T4-b, GP | T1, T2, T3, T4-b, GP | 23 / 28 | boş | 28 / 28 | 11.24 |
| b2 | `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductMaster/ProductMasterContract.cs:19` (§5 dışı, geçici) | `ReadPermission = "mdm.product-master.read"` → `"mdm.product-master.sabotage"` | T1, T2, T3, T4-b, GP | T1, T2, T3, T4-b, GP | 23 / 28 | boş | 28 / 28 | 8.62 |
| c | Sağlayıcı:95 | `RoutePath: "/MasterDataManagement/GlobalProducts"` → `"/Platform/MasterDataManagement/GlobalProducts"` | T3 (PlatformAdmin), ID | T3, ID | 26 / 28 | boş | 28 / 28 | 9.20 |
| d | Sağlayıcı:121 + Sağlayıcı:269 | eylem satırı silindi; belge sonuna `ApiPermissions: [new ModuleManifestPermission("mdm.product-master.read", "Read Product Master")]` | T1, T4-b, GP; **T3 yeşil** | T1, T4-b, GP; **T3 yeşil** | 25 / 28 | boş | 28 / 28 | 7.56 |
| e | `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductMasterController.cs:24` (§5 dışı, geçici) | sınıf `[HasPermission(ProductMasterContract.ReadPermission)]` satırı silindi | T2 | T2 | 27 / 28 | boş | 28 / 28 | 7.93 |
| f | Sağlayıcı:121 | `"View", 60` → `"Operator", 60` | T1 | T1 | 27 / 28 | boş | 28 / 28 | 10.32 |
| g | Sağlayıcı:121 | `IsRowAction: false)` → `IsRowAction: true)` | T1 | T1 | 27 / 28 | boş | 28 / 28 | 8.63 |
| h | Sağlayıcı:121 | `IsToolbarAction: false` → `IsToolbarAction: true` | T1 | T1 | 27 / 28 | boş | 28 / 28 | 10.28 |
| i | Sağlayıcı:121 | `IsRowAction: false)` → `IsRowAction: false, PermissionOwnerModuleCode: "brand-product-master")` | T1, T3, T4-b | T1, T3, T4-b | 25 / 28 | boş | 28 / 28 | 11.82 |
| j | Sağlayıcı:121 | `IsDangerous: false` → `IsDangerous: true` | T1 | T1 | 27 / 28 | boş | 28 / 28 | 9.69 |
| k | Sağlayıcı:121 | `"Read Product Master"` → `"Read Product Master Data"` | T1 | T1 | 27 / 28 | boş | 28 / 28 | 8.18 |

On iki sabotajın her birinde adı verilen testlerin hepsi kırmızı oldu, beklenmeyen kırmızı test olmadı. d'de T3 yeşil kaldı:
`ApiPermissions` yolu Auth katalog eşitlemesine ulaşıyor ama plan okumasına ulaşmıyor. Bu yüzden yalnız T4-b yakalıyor (paket §0.3).
CompositionRootGuardTests'in iki testi on iki koşunun hepsinde yeşil kaldı.

## Kırmızıdan yeşile sıra (paket §21)

| Adım | Değişiklik | Sonuç |
|---|---|---|
| Taban (değişiklik yok, `2c7301cd4`) | — | 25 / 25 yeşil |
| 1 | csproj (2 satır) + T1 + T2 genişlemesi + ID / GP listeleri | 4 kırmızı: T1, T2, GP, ID · 22 / 26 |
| 2 | + T3 / T4-b (yeni dosya) | 6 kırmızı: + T3, T4-b · 22 / 28 (iddia hatası; NRE yok, Strict Moq üye istisnası yok) |
| 3 | + manifest satırı ve 1.4.0 | 28 / 28 yeşil |

**Açık kayıt (yük kuralı):** Adım 1'in koşusu 1 dakikalık yük **12.06** iken başladı. `uptime` aynı komutta ölçüldü
ama 60 saniye beklenmedi; sınır 12 idi. Sonraki her `dotnet` komutu bir yük kapısının arkasında koştu (bekle ve yeniden
ölç, 12'nin altında başla). Adım 1'in sonucu (derleme + 4 kırmızı) yeniden koşulmadı; adım 2 aynı kırmızıları yük
9'un altında yeniden gösterdi.

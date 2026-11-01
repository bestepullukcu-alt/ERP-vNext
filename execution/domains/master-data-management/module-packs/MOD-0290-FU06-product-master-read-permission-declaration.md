---
id: MOD-0290-FU06
name: Product Master Read Permission Declaration
parent: MOD-0290
parent_name: Product / Item / SKU Master
domain: master-data-management
service: Diten.MdmService
shell: none
golden_reference: none
entity_base: EntityBase
status: ready-for-dev
runtime_code_allowed: true
runtime_code_scope: "Yalniz DL-1; onay satiri ve prompt kapisi olmadan dagitilmaz."
owner: module-pack-author
branch: chore/ct/mdm-ct-02
started: 2026-11-02
target: DL-1 (CT prompt'uyla; tarih verilmedi)
form_field_count: 0
data_classification: "Kisisel veri yok; kod icindeki izin beyani."
audit_class: "Yazma komutu yok; Platform'un manifest itme denetimi aynen calisir."
dependencies:
  - MOD-0290 (ebeveyn; `product-item-sku-master` modul manifesti)
  - MOD-0290-FU04 S4 (okuma sozlesmesi kodda; FU04 paketi taslak kalir, bu paket yalniz okur)
  - MOD-0018 (RBAC; yalniz tuketim, Auth tarafi BL-592 ile hazir)
---

# MOD-0290-FU06 — Product Master Read Permission Declaration

> **Küçük paket, tek dilim (DL-1).** Eksik izin beyanını `System` eylemi olarak ekler (§0.3). Onay satırı ve prompt kapısı olmadan kod yazılmaz.
>
> **DCP-002 kimlik kapısı — PASS (2026-11-02, bu dal, `b0ca699d7`):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0290-FU06 --name "Product Master Read Permission Declaration" --parent MOD-0290`
> → `OK  MOD-0290-FU06: proven against Blueprint/registry.` (çıkış 0).
> **Tüm dallarda:** `git log --all -S 'MOD-0290-FU06'` = 0 commit; `git log --all -- '*MOD-0290-FU06*'` = 0 commit.
> **Keşif:** `--discover "Product Master Read Permission Declaration"` çıkış 3 (inceleme gerekli; 843 satır okundu). Mükerrer yok:
> bulgular ebeveyn MOD-0290, ad benzeri izin paketleri MOD-0018-FU16 / FU20 / FU22 / FU23 / FU26 (içerikleri okunmadı, çıkarım:
> başka anahtarların Auth tarafı) ve `/api/product-master` geçidi.
> Hiçbir dalda bu anahtarı beyan eden manifest yok: `git log --all -S"mdm.product-master.read"` sağlayıcı dosyasında 0 commit.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.
> Registry satırı kule işidir; bu paket eklemez.

## 0. Kararlar

### 0.1 Sahip kararları (2026-11-02, kule sohbeti; değiştirilmedi)

| # | Karar | Gelecek regresyon riski |
|---|---|---|
| 1 | Yalnız bu izni kapsayan küçük ayrı paket. FU04'ün tamamı onaylanmıyor; FU04 taslak kalır | 🟢 |
| 2 | Stok davranışı izinleri (`mdm.gskus.edit-stock-behaviour`, `mdm.lskus.edit-stock-behaviour`, `mdm.finished-goods.edit-stock-behaviour`; BL-573 ikinci yarı) **kapsam dışı**, ayrı iş | 🟡 bugünkü durum sürer, yeni kırılma yok |
| 3 | Auth'a MDM anahtarı tohumla girmez (CT kararı 2026-10-10, BL-592) | 🟢 |

### 0.2 Kaynak kararlar

| Kaynak | Ne diyor | Bu pakete etkisi |
|---|---|---|
| [`product-backlog.md`](../../../../docs/roadmap/backlog/product-backlog.md) `:9216` | Anahtar rollere yalnız manifest → Platform kataloğu → Auth eşitlemesi ile girer; eşitleme yalnız modülün beyan ettiğini verir | Tek yol manifest |
| Aynı dosya `:9221` | Auth / Platform KABUL (`432c639ac`); kalan manifest adımı | Bu paket o adımın yarısı |
| Aynı dosya `:9222` | "`mdm.product-master.read` sayfasız API eylemi olarak beyan (`ACTIVATE_ROLLOUT` deseni; enforced listesine `ProductMasterController`)" | Önerilen yol A bu cümledir |
| FU04 O-13 (`execution/domains/master-data-management/module-packs/MOD-0290-FU04-item-material-master.md:910`) | Sözleşme okumalarının tek anahtarı `mdm.product-master.read` | Anahtar adı |
| FU04 §23 #8 (aynı dosya, "Modül platform bağlantıları") | Modül hakkı olan kiracının Admin'i izinleri plan eşitlemesiyle alır | Amaç: plan verimi, elle atama değil |

### 0.3 Kule kararı (2026-11-02) — anahtar hangi yolla beyan edilir?

**Bulgu (kod okuması; derlenmedi, çalıştırılmadı).** `ApiPermissions` yolu katalog eşitlemesine ulaşır, plan okumasına ulaşmaz:

1. `RegisterModuleManifestCommandHandler.cs:235-258`: `ApiPermissions` anahtarı Auth'a eşitlenir, sayfa ya da eylem kaydı oluşmaz.
   Auth satırı `Module = product-item-sku-master` ile yazılır (`InternalPermissionsController.cs:74`, `:85-86`).
2. `GetTenantEntitledModulePermissionsQueryHandler.cs:96-114`: Auth'un kiracı başına okuduğu anahtar listesi yalnız sayfa
   `RequiredPermission` ∪ eylem `PermissionKey`'dir. `ApiPermissions` burada yok ve Platform onu saklamıyor.
3. `EntitlementSyncConsumer.cs:165-166`, `:174`: Auth bu listeyi `GrantModuleWithKeysAsync` / `SyncTenantModulesWithKeysAsync`'e verir.
4. `EntitlementPermissionSyncService.cs:236-256`: bu modül özel profil olduğu için Auth kataloğundaki modül anahtarları ile gelen
   liste **birebir eşit** olmak zorundadır; değilse `"Special entitlement reconciliation requires the complete active module descriptor set."` fırlatılır.
5. Sonuç: katalogda anahtar var, listede yok → **`product-item-sku-master`'ın her hak verme ve uzlaştırma çağrısı fırlar.** Olay
   tüketicisinde tekrar denenir (`EntitlementSyncConsumer.cs:180-200`); kiracı açılışında yutulur, yalnız uyarı kalır
   (`InternalEventsController.cs:301-306`). Var olan haklar silinmez; yeni kiracı ve yeni anahtar bu modülden rol alamaz. Anahtarın kendisi de kimseye gitmez.

Testler bunu göremez: Auth testlerinin yardımcısı (`EntitlementPermissionSyncServiceTests.cs:2168-2174`) listeyi Platform'dan değil Auth
kataloğundan türetir. İş tanımındaki T3 (yalnız katalog eşitlemesi) de bu yolda **yeşil** kalırdı. Aynı dosya `:2036-2040` yorumu kuralı açık söyler.
Diğer `ApiPermissions` kullanan modüller (`InventoryLedgerManifestProvider.cs:157-160`, `ESignManifestProvider.cs:42-46`) etkilenmez: özel
profilleri yok, anahtar yalnız elle atanır (`ModuleManifestDocument.cs:65-68` "so a tenant admin can grant it").

**İş tanımındaki gerekçelerin ölçümü:**

| Gerekçe | Ölçüm | Sonuç |
|---|---|---|
| FU-RSV-01: UI'sız anahtar `ApiPermissions`'a | `ModuleManifestDocument.cs:64-70`, `module-self-registration-standard.md` §2d | Metin doğru; amacı elle atama, plan verimi değil |
| `ApiPermissions` 2026-10-02'den beri var | `c0abc6728` 2026-10-02 | Doğru |
| Eylem deseni izni ilgisiz sayfaya bağlar | Doğru; ama plan listesine girmesini sağlayan tam da bu bağ | Gerekçe tersine döner |
| Platform iki yolda da anahtarı aynı sahiple eşitler | Katalogda doğru (`RegisterModuleManifestCommandHandler.cs:216-227`, `:254`); plan okumasında iki yol ayrılır (madde 2) | Eksik |

**Seçenekler:**

| | Yol | Dokunulan | Regresyon riski |
|---|---|---|---|
| **A (öneri)** | Arayüzü olmayan `System` eylemi (BL-592 `:9222` metni) | Yalnız MDM manifesti + MDM testleri | 🟡 §2d "sahte sayfa/düğme yok" cümlesiyle gerilim: düğme çizilmez ama katalogda eylem satırı görünür. Modülde emsali var: `RETIRE`, `RECOVER_ORPHANED_LIFECYCLE_OPERATION` |
| B | `ApiPermissions` + Platform saklar ve plan okumasına ekler | Platform kodu + saklama (XMC-001) | 🔴 bütün modüllerin API anahtarları Admin / Viewer'a plandan gitmeye başlar; yetki genişler |
| C | `ApiPermissions` + Auth bu anahtarı eşitlik denetiminden muaf tutup adıyla verir | Auth kodu (XMC-001) | 🟡 "yalnız beyan edileni ver" kuralına istisna; BL-592 Auth kabulü yeniden açılır |
| D | Değişiklik olmadan `ApiPermissions` | Yalnız MDM | 🔴 yukarıdaki kırılma |

Öneri A: sahip kararlarının üçünü de korur, Auth ve Platform'a dokunmaz, BL-592'nin yazdığı desen. Karşılaştırma (kaynak `product-backlog.md:9362`):
"SAP'de yetki nesnesi profil üretecinde bildirilmeden role girmez; Oracle'da da bildirilmemiş fonksiyona yetki verilemez." A da aynı modeldir: role giden anahtar modül bildiriminden gelir.

**Karar: A** (kule, 2026-11-02; bulgu kule tarafından `GetTenantEntitledModulePermissionsQueryHandler.cs:96-114` ve `EntitlementPermissionSyncService.cs:236-256` okunarak doğrulandı; kod okuması, koşulmadı). Görev tanımında kule yol seçimini gerekçesiyle kendi yapar; B ve C başka modüle dokunduğu için seçilmedi.

## 1. Module Summary

`mdm.product-master.read` anahtarı `ProductMasterController` tarafından zorlanıyor ama modül manifestinde beyan edilmiyor; hiçbir rol tutamıyor.
Bu paket anahtarı `product-item-sku-master` manifestine ekler, böylece modül hakkı olan kiracıda Admin ve Viewer plan eşitlemesiyle alır.
Kullanıcı: stok ekibinin okuma sözleşmesini çağıran kiracı kullanıcıları. Ekran, kayıt, uç yok.

## 2. Ownership and Boundaries

**Kapsamda (tek dilim DL-1):** manifestte tek eylem, modül sürümü 1.3.0 → 1.4.0, §18 testleri.

**Kapsam dışı:** stok davranışı izinleri (§0.1 #2) · Auth, Platform, Inventory, Web, Gateway kodu · Platform yapılandırması (post-deploy §7, operatör) · yeni entity, uç, ekran, dil metni · FU04'ün geri kalanı.

## 3. Owned Objects

| Tür | Ad | Durum |
|---|---|---|
| İzin beyanı | `mdm.product-master.read` (`ProductMasterContract.cs:19` sabiti) | zorlama var (`ProductMasterController.cs:24`), beyan yeni |
| Manifest eylemi | `GLOBAL_PRODUCTS` sayfasında `READ_PRODUCT_MASTER` | yeni |
| Manifest sürümü | `product-item-sku-master` 1.3.0 → 1.4.0 | değişir |

Entity, depo, komut, sorgu, DTO, uç, ön yüz rotası: yok.

## 4. Entity Fields

Uygulanmaz: yeni alan ya da dizin yok. `entity_base` künye zorunluluğu için servisin değeridir.

## 5. Repo Scope

- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductMasterReadKeyPlatformPathTests.cs` (yeni)
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj` (iki satır, §18)

## 6. Protected Paths

`.antigravity/**` · `services/Diten.AuthService/**` · `services/Diten.Platform/**` · `services/Diten.InventoryService/**` ·
`frontend/**` · `gateway/**` (`ocelot.json` dahil) · `docs/guides/operations/post-deploy-steps.md` (kule işi, §21) ·
`execution/registries/**` (kule işi). Bunlardan birine dokunmak gerekirse XMC-001 durağı.

## 7. Dependencies

| Bağımlılık | Kullanım | Durum |
|---|---|---|
| FU04 S4 sözleşmesi | `ProductMasterContract.ReadPermission`, sınıf düzeyi zorlama | kodda var |
| Auth BL-592 | İsteğe bağlı tekil anahtar (`ProductIdentityLifecycleEntitlementGrantProfile.cs:152-156`, `:328-331`), Viewer listesi (`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs:167`) | kodda var; canlı ölçülmedi |
| Platform BL-592 | Güvenilir çift listesi (`TrustedLegalEntityScopeCredentialAuthenticator.cs:15`, `:55`) | kodda var; yapılandırma her ortamda ayrı, canlı ölçülmedi |
| Platform manifest eşitlemesi | `RegisterModuleManifestCommandHandler.cs:216-227` | kodda var, değişmez |

Bu dilimin kabulü canlı durumuna bağlı değildir; canlı sırası §23.

## 8. Runtime Constraints

Servis 5059, Gateway 5000; değişmez. Kiracı yalıtımı ve yumuşak silme etkilenmez: veri yazımı yok. Eşitleme Platform'da en iyi çaba ile çalışır (`CatalogPermissionSyncService.cs`); hatası §13'te.

## 9. Layout & Shell Contract

`shell: none`. Razor görünümü ve layout yok; kabul maddesi gerekmez.

## 10. Backend File Convention

Uygulanmaz: `Features/` altında dosya, işleyici ya da doğrulayıcı yok. Golden Reference (`golden_reference: none`) DataTable modülü olmadığı için alınmadı; kodu şablon olarak açılmadı. Değişiklik mevcut manifest sağlayıcısının biçimini izler (`private const` + `ModuleManifestAction` satırı).

```csharp
private const string ProductMasterRead = ProductMasterContract.ReadPermission;

new ModuleManifestAction("READ_PRODUCT_MASTER", "Read Product Master", ProductMasterRead, "System", 60,
    IsDangerous: false, IsToolbarAction: false, IsRowAction: false)
```

- Sabit Api projesinden erişilebilir: `public const`, `ProductMasterController` aynı yoldan kullanıyor (`ProductMasterContract.cs:16`, `:19`).
- `ActionType` **`"System"`**. `"Operator"` yazılmaz: Platform enum'unda yok, sessizce `Toolbar` olur (`ModulePageActionType.cs:3-12`, `RegisterModuleManifestCommandHandler.cs:709`, `:792`). BL-592'nin andığı `ACTIVATE_ROLLOUT` bu durumda (§22 F-3).
- Sayfa `GLOBAL_PRODUCTS`: modülün arayüzsüz `System` eylemleri zaten orada (`ProductItemSkuMasterManifestProvider.cs:106-115`). Kule seçimi (§25 K-2).

## 11. Frontend File Contract

Uygulanmaz: ön yüz yok. Sayfa koduna bağlı ön yüz tüketicisi yok (`GLOBAL_PRODUCTS` deposunda yalnız sağlayıcı, MDM testi ve Platform testinde geçiyor; ölçüldü). Genel katalog ekranı eylemi satır olarak gösterebilir; ölçülmedi.

## 12. Validation Rules

| Alan | Kural | Ölçüm |
|---|---|---|
| Anahtar | `mdm.product-master.read`, küçük harf, üç parça (PKS-001 §1); `ProductMasterController` ile bayt bayt aynı | T1, T2 |
| Eylem kodu | `READ_PRODUCT_MASTER`, sayfada tekil | T1 |
| Görünen ad | `Read Product Master`. Manifestlerin dil tutumu İngilizce, sabit metin: `ProductItemSkuMasterManifestProvider.cs:98`, `InventoryLedgerManifestProvider.cs:159`, `ESignManifestProvider.cs:44-45`. Dil kaynağı eklenmez | T1 |
| Bayraklar | `IsToolbarAction`, `IsRowAction`, `IsDangerous` üçü de `false` | T1 |
| `ApiPermissions` | Bu modülde boş kalır; dolarsa §0.3 kırılması | T1, T4-b |

Sayfa ve kullanıcı girdisi yok.

## 13. Failure Path to Verify

| Durum | Beklenen | Nerede |
|---|---|---|
| Anahtar yanlış yazıldı | Kırmızı | T1, T2, T3, T4-b |
| Anahtar `ApiPermissions`'a taşındı | Kırmızı | T1, T4-b |
| Anahtarı olmayan kullanıcı | 403; mevcut sınıf düzeyi zorlama | mevcut |
| Modül hakkı olmayan kiracı | Admin ve Viewer almaz | `EntitlementPermissionSyncServiceTests.cs:2121` |
| Başka modüle katalogda yazılmış anahtar | Ürün eşitlemesi kimseye vermez | `EntitlementPermissionSyncServiceTests.cs:2102` |
| Eski Auth (`432c639ac` öncesi) | Kalıcı Viewer hakkı riski; sıra §23 | post-deploy §7 (c) |
| İtme sırasında Auth eşitlemesi başarısız | Platform listesinde var, Auth kataloğunda yok → eşitlik denetimi fırlar, sonraki itmeye kadar. Her yeni anahtarda bugün de geçerli | §23 kontrol |
| Aynı manifest ikinci kez | Aynı değerler, çoğalma yok | Platform testi `RegisterModuleManifestCommandHandlerTests.cs:41` |

Yarış durumu: veri yazımı yok.

## 14. Authorization Convention

```text
Policy:     [Authorize] + [HasPermission(ProductMasterContract.ReadPermission)]   // sınıf düzeyi, değişmez
Permission: mdm.product-master.read                                               // PKS-001
Actor type: tenant_user; servis kimliği bu uçları okumaz (ProductMasterController özeti)
Scope:      Tenant (eylemde kapsam alanı yok; sayfa rotasından türer, ModulePageDescriptorNormalizer.cs:37-43)
```

Roller (kod okuması): hak varsa **Admin + Viewer** (`EntitlementPermissionSyncServiceTests.cs:1968`, `DefaultRolePermissionTemplate.cs:167`); `ProductDataSteward`, `ProductIdentityApprover`, `ProductIdentityRetirementSteward` almaz (`ProductIdentityLifecycleEntitlementGrantProfile.cs:179-237`). Hak yoksa kimse almaz. Tohum yok (§0.1 #3).

## 15. Audited Events (AUD-001)

| Komut | Olay | Yol | İstisna |
|---|---|---|---|
| Bu pakette yazma komutu yok | — | — | Kod içi beyan |

Bilgi: manifest itmesinde Platform denetim kaydı `ActionsAdded` içinde `GLOBAL_PRODUCTS/READ_PRODUCT_MASTER` taşır (`RegisterModuleManifestCommandHandler.cs:411-429`, `:568`). `ApiPermissions` bu özete girmezdi. Servisin denetim altyapısı: `tests/architecture/audit-ledger/Diten.MdmService.md`; defter değişmez.

## 16. Gateway / API Routing Decision

**Gereksiz.** `gateway/Diten.ApiGateway/ocelot.json:465-473` `/api/product-master/{everything}` var. Yeni uç yok.

## 17. Acceptance Criteria

- [ ] AC-1 T1 yeşil: üretim sağlayıcısı anahtarı `GLOBAL_PRODUCTS` üzerinde `System` eylemi olarak taşır; `ApiPermissions` boştur.
- [ ] AC-2 T2 yeşil: sınıf düzeyi öznitelik okunur, `ProductMasterController` listededir; beyan sayısı 52 → 53, neden yalnız bu anahtar.
- [ ] AC-3 T3 yeşil: üretim işleyicisi üretim manifestiyle koşar; Auth çağrısı `("mdm.product-master.read", "PRODUCT-ITEM-SKU-MASTER", "Tenant")`.
- [ ] AC-4 T4-b yeşil: plan okuması anahtarı listeler ve modül sahibiyle eşitlenen küme plan listesine eşittir.
- [ ] AC-5 §18 sabotajlarının her biri adı geçen testte kırmızı, kayıtlı.
- [ ] AC-6 `ModuleVersion` `1.4.0`; test `:38` güncel.
- [ ] AC-7 `git diff` yalnız §5 dosyaları; Auth, Platform, Web, Gateway boş.
- [ ] AC-8 `Declares_exact_identity_route_and_non_baseline_flags`: sayfa sayısı 8, değişmez.
- [ ] AC-9 `CompositionRootGuardTests` yeşil (yeni başvuruyla, §18).

Canlı doğrulama bu dilimin kabulü değildir (§23).

## 18. Test Expectations

Üretim koduyla ölçülür; anahtar test içinde yazıyla geçer, sabitten okunmaz (emsal `EntitlementPermissionSyncServiceTests.cs:1926`).

| Test | Ad | Ölçer |
|---|---|---|
| T1 | `Product_master_read_is_a_non_ui_system_action_and_not_an_api_permission` | Gerçek `GetManifest()`: eylem, anahtar, `ActionType`, bayraklar, görünen ad; `ProductMasterContract.ReadPermission` eşitliği; `ApiPermissions` boş |
| T2 | `Declares_exact_unique_canonical_permission_contract_and_matches_runtime_enforcement` (mevcut, genişler) | `enforced` metot **ve sınıf** özniteliğini okur (`controller.GetCustomAttributes<HasPermissionAttribute>()`), dizide `ProductMasterController`; `declared` `ApiPermissions`'ı da sayar; beklenen listeye anahtar; 52 → 53 |
| T3 | `Production_manifest_syncs_the_read_key_with_the_module_owner_and_tenant_scope` | Üretim `RegisterModuleManifestCommandHandler` + üretim manifesti, `TrustedProducerOwnerCode = "DITENMDMSERVICE"` (koruma gerçek koşar; emsal `RegisterModuleManifestCommandHandlerTests.cs:44`) |
| T4-a | atıf, yeni test yok | Auth plan eşitlemesi, aşağıda |
| T4-b | `Every_key_synced_under_the_module_is_in_the_entitled_read_set` | T3'ün doldurduğu sayfa ve eylem depolarıyla üretim `GetTenantEntitledModulePermissionsQueryHandler`: liste anahtarı içerir ve `PRODUCT-ITEM-SKU-MASTER` sahibiyle eşitlenen küme ile **eşittir** (Auth'un `EntitlementPermissionSyncService.cs:252` denetimi). İş tanımında yoktu; §0.3 bu test olmadan görünmez |

Ayrıca iki mevcut test listesi güncellenir: `Declares_exact_identity_route_and_non_baseline_flags` (`:38`, 1.4.0) ve `Global_product_declares_exact_lifecycle_actions_without_approval_or_rejection_actions` (`:199`, `:201-214`). Sınıf düzeyi okuma diğer dokuz kontrolörü etkilemez: sınıf satırından önce `HasPermission` yok (ölçüldü); `ProductMasterController`'da metot düzeyi yok, tek öznitelik `:24`.

**Sahip eşlemesi.** İş tanımı `product-item-sku-master` diyor; işleyici çağrıda büyük harf gönderir (`ModuleCatalogCodeNormalizer`, emsal `RegisterModuleManifestCommandHandlerTests.cs:59`), Auth saklarken küçültür (`InternalPermissionsController.cs:74`). T3 büyük harfi ölçer; küçük hali Auth tarafındadır.

**T3 / T4-b uygulanabilirliği (csproj ve tip okuması; derlenmedi).** Uygulanabilir, tek koşulla: **takma ad zorunlu.**

```xml
<ProjectReference Include="..\..\..\Diten.Platform\src\Diten.Platform.Application\Diten.Platform.Application.csproj" Aliases="PlatformProducer" />
<PackageReference Include="Moq" Version="4.20.72" />
```

- **Tip çakışması, ölçüldü:** iki projeler kümesinde tek ortak tam ad var: `Diten.Platform.Application.Contracts.Eventing.EventTransportMessage`, kasıtlı olarak aynı (`services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Eventing/EventTransportMessage.cs:1-6`). MDM testleri onu kullanıyor (`CompetitorTestDoubles.cs:189`, `WorkflowApprovalCoreTests.cs:73`); takma adsız başvuru CS0433 verir. Takma adla onlar MDM kopyasında kalır.
- **Emsal:** `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Diten.AuthService.Application.Tests.csproj:47` aynı satır; kullanım `extern alias PlatformProducer;` (`EntitlementAttestationConsumerTests.cs:1`, `:8`). `Platform.Domain` ve `Platform.Common` geçişli gelir, küresel kalır; manifest tipi ortak `Diten.BuildingBlocks.ModuleRegistration.Abstractions`'tan olduğu için iki taraf aynı tipi görür.
- **Paket sürümleri:** ortak paketler aynı ya da yukarı yönlü; `MongoDB.Driver` kümede 2.24 ve 2.27, MDM doğrudan 2.27. Küme Serilog, OpenTelemetry, `Microsoft.AspNetCore.Http.Abstractions 2.2.0` getirir; Auth test projesi aynı başvuruyu taşıyor; MDM'de **ölçülmedi**.
- **Bileşim bekçisi:** `CompositionRootGuardTests.cs:97-104` süreçteki bütün `Diten.*` derlemelerini tarar; Platform derlemesi yüklenince `IModuleManifestProvider` uygulayıcıları da taranır. 24 dosya tarandı, isteğe bağlı kurucu parametresi bulunmadı. Risk düşük; AC-9 ölçer. Sonuç test sırasına bağlı olabilir; ikisi aynı süreçte koşulur.
- **Dublörler:** işleyicinin günlük dışında sekiz bağımlılığı var; Platform testinin dublörleri `private` (`RegisterModuleManifestCommandHandlerTests.cs:860-981`), bağlanamaz. MDM testinde Moq yok; Platform ile aynı sürüm eklenir. Eşgüdümleyici ve oturum elle yazılır (genel metot; emsal `GlobalApplicabilityTestDependencies.cs:14-38`). Üretim işleyicileri dublör edilmez.
- **Maliyet:** MDM test derlemesi Platform.Application'ı da derler; CI satırı etkisi ölçülmedi.
- **Derleme kırılırsa:** dur, kuleye bildir. En dar alternatif testi Platform'a koymaktır, o da XMC-001 durağıdır ve Platform testi MDM manifestini ancak `Diten.MdmService.Api` başvurusuyla görür. Manifest kopyalanarak test yazılmaz.

**T4-a, Auth testlerine atıf** (`services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`):
`:1968` Admin tam, Viewer iki okuma · `:2005` bilinmeyen eylem ret · `:2023` kalem ve okuma bağımsız · `:2042` beyan yoksa kimseye yok · `:2102` başka modüldeki anahtar · `:2121` hak yoksa ikisi de yok. **Sınır:** bunlar listeyi Auth kataloğundan türetir (`:2168-2174`); Platform'un gerçekte gönderdiğini T4-b ölçer.

**Anahtar eşitliği, ölçüm yöntemi:** `git grep -nF '"mdm.product-master.read"' -- 'services/*/src/*'` kabul commit'inde dört üretim satırı döner, sabit dizgi araması bayt eşitliğidir: `ProductMasterContract.cs:19`, `ProductIdentityLifecycleEntitlementGrantProfile.cs:58`, `DefaultRolePermissionTemplate.cs:167`, `TrustedLegalEntityScopeCredentialAuthenticator.cs:55`. Sayı 4 değilse ya da bir satır eksikse DUR. MDM testi Auth'a başvurmaz.

**Sabotaj (üretim kodunda; çapa tam bir kez, taban yeşil, kırmızı adlı test):**

| # | Sabotaj | Beklenen kırmızı |
|---|---|---|
| a | `READ_PRODUCT_MASTER` satırını sil | T1, T2, T3, T4-b, `Global_product_declares_exact_lifecycle_actions_without_approval_or_rejection_actions` |
| b | Sağlayıcıda anahtarı `mdm.product-master.reads` yap | T1, T2, T3, T4-b |
| b2 | `ProductMasterContract.cs:19` değerini değiştir | T1, T2, T3, T4-b (hepsi yazıyla karşılaştırır) |
| c | Kapsam: eylemde `Scope` alanı yok (`ModuleManifestDocument.cs:51-62`); `GLOBAL_PRODUCTS` rotasını `/Platform/…` yap | T3 (`PlatformAdmin`), `Declares_exact_identity_route_and_non_baseline_flags` |
| d | Anahtarı eylemden çıkarıp `ApiPermissions`'a koy (§0.3 yolu) | T1, T4-b, `Global_product_declares_exact_lifecycle_actions_without_approval_or_rejection_actions`. **T3 yeşil kalır**; T4-b'nin var olma nedeni budur |
| e | `ProductMasterController.cs:24` özniteliğini sil, koşudan sonra geri al | T2 (sınıf düzeyi okumanın kanıtı) |

**Koşulacak:** MDM test projesi süzgeçle `ModuleRegistration` + `CompositionRootGuardTests`. Tam süit yok. Yazar bu paketi yazarken `dotnet` koşmadı.

## 19. Page Help (UXH-001)

Uygulanmaz: yeni ya da değişen sayfa metni yok; tur yok.

## 20. Ready-for-dev Checklist

- [x] DCP-002 kimlik kapısı (tüm dallarda ölçüldü)
- [x] Künye zorunlu alanları dolu; DataTable değil, `data_mode` gerekmez
- [x] Layout: `shell: none`, uygulanmaz
- [x] Validation Rules, Failure Path (8 satır), Authorization, Gateway kararı, Audited Events
- [x] Teslim dilimleri tablosu, kanıt seviyesi, plan sütunları
- [x] §0.3 kararı: A (kule, 2026-11-02)
- [x] Künye `status`, `runtime_code_allowed`, `runtime_code_scope`: karardan sonra güncellendi (kule)
- [ ] §25'te açık kalan K-3, K-4, K-5: kodu durdurmaz, canlı adımı ve kapanışla kapanır
- [ ] Bağımsız inceleme ve onay satırı (kule)

## 21. Implementation Notes

- **Dilim:** tek dilim, tablo `## 24. Teslim dilimleri` altında. FU05'in §21 tablosu `pack_approval.py` tarafından okunmaz (`scripts/pack_approval.py:122`, `:1337-1339` yalnız o başlığı kabul eder); araçlar çalışsın diye tablo orada, kapsadığı bölümlerde bu başlık tam adıyla geçer.
- **Plan:** plan satırı bu pakete verilmedi; dilim sütunlarında `plan satırı yok`.
- **Sıra:** önce csproj + T1, sonra T3 / T4-b, en son manifest satırı (kırmızıdan yeşile). Derleme kırılırsa §18.
- **Ölçüm sınırı:** §0.3 ve §18 kod okumasıdır; hiçbir test koşulmadı, canlı ölçülmedi.
- **Kısa adla geçen kaynakların tam yolları** (inceleme ZIP'i bu satırlardan toplar):
  - `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.ModuleRegistration.Abstractions/ModuleManifestDocument.cs`
  - `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs`
  - `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/QueryHandlers/GetTenantEntitledModulePermissionsQueryHandler.cs`
  - `services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/ModulePageDescriptorNormalizer.cs`
  - `services/Diten.Platform/src/Diten.Platform.Domain/Enums/ModulePageActionType.cs`
  - `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs`
  - `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTenantEntitlementsController.cs`
  - `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/CatalogPermissionSyncService.cs`
  - `services/Diten.Platform/src/Diten.Platform.Application/Features/ESignature/SelfRegistration/ESignManifestProvider.cs`
  - `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleRegistration/RegisterModuleManifestCommandHandlerTests.cs`
  - `services/Diten.Platform/tests/Diten.Platform.Application.Tests/GlobalApplicability/GlobalApplicabilityTestDependencies.cs`
  - `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`
  - `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs`
  - `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Eventing/EntitlementSyncConsumer.cs`
  - `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs`
  - `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalEventsController.cs`
  - `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/S2S/EntitlementAttestationConsumerTests.cs`
  - `services/Diten.InventoryService/src/Diten.InventoryService.Api/ModuleRegistration/InventoryLedgerManifestProvider.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductMaster/ProductMasterContract.cs`
  - `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductMasterController.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/CompositionRootGuardTests.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/CompetitorProducts/CompetitorTestDoubles.cs`
  - `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Approvals/WorkflowApprovalCoreTests.cs`
  - `.antigravity/rules/module-self-registration-standard.md`
  - `docs/guides/operations/post-deploy-steps.md`

**Kule işi, post-deploy §7 düzeltmeleri** (BL-592 `:9221` "manifest adımına taşınan küçükler"; yazar dosyaya dokunmadı):

1. Adım 3'e "Auth ≥ `432c639ac`".
2. Adım 1'e "bütün Platform kopyaları".
3. Viewer denetimi adım 4'ten sonraya; hak almayan kiracıda Viewer **ve** Admin.
4. Adım 4 metni "küçük MDM adımı" → bu paket; `/api/product-master` denetiminin yeşile dönmesi için tetikleyici (§23).

BL-592 aynı maddesinin diğer ikisi belge değil Auth kodudur, bu listeye girmez: "Duplicated:" iddiası (hata metni tekrarları adlandırıyor ama fırlatma koşulunda yok, `ProductIdentityLifecycleEntitlementGrantProfile.cs:345-357`) ve tek başına bilinmeyen `mdm.product-master.*` eylemi testi (`:2004` okuma anahtarıyla birlikte koşuyor). Bkz. §22 F-2.

## 22. Follow-up Items

| # | Konu | Sahip | Regresyon riski |
|---|---|---|---|
| F-1 | BL-573 ikinci yarı: stok davranışı anahtarları (§0.1 #2) | MDM + Auth, ayrı iş | 🟡 bugünkü durum sürer |
| F-2 | Auth: "Duplicated:" ve tek başına bilinmeyen eylem testi (XMC-001) | Auth | 🟢 |
| F-3 | `ACTIVATE_ROLLOUT`, `ROLLBACK_ROLLOUT` `"Operator"`, `PROPOSE` `"Action"` enum'da yok, `Toolbar` saklanıyor | MDM | 🟢 bayraklar `false`; canlı satır okunmadı |
| F-4 | Standart §2d, özel profilli modülde `ApiPermissions`'ın eşitlemeyi durdurduğunu söylemiyor (`.antigravity`, sahip onayı) | Kule | 🟡 aynı hata tekrarlanabilir |
| F-5 | Platform plan okuması ile Auth eşitlik denetimi arasında bütün özel profil modülleri için uçtan uca test yok; Auth yardımcısı (`:2168-2174`) kör | Auth + Platform, XMC-001 | 🟡 |
| F-6 | `module-implementation-status.md` satırı, kod gelince | Uygulayan ajan | 🟢 |

## 23. Canlı doğrulama (bu dilimin kabulü değildir)

**Canlı durum ölçülmedi.** Hangi ortamda hangi adımın koştuğu bu pakette bilinmiyor.

Sıra (post-deploy §7): **1.** Platform kodu + 38 çift yapılandırması. **2.** Auth ≥ `432c639ac`. **3.** En son bu manifest.

**Tetikleyici boşluğu (kod okuması):** manifest itmesi var olan kiracıda rol eşitlemesi başlatmaz. `GlobalApplicabilityChangedV1`'in Auth'ta tüketicisi yok (0 eşleşme); eşitleme yalnız hak olayları ya da abonelik değişimi (`EntitlementSyncConsumer.cs:49-60`) ve kiracı açılışıyla (`InternalEventsController.cs:301`) çalışır. Var olan kiracının bir hak olayı alması gerekir; post-deploy §7 bunu yazmıyor (§25 K-3).

Kontrol maddeleri:

- [ ] Auth kataloğunda anahtar: `Module = product-item-sku-master`, `Scope = Tenant`, `Resource = product-master`, `Action = read`.
- [ ] Platform `GET {tenantId}/entitled-modules-with-permissions` (`InternalTenantEntitlementsController.cs:103-116`) hak olan kiracıda anahtarı listeler.
- [ ] Auth günlüğünde `complete active module descriptor set` yok.
- [ ] Ürün modülü hakkı **olan** kiracıda Admin ve Viewer anahtarı tutar; dedicated roller tutmaz.
- [ ] Ürün modülü hakkı **olmayan** kiracıda Viewer ve Admin ne `mdm.items.read` ne `mdm.product-master.read` tutar.
- [ ] Enforced kiracıda `/api/product-master/…` ürün satırı döner (post-deploy §7: adım 3'ten önce kırmızı olması hata değil, sonra hatadır).
- [ ] Platform MDM kimlik bilgisi 38 çiftle geçerli.

**Stok ekibine "hazır" haberi ancak bunlardan sonra.** Haberde yazılacak (BL-592 `:9225`): varsayılan olarak hak olan kiracıda **Admin + Viewer** alır; başka rol almaz (§14). Bu cümle kod okumasıdır, canlı ölçüm sonrası teyit edilir. BL-592 "canlı rol önerisiyle hizalanır" der: `docs/records/analysis/roles/2026-10-09-canli-rol-onerisi.md`'de bu anahtar geçmiyor (ölçüldü), hizalama kule işi (§25 K-5).

## 24. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | izin beyanı ve platform yolu testleri | Owned Objects, Repo Scope, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Acceptance Criteria, Test Expectations, Implementation Notes | E2 | plan kimliği yok | plan satırı yok |

DL-1 çekirdek ve tek dilimdir. Kayıt ya da yazım yok; E2 otomatik test + sabotaj kanıtıdır. Canlı §23 ayrı kapıdır.

## 25. Açık kararlar

| # | Karar | Kim | Öneri |
|---|---|---|---|
| K-1 | §0.3 yol | Kule | **Kapandı: A** |
| K-2 | Eylemin sayfası | Kule | **Kapandı: `GLOBAL_PRODUCTS`** |
| K-3 | Var olan kiracıda eşitlemeyi kim, nasıl tetikler | Kule + deploy operatörü | post-deploy §7'ye adım; nasılı TBD (hazır toplu tetikleyici bulunmadı) |
| K-4 | `target` tarihi, onaylayan, modül PR'ını birleştiren ikinci kişi | Kule | TBD; verilmedi, uydurulmadı |
| K-5 | Stok kullanıcı rolleri + canlı rol önerisi hizalaması | Kule + stok ekibi | Hak olan kiracıda Admin + Viewer; başka rol istenirse elle atama |
| K-6 | MDM test projesine Platform başvurusu + Moq | Kule | **Kapandı: kabul** (§18) |
| K-7 | Registry satırı | Kule | **Kapandı:** kule dalında eklendi |

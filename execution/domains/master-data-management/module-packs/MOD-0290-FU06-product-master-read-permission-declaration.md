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

> **Küçük paket, tek dilim (DL-1).** Eksik izin beyanını arayüzsüz `View` eylemi olarak ekler (§0.3). Onay satırı ve prompt kapısı olmadan kod yazılmaz.
>
> **DCP-002 kimlik kapısı — PASS (2026-11-02, `b0ca699d7`):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0290-FU06 --name "Product Master Read Permission Declaration" --parent MOD-0290`
> → `OK  MOD-0290-FU06: proven against Blueprint/registry.` (çıkış 0).
> Kaynak ZIP dışı ölçümler: `git log --all -S 'MOD-0290-FU06'` ve `git log --all -- '*MOD-0290-FU06*'` 0 commit; `--discover` çıkış 3, mükerrer yok (ebeveyn MOD-0290, ad benzeri MOD-0018-FU16 / FU20 / FU22 / FU23 / FU26, içerikleri okunmadı); sağlayıcı dosyasında `git log --all -S"mdm.product-master.read"` 0 commit.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

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
| [`product-backlog.md`](../../../../docs/roadmap/backlog/product-backlog.md) `### BL-592`, "CT kararı (2026-10-10…)" | Anahtar rollere yalnız manifest → Platform kataloğu → Auth eşitlemesi ile girer; eşitleme yalnız modülün beyan ettiğini verir | Tek yol manifest |
| Aynı başlık, "BL-592 Auth / Platform KABUL" | Kalan manifest adımı (`432c639ac`) | Bu paket o adımın yarısı |
| Aynı başlık, "MDM manifest adımı" | "`mdm.product-master.read` sayfasız API eylemi olarak beyan (`ACTIVATE_ROLLOUT` deseni; enforced listesine `ProductMasterController`)" | Yol A bu cümledir |
| `execution/domains/master-data-management/module-packs/MOD-0290-FU04-item-material-master.md` O-13 satırı | Sözleşme okumalarının tek anahtarı `mdm.product-master.read` | Anahtar adı |
| Aynı paket, "Modül platform bağlantıları" #8 | Modül hakkı olan kiracının Admin'i izinleri plan eşitlemesiyle alır | Amaç: plan verimi, elle atama değil |

### 0.3 Kule kararı (2026-11-02) — anahtar hangi yolla beyan edilir?

**`ApiPermissions` katalog eşitlemesine ulaşır, plan okumasına ulaşmaz (kod okuması; derlenmedi, çalıştırılmadı):**

1. `RegisterModuleManifestCommandHandler.Handle`, `ApiPermissions` döngüsü: anahtar Auth'a eşitlenir, sayfa ya da eylem kaydı oluşmaz. Auth satırı `InternalPermissionsController.Sync` içinde `moduleOverride` ile `product-item-sku-master` modülüne yazılır.
2. `GetTenantEntitledModulePermissionsQueryHandler.Handle`: Auth'un kiracı başına okuduğu liste yalnız sayfa `RequiredPermission` ∪ eylem `PermissionKey`'dir; Platform `ApiPermissions`'ı saklamaz.
3. `EntitlementSyncConsumer.ConsumeAsync`: Auth bu listeyi `GrantModuleWithKeysAsync` / `SyncTenantModulesWithKeysAsync`'e verir.
4. `EntitlementPermissionSyncService.GrantModuleWithKeysAsync`: bu özel profilli modülde Auth kataloğundaki modül anahtarları ile gelen liste birebir eşit olmalıdır; değilse `"Special entitlement reconciliation requires the complete active module descriptor set."` fırlatılır.
5. Sonuç: katalogda var, listede yok → `product-item-sku-master`'ın her hak verme ve uzlaştırma çağrısı fırlar. Olay tüketicisi gelen kutusu kaydını bırakıp yeniden fırlatır (tekrar denenir); kiracı açılışında `InternalEventsController.SyncEntitledModulesBestEffortAsync` yutar, yalnız uyarı kalır. Var olan haklar silinmez; yeni kiracı ve yeni anahtar bu modülden rol alamaz.

Auth testlerinin yardımcısı `EntitlementPermissionSyncServiceTests.ProductIdentityDeclaredKeys` listeyi Auth kataloğundan türettiği için bunu göremez. Özel profili olmayan `ApiPermissions` kullanıcıları (`InventoryLedgerManifestProvider`, `ESignManifestProvider`) bu denetime girmez.

**Seçenekler:**

| | Yol | Dokunulan | Regresyon riski |
|---|---|---|---|
| **A (seçildi)** | Arayüzsüz eylem (`### BL-592`, "MDM manifest adımı") | Yalnız MDM manifesti + MDM testleri | 🟡 Standart §2 ve §2d'den bilinçli sapma (§22 F-4) |
| B | `ApiPermissions` + Platform saklar ve plan okumasına ekler | Platform kodu + saklama (XMC-001) | 🔴 bütün modüllerin API anahtarları Admin / Viewer'a plandan gitmeye başlar; yetki genişler |
| C | `ApiPermissions` + Auth bu anahtarı eşitlik denetiminden muaf tutup adıyla verir | Auth kodu (XMC-001) | 🟡 "yalnız beyan edileni ver" kuralına istisna; BL-592 Auth kabulü yeniden açılır |
| D | Değişiklik olmadan `ApiPermissions` | Yalnız MDM | 🔴 yukarıdaki kırılma |

Karşılaştırma (kaynak `### BL-573`, "Ek"): "SAP'de yetki nesnesi profil üretecinde bildirilmeden role girmez; Oracle'da da bildirilmemiş fonksiyona yetki verilemez." A da aynı modeldir.

**Karar: A** (kule, 2026-11-02; `GetTenantEntitledModulePermissionsQueryHandler` ve `EntitlementPermissionSyncService.GrantModuleWithKeysAsync` okunarak doğrulandı; kod okuması, koşulmadı). B ve C başka modüle dokunduğu için seçilmedi.

## 1. Module Summary

`mdm.product-master.read` anahtarı `ProductMasterController` tarafından zorlanıyor ama modül manifestinde beyan edilmiyor; hiçbir rol tutamıyor. Bu paket anahtarı `product-item-sku-master` manifestine ekler; modül hakkı olan kiracıda Admin ve Viewer plan eşitlemesiyle alır. Kullanıcı: stok ekibinin okuma sözleşmesini çağıran kiracı kullanıcıları. Ekran, kayıt, uç yok.

## 2. Ownership and Boundaries

**Kapsamda (tek dilim DL-1):** manifestte tek eylem, modül sürümü 1.3.0 → 1.4.0, §18 testleri, yerel yığın kanıtı.

**Kapsam dışı:** stok davranışı izinleri (§0.1 #2) · Auth, Platform, Inventory, Web, Gateway kodu · Platform yapılandırması (post-deploy §7, operatör) · yeni entity, uç, ekran, dil metni · FU04'ün geri kalanı.

## 3. Owned Objects

| Tür | Ad | Durum |
|---|---|---|
| İzin beyanı | `mdm.product-master.read` (`ProductMasterContract.ReadPermission`) | zorlama var (`ProductMasterController` sınıf özniteliği), beyan yeni |
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

`.antigravity/**` · `services/Diten.AuthService/**` · `services/Diten.Platform/**` · `services/Diten.InventoryService/**` · `frontend/**` · `gateway/**` (`ocelot.json` dahil) · `docs/guides/operations/post-deploy-steps.md` (kule işi, §21) · `execution/registries/**` (kule işi). Bunlardan birine dokunmak gerekirse XMC-001 durağı.

## 7. Dependencies

| Bağımlılık | Kullanım | Durum |
|---|---|---|
| FU04 S4 sözleşmesi | `ProductMasterContract.ReadPermission`, sınıf düzeyi zorlama | kodda var |
| Auth BL-592 | `ProductIdentityLifecycleEntitlementGrantProfile.ProductMasterPermissionKeys`; `DefaultRolePermissionTemplate.EntitlementOnlyViewerPermissions` | kodda var; canlı ölçülmedi |
| Platform BL-592 | `TrustedLegalEntityScopeCredentialAuthenticator.MaxAllowedPairs`, `FirstConsumerPermissions` | kodda var; yapılandırma her ortamda ayrı, canlı ölçülmedi |
| Platform manifest eşitlemesi | `RegisterModuleManifestCommandHandler` | kodda var, değişmez |

## 8. Runtime Constraints

Servis 5059, Gateway 5000; değişmez. Kiracı yalıtımı ve yumuşak silme etkilenmez: MDM'de veri yazımı yok. Manifest her MDM kopyasının açılışında itilir (`ModuleRegistrationHostedService`); Platform'un Auth eşitlemesi en iyi çabadır (`CatalogPermissionSyncService`). Hataları §13.

## 9. Layout & Shell Contract

`shell: none`. Razor görünümü ve layout yok; kabul maddesi gerekmez.

## 10. Backend File Convention

Uygulanmaz: `Features/` altında dosya yok. `golden_reference: none`, DataTable modülü değil. Değişiklik mevcut sağlayıcının biçimini izler.

```csharp
private const string ProductMasterRead = ProductMasterContract.ReadPermission;

new ModuleManifestAction("READ_PRODUCT_MASTER", "Read Product Master", ProductMasterRead, "View", 60,
    IsDangerous: false, IsToolbarAction: false, IsRowAction: false)
```

- Sabit Api projesinden erişilebilir: `public const`, `ProductMasterController` aynı yoldan kullanıyor.
- **`ActionType`:** eylem türünün tek okuyucusu Platform katalog yönetim ekranıdır (etiket; `frontend/Diten.Web/wwwroot/assets/js/Platform/ModuleCatalog/page-details.js` `actionTypeLabel`). Plan okuması, Auth ve kiracı menüsü türe bakmaz (`ModulePageActionType` kod araması, kaynak ZIP dışı tarama); `System` ile `View` davranışta aynıdır. `View` seçildi (kule, K-8): anahtarı insan tutar ve bir okumadır; `WorkAggregationManifestProvider` sınıf notu aynı amacı ("non-UI View action … the entitled-module key pull") bu türle yazar. `System` seçilmedi: bu modülde o tür kişiye verilmeyen eylemleri taşır (`RETIRE`, `RECOVER_ORPHANED_LIFECYCLE_OPERATION`).
- `"Operator"` yazılmaz: `ModulePageActionType` enum'unda yok, `RegisterModuleManifestCommandHandler.ParseEnum` sessizce `Toolbar` yapar (§22 F-3).
- Sayfa `GLOBAL_PRODUCTS`: modülün arayüzsüz eylemleri zaten orada. Kule seçimi (§25 K-2).

## 11. Frontend File Contract

Uygulanmaz: ön yüz değişmez. Tek görünür etki: Platform katalog yönetim ekranındaki eylem listesinde arayüzsüz bir satır (§10); kiracı ekranında düğme yok (`GLOBAL_PRODUCTS` kod araması, kaynak ZIP dışı).

## 12. Validation Rules

| Alan | Kural | Ölçüm |
|---|---|---|
| Anahtar | `mdm.product-master.read`, küçük harf, üç parça (`.antigravity/rules/permission-key-standard.md` "Canonical format"); `ProductMasterController` ile aynı | T1, T2 |
| Eylem kodu | `READ_PRODUCT_MASTER`, sayfada tekil | T1 |
| Görünen ad | `Read Product Master`. Manifestlerin dil tutumu İngilizce sabit metin (`ProductItemSkuMasterManifestProvider`, `InventoryLedgerManifestProvider`, `ESignManifestProvider`). Dil kaynağı eklenmez | T1 |
| Bayraklar | `IsToolbarAction`, `IsRowAction`, `IsDangerous` üçü de `false`; `PermissionOwnerModuleCode` boş | T1 |
| `ApiPermissions` | Bu modülde boş kalır | T1, T4-b |

Sayfa ve kullanıcı girdisi yok.

## 13. Failure Path to Verify

| Durum | Beklenen | Nerede |
|---|---|---|
| Anahtarı olmayan kullanıcı | 403; mevcut sınıf düzeyi zorlama | E3 |
| Modül hakkı olmayan kiracı | Admin ve Viewer almaz | `BL592_A_tenant_without_the_product_module_entitlement_gets_neither` (destekleyici) |
| Başka modüle katalogda yazılmış anahtar | Ürün eşitlemesi kimseye vermez | `BL592_FIX1_A_read_contract_catalogued_under_another_module_is_granted_to_nobody_by_the_product_sync` (destekleyici) |
| Eski Auth (`432c639ac` öncesi) | Kalıcı Viewer hakkı riski; sıra §23 | post-deploy §7 "Yapılmazsa" (c) |
| İtme sırasında Auth eşitlemesi başarısız | Platform listesinde var, Auth kataloğunda yok → eşitlik denetimi fırlar, sonraki itmeye kadar. Her yeni anahtarda bugün de geçerli | §23 denetim |
| Platform'a itme 5 denemede başarısız | Eski manifest kalır, MDM yalnız uyarı yazar (`ModuleRegistrationHostedService`); anahtar beyan edilmemiş sayılır | §23 denetim |
| **MDM 1.3.0'a geri döner** ya da **eski kopya yeniden başlar** | Eylem budanır (`RegisterModuleManifestCommandHandler` "MC-6"), Auth satırı silinmez ("additive") → §0.3 kırılması. Platform sürüm karşılaştırmaz, son itme kazanır. **Kule kararı: geri alınmaz, yalnız ileri düzeltme** (§23) | §23 |
| Eski ve yeni kopya karışık iter | Her kopya açılışta iter; son başlayan belirler. Eski kopya sonra başlarsa aynı kırılma. Yeni kopyanın yeniden başlaması düzeltir: `ux_platform_module_page_actions_tenant_page_action` kısmi dizindir (`IsDeleted = false`; `PlatformSchemaManifest.Core`, `PlatformSchemaMigrations` "FIX C"), budanan satır kodu tutmaz, eylem yeni satırla döner (kod okuması). Kopya sayısı depoda bulunmadı (kaynak ZIP dışı arama) | §23 |
| Aynı manifest ikinci kez | Aynı değerler, çoğalma yok | `Explicit_permission_owner_flows_in_descriptor_order_and_replay_converges_to_the_same_payloads` (destekleyici) |

## 14. Authorization Convention

```text
Policy:     [Authorize] + [HasPermission(ProductMasterContract.ReadPermission)]   // sınıf düzeyi, değişmez
Permission: mdm.product-master.read                                               // PKS-001
Actor type: tenant_user; servis kimliği bu uçları okumaz (ProductMasterController özeti)
Scope:      Tenant (eylemde kapsam alanı yok; ModulePageDescriptorNormalizer.ScopeFromRoute sayfa rotasından türetir)
```

Rol verimi (kod okuması):

| Kim | Ne zaman | Kaynak |
|---|---|---|
| Kiracı **Admin + Viewer** | Ürün modülü hakkı olan kiracıda, eşitleme çalışınca | `BL592_Declared_item_keys_and_read_contract_go_to_admin_in_full_and_to_viewer_as_reads`; `DefaultRolePermissionTemplate.EntitlementOnlyViewerPermissions` |
| Varsayılan kiracı **SuperAdmin** | Anahtar Auth kataloğuna ilk yazıldığında | `InternalPermissionsController.Sync` → `FullCatalogPermissionGrantService.GrantToFullCatalogRolesAsync`; `DefaultRolePermissionTemplate.SelectFor` |
| Kiracı rolü, **elle** | Kiracı yöneticisi atarsa; modül hakkına bakılmaz | `DefaultRolePermissionTemplate.IsTenantAssignable` (Tenant kapsamı), `AssignPermissionCommandHandler` |
| `ProductDataSteward`, `ProductIdentityApprover`, `ProductIdentityRetirementSteward` | Almaz | `ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles` |

Modül hakkı yoksa plan eşitlemesi vermez. Tohum yok (§0.1 #3).

## 15. Audited Events (AUD-001)

| Komut | Olay | Yol | İstisna |
|---|---|---|---|
| Bu pakette yazma komutu yok | — | — | Kod içi beyan |

Bilgi: manifest itmesinde Platform denetim kaydı `ActionsAdded` içinde `GLOBAL_PRODUCTS/READ_PRODUCT_MASTER` taşır (`RegisterModuleManifestCommandHandler.AuditMetadataOf`). Defter `tests/architecture/audit-ledger/Diten.MdmService.md` değişmez.

## 16. Gateway / API Routing Decision

**Gereksiz.** `gateway/Diten.ApiGateway/ocelot.json` `/api/product-master/{everything}` rotası var. Yeni uç yok.

## 17. Acceptance Criteria

- [ ] AC-1 T1 yeşil: üretim sağlayıcısı anahtarı `GLOBAL_PRODUCTS` üzerinde arayüzsüz `View` eylemi olarak taşır; `ApiPermissions` boştur.
- [ ] AC-2 T2 yeşil: sınıf düzeyi öznitelik okunur, `ProductMasterController` listededir; beyan sayısı 52 → 53, neden yalnız bu anahtar.
- [ ] AC-3 T3 yeşil: üretim işleyicisi üretim manifestiyle koşar; Auth çağrısı `("mdm.product-master.read", "PRODUCT-ITEM-SKU-MASTER", "Tenant")`.
- [ ] AC-4 T4-b yeşil: plan okuması anahtarı listeler ve modül sahibiyle eşitlenen küme plan listesine eşittir.
- [ ] AC-5 §18 sabotajlarının her biri adı geçen testte kırmızı; `SABOTAJ.md` satırında çapa, kırmızı test adı ve geri alma sonrası `git status --porcelain` boş.
- [ ] AC-6 `ModuleVersion` `1.4.0`; `Declares_exact_identity_route_and_non_baseline_flags` güncel, sayfa sayısı 8 değişmez.
- [ ] AC-7 `git diff` yalnız §5 dosyaları ve §18 kanıt dizini; Auth, Platform, Web, Gateway boş.
- [ ] AC-8 `CompositionRootGuardTests` yeşil, T3 ile aynı süreçte.
- [ ] AC-9 §18 E3 **yükseltme yolu**, tek `run_record.py` koşusunun süzülmüş çıktısında, sırayla: (Y2) taban MDM ile hak satırı var, Admin ve Viewer yeni jetonda anahtarsız, `/api/product-master/stockable-items` 403 ve gövdede `LEGAL_ENTITY_SCOPE_FORBIDDEN` yok; (Y3) 1.4.0 itildi, Auth kataloğunda satır var, kiracı hâlâ anahtarsız, 403; (Y4) `PATCH` uzlaştırmasından sonra yeni jetonda Admin ve Viewer anahtarlı, 200, anahtarsız kullanıcı 403, Auth günlüğünde eşitlik hatası 0; (Y5) yeni kiracıda yeni hak satırıyla 200. Çıktıda jeton ve anahtar yok. **Yük sınırı aşılırsa E3 bekler; DL-1 kabulü de bekler.**

§23 canlı doğrulaması bu dilimin kabulü değildir.

## 18. Test Expectations

Üretim koduyla ölçülür; anahtar test içinde yazıyla geçer, sabitten okunmaz (emsal `EntitlementPermissionSyncServiceTests`, `ItemKeys` üstündeki not).

| Test | Ad | Ölçer |
|---|---|---|
| T1 | `Product_master_read_is_a_non_ui_view_action_and_not_an_api_permission` | Gerçek `GetManifest()`: eylem, anahtar, `ActionType`, üç bayrak, `PermissionOwnerModuleCode`, görünen ad; `ProductMasterContract.ReadPermission` eşitliği; `ApiPermissions` boş |
| T2 | `Declares_exact_unique_canonical_permission_contract_and_matches_runtime_enforcement` (mevcut, genişler) | `enforced` metot **ve sınıf** özniteliğini okur (`controller.GetCustomAttributes<HasPermissionAttribute>()`), dizide `ProductMasterController`; `declared` `ApiPermissions`'ı da sayar; beklenen listeye anahtar; 52 → 53 |
| T3 | `Production_manifest_syncs_the_read_key_with_the_module_owner_and_tenant_scope` | Üretim `RegisterModuleManifestCommandHandler` + üretim manifesti, `TrustedProducerOwnerCode = "DITENMDMSERVICE"` (koruma gerçek koşar). Sahip büyük harftir (`ModuleCatalogCodeNormalizer`); Auth saklarken küçültür (`InternalPermissionsController.Sync`) |
| T4-a | atıf, yeni test yok | Aşağıda; destekleyici |
| T4-b | `Every_key_synced_under_the_module_is_in_the_entitled_read_set` | T3'ün doldurduğu sayfa ve eylem depolarıyla üretim `GetTenantEntitledModulePermissionsQueryHandler`: liste anahtarı içerir ve `PRODUCT-ITEM-SKU-MASTER` sahibiyle eşitlenen küme ile **eşittir** (`GrantModuleWithKeysAsync` denetiminin girdisi) |

İki mevcut test listesi güncellenir: `Declares_exact_identity_route_and_non_baseline_flags` (1.4.0) ve `Global_product_declares_exact_lifecycle_actions_without_approval_or_rejection_actions`. Sınıf düzeyi okuma diğer dokuz kontrolörü etkilemez: sınıf satırından önce `HasPermission` yok (§21 listesindeki dosyalarla ölçülür); `ProductMasterController`'da metot düzeyi yok.

**T3 / T4-b başvurusu (csproj ve tip okuması; derlenmedi).** Takma ad zorunlu:

```xml
<ProjectReference Include="..\..\..\Diten.Platform\src\Diten.Platform.Application\Diten.Platform.Application.csproj" Aliases="PlatformProducer" />
<PackageReference Include="Moq" Version="4.20.72" />
```

- **Tip çakışması:** iki proje kümesinde tek ortak tam ad `Diten.Platform.Application.Contracts.Eventing.EventTransportMessage`, kasıtlı olarak aynı (`services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Eventing/EventTransportMessage.cs`). MDM testleri kullanıyor (`CompetitorTestDoubles`, `WorkflowApprovalCoreTests`); takma adsız CS0433. Emsal `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Diten.AuthService.Application.Tests.csproj`, `extern alias PlatformProducer` (`EntitlementAttestationConsumerTests`). `Platform.Domain` ve `Platform.Common` geçişli, küresel kalır; manifest tipi ortak `Diten.BuildingBlocks.ModuleRegistration.Abstractions`'tan gelir.
- **Paket sürümleri:** ortak paketler aynı ya da yukarı yönlü; MDM'de derleme **ölçülmedi**.
- **Bileşim bekçisi:** `CompositionRootGuardTests.Host` süreçteki bütün `Diten.*` derlemelerini tarar, MDM konteynerinde kayıtlı arayüz uygulayanları seçer. Platform'un `IModuleManifestProvider`'ı MDM'ninkinden ayrı tiptir; ortak `Abstractions` arayüz taşımaz. Kod okumasına göre yeni tip seçilmez; AC-8 ölçer.
- **Derleme kırılırsa:** dur, kuleye bildir. En dar alternatif testi Platform'a koymaktır, o da XMC-001 durağıdır ve MDM manifestini ancak `Diten.MdmService.Api` başvurusuyla görür. Manifest kopyalanarak test yazılmaz.

**Gerçek ve dublör.** Yalnız ölçülen iki işleyici gerçek koşar: `RegisterModuleManifestCommandHandler`, `GetTenantEntitledModulePermissionsQueryHandler`. Hak kararı ölçülmez: alt sorgular `IMediator` dublörüyle "hak var" der. Platform testinin dublörleri `private`, bağlanamaz.

| Bağımlılık | Dublör | Zorunlu kurulum |
|---|---|---|
| `ITransactionalModuleCatalogRepository` | Moq | `GetByCodeIncludingDeletedAsync`, `GetByCodeAsync(session, …)` → null; `CreateAsync(session, …)` |
| `IModulePageDescriptorRepository` | Moq + bellek listesi | `GetByModuleAsync` **liste döner** (null → `BuildReconcilePlanAsync` NRE), `CreateAsync`, `UpdateAsync` |
| `IModulePageActionDescriptorRepository` | Moq + bellek listesi | `GetByPageAsync` **liste döner** (null → NRE), `CreateAsync`, `UpdateAsync` |
| `ICatalogPermissionSyncService` | Moq, kaydeder | `SyncPermissionAsync` → `Synced` |
| `IModuleTaxonomyResolver` | Moq | `ResolveServiceCodeAsync` girdiyi döner |
| `IModuleDomainRepository` | Moq | `QueryAsync` → `MASTERDATAMANAGEMENT` kodlu `ModuleDomain` satırı, toplam 1 (**kurulmazsa `ResolveOrRegisterDomainCodeAsync` NRE**) |
| `IGlobalApplicabilityTransactionCoordinator`, `IPlatformTransactionSession` | Elle (genel metot; emsal `GlobalApplicabilityTestDependencies.InlineCoordinator`) | Gövdeyi koşar, izdüşümü yazar |
| `IGlobalApplicabilityStateRepository` | Moq | `UpsertModuleCatalogAsync` tamamlanır |
| `ILogger<>` | `NullLogger` | — |
| `IMediator` | Moq (emsal `GetTenantEntitledModulePermissionsQueryHandlerTests.Build`) | `GetTenantModuleEntitlementsQuery` → modül satırı, `GetTenantModuleEffectiveAccessQuery` → `HasAccess = true`; kiracı kimlikleri eşit |
| `ITenantContext` | Gerçek `TenantContext` (`Diten.Platform.Common`) | Üretim `TenantScope.BeginPlatform` kullanır |

**T4-a, destekleyici** (`EntitlementPermissionSyncServiceTests`): `BL592_Declared_item_keys_and_read_contract_go_to_admin_in_full_and_to_viewer_as_reads` · `BL592_An_unknown_item_or_product_master_action_is_refused` · `BL592_Item_keys_and_the_read_contract_are_independent_optional_parts` · `BL592_An_undeclared_read_contract_is_granted_to_nobody_and_refuses_nothing` · `BL592_FIX1_A_read_contract_catalogued_under_another_module_is_granted_to_nobody_by_the_product_sync` · `BL592_A_tenant_without_the_product_module_entitlement_gets_neither`. Listeyi Auth kataloğundan türetirler; Platform'un gönderdiğini T4-b ölçer.

**Anahtar eşitliği:** `git grep -nF '"mdm.product-master.read"' -- 'services/*/src/*'` kabul commit'inde dört üretim satırı döner, sabit dizgi araması bayt eşitliğidir: `ProductMasterContract`, `ProductIdentityLifecycleEntitlementGrantProfile`, `DefaultRolePermissionTemplate`, `TrustedLegalEntityScopeCredentialAuthenticator`. Sayı 4 değilse DUR. MDM testi Auth'a başvurmaz.

**Sabotaj (üretim kodunda; çapa tam bir kez, taban yeşil, kırmızı adlı test; her biri koşudan sonra geri alınır). Ağaç kirlendiği için `run_record` dışıdır; kayıt kanıt dizininde `SABOTAJ.md`.** `Global_product_…` = `Global_product_declares_exact_lifecycle_actions_without_approval_or_rejection_actions`.

| # | Sabotaj | Beklenen kırmızı |
|---|---|---|
| a | `READ_PRODUCT_MASTER` satırını sil | T1, T2, T3, T4-b, `Global_product_…` |
| b | Sağlayıcıda anahtarı `mdm.product-master.reads` yap | T1, T2, T3, T4-b, `Global_product_…` |
| b2 | `ProductMasterContract.ReadPermission` değerini değiştir; §5 dışı dosya, geri alınır | T1, T2, T3, T4-b, `Global_product_…` |
| c | Eylemde `Scope` alanı yok; `GLOBAL_PRODUCTS` rotasını `/Platform/…` yap | T3 (`PlatformAdmin`), `Declares_exact_identity_route_and_non_baseline_flags` |
| d | Anahtarı eylemden çıkarıp `ApiPermissions`'a koy | T1, T4-b, `Global_product_…`. **T3 yeşil kalır**; T4-b bu yüzden var |
| e | `ProductMasterController` sınıf özniteliğini sil; §5 dışı dosya, geri alınır | T2 |
| f | `ActionType` `"View"` → `"Operator"` | T1 |
| g | `IsRowAction` `true` | T1 |
| h | `IsToolbarAction` `true` | T1 |
| i | Eyleme `PermissionOwnerModuleCode: "brand-product-master"` | T1, T3, T4-b (sahip `BRAND-PRODUCT-MASTER` olur; plan listesinde anahtar kalır, modül sahipli küme eşit olmaz) |
| j | `IsDangerous` `true` | T1 |
| k | Görünen adı değiştir | T1 |

**Koşulacak (E2).** Sabit `MdmTestMongo` test mongod'u ister (`DITEN_TEST_MONGOD`; bekçi için `MDM_COMPOSITION_GUARD_MONGO_URI` de olur); paylaşılan Mongo kullanılmaz. Aynı süreçte, tek komut; tam süit yok:

```text
dotnet test services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Diten.MdmService.Application.Tests.csproj --filter "FullyQualifiedName~ModuleRegistration|FullyQualifiedName~CompositionRootGuardTests"
```

**Kanıt kaydı.** `scripts/run_record.py` kirli ağacı reddeder, `--out` yalnız denetim kayıtları altında yeni dizin ister, argv ve stdout'u saklar. Bu yüzden:

- **Tek kanıt betiği, tek koşu.** Kart S-83 (`docs/guides/operations/control-tower-operating-card.md` "Kanıt betiğini CT yazar"): betiği **kule yazar**, kırmızı tabanını kaydeder, prompt'a yol + SHA-256 ile verir; işçi yazmaz, değiştirmez.
- **Sırlar:** yalnız ortam değişkeninden okunur, argv'ye girmez. Betik çıktıyı süzer: `Authorization`, jeton ve anahtar değeri yazılmaz; yanıttan yalnız durum kodu ve beklenen alan.
- **Kanıt dizini:** `docs/records/audits/2026-11/mod0290-fu06-dl1/` (yeni): betik ve `SABOTAJ.md` burada; E3 koşusu `--out .../e3-run`, E2 komutu ayrı `--out .../e2-run`. Kod commit'inden sonra, temiz ağaçta.

**Yerel yığın (E3): yükseltme yolu.** Canlı değildir, §23'ün yerine geçmez. Var olan kiracının kazancı `PATCH` uzlaştırmasıyla gelir (§23 pilot); hak satırı yeni manifestten sonra açılırsa `TenantEntitlementAddedV1` anahtarı önceden verir ve `PATCH` hiçbir şey kanıtlamaz. Bu yüzden yığın **taban MDM** ile başlar. Kaynak `scripts/dev-up.sh`; portlar `AGENTS.md` §3.

**Taban commit:** `git merge-base origin/main <dal>`, ölçülen değer kayda yazılır. Koşul: o commit'te `ProductItemSkuMasterManifestProvider.cs` `ModuleVersion: "1.3.0"` taşır ve `READ_PRODUCT_MASTER` taşımaz (`git show <taban>:<yol> | grep`; bugün `b0ca699d7`, koşul tutuyor). Tutmazsa onay anında kayda yazılan commit kullanılır; o da tutmazsa kuleye dur. Taban ikilisi repo dışı geçici dizindedir: `git worktree add --detach <geçici> <taban>`, orada yalnız `services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj` derlenir, koşu sonunda `git worktree remove`.

**Jeton:** MDM izni jetondaki `permission` iddiasından okur (`PermissionAuthorizationHandler`). Her ölçümde kullanıcı **yeniden girer**; anahtar kararı yeni jetondadır, betik yalnız var/yok yazar.

**Başta ölçülür (yığın açılmadan; biri tutmazsa durur, kayda yazar):**

| Önkoşul | Ölçüm | Tutmazsa |
|---|---|---|
| Yük | `uptime` 1 dakikalık ≤ 12 | E3 bekler |
| Port ve süreç | 5000, 5001, 5056, 5057, 5058, 5059, 5060 dinleyen yok (`lsof`). `scripts/dev-up.sh --restart` **kullanılmaz**: porttaki süreci sahibine bakmadan öldürür. Yalnız betiğin başlattığı süreç, kayıtlı PID + başlangıç zamanı eşleşirse durur | **Kuleye dur.** Başka oturum olabilir; yeniden başlatılmaz |
| Atılabilir veritabanı (zorunlu) | Kendi mongod'u: `--replSet`, paylaşılan 27017–27021 dışı port, yeni `--dbpath` (emsal `MdmTestMongo`). Servislere ortam değişkeni: Auth ve Platform `MongoDbSettings__ConnectionString`, `MongoDbSettings__DatabaseName`, Platform `BackgroundJobs__StorageDatabaseName`, MDM `Mongo__ConnectionString`, `Mongo__DatabaseName`. `scripts/dev-up.sh` süreci `env` ile başlattığı için ortam geçer (kod okuması; koşulmadı) | **Kuleye dur.** Paylaşılan veritabanına yazılmaz |
| RabbitMQ (zorunlu) | Çalışıyor (`scripts/dev-up.sh` başlatmaz), yeni vhost: `Eventing__VirtualHost` (üç serviste aynı ayar) | Kuleye dur |
| MDM kayıt kimliği | Platform `ModuleRegistrationCredentials:Mdm` ile MDM `PlatformRegistration:ModuleRegistrationCredential*` ayarları eşleşir | Kuleye dur |
| Derleme | Bu daldan Auth, Platform, MDM, Gateway Api projeleri (Gateway `scripts/dev-up.sh` için ikili ister); taban MDM | Kuleye dur |

**Yığın ayaktayken kurulur ve ölçülür:** kiracı, kullanıcılar, hak satırı, yayılım modu. Veri yeni veritabanına ancak servisler açıkken yazılır; kullanıcı kurulum yolunu betik yazılırken kule ölçer (bu pakette ölçülmedi).

**Betik sırası (gerçek sıra):**

| # | Adım | Komut | Beklenen |
|---|---|---|---|
| Y1 | Yığın, taban MDM | Taban MDM 5059'da başlar (`--urls`, içerik kökü proje dizini, `scripts/dev-up.sh` `start_one` gibi), sonra `scripts/dev-up.sh` bayraksız: dolu porta dokunmaz ("already up"). Auth ve Platform `/health/live` cevap verince taban MDM kendi PID + başlangıç zamanıyla durur, bir kez yeniden başlar: itme 5 denemeyle sınırlı, Platform'dan önce biterse kaybolur | MDM günlüğünde `Module manifest self-registered … ModuleCode=product-item-sku-master`; Platform katalogunda **1.3.0**, `READ_PRODUCT_MASTER` yok |
| Y2a | Kiracı ve hak satırı | Kiracı + Admin + Viewer + anahtarsız kullanıcı; ürün modülü planda değil. `POST /api/platform/tenants/{tenantId}/commercial/module-entitlements` (`AddTenantModuleEntitlementCommandHandler`, `AddTenantModuleEntitlementCommandValidator`): `Source = Addon`, `ExpiryDateUtc` gelecekte, `Reason` dolu; `PlatformAdminOnly`. §23 pilot koşuluna uyar | Satır ve `RowVersion` okunur. `ProductLegalEntityScopeRolloutMode` ölçülür; `Enforced` ise çift gerekir (`TrustedLegalEntityScopeCredential:Mdm:AllowedPairs`, geliştirme ayarlarında yok, kaynak ZIP dışı ölçüm; Platform başlamadan verilmeliydi): kuleye dur |
| Y2b | Ön ölçüm | Admin ve Viewer yeni jeton; `GET http://localhost:5000/api/product-master/stockable-items`. Auth Mongo `permissions`, `Key = mdm.product-master.read` | Jetonda anahtar yok, 403, gövdede `LEGAL_ENTITY_SCOPE_FORBIDDEN` yok; katalogda satır yok |
| Y3a | MDM yeni sürüm | Taban MDM kendi PID + başlangıç zamanıyla durur; `scripts/dev-up.sh` bayraksız yalnız boş 5059'u bu dalın ikilisiyle açar | Günlükte yeni itme; Platform katalogunda **1.4.0**, `READ_PRODUCT_MASTER` var |
| Y3b | Henüz kazanç yok | Auth `permissions`; `GET http://localhost:5057/api/internal/tenants/{tenantId}/entitled-modules-with-permissions`, başlık `X-Internal-Api-Key`; Admin ve Viewer yeni jeton, aynı istek | Katalogda `Module = product-item-sku-master`, `Scope` Tenant; Platform listesinde anahtar; **jetonda hâlâ yok, 403**; `entitlement.sync.applied` sayısı artmadı |
| Y4a | Tetikleyici | §23 pilot `PATCH`: aynı bitiş, güncel `RowVersion`, yeni `Reason`. Olay asenkron: en çok 60 sn yoklanır | Auth günlüğünde yeni `entitlement.sync.applied` |
| Y4b | Kazanç | Admin ve Viewer yeni jeton, aynı istek; anahtarsız kullanıcı yeni jeton, aynı istek | Admin ve Viewer jetonda anahtarlı, 200; anahtarsız 403, gövdede `LEGAL_ENTITY_SCOPE_FORBIDDEN` yok |
| Y4c | Eşitlik hatası | Auth günlüğünde `complete active module descriptor set` sayısı, bütün koşu | 0 |
| Y5 | Yeni kiracı, yeni hak | İkinci kiracı + Admin + Viewer; aynı `POST` (`TenantEntitlementAddedV1`); `PATCH` yok; yeni jeton, aynı istek | Admin ve Viewer 200 |
| Y6 | Temizlik | Betiğin süreçleri PID + başlangıç zamanıyla durur; `git worktree remove`; `git status --porcelain` yalnız `--out` | Paylaşılan veritabanı ve portlar dokunulmamış |

**403 ayrımı:** rol yokluğu `[HasPermission]`'dan gelir; `LEGAL_ENTITY_SCOPE_FORBIDDEN` kapsam reddidir (`ProductLegalEntityScopeCandidateFacade`, `ProductMasterController.Answer`). Y2b, Y3b ya da Y4b'de bu kod çıkarsa neden kapsamdır, E3 kırmızı.

## 19. Page Help (UXH-001)

Uygulanmaz: yeni ya da değişen sayfa metni yok; tur yok.

## 20. Ready-for-dev Checklist

- [x] DCP-002 kimlik kapısı
- [x] Künye zorunlu alanları dolu; DataTable değil, `data_mode` gerekmez
- [x] Layout: `shell: none`, uygulanmaz
- [x] Validation Rules, Failure Path, Authorization, Gateway kararı, Audited Events
- [x] Teslim dilimleri tablosu, kanıt seviyesi, plan sütunları
- [x] §0.3 kararı: A (kule, 2026-11-02)
- [x] Künye `status`, `runtime_code_allowed`, `runtime_code_scope`: karardan sonra güncellendi (kule)
- [ ] §25 K-3, K-4, K-5: kodu durdurmaz; K-3 ve K-5 stok haberinden önce kapanır
- [ ] post-deploy §7 düzeltmeleri modül PR'ından önce (§21, kule)
- [ ] Bağımsız inceleme ve onay satırı (kule)

## 21. Implementation Notes

- **Dilim:** tablo `## 24. Teslim dilimleri` altında; araç yalnız o başlığı okur (`scripts/pack_approval.py` `SLICE_HEADING`).
- **Plan:** plan satırı bu pakete verilmedi; dilim sütunlarında `plan satırı yok`.
- **Sıra:** önce csproj + T1, sonra T3 / T4-b, en son manifest satırı (kırmızıdan yeşile).
- **Ölçüm sınırı:** bu paketteki davranış iddiaları kod okumasıdır; yazar hiçbir test koşmadı, canlı ölçülmedi.

**Kule işi, post-deploy §7 düzeltmeleri, modül PR'ından önce** (`### BL-592`, "manifest adımına taşınan küçükler"):

1. Adım 3'e "Auth ≥ `432c639ac`".
2. Adım 1'e "bütün Platform kopyaları".
3. Viewer denetimi adım 4'ten sonraya; hak almayan kiracıda Viewer **ve** Admin.
4. Adım 4 "küçük MDM adımı" → "en son MDM dağıtımı (bu paket; MDM açılışta iter)"; ardından §23 pilot kiracı adımı.
5. "Geri alma" satırı: MDM manifest adımı geri alınmaz, yalnız ileri düzeltme; zorunluysa §23 sırası. Bugünkü "önce manifest adımı geri alınmalı" cümlesi §0.3 kırılmasını doğurur.

Aynı BL maddesinin diğer ikisi Auth kodudur (§22 F-2).

**Tam yollar (inceleme ZIP'i bu satırlardan toplar).** `appsettings.*.json` ZIP'e girmez (`scripts/review_zip.py` `APPSETTINGS`): bu paketteki ayar adı ölçümleri **kaynak ZIP dışıdır**, dosya yolları listelenmez.

- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.ModuleRegistration.Abstractions/ModuleManifestDocument.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Diten.Platform.Application.csproj`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/QueryHandlers/GetTenantEntitledModulePermissionsQueryHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/UpdateTenantModuleEntitlementExpiryCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/RefreshTenantModuleEntitlementProjectionCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/AddTenantModuleEntitlementCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/TenantModuleEntitlementActionGate.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/TenantModuleEntitlementRowActions.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Validators/TenantModuleEntitlementValidators.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.Core.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaMigrations.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/RabbitMqEventingOptions.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Diten.Platform.Domain.csproj`
- `services/Diten.Platform.Common/src/Diten.Platform.Common/Diten.Platform.Common.csproj`
- `services/Diten.Platform.Contracts/Diten.Platform.Contracts.csproj`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.ModuleRegistration.Abstractions/Diten.BuildingBlocks.ModuleRegistration.Abstractions.csproj`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.BackgroundJobs/Diten.BuildingBlocks.BackgroundJobs.csproj`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Email/Diten.BuildingBlocks.Email.csproj`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.Eventing/Diten.BuildingBlocks.Eventing.csproj`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.InterfaceRegistry.Abstractions/Diten.BuildingBlocks.InterfaceRegistry.Abstractions.csproj`
- `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.ListExport/Diten.BuildingBlocks.ListExport.csproj`
- `services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets/Diten.BuildingBlocks.Security.Secrets.csproj`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Eventing/AuthServiceEventingOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductLegalEntityScopeRolloutMode.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeCandidateFacade.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PermissionAuthorizationHandler.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MdmTestMongo.cs`
- `scripts/run_record.py`
- `docs/guides/operations/control-tower-operating-card.md`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionTransactionWriter.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/ModulePageDescriptorNormalizer.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/DeleteModulePageActionDescriptorCommandHandler.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/ModuleCatalogCodeNormalizer.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/GlobalApplicability/GlobalApplicabilityChangedV1.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/SelfRegistration/WorkAggregationManifestProvider.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ESignature/SelfRegistration/ESignManifestProvider.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/IModuleManifestProvider.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/TenantScope.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/ModulePageActionType.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTenantEntitlementsController.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalModuleRegistrationController.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/TenantModuleEntitlementsController.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/TenantCommercialSubscriptionsController.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/CatalogPermissionSyncService.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/ModulePageActionDescriptorRepository.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ModuleRegistration/RegisterModuleManifestCommandHandlerTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/GetTenantEntitledModulePermissionsQueryHandlerTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/GlobalApplicability/GlobalApplicabilityTestDependencies.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/ProductIdentityLifecycleEntitlementGrantProfile.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/FullCatalogPermissionGrantService.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/AssignPermissionCommandHandler.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Eventing/EntitlementSyncConsumer.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs`
- `services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalEventsController.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/EntitlementPermissionSyncServiceTests.cs`
- `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/S2S/EntitlementAttestationConsumerTests.cs`
- `services/Diten.InventoryService/src/Diten.InventoryService.Api/ModuleRegistration/InventoryLedgerManifestProvider.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductMaster/ProductMasterContract.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Diten.MdmService.Application.csproj`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Diten.MdmService.Domain.csproj`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Diten.MdmService.Infrastructure.csproj`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/HasPermissionAttribute.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Diten.MdmService.Persistence.csproj`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/ApiStartupExecutionMode.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/IModuleManifestProvider.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ModuleRegistrationHostedService.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/LegalEntityManifestProvider.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/BrandProductMasterManifestProvider.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductMasterController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GlobalProductsController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/FinishedGoodsController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/GskusController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LskusController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductAbbreviationsController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductLegalEntityScopesController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ItemsController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/StockBehaviourController.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/CompetitorProductsController.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/CompositionRootGuardTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/CompetitorProducts/CompetitorTestDoubles.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Approvals/WorkflowApprovalCoreTests.cs`
- `.antigravity/rules/module-self-registration-standard.md`
- `.antigravity/rules/permission-key-standard.md`
- `.antigravity/rules/module-pack-standard.md`
- `execution/registries/module-id-registry.md`
- `docs/guides/operations/post-deploy-steps.md`
- `scripts/dev-up.sh`

## 22. Follow-up Items

| # | Konu | Sahip | Regresyon riski |
|---|---|---|---|
| F-1 | BL-573 ikinci yarı: stok davranışı anahtarları (§0.1 #2) | MDM + Auth, ayrı iş | 🟡 bugünkü durum sürer |
| F-2 | Auth: `ValidateExactDeclaredPermissionSet` "Duplicated:" metni fırlatma koşulunda yok; `BL592_An_unknown_item_or_product_master_action_is_refused` okuma anahtarıyla birlikte koşuyor, tek başına bilinmeyen eylem ölçülmüyor (XMC-001) | Auth | 🟢 |
| F-3 | `ACTIVATE_ROLLOUT`, `ROLLBACK_ROLLOUT` `"Operator"`, `PROPOSE` `"Action"` enum'da yok, `Toolbar` saklanıyor | MDM | 🟢 bayraklar `false`; canlı satır okunmadı |
| F-4 | Bu paket standart §2 (eylem = çizilen düğme) ve §2d'den (UI'sız anahtar `ApiPermissions`'a) **bilinçli sapar**. Standarda özel profilli modül istisnası yazılır (`.antigravity`, sahip onayı): bu modüllerde `ApiPermissions` eşitlemeyi durdurur, arayüzsüz eylem kullanılır | Kule | 🟡 yazılmazsa aynı hata tekrarlanabilir |
| F-5 | Platform plan okuması ile Auth eşitlik denetimi arasında bütün özel profil modülleri için uçtan uca test yok; `ProductIdentityDeclaredKeys` kör | Auth + Platform, XMC-001 | 🟡 |
| F-6 | `execution/registries/module-implementation-status.md` satırı, kod gelince | Kule | 🟢 |

## 23. Canlı doğrulama (bu dilimin kabulü değildir)

**Canlı durum ölçülmedi.** Hangi ortamda hangi adımın koştuğu bu pakette bilinmiyor.

**Sıra (post-deploy §7):** **1.** Platform kodu + 38 çift yapılandırması. **2.** Auth ≥ `432c639ac`. **3.** En son MDM dağıtımı: MDM manifesti açılışta iter (`ModuleRegistrationHostedService`, `ApiStartupExecutionMode.AddModuleRegistrationHostedService`). **4.** Pilot kiracı. Eski kopyaların hepsi durmadan pilot başlamaz; son başlayan kopya yeni sürüm olmalıdır (§13).

**Geri alınmaz; yalnız ileri düzeltme.** Bozuk manifest yeni sürümle düzeltilir ya da yeni kopya yeniden başlatılır. Geri dönüş zorunluysa önce Auth satırı kaldırılır:

1. Bütün yeni MDM kopyaları durur (yeniden itme olmasın).
2. `DELETE /internal/permissions/mdm.product-master.read`, Auth 5056, başlık `X-Internal-Api-Key` (`InternalPermissionsController.Delete`). Katalogdan doğan satır `MarkAsUserDefined` olduğu için 409 vermez; **anahtarın bütün rol hakları silinir**, satır yumuşak silinir, `permission_catalog_removed` denetimi yazılır. Platform ekranından yol yok: kodla yönetilen modülde `DeleteModulePageActionDescriptorCommandHandler` 409 döner.
3. Eski MDM başlar, eylem budanır.

Silme verilmiş jetonu geri almaz: MDM izni jetondaki `permission` iddiasından okur (`PermissionAuthorizationHandler`), jeton süresince erişim sürer; süre ortama bağlı, Auth tarafı önbellek etkisi ölçülmedi.

2 ile 3 arasında Platform listesinde var, Auth kataloğunda yok: hak olayları fırlar, tekrar denenir, 3'ten sonra yakınsar. Sonraki ileri düzeltmede satır `reactivated` olur; kiracı hakları pilot adımıyla yeniden kurulur.

**Pilot kiracı (var olan kiracı).** E3 bu yükseltmeyi atılabilir veritabanında ölçer (§18 Y1–Y4); canlının yerine geçmez. Manifest itmesi rol eşitlemesi başlatmaz: `GlobalApplicabilityChangedV1`'in Auth'ta tüketicisi yok. `EntitlementSyncConsumer.ConsumeAsync`'i tetikleyen olaylar ve onları doğuran işlemler:

| Olay | İşlem | Not |
|---|---|---|
| `TenantEntitlementExpiryUpdatedV1` (uzlaştırma) | `PATCH /api/platform/tenants/{tenantId}/commercial/module-entitlements/{entitlementId}/expiry` | **Pilot için bu, yalnız şu koşulda:** `TenantModuleEntitlementActionGate.RefuseStoredAsync` + `TenantModuleEntitlementRowActions.For` (`extendExpiry`): kayıtlı satır (plan satırı değil), `Source` `Addon`, `Trial` ya da `ManualOverride` (`System` değil), modül çekirdek/baseline değil, katalogda; `UpdateTenantModuleEntitlementExpiryCommandValidator`: `ExpiryDateUtc` dolu ve gelecekte, `RowVersion` dolu. **Ticari değer yalnız satırın bitişi zaten gelecekteyse değişmez** (aynı tarih + yeni `Reason`); bitişsiz ya da süresi geçmiş satırda tarih değişir. Tarih ve neden aynıysa 204, olay yok. `PlatformAdminOnly` |
| `TenantSubscriptionChangedV1` (uzlaştırma) | `TenantCommercialSubscriptionsController` eylemleri (`TenantSubscriptionTransactionWriter`) | Koşula uymayan kiracıda ölçülen tek yol; ticari etkisi var: K-3 sahip kararı |
| `TenantEntitlementAddedV1`, `TenantEntitlementEnabledV1`, `TenantEntitlementOverrideRemovedV1` | `TenantModuleEntitlementsController` | Hak durumunu değiştirir; pilot için kullanılmaz |

`refresh-projection` olay üretmez (`RefreshTenantModuleEntitlementProjectionCommandHandler` yalnız 204). **Toplu yol: yok**; her kiracı ayrı işlem ister. Yeni kiracı açılışta eşitlenir (`InternalEventsController.SyncEntitledModulesBestEffortAsync`).

**Denetimler (sırayla):**

- [ ] Platform katalogunda modül `1.4.0`, `GLOBAL_PRODUCTS/READ_PRODUCT_MASTER` var; son başlayan MDM kopyası yeni sürüm.
- [ ] Auth kataloğunda anahtar: `Module = product-item-sku-master`, `Scope = Tenant`, `Resource = product-master`, `Action = read`.
- [ ] Varsayılan kiracı SuperAdmin anahtarı tutar (ilk eşitlemeden).
- [ ] Platform `GET /api/internal/tenants/{tenantId}/entitled-modules-with-permissions` hak olan kiracıda anahtarı listeler.
- [ ] Pilot kiracıda yukarıdaki olay tetiklendi; Auth günlüğünde `entitlement.sync.applied`, `complete active module descriptor set` yok.
- [ ] **Bu adımdan sonra, yeni jetonla:** pilot kiracıda Admin ve Viewer anahtarı tutar; adanmış üç rol tutmaz. İzin jetondan okunur: eski jeton süresi dolana ya da kullanıcı yeniden girene kadar anahtarsız kalır.
- [ ] Ürün modülü hakkı **olmayan** kiracıda Viewer ve Admin ne `mdm.items.read` ne `mdm.product-master.read` tutar (elle atanmadıysa).
- [ ] Enforced kiracıda `/api/product-master/…` ürün satırı döner.
- [ ] Platform MDM kimlik bilgisi 38 çiftle geçerli.

**Stok ekibine "hazır" haberi yalnız bunlardan ve K-3 kapanışından sonra.** Haberde yazılacak (`### BL-592`, "Stok ekibine söz"): hak olan kiracıda varsayılan olarak Admin + Viewer alır; var olan kiracı ancak eşitleme tetiklenince ve kullanıcı yeniden girince alır; varsayılan kiracı SuperAdmin ilk eşitlemede alır; kiracı yöneticisi başka role elle atayabilir; başka rol varsayılan almaz. BL-592 "canlı rol önerisiyle hizalanır" der: `docs/records/analysis/roles/2026-10-09-canli-rol-onerisi.md` bu anahtarı anmıyor, hizalama kule işi (K-5).

## 24. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | izin beyanı, platform yolu testleri ve yerel yığında yükseltme kanıtı | Owned Objects, Repo Scope, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Acceptance Criteria, Test Expectations, Implementation Notes | E3 | plan kimliği yok | plan satırı yok |

DL-1 çekirdek ve tek dilimdir. E3 kartta birikimlidir: §18 otomatik test + sabotaj (AC-1…AC-8) ve taban MDM'den 1.4.0'a yerel yükseltme (AC-9) birlikte istenir. Canlı §23 ayrı kapıdır.

## 25. Açık kararlar

| # | Karar | Kim | Öneri |
|---|---|---|---|
| K-1 | §0.3 yol | Kule | **Kapandı: A** |
| K-2 | Eylemin sayfası | Kule | **Kapandı: `GLOBAL_PRODUCTS`** |
| K-3 | Var olan kiracının eşitlenmesi | **Kule; kapı: stok ekibine haberden önce.** Pilot koşuluna uymayan canlı kiracı (plandan gelen hak, bitişsiz ya da süresi geçmiş satır) için yol: **sahip kararı, açık** | Koşula uyan için pilot; toplu yol yok (§23) |
| K-4 | `target` tarihi, onaylayan, modül PR'ını birleştiren ikinci kişi | Kule | TBD; verilmedi, uydurulmadı |
| K-5 | Stok kullanıcı rolleri + canlı rol önerisi hizalaması | Kule + stok ekibi | Hak olan kiracıda Admin + Viewer; başka rol elle atama |
| K-6 | MDM test projesine Platform başvurusu + Moq | Kule | **Kapandı: kabul.** `EventTransportMessage` dosyası bu başvuruyu üretim için "wrong layer, heavy graph" diye reddeder; burada yalnız test projesinde, takma adla kabul edildi: üretim grafiği değişmez. Derleme maliyeti ölçülmedi |
| K-7 | Registry satırı | Kule | **Kapandı:** kule dalında eklendi |
| K-8 | `ActionType` (§10) | Kule | **Kapandı: `View`** |

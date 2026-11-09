---
id: MOD-0290-FU07
name: Product Identifiers (GTIN)
parent: MOD-0290
parent_name: Product / Item / SKU Master
domain: master-data-management
service: Diten.MdmService + frontend/Diten.Web
shell: tenant
golden_reference: slim
data_mode: server
entity_base: EntityBase
status: draft
runtime_code_allowed: false
runtime_code_scope: "YOK. Kod yalnız bu paket onaylanınca, dilim başına CT prompt'uyla açılır."
owner: module-pack-author
branch: chore/ct/mdm-ct-02
started: 2026-11-10
target: "2026-11-20 (tahmin, sahip takvimi)"
form_field_count: 4
data_classification: "Kiracı içinde ana veri; kişisel veri yok."
audit_class: "Ana veri — yol b (merkezi günlük), FU04 ile aynı sınıf (FU04 künyesi, 'Karar 3a')."
dependencies:
  - MOD-0290 (ebeveyn; GSKU / LSKU / Bitmiş Ürün kümeleri, `product-item-sku-master` manifesti)
  - MOD-0290-FU04 (Kalem kaydı ve Bitmiş Ürün satış kutusu GTIN'i; paket taslak, kod S3'te var)
  - MOD-0290 P9 ambalaj hiyerarşisi (ayrı paket, sahibi MDM; henüz yok — koli / palet düzey kaydı)
  - MOD-0018 (RBAC; manifest → katalog → Auth eşitlemesi, yalnız tüketim)
  - MOD-0021 (merkezi denetim günlüğü, yol b)
---

# MOD-0290-FU07 — Product Identifiers (GTIN)

> **Taslak.** Plan P6 "Tanımlayıcılar" ([`mdm-product-master-completion-plan-2026-10-02.md`](../../../../docs/roadmap/plans/mdm-product-master-completion-plan-2026-10-02.md) §4 "Paketler"). Onay satırı ve prompt kapısı olmadan kod yazılmaz.
>
> **DCP-002 kimlik kapısı — PASS (2026-11-10, HEAD `9db12378a`, bu oturumda yeniden ölçüldü):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0290-FU07 --name "Product Identifiers (GTIN)" --parent MOD-0290`
> → `OK  MOD-0290-FU07: proven against Blueprint/registry.` (çıkış 0).
> `git log --all -S 'MOD-0290-FU07'` ve `git log --all -- '*MOD-0290-FU07*'`: 0 commit.
> `--discover "Product Identifiers GTIN Barcode"`: çıkış 0, "incelenen kaynaklarda eşleşme bulunamadı", bulgu 0 (blueprint · sor · registry · paket · gateway · görev), rapor-sha256 `0cdf07bba888…`. Kayıtlı keşif [`DISCOVERY-mod0290-fu07-p6.txt`](../../../../docs/records/audits/2026-11/mdm-ct-02-dl1-inspection-01/DISCOVERY-mod0290-fu07-p6.txt) (HEAD `d95c4df12`, rapor-sha256 `548c79266bb6…`) ile tek fark ref listesinin özeti (dal başları ilerledi); bulgular aynı. Keşif EKSİK satırları geçerli: başka makinedeki ve kayıtsız işler görülmedi.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

## 0. Kararlar

### 0.1 Sahip kararları (değiştirilmedi)

| # | Tarih | Karar |
|---|---|---|
| S-1 | 2026-11-05 | P6 tahmini bitiş 2026-11-20 (Beste'ye bildirildi). P9 ambalaj hiyerarşisi ayrı paket, sahibi MDM |
| S-2 | 2026-11-05 (Beste kabul etti) | Kapsam: GTIN her ambalaj düzeyinde (kalem, LSKU, satış kutusu, koli, palet), yaşam döngüsüyle; kiracının GS1 şirket öneki |
| S-3 | 2026-11-05 | MDM'de **olmayanlar:** SSCC (00) her palet örneğinde depoda üretilir (MOD-0178 / MOD-0182), MDM yalnız şirket önekini verir; (10) lot ve (17) son kullanma stokta (MOD-0174); barkod dizgesi MOD-0182'de |
| S-4 | 2026-11-05 | P6 / P9 sınırı: P9 düzey hiyerarşisini kurar (kutu → koli → palet, adetler); P6 GTIN'in hangi düzeye bağlandığını tanımlar, hiyerarşiyi kurmaz |
| S-5 | 2026-11-10 | "P6 paketinin yazımına şimdi başla" |

### 0.2 Kaynaklar

| Kaynak | Ne diyor | Bu pakete etkisi |
|---|---|---|
| Tamamlama planı §4 "Paketler", P6 / P9 satırları | P6: GTIN / barkod, yaşam döngüsüyle; P9: P2 ve P6'dan sonra | Kapsam ve sıra |
| Aynı plan §4a "Stok paketi", "GTIN (D-1)" | Satış kutusu GTIN'i gerekli (İTS (01)); koli / palet P9'da | Satış kutusu FU04'te kalır, burada deftere bağlanır |
| [`mod-0290-product-master-requirements-all-mvps-2026-10-09.md`](../../../../docs/roadmap/plans/mod-0290-product-master-requirements-all-mvps-2026-10-09.md) W-04, W-05, D-A | Tarama için GTIN ile SKU bulma (`GET /skus?gtin=`); düzey başına GTIN; "MVP-5'ten önce, P6 + P9 ile" kabul | §16 okuma ekleri |
| [`integration-phase-and-mvp5-start-plan-2026-10-21.md`](../../../../docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md) §4 "MVP-5 kararları" W-7, W-8 | P6 / P9 MOD-0182'den önce şart; MOD-0182 en sona (MOD-0365'e bağlı) | Aciliyet: MOD-0182'den önce, MOD-0178'den önce değil |
| [`MOD-0178-putaway-picking-packing.md`](../../supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md) "Does not own" tablosundaki ürün kimliği / GTIN satırı ve "19.5 Açık sorular" notu | 0178 P6 / P9'a bağımlı değil; "P6 (GTIN) 0178'e gerekmez (MOD-0182)" | Doğrular |
| [`mvp5-wms/CT-QUEUE.tsv`](../../supply-chain-execution/work-packs/mvp5-wms/CT-QUEUE.tsv) | Yalnız MOD-0178 DL-1…DL-8 ve MOD-0179; MOD-0182 satırı yok | Bu paket bugün hiçbir MVP-5 işini durdurmuyor |
| FU04 paketi §4.2, §12, §13, §16, §25 O-6 | FG `Gtin` GTIN-14, GS1 kontrol hanesi, kiracıda emekli olmayan FG'ler arasında tekil; `FG_GTIN_INVALID` 422, `FG_GTIN_CONFLICT` 409; LSKU `gtin` P6'ya kadar null (O-6) | Korunacak davranış |
| MOD-0290 ebeveyn paketi, BL-017 / BL-022 satırları | Koli / palet GTIN'i ve GS1 yaşam döngüsü BL-022'de kaldı | Bu paket BL-022'yi kapatır; BL-017 P9'da |

### 0.3 Bugünkü kod (kod okuması, 2026-11-10, derlenmedi)

- `FinishedGood.Gtin`, `ReleasedGtin`, `GtinCommand` (`Domain/Entities/FinishedGood.cs`); yazan tek komut `AssignFinishedGoodGtinCommand` (`StockBehaviourHandlers.cs`, `AssignFinishedGoodGtinHandler`), uç `PUT /api/finished-goods/{id}/gtin`, izin `StockBehaviourPermissions.EditFinishedGood` = `mdm.finished-goods.create` (O-12 geçici anahtar).
- Kontrol hanesi: `StockBehaviourRules.IsValidGtin14` — tam 14 ASCII hane, GS1 mod-10 (sağdan 3,1,3,1…), dolgu yok.
- Tekillik: `ux_finished_goods_tenant_gtin_live_v1` (`ProductStockBehaviourRepository.EnsureGtinIndexAsync`) — (TenantId, Gtin), kısmi: Draft / PendingIdentityApproval / IdentityApproved. **Emekli FG'nin GTIN'i bugün serbest kalır.** İptal edilen taslak GTIN'i bırakır, değeri `ReleasedGtin`'de kalır (`FinishedGoodRepository`, "S3 FIX1 R2", "S3 FIX2").
- Dolu GTIN taslak olmayan kayıtta kilitli (`StockBehaviourPlan.LockedField` → 409 `STOCK_BEHAVIOUR_FIELD_LOCKED`).
- Okuma: `ProductMasterContract` FG `gtin` yazar; LSKU / Kalem `gtin` null; `ProductMasterReadStore` stoklanabilir FG için dolu GTIN ister.
- Kimlik doğrulayıcıları (`CreateLskuDraftValidator`, `CreateFinishedGoodDraftValidator`, `CreateFirstGskuDraft*Validator`, `ItemValidators`) `Gtin` alanını reddeder; değişmez.
- GS1 şirket öneki: depoda yok (`CompanyPrefix` / "GS1 prefix" araması, MDM ve Platform kaynakları, 0 eşleşme).

## 1. Module Summary

Kiracının ürün tanımlayıcılarını tek defterde tutar: her GTIN bir kayda ve bir ambalaj düzeyine bağlanır, atanır, geri çekilir, yeniden kullanılmaz. Kiracının GS1 şirket önekini tutar; depo SSCC üretirken onu okur. Stok ve depo tüketicileri GTIN'den kayda ulaşır. Kullanıcı: ürün ana veri sorumlusu (yazma), stok / depo kullanıcıları ve servisleri (okuma).

## 2. Ownership and Boundaries

**Kapsamda:** tanımlayıcı defteri (yalnız GTIN türü), düzey bağlama, yaşam döngüsü, GS1 şirket öneki, FG satış kutusu GTIN'inin deftere bağlanması (davranış korunur), okuma sözleşmesine ekleme, ekran (DL-2).

**Kapsam dışı:** SSCC üretimi (MOD-0178 / MOD-0182, S-3) · (10) lot, (17) son kullanma (MOD-0174) · barkod dizgesi, etiket basımı (MOD-0182) · düzey hiyerarşisi ve adetler (P9, S-4) · GSKU GTIN'i (S-2 listesinde yok; FU04 §4.2 "gereksiz") · GTIN'i önekten otomatik üretme (§25 AK-8) · GS1 kayıt sistemine (GEPIR / Verified by GS1) bağlantı · İTS bildirimi.

## 3. Owned Objects

| Tür | Ad | Not |
|---|---|---|
| Entity | `ProductIdentifier` (koleksiyon `mdm_product_identifiers`) | yeni |
| Entity | `Gs1CompanyPrefix` (koleksiyon `mdm_gs1_company_prefixes`) | yeni |
| Komut | `AssignProductIdentifierCommand` · `RetireProductIdentifierCommand` · `WithdrawProductIdentifierCommand` | yeni |
| Komut | `AddGs1CompanyPrefixCommand` · `RetireGs1CompanyPrefixCommand` | yeni |
| Komut (değişir) | `AssignFinishedGoodGtinCommand` | dış davranış aynı; deftere de yazar (§21 N-2) |
| Göç | FG `Gtin` → defter, açılışta, tekrarlanabilir | yeni (§13) |
| Sorgu | `GetProductIdentifierListQuery` · `GetProductIdentifierByIdQuery` · `ResolveGtinQuery` · `GetGs1CompanyPrefixListQuery` | yeni |
| İzin (manifest, DL-2) | `mdm.product-identifiers.read` · `.assign` · `.retire` · `mdm.gs1-company-prefixes.manage` | yeni (§14) |
| Sözleşme eki | `product-master-bundle` §16 | yalnız ekleme |

RCS-001: kayıt kodu yok. GTIN GS1 tanımlayıcısıdır, kayıt kodu değildir (FU04 §22 aynı hüküm); önek kaydının da görünen kodu yok. Sınıf: İKİSİ DE değil.

## 4. Entity Fields

**`ProductIdentifier`** (`EntityBase`: `Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, …)

| Alan | Tür | Kural |
|---|---|---|
| `Gtin` | string(14) | Saklama hep GTIN-14 (§12) |
| `PackagingLevel` | enum | `Item` · `Lsku` · `SalesBox` · `Case` · `Pallet` |
| `TargetKind` / `TargetId` | enum / Guid | Düzeye göre: `Item` → Kalem; `Lsku` → LSKU; `SalesBox`, `Case`, `Pallet` → Bitmiş Ürün |
| `PackagingLevelId` | Guid? | P9 düzey kaydı; P9 gelene kadar null (§25 AK-1) |
| `Origin` | enum | `Own` (kiracının önekinden) · `External` (tedarikçi / lisans veren) |
| `Gs1CompanyPrefixId` | Guid? | `Own` ise eşleşen önek |
| `Status` | enum | `Active` → `Retired` (son) |
| `AssignedAt`, `RetiredAt`, `RetirementReason` | | `RetirementReason` zorunlu, en çok 500 |
| `Version`, `LastCommand` | | İyimser eşzamanlılık + `Idempotency-Key` tekrarı (FU04 deseni) |

Dizinler: tekil (TenantId, Gtin) kısmi `IsDeleted = false` — **`Retired` dizinde kalır** (yeniden kullanım yok); tekil (TenantId, TargetKind, TargetId, PackagingLevel) kısmi `IsDeleted = false` ve `Status = Active` (düzey başına tek etkin GTIN, §25 AK-1).

**`Gs1CompanyPrefix`** (`EntityBase`): `Prefix` (string, yalnız hane), `Status` (`Active` / `Retired`), `ValidFrom`, `RetiredAt`, `Version`. Tekil (TenantId, Prefix) kısmi `IsDeleted = false`. Bir önek başka bir önekin ön eki olamaz (kiracı içinde).

## 5. Repo Scope

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/` — `ProductIdentifier.cs`, `Gs1CompanyPrefix.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/ValueObjects/` — `Gs1Rules.cs` (`IsValidGtin14` buraya taşınmaz; çağrılır, §12)
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/` — iki arayüz
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/` — iki depo + dizin kurucu
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductIdentifiers/` (§10)
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/StockBehaviour/StockBehaviourHandlers.cs` — yalnız `AssignFinishedGoodGtinHandler`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductMaster/` — okuma ekleri (§16)
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductIdentifiersController.cs`, `ProductMasterController.cs` (ek eylemler)
- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs` (DL-2)
- `services/Diten.MdmService/tests/**` — §18
- `frontend/Diten.Web/Views/MasterDataManagement/ProductIdentifiers/**`, `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/ProductIdentifiers/**`, `Views/MasterDataManagement/{FinishedGoods,Lskus,Items}/` altında yalnız ekleme (ayrıntı bölümü), ilgili controller ve 7 dil kaynakları (DL-2)
- `docs/analysis/contracts/product-master-bundle.openapi.yaml` (§16, yalnız ekleme)

## 6. Protected Paths

`.antigravity/**` · `services/Diten.AuthService/**` · `services/Diten.Platform/**` · `services/Diten.InventoryService/**` · `gateway/**` (`ocelot.json` yalnız integration-agent, §16) · `execution/registries/**` (kule) · MOD-0174 / MOD-0178 paketleri. Birine dokunmak gerekirse XMC-001 durağı.

## 7. Dependencies

| Bağımlılık | Kullanım | Durum |
|---|---|---|
| FU04 S3 | FG GTIN alanı, komutu, dizini | kodda var |
| FU04 S1 Kalem | `Item` hedefi | kodda var (`Item.cs`) |
| P9 | Koli / palet düzey kaydı | **yok** — §25 AK-1 |
| Auth özel profili | `ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet`: `mdm.product-identifiers.*` ve `mdm.gs1-company-prefixes.*` hiçbir kümede sayılmıyor, reddedilmiyor (kod okuması). Rol verimi (Admin / Viewer / adanmış roller) **ölçülmedi** | DL-2 önkoşulu, §25 AK-6 |
| Denetim altyapısı | `tests/architecture/audit-ledger/Diten.MdmService.md` `## İzler`: `mdm-merkezi-iletim`, yol b, kabul edilmiş | var |
| MOD-0048 | Yok — düzey bir enum, birim listesi kullanılmaz | — |

## 8. Runtime Constraints

MDM 5059, Gateway 5000, Web 5001; değişmez. Her sorgu ve uç `TenantId` ile sınırlı; başka kiracının kaydı 404. Tekillik yalnız kiracı içindedir: başka kiracıda aynı GTIN'in varlığı denetlenmez ve sızdırılmaz. Silme yalnız yumuşak (`IsDeleted`, `DeletedAt`) ve yalnız `Withdraw` ile (§12). Yeni dizinler açılışta kurulur (FU04 deseni, `ProductStockBehaviourIndexInitializer`).

## 9. Layout & Shell Contract

`shell: tenant`. DL-2 görünümleri `@{ Layout = "_LayoutTenantShell"; }` satırını **açıkça** yazar (`.antigravity/rules/views-organization.md`, tenant satırı; MDM emsali `Views/MasterDataManagement/Items/`). Kabul: AC-12.

## 10. Backend File Convention

Golden Reference slim (`Features/GoldenReferenceSlim/`) birebir:

```text
Features/ProductIdentifiers/
├── Commands/   AssignProductIdentifierCommand · RetireProductIdentifierCommand · WithdrawProductIdentifierCommand
│               AddGs1CompanyPrefixCommand · RetireGs1CompanyPrefixCommand            (sealed record)
├── Queries/    GetProductIdentifierListQuery · GetProductIdentifierByIdQuery · ResolveGtinQuery · GetGs1CompanyPrefixListQuery
├── Handlers/
│   ├── CommandHandlers/  AssignProductIdentifierHandler · RetireProductIdentifierHandler · WithdrawProductIdentifierHandler
│   │                     AddGs1CompanyPrefixHandler · RetireGs1CompanyPrefixHandler
│   └── QueryHandlers/    GetProductIdentifierListHandler · GetProductIdentifierByIdHandler · ResolveGtinHandler
│                         GetGs1CompanyPrefixListHandler
├── Validators/ AssignProductIdentifierValidator · RetireProductIdentifierValidator · AddGs1CompanyPrefixValidator
└── ProductIdentifiersModels.cs
```

Handler / Validator adında `Command` / `Query` eki yok. `Response<T>` zarfı. Toplu silme yok: geri çekme ve emekliye ayırma tek tek, gerekçeli (DataTable `BulkDelete` bilinçli olarak yok; §20).

## 11. Frontend File Contract

Yalnız DL-2. Slim dosya seti, `Views/MasterDataManagement/ProductIdentifiers/`: `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`, `data-dt-data-mode="server"`, iskelet), `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (atama), `_DetailsQuickView.cshtml`, `ProductIdentifiersIndex.cs`; JS `index.js`, `index.l10n.js`. Liste: GTIN, düzey, hedef kodu ve adı, kaynak, durum, atanma tarihi; süzgeç: düzey, durum, kaynak, GTIN / kod araması. GS1 önekleri aynı sayfada ayrı bölüm (küçük liste + ekleme / emekliye ayırma iletişimi). Bitmiş Ürün, LSKU ve Kalem ayrıntısına "Tanımlayıcılar" salt-okunur bölümü (bağlantı bu sayfaya). 7 dil (en, tr, fr, es, zh, ar, ru), menü, Ctrl+K, yetkisiz yüz (UAS-001), sayfa yardımı (§19). `verify_datatable_page.py --area MasterDataManagement --module ProductIdentifiers` yeşil.

## 12. Validation Rules

| Alan | Zorunlu | Kural | Hata |
|---|---|---|---|
| `Gtin` (girdi) | evet | 8, 12, 13 ya da 14 ASCII hane; soldan sıfırla 14'e tamamlanır; `StockBehaviourRules.IsValidGtin14` doğru | 422 `GTIN_INVALID` |
| `Gtin` | evet | Kiracıda başka kayıtta yok (`Retired` dahil) | 409 `GTIN_CONFLICT` |
| `PackagingLevel` | evet | Enum; `SalesBox` bu uçta reddedilir (tek yol FG ucu, §21 N-2) | 422 `GTIN_LEVEL_USE_FINISHED_GOOD_ENDPOINT` |
| `TargetKind` / `TargetId` | evet | Düzeyle uyumlu; kiracıda var; hedef `Retired` değil | 404 `TARGET_NOT_FOUND` · 422 `GTIN_LEVEL_TARGET_MISMATCH` · 409 `TARGET_RETIRED` |
| Düzey başına | — | Hedef + düzeyde etkin GTIN varsa | 409 `GTIN_LEVEL_OCCUPIED` |
| `Origin` | evet | `Own` ise GTIN'in gösterge hanesinden sonraki 13 hanesi kiracının etkin öneklerinden biriyle başlar; `External` ise önek denetlenmez | 422 `GTIN_PREFIX_MISMATCH` |
| `RetirementReason` | emekliye ayırmada | 1–500 karakter | 400 |
| Geri çekme | — | Yalnız hedef `Draft` iken (hiç yayımlanmamış); yumuşak silme, GTIN serbest kalır | 409 `GTIN_WITHDRAW_NOT_DRAFT` |
| `Prefix` | evet | Yalnız hane, 4–12 hane (GS1 Genel Spesifikasyonları; dış kaynak, depoda doğrulanmadı — §25 AK-4); kiracıda tekil; başka etkin önekin ön eki ya da uzantısı değil | 422 `GS1_PREFIX_INVALID` · 409 `GS1_PREFIX_CONFLICT` |
| Önek emekliye ayırma | — | O önekle `Own` etkin GTIN varsa da izin verilir (GTIN'ler geçerli kalır); yeni `Own` ataması o önekle reddedilir | — |
| `ExpectedVersion`, `Idempotency-Key` | evet | FU04 deseni: sürüm uyuşmazlığı 409, aynı anahtar + aynı gövde aynı sonuç | 409 `VERSION_CONFLICT` |

FG ucu (`PUT /api/finished-goods/{id}/gtin`) kendi kurallarını ve kodlarını (`FG_GTIN_INVALID` 422, `FG_GTIN_CONFLICT` 409, `STOCK_BEHAVIOUR_FIELD_LOCKED` 409) aynen korur; tek ek: çakışma denetimi artık defterin bütün düzeylerine bakar (§25 AK-3).

## 13. Failure Path to Verify

| Durum | Beklenen |
|---|---|
| Kontrol hanesi yanlış · 15 hane · harf | 422 `GTIN_INVALID`, yazım yok |
| Aynı GTIN başka düzeyde / başka kayıtta (koli GTIN'i bir FG satış kutusunda) | 409 `GTIN_CONFLICT` |
| Emekli GTIN yeniden atanıyor | 409 `GTIN_CONFLICT` |
| İki eşzamanlı atama, aynı GTIN | Biri 201, öbürü 409 (tekil dizin) |
| Başka kiracının tanımlayıcısı / hedefi | 404 |
| İzinsiz kullanıcı | 403; ekran iskelet çizmez (UAS-001) |
| Eski sürümle yazma | 409 `VERSION_CONFLICT` |
| `Own` + önek yok / eşleşmiyor | 422 `GTIN_PREFIX_MISMATCH` |
| Göç: FG GTIN'i defterde başka kayıtta (emekli FG ile canlı FG aynı GTIN — bugün mümkün, §0.3) | Açılış durmaz; canlı FG `Active` alır, emekli FG satırı `Retired` + işaret; çakışma günlüğe ve sayaç olarak yazılır; ikinci koşu aynı sonucu verir (§25 AK-3) |
| FG ucu: deftere yazım başarılı, FG yazımı başarısız | Aynı `Idempotency-Key` ile tekrar yarım kalanı tamamlar; tutarsız durum okumada görünmez (`gtin` defterden okunur) — §21 N-2 |
| Hedef emekliye ayrıldı | Tanımlayıcı `Active` kalır (piyasadaki ambalaj taranabilir); çözümleme hedefin `lifecycleStatus`'unu döner |
| Denetim günlüğüne iletim başarısız | İş durmaz, uyarı kalır (yol b, FU04 sınıfı) |

## 14. Authorization Convention

```text
Policy:     [Authorize] + [HasPermission(...)] her eylemde
Permission: mdm.product-identifiers.read | .assign | .retire ; mdm.gs1-company-prefixes.manage   // PKS-001, üç parça
Reads:      sözleşme okumaları (§16) mevcut mdm.product-master.read ile; yeni okuma anahtarı yok
FG ucu:     StockBehaviourPermissions.EditFinishedGood (bugünkü anahtar) — değişmez
Actor type: tenant_user; sözleşme okumalarında servis kimliği (mevcut ProductMasterController modeli)
Scope:      Tenant
```

- **Tohum yok.** Anahtarlar Auth'a yalnız manifest → Platform kataloğu → Auth eşitlemesiyle girer (BL-592 CT kararı 2026-10-10).
- **Her anahtar manifestte sayfa ya da eylem;** `ApiPermissions` kullanılmaz: bu modül özel profillidir, `ApiPermissions` plan okumasına girmez ve her hak verme çağrısını kırar (FU06 §0.3, "ApiPermissions katalog eşitlemesine ulaşır, plan okumasına ulaşmaz").
- Manifest (DL-2): yeni sayfa `PRODUCT_IDENTIFIERS` (`RequiredPermission` = `.read`); eylemler `ASSIGN` (Toolbar, `.assign`), `WITHDRAW` (RowAction, `.assign`), `RETIRE` (RowAction, `IsDangerous`, `.retire`), `MANAGE_GS1_PREFIXES` (Toolbar, `mdm.gs1-company-prefixes.manage`). Modül sürümü bir küçük artar.
- DL-1'de anahtarlar zorlanır ama beyan edilmez: rol tutmaz, yazma uçları yalnız testte çalışır. Bilinçli: ekransız sayfa menüde boş sayfa açardı. FG ucu ve okumalar DL-1'de canlı çalışır.
- Rol verimi Auth'ta ölçülmeden DL-2 prompt'u yazılmaz (§25 AK-6).

## 15. Audited Events (AUD-001)

| Komut | Olay | Nesne | Yol | Önce / sonra | İstisna |
|---|---|---|---|---|---|
| `AssignProductIdentifierCommand` | `product-identifier.assigned` | ProductIdentifier | b | Sonra: GTIN, düzey, hedef, kaynak | — |
| `RetireProductIdentifierCommand` | `product-identifier.retired` | ProductIdentifier | b | Önce / sonra: durum; gerekçe | — |
| `WithdrawProductIdentifierCommand` | `product-identifier.withdrawn` | ProductIdentifier | b | Önce: GTIN, hedef | — |
| `AddGs1CompanyPrefixCommand` | `gs1-company-prefix.added` | Gs1CompanyPrefix | b | Sonra: önek | — |
| `RetireGs1CompanyPrefixCommand` | `gs1-company-prefix.retired` | Gs1CompanyPrefix | b | Önce / sonra: durum | — |
| `AssignFinishedGoodGtinCommand` (değişir) | `finished-good.gtin-assigned` (FU04 §21, aynen) | FinishedGood | b | Önce / sonra: GTIN | — |
| Göç (açılış) | `product-identifier.migrated` (kayıt başına) | ProductIdentifier | b | Sonra: GTIN, hedef, durum, çakışma işareti | — |

Kişisel veri, gizli değer yok.

## 16. Gateway / API Routing Decision

- Yazma uçları yeni kök `/api/product-identifiers` ve `/api/gs1-company-prefixes` → **Gateway rotası gerekir**; `ocelot.json`'u yalnız integration-agent değiştirir. Ayrı kuyruk satırı, DL-1'den sonra (emsal `WMS-MVP5-DL8`).
- Sözleşme okumaları mevcut `/api/product-master/{everything}` altında; rota gerekmez. FG ucu `/api/finished-goods/{id}/gtin` var.
- Tarayıcı servise gitmez: aynı kökenli MVC vekili → Gateway.

**Okuma sözleşmesi eki — `product-master-bundle`, yalnız ekleme (stok sözleşmesinden ayrı başlık):**

| Ek | Ne |
|---|---|
| `GET /product-master/identifiers/{gtin}` | Çözümleme (W-04): 8–14 hane kabul, 14'e tamamlanır; `{gtin, packagingLevel, targetKind, targetId, skuLevel, canonicalCode, status, origin, targetLifecycleStatus}`; bilinmeyen 404 `UNKNOWN_GTIN`; geçersiz 400. W-04 metni `GET /skus?gtin=` önerir; tek kayıt döndüğü ve düzeyi taşıdığı için yol önerisi bu (§25 AK-7) |
| `SkuRef.identifiers[]` (isteğe bağlı alan) | `getSku` yanıtında kaydın bütün tanımlayıcıları: `{gtin, packagingLevel, status, origin}` |
| `GET /product-master/gs1-company-prefixes` | Kiracının etkin önekleri; SSCC üretimi için (MOD-0178 / MOD-0182) |
| `gtin` (v1.0 alanı) | **Anlamı değişmez:** FG'de satış kutusu GTIN'i; LSKU ve Kalem'de null kalır. LSKU / Kalem GTIN'i yalnız `identifiers[]`'ta |

**Stok tüketicisi etkisi:** MOD-0174 `gs1-check` SKU'nun `gtin`'i null iken GTIN denetimini uyarıyla atlar (`MOD-0174-lot-serial-tracking.md` K-B, §7 "PRODUCT-MASTER `gtin`" satırı). `gtin` anlamı değişmediği için o davranış sessizce değişmez. LSKU `gtin`'inin dolması stok ekibine haberli ayrı bir karardır (§25 AK-2). Bilinmeyen alan hata değildir (sözleşmenin açık enum / ek alan kuralı, `info.x-notes`).

## 17. Acceptance Criteria

- [ ] AC-1 (zorunlu) — DL-1: `Item`, `Lsku`, `Case`, `Pallet` düzeylerinde atama 201; GTIN-14 saklanır; 8 / 12 / 13 haneli girdi soldan sıfırla tamamlanır; yanlış kontrol hanesi 422 `GTIN_INVALID`.
- [ ] AC-2 (zorunlu) — DL-1: aynı GTIN kiracıda ikinci kez (başka düzey, başka hedef ya da `Retired`) 409 `GTIN_CONFLICT`; eşzamanlı iki atamada tam biri başarılı.
- [ ] AC-3 (zorunlu) — DL-1: başka kiracının tanımlayıcısı, hedefi ve çözümlemesi 404; başka kiracıdaki aynı GTIN atamayı engellemez.
- [ ] AC-4 (zorunlu) — DL-1: emekliye ayırma gerekçesiz 400, gerekçeli 200; emekli GTIN çözümlemede `status: Retired` döner; geri çekme yalnız `Draft` hedefte, yumuşak siler ve GTIN'i serbest bırakır.
- [ ] AC-5 (zorunlu) — DL-1: `Own` atama kiracının etkin önekiyle başlamıyorsa 422 `GTIN_PREFIX_MISMATCH`; `External` denetlenmez; önek eklemede 422 / 409 kuralları §12.
- [ ] AC-6 (zorunlu) — DL-1: FG ucu bugünkü testleriyle yeşil (`FG_GTIN_INVALID`, `FG_GTIN_CONFLICT`, `STOCK_BEHAVIOUR_FIELD_LOCKED`, iptal edilen taslağın GTIN'i bırakması); ek olarak defterde koli düzeyinde duran GTIN FG ucunda 409 `FG_GTIN_CONFLICT`.
- [ ] AC-7 (zorunlu) — DL-1: göç ikinci koşuda değişiklik üretmez; §13 çakışma satırı test edilir; açılış durmaz.
- [ ] AC-8 (zorunlu) — DL-1: `GET /product-master/identifiers/{gtin}`, `identifiers[]` ve önek okuması sözleşme dosyasındaki şemaya uyar; FG `gtin` değeri ve LSKU / Kalem `gtin: null` değişmez (mevcut S4 testleri yeşil).
- [ ] AC-9 (zorunlu) — DL-1: §15'teki her komut tam bir denetim niyeti bırakır; olay adları tablodakiyle aynı.
- [ ] AC-10 (zorunlu) — DL-1: §18 sabotajlarının her biri adı geçen testte kırmızı, geri alınca yeşil; `git status --porcelain` boş.
- [ ] AC-11 (zorunlu) — DL-2: manifestte §14 sayfa ve eylemleri; `ApiPermissions` boş; modül hakkı olan kiracıda rol verimi §25 AK-6 kararıyla aynı (yerel yığında ölçülür).
- [ ] AC-12 (zorunlu) — DL-2: görünümler `Layout = "_LayoutTenantShell"` yazar; `verify_datatable_page.py` sapma 0; 7 dilde anahtar eşitliği; izinsiz kullanıcıya iskelet çizilmez.
- [ ] AC-13 (zorunlu) — DL-2: FG, LSKU, Kalem ayrıntısında "Tanımlayıcılar" bölümü kaydın etkin ve emekli GTIN'lerini gösterir.
- [ ] AC-14 (isteğe bağlı) — DL-2: sayfa yardımı ve tur (§19).

## 18. Test Expectations

Üretim koduyla, gerçek Mongo ile HTTP testleri (FU04 S3 / S4 deseni); test çifti yalnız denetim iletimi için.

| Test | Ölçer |
|---|---|
| `Gs1RulesTests` | Tamamlama (8/12/13/14), kontrol hanesi (mevcut `IsValidGtin14`'e çağrı), önek eşleşmesi gösterge hanesinden sonra, önek ön-ek çakışması |
| `ProductIdentifierHttpTests` | AC-1…AC-5; kiracı yalıtımı; sürüm ve `Idempotency-Key` tekrarı |
| `FinishedGoodGtinRegistryHttpTests` | AC-6 |
| `ProductIdentifierMigrationTests` | AC-7, çakışma örneği dahil |
| `ProductMasterIdentifierContractTests` | AC-8, şemaya karşı |
| `ProductIdentifierAuditTests` | AC-9 |

Sabotajlar (her biri adlı testte kırmızı): (1) tekil dizinden `Retired`'ı çıkar → AC-2; (2) kiracı süzgecini kaldır → AC-3; (3) tamamlamayı kaldır → AC-1; (4) FG ucunda defter denetimini kaldır → AC-6; (5) göçte "zaten var" denetimini kaldır → AC-7; (6) LSKU `gtin`'ini defterden doldur → AC-8.

## 19. Page Help (UXH-001)

DL-2: sayfa yardımı 7 dilde — GTIN nedir, düzeyler, `Own` / `External`, emekli GTIN'in neden yeniden kullanılmadığı, SSCC'nin depoda üretildiği. Tur isteğe bağlı (AC-14).

## 20. Ready-for-dev Checklist

- [x] DCP-002 kimlik kapısı (2026-11-10)
- [x] Künye zorunlu alanları; `golden_reference: slim`, `data_mode: server`, `form_field_count: 4` (hedef, düzey, GTIN, kaynak)
- [x] Layout: `_LayoutTenantShell` açıkça (§9)
- [x] Validation Rules, Failure Path, Authorization, Gateway kararı, Audited Events
- [x] Teslim dilimleri tablosu, kanıt seviyesi, plan sütunları
- [ ] §25 AK-1, AK-2, AK-3: DL-1 prompt'undan önce sahip kararı
- [ ] §25 AK-6: DL-2 prompt'undan önce Auth ölçümü
- [ ] Toplu silme yokluğu: Golden Reference'tan bilinçli sapma (tanımlayıcı gerekçesiz silinmez); kule onayı
- [ ] Gateway kuyruk satırı (integration-agent)
- [ ] Bağımsız inceleme ve onay satırı (kule)

## 21. Implementation Notes

- **N-1 Dilim önerisi: iki dilim.** DL-1 arka uç (defter, önek, FG bağlama, göç, sözleşme eki): MOD-0182 ve MOD-0174'ün ihtiyacı yalnız buradadır ve E4 kanıtını ekransız verebilir. DL-2 ekran + manifest + 7 dil: Auth ölçümüne (AK-6) bağlıdır, DL-1'i bekletmemelidir. Tek dilim FU04 deneyimine göre bir prompt'a sığmaz (FU04 S3 + S5 ayrı dilimlerdi) ve AK-6 belirsizliği arka ucu da durdururdu. Koli / palet için üçüncü dilim önerilmez: AK-1 (b) seçilirse DL-1'de, (a) seçilirse P9'un işidir.
- **N-2 FG satış kutusu: defter tek kaynak.** `PUT /api/finished-goods/{id}/gtin` dış sözleşmesi (yol, gövde, izin, hata kodları, kilit, iptal edilen taslağın bırakması) değişmez. İşleyici önce defteri (tekil dizin bütün düzeyleri korur), sonra `FinishedGood.Gtin`'i yazar; okuma defterden. Mongo çoklu belge işlemi MDM'de kullanılıyor mu ölçülmedi; kullanılmıyorsa yarım yazım `Idempotency-Key` tekrarıyla tamamlanır ve göç / tutarlılık testi bunu yakalar. `FinishedGood.Gtin` ve `ux_finished_goods_tenant_gtin_live_v1` bu pakette kaldırılmaz (geri dönüş yolu).
- **N-3 Koli / palet — P9 düzey kaydı gelene kadar (öneri, açık karar AK-1).** Seçenekler: **(a)** P9'u bekle: `Case` / `Pallet` DL-1'de reddedilir. 🟢 risk yok; MOD-0182 P9'u bekler. **(b) Önerilen:** geçici bağ — hedef Bitmiş Ürün + düzey, `PackagingLevelId = null`, hedef + düzey başına tek etkin GTIN. P9 düzey kaydını açınca tek seferlik "sahiplenme" komutuyla `PackagingLevelId`'yi doldurur (P9'un komutu, P9 paketinde); GTIN ve geçmişi değişmez. Aynı FG'nin iki farklı koli yapılandırması (12'li, 24'lü) P9'dan önce tutulamaz: 🟡 gerekirse P9'da yeni GTIN. **(c)** SAP tarzı birim başına bağ (FG + `XCS`): koli için FG'nin koli ↔ kutu çevrimi var, palet birimi `item-uom` listesinde yok (FU04 §16.2 "kutu `XBX`, koli `XCS`, rulo `XRO`"); MOD-0048 listesine dokunur, XMC-001.
- **N-4 Göç:** açılışta, kiracı başına, `TenantId + FinishedGoodId + SalesBox` anahtarıyla tekrarlanabilir; dizin kurucudan sonra.
- **N-5 SAP / Oracle:** SAP malzeme ana kaydında ölçü birimi başına EAN / UPC (birim başına ek EAN'lar), şirket önekinden numara aralığıyla iç atama. Oracle kalem + birim GTIN çapraz başvurusu. Bu paket ikisinin ortası: GTIN kayıt + düzeye bağlanır (Oracle'a yakın), düzey P9'da birime eşlenir (SAP'ye yakın); otomatik atama yok (AK-8).
- **N-6 Plan:** plan satırı bu pakete verilmedi; dilim sütunlarında `plan kimliği yok` / `plan satırı yok`. P6 tamamlama planı §4'tendir; MVP-5 ilişkisi W-7.
- Dilim tablosu `## 24. Teslim dilimleri` başlığı altında (`scripts/pack_approval.py` `SLICE_HEADING`).
- Okunacak kurallar: `.antigravity/rules/module-pack-standard.md`, `handler-design.md`, `erp-architecture.md`, `response-envelope.md`, `multi-tenancy`, `localization-standard`, `permission-key-standard.md`, `audit-trail-standard.md`.

## 22. Follow-up Items

| # | Konu | Sahip | Regresyon riski |
|---|---|---|---|
| F-1 | P9 ambalaj hiyerarşisi paketi: düzey kaydı + AK-1 (b) seçilirse sahiplenme komutu | MDM | 🟡 P9 yoksa koli / palet düzeyi geçici kalır |
| F-2 | MOD-0182 paketi: SSCC üretimi önek okumasıyla, tarama `identifiers/{gtin}` ile | MVP-5 CT | 🟢 |
| F-3 | Stok ekibine haber: `identifiers[]` ve çözümleme; LSKU `gtin` kararı (AK-2) | Kule | 🟡 haber verilmezse gs1-check LSKU'da atlamaya devam eder |
| F-4 | `FinishedGood.Gtin` alanının ve eski dizinin kaldırılması (defter oturduktan sonra) | MDM | 🟡 ayrı paket |
| F-5 | O-12: FG ucunun geçici anahtarı (`mdm.finished-goods.create`) — BL-573 ikinci yarı | MDM + Auth | 🟢 bugünkü durum |
| F-6 | `execution/registries/module-implementation-status.md` satırı, kod gelince | Kule | 🟢 |

## 23. Modül platform bağlantıları (DL-2)

Menü (MDM → Ürün ana verisi → Tanımlayıcılar) · Ctrl+K · manifest sayfası · 7 dil · yetkisiz yüz · sayfa yardımı · FG / LSKU / Kalem ayrıntı bağlantısı · Gateway rotası · denetim günlüğü · plan hakkıyla rol verimi (AK-6). Satır başına durum DL-2 prompt'unda ölçülür.

## 24. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | tanımlayıcı defteri, GS1 öneki, FG satış kutusu bağı, göç ve okuma sözleşmesi eki (arka uç) | Owned Objects, Entity Fields, Repo Scope, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events (AUD-001), Gateway / API Routing Decision, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | Tanımlayıcılar ekranı, ayrıntı bölümleri, manifest beyanı ve 7 dil | Layout & Shell Contract, Frontend File Contract, Authorization Convention, Page Help (UXH-001), Acceptance Criteria | E4 | plan kimliği yok | plan satırı yok |

DL-1 çekirdektir. DL-2 DL-1'e dayanır.

## 25. Açık kararlar

| # | Karar | Kim | Öneri |
|---|---|---|---|
| AK-1 | Koli / palet GTIN'i P9 düzey kaydı gelene kadar nasıl tutulur (S-4 sınırı) | Sahip | **(b) geçici bağ**, P9 sahiplenir (§21 N-3) |
| AK-2 | LSKU GTIN'inin anlamı: FG satış kutusu GTIN'iyle aynı ticari ürün mü? (LSKU ile FG arasında doğrudan bağ yok, BL-018.) LSKU `gtin` sözleşme alanı dolsun mu? | Sahip + stok ekibi | LSKU kendi GTIN'ini `Lsku` düzeyinde alır, `gtin` alanı null kalır; dolması ayrı, haberli karar |
| AK-3 | Emekli FG'nin GTIN'i: bugün serbest kalıyor; bu paketle yeniden kullanılmaz (GS1 GTIN Yönetim Standardı — dış kaynak, depoda doğrulanmadı). Göçteki çakışmalar | Sahip | Yeniden kullanım yok; iptal edilen taslağınki serbest (bugünkü gibi). 🟡 bugün izin verilen bir yeniden atama reddedilir |
| AK-4 | GS1 önek uzunluk aralığı (4–12) ve tek mi çok mu önek | Sahip | Çok önek, kiracı başına liste |
| AK-5 | Önek kiracıya mı, tüzel kişiye mi bağlı (GS1 öneki şirkete lisanslanır)? | Sahip | v1 kiracı (S-2); tüzel kişi alanı sonra eklenebilir |
| AK-6 | Yeni anahtarların Auth rol verimi (Admin / Viewer / `ProductDataSteward` …) ve gerekirse Auth değişikliği (XMC-001) | Kule + Auth sahibi | DL-2'den önce kod okuması + yerel ölçüm; Viewer yalnız `.read` |
| AK-7 | Çözümleme yolu: `GET /product-master/identifiers/{gtin}` mi, W-04'teki `GET /skus?gtin=` mi | Kule + MVP-5 CT | `identifiers/{gtin}` |
| AK-8 | GTIN'i önekten otomatik üretme (SAP iç numara aralığı gibi) | Sahip | v1'de yok, elle giriş |
| AK-9 | Sözleşme sürümü: v1.1 taslağına mı, ayrı v1.2'ye mi | Kule | Ayrı v1.2 (stok ekibinin v1.1'i karışmasın) |
| AK-10 | `target` tarihi, onaylayan ve modül PR'ını birleştiren ikinci kişi | Kule | TBD; verilmedi, uydurulmadı (künyedeki tarih sahibin tahmini) |

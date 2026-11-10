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
status: ready-for-dev
runtime_code_allowed: true
runtime_code_scope: "Yalniz DL-1 ve DL-2, dilim basina; onay satiri ve prompt kapisi olmadan dagitilmaz."
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

> Plan P6 "Tanımlayıcılar" ([`mdm-product-master-completion-plan-2026-10-02.md`](../../../../docs/roadmap/plans/mdm-product-master-completion-plan-2026-10-02.md) §4 "Paketler"). Onay satırı ve prompt kapısı olmadan kod yazılmaz.
>
> **DCP-002 kimlik kapısı — PASS (2026-11-10, HEAD `9db12378a`):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0290-FU07 --name "Product Identifiers (GTIN)" --parent MOD-0290`
> → `OK  MOD-0290-FU07: proven against Blueprint/registry.` (çıkış 0).
> `git log --all -S 'MOD-0290-FU07'` ve `git log --all -- '*MOD-0290-FU07*'`: 0 commit.
> `--discover "Product Identifiers GTIN Barcode"`: çıkış 0, bulgu 0 (blueprint · sor · registry · paket · gateway · görev), rapor-sha256 `0cdf07bba888…`. Kayıtlı keşif [`DISCOVERY-mod0290-fu07-p6.txt`](../../../../docs/records/audits/2026-11/mdm-ct-02-dl1-inspection-01/DISCOVERY-mod0290-fu07-p6.txt) (HEAD `d95c4df12`, rapor-sha256 `548c79266bb6…`) ile tek fark ref listesinin özeti; bulgular aynı. Keşif EKSİK satırları geçerli.
>
> **Değişen bölümler:** 2026-11-10 (`0d271a2e6` sonrası): §0.4 kapanan kararlar. 2026-11-10 inceleme turu 1 (KOŞULLU, 3 ENGEL / 9 ORTA / 5 DÜŞÜK): §0.4 K-11…K-14 ve AK-9'un yerine geçen K-13; §2, §3, §5, §8, §10, §12–§18, §20–§25 düzeltildi; §23 "Canlı sıra ve geri dönüş" eklendi. Kapsam ve dilim sayısı değişmedi.
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
| Tamamlama planı §4 "Paketler", P6 / P9 satırları; §4a "Stok paketi", "GTIN (D-1)" | P6 GTIN yaşam döngüsü; P9 P6'dan sonra; satış kutusu GTIN'i gerekli (İTS (01)) | Kapsam, sıra |
| [`mod-0290-product-master-requirements-all-mvps-2026-10-09.md`](../../../../docs/roadmap/plans/mod-0290-product-master-requirements-all-mvps-2026-10-09.md) W-04, W-05, D-A; MOD-0176 / 0177 paragrafı | GTIN ile SKU bulma; düzey başına GTIN; "MVP-5'ten önce, P6 + P9 ile"; "recall per market uses `gtin` + `marketCode`" | §16 |
| [`integration-phase-and-mvp5-start-plan-2026-10-21.md`](../../../../docs/roadmap/plans/integration-phase-and-mvp5-start-plan-2026-10-21.md) §4 "MVP-5 kararları" W-7, W-8 | P6 / P9 MOD-0182'den önce şart; MOD-0182 en sona | Aciliyet |
| [`MOD-0178-putaway-picking-packing.md`](../../supply-chain-execution/module-packs/MOD-0178-putaway-picking-packing.md) "Does not own" ürün kimliği satırı, "19.5 Açık sorular" | "P6 (GTIN) 0178'e gerekmez (MOD-0182)" | MOD-0178'i durdurmaz |
| [`mvp5-wms/CT-QUEUE.tsv`](../../supply-chain-execution/work-packs/mvp5-wms/CT-QUEUE.tsv) | MOD-0182 satırı yok | Bugün hiçbir MVP-5 işini durdurmuyor |
| FU04 paketi §4.2, §12, §13, §16, §25 O-6 | FG `Gtin` GTIN-14, tekil; `FG_GTIN_INVALID` 422, `FG_GTIN_CONFLICT` 409; LSKU `gtin` P6'ya kadar null | Korunan davranış (tek değişiklik K-3) |
| MOD-0290 ebeveyn paketi, BL-017 / BL-022 | Koli / palet GTIN'i ve GS1 yaşam döngüsü BL-022'de | BL-022'yi kapatır; BL-017 P9'da |

### 0.3 Bugünkü kod (kod okuması, 2026-11-10, derlenmedi)

- `FinishedGood.Gtin`, `ReleasedGtin`, `GtinCommand`; tek yazıcı `AssignFinishedGoodGtinCommand` (`StockBehaviourHandlers.cs`), uç `PUT /api/finished-goods/{id}/gtin`, izin `StockBehaviourPermissions.EditFinishedGood` = `mdm.finished-goods.create` (O-12 geçici). Komut çitli doğar (`ProductLegalEntityScopeWriteAdmissionContractTests.ExactMutationCommands`, "S3 … born fenced"); kapsam `StockBehaviourContext.ResolveGskuAsync` → `ProductLegalEntityScopeConsumerGuard`.
- Kontrol hanesi `StockBehaviourRules.IsValidGtin14`: tam 14 ASCII hane, GS1 mod-10, dolgu yok.
- Tekillik `ux_finished_goods_tenant_gtin_live_v1`: (TenantId, Gtin), yalnız Draft / PendingIdentityApproval / IdentityApproved. **Emekli FG'nin GTIN'i bugün serbest** (`A_gtin_held_by_another_live_finished_good_is_refused_and_a_retired_ones_is_free`). İptal edilen taslak GTIN'i bırakır, `ReleasedGtin`'de tutar (`FinishedGoodRepository.CancelDraftAsync`).
- Dolu GTIN taslak olmayan kayıtta kilitli (409 `STOCK_BEHAVIOUR_FIELD_LOCKED`); taslakta değiştirilebilir.
- Mongo işlemi MDM'de kullanılıyor: `FinishedGoodRepository` (snapshot / majority işlem, `UnknownTransactionCommitResult` yeniden kesinleştirme), `MasterDataAtomicWrite`, `ProductLegalEntityScopeGuardedWriteSession`; denetim defteri `tests/architecture/audit-ledger/Diten.MdmService.md` `mdm-guvenilir-kaynak-niyeti` satırı ("aynı Mongo işleminde"). Testler kendi replica set'ini açar (`MdmTestMongo`, `--replSet mdm-owned`; CI `service-tests.yml` "every test owns its mongod").
- Okuma: `ProductMasterContract` FG `gtin` yazar; LSKU / Kalem `gtin` null; `ProductMasterReadStore` stoklanabilir FG için dolu `Gtin` ister.
- GS1 şirket öneki: depoda yok (0 eşleşme).

### 0.4 Kararlar (2026-11-10, kule sohbeti)

Sahip kararları butonlarla; kule kararları sahibin bıraktığı yetkiyle.

| # | Karar | Veren | Pakete etkisi |
|---|---|---|---|
| AK-1 | **Geçici bağ:** koli / palet GTIN'i Bitmiş Ürün + düzey adına, `PackagingLevelId` null; P9 sahiplenir | Sahip | §4, §21 N-3 |
| AK-2 | LSKU kendi GTIN'ini `Lsku` düzeyinde alır; sözleşmedeki `gtin` null kalır, yeni GTIN'ler `identifiers[]`'ta | Sahip | §16 (kalıcılığı AK-11) |
| AK-3 | Emekli ürünün GTIN'i hiç yeniden verilmez; iptal edilen taslağınki serbest; göç çakışmaları raporlanır | Sahip | §4, §12, §13 |
| AK-4 / AK-5 | Önek kiracıya bağlı, birden çok önek listesi; tüzel kişi alanı sonra eklenebilir. Uzunluk 4–12 dış kaynak işaretiyle | Sahip | §4, §12 |
| AK-6 | Anahtarlar DL-2'de manifestte; rol verimi DL-2 öncesi ölçülür; Auth değişikliği gerekirse XMC-001 | Kule | §14 |
| AK-7 | Çözümleme `GET /product-master/identifiers/{gtin}` | Kule | §16 |
| AK-8 | v1'de otomatik GTIN üretimi yok | Kule | §2 |
| ~~AK-9~~ | ~~Ayrı v1.2~~ — **K-13'le değişti** | Kule | — |
| GR-1 | Toplu silme yok (Golden Reference sapması kabul): ana veri, tek tek emeklilik. Yumuşak silme yalnız taslak hedefte geri çekme (AK-3'ün "taslağınki serbest" yolu) | Kule | §10, §12 |
| K-11 | **Göç:** çakışmada yalnız canlı FG'ye `Active` satır; canlı yoksa en son emekliye alınan FG'ye `Retired` satır; diğer emekli FG'ler satır almaz, göç raporunda | Kule | §13, AC-7 |
| K-12 | **FG ucu tek Mongo işlemi:** defter + FG aynı işlemde (§0.3 kaynakları) | Kule | §13, §21 N-2, AC-6 |
| K-13 | `identifiers[]` ve yeni okumalar mevcut **v1.1 taslağına** (dondurulmamış); v1.2 BL-584'e ayrılmış | Kule | §16 |
| K-14 | **Tüzel kişi kapsamı:** FG / LSKU hedefli her uç `ProductLegalEntityScopeConsumerGuard` ile, kapsam dışı 404; yeni yazıcılar çitli, `KnownUnguardedWriters`'a girmez. Kalem kiracı geneli (FU04 Karar 6) | Kule | §8, §10, §12, §13 |
| AK-10 | `target`, onaylayan, ikinci kişi | — | **TBD**; tarih "2026-11-20 (tahmin)" |

## 1. Module Summary

Kiracının ürün tanımlayıcılarını tek defterde tutar: her GTIN bir kayda ve bir ambalaj düzeyine bağlanır, atanır, geri çekilir ya da emekliye ayrılır, yeniden kullanılmaz. Kiracının GS1 şirket öneklerini tutar; depo SSCC üretirken okur. Stok ve depo tüketicileri GTIN'den kayda ulaşır. Kullanıcı: ürün ana veri sorumlusu (yazma), stok / depo kullanıcıları ve servisleri (okuma).

## 2. Ownership and Boundaries

**Kapsamda:** tanımlayıcı defteri (yalnız GTIN), düzey bağlama, yaşam döngüsü, GS1 önekleri, FG satış kutusu GTIN'inin deftere bağlanması (FG ucunun dış sözleşmesi aynı; tek davranış değişikliği emekli GTIN'in serbest kalmaması, AK-3), okuma sözleşmesine ekleme, ekran (DL-2).

**Kapsam dışı:** SSCC üretimi (S-3) · (10) lot, (17) son kullanma (MOD-0174) · barkod dizgesi, etiket (MOD-0182) · düzey hiyerarşisi ve adetler (P9) · GSKU GTIN'i (S-2'de yok) · otomatik GTIN üretimi (AK-8) · GS1 kayıt sistemi bağlantısı · İTS bildirimi.

## 3. Owned Objects

| Tür | Ad | |
|---|---|---|
| Entity | `ProductIdentifier` (`mdm_product_identifiers`) | yeni |
| Entity | `Gs1CompanyPrefix` (`mdm_gs1_company_prefixes`) | yeni |
| Komut | `AssignProductIdentifierCommand` · `RetireProductIdentifierCommand` · `WithdrawProductIdentifierCommand` | yeni, çitli |
| Komut | `AddGs1CompanyPrefixCommand` · `RetireGs1CompanyPrefixCommand` | yeni, kiracı geneli |
| Komut | `AssignFinishedGoodGtinCommand` | değişir: defter + FG tek işlem (K-12) |
| Komut | `CancelFinishedGoodDraftCommand` | değişir: aynı işlemde defter satırını geri çeker |
| Göç | FG `Gtin` → defter, açılışta, tekrarlanabilir, sistem aktörü | yeni (K-11) |
| Sorgu | `GetProductIdentifierListQuery` · `GetProductIdentifierByIdQuery` · `ResolveGtinQuery` · `GetGs1CompanyPrefixListQuery` | yeni |
| İzin (DL-2 manifest) | `mdm.product-identifiers.read` · `.assign` · `.retire` · `mdm.gs1-company-prefixes.manage` | yeni |
| Sözleşme eki | `product-master-bundle` v1.1 taslağı (K-13) | yalnız ekleme |

RCS-001: kayıt kodu yok; GTIN GS1 tanımlayıcısıdır (FU04 §22). Sınıf: İKİSİ DE değil.

## 4. Entity Fields

**`ProductIdentifier`** (`EntityBase`)

| Alan | Tür | Kural |
|---|---|---|
| `Gtin` | string(14) | Hep GTIN-14 |
| `PackagingLevel` | enum | `Item` · `Lsku` · `SalesBox` · `Case` · `Pallet` |
| `TargetKind` / `TargetId` | enum / Guid | `Item` → Kalem; `Lsku` → LSKU; `SalesBox`, `Case`, `Pallet` → Bitmiş Ürün |
| `PackagingLevelId` | Guid? | P9 düzey kaydı; P9'a kadar null (AK-1) |
| `Origin` | enum | `Own` · `External` |
| `Gs1CompanyPrefixId` | Guid? | `Own` ise eşleşen önek |
| `Status` | enum | `Active` → `Retired` (son) |
| `AssignedAt`, `RetiredAt`, `RetirementReason`, `MigrationNote` | | `MigrationNote` yalnız göç satırında |
| `Version`, `LastCommand` | | İyimser eşzamanlılık + `Idempotency-Key` (FU04 deseni) |

Dizinler: tekil (TenantId, Gtin) kısmi `IsDeleted = false` — `Active` ve `Retired` birlikte, GTIN başına tek satır, yeniden kullanım yok; tekil (TenantId, TargetKind, TargetId, PackagingLevel) kısmi `IsDeleted = false` + `Status = Active`.

**`Gs1CompanyPrefix`** (`EntityBase`): `Prefix`, `Status` (`Active` / `Retired`), `ValidFrom`, `RetiredAt`, `Version`. Tekil (TenantId, Prefix) kısmi `IsDeleted = false`.

## 5. Repo Scope

Tam dosya listesi §21 N-7. Özet:

- MDM Domain / Persistence: iki entity, iki depo, dizin kurucu, göç (yeni); `FinishedGoodRepository.cs`, `ProductStockBehaviourRepository.cs` (işleme defter yazımı), `ProductMasterReadStore.cs` (`identifiers[]` ve çözümleme okuması) (değişir).
- MDM Application: `Features/ProductItemSkuMaster/ProductIdentifiers/`, `Features/Gs1CompanyPrefixes/` (yeni); `StockBehaviourHandlers.cs` (`AssignFinishedGoodGtinHandler`), `CancelFinishedGoodDraftHandler.cs`, `ProductMasterContract.cs` (değişir).
- MDM Api: `ProductIdentifiersController.cs`, `Gs1CompanyPrefixesController.cs` (yeni); `ProductMasterController.cs` (ek eylemler); manifest (DL-2).
- MDM testleri (§18), DL-2 ön yüzü (§11).
- `docs/analysis/contracts/product-master-bundle.openapi.yaml`: v1.1 taslağına ekler + metin düzeltmesi (`info.x-notes` "P6'ya kadar null" cümlesi ve `SkuRef` tablo satırı, §16).

## 6. Protected Paths

`.antigravity/**` · `services/Diten.AuthService/**` · `services/Diten.Platform/**` · `services/Diten.InventoryService/**` · `gateway/**` (`ocelot.json` yalnız integration-agent) · `execution/registries/**` · MOD-0174 / MOD-0177 / MOD-0178 paketleri ve `docs/analysis/contracts/trace.openapi.yaml`. Birine dokunmak gerekirse XMC-001.

## 7. Dependencies

| Bağımlılık | Kullanım | Durum |
|---|---|---|
| FU04 S1 / S3 | Kalem hedefi; FG GTIN alanı, komutu, dizini | kodda var |
| P9 | Koli / palet düzey kaydı | yok — geçici bağ (AK-1) |
| Tüzel kişi kapsamı | `ProductLegalEntityScopeConsumerGuard`, yazma çiti (`IProductLegalEntityScopeInventoryMutation`) | kodda var |
| Auth özel profili | `ValidateExactDeclaredPermissionSet`: yeni kaynaklar hiçbir kümede sayılmıyor, reddedilmiyor (kod okuması); rol verimi ölçülmedi | DL-2 önkoşulu (§14) |
| Denetim | `mdm-merkezi-iletim`, yol b, kabul edilmiş | var |
| Gateway | `/api/product-identifiers`, `/api/gs1-company-prefixes` | integration-agent satırı (§16) |

## 8. Runtime Constraints

MDM 5059, Gateway 5000, Web 5001. Her sorgu ve uç `TenantId` ile; başka kiracı 404. **FG / LSKU hedefli her uç** (atama, emekli, geri çekme, liste, ById, çözümleme) hedefin GSKU'su üzerinden `ProductLegalEntityScopeConsumerGuard` ile sınırlanır (`StockBehaviourContext.ResolveGskuAsync` deseni); kapsam dışı 404, listede görünmez ve sayılmaz. **Kalem hedefli satırlar ve önekler kiracı genelidir** (FU04 Karar 6). Tekillik yalnız kiracı içinde; başka kiracı denetlenmez, sızdırılmaz. Silme yalnız yumuşak, yalnız taslak hedefte geri çekmeyle (GR-1). FG ucu ve taslak iptali tek Mongo işlemi; işlem desteklemeyen sunucuda `MasterDataAtomicWrite` politikası (Development dışında 503). Dizinler açılışta.

## 9. Layout & Shell Contract

`shell: tenant`. DL-2 görünümleri `@{ Layout = "_LayoutTenantShell"; }` satırını **açıkça** yazar (`.antigravity/rules/views-organization.md`; emsal `Views/MasterDataManagement/Items/`). Kabul: AC-14.

## 10. Backend File Convention

Golden Reference slim birebir. Ürün yazıcıları `Features/ProductItemSkuMaster/` altında durur ki kapsam kabul testi (`IsProductCommandNamespace`) onları görsün (emsal `ProductItemSkuMaster/StockBehaviour/`); önekler kiracı geneli olduğu için ayrı özellik klasöründe.

```text
Features/ProductItemSkuMaster/ProductIdentifiers/
├── Commands/   AssignProductIdentifierCommand · RetireProductIdentifierCommand · WithdrawProductIdentifierCommand  (sealed record, IProductLegalEntityScopeInventoryMutation)
├── Queries/    GetProductIdentifierListQuery · GetProductIdentifierByIdQuery · ResolveGtinQuery
├── Handlers/CommandHandlers/  AssignProductIdentifierHandler · RetireProductIdentifierHandler · WithdrawProductIdentifierHandler
├── Handlers/QueryHandlers/    GetProductIdentifierListHandler · GetProductIdentifierByIdHandler · ResolveGtinHandler
├── Validators/ AssignProductIdentifierValidator · RetireProductIdentifierValidator
└── ProductIdentifiersModels.cs
Features/Gs1CompanyPrefixes/
├── Commands/ AddGs1CompanyPrefixCommand · RetireGs1CompanyPrefixCommand
├── Queries/  GetGs1CompanyPrefixListQuery
├── Handlers/CommandHandlers/ AddGs1CompanyPrefixHandler · RetireGs1CompanyPrefixHandler
├── Handlers/QueryHandlers/   GetGs1CompanyPrefixListHandler
├── Validators/ AddGs1CompanyPrefixValidator
└── Gs1CompanyPrefixesModels.cs
```

Handler / Validator adında `Command` / `Query` eki yok; `Response<T>` zarfı; `BulkDelete` yok (GR-1).

## 11. Frontend File Contract

Yalnız DL-2. Slim set `Views/MasterDataManagement/ProductIdentifiers/`: `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml` (`data-dt-standard="v2"`, `data-dt-data-mode="server"`, iskelet), `_IndexL10n.cshtml`, `_CreateEditOffcanvas.cshtml` (atama), `_DetailsQuickView.cshtml`, `ProductIdentifiersIndex.cs`; JS `index.js`, `index.l10n.js`. Liste: GTIN, düzey, hedef kodu ve adı, kaynak, durum, atanma tarihi; süzgeç: düzey, durum, kaynak, GTIN / kod. Önekler aynı sayfada ayrı bölüm. FG, LSKU, Kalem ayrıntısına salt-okunur "Tanımlayıcılar" bölümü. 7 dil, menü, Ctrl+K, yetkisiz yüz (UAS-001), sayfa yardımı (§19). `verify_datatable_page.py --area MasterDataManagement --module ProductIdentifiers` yeşil.

## 12. Validation Rules

| Alan | Kural | Hata |
|---|---|---|
| `Gtin` girdi | 8, 12, 13 ya da 14 ASCII hane; soldan sıfırla 14'e; `IsValidGtin14` doğru | 422 `GTIN_INVALID` |
| `Gtin` | Kiracıda başka satırda yok (`Retired` dahil) | 409 `GTIN_CONFLICT` |
| `PackagingLevel` | `SalesBox` defter uçlarından atanamaz, geri çekilemez, emekliye ayrılamaz; tek yol FG ucu ve taslak iptali | 422 `GTIN_LEVEL_USE_FINISHED_GOOD_ENDPOINT` |
| Hedef | Düzeyle uyumlu; kiracıda var; FG / LSKU ise kapsam içinde (K-14); hedef `Retired` değil | 404 `TARGET_NOT_FOUND` · 422 `GTIN_LEVEL_TARGET_MISMATCH` · 409 `TARGET_RETIRED` |
| Düzey başına | Hedef + düzeyde etkin GTIN varsa | 409 `GTIN_LEVEL_OCCUPIED` |
| `Origin` | `Own`: gösterge hanesinden sonraki 13 hane etkin bir önekle başlar; `External`: denetlenmez | 422 `GTIN_PREFIX_MISMATCH` |
| Emekliye ayırma | `RetirementReason` 1–500 | 400 |
| Geri çekme | Yalnız hedef `Draft`; yumuşak silme, GTIN serbest | 409 `GTIN_WITHDRAW_NOT_DRAFT` |
| `Prefix` | Yalnız hane, 4–12 (GS1 Genel Spesifikasyonları; dış kaynak, depoda doğrulanmadı); tekil; başka etkin önekin ön eki ya da uzantısı değil | 422 `GS1_PREFIX_INVALID` · 409 `GS1_PREFIX_CONFLICT` |
| Önek emekliye ayırma | Var olan `Own` GTIN'ler geçerli kalır; o önekle yeni `Own` reddedilir | — |
| `ExpectedVersion`, `Idempotency-Key` | FU04 deseni: sürüm uyuşmazlığı, aynı anahtar + farklı gövde | 409 `VERSION_CONFLICT` · 409 `IDEMPOTENCY_KEY_CONFLICT` |

**FG ucu** (`PUT /api/finished-goods/{id}/gtin`): yol, gövde, izin, kodlar (`FG_GTIN_INVALID`, `FG_GTIN_CONFLICT`, `STOCK_BEHAVIOUR_FIELD_LOCKED`) aynı. Değişen: çakışma defterin bütün düzeylerine ve **emekli GTIN'lere** bakar (AK-3, 409 `FG_GTIN_CONFLICT`). Taslakta A → B değişimi aynı işlemde A satırını geri çeker (yumuşak silme), B'yi `Active` yazar.

## 13. Failure Path to Verify

| Durum | Beklenen |
|---|---|
| Kontrol hanesi yanlış · 15 hane · harf | 422 `GTIN_INVALID`, yazım yok |
| GTIN başka düzeyde / kayıtta ya da `Retired` | 409 `GTIN_CONFLICT` (FG ucunda `FG_GTIN_CONFLICT`) |
| İki eşzamanlı atama, aynı GTIN | Biri başarılı, öbürü 409 (tekil dizin) |
| Başka kiracı · kapsam dışı FG / LSKU (atama, emekli, geri çekme, ById, çözümleme) | 404; listede yok |
| İzinsiz | 403; ekran iskelet çizmez |
| FG ucu ya da taslak iptali: işlem içinde ikinci yazım başarısız | İşlem geri alınır, iki koleksiyonda değişiklik yok; geçici hata tüm işlemi yeniden koşar, sonucu bilinmeyen kesinleştirme yeniden kesinleştirilir (`MasterDataAtomicWrite`). Tekrar anahtarı işaretleri (`GtinCommand`, `LastCommand`) aynı işlemde: aynı anahtar + aynı gövde ikinci yazım yapmadan aynı sonucu döner |
| İşlem desteklemeyen sunucu | Development dışında 503 |
| Göç, çakışma (aynı GTIN'de canlı ve emekli FG'ler) | Canlı FG `Active`; emekliler satır almaz, raporda (K-11) |
| Göç, yalnız emekli FG'ler | En son emekliye alınan FG `Retired` satır (`MigrationNote`), diğerleri raporda |
| Göç ikinci koşu | Değişiklik yok; açılış hiçbir durumda durmaz |
| Hedef P6'dan sonra emekliye ayrıldı | Satır olduğu gibi kalır (`Active`; `Status` tanımlayıcının yaşam döngüsüdür, hedefinki değil); GTIN dizinde, yeniden verilmez; çözümleme `targetLifecycleStatus: Retired` döner; emekliye ayırma ayrı komut (SalesBox hariç) |
| Denetim iletimi başarısız | İş durmaz, uyarı (yol b) |

## 14. Authorization Convention

```text
Policy:     [Authorize] + [HasPermission(...)] her eylemde
Permission: mdm.product-identifiers.read | .assign | .retire ; mdm.gs1-company-prefixes.manage   // PKS-001
Reads:      sözleşme okumaları (§16) mevcut mdm.product-master.read ile
FG ucu:     StockBehaviourPermissions.EditFinishedGood — değişmez
Actor type: tenant_user; sözleşme okumalarında servis kimliği (mevcut model)
Scope:      Tenant + tüzel kişi kapsamı (FG / LSKU hedefleri, K-14); Kalem ve önek kiracı geneli
```

- **Tohum yok;** anahtarlar yalnız manifest → katalog → Auth eşitlemesiyle (BL-592). **`ApiPermissions` yok** (FU06 §0.3).
- Manifest (DL-2): sayfa `PRODUCT_IDENTIFIERS` (`RequiredPermission` `.read`); eylemler `ASSIGN` (Toolbar, `.assign`), `WITHDRAW` (RowAction, `.assign`), `RETIRE` (RowAction, `IsDangerous`, `.retire`), `MANAGE_GS1_PREFIXES` (Toolbar, `.manage`). Modül sürümü bir küçük artar.
- DL-1'de anahtarlar zorlanır, beyan edilmez; yazma uçlarını yalnız testler çağırır. FG ucu ve okumalar DL-1'de canlı.

**Beklenen rol verimi (DL-2 öncesi ölçülür, AK-6):**

| Anahtar | Kiracı Admin | Viewer | `ProductDataSteward` | `ProductIdentityApprover`, `ProductIdentityRetirementSteward` |
|---|---|---|---|---|
| `mdm.product-identifiers.read` | ✔ | ✔ | ✔ | — |
| `mdm.product-identifiers.assign` | ✔ | — | ✔ | — |
| `mdm.product-identifiers.retire` | ✔ | — | ✔ | — |
| `mdm.gs1-company-prefixes.manage` | ✔ | — | — | — |

Admin / Viewer sütunları plan eşitlemesinin genel kuralıdır (`DefaultRolePermissionTemplate.EntitlementOnlyViewerPermissions`; ölçülmedi). Adanmış roller yalnız `DedicatedRoles` şablonundakini alır; `ProductDataSteward` sütunu Auth değişikliği ister → XMC-001 sorusu. Ölçüm farklı çıkarsa DL-2 prompt'u yazılmaz, kule karar verir.

## 15. Audited Events (AUD-001)

Yol b, iz `mdm-merkezi-iletim` (`IAuditableCommand` + `IAuditMetadataProvider`).

| Komut | Olay | Nesne | Yol | Önce / sonra | İstisna |
|---|---|---|---|---|---|
| `AssignProductIdentifierCommand` | `product-identifier.assigned` | ProductIdentifier | b | Sonra: GTIN, düzey, hedef, kaynak | — |
| `RetireProductIdentifierCommand` | `product-identifier.retired` | ProductIdentifier | b | Önce / sonra: durum; gerekçe | — |
| `WithdrawProductIdentifierCommand` | `product-identifier.withdrawn` | ProductIdentifier | b | Önce: GTIN, hedef | — |
| `AddGs1CompanyPrefixCommand` | `gs1-company-prefix.added` | Gs1CompanyPrefix | b | Sonra: önek | — |
| `RetireGs1CompanyPrefixCommand` | `gs1-company-prefix.retired` | Gs1CompanyPrefix | b | Önce / sonra: durum | — |
| `AssignFinishedGoodGtinCommand` (değişir) | `finished-good.gtin-assigned` (FU04 §21) | FinishedGood | b | Önce / sonra: GTIN; geri çekilen önceki GTIN | — |
| `CancelFinishedGoodDraftCommand` (değişir) | mevcut iptal olayı, ek alan: geri çekilen GTIN | FinishedGood | mevcut | Önce: GTIN | — |
| Göç (açılış) | `product-identifier.migrated`, satır başına; çakışma raporu ayrı olay | ProductIdentifier | b, **sistem aktörü** (`system:mdm-gtin-migration`) | Sonra: GTIN, hedef, durum, not | — |

## 16. Gateway / API Routing Decision

- Yazma uçları `/api/product-identifiers` ve `/api/gs1-company-prefixes` → **Gateway rotası gerekir**, yalnız integration-agent; kuyruk satırı DL-1'den sonra (emsal `WMS-MVP5-DL8`).
- Sözleşme okumaları `/api/product-master/{everything}` altında; FG ucu var. Tarayıcı aynı kökenli MVC vekiliyle Gateway'e gider.

**Okuma sözleşmesi — `product-master-bundle` v1.1 taslağına ekleme (K-13):**

| Ek | Ne |
|---|---|
| `GET /product-master/identifiers/{gtin}` | 8–14 hane, 14'e tamamlanır; `{gtin, packagingLevel, targetKind, targetId, skuLevel, canonicalCode, status, origin, targetLifecycleStatus}`; bilinmeyen ya da kapsam dışı 404 `UNKNOWN_GTIN`; geçersiz 400 (AK-7) |
| `SkuRef.identifiers[]` (isteğe bağlı) | `{gtin, packagingLevel, status, origin}` |
| `GET /product-master/gs1-company-prefixes` | Etkin önekler (SSCC için) |
| `gtin` (v1.0 alanı) | Anlamı aynı: FG'de satış kutusu; LSKU ve Kalem'de null |
| Metin düzeltmesi | `info.x-notes` "LSKU … P6'ya kadar null" ve `SkuRef` tablosundaki "null (P6'ya kadar, O-6)" → "null; LSKU GTIN'i `identifiers[]`'ta; kalıcılığı stok ekibinin yazılı kabulüne bağlı (AK-11)" |

**Tüketici etkisi (hepsi eklemeli; sessiz davranış değişikliği yok):**

| Tüketici | Bugün | P6 sonrası |
|---|---|---|
| MOD-0174 `gs1-check` (`MOD-0174-lot-serial-tracking.md` K-B) ve TRACE `checkGs1Code` (`trace.openapi.yaml`, `gtinSkippedReason: PRODUCT_MASTER_GTIN_NULL`) | SKU `gtin` null ise GTIN denetimi uyarıyla atlanır | FG aynı; LSKU'da atlama sürer. LSKU denetimi için `identifiers[]`'ı okumaları stok ekibinin kendi değişikliği |
| MOD-0177 geri çağırma (pakette §8.11.6 "PRODUCT-MASTER — none"; gereksinim belgesi "recall per market uses `gtin` + `marketCode`") | GTIN okumuyor | Pazar başına geri çağırma LSKU GTIN'ini `identifiers[]`'tan okumalı; `gtin` null kalırsa (AK-11) bu yol tek yoldur |
| MOD-0182 (paket yok) | — | Tarama `identifiers/{gtin}`, SSCC önek okuması |

## 17. Acceptance Criteria

- [ ] AC-1 (zorunlu) — DL-1: `Item`, `Lsku`, `Case`, `Pallet` atama 201; GTIN-14 saklanır; 8 / 12 / 13 hane tamamlanır; yanlış kontrol hanesi 422 `GTIN_INVALID`.
- [ ] AC-2 (zorunlu) — DL-1: aynı GTIN ikinci kez (başka düzey, hedef ya da `Retired`) 409 `GTIN_CONFLICT`; eşzamanlı iki atamada tam biri başarılı.
- [ ] AC-3 (zorunlu) — DL-1: başka kiracının tanımlayıcısı, hedefi, çözümlemesi 404; başka kiracıdaki aynı GTIN engellemez.
- [ ] AC-4 (zorunlu) — DL-1: kapsam dışı FG / LSKU hedefinde atama, emekli, geri çekme, ById ve çözümleme 404, listede yok; Kalem satırı kapsamdan bağımsız görünür; yeni üç komut `ExactMutationCommands`'ta, `KnownUnguardedWriters` değişmez, `ProductLegalEntityScopeWriteAdmissionContractTests` yeşil.
- [ ] AC-5 (zorunlu) — DL-1: emekliye ayırma gerekçesiz 400, gerekçeli 200; geri çekme yalnız taslak hedefte, GTIN'i serbest bırakır; `SalesBox` satırı defter uçlarında 422.
- [ ] AC-6 (zorunlu) — DL-1: `Own` önek kuralı 422; önek ekleme 422 / 409 (§12).
- [ ] AC-7 (zorunlu) — DL-1: FG ucu — mevcut testler yeşil (`A_gtin_with_a_wrong_check_digit_is_refused_by_name`, `The_unique_index_refuses_a_gtin_race_the_pre_check_cannot_see_and_null_gtins_never_collide`, `A_cancelled_draft_gives_its_gtin_back_and_a_new_finished_good_takes_it`, `StockBehaviourHttpTests`); `A_gtin_held_by_another_live_finished_good_is_refused_and_a_retired_ones_is_free` emekli yarısında 409 bekleyecek biçimde değişir; koli düzeyindeki GTIN 409 `FG_GTIN_CONFLICT`; taslakta A → B'de A geri çekilir; işlem içi hata kancasıyla iki koleksiyonda değişiklik yok; aynı anahtar ikinci yazım yapmaz.
- [ ] AC-8 (zorunlu) — DL-1: göç K-11'e uyar (canlı `Active`; canlı yoksa en son emekli `Retired`; diğerleri raporda); ikinci koşu değişiklik üretmez; açılış durmaz.
- [ ] AC-9 (zorunlu) — DL-1: çözümleme, `identifiers[]` ve önek okuması sözleşme dosyasındaki şemaya uyar; FG `gtin` ve LSKU / Kalem `gtin: null` değişmez (mevcut S4 testleri yeşil).
- [ ] AC-10 (zorunlu) — DL-1: §15'teki her yazma tam bir yol b denetim kaydı üretir; göç kaydı sistem aktörüyle; olay adları tablodakiyle aynı.
- [ ] AC-11 (zorunlu) — DL-1: §18 sabotajlarının her biri adlı testte kırmızı, geri alınca yeşil; `git status --porcelain` boş.
- [ ] AC-12 (zorunlu) — DL-2: manifestte §14 sayfa ve eylemleri; `ApiPermissions` boş; mevcut 53 beyan (FU06 T2 listesi; DL-2 öncesi yeniden sayılır) değişmez, sayı 4 artar; Auth günlüğünde `complete active module descriptor set` hatası yok.
- [ ] AC-13 (zorunlu) — DL-2: yerel yığında rol verimi §14 tablosuyla aynı (ya da kule kararıyla değişmiş tablo).
- [ ] AC-14 (zorunlu) — DL-2: `Layout = "_LayoutTenantShell"`; `verify_datatable_page.py` sapma 0; 7 dilde anahtar eşitliği; izinsiz kullanıcıya iskelet yok; FG / LSKU / Kalem ayrıntısında "Tanımlayıcılar" bölümü.
- [ ] AC-15 (isteğe bağlı) — DL-2: sayfa yardımı turu (§19).

## 18. Test Expectations

Üretim koduyla, gerçek Mongo (replica set, `MdmTestMongo`) ile; test çifti yalnız denetim iletimi için.

| Test | Ölçer |
|---|---|
| `Gs1RulesTests` (yeni) | Tamamlama, kontrol hanesi (`IsValidGtin14` çağrısı), önek eşleşmesi, önek ön-ek çakışması |
| `ProductIdentifierHttpTests` (yeni) | AC-1…AC-6 |
| `StockBehaviourMongoTests` (değişir) | AC-7; bir test adının emekli beklentisi değişir |
| `FinishedGoodGtinRegistryTransactionTests` (yeni) | AC-7 işlem atomikliği, A → B, taslak iptali, tekrar anahtarı |
| `ProductIdentifierMigrationTests` (yeni) | AC-8 |
| `ProductMasterIdentifierContractTests` (yeni) | AC-9 |
| `ProductIdentifierAuditTests` (yeni) | AC-10 |
| `ProductLegalEntityScopeWriteAdmissionContractTests` (değişir) | `ExactMutationCommands` 26 → 29; test adındaki sayı güncellenir |
| `ProductItemSkuMasterManifestProviderTests` (DL-2, değişir) | AC-12 |

**Sabotajlar:** (1) tekil dizinden `Retired`'ı çıkar → AC-2; (2) kiracı süzgecini kaldır → AC-3; (3) atama işleyicisinden kapsam korumasını kaldır → AC-4; (4) tamamlamayı kaldır → AC-1; (5) FG ucunda defter denetimini kaldır → AC-7 koli satırı; (6) FG ucunda emekli GTIN'i serbest say → AC-7 emekli satırı; (7) FG ucunda işlemi iki ayrı yazıma böl → AC-7 atomiklik; (8) göçte "en son emekli" seçimini kaldır → AC-8; (9) LSKU `gtin`'ini defterden doldur → AC-9.

## 19. Page Help (UXH-001)

DL-2: 7 dilde — GTIN, düzeyler, `Own` / `External`, emekli GTIN'in neden yeniden kullanılmadığı, SSCC'nin depoda üretildiği. Tur isteğe bağlı (AC-15).

## 20. Ready-for-dev Checklist

- [x] DCP-002 kimlik kapısı (2026-11-10)
- [x] Künye; `golden_reference: slim`, `data_mode: server`, `form_field_count: 4` (hedef, düzey, GTIN, kaynak)
- [x] Layout `_LayoutTenantShell` (§9)
- [x] Validation, Failure Path, Authorization, Gateway, Audited Events
- [x] Teslim dilimleri, kanıt seviyesi, plan sütunları
- [x] Kararlar §0.4 (AK-1…AK-8, GR-1, K-11…K-14)
- [x] Bağımsız inceleme turu 1 (KOŞULLU) bulguları işlendi
- [ ] Bağımsız inceleme turu 2 ve onay satırı (kule)
- [ ] AK-11 stok ekibinin yazılı kabulü — DL-1'i durdurmaz; sözleşme metni kabul gelene kadar "kalıcılığı kabule bağlı" der
- [ ] AK-6 ölçümü — DL-2 prompt'undan önce
- [ ] Gateway kuyruk satırı (integration-agent)

## 21. Implementation Notes

- **N-1 İki dilim.** DL-1 arka uç: MOD-0182 / MOD-0174 ihtiyacı yalnız burada; E4 ekransız verilebilir. DL-2 ekran + manifest + 7 dil: AK-6 ölçümüne bağlı, DL-1'i bekletmez. Tek dilim bir prompt'a sığmaz (FU04'te S3 ile S5 ayrıydı).
- **N-2 FG satış kutusu (K-12).** `AssignFinishedGoodGtinHandler` ve `CancelFinishedGoodDraftHandler` defter satırını ve FG belgesini **tek Mongo işleminde** yazar (`FinishedGoodRepository`'deki işlem seçenekleri: snapshot okuma, majority yazma, `UnknownTransactionCommitResult` yeniden kesinleştirme). Defter tekil dizini bütün düzeyleri korur. `gtin` okuması `FinishedGood.Gtin`'den sürer (işlem ikisini eşit tutar); `identifiers[]` defterden. `FinishedGood.Gtin`, `ReleasedGtin` ve `ux_finished_goods_tenant_gtin_live_v1` bu pakette kaldırılmaz.
- **N-3 Koli / palet (AK-1).** Geçici bağ: hedef FG + düzey, `PackagingLevelId = null`, hedef + düzey başına tek etkin GTIN. P9 düzey kaydını açınca kendi paketindeki sahiplenme komutuyla `PackagingLevelId`'yi doldurur; GTIN ve geçmişi değişmez. Aynı FG'nin iki koli yapılandırması P9'dan önce tutulamaz (🟡). Elenen seçenekler: P9'u beklemek (MOD-0182 gecikir); birim başına bağ (palet birimi `item-uom`'da yok, MOD-0048'e dokunur).
- **N-4 Göç (K-11):** açılışta, dizin kurucudan sonra, kiracı başına; anahtar (TenantId, Gtin). İptal edilen taslağın `ReleasedGtin`'i taşınmaz (serbest, AK-3).
- **N-5 SAP / Oracle:** SAP malzeme ana kaydında ölçü birimi başına EAN / UPC, önekten iç numara aralığı. Oracle kalem + birim GTIN çapraz başvurusu. Burada GTIN kayıt + düzeye bağlanır (Oracle'a yakın), düzey P9'da birime eşlenir (SAP'ye yakın); otomatik atama yok.
- **N-6 Plan:** plan satırı verilmedi; dilim sütunlarında `plan kimliği yok` / `plan satırı yok`. P6 tamamlama planı §4'ten; MVP-5 ilişkisi W-7.
- **N-7 Dosyalar** (`services/Diten.MdmService/` altında; (y) yeni, (d) değişir):
  - (y) `src/Diten.MdmService.Domain/Entities/ProductIdentifier.cs`, `Gs1CompanyPrefix.cs`; `src/Diten.MdmService.Domain/Repositories/IProductIdentifierRepository.cs`, `IGs1CompanyPrefixRepository.cs`; `src/Diten.MdmService.Domain/ValueObjects/Gs1Rules.cs`
  - (y) `src/Diten.MdmService.Persistence/Repositories/ProductIdentifierRepository.cs`, `Gs1CompanyPrefixRepository.cs`; `src/Diten.MdmService.Api/Services/ProductIdentifiers/ProductIdentifierIndexInitializer.cs`, `ProductIdentifierMigrationHostedService.cs`
  - (y) `src/Diten.MdmService.Application/Features/ProductItemSkuMaster/ProductIdentifiers/**`, `src/Diten.MdmService.Application/Features/Gs1CompanyPrefixes/**` (§10)
  - (y) `src/Diten.MdmService.Api/Controllers/ProductIdentifiersController.cs`, `Gs1CompanyPrefixesController.cs`
  - (d) `src/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs`, `ProductStockBehaviourRepository.cs`, `ProductMasterReadStore.cs`
  - (d) `src/Diten.MdmService.Application/Features/ProductItemSkuMaster/StockBehaviour/StockBehaviourHandlers.cs`, `src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/CancelFinishedGoodDraftHandler.cs`, `src/Diten.MdmService.Application/Features/ProductMaster/ProductMasterContract.cs`
  - (d) `src/Diten.MdmService.Api/Controllers/ProductMasterController.cs`; DI kaydı (`Program.cs` ya da servisin kayıt uzantısı)
  - (d, DL-2) `src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
  - (y / d) `tests/**` (§18)
  - (y, DL-2) `frontend/Diten.Web/Views/MasterDataManagement/ProductIdentifiers/**`, `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/ProductIdentifiers/**`, MVC controller, 7 dil kaynakları; (d) `Views/MasterDataManagement/{FinishedGoods,Lskus,Items}/` ayrıntı bölümü
  - (d) `docs/analysis/contracts/product-master-bundle.openapi.yaml`
- Dilim tablosu `## 24. Teslim dilimleri` altında (`scripts/pack_approval.py` `SLICE_HEADING`).
- Kurallar: `module-pack-standard.md`, `handler-design.md`, `erp-architecture.md`, `response-envelope.md`, `multi-tenancy`, `localization-standard`, `permission-key-standard.md`, `audit-trail-standard.md`.

## 22. Follow-up Items

| # | Konu | Sahip | Regresyon riski |
|---|---|---|---|
| F-1 | P9: düzey kaydı + sahiplenme komutu | MDM | 🟡 P9 yoksa koli / palet geçici kalır |
| F-2 | MOD-0182: SSCC önek okumasıyla, tarama `identifiers/{gtin}` ile | MVP-5 CT | 🟢 |
| F-3 | Stok ekibine haber + AK-11 yazılı kabul; MOD-0174 / TRACE / MOD-0177 `identifiers[]` okuması | Kule | 🟡 haber verilmezse LSKU GTIN denetimi atlanmaya devam eder |
| F-4 | `FinishedGood.Gtin` ve eski dizinin kaldırılması | MDM | 🟡 ayrı paket |
| F-5 | O-12: FG ucunun geçici anahtarı — BL-573 ikinci yarı | MDM + Auth | 🟢 |
| F-6 | `module-implementation-status.md` satırı, kod gelince | Kule | 🟢 |

## 23. Canlı sıra ve geri dönüş

**Kanıt.** DL-1 kabulü E4: otomatik test + gerçek Mongo kalıcılığı + kiracı ve tüzel kişi kapsamı + denetim + hata yolları (yerel). **Canlı kabul** DL-2 ve Gateway rota satırı tamamlandıktan sonra, pilot kiracıda.

**Sıra:** 1. MDM DL-1 (açılışta dizinler, sonra göç; göç raporu okunur, çakışmalar kuleye). 2. Gateway rotası. 3. MDM DL-2 (manifest; FU06 dersi: son başlayan kopya yeni sürüm, eski kopyalar durmadan pilot başlamaz). 4. AK-6 tablosu pilot kiracıda doğrulanır. 5. Stok ekibine haber (F-3).

**Geri dönüş: yalnız ileri düzeltme.** Defter eklemelidir, `FinishedGood.Gtin` korunur. Eski MDM'ye dönülürse FG ucu eski davranışa döner (emekli GTIN yeniden serbest, defter yazılmaz); o arada atanan FG GTIN'leri ileri dönüşte göçün tekrarında deftere girer, çakışanlar raporlanır. DL-2 manifest sürümünün geri alınması FU06 §13 / §23'teki budama sorununu doğurur; aynı kural: geri alınmaz, gerekirse önce Auth satırları kaldırılır.

**Platform bağlantıları (DL-2):** menü · Ctrl+K · manifest sayfası · 7 dil · yetkisiz yüz · sayfa yardımı · ayrıntı bağlantıları · Gateway · denetim · plan hakkıyla rol verimi; satır başına durum DL-2 prompt'unda ölçülür.

## 24. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | tanımlayıcı defteri, GS1 önekleri, FG satış kutusu bağı (tek işlem), göç, kapsam ve okuma sözleşmesi eki (arka uç) | Owned Objects, Entity Fields, Repo Scope, Runtime Constraints, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events (AUD-001), Gateway / API Routing Decision, Acceptance Criteria, Test Expectations | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | Tanımlayıcılar ekranı, ayrıntı bölümleri, manifest beyanı, rol verimi ve 7 dil | Layout & Shell Contract, Frontend File Contract, Authorization Convention, Page Help (UXH-001), Acceptance Criteria, Canlı sıra ve geri dönüş | E4 | plan kimliği yok | plan satırı yok |

DL-1 çekirdektir; DL-2 DL-1'e dayanır.

## 25. Açık kararlar

| # | Karar | Kim | Durum |
|---|---|---|---|
| AK-1…AK-8, GR-1 | §0.4 | Sahip / Kule | **Kapandı** |
| AK-9 | Sözleşme sürümü | Kule | **Kapandı, K-13 ile değişti:** v1.1 taslağı |
| K-11…K-14 | Göç, tek işlem, sözleşme sürümü, tüzel kişi kapsamı | Kule | **Kapandı** (inceleme turu 1) |
| AK-6 | Rol verimi ölçümü | Kule + Auth sahibi | Karar kapandı; ölçüm DL-2 öncesi açık iş |
| AK-10 | `target` tarihi, onaylayan, ikinci kişi | Kule | **Açık: TBD**; tarih "2026-11-20 (tahmin)" |
| AK-11 | LSKU `gtin`'in "P6'ya kadar null" vaadinin "kalıcı null" olması | Stok ekibi (yazılı kabul), kule | **Açık.** DL-1'i durdurmaz; kabul gelmezse `gtin`'in LSKU'da dolması ayrı karar (MOD-0174 / TRACE davranışı değişir) |

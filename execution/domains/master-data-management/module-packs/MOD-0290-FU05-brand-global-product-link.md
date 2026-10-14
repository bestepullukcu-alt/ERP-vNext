---
id: MOD-0290-FU05
name: Brand to Global Product Link
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
runtime_code_scope: "YOK (taslak). Kod, sahibin §0 kararları ve bu paketin `ready-for-dev` onayından sonra §21'deki dilimlerle, her dilim ayrı CT prompt'uyla açılır."
owner: module-pack-author
branch: feature/mdm/mod-0290-fu05-brand-global-product-link
started: 2026-10-15
target: Dilim 1 sahibin §0 kararları + CT `ready-for-dev` onayıyla
form_field_count: 2
data_classification: "Kiracı içinde ana veri; kişisel veri yok (yalnız işlemi yapanın kullanıcı kimliği)."
audit_class: "Ana veri — Global Ürün'ün mevcut güvenilir kaynak niyeti (yol b, `mdm-guvenilir-kaynak-niyeti`); yeni denetimsiz komut yok."
dependencies:
  - MOD-0290 (Product / Item / SKU Master — ebeveyn; Global Ürün kümesi, durum akışı, kod defteri, denetim niyeti)
  - MOD-0290-FU02 (Brand / Product Runtime + UI — `Brand` kümesi, `IsLinkableAt`, `ProductBrandLink.Refusal`, `brand_*` neden kodları)
  - MOD-0290-FU03 (Ürün Tüzel Kişi Kapsamı — Global Ürün okuma / yazma uçlarındaki kapsam bekçisi; bu paket aynen kullanır)
  - MOD-0021 (merkezi denetim günlüğü — Platform güvenilir kaynak ucu ve işlem haritası)
  - MOD-0018 (RBAC — yalnız tüketim; yeni anahtar katalog → Auth eşitlemesiyle)
  - CRM W10 · Marka entegrasyonu (tüketici — "markayı Global Ürün'den türetmek"; `ROADMAP-visit-workspace.md:126-136`)
  - DEV-0000 (Golden Reference Slim — ekran şablonu; Global Ürün ekranı zaten Slim + `_ListShell`)
---

# MOD-0290-FU05 — Brand to Global Product Link (Marka ↔ Global Ürün bağı)

> **DRAFT · KOD YOK.** Sahibin 2026-10-11 kararı ("Marka Global Ürüne bağlanır") bugün kodda yok: Global Ürün kaydında
> marka alanı bulunmuyor, marka yalnız eski "Ürünler" (CRM tarafı) kaydına bağlanabiliyor. CRM'in son fazı (W10 · Marka
> entegrasyonu) bu bağı ön koşul olarak yazıyor. Bu paket bağı açan **en küçük dilimi** tanımlar.
>
> **DCP-002 kimlik kapısı — PASS (2026-10-15, bu dal, 6b8a62d5a):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0290-FU05 --name "Brand to Global Product Link" --parent MOD-0290`
> → `OK  MOD-0290-FU05: proven against Blueprint/registry.` (exit 0).
> **Tüm dallarda ölçüldü:** `git log --all -S 'MOD-0290-FU05'` = 0 commit; `git log --all -- '*MOD-0290-FU05*'` = 0 commit.
> Doğrulayıcı yalnız bu dalın registry'sini okur (FU04 §25 F8 notu); numara bu yüzden tüm dallarda ayrıca ölçüldü.
>
> Otorite sırası: **Blueprint Excel** > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.

## 0. Kararlar

### 0.1 Kaynak kararlar (verilmiş, bu paket uygular)

| Kaynak | Ne diyor | Bu pakete etkisi |
|---|---|---|
| Sahip, 2026-10-11 — [`MOD-0290-urun-kalem-sku-haritasi.md:96`](../../../../docs/reference/modules/tenant/MOD-0290-urun-kalem-sku-haritasi.md) (açık soru 1) | "Marka Global Ürüne bağlanır, 'Ürünler' sayfası zamanla Global Ürünün CRM görünümü olur." KARAR: öneri kabul | Bağın yeri Global Ürün'dür; eski `Product` kümesi değil |
| Aynı belge `:21` (şema) ve `:40` (kayıt tablosu) | "Marka — GP'ye bağ ileride"; "Global Ürüne bağı sonraki GP turunda" | Bu paket o "sonraki GP turu"dur |
| [Plan P8](../../../../docs/roadmap/plans/mdm-product-master-completion-plan-2026-10-02.md) `:67` | "Marka ↔ Global Ürün bağı; Markanın altında tescil listesi" | Bu paket P8'in **yalnız bağ** yarısıdır; tescil listesi dışarıda (§2.3) |
| Plan `:157`, `:181` | Açık soru 1 P8'i belirler; "CRM'in Ürünler kaydı Global Ürünü kimliğiyle gösterir" | Eski kayda dokunulmaz; CRM geçişi W10'un işi |
| [BL-562](../../../../docs/roadmap/backlog/product-backlog.md) (`product-backlog.md:9528-9536`) | "Global Ürün'e isteğe bağlı Marka başvurusu (yalnız bağlanabilir markaya) sonraki bir GP turunda" | İsteğe bağlı alan + bağlanabilirlik kuralı. BL-562'nin diğer üç maddesi (arşivi geri alma, ikinci onay, tescil) bu pakette değil |
| CRM [`ROADMAP-visit-workspace.md:126-136`](../../commercial-suite/work-packs/ROADMAP-visit-workspace.md) | W10 ön koşulu: "MDM'nin 'Marka → Global Ürün' birleştirme kararı … markayı Global Ürün'den türetmek için bu bağ gerekir" | Okuma modelleri (liste, detay, seçici) markayı taşır |
| CRM [`MDM-BRAND-CHANGE-crm-impact-2026-10-09.md:17`](../../commercial-suite/work-packs/MDM-BRAND-CHANGE-crm-impact-2026-10-09.md), `:44`, `:53` | "Ürünler" kaydına bağlı 4 eski CRM yeri var; yeni `api/mdm/products` bağımlılığı eklenmez; geçiş planı MDM ile birlikte yazılır | Eski `Product` / `api/mdm/products` / `brand-products/contract` **değişmez** |

### 0.2 Sahip kararları — ÖNERİLEN — SAHİP ONAYI BEKLİYOR

Üçü de **karar değildir**; sahip onaylayana kadar §20'deki ilgili kutular boş kalır.

#### Karar (a) — Onaylı Global Ürün'de marka nasıl değişir?

**ÖNERİLEN — SAHİP ONAYI BEKLİYOR.**

- **Taslak** (`Draft`) Global Ürün: marka normal taslak düzenlemesiyle (`PUT api/global-products/{id}`, `mdm.global-products.update`) değişir.
- **Onaylı** (`IdentityApproved`) Global Ürün: marka, kendi izniyle (**`mdm.global-products.update-brand`**, §14) **doğrudan, denetimli** bir
  komutla değişir; tam düzeltme akışı (MOD-0023 onayı) **gerekmez**. Kimlik (ad, kod) değişmez; yalnız ticari sınıflandırma değişir.
- Onay bekleyen (`PendingIdentityApproval`), emekli (`Retired`) ya da üzerinde süren yaşam döngüsü işlemi olan
  (`ActiveLifecycleOperation != null`) Global Ürün'de marka değişmez → 409.

Gerekçe: bugünkü düzeltme akışı yalnız adı taşır (`StartGlobalProductCorrectionWorkflowCommand` isteği yalnız
`GlobalProductName` — `GlobalProductsController.cs:155-156`); markayı ona eklemek onay motorunu, onaycı pozisyonunu ve
düzeltme işlemcisini (`GlobalProductCorrectionWorkflowProcessor`) dilim 1'e çeker. CRM W10 yalnız bağ ister.

| | SAP S/4HANA | Oracle Fusion (Product Hub) | Öneri |
|---|---|---|---|
| Onaylı kayıtta sınıflandırma alanı | Malzeme ana verisi doğrudan değiştirilir; her değişiklik değişiklik belgesine yazılır. Onay (MDG değişiklik talebi) kuruma göre alan bazında yapılandırılır | Kalem özniteliği değişiklik kontrolü altındaysa değişiklik siparişiyle, değilse doğrudan ve geçmişli düzenlenir; kuruma göre yapılandırılır | Doğrudan + denetimli + ayrı izin (SAP'nin "değişiklik belgesi" modeli). İleride onay gerekirse MOD-0023'e alınır (BL-562 "marka değişikliğinde ikinci onay") |

Regresyon riski (onaysız bırakmanın): 🟡 — onaylı ürünün markası tek kişinin izniyle değişir; denetim izi ve ayrı izin
anahtarı bunu sınırlar. Sahip "onaylı ürüne onay şart" derse dilim 2 MOD-0023'e bağlanır (+1–2 prompt).

#### Karar (b) — Okuma modeli markayı nasıl taşır?

**ÖNERİLEN — SAHİP ONAYI BEKLİYOR.**

Liste, detay ve seçici cevapları `brandId` + `brandCode` + `brandName` taşır. Kod ve ad **okuma anında** `mdm_brands`'tan
çözülür; Global Ürün belgesinde yalnız `BrandId` saklanır (kopya yok). Böylece CRM, Global Ürün'den markayı okumak için
`mdm.brands.read` istemez; `mdm.global-products.read` yeter (CRM'in bugünkü izni, `MDM-BRAND-CHANGE-crm-impact-2026-10-09.md`
§2 son satırları). Marka yeniden adlandırılınca bütün Global Ürün okumaları yeni adı hemen gösterir.

| | SAP S/4HANA | Oracle Fusion | Öneri |
|---|---|---|---|
| Okuma | OData servisleri kod alanını metin ilişkisiyle (açıklama) birlikte döner; açıklama ayrı tablodan okunur, malzemeye kopyalanmaz | REST kalem kaynağı öznitelik değerini görünen adıyla döner; ad kaynağından okunur | Kimlik saklanır, kod + ad okumada çözülür |

Regresyon riski: 🟢 — eklemedir; mevcut alanlar aynı ad ve türle kalır.

#### Karar (c) — Mevcut Global Ürünler ve eski "Ürünler" kaydı

**ÖNERİLEN — SAHİP ONAYI BEKLİYOR.**

Dilim 1'de **geri doldurma (backfill) yok**. Mevcut Global Ürünler markasız başlar. Eski "Ürünler" kaydındaki
`Product.BrandId`'yi Global Ürünlere eşlemek, CRM'in W10 geçiş planıyla birlikte yazılacak **ayrı bir sonraki dilimdir**
(eşleme kuralı — hangi eski ürün hangi Global Ürün — bugün tanımlı değil; iki kayıt arasında kimlik bağı yok).

| | SAP S/4HANA | Oracle Fusion | Öneri |
|---|---|---|---|
| Yeni alan, mevcut kayıtlar | Yeni alan boş gelir; toplu bakım (mass maintenance) ya da göç aracıyla sonra doldurulur | Yeni öznitelik boş gelir; toplu içe aktarma / toplu güncellemeyle sonra doldurulur | Boş başlar; eşleme CRM W10 ile ayrı dilimde |

Regresyon riski: 🟡 — W10'a kadar CRM ekranları markasız Global Ürün görür; eski 4 CRM yeri eski kayıttan okumaya devam
eder, kırılmaz.

### 0.3 CT kararı bekleyen teknik maddeler (sahip kararı değil)

| # | Konu | Öneri | Regresyon riski |
|---|---|---|---|
| T-1 | Marka seçicisinin kaynağı | Global Ürün'ün kendi ucu `GET api/global-products/brand-options` (yalnız bağlanabilir markalar, sayfalı, `search`); izin `mdm.global-products.create` **veya** `.update` **veya** `.update-brand` (`HasPermissionAttribute(string, params string[])` — `HasPermissionAttribute.cs:15`). Gerekçe: `GET api/mdm/brands` `mdm.brands.read` ister (`BrandsController.cs:41-46`) ve bağlanabilirlik süzgeci taşımaz (`IsLinkable` yalnız detayda — `BrandModels.cs:52-54`, `:117`). Emsal: `GET api/gskus/create-options` (`GskusController.cs:38-43`) | 🟢 |
| T-2 | Onaylı ürün marka komutunun denetim yolu | Mevcut Global Ürün kalıbı: güvenilir kaynak niyeti, yeni `ProductAuditOperation` değeri + Platform işlem haritasına bir satır (`TrustedSourceAuditIntentOperationMap.cs:26` kalıbı). Değer numarası CT'nin: 87–99 Bitmiş Ürün'e, 100–119 Kalem'e ayrılı (`ProductAuditOperation.cs:105-108` yorumu); öneri **120** `GlobalProductBrandChanged`. Platform dosyası başka modüldür → XMC-001: "bunu da düzeltelim mi?" | 🟡 harita satırı gelmezse niyet Platform'da eşlenemez |
| T-3 | `CreateGlobalProductDraftCommand` bilinen denetim borcunda (`tests/architecture/audit-ledger/Diten.MdmService.md:62`) | Dilim 1 oluşturmaya marka alanı ekliyor; borç büyümez ama marka oluşturmada denetimsiz kalır. Öneri: dilim 1 bu borcu **öder** (enum'da `GlobalProductDraftCreated = 5` zaten var — `ProductAuditOperation.cs:9`) ve satırı defterden siler (defter yalnız küçülür). Kabul edilmezse borç satırı kalır, gerekçe raporda yazılır | 🟡 (ödenmezse) |
| T-4 | Taslak düzenlemede `brandId` alanının yokluğu | Alan **yoksa** marka değişmez; `null` markayı kaldırır; değer markayı bağlar. Uygulama: `init` ayarlayıcısı "belirtildi" bayrağı koyar (System.Text.Json ayarlayıcıyı yalnız alan geldiğinde çağırır). Gerekçe: alanı tanımayan bir istemci markayı sessizce silmesin | 🟢 |

## 1. Module Summary

Global Ürün'e **isteğe bağlı, tek** bir marka bağı (`BrandId`) eklenir. Bağ yalnız **bağlanabilir** markaya kurulur
(Etkin + geçerlilik penceresi içinde, arşivli değil — `Brand.IsLinkableAt`, `Brand.cs:42-47`); ret kodları eski
"Ürünler" bağıyla **aynıdır** (`ProductBrandLink.Refusal`, `ProductBrandLink.cs:12-22`: `brand_archived` 409,
`brand_not_linkable` 409). Liste, detay ve seçici markayı kimlik + kod + ad olarak okuma anında çözer; liste markaya göre
süzülebilir. Global Ürün ekranında oluştur / düzenle paneline marka seçimi, detaya ve listeye marka eklenir (7 dil).

Hedef kullanıcı: ürün ana veri sorumlusu (yazar), CRM ve diğer tüketiciler (okur). Kapasite: kiracı başına sınırsız
Global Ürün (`data_mode: server`, bugünkü ekranla aynı — `_DataTable.cshtml:17`).

## 2. Ownership and Boundaries

### 2.1 SoR kararı

- Marka kaydının sahibi FU02'dir (`mdm_brands`); bu paket markaya **yazmaz**.
- Global Ürün kaydının sahibi MOD-0290'dır (`mdm_global_products`); bu paket ona tek alan ekler.
- Bağ yalnız kimliktir: Global Ürün'de marka kodu ya da adı **saklanmaz** (Karar b).

### 2.2 In-scope (dilim 1 — CRM W10'u açan en küçük kapsam)

- `GlobalProduct.BrandId` (Guid?, isteğe bağlı) + dizin `(TenantId, BrandId)`.
- Oluşturmada (`POST api/global-products/drafts`) ve taslak düzenlemede (`PUT api/global-products/{id}`) marka.
- Bağlanabilirlik kuralı; değişmeyen değer yeniden doğrulanmaz (eski "Ürünler" kuralı — `UpdateProductHandler.cs:137-140`).
- Okuma modelleri: liste, detay, seçici `brandId`, `brandCode`, `brandName` taşır (okuma anında çözülür).
- Liste süzgeci `brandId`.
- Marka seçenek ucu `GET api/global-products/brand-options` (T-1).
- Karar (a) onaylanırsa: onaylı Global Ürün'de marka komutu `POST api/global-products/{id}/brand` + izin
  `mdm.global-products.update-brand` + satır eylemi `CHANGE_BRAND`.
- Ekran: oluştur / düzenle panelinde Select2 marka seçimi (yalnız bağlanabilir markalar), detayda ve listede marka;
  7 dil.
- Denetim niyeti (marka değişikliği önce / sonra) — §15.

### 2.3 Out-of-scope / yetkisiz

- **Eski kayıt değişmez:** `mdm_products`, `api/mdm/products`, `api/mdm/brand-products/contract`, `Product.BrandId`
  (`Product.cs:20`). CRM'in 4 eski yeri bu kayda bağlı (`MDM-BRAND-CHANGE-crm-impact-2026-10-09.md:17`).
- Mevcut Global Ürünlerin geri doldurulması / eski ürünlerden göç (Karar c).
- Marka tescil listesi (P8'in ikinci yarısı; BL-562'ye göre ileride RIM modülünün işi).
- Marka kaydına yazma, marka arşivini geri alma, marka değişikliğinde ikinci onay (BL-562'nin diğer maddeleri).
- Çoklu marka (bir Global Ürün en çok bir marka — eski `Product` ile aynı kural, `Product.cs:7-8`).
- GSKU / LSKU / Bitmiş Ürün'e marka alanı (marka Global Ürün'den türetilir).
- Seçicide marka süzgeci, sözleşme (`product-master-bundle`) değişikliği — CRM W10 isterse sonraki dilim.
- CRM tarafında herhangi bir dosya; Auth rol tohumu (`DataSeeder`).

## 3. Owned Objects

| Tür | Ad | Durum |
|---|---|---|
| Entity alanı | `GlobalProduct.BrandId` (Guid?) | yeni (`GlobalProduct.cs:8-20`'de marka yok — ölçüldü) |
| Dizin | `ix_mdm_global_products_tenant_brand` (`TenantId` + `BrandId`, tekil değil) | yeni (`GlobalProductRepository.cs:1826-1840` dizin kümesine) |
| Komut (mevcut, genişler) | `CreateGlobalProductDraftCommand` (+`BrandId`) | değişir |
| Komut (mevcut, genişler) | `UpdateGlobalProductDraftCommand` (+`BrandId`, T-4) | değişir |
| Komut (yeni, Karar a'ya bağlı) | `ChangeGlobalProductBrandCommand` | yeni |
| Sorgu (mevcut, genişler) | `GetGlobalProductsQuery` (+`BrandId` süzgeci), `GetGlobalProductByIdQuery`, `GetGlobalProductSelectorQuery` | cevap şekli genişler |
| Sorgu (yeni) | `GetGlobalProductBrandOptionsQuery` | yeni (T-1) |
| DTO (genişler) | `CreateGlobalProductDraftRequest`, `UpdateGlobalProductDraftRequest`, `GlobalProductListItemDto`, `GlobalProductDetailDto`, `GlobalProductSelectorDto`, `GlobalProductDraftDto`, `GlobalProductDraftUpdateDto` (`ProductItemSkuMasterModels.cs:18-84`) | yalnız ekleme |
| DTO (yeni) | `ChangeGlobalProductBrandRequest`, `GlobalProductBrandOptionDto` | yeni |
| Depo | `IBrandRepository.GetByIdsAsync` (toplu okuma; bugün yok — `IBrandRepository.cs:13-59`, `IRepository.cs:8-11`) | yeni |
| Depo | `IGlobalProductRepository.UpdateDraftAsync` (+marka), `ChangeBrandAsync` (yeni), sayfa sorgularına `brandId` (`IGlobalProductRepository.cs:33-75`) | değişir |
| API | `POST api/global-products/drafts` · `PUT api/global-products/{id}` · `GET api/global-products` · `GET api/global-products/{id}` · `GET api/global-products/selector` (mevcut) · `GET api/global-products/brand-options` (yeni) · `POST api/global-products/{id}/brand` (yeni, Karar a) | |
| İzin | `mdm.global-products.update-brand` (yeni, Karar a) | |
| Manifest | `GLOBAL_PRODUCTS` sayfasına `CHANGE_BRAND` eylemi; modül sürümü `1.2.0` → `1.3.0` (`ProductItemSkuMasterManifestProvider.cs:79`, `:93-110`) | Karar a'ya bağlı |
| Ön yüz rotası | `/MasterDataManagement/GlobalProducts` (mevcut) + vekil uçlar `api/brand-options`, `api/{id}/brand` | |

## 4. Entity Fields

| Alan | Tür | Zorunlu | Kural | Dizin |
|---|---|---|---|---|
| `BrandId` | `Guid?` | Hayır | Aynı kiracıda var olan `Brand` kimliği; yeni ya da değişen değer için `ProductBrandLink.Refusal(brand, now) == null`; `Guid.Empty` = geçersiz (400); değişmeyen değer yeniden doğrulanmaz | `(TenantId, BrandId)` — liste süzgeci ve "bu markanın Global Ürünleri" sorgusu için |

- `TenantId` sunucuda çözülür, hiçbir istekte yoktur (create doğrulayıcısı `TenantId`'yi zaten reddeder — `CreateGlobalProductDraftValidator.cs:21-23`).
- **Eski belge:** `BrandId` alanı olmayan mevcut belgeler `null` okunur. Mongo'da `Eq(BrandId, null)` alanı olmayan belgeyi
  de eşler; yine de testte alansız bir belge tohumlanır (proje notu: yeni alan koşulu eski belgeyi kaçırabilir).
- Okuma modeli alanları (`brandCode`, `brandName`) **saklanmaz**. Marka belgesi bulunamazsa (FU02'de sert silme yok;
  yalnız teknik `IsDeleted` durumu) `brandId` döner, kod ve ad `null` döner; cevap kırılmaz.
- Arşivli / bağlanamaz hale gelmiş marka **mevcut bağda kalır** ve okumada görünür (FU02 kuralı: "var olan bağlar bozulmaz" —
  `Brand.cs:8-11`, `UpdateProductHandler.cs:137-139`).

## 5. Repo Scope

Backend (`services/Diten.MdmService/src/`):
- `Diten.MdmService.Domain/Entities/GlobalProduct.cs` — `BrandId`.
- `Diten.MdmService.Domain/Enums/ProductAuditOperation.cs` — yalnız T-2 / T-3 CT kararıyla.
- `Diten.MdmService.Domain/Repositories/IGlobalProductRepository.cs`, `IBrandRepository.cs`.
- `Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs` (dizin, `UpdateDraftAsync` — `:383-434`, yeni `ChangeBrandAsync`, sayfa sorgularında süzgeç), `BrandRepository.cs` (`GetByIdsAsync`).
- `Diten.MdmService.Application/Features/ProductItemSkuMaster/` — `ProductItemSkuMasterModels.cs`; `Commands/CreateGlobalProductDraftCommand.cs`; `Handlers/CommandHandlers/CreateGlobalProductDraftHandler.cs` (`:222` varlık kurulumu); `Validators/CreateGlobalProductDraftValidator.cs`; `Lifecycle/Commands/UpdateGlobalProductDraftCommand.cs`; `Lifecycle/Handlers/CommandHandlers/UpdateGlobalProductDraftHandler.cs`; `Lifecycle/Validators/UpdateGlobalProductDraftValidator.cs`; `Queries/GetGlobalProductsQuery.cs`; `Handlers/QueryHandlers/GetGlobalProducts*Handler.cs`, `GetGlobalProductByIdHandler.cs`, `GetGlobalProductSelectorHandler.cs`; yeni `Lifecycle/Commands/ChangeGlobalProductBrandCommand.cs` + işleyici + doğrulayıcı; yeni `Queries/GetGlobalProductBrandOptionsQuery.cs` + işleyici + doğrulayıcı.
- `Diten.MdmService.Application/Features/Product/ProductBrandLink.cs` — **yalnız okunur / çağrılır**, değişmez.
- `Diten.MdmService.Api/Controllers/GlobalProductsController.cs` — iki yeni uç; mevcut uçlar aynı.
- `Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs` — Karar a.
- Testler: `services/Diten.MdmService/tests/Diten.MdmService.Api.Tests/` (yeni `GlobalProductBrandLinkHttpTests.cs`), `Diten.MdmService.Application.Tests/`.
- `tests/architecture/audit-ledger/Diten.MdmService.md` — yalnız satır **silme** (T-3) ya da iz belirtecine üye ekleme (T-2).

Ön yüz (`frontend/Diten.Web/`):
- `Controllers/GlobalProductsController.cs` — form alanı beyaz listesi (`:182`, `:266`), liste sorgusu beyaz listesi (`BuildListQuery`, `:100`), iki yeni vekil uç.
- `Models/GlobalProducts/GlobalProductViewModels.cs`.
- `Views/MasterDataManagement/GlobalProducts/_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, `_DataTable.cshtml`, `_Filter.cshtml` (marka süzgeci), `_IndexL10n.cshtml`.
- `wwwroot/assets/js/MasterDataManagement/GlobalProducts/index.js`, `index.l10n.js`.
- `Resources/Views/MasterDataManagement/GlobalProducts/GlobalProductsIndex.{en,tr,fr,es,zh,ar,ru}.resx`.
- Testler: `frontend/Diten.Web.Tests/Controllers/` (mevcut `GlobalProductListComponentProxyTests.cs`, `GlobalProductsIndexL10nTests.cs` kalıbı), `frontend/Diten.Web/tests/` (vitest; mevcut `global-products-register.test.js`).

## 6. Protected Paths

- `.antigravity/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration-agent; bu pakette değişiklik gerekmez — §16)
- Eski "Ürünler" kaydı: `Domain/Entities/Product.cs`, `Features/Product/**` (yalnız `ProductBrandLink` okunur), `Features/BrandProductContract/**`, `Api/Controllers/ProductsController.cs`, `BrandProductContractController.cs` ve `mdm_products` koleksiyonu
- Marka kaydı: `Domain/Entities/Brand.cs`, `Features/Brand/**`, `BrandsController.cs`, `BrandProductMasterManifestProvider.cs`
- `frontend/Diten.Web/Views/MasterData/**`, `Controllers/MasterData*` (Markalar / Ürünler ekranları)
- `services/Diten.CrmService/**`, `frontend/Diten.Web/Controllers/CRM/**` (tüketici; değişmez)
- `services/Diten.AuthService/**` ve `services/Diten.Platform/**` — yalnız §0.3 T-2 ve §14'teki adlı satırlar, XMC-001 onayıyla (bkz. §7)
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN), `Views/Archive/**`, `Controllers/Archive/**`
- `docs/roadmap/backlog/page-help-ledger.md` — bu pakette satır eklenmez (§19)

## 7. Dependencies

| Bağımlılık | Ne kullanılır | Durum |
|---|---|---|
| FU02 `Brand` | `IsLinkableAt` (`Brand.cs:42-47`), `ProductBrandLink.Refusal` (`ProductBrandLink.cs:12-22`), kodlar `brand_not_found` / `brand_archived` / `brand_not_linkable` (`BrandProductSharedModels.cs:26`, `:30`, `:55`) | kodda var |
| FU03 kapsam bekçisi | Global Ürün okuma / yazma uçları kapsam dışı ürüne 404 verir (`UpdateGlobalProductDraftHandler.cs:62-77`, `GetGlobalProductByIdHandler.cs:41-66`) | kodda var; yeni uçlar aynısını kullanır |
| MOD-0021 Platform denetim | Güvenilir kaynak ucu + `TrustedSourceAuditIntentOperationMap.cs:26` | yeni işlem için bir satır gerekir (T-2) → **XMC-001 onayı** |
| MOD-0018 / Auth | Yeni anahtar `mdm.global-products.update-brand` katalog → Auth eşitlemesiyle; Global Ürün izin profili Auth'ta (`ProductIdentityLifecycleEntitlementGrantProfile.cs:14-21`) | yeni satır gerekir (Karar a) → **XMC-001 onayı** |
| CRM W10 | `api/global-products/selector` ve `api/global-products/{id}` okuyucuları (`SegmentAttributeValueSource.cs:40`, `MdmSegmentProductReferenceValidator.cs:55`, `MdmStrategyTemplateReferenceValidator.cs:57`, `MdmProductNameReader.cs:40`; Web CRM vekilleri `StrategyTemplatesController.cs:339-341`, `ObjectionResponsesController.cs:245`, `KnowledgeConceptsController.cs:373`) | tüketici; cevaba yalnız alan eklenir |
| MOD-0023 onay motoru | — | Karar (a) önerisiyle **gerekmez** |

## 8. Runtime Constraints

- Ağ geçidi 5000 → MDM 5059; Web yalnız kendi vekil uçlarından ağ geçidine gider (`GlobalProductsController.cs` Web, `:74`, `:169`, `:173`).
- Yanıt `Response<T>` zarfı; hata kodları mevcut biçimde (`brand_not_linkable: …` önekli ilk hata — `BrandFix2HttpTests.cs:139` kalıbı).
- Yumuşak silme: `GlobalProduct` mevcut `ActiveTenantFilter` (`GlobalProductRepository.cs:421`) — değişmez.
- Eşzamanlılık: her yazım `ExpectedVersion` CAS; yazım `Version`'ı bir artırır (`GlobalProductRepository.cs:423`, `:433`).
- Tekrar güvenliği: taslak düzenleme ve marka komutu `Idempotency-Key` (GUID "D") ister (`GlobalProductsController.cs:63`, `:70-73`).
- Tanımsız istek alanı 400 kalır (`[JsonExtensionData]` + `HasUnknownFields` — `GlobalProductsController.cs:66`, `:180-181`; oluşturmada `UNKNOWN_WRITE_FIELD_FORBIDDEN` — `CreateGlobalProductDraftValidator.cs:33-35`). `brandId` modele **açıkça** eklenir; `brandCode` / `brandName` istekte kabul edilmez.
- Marka okuma: sayfa başına **tek** toplu marka sorgusu (N+1 yok); sayfa boyutu üst sınırı mevcut doğrulayıcıdan.
- `EntityBase` gerekçesi: kiracıya ait kayıt; `GlobalEntity` değil.

## 9. Layout & Shell Contract

- `shell: tenant` → `Views/MasterDataManagement/GlobalProducts/Index.cshtml` `Layout = "_LayoutTenantShell"` (ölçüldü `Index.cshtml:7`); yeni görünüm dosyası yok.
- Rota `/MasterDataManagement/GlobalProducts` (manifest `ProductItemSkuMasterManifestProvider.cs:87`).
- Yetkisiz yüz (UAS-001) mevcut davranış (Web `GlobalProductsController.cs:54-62`); değişmez. Marka seçimi, izni olmayan kullanıcıya çizilmez.

## 10. Backend File Convention

Mevcut `ProductItemSkuMaster` özelliği klasör düzeni korunur (Golden Reference Slim'in Commands / Queries /
Handlers/CommandHandlers / Handlers/QueryHandlers / Validators ayrımı; yaşam döngüsü komutları `Lifecycle/` altında —
ölçülen yerleşim). Yeni dosyalar:

- `Lifecycle/Commands/ChangeGlobalProductBrandCommand.cs` → `Lifecycle/Handlers/CommandHandlers/ChangeGlobalProductBrandHandler.cs` → `Lifecycle/Validators/ChangeGlobalProductBrandValidator.cs`
- `Queries/GetGlobalProductBrandOptionsQuery.cs` → `Handlers/QueryHandlers/GetGlobalProductBrandOptionsHandler.cs` → `Validators/GetGlobalProductBrandOptionsValidator.cs`
- DTO'lar tek dosyada: `ProductItemSkuMasterModels.cs`.
- Adlarda `Command` / `Query` soneki yalnız kayıtta; işleyici / doğrulayıcıda yok.
- Bağlanabilirlik kuralı **kopyalanmaz**: `ProductBrandLink.Refusal` çağrılır (tek kural, tek ret kodu).

## 11. Frontend File Contract

Slim (mevcut ekran; ölçüldü: `_CreateEditOffcanvas.cshtml` + `_DetailsQuickView.cshtml` + `_ListShell`):

| Dosya | Değişiklik |
|---|---|
| `_CreateEditOffcanvas.cshtml` | Ad alanının altına marka Select2 (`#globalProductBrand`, `select2`, boş seçenek "Marka yok"); kaynak `api/brand-options`. Birincil düğme `data-dt-primary="enter"` kalır (`:39`) |
| `_DetailsQuickView.cshtml` | "Marka" satırı (`oc-brand`: `KOD · Ad`, yoksa "—") |
| `_DataTable.cshtml` | "Marka" sütunu (sıralanamaz; `orderBy` beyaz listesi değişmez — `GetGlobalProductsQuery.cs:20-26`) |
| `_Filter.cshtml` | Marka süzgeci (Select2, aynı seçenek ucu + "Markasız" seçeneği — UNVERIFIED: "markasız" süzgeci ihtiyacı sahip / CRM'e sorulur; dilim 1 en az `brandId` süzgecini taşır) |
| `index.js` | Select2 başlatma (panel içinde `dropdownParent`), taslak düzenlemede mevcut markayı doldurma, onaylı üründe `CHANGE_BRAND` satır eylemi (Karar a) — sunucunun `availableActions` listesinden |
| `index.l10n.js`, `_IndexL10n.cshtml` | yeni anahtarlar |
| `GlobalProductViewModels.cs` | `BrandId` (create), `BrandId` / `BrandCode` / `BrandName` (liste / detay / seçici) |
| `GlobalProductsIndex.*.resx` (7) | yeni anahtarlar (§17 dil maddesi) |

Satır içi CSS yok (FG-003); gerekirse sınıf `backbone-custom.css`'e.

## 12. Validation Rules

| Alan | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| `BrandId` (oluşturma) | Hayır | GUID; `Guid.Empty` → 400; kiracıda yoksa 404 `brand_not_found`; arşivli 409 `brand_archived`; bağlanamaz 409 `brand_not_linkable` | — | `IBrandRepository.GetByIdAsync` (kiracı süzgeçli — `BrandRepository.cs:28`) + `ProductBrandLink.Refusal` |
| `BrandId` (taslak düzenleme) | Hayır (T-4: yoksa değişmez) | Aynı; **değişmeyen** değer yeniden doğrulanmaz; `null` bağı kaldırır | — | aynı |
| `BrandId` (onaylı ürün komutu, Karar a) | İstekte alan zorunlu (`null` = kaldır) | Aynı; aynı değer → 200, yazım yok (sürüm artmaz) — **öneri**, CT onayı | — | aynı + durum `IdentityApproved` ve `ActiveLifecycleOperation == null` |
| `ExpectedVersion` | Evet (yazımlarda) | ≥ 0 | CAS | — |
| `Idempotency-Key` | Evet (düzenleme, marka komutu) | GUID "D", boş değil | tekrar araması (`FindReplayAsync`) | — |
| `brandId` (liste süzgeci) | Hayır | GUID; bozuk → 400 | `(TenantId, BrandId)` dizini | — |
| `search` / sayfa (brand-options) | Hayır | mevcut seçici doğrulayıcısıyla aynı sınırlar (`GetGlobalProductSelectorValidator`) | `mdm_brands` mevcut dizinleri | — |
| `brandCode`, `brandName`, `TenantId` istekte | — | 400 (tanımsız alan) | — | `[JsonExtensionData]` |

İlişkili kural: bağlanabilirlik `DateTimeOffset.UtcNow` yerine işleyicinin `TimeProvider`'ıyla ölçülür (taslak işleyicisi
zaten `TimeProvider` alır — `UpdateGlobalProductDraftHandler.cs:25`); eski "Ürünler" işleyicisi `UtcNow` kullanıyor
(`UpdateProductHandler.cs:140`) — o dosyaya dokunulmaz.

## 13. Failure Path to Verify (HTTP, gerçek geçici mongod)

| Durum | Beklenen |
|---|---|
| Arşivli markayla oluşturma / taslak düzenleme / onaylı ürün komutu | 409 `brand_archived`; yazım yok, sürüm aynı |
| Taslak, pasif ya da geçerlilik penceresi dışındaki markayla | 409 `brand_not_linkable`; yazım yok |
| Başka kiracının marka kimliği | 404 `brand_not_found` (ifşa yok — yabancı kimlik "yok" sayılır); yazım yok |
| Başka kiracının Global Ürün kimliği / kapsam dışı Global Ürün | 404 `GLOBAL_PRODUCT_NOT_FOUND` (mevcut — `UpdateGlobalProductDraftHandler.cs:68-77`) |
| İstekte tanımsız alan (`brandCode`, `brandName`, `foo`) | 400 (`PRODUCT_IDENTITY_LIFECYCLE_REQUEST_INVALID` düzenlemede — `GlobalProductsController.cs:183-184`; `UNKNOWN_WRITE_FIELD_FORBIDDEN` oluşturmada) — `brandId` eklendikten sonra da |
| `brandId = 00000000-…` | 400 |
| Eski sürümle yazım | 409 `GLOBAL_PRODUCT_CONCURRENCY_CONFLICT` (`GlobalProductRepository.cs:1599`); sessiz üzerine yazma yok |
| Aynı `Idempotency-Key` ile tekrar | ilk cevap, ikinci yazım yok, ikinci denetim niyeti yok |
| Onay bekleyen / emekli / işlem süren Global Ürün'de marka komutu | 409 `GLOBAL_PRODUCT_STATE_CONFLICT` (`GlobalProductRepository.cs:1605`) |
| Onaylı üründe taslak düzenleme ucuyla marka | 409 (mevcut taslak süzgeci — `GlobalProductRepository.cs:424-426`) |
| Kimliksiz | 401; tenant başlığı yoksa servis geneli 400 "Missing Tenant" (FU04 §13 ölçümü) |
| İzinsiz (`update-brand` yok) | 403; ekranda `CHANGE_BRAND` eylemi çizilmez (sunucunun `availableActions` listesi) |
| Bağlı marka sonradan arşivlendi | Global Ürün okunur, `brandId` + kod + ad döner; aynı değerle düzenleme 200 (yeniden doğrulanmaz); başka arşivli markaya taşıma 409 |
| Marka belgesi okunamıyor (silinmiş işaretli) | 200; `brandId` dolu, `brandCode` / `brandName` `null` |
| Mongo işlemi çalışamıyor | 503 `ProductIdentityStore.Unavailable` (mevcut — `UpdateGlobalProductDraftHandler.cs:185`) |

## 14. Authorization Convention

```text
Policy:     [Authorize]                                   // shell: tenant, MdmService
Permission: [HasPermission("mdm.{resource}.{action}")]    // PKS-001: küçük harf, noktalı, >= 3 parça, kebab-case
Actor type: tenant_user (platform_admin mevcut çalışma zamanı davranışıyla geçer)
```

| Uç | İzin | Durum |
|---|---|---|
| `GET api/global-products`, `/{id}`, `/selector` | `mdm.global-products.read` | mevcut (`GlobalProductsController.cs:27-42`) |
| `POST api/global-products/drafts` (+marka) | `mdm.global-products.create` | mevcut |
| `PUT api/global-products/{id}` (+marka, yalnız Draft) | `mdm.global-products.update` | mevcut |
| `GET api/global-products/brand-options` | `mdm.global-products.create` · `.update` · `.update-brand` (biri yeter) | yeni (T-1) |
| `POST api/global-products/{id}/brand` | **`mdm.global-products.update-brand`** | yeni — **Karar (a) bekliyor** |

**Anahtar adı — ölçüm.** PKS-001 iki biçime de izin verir: `≥ 3` parça (`permission-key-standard.md:30`, `:50`) ve
Tier-3 modül eylemi (`:127-131`). Platform doğrulayıcısı `≥ 3` parçaya genişletilmiş (`ModulePageDescriptorNormalizer.cs:66-73`).
Öneri **`mdm.global-products.update-brand`** (3 parça, Tier-3): bu modülün mevcut anahtarları aynı biçimdedir
(`request-correction`, `request-retirement` — `ProductIdentityLifecycleModels.cs:11-12`; `mdm.gskus.edit-stock-behaviour` —
`ProductIdentityLifecycleEntitlementGrantProfile.cs:41`). Dört parçalı `mdm.global-products.brand.update` de geçerlidir ama
MDM'de dört parçalı tek anahtar `mdm.product-identity.lifecycle-operations.recover`'dır; tutarlılık için üç parça önerilir.
Tier-3 eylemi bu pakette kayıtlıdır (PKS-001 §5).

- Okuyucu (CRM) yeni izin istemez: marka kodu / adı `mdm.global-products.read` ile gelir (Karar b).
- Seçenek ucu marka okumak için `mdm.brands.read` istemez (T-1).
- Rol tohumu bu pakette yok. Yeni anahtar: manifest → katalog → Auth eşitlemesi + Auth'taki Global Ürün izin profiline bir
  satır (XMC-001 onayıyla).

## 15. Audited Events (AUD-001)

Servisin denetim altyapısı var: Global Ürün yazımları güvenilir kaynak niyetiyle (`mdm-guvenilir-kaynak-niyeti`, yol b)
denetlenir; belirteçte `IGlobalProductRepository.UpdateDraftAsync` yazılı (`audit-ledger/Diten.MdmService.md:14`).
Okuma sorguları (liste, detay, seçici, brand-options) denetlenmez (kişisel veri yok, ana veri okuması).

| Komut | Olay adı | Nesne | Yol | Önceki / sonraki ya da değişen alanlar | İstisna |
|---|---|---|---|---|---|
| `CreateGlobalProductDraftCommand` (+marka) | `global-product.draft-created` (`GlobalProductDraftCreated = 5`) | GlobalProduct | b — **T-3 kararıyla**; aksi hâlde bilinen borçta kalır (`Diten.MdmService.md:62`) | Sonraki: kod, ad, `BrandId` | — (borç gerekçesi T-3) |
| `UpdateGlobalProductDraftCommand` (+marka) | `global-product.draft-updated` (`GlobalProductDraftUpdated = 38`) | GlobalProduct | b (mevcut niyet) | Önce / sonra: ad, **`BrandId`** — niyet kanıtına (`EncodeFacts`, `UpdateGlobalProductDraftHandler.cs:123-129`) marka önce / sonra eklenir | — |
| `ChangeGlobalProductBrandCommand` (Karar a) | `global-product.brand-changed` (öneri `GlobalProductBrandChanged = 120`, T-2) | GlobalProduct | b (niyet, durum yazımıyla aynı koşullu yazımda) | Önce / sonra: `BrandId`; aktör, sürüm | — |
| Reddedilen marka bağı (409 / 404) | ayrı olay yok | — | — | Ret cevapta (`errors`); durum değişmediği için niyet yazılmaz | Durum değiştirmeyen ret (yazım yok) — mevcut GP davranışı; AUD-001 §3 "reddedilen komut da kaydedilir" ile çelişip çelişmediği CT'ye sorulur (UNVERIFIED) |

Kayda yazılmayan alan yok (marka kodu / adı kayda değil, yalnız kimlik). Platform işlem haritasına yeni işlem yalnız
ekleme ile girer (T-2).

## 16. Gateway / API Routing Decision

**Karar: Gateway değişikliği gereksiz.**

- `ocelot.json:274-290` `/api/global-products` (`GET, POST, OPTIONS`) ve `:291-308` `/api/global-products/{everything}`
  (`GET, POST, PUT, OPTIONS`) → `localhost:5059`. Yeni uçlar (`GET …/brand-options`, `POST …/{id}/brand`) bu catch-all
  altına düşer. CRM'in kendi notu da aynı rotaya dayanır (`MdmStrategyTemplateReferenceValidator.cs:55`).
- Web 5000 üzerinden çağırır; servis portuna doğrudan gitmez.
- `ocelot.json` korumalı yol; bu paket yazmaz.

## 17. Acceptance Criteria

- [ ] `POST api/global-products/drafts` geçerli `brandId` ile 200/201; detay `brandId`, `brandCode`, `brandName` döner; Mongo belgesinde yalnız `BrandId` var (kod / ad yok).
- [ ] `brandId` olmadan oluşturma bugünkü gibi çalışır; belge `BrandId` taşımaz ya da `null` taşır; okuma `null` döner.
- [ ] Taslak düzenleme: `brandId` alanı yok → marka değişmez; `null` → kaldırılır; yeni değer → bağlanır; her biri ayrı HTTP testi.
- [ ] §13'teki her satır adlı bir HTTP testiyle ölçülür (arşivli, bağlanamaz, yabancı kiracı markası 404, tanımsız alan 400, eski sürüm 409, tekrar anahtarı, onay bekleyen / emekli 409).
- [ ] Liste `GET api/global-products?brandId={id}` yalnız o markanın Global Ürünlerini döner; FU03 kapsamı zorlanan modda da (`GetEnforcedLegalEntityScopePageAsync`).
- [ ] Liste, detay ve seçici her öğede `brandId`, `brandCode`, `brandName` taşır; marka yeniden adlandırılınca Global Ürün okuması yeni adı döner (kopya yok kanıtı).
- [ ] Liste sayfası marka adlarını **tek** toplu sorguyla çözer (test: depo çağrı sayısı sayfa başına 1).
- [ ] `GET api/global-products/brand-options` yalnız `IsLinkableAt(now)` olan markaları döner; arşivli / taslak / pasif / süresi geçmiş marka listede yok; `mdm.brands.read` olmayan ama `mdm.global-products.create` olan kullanıcı 200 alır.
- [ ] (Karar a) `POST api/global-products/{id}/brand` onaylı üründe 200, sürüm +1, denetim niyeti `GlobalProductBrandChanged`; taslakta 409; `update-brand` izni yoksa 403; detayda `CHANGE_BRAND` eylemi yalnız izin + `IdentityApproved` + işlem yokken.
- [ ] Mevcut uçların eski alanları aynı ad ve türle döner (geriye uyum testi: v-önceki cevap şekli alt küme).
- [ ] Eski kayıt değişmedi: `git diff` `Product.cs`, `Features/Product/**` (ProductBrandLink dahil), `Features/BrandProductContract/**`, `ProductsController.cs` için boş; mevcut Marka / Ürün testleri değişmeden yeşil.
- [ ] Ekran: oluştur / düzenle panelinde marka seçimi yalnız bağlanabilir markaları gösterir; seçimli kayıt detayda `KOD · Ad`, listede marka sütunu; Enter paneli kaydeder (açık Select2'de Enter seçim yapar — UXH-001 §3).
- [ ] `verify_datatable_page.py . --area MasterDataManagement --module GlobalProducts --reference slim --data-mode server` PASS ve `quality-gate-datatable`; dokunma protokolü (`--format gaps`) çıktısı raporda.
- [ ] `Views/MasterDataManagement/GlobalProducts/Index.cshtml` `Layout = "_LayoutTenantShell"` açıkça (değişmeden).
- [ ] 7 dil: `GlobalProductsIndex.{en,tr,fr,es,zh,ar,ru}.resx` anahtar kümeleri eşit (bugün 67 anahtar, eşit — ölçüldü); yeni anahtarlar yedisinde; hata kodu → cümle köprüsü `brand_not_found`, `brand_archived`, `brand_not_linkable` için 7 dilde.
- [ ] Yetkisiz yüz (UAS-001) değişmedi; izinsiz kullanıcıya marka seçimi / eylemi çizilmez; dar ekranda panel taşmaz; Arapça (RTL) panel doğru.
- [ ] Canlı kontrol (dev): Gateway üzerinden oluştur → markalı detay → listede süzgeç; CRM'in `api/global-products/selector` okuyucusu yeni alanlarla 200 (kırılma yok).

## 18. Test Expectations

- **HTTP gidiş-dönüş** (`MdmHttpHost` — gerçek MDM API'si süreç içinde, **kendi geçici mongod'u**; paylaşılan 27017'ye
  bağlanmayı reddeder — `MdmHttpHost.cs:26-50`, `:140`): yeni `GlobalProductBrandLinkHttpTests.cs`; §13 her satır bir test.
  27017 hiçbir testte kullanılmaz.
- **Mongo testleri:** dizin varlığı (`ix_mdm_global_products_tenant_brand`); alansız eski belge tohumlanır ve hem okuma
  (`null`) hem `brandId` süzgeci (eşleşmez) ölçülür; kiracı yalıtımı (başka kiracının markası 404, başka kiracının Global
  Ürünü süzgeçte görünmez).
- **Sabotaj (çapa tam bir kez, taban yeşil, kırmızı = adlı test):**
  1. Yeni işleyicilerde `ProductBrandLink.Refusal` çağrısı kaldırılır → `Create_with_not_linkable_brand_is_409` ve
     `Update_draft_to_archived_brand_is_409` KIRMIZI.
  2. Okuma modelinde marka adı çözümü kaldırılır → `Renamed_brand_shows_new_name_on_global_product_detail` KIRMIZI.
  3. Kiracı süzgeçli marka okuması yerine süzgeçsiz okuma → `Foreign_tenant_brand_id_is_404` KIRMIZI.
  4. `HasUnknownFields` kontrolü kaldırılır → `Unknown_field_brandName_is_400` KIRMIZI.
- **Uygulama testleri:** T-4 üç durumu (yok / `null` / değer) işleyici düzeyinde; denetim niyeti kanıtında marka önce / sonra.
- **Mimari:** `AuditTrailStandardTests` yeşil; yeni komut ya defterde izli ya da istisnalı değil, **denetimli** (T-2); T-3 kabul edilirse borç satırı silinir ve test bunu doğrular.
- **Web C#:** vekil form beyaz listesi `BrandId`'yi kabul eder, `BrandCode`'u reddeder; liste sorgusu `brandId`'yi geçirir (`GlobalProductListComponentProxyTests` kalıbı); `GlobalProductsIndexL10nTests` 7 dil.
- **vitest** (`frontend/Diten.Web/tests/`): marka Select2'si yalnız `brand-options` cevabını çizer; taslak düzenlemede mevcut marka seçili gelir; "Marka yok" `null` gönderir; `CHANGE_BRAND` eylemi yalnız sunucu listesinde varsa çizilir. Karşılaştırma ANSI'siz "→" / `+ "…"` satırlarıyla (proje notu BL-614).
- **RESX parite:** 7 dil anahtar eşitliği; tenant 7 dil kuralı.
- **Build:** MDM servis + Web + Gateway (değişmeden) PASS; MDM ve Web test projeleri; `tests/architecture`.
- **Canlı:** §17 son madde; yeşil test ≠ çalışan ekran.

## 19. Page Help (UXH-001)

- **Ölçüm:** Global Ürünler sayfasının (`GLOBALPRODUCTS`) yardım metni **yok** — `PageHelpResource.*.resx` içinde
  `Help.GLOBALPRODUCTS.*` anahtarı bulunmuyor; sayfa borç defterinde (`docs/roadmap/backlog/page-help-ledger.md:99`, tavan
  kiracı 246 — `:5`).
- **Bu dilimde:** güncellenecek mevcut metin yok; deftere satır eklenmez (sayfa zaten orada). Yeni sayfa açılmadığı için
  UXH-001 §2 "yeni sayfa metniyle gelir" kuralı tetiklenmez.
- **Öneri (sahip / CT seçer, §22 F-3):** dokunulmuşken metni yazmak — `Help.GLOBALPRODUCTS.Purpose`, `.Audience`,
  `.Step1…Step5` + `.Step{n}Title` (adımlardan biri "Marka seçin — yalnız etkin ve geçerli markalar listelenir"), 7 dil;
  defter satırı silinir ve başlık 246 → 245. Regresyon riski (yazılmazsa): 🟢.
- **Tur:** Global Ürün onaya gönderme beş tur akışından biridir (UXH-001 §5 madde 3); bugün sayfada `data-tour` /
  `DitenTour` yok (ölçüldü). Bu paket tur eklemez; tur yazıldığında marka adımı opsiyonel bir adım olur → "tur yok (bu pakette)".

## 20. Ready-for-dev Checklist

- [x] DCP-002 kimlik kapısı (FU05, tüm dallarda ölçüldü)
- [x] Golden Reference: mevcut ekran Slim + `_ListShell` (ölçüldü); `form_field_count` 1 → 2
- [x] Frontmatter zorunlu alanları dolu
- [x] Layout & Shell Contract — `Layout = "_LayoutTenantShell"` açık (`Index.cshtml:7`)
- [x] Validation Rules her alan için
- [x] Failure Path ≥ 4 senaryo (§13: 14 satır)
- [x] Authorization Convention + anahtar adı ölçümü
- [x] Gateway kararı: gereksiz (ölçüldü)
- [x] Audited Events tablosu her yazma komutunu bir kez içeriyor
- [x] Sayfa yardımı ölçüldü (defterde); tur: bu pakette yok
- [ ] **Karar (a)** — onaylı üründe doğrudan, denetimli marka değişikliği + `mdm.global-products.update-brand` (SAHİP)
- [ ] **Karar (b)** — okuma modeli kimlik + kod + ad, okuma anında (SAHİP)
- [ ] **Karar (c)** — dilim 1'de geri doldurma yok; eşleme W10 ile ayrı dilim (SAHİP)
- [ ] T-1 seçenek ucu (CT)
- [ ] T-2 `ProductAuditOperation` değeri + Platform harita satırı (CT) — XMC-001: "Platform'daki denetim haritasını da düzeltelim mi?"
- [ ] T-3 oluşturma denetim borcunu bu dilimde ödemek (CT)
- [ ] T-4 `brandId` yokluk anlamı (CT)
- [ ] Auth Global Ürün izin profiline `update-brand` satırı — XMC-001: "Auth'taki izin profilini de düzeltelim mi?" (Karar a onaylanırsa)
- [ ] CRM'e bilgi: okuma modeline üç alan eklenecek; seçicide marka süzgeci isteniyor mu (UNVERIFIED — sorulmadı)
- [ ] Paket `ready-for-dev` (CT)

## 21. Implementation Notes — dilimler (2–3 prompt)

| Sıra | Prompt | İçerik | Risk |
|---|---|---|---|
| 1 | **BE — bağ + okuma** | `BrandId` alanı + dizin; oluşturma / taslak düzenlemede marka (T-4), `ProductBrandLink.Refusal`; `IBrandRepository.GetByIdsAsync`; liste / detay / seçici okuma modeli; liste `brandId` süzgeci (iki sayfa sorgusu); `brand-options` ucu; denetim niyeti kanıtına marka; T-3 kabulse oluşturma niyeti; HTTP + Mongo testleri; sabotaj 1–4 | 🟡 GP deposu büyük ve işlemli (`GlobalProductRepository.cs` 1868 satır); FU03 kapsam bekçisi her yeni uca uygulanmalı |
| 2 | **BE — onaylı ürün marka komutu** (Karar a onaylanırsa; aksi hâlde düşer ya da MOD-0023'e yeniden yazılır) | `ChangeGlobalProductBrandCommand` + `ChangeBrandAsync` (CAS + niyet aynı yazımda, `IdentitySubmitReservations` kuralına uyum — `GlobalProduct.cs:13-15`); `BuildAvailableActions`'a `CHANGE_BRAND` (`GetGlobalProductByIdHandler.cs:81-107`); manifest eylemi + sürüm 1.3.0; T-2 Platform satırı ve Auth profil satırı (XMC-001 onaylarıyla, ayrı commit) | 🟡 iki başka servise birer satır |
| 3 | **WEB** | Panel Select2, detay, liste sütunu, süzgeç, `CHANGE_BRAND` paneli; Web vekil beyaz listeleri; 7 dil + hata köprüsü; vitest; verifier; canlı kontrol | 🟢 |

Prompt 1 ile 3 paralel yürüyebilir (3, 1'in cevap şekline bağlı; şekil bu pakette sabit). Prompt 2 yalnız Karar (a)'dan
sonra. Her prompt'un ilk satırı hedefini adlandırır; worktree yalnız bu dal; `git add -A` yok.

Dikkat notları (ölçülen):
- `ProductBrandLink` `Features/Product` ad alanındadır; Global Ürün özelliğinden çağrılması özellikler arası bir başvurudur
  (kopyalamaktan iyidir; ret mesajı "product links" der — `ProductBrandLink.cs:16`, `:22`; kod aynı kalır, mesaj Web'de
  köprüyle çevrilir).
- Taslak düzenleme `UpdateDraftAsync` depo sözleşmesi adı ve normalize adı doğrular (`GlobalProductRepository.cs:393-414`);
  marka parametresi eklenirken bu sözleşme doğrulamasına marka kimliği de girer.
- CRM okuyucularında `JsonUnmappedMemberHandling.Disallow` yalnız CRM istek modellerinde bulundu
  (`ContentEngagementJourneyRequests.cs:11`), MDM okuyucularında değil (grep). Okuyucuların eklenen alanlara toleransı testle
  ölçülmedi — UNVERIFIED; canlı kontrol maddesi (§17) bunu kapatır.

## 22. Follow-up Items

| # | Konu | Sahip | Regresyon riski |
|---|---|---|---|
| F-1 | Eski "Ürünler" → Global Ürün eşlemesi ve `Product.BrandId` göçü; CRM'in 4 eski yerinin geçişi (Karar c) | MDM + CRM, W10 ile | 🟡 |
| F-2 | Seçicide `brandId` süzgeci / sözleşmede marka (CRM W10 isterse) | MDM | 🟢 |
| F-3 | Global Ürünler sayfa yardım metni (7 dil) + defterden çıkış (§19) | MDM / CT | 🟢 |
| F-4 | Marka tescil listesi (P8 ikinci yarısı; BL-562 → RIM) | sonraki paket | 🟢 |
| F-5 | Onaylı üründe marka değişikliğine ikinci onay (BL-562; Karar a "hayır" derse dilim 2 bu olur) | MDM + MOD-0023 | 🟡 |
| F-6 | "Bu markanın Global Ürünleri" — marka detayında (`BrandsController` `/{id}/products` bugün eski kayıttan) | FU02 sahibi; XMC-001 | 🟢 |
| F-7 | `module-implementation-status.md` satırı — kod geldiğinde | uygulayan ajan | 🟢 |

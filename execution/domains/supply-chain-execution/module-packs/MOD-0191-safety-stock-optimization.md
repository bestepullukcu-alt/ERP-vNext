---
id: MOD-0191
name: Safety Stock Optimization
domain: supply-chain-execution
service: Diten.SafetyStockService
service_port: 5069
service_status: temporary
shell: tenant
golden_reference: none
entity_base: EntityBase
status: draft
owner: "Supply Chain Execution / Control Tower; human owner TBD"
branch: feature/sce/mod-0191-safety-stock-optimization
started: 2026-10-09
target: TBD
---

# MOD-0191 — Safety Stock Optimization

> Control Tower/Ali incelemesine sunulan yalnız hazırlık taslağıdır. MVP-4 içindeki SoR yalnız SafetyStockPolicy'dir. Ali ayrı geçici Diten.SafetyStockService:5069 kararını verdi; yöntem, eşik, onay yetkisi ve Demand v2 tüketici kuralı açık kalır. Bu pack kod, scaffold, rota, canlı entegrasyon veya ready-for-dev yetkisi vermez. MOD-0188'in açık dalı ve kodu kapsam dışıdır.

## 1. Module Summary

MOD-0191, yetkili planlayıcının Tenant × LegalEntity × SKU × Warehouse kapsamındaki güvenlik stoğu politikasını tanımlaması, girdileri ve sürümüyle açıklanabilir bir aday sonucu incelemesi, gerekçeli manuel değişikliği kaydetmesi ve yetkili onaydan sonra politikanın MRP tarafından tüketilmesi için sözleşme hazırlar. MVP-4 iş sonucu, güvenlik stoğu kararı ile stok gerçeği, brüt talep ve ikmal önerisi arasındaki sahiplik çizgisini korumaktır. Hedef aktörler planlayıcı, onaylayıcı ve yetkili denetçidir. Yöntem, hedef hizmet seviyesi, eşiği, hesap periyodu ve otomatik çalışma kararı ürün sahibi tarafından TBD'dir; bu taslak bunlardan hiçbirini seçmez.

## 2. Ownership and Boundaries

- **Sahip:** SafetyStockPolicy ve onun onaylı politika sürümü, aday hesap sonucu, kullanılan girdi kaynağı/snapshot kimliği, gerekçeli manuel değişiklik ve karar izi. Policy, stok bakiyesi değildir.
- **Tüketir:** MOD-0173 availability; MOD-0290 ürün/SKU/UoM kimliği; MOD-0193 BOM yalnız üretim senaryosu kararlaştırılırsa; MOD-0188 yetkili Demand v2 snapshot'ı ancak tüketici kuralı ve sözleşme onaylanırsa; MOD-0189 ile güvenlik stoğu girdisi/çıktısı için ayrı entegrasyon kararı.
- **Sahiplenmez:** MOD-0173 stok hareketi/balance, MOD-0188 forecast veya yayın, MOD-0189 MRPRun/NetRequirement/ReplenishmentProposal, MOD-0141 PO, MOD-0193 BOM, MOD-0290 master, LegalEntity yetki kaynağı. Otomatik PO, stok rezervasyonu, stok değişikliği, v1 Demand'i v2 yerine kullanma ve MVP-6 servis scaffold'u bu pack'in dışındadır.
- Blueprint bağımlılık sırası 0188 → 0189 → 0191; Scenario Modeling ayrıca backbone kapısıdır. WP-MVP4 yalnız 0191 için SafetyStockPolicy SoR yazar; ayrıntılı algoritma tanımlamaz.

## 3. Owned Objects

| Nesne / yüzey | Taslak sözleşme ve karar kapısı |
|---|---|
| SafetyStockPolicy | Tenant ve sunucuda doğrulanan LegalEntity kapsamında SKU × Warehouse için sürümlü politika. Çakışma/etkinlik kuralı TBD; ikinci etkin politika otomatik seçilemez. |
| SafetyStockPolicyRevision | Önceki politikanın üzerine sessiz yazmadan, girdiler/yöntem/parametre kararı ve aktör-zaman-gerekçe içeren iş sürümü. Teknik EntityBase.Version ile karıştırılmaz. |
| SafetyStockEvaluation | Seçilmiş policy revision ve yetkili kaynak sürümlerine bağlı aday sonuç; sonuç miktarı, birim, kapsama, kaynak/tarih, uyarı ve kararın nasıl oluştuğu gösterilir. Hesap formülü TBD. |
| SafetyStockOverride / ApprovalDecision | Aday ile manuel sonucun ayrı tutulması; önceki/yeni değer, neden, aktör, zaman, onay/ret ve görev ayrılığı. Onay akışı ve rol sahibi TBD. |
| Komut niyetleri | Taslak oluştur/revize et, hesap iste, manuel değiştir, incelemeye gönder, onayla/reddet, etkinleştir/geri çek. Kesin komut adları ve durum geçişleri ürün/denetim kararı sonrasında dondurulur; hiçbiri bugün uygulanabilir değildir. |
| Sorgu/DTO niyetleri | Yetkili politikaları, revision ve aday ayrıntısını, girdi-lineage'ını ve karar geçmişini görüntüle. Tenant/LE/kapsam süzgeci sunucudadır. |
| API/route/permission | Kesin endpoint, izin anahtarı ve Gateway eşlemesi TBD; servis yerleşimi geçici Diten.SafetyStockService:5069; §14–15 kapıları geçmeden runtime literal/route üretilmez. |

## 4. Entity Fields

Aşağıdakiler iş sözleşmesi için önerilen asgari veri kategorileridir; tip/uzunluk/index ve hesap semantiği onay gerektirir. Tenant-owned kayıtlar EntityBase'in Id, TenantId, IsDeleted, DeletedAt, CreatedAt, UpdatedAt ve teknik Version alanlarını miras alır. TenantId, CreatedBy ve UpdatedBy istemciden alınmaz; aktör sunucuda çözülür. PolicyRevision iş sürümüdür, Version adı kullanılmaz.

| Alan / kategori | Tip | Kural / açık karar |
|---|---|---|
| LegalEntityId | canonical ID (tip TBD) | Sunucuda yetkili kümeden doğrulanır; MDM referans doğrulaması tek başına yetki değildir. |
| ItemId, SkuId, SkuLevel, WarehouseId | canonical ID / enum | PRODUCT-MASTER ve Location/Inventory kimlikleriyle eşleştirme ve SKU×Warehouse uygunluğu TBD; kopya master yok. |
| PolicyRevision, state, effective interval | sürüm/durum/tarih | Değişmez onaylı sürüm ve geçerlilik çakışması kuralı TBD; teknik Version ayrı. |
| MethodId, ParameterSet, ServiceObjective | sürümlü referans / değer | Hangi yöntem/parametre/hedefin destekleneceği ve geçerli aralıkları TBD. Varsayılan/formül/eşik uydurulmaz. |
| DemandRevisionId, demand state/integrity | kaynak kimliği/kanıtı | Demand v2 tüketici kararı gelmeden üretimde boş/kapalı; v1 otomatik ikame edilmez. |
| Availability snapshot/asOf, product/UoM, BOM version, lead-time source | kaynak kimliği, tarih, ölçü | Hangi kaynakların hesap için zorunlu olduğu ve snapshot tutarlılığı TBD; eksik/eski kaynakla sessiz öneri yok. |
| CandidateQuantity, FinalQuantity, BaseUomId | decimal / unit | Aday ve manuel/onaylı değer ayrı; miktar ve birim validasyonu onaylı yöntem/kaynak üzerinden. |
| Explanation, warnings, override reason, approval record | yapılandırılmış kanıt | Etken girdiler ve kaynak sürümleri, veri yeterliliği, önceki/yeni değer, aktör/zaman/gerekçe; birleşik belirsiz güven puanı yok. |

Tenant-first indeksler ve IsDeleted filtresi gerekir. Olası sorgu anahtarları TenantId + LegalEntityId + SkuId + WarehouseId + state; unique/partial index, revizyon geçerlilik çakışması ve retention politikası TBD. Hesap kaynağı snapshot'ları ikinci stok/talep SoR'u oluşturmaz.

## 5. Repo Scope

- **Bu hazırlık görevi:** yalnız execution/domains/supply-chain-execution/module-packs/MOD-0191-safety-stock-optimization.md.
- **Gelecekte ayrı onay gerektiren olası uygulama kapsamı:** services/Diten.SafetyStockService/** içinde yalnız MOD-0191 feature klasörü ve testleri; frontend/Diten.Web/Views/SupplyChain/SafetyStockOptimization/ ile ilgili controller, JS ve yedi dil RESX. Geçici servis/port kararı tek başına kod veya scaffold yetkisi değildir.
- Gateway, ortak izin kaydı, merkezî kontrat ve DCP uzlaştırması bu pack'in yazma kapsamı değildir; sahipleriyle ayrı görev gerektirir.

## 6. Protected Paths

Bu hazırlıkta pack dışındaki **bütün dosyalar** korunur. Özellikle AGENTS.md, .antigravity/**, execution/domains/supply-chain-execution/domain-config.md, DCP-009, registry, docs/analysis/contracts/**, gateway/Diten.ApiGateway/**/ocelot.json, frontend/Diten.Web/Views/Shared/_Layout.cshtml, Archive controller/view yolları, MOD-0188 dalı/kodu, services/** ve MVP-6 dosyaları değiştirilmez. Diğer domain servisleri gelecekte de MOD-0191 uygulama kapsamı dışındadır.

## 7. Dependencies

| Kaynak | Belgelenen durum | Taslakta izinli kullanım | Üretim kapısı |
|---|---|---|---|
| INVENTORY availability, MOD-0173 | inventory-bundle.openapi.yaml v1 **FROZEN**; dosya açıkça Prism mock kaynağıdır. GET /availability Item/SKU/Warehouse, LegalEntity, baseUom, asOf döndürür. | Sözleşmeye karşı fixture/mock ve salt okuma. | Gerçek MOD-0173 producer ve yetki/snapshot tutarlılığı doğrulanmadan canlı hesap/etkin policy tüketimi kapalı. Stok hareketi veya balance yazımı yok. |
| PRODUCT-MASTER, MOD-0290 | product-master-bundle.openapi.yaml v1 **FROZEN**, mock örneği var; kimlik/UoM MDM SoR'dur. | Fixture/mock ile SKU, item, UoM doğrulaması. | Gerçek producer, SKU/LegalEntity uygunluğu ve tarihsel UoM doğrulanmadan canlı tüketim kapalı. |
| BOM, MOD-0193 | bom.openapi.yaml v1 **FROZEN** contract; WP-MVP4 BOM'u **mock** diye niteler. | Üretim senaryosu kapsamı onaylanırsa fixture/mock; olmayan BOM'u sıfır bileşen sayma. | Gerçek effective BOM producer/versiyon/LE sözleşmesi ve ürün kararı olmadan canlı manufacturing etkisi kapalı. |
| Demand, MOD-0188 | demand.openapi.yaml **v1 FROZEN**. Açık MOD-0188 pack'i v2 producer semantiğini tarif eder; v2 merkezi freeze/yayın **DRAFT/açık kapı**, origin/main'de v2 contract yoktur. | V2 için yalnız ayrı etiketli taslak fixture; v1 ikame değil. | MOD-0191'in v2 tüketici kuralı, revision/status/integrity/asOf/invalidated davranışı ve ortak testleri CT/Ali tarafından onaylanmadan canlı tüketim kapalı; v2'yi bu pack dondurmaz. |
| MOD-0189 ve Scenario Modeling | Blueprint 0191 için 0189 + Scenario Modeling bağımlılığı yazar; main'de 0189 pack/üretim entegrasyonu ve Scenario Modeling tüketici kontratı bu incelemede kanıtlanmadı. | Bağımsız sözleşme taslağı ve mock sınırı. | MRP politika tüketim noktası, idempotency ve senaryo sahipliği onayı olmadan otomatik netting/proposal etkisi kapalı. |
| Platform LegalEntity kapsamı | MOD-0188 pack'inde tam yetkili şirket kümesi + dışlamalar, atama iptali ve MDM güven modeli açık canlı kapıdır. | Ayrı kapsam/izin fixture'ları. | Platform'un tam yetkili küme sözleşmesi ve iptal testi kabul edilmeden canlı LE seçimi kapalı. |
| Audit altyapısı | AUD-001 ortak iletici K4; geçici Diten.SafetyStockService'in kabul edilmiş izi henüz yok. | Audited Events taslağı. | Yazma komutu/approval ve K2 fail-closed kanıtı olmadan runtime kapalı. |

## 8. Runtime Constraints

MongoDB tek DB/tenant izolasyonu; her kayıtta TenantId, soft delete ve LegalEntity/data-scope sunucu doğrulaması gerekir. Başka tenant kaydı 404, kimlikle istenen kapsam dışı kayıt 404/boş; izin yoksa 403. Kaynak yok/bozuk, eşleşmeyen birim, geçersiz veya invalidated Demand revision, aynı politika için sürüm çatışması ve audit yazma hatası güvenli şekilde durur. Yanlış/yetersiz veriden sessiz sayısal politika üretilmez. Beş katman, CQRS/MediatR, Response<T>, CustomBaseController, Validation/Logging/Exception/Performance ve denetim davranışı seçilen serviste doğrulanır. Gateway 5000, frontend 5001; browser mikroservis portuna doğrudan gitmez.

**Kapı ayrımı:** Ali/CT izole geliştirme başlangıcını yalnız seçilmiş servis/port, sınırlı iş kapsamı, onaylı yöntem/formül, adlandırılmış mock/fixture sözleşmeleri, izin/veri kapsamı ve audit tasarımıyla açabilir. Kararsız hesaplama veya etkinleştirme davranışı kodlanmaz. Mock sonuçları canlı producer, yetkili LegalEntity kaynağı, Demand v2, Gateway veya audit kabulünün kanıtı değildir; bu yüzeyler §18 canlı kapısına kadar kapalı kalır. Gerçek secret kaynak dosyası, fixture, log veya pack içine konmaz.

### Geçici servis kararı ve port sınırı

Ali'nin kararı: MOD-0191 ayrı geçici Diten.SafetyStockService içinde, güncel main tabanından bağımsız geliştirme adayıdır. Bugün MOD-0188 dalına veya Diten.PlanningService koduna bağlanmaz; MOD-0188'in dalı/kodu değişmez. 5069 Ali tarafından onaylandı. Bu oturumdaki ölçümde 5069 için dinleyici yoktu ve loopback bind testi başarılıydı. Bu, yalnız ölçüm anındaki kullanılabilirliktir; kalıcı port tahsisi, servis scaffold'u veya üretim izni değildir. AGENTS.md ile .antigravity/rules/ports.md kayıtları merkezî sahiplerin **bekleyen ayrı işi**; Gateway rotası integration-agent'ın **bekleyen ayrı işi**dir. Hiçbiri bu pack ile yapılmış sayılmaz.

Birleşme **bugün yapılmaz ve hedefi bugün seçilmez**. Geçici servis sınırı korunur; aşağıdaki göreli maliyetler gün/para tahmini değil, henüz uygulanmamış taşıma etkisidir.

| Taşınacak etki | PlanningService — ancak uygun kod main'e geldikten ve sahipleri onayladıktan sonra | Nihai SupplyChainService — DCP-009 hedefi |
|---|---|---|
| MOD-0191 feature | **Orta:** aynı planlama bağlamı olası uyum sağlar; ayrı servis namespace ve bağımlılıkları yeniden bağlanır. | **Orta:** feature tek sahibinde kalır; 5061 hedefinin servis yapısına taşınır. |
| DI/config ve servis sınırı | **Orta:** PlanningService'in main'deki gerçek DI/config'iyle uyuşma ve geçici servisi kapatma gerekir. Bugünkü açık dal kanıt sayılmaz. | **Yüksek:** origin/main'de SupplyChainService scaffold'u yok; MVP-6 sırası, DI/config ve geçici servisi kaldırma ayrıca yönetilir. |
| İzin/entitlement | **Orta:** permission anahtarları ve tenant/LE/data-scope bağları yeni hostta yeniden doğrulanır. | **Orta:** aynı güvenlik sınırı 5061 hostuna taşınır; scope ve rol testleri yeniden çalışır. |
| Mongo koleksiyonları | **Yüksek:** tenant-first index, collection sahipliği, veri taşıma/geri dönüş ve audit lineage bütünlüğü ayrı migration ister. | **Yüksek:** aynı fiziksel veri ve audit lineage taşıma/uzlaştırma yükü vardır; ikinci stok SoR'u açılamaz. |
| Gateway route | **Orta:** geçici 5069 downstream'i yeni hosta yönlendirme, OPTIONS/auth/header ve eski rota kapatma gerekir; integration-agent işi. | **Orta:** 5061 downstream ve geçici rota kapatma aynı ayrı integration-agent kontrolünü ister. |
| Test/kanıt | **Yüksek:** fixture, tenant/LE, audit, Demand/stock ve handoff regresyonu yeni hostta tekrarlanır. | **Yüksek:** aynı testlere MVP-6/SupplyChainService regresyonu ve birleşme kabulü eklenir. |

PlanningService seçeneği MOD-0188 dalından kod kopyalama veya ona bugünkü bağımlılık kurma izni değildir. SupplyChainService seçeneği MVP-6 scaffold sırasını öne çekmez. Taşıma ve hedef kararı Ali/Control Tower ile servis sahiplerinin sonraki ayrı kapısıdır.

## 9. Layout & Shell Contract

shell: tenant. Önerilen ana yüzey politika, girdi kaynakları, aday açıklaması, manuel değişiklik ve karar geçmişini birlikte gösteren özel çalışma alanıdır; salt CRUD/DataTable varsayılmaz. Her Razor sayfası açıkça Layout = "_LayoutTenantShell" seçer; Views/SupplyChain/SafetyStockOptimization/ önerilen klasördür. Tenant dilleri en, tr, fr, es, zh, ar, ru; .resx ve window.L10n bridge gerekir. İzinsiz kullanıcıya başlık/kart/boş tablo çizilmez, tek açıklama gösterilir. Canlı LegalEntity seçici Platform'un yetkili tam kümesi/dışlamalar sözleşmesine bağlıdır; boş/doğrulanamayan kapsamda iş yüzeyi açılmaz.

## 10. Backend File Convention

Ayrı geliştirme onayı verilirse geçici Diten.SafetyStockService içinde MOD-0191 feature'ı Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/ ve SafetyStockOptimizationModels.cs düzeninde tasarlanır. Her command/query/handler/validator ayrı dosya; handler adında Command/Query/Request suffix yok. Dış kontrat istemcileri Application arayüzü, Infrastructure uygulaması üzerinden çalışır; handler doğrudan HTTP/DB kodu taşımaz. GoldenReferenceSlim/Compact gerçek kodu ve pack'leri okundu; bunların DataTable CRUD yüzeyi bu özel çalışma alanına otomatik kopyalanmaz. Geçici servisteki gerçek base/repository/audit altyapısı ayrıca doğrulanmadan uygulama dosya listesi tamamlanmış sayılamaz.

## 11. Frontend File Contract

Özel tenant çalışma alanı için beklenen yüzeyler: kapsamlı policy seçimi; revision/girdi kaynağı/uyarı ayrıntısı; aday ile manuel değerin yan yana açıklaması; gerekçe ve onay geçmişi. Ekran listesi, create/edit form alan sayısı ve etkileşim tasarımı ürün sahibiyle TBD. Bu nedenle golden_reference: none ve data_mode/form_field_count yoktur. DataTable liste ekranı sonradan onaylanırsa kullanıcı form alanları sayılır; ≤8 Slim, >8 Compact, veri modu ve gerçek Golden Reference dosya seti ekran bazında pack'e eklenir. Yüklenen her veri yüzeyinde içeriğin biçimine uygun skeleton; yükleme, boş, hata ve yetkisiz durumlar ayrıdır. Browser yalnız Gateway 5000 veya yetkili aynı-origin MVC proxy kullanır.

## 12. Validation Rules

| Alan / işlem | Zorunlu ön koşul | Hata / açık karar |
|---|---|---|
| Tenant/LegalEntity | Tenant sunucuda çözümlenir; seçilen tek LE Platform tam yetkili küme ve dışlamalar içinde her istekte yeniden doğrulanır. MDM lookup yalnız kimlik doğrular. | Kapsam belirsizse kapalı; başka tenant 404; aynı tenant yetkisiz LE create 403 / sıfır kayıt ancak yetkili sonuç sözleşmesi bu ayrımı doğrulayınca. |
| SKU/Warehouse/UoM | Canonical kimlikler ve birim eşleşir; payload master veya stok yetkisi sağlamaz. | Geçersiz/eksik kaynakla etkin sonuç yok. |
| Policy revision | Zorunlu alanlar, sürüm token'ı ve geçerlilik çakışması onaylı iş kuralına uyar. | Eksik 400, duplicate/çakışma 409; iş kuralı ayrıntısı TBD. |
| Hesap girdisi | Kaynak asOf/sürüm/bütünlük, yetkili Demand v2 durumu, availability, ürün ve gerekiyorsa BOM bağlanır. | Kaynak eksik/stale/invalidated ise açıklamalı kapalı sonuç; v1 fallback yok. |
| Method/parameter/threshold | Yalnız ürün sahibinin onayladığı sürümlü yöntem ve aralık geçerlidir. | Henüz onay yok: hesap/etkinleştirme kapalı; formül veya varsayılan değer yok. |
| Manuel değişiklik/onay | Gerekçe, önceki/yeni değer, aktör, zaman ve bağımsız karar izi; onay yetkisi/SoD TBD. | Eksik gerekçe/izin, eski sürüm veya audit başarısızlığında etki yok. |

## 13. Failure Path to Verify

- **Aynı kapsam/etkin dönem için çakışan policy:** İkinci kayıt/etkinleştirme 409; tek geçerli sürüm kuralı CT tarafından tanımlanana dek etkinleştirme kapalı.
- **Eksik yöntem veya kaynak:** 400 ya da kaynağa uygun kapalı hata; sayısal aday, stok hareketi ve MRP etkisi sıfır.
- **Yetkisiz kullanıcı/LE:** İzin yok 403; başka tenant veya kapsam dışı mevcut policy ID 404/boş; kapsam kaynağı arızası erişim açmaz.
- **Eşzamanlı revizyon/onay:** Eski teknik Version/state token 409; onay veya override sessiz overwrite yapmaz.
- **Demand v2 invalidated/eksik parça:** Aday ve henüz uygulanmamış tüketim bloke; v1'e veya eski revision'a sessiz dönüş yok; kesin yeniden değerlendirme protokolü TBD.
- **Audit yazma hatası:** GxP/K2 sınıfıysa politika değişikliği/onay durur; seçilen serviste doğrulanmış fail-closed yol yoksa komut uygulanmaz.

## 14. Authorization Convention

[Authorize] ve action bazlı [HasPermission] gerekir. Öneri namespace'i safety-stock.policy.read/create/update/submit/approve/activate ve safety-stock.evaluation.read/run, safety-stock.override.create; kesin anahtarlar, ModuleCode/entitlement ve rol matrisi CT/EA onayına bağlı TBD'dir. Policy yazma, değerlendirme, onay, Demand okuma ve inventory availability okuma izinleri birbirinin yerine geçmez. Actor type tenant_user; servis hesabı için ayrı teknik kimlik ve aynı Tenant/LE/data-scope sınırı gerekir. Veri kapsamı hangi alanlara çevrilecek (en az LegalEntity; SKU/Warehouse alt kapsamı TBD), dışlamalar ve tenant-wide izni/anahtarı Platform sözleşmesiyle kararlaştırılır. JWT claim'i ya da MDM lookup-validation kullanıcının şirket yetkisi değildir. Politika onaylayanı kendi değişikliğini onaylayabilir mi: TBD, onaylanmadan izin açılmaz.

## 15. Gateway / API Routing Decision

Karar TBD. MOD-0191 API resource adı, endpoint/method matrisi, mevcut catch-all ve Gateway güvenlik profili CT/integration-agent tarafından doğrulanmadan rota eklenmez. Geçici servis 5069 kararı Gateway route'unun eklenmiş veya merkezî port kaydının yapılmış olduğu anlamına gelmez. Gerekirse integration-agent explicit base ve /{everything} rotalarını PATCH/OPTIONS dahil ayrı görevle ekler. Frontend Gateway 5000'den geçer; ocelot.json protected path'tir. Demand/Inventory/Product/BOM tüketimi published contract üzerinden olur; başka modülün DB koleksiyonunu veya iç tipini paylaşmaz.

## 16. Acceptance Criteria

- [ ] AC-S01: SafetyStockPolicy dışındaki stok balance, forecast, MRP proposal ve PO kayıtlarında MOD-0191 yazıcısı yok; contract testinde inventory erişimi yalnız availability read'dir.
- [ ] AC-S02: Her policy/revision/aday/manuel karar Tenant ve sunucuda doğrulanmış LE ile sorgulanır; iki tenant ve iki LE verisi birbirine görünmez; kapsam daralınca eski UI seçimi doğrudan API'de erişim sağlamaz.
- [ ] AC-S03: Onaylı yöntem/parametre ve zorunlu kaynak kararı bulunmadığında run/activate hiçbir sayısal politika veya aşağı akış etkisi üretmez; eksik veri açık uyarı döndürür.
- [ ] AC-S04: Onaylı yöntem/formül için aynı sözleşmeli fixture ve aynı policy revision/kaynak snapshot'ı ile yinelenen izole değerlendirme aynı sonuç ve lineage'ı verir; sabit örnekler sayısal doğruluğu ölçer. Fixture kanıtı canlı kaynak veya AC kapanışı sayılmaz.
- [ ] AC-S05: Aday, manuel değişiklik ve onaylı son değer ayrıdır; her manuel değişiklikte eski/yeni değer, gerekçe, aktör, zaman ve revision görünür; eksik gerekçede kayıt yoktur.
- [ ] AC-S06: Revizyon/onay/eşzamanlılık çatışmasında 409 ve sıfır kısmi değişiklik; policy iş sürümü teknik Version'dan ayrı kalır.
- [ ] AC-S07: Demand v1 hiç çağrılmaz; v2 tüketici onayı gelince Published, Superseded, Invalidated, eksik sayfa/checksum, gecikmiş olay ve servis arızası için kapalı davranış ortak testle kanıtlanır.
- [ ] AC-S08: BOM gereken onaylı üretim senaryosunda yalnız effective BOM/version tüketilir; senaryo kararı yoksa BOM etkisi üretim kapalıdır.
- [ ] AC-S09: Tenant ekranları açıkça _LayoutTenantShell seçer; yedi dil RESX paritesi, yükleme/boş/hata/izinsiz yüzeyleri ve yalnız Gateway akışı browser smoke ile doğrulanır.
- [ ] AC-S10: Seçilen servis için her yazma niyeti AUD-001 izine bağlanır; GxP/K2 ise audit başarısızlığında işlem durur. K4 altyapısı yokken yazma komutu eklenmez. Mock audit tasarımı canlı kayıt ve kapalı-başarısızlık kanıtı yerine geçmez.
- [ ] AC-S11: MOD-0189'a güvenlik stoğu aktarımı yalnız onaylı, geçerli policy revision'ı ve kaynak bütünlük bilgisini kullanır; idempotency ve invalidation etkisi iki taraflı sözleşme ile test edilir. Sözleşme onaylanana kadar aktarım kapalıdır.

## 17. Test Expectations

Bu pack düzeltmesinde kod veya test çalıştırılmaz. **İzole geliştirme kanıtı** yalnız Ali/CT'nin sınırladığı ve ürün sahibinin yöntem/formülünü onayladığı dilim içindir: unit testleri sabit sayısal örnekleri, revision/override/onay kuralı ve açıklama alanlarını; sözleşmeli mock/fixture testleri yinelenebilir sonucu, eksik/bozuk kaynakta fail-closed davranışı, tenant-first Mongo filtre/soft delete/index/eşzamanlılığı, iki tenant ve iki LE ayrımını, ayrı Demand/stock/policy izinlerini ve görev ayrılığını ölçer. Gerçek secret fixture, kaynak dosyası veya logda kullanılmaz. AUD-001 kabul edilmiş yol yoksa yazma komutu testi onun eksikliğini telafi etmez: yazma kodu açılmaz; GxP/K2 ise kayıt hatasında sıfır iş etkisi kuralı korunur.

**Canlı kabul/AC kapanışı kanıtı** ayrıca gerçek Platform yetkili şirket tam kümesi/dışlama ve iptal sonrası erişim kesilmesini; onaylı Demand v2 producer ve MOD-0191 tüketici davranışını; gerçek Inventory/Product ve kapsamdaysa BOM üreticilerini; MOD-0189 handoff/fencing/idempotency'yi; Gateway'den geçen API'yi; yedi dil RESX, kullanıcı ekranı ve browser smoke'u; seçilen servisin gerçek audit pipeline'ında başarılı/reddedilmiş yazıların aktör/tenant/nesne/sonuç kaydını ve GxP/K2 audit arızasında işlemin durduğunu ölçer. Servis, frontend, gateway build ve architecture testleri bu aşamada raporlanır. Mock/fixture sonuçları bu canlı kanıtların hiçbirini tamamlamaz. DataTable verifier yalnız onaylı DataTable ekranı eklenirse çalışır.

## 18. Ready-for-dev Checklist

Bu iki grup farklı kapılardır. **A grubu**, Ali/CT'nin ayrıca izin vereceği sınırlandırılmış izole geliştirmeye başlamadan önce kapatılır; bu pack bugün draft'tır ve hazır ilan edilmez. **B grubu**, gerçek entegrasyonla canlı kabul ve ilgili AC'lerin kapanması içindir; A grubunda kullanılan mock/fixture B grubunu geçirmez. A grubu onayı kararsız davranışı veya denetimsiz yazmayı açmaz.

### A — İzole geliştirmeye başlama kapısı

- [x] DCP-002 MOD-0191 / Safety Stock Optimization preflight exit 0; MOD-0188'den ayrı Blueprint modülü.
- [x] Pack draft, MVP-4 SafetyStockPolicy SoR ve sınırlar kaydedildi.
- [x] Ali ayrı geçici Diten.SafetyStockService:5069 ve güncel main'den bağımsız MOD-0191 yerleşimini kararlaştırdı; frontmatter service/service_port/service_status güncellendi. MOD-0188 dalı/PlanningService kodu bugünkü bağımlılık değildir.
- [ ] Ali/Control Tower uygulanacak sınırlı iş dilimini ve gelecekteki birleşme kapısını ayrıca netleştirdi; insan owner ve gerçek target tarihi belirlendi. 5069'un merkezî AGENTS.md/ports.md kaydı ve Gateway route'u ayrı bekleyen işlerdir; izole geliştirme onayı veya canlı kabul gibi işaretlenmez.
- [ ] Ürün sahibi yalnız uygulanacak dilim için yöntem/formül, parametre/eşik, veri yeterliliği, politika kapsamı ve sürüm/çakışma kararını onayladı. Karar verilmeyen hesaplama veya etkinleştirme kodu açılmaz; yöntem ve ürün kararı bu taslakta TBD'dir.
- [ ] Kullanılacak INVENTORY, PRODUCT-MASTER ve koşullu BOM frozen sözleşmelerine karşı mock/fixture kapsamı ve kaynak eksikliği davranışı yazıldı. Demand v2 taslak fixture'ı v1'den ayrı etiketlendi; tüketici kuralı onaylanmadıkça v2'ye bağlı hesap/etkinleştirme kodu açılmaz. Mock canlı kaynak kanıtı değildir.
- [ ] Tenant/LegalEntity izolasyonu, Platform yetkili kümesine bağlanacak veri kapsamı arayüzü, dışlamalar, ayrı policy/Demand/stock işlem izinleri ve görev ayrılığı tasarlandı; izole testte fail-closed kanıtlandı. JWT claim'i veya MDM lookup kullanıcıya şirket yetkisi vermez.
- [ ] Her planlanan yazma için §21 AUD-001 yolu ve GxP/K2 sınıflaması tasarlandı. Seçilen serviste kabul edilmiş audit altyapısı yokken K4 uyarınca yeni yazma komutu kodlanmaz; K2 ise fail-closed yol doğrulanmadan açılmaz. Gerçek secret hiçbir kaynak, fixture, log veya pack'e konmaz.
- [ ] Scenario Modeling bağımlılığı uygulanacak sınırlı dilimde ya onaylı sözleşmeyle tanımlandı ya da açıkça kapsam dışı bırakıldı. Sınırlı endpoint/validation/test beklentisi ve UI gerekiyorsa form alanları kararlaştırıldı; DataTable çıkarsa Slim/Compact ve data_mode ayrıca seçildi.
- [ ] Ali/Control Tower pack'i ve yalnız bu izole başlangıç kapısını ayrıca inceledi; olası status kararı açıkça kaydedildi. Bu checklist tek başına status değiştirmez.

### B — Canlı kabul ve AC kapanışı kapısı

- [ ] Platform'un gerçek yetkili LegalEntity tam kümesi/dışlamaları ve pozisyon ataması iptalinden sonra erişimin kesilmesi testle kanıtlandı; MDM referans güven modeli ve SKU/Warehouse veri kapsamı kabul edildi.
- [ ] Demand v2 producer sözleşmesi merkezce onaylanıp yayımlandı; MOD-0191 dönem/revision/status/bütünlük/invalidation tüketici kuralı ve ortak testleri kabul edildi. Demand v1 fallback yok.
- [ ] Gerçek MOD-0173 INVENTORY availability ve MOD-0290 PRODUCT-MASTER üreticileri; üretim senaryosu kapsamdaysa gerçek MOD-0193 BOM üreticisi, birim/snapshot ve hata yollarıyla doğrulandı. Fixture bu maddeyi kapatmaz.
- [ ] MOD-0189'a yalnız onaylı/geçerli policy revision'ı devreden handoff, idempotency ve invalidation davranışı iki taraflı sözleşme ve gerçek entegrasyonla kanıtlandı.
- [ ] Gateway rota/izin davranışı, tenant kullanıcı ekranı, yedi dil RESX, yetkisiz yüzey, doğrudan API/data-scope ve browser smoke gerçek yol üzerinden doğrulandı.
- [ ] Seçilen servisin canlı audit pipeline'ı başarılı ve reddedilmiş yazıları kalıcı kaydetti; görev ayrılığı ve GxP/K2 audit arızasında işlemin durması ölçüldü. Mock audit veya yalnız işaret testi kabul değildir.
- [ ] İlgili servis/frontend/gateway build, architecture ve sözleşme testleri geçti; AC-S01–S11 gerçek kaynak gerektiren maddeleriyle kapandı. Ali/CT canlı kabulü ayrıca kaydetti.

## 19. Implementation Notes

Kaynaklar: WP-MVP4-planning; DCP-009; supply-chain domain-config; inventory capability report §14.8/15/21; dört OpenAPI sözleşmesi; açık MOD-0188 dalındaki pack (salt okunur). Ali MOD-0191 için ayrı geçici Diten.SafetyStockService:5069'u onayladı; DCP-009 SupplyChainService/5061'i nihai servis olarak kaydeder, MOD-0188 pack'i geçici PlanningService/5068 anlatır. MOD-0191 bugün bu iki servisten birine bağlanmaz; sonraki birleşme hedefi ayrı karardır. origin/main'de izlenen PlanningService veya SupplyChainService scaffold'u yok; checkout'ta görülebilen izlenmeyen/ignored build dosyaları main kod kanıtı değildir.

Üretici kalite karşılaştırması, tasarım ilkesi düzeyindedir: [SAP IBP Inventory Optimization](https://help.sap.com/docs/SAP_INTEGRATED_BUSINESS_PLANNING/252c83bbf8184915a56d4cdd0917ef06/cdbbdf46dc8d4212b97d0255f0af93a9.html) demand/supply değişkenliği, lead time ve hizmet hedefini ayrı sürücüler olarak ele alır; [Oracle Safety Stock Planning Methods](https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/25d/faspf/safety-stock-planning-methods.html) yöntem seçimi ile kullanıcı override'ını ayırır. Buradan yalnız açıklanabilir girdi ve manuel karar ayrımı alınır. Bu repo için çok kademeli optimizasyon, istatistiksel yöntem, servis seviyesi, varsayılan eşik veya otomatik override davranışı kararlaştırılmış değildir; SAP/Oracle formülü kopyalanmaz.

## 20. Follow-up Items

**İzole geliştirme başlangıcı için:**

1. Ali'nin ayrı geçici Diten.SafetyStockService:5069 ve MOD-0188'den bağımsız main tabanı kararı kaydedildi. Ali/Control Tower sınırlandırılmış iş dilimini ve sonraki birleşme hedefini ayrıca kararlaştırsın. 5069'un AGENTS.md/ports.md kalıcı kaydı merkezî sahiplerin; Gateway route'u integration-agent'ın bekleyen ayrı işidir; bu pack bunları tamamlamaz.
2. Ürün sahibi: uygulanacak dilimde hangi SKU/Warehouse/LE kapsamı, politika hedefi, yöntem/formül, hesap girdisi ve kalite eşiği onaylı? Manuel override'ın onay rolü, görev ayrılığı ve etkinlik/retention kuralı nedir? Kararsız hesaplama/etkinleştirme kodu açılmaz.
**Canlı kabul ve AC kapanışı için:**

3. Demand sahibi ve MOD-0189 tüketicisi: MOD-0191 v2'de hangi dönem/revision'ı seçecek; Published/Superseded/Invalidated geçişi, tam snapshot/checksum, olay gecikmesi ve yeniden hesaplama nasıl işleyecek?
4. Platform/MDM sahipleri: yetkili şirket tam kümesi/dışlama/iptal, referans doğrulama güven sınırı, Warehouse–LE ve SKU kapsamı.
5. Inventory/Product/BOM sahipleri: gerçek producer ve birim/snapshot doğrulaması; Scenario Modeling sahibi: bağımlılık sözleşmesi.
6. Security/audit sahibi: izole başlangıç için permission/veri kapsamı ve audit tasarımını; canlı kabul için gerçek audit iletici, görev ayrılığı ve GxP/K2 arıza kanıtını tamamlasın. UI sahibi: ekran alanları ve Golden Reference kararını verip canlı kullanıcı ekranını doğrulasın. Gerçek secret hiçbir aşamada kaynak/fixture/log içine girmez.

## 21. Audited Events

Bunlar yalnız **komut niyetidir**, uygulanabilir komut listesi veya geçerli audit yolu değildir. Geçici Diten.SafetyStockService içinde kabul edilmiş iz ve GxP/K2 sınıflaması bilinmediği için tüm Yol hücreleri TBD'dir; istisna talep edilmez. AUD-001 §5 K4 gereği iz yokken yeni yazma komutu kodlanamaz.

| Planlanan komut niyeti | Olay | Nesne | Yol | Kaydedilecek değişim | Açık kapı |
|---|---|---|---|---|---|
| CreatePolicyDraft | safety-stock.policy-draft-created | SafetyStockPolicy | TBD | yeni kapsam, aktör, zaman | Ortak iletici; K2 kararı |
| RevisePolicyDraft | safety-stock.policy-revised | SafetyStockPolicyRevision | TBD | önceki/yeni alanlar, gerekçe | Ortak iletici; version |
| RecordEvaluation | safety-stock.evaluation-recorded | SafetyStockEvaluation | TBD | girdi/revision kimliği, sonuç/uyarı | Hesap yöntemi; audit |
| RecordManualOverride | safety-stock.override-recorded | SafetyStockOverride | TBD | aday ve manuel miktar, gerekçe | SoD; K2 |
| SubmitPolicy | safety-stock.policy-submitted | SafetyStockPolicy | TBD | durum geçişi | Workflow/SoD |
| ApproveOrRejectPolicy | safety-stock.policy-decision-recorded | ApprovalDecision | TBD | karar, aktör, gerekçe | Onay yetkisi; K2 |
| ActivateOrWithdrawPolicy | safety-stock.policy-state-changed | SafetyStockPolicy | TBD | önceki/yeni durum, revision | MRP tüketimi; K2 |

Parola, token, gereksiz kişisel veri veya tam kaynak payload'ı audit metadata'sına yazılmaz. Audit kayıt deposunun değişmezliği ve başarısız yazma davranışı geçici servis altyapısı hazır olduğunda ayrıca kanıtlanır.

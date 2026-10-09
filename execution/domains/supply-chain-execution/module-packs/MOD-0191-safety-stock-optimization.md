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
status: ready-for-dev
owner: "Supply Chain Execution / Control Tower"
branch: feature/sce/mod-0191-safety-stock-optimization
started: 2026-10-09
target: TBD
---

# MOD-0191 — Safety Stock Optimization

> Ali/Control Tower onayıyla yalnız ilk izole, salt okunur Yöntem A fixture hesaplama çekirdeği için ready-for-dev'dir. Geçici Diten.SafetyStockService:5069 içinde bu çekirdeğin derlenmesi ve test edilmesi izinlidir; API/host, kalıcı policy veya evaluation, yazma, rota, UI ve canlı entegrasyon izinli değildir. Nihai stok/sipariş birimi yuvarlaması, Yöntem B, onay/etkinleştirme ve Demand v2 tüketici kuralı açık kalır. MVP-4 SoR'u yalnız SafetyStockPolicy'dir; MOD-0188'in açık dalı ve kodu kapsam dışıdır.

## 1. Module Summary

MOD-0191, yetkili planlayıcının Tenant × LegalEntity × SKU × Warehouse kapsamındaki güvenlik stoğu politikasını tanımlaması, girdileri ve sürümüyle açıklanabilir bir aday sonucu incelemesi, gerekçeli manuel değişikliği kaydetmesi ve yetkili onaydan sonra politikanın MRP tarafından tüketilmesi için sözleşme hazırlar. MVP-4 iş sonucu, güvenlik stoğu kararı ile stok gerçeği, brüt talep ve ikmal önerisi arasındaki sahiplik çizgisini korumaktır. Hedef aktörler planlayıcı, onaylayıcı ve yetkili denetçidir. **Onaylanan ilk izole dilim** yalnız tamlığı doğrulanmış, gün bazlı, sürümlü fixture üzerinden takvim günü başına ortalama talep × açıkça verilen pozitif kapsama günü ile salt okunur ham decimal aday önizlemesidir; kalıcı kayıt veya canlı çağrı yoktur. Diğer yöntemler, hedef hizmet seviyesi, sayısal üst eşik, nihai stok/sipariş birimi yuvarlaması ve otomatik çalışma kararı TBD'dir.

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
| SafetyStockEvaluation | İleride seçilmiş policy revision ve yetkili kaynak sürümlerine bağlı kalıcı aday sonucu için taslak nesne; ilk dilimde kayıt oluşturulmaz. İlk dilimin geçici çıktısı Yöntem A'nın temel UoM cinsinden ham decimal miktarını, Tenant × LegalEntity × SKU × Warehouse kapsamını, takvim günü penceresini, fixture sürümünü, girdilerini, hesap adımlarını ve varsa neden aday üretilmediğini açıklar. |
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
| MethodId, ParameterSet, ServiceObjective | sürümlü referans / değer | İlk salt okunur fixture diliminde yalnız Yöntem A ve istekle açıkça verilen, sıfırdan büyük kapsama günü onaylıdır; kalıcı policy parametresi, diğer yöntemler, hizmet hedefi, üst eşik ve nihai stok/sipariş birimi yuvarlaması TBD. Örtük kapsama günü yok. |
| DemandRevisionId, demand state/integrity | kaynak kimliği/kanıtı | Demand v2 tüketici kararı gelmeden üretimde boş/kapalı; v1 otomatik ikame edilmez. |
| Availability snapshot/asOf, product/UoM, BOM version, lead-time source | kaynak kimliği, tarih, ölçü | Hangi kaynakların hesap için zorunlu olduğu ve snapshot tutarlılığı TBD; eksik/eski kaynakla sessiz öneri yok. |
| CandidateQuantity, FinalQuantity, BaseUomId | decimal / unit | İlk dilimin geçici ham decimal aday miktarı yalnız fixture içinde tutarlı temel UoM ile döner; canlı MDM doğrulaması, nihai stok/sipariş birimi yuvarlaması, FinalQuantity, override ve onaylı değer üretilmez. Kalıcı aday ve manuel/onaylı değerler ileride ayrı tutulur. |
| Explanation, warnings, override reason, approval record | yapılandırılmış kanıt | Etken girdiler ve kaynak sürümleri, veri yeterliliği, önceki/yeni değer, aktör/zaman/gerekçe; birleşik belirsiz güven puanı yok. |

Tenant-first indeksler ve IsDeleted filtresi gerekir. Olası sorgu anahtarları TenantId + LegalEntityId + SkuId + WarehouseId + state; unique/partial index, revizyon geçerlilik çakışması ve retention politikası TBD. Hesap kaynağı snapshot'ları ikinci stok/talep SoR'u oluşturmaz.

## 5. Repo Scope

- **Bu onay kaydı:** yalnız execution/domains/supply-chain-execution/module-packs/MOD-0191-safety-stock-optimization.md.
- **İzinli ilk geliştirme dilimi:** services/Diten.SafetyStockService/** altında yalnız kalıcı kayıt yapmayan Yöntem A hesaplama çekirdeği, sözleşmeli gün bazlı fixture doğrulaması, derleme için gereken en küçük proje yapısı ve odaklı testler. API/host, Persistence, canlı servis çağrısı, endpoint, Gateway ve UI bu izin kapsamında değildir.
- **Gelecekte ayrı onay gerektiren olası uygulama kapsamı:** services/Diten.SafetyStockService/** içinde yalnız MOD-0191 feature klasörü ve testleri; frontend/Diten.Web/Views/SupplyChain/SafetyStockOptimization/ ile ilgili controller, JS ve yedi dil RESX. Geçici servis/port kararı tek başına kod veya scaffold yetkisi değildir.
- Gateway, ortak izin kaydı, merkezî kontrat ve DCP uzlaştırması bu pack'in yazma kapsamı değildir; sahipleriyle ayrı görev gerektirir.

## 6. Protected Paths

Bu pack onayı kaydında pack dışındaki **bütün dosyalar** korunur. Ayrı ilk dilim geliştirmesinde yalnız §5'teki dar Diten.SafetyStockService kapsamı açılır. AGENTS.md, .antigravity/**, execution/domains/supply-chain-execution/domain-config.md, DCP-009, registry, docs/analysis/contracts/**, gateway/Diten.ApiGateway/**/ocelot.json, frontend/Diten.Web/Views/Shared/_Layout.cshtml, Archive controller/view yolları, MOD-0188 dalı/kodu, diğer services/** ve MVP-6 dosyaları korunur. Diğer domain servisleri gelecekte de MOD-0191 uygulama kapsamı dışındadır.

## 7. Dependencies

| Kaynak | Belgelenen durum | Taslakta izinli kullanım | Üretim kapısı |
|---|---|---|---|
| INVENTORY availability, MOD-0173 | inventory-bundle.openapi.yaml v1 **FROZEN**; dosya açıkça Prism mock kaynağıdır. GET /availability Item/SKU/Warehouse, LegalEntity, baseUom, asOf döndürür. | Sonraki dilimde sözleşmeye karşı fixture/mock ve salt okuma; ilk çekirdek çağırmaz. | Gerçek MOD-0173 producer ve yetki/snapshot tutarlılığı doğrulanmadan canlı hesap/etkin policy tüketimi kapalı. Stok hareketi veya balance yazımı yok. |
| PRODUCT-MASTER, MOD-0290 | product-master-bundle.openapi.yaml v1 **FROZEN**, mock örneği var; kimlik/UoM MDM SoR'dur. | Sonraki dilimde fixture/mock ile SKU, item, UoM doğrulaması; ilk çekirdek yalnız fixture içindeki temel UoM beyanı ve tutarlılığını doğrular, producer çağırmaz. | Gerçek producer, SKU/LegalEntity uygunluğu ve tarihsel UoM doğrulanmadan canlı tüketim kapalı. |
| BOM, MOD-0193 | bom.openapi.yaml v1 **FROZEN** contract; WP-MVP4 BOM'u **mock** diye niteler. | Sonraki üretim senaryosu onaylanırsa fixture/mock; ilk çekirdek kullanmaz. Olmayan BOM'u sıfır bileşen sayma. | Gerçek effective BOM producer/versiyon/LE sözleşmesi ve ürün kararı olmadan canlı manufacturing etkisi kapalı. |
| Demand, MOD-0188 | demand.openapi.yaml **v1 FROZEN**. Açık MOD-0188 pack'i v2 producer semantiğini tarif eder; v2 merkezi freeze/yayın **DRAFT/açık kapı**, origin/main'de v2 contract yoktur. | V2 için yalnız ayrı etiketli taslak fixture; v1 ikame değil. | MOD-0191'in v2 tüketici kuralı, revision/status/integrity/asOf/invalidated davranışı ve ortak testleri CT/Ali tarafından onaylanmadan canlı tüketim kapalı; v2'yi bu pack dondurmaz. |
| İlk dilim hesap fixture'ı | Fixture sürümü, Tenant × LegalEntity × SKU × Warehouse kapsamı, temel UoM, başlangıç ve bitiş tarihini açıkça taşır. Boş olmayan aralıktaki her takvim günü için tam bir kayıt gerekir; canlı Demand v2 sözleşmesi veya üreticisi değildir. | Yalnız salt okunur Yöntem A çekirdeğine girdi; yinelenen gün ve kapsam/birim uyuşmazlığı reddedilir. Fixture içi UoM tutarlılığı canlı MDM doğrulaması değildir. | Fixture başarısı canlı Demand v2, Inventory, Product veya BOM kabulü değildir. |
| MOD-0189 ve Scenario Modeling | Blueprint 0191 için 0189 + Scenario Modeling bağımlılığı yazar; main'de 0189 pack/üretim entegrasyonu ve Scenario Modeling tüketici kontratı bu incelemede kanıtlanmadı. | Bağımsız sözleşme taslağı ve mock sınırı. | MRP politika tüketim noktası, idempotency ve senaryo sahipliği onayı olmadan otomatik netting/proposal etkisi kapalı. |
| Platform LegalEntity kapsamı | MOD-0188 pack'inde tam yetkili şirket kümesi + dışlamalar, atama iptali ve MDM güven modeli açık canlı kapıdır. | Ayrı kapsam/izin fixture'ları. | Platform'un tam yetkili küme sözleşmesi ve iptal testi kabul edilmeden canlı LE seçimi kapalı. |
| Audit altyapısı | AUD-001 ortak iletici K4; geçici Diten.SafetyStockService'in kabul edilmiş izi henüz yok. | Audited Events taslağı. | Yazma komutu/approval ve K2 fail-closed kanıtı olmadan runtime kapalı. |

## 8. Runtime Constraints

Sonraki kalıcı policy/evaluation ve kullanıcı yüzeyi için MongoDB tek DB/tenant izolasyonu; her kayıtta TenantId, soft delete ve LegalEntity/data-scope sunucu doğrulaması gerekir. Başka tenant kaydı 404, kimlikle istenen kapsam dışı kayıt 404/boş; izin yoksa 403. Kaynak yok/bozuk, eşleşmeyen birim, geçersiz veya invalidated Demand revision, aynı politika için sürüm çatışması ve audit yazma hatası güvenli şekilde durur. Yanlış/yetersiz veriden sessiz sayısal politika üretilmez. Beş katman, CQRS/MediatR, Response<T>, CustomBaseController, Validation/Logging/Exception/Performance ve denetim davranışı seçilen serviste doğrulanır. Gateway 5000, frontend 5001; browser mikroservis portuna doğrudan gitmez. İlk saf fixture çekirdeği Mongo veya kullanıcı isteği açmaz.

**İlk izole dilimin hesap kuralı (Ali onayı; yalnız §5'teki çekirdek için uygulama izni):** `ortalama günlük talep = tamlığı doğrulanmış penceredeki günlük talep toplamı / penceredeki takvim günü sayısı`; `ham aday = ortalama günlük talep × istekle açıkça verilen kapsama günü`. Fixture sürümü, Tenant × LegalEntity × SKU × Warehouse kapsamı, temel UoM, başlangıç ve bitiş tarihlerini taşır. Başlangıç ve bitiş dahil tarih aralığı boş olamaz; her takvim günü için tam bir kayıt bulunur. Yinelenen gün, günlük kaydın fixture kapsamı/temel UoM'siyle uyuşmaması, eksik/bilinmeyen gün, negatif talep veya boş/bozuk dönem sayısal aday üretmez. Doğrulanmış günlük `0` geçerlidir; eksik gün sıfıra dönüştürülmez. Kapsama günü istekle açıkça verilir ve sıfırdan büyüktür; sabit/örtük varsayılan yoktur. Günlük talep ve ham aday fixture içinde tutarlı temel UoM miktarıdır; bu kontrol canlı MDM doğrulaması değildir. Çekirdek ham decimal hesap sonucunu ve kapsamı, pencereyi, fixture sürümünü, günlük girdileri, toplamı, böleni, kapsama gününü ve hesap adımlarını içeren izi döndürür; nihai stok/sipariş birimine yuvarlamaz. Decimal toplam/bölme/çarpma taşması veya temsil edilemeyen sonuçta aday üretmez, hata nedenini açıklar; sessiz clamp/truncation yoktur. Haftalık Demand v2'nin günlere dağıtımı TBD'dir ve ilk dilimde kullanılmaz. Çekirdek yalnız bellek içi salt okunur hesap yapar; değerlendirme/policy veya audit kaydı oluşturmaz.

**Kapı ayrımı:** İlk çekirdek için Yöntem A, takvim günü, fixture tamlığı, pozitif kapsama günü ve ham decimal çıktı kararlaştırıldı; bunlar §18 A'da karar kaydıdır, test/uygulama kanıtı değildir. Çekirdeğin kullanıcıya veya canlı kaynağa açılması ayrı izin/veri kapsamı kapısıdır; sonraki yazma/override/onay dilimi için audit ve görev ayrılığı ayrıca zorunludur. Kararsız hesaplama veya etkinleştirme davranışı kodlanmaz. Fixture sonucu canlı producer, yetkili LegalEntity kaynağı, Demand v2, Gateway veya audit kabulünün kanıtı değildir; bu yüzeyler §18 B'ye kadar kapalı kalır. Gerçek secret kaynak dosyası, fixture, log veya pack içine konmaz.

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

İlk izinli dilim yalnız geçici Diten.SafetyStockService altında saf Yöntem A hesaplama/fixture doğrulama çekirdeği ve odaklı testlerdir; command, handler, API, Mongo veya dış kontrat istemcisi açmaz. Sonraki ayrı onayla MOD-0191 feature'ı Commands/, Queries/, Handlers/CommandHandlers/, Handlers/QueryHandlers/, Validators/ ve SafetyStockOptimizationModels.cs düzeninde tasarlanır. Her command/query/handler/validator ayrı dosya; handler adında Command/Query/Request suffix yok. Dış kontrat istemcileri Application arayüzü, Infrastructure uygulaması üzerinden çalışır; handler doğrudan HTTP/DB kodu taşımaz. GoldenReferenceSlim/Compact gerçek kodu ve pack'leri okundu; bunların DataTable CRUD yüzeyi bu özel çalışma alanına otomatik kopyalanmaz. Geçici servisteki gerçek base/repository/audit altyapısı ayrıca doğrulanmadan sonraki uygulama dosya listesi tamamlanmış sayılamaz.

## 11. Frontend File Contract

Özel tenant çalışma alanı için beklenen yüzeyler: kapsamlı policy seçimi; revision/girdi kaynağı/uyarı ayrıntısı; aday ile manuel değerin yan yana açıklaması; gerekçe ve onay geçmişi. Ekran listesi, create/edit form alan sayısı ve etkileşim tasarımı ürün sahibiyle TBD. Bu nedenle golden_reference: none ve data_mode/form_field_count yoktur. DataTable liste ekranı sonradan onaylanırsa kullanıcı form alanları sayılır; ≤8 Slim, >8 Compact, veri modu ve gerçek Golden Reference dosya seti ekran bazında pack'e eklenir. Yüklenen her veri yüzeyinde içeriğin biçimine uygun skeleton; yükleme, boş, hata ve yetkisiz durumlar ayrıdır. Browser yalnız Gateway 5000 veya yetkili aynı-origin MVC proxy kullanır.

## 12. Validation Rules

| Alan / işlem | Zorunlu ön koşul | Hata / açık karar |
|---|---|---|
| Tenant/LegalEntity (sonraki kullanıcı yüzeyi) | Tenant sunucuda çözümlenir; seçilen tek LE Platform tam yetkili küme ve dışlamalar içinde her istekte yeniden doğrulanır. İlk saf fixture çekirdeğinde kullanıcı seçimi/Platform çağrısı yoktur. MDM lookup yalnız kimlik doğrular. | Kapsam belirsizse kapalı; başka tenant 404; aynı tenant yetkisiz LE create 403 / sıfır kayıt ancak yetkili sonuç sözleşmesi bu ayrımı doğrulayınca. |
| SKU/Warehouse/UoM | Canonical kimlikler ve birim eşleşir; payload master veya stok yetkisi sağlamaz. | Geçersiz/eksik kaynakla etkin sonuç yok. |
| Policy revision | Zorunlu alanlar, sürüm token'ı ve geçerlilik çakışması onaylı iş kuralına uyar. | Eksik 400, duplicate/çakışma 409; iş kuralı ayrıntısı TBD. |
| İlk dilim günlük fixture | Sürüm, Tenant × LegalEntity × SKU × Warehouse, temel UoM, başlangıç ve bitiş tarihi zorunlu; başlangıç ≤ bitiş, sınırlar dahil her takvim gününde tam bir kayıt gerekir. Günlük kayıt kapsamı/birimi fixture başlığıyla eşleşir; gerçek sıfır talep geçerlidir. | Eksik/bilinmeyen veya yinelenen gün, kapsam/birim uyuşmazlığı, negatif talep ve boş/bozuk dönem aday üretmez. Fixture içi tutarlılık canlı MDM yetkisi/doğrulaması değildir. |
| İlk dilim Yöntem A / kapsama günü | Ortalama günlük talep × istekle açıkça verilen sıfırdan büyük takvim günü kapsaması; ham decimal sonuç ve hesap izi döner. | Eksik, sıfır veya negatif kapsama günü aday üretmez; örtük/sabit varsayılan yok. Decimal taşması/temsil hatası aday üretmez. Nihai stok/sipariş birimi yuvarlaması yapılmaz; Yöntem B onaylı değil. |
| Canlı hesap girdisi | Kaynak asOf/sürüm/bütünlük, onaylı Demand v2 durumu, availability, ürün ve gerekiyorsa BOM ayrı canlı dilimde bağlanır. | Kaynak eksik/stale/invalidated ise açıklamalı kapalı sonuç; v1 fallback yok. İlk fixture çekirdeği bu çağrıları yapmaz. |
| Manuel değişiklik/onay | Gerekçe, önceki/yeni değer, aktör, zaman ve bağımsız karar izi; onay yetkisi/SoD TBD. | Eksik gerekçe/izin, eski sürüm veya audit başarısızlığında etki yok. |

## 13. Failure Path to Verify

- **Aynı kapsam/etkin dönem için çakışan policy:** İkinci kayıt/etkinleştirme 409; tek geçerli sürüm kuralı CT tarafından tanımlanana dek etkinleştirme kapalı.
- **Eksik yöntem veya kaynak:** 400 ya da kaynağa uygun kapalı hata; sayısal aday üretilmez, stok hareketi ve MRP etkisi oluşmaz. Doğrulanmış sıfır talebin sıfır adayı bu hata durumundan ayrıdır.
- **İlk dilim geçersiz girdi/taşma:** Eksik/bilinmeyen veya yinelenen takvim günü, negatif günlük talep, boş/bozuk pencere, fixture sürümü/kapsamı/temel UoM'si eksikliği veya günlük kayıt uyuşmazlığı, eksik/sıfır/negatif kapsama günü ve decimal taşması/temsil hatası sayısal aday üretmez; neden açıklanır. Gerçek sıfır gün geçerlidir; hiçbir sonuç clamp edilmez veya nihai stok/sipariş birimine yuvarlanmaz.
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
- [ ] AC-S03: İlk dilim yalnız Yöntem A'yı sürüm/kapsam/temel UoM/tarihleri açık fixture ile değerlendirir; başlangıç ve bitiş dahil her takvim gününde tam bir kayıt bulunur. Eksik/bilinmeyen veya yinelenen gün, kapsam/birim uyuşmazlığı, negatif talep, boş/bozuk dönem ve eksik/sıfır/negatif kapsama günü sayısal aday vermez. Doğrulanmış sıfır gün geçerlidir. Canlı run/activate ve aşağı akış etkisi kapalıdır.
- [ ] AC-S04: Aynı tam fixture sürümü, Tenant × LegalEntity × SKU × Warehouse kapsamı, pencere, temel UoM ve pozitif kapsama günü aynı ham decimal aday miktarını ve hesap izini verir; testler günlük toplam / takvim günü sayısı × kapsama gününü sabit örneklerle ölçer. İz kapsamı, pencereyi, sürümü, girdileri, toplamı, böleni, kapsama gününü ve hata nedenini taşır. Decimal taşmasında aday yoktur; clamp/truncation, nihai stok/sipariş birimi yuvarlaması veya kalıcı evaluation/policy kaydı yoktur. Fixture kanıtı canlı kaynak veya AC kapanışı sayılmaz.
- [ ] AC-S05: Aday, manuel değişiklik ve onaylı son değer ayrıdır; her manuel değişiklikte eski/yeni değer, gerekçe, aktör, zaman ve revision görünür; eksik gerekçede kayıt yoktur.
- [ ] AC-S06: Revizyon/onay/eşzamanlılık çatışmasında 409 ve sıfır kısmi değişiklik; policy iş sürümü teknik Version'dan ayrı kalır.
- [ ] AC-S07: Demand v1 hiç çağrılmaz; v2 tüketici onayı gelince Published, Superseded, Invalidated, eksik sayfa/checksum, gecikmiş olay ve servis arızası için kapalı davranış ortak testle kanıtlanır.
- [ ] AC-S08: BOM gereken onaylı üretim senaryosunda yalnız effective BOM/version tüketilir; senaryo kararı yoksa BOM etkisi üretim kapalıdır.
- [ ] AC-S09: Tenant ekranları açıkça _LayoutTenantShell seçer; yedi dil RESX paritesi, yükleme/boş/hata/izinsiz yüzeyleri ve yalnız Gateway akışı browser smoke ile doğrulanır.
- [ ] AC-S10: Seçilen servis için her yazma niyeti AUD-001 izine bağlanır; GxP/K2 ise audit başarısızlığında işlem durur. K4 altyapısı yokken yazma komutu eklenmez. Mock audit tasarımı canlı kayıt ve kapalı-başarısızlık kanıtı yerine geçmez.
- [ ] AC-S11: MOD-0189'a güvenlik stoğu aktarımı yalnız onaylı, geçerli policy revision'ı ve kaynak bütünlük bilgisini kullanır; idempotency ve invalidation etkisi iki taraflı sözleşme ile test edilir. Sözleşme onaylanana kadar aktarım kapalıdır.

## 17. Test Expectations

Bu pack düzeltmesinde kod veya test çalıştırılmaz. **İlk salt okunur çekirdeğin ilerideki izole test beklentisi:** fixture sürümü, Tenant × LegalEntity × SKU × Warehouse, temel UoM ve başlangıç/bitiş tarihleri zorunluluğu; sınırlar dahil gün sayısı ile kayıt sayısı/eşsiz tarihlerin eşleşmesi; tek günlük ve çok günlük geçerli pencere; doğrulanmış `0`; eksik/bilinmeyen veya yinelenen gün; günlük kapsam/birim uyuşmazlığı; negatif talep; ters/boş dönem; eksik/sıfır/negatif kapsama günü. Sabit örnekler ham decimal formülü ve açıklama izini ölçer; aynı girdi aynı çıktıyı verir. Decimal toplam, bölme ve çarpma sınırı/taşması, büyük fakat temsil edilebilir değer ve temsil edilemeyen sonuçta aday üretmeme sınanır; sessiz clamp/truncation ve nihai stok/sipariş birimi yuvarlaması yoktur. Fixture içi UoM doğrulaması canlı MDM testi değildir. Kalıcı kayıt ile canlı Demand v2/Inventory/Product/BOM/Platform çağrısının yokluğu kanıtlanır. Haftalık Demand v2 dağıtımı ve ürün adına yeni hizmet hedefi/üst eşik seçilmez. Gerçek secret fixture, kaynak dosyası veya logda kullanılmaz.

**Sonraki yazma veya kullanıcıya açılan dilimlerin izole kanıtı:** revision/override/onay kuralları; tenant-first Mongo filtre/soft delete/index/eşzamanlılığı; iki tenant ve iki LE ayrımı; ayrı Demand/stock/policy izinleri, görev ayrılığı ve fail-closed davranış testleri ayrıca gerekir. Bunlar salt okunur, kalıcı kayıt yapmayan çekirdeğin tamamlanmış koşulu veya kanıtı sayılmaz. AUD-001 kabul edilmiş yol yoksa yazma komutu kodlanmaz; GxP/K2 ise kayıt hatasında sıfır iş etkisi kuralı korunur.

**Canlı kabul/AC kapanışı kanıtı** ayrıca gerçek Platform yetkili şirket tam kümesi/dışlama ve iptal sonrası erişim kesilmesini; onaylı Demand v2 producer ve MOD-0191 tüketici davranışını; gerçek Inventory/Product ve kapsamdaysa BOM üreticilerini; MOD-0189 handoff/fencing/idempotency'yi; Gateway'den geçen API'yi; yedi dil RESX, kullanıcı ekranı ve browser smoke'u; seçilen servisin gerçek audit pipeline'ında başarılı/reddedilmiş yazıların aktör/tenant/nesne/sonuç kaydını ve GxP/K2 audit arızasında işlemin durduğunu ölçer. Servis, frontend, gateway build ve architecture testleri bu aşamada raporlanır. Mock/fixture sonuçları bu canlı kanıtların hiçbirini tamamlamaz. DataTable verifier yalnız onaylı DataTable ekranı eklenirse çalışır.

## 18. Ready-for-dev Checklist

Bu iki grup farklı kapılardır. Ali/Control Tower **A grubundaki ilk salt okunur çekirdeği** ready-for-dev olarak onayladı; açıkça “sonraki dilim” diye işaretlenen güvenlik/audit maddeleri ilk saf çekirdeğin ön koşulu değildir, ilgili kullanıcı veya yazma yüzeyi açılmadan önce kapatılır. **B grubu**, gerçek entegrasyonla canlı kabul ve ilgili AC'lerin kapanması içindir; fixture B grubunu geçirmez. Bu statü kararsız davranışı veya denetimsiz yazmayı açmaz. Ali, yalnız bu dar dilim için standarttaki `target` tarihine açık istisna verdi: `target: TBD` korunur, tarih veya teslim sözü uydurulmaz.

### A — İzole geliştirmeye başlama kapısı

- [x] DCP-002 MOD-0191 / Safety Stock Optimization preflight exit 0; MOD-0188'den ayrı Blueprint modülü.
- [x] MVP-4 SafetyStockPolicy SoR ve dar ready-for-dev sınırı kaydedildi.
- [x] Ali ayrı geçici Diten.SafetyStockService:5069 ve güncel main'den bağımsız MOD-0191 yerleşimini kararlaştırdı; frontmatter service/service_port/service_status güncellendi. MOD-0188 dalı/PlanningService kodu bugünkü bağımlılık değildir.
- [x] Ali ilk izole dilimi kalıcı kayıt yapmayan, salt okunur Yöntem A çekirdeğiyle sınırladı: tamlığı doğrulanmış gün bazlı sürümlü fixture, takvim günü, açık kapsama günü; canlı çağrı, Gateway, UI, MOD-0189 handoff ve yazma dışarıda. Yöntem B onaylı değil. 5069'un AGENTS.md/ports.md kaydı ve Gateway route'u bekleyen ayrı işlerdir.
- [x] Ekip sahipliği frontmatter'da kayıtlıdır. Ali yalnız ilk salt okunur dilim için zorunlu `target` tarihine istisna verdi; `target: TBD` bilinçli olarak korunur. Gelecekteki birleşme hedefi ayrı karardır.
- [x] Ali ilk dilimin fixture şemasını ve sınırlarını kararlaştırdı: sürüm, Tenant × LegalEntity × SKU × Warehouse, temel UoM, başlangıç/bitiş; boş olmayan aralıkta başlangıç ve bitiş dahil her gün tek tam kayıt; sıfır geçerli, eksik/bilinmeyen/yinelenen gün, kapsam/birim uyuşmazlığı ve negatif talep kapalı. Açık pozitif kapsama günü zorunlu; ham decimal sonuç/iz döner, nihai stok/sipariş birimi yuvarlanmaz. Bu karar kaydı test veya AC kabulü değildir.
- [x] Ali/Control Tower salt okunur çekirdeğin sınırlı giriş/çıkış sözleşmesini ve §17 doğrulama beklentisini inceledi; veri veya kapsam belirsizse aday üretilmez. Haftalık Demand v2'yi günlere dağıtma kuralı bu dilimin dışındadır.
- [ ] **Sonraki canlı kaynak/kullanıcı yüzeyi için:** INVENTORY, PRODUCT-MASTER, koşullu BOM ve Demand v2 tüketici sözleşmeleri; Platform yetkili kümesine bağlı Tenant/LegalEntity veri kapsamı ve ayrı policy/Demand/stock işlem izinleri tasarlandı. JWT claim'i veya MDM lookup kullanıcıya şirket yetkisi vermez. Bu madde saf hesap çekirdeğinin ön koşulu değildir.
- [ ] **Sonraki yazma/override/onay dilimi için:** §21 AUD-001 yolu, GxP/K2 sınıflaması, görev ayrılığı, revision/çakışma ve fail-closed audit tasarlandı. Kabul edilmiş altyapı yokken K4 uyarınca yazma komutu kodlanmaz; K2 yolu doğrulanmadan açılmaz. Bu madde salt okunur çekirdeğin ön koşulu değildir. Gerçek secret hiçbir kaynak, fixture, log veya pack'e konmaz.
- [x] Scenario Modeling, endpoint, Gateway ve UI ilk salt okunur çekirdek kapsamı dışındadır; sonraki dilimde sözleşme/ekran kararı gerekir. DataTable çıkarsa Slim/Compact ve data_mode ayrıca seçilir.
- [x] Ali/Control Tower pack'i yalnız bu izole başlangıç kapsamında inceledi ve `ready-for-dev` kararını verdi. Bu karar AC kabulü değildir.

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

Üretici kalite karşılaştırması, tasarım ilkesi düzeyindedir: [SAP IBP Inventory Optimization](https://help.sap.com/docs/SAP_INTEGRATED_BUSINESS_PLANNING/252c83bbf8184915a56d4cdd0917ef06/cdbbdf46dc8d4212b97d0255f0af93a9.html) demand/supply değişkenliği, lead time ve hizmet hedefini ayrı sürücüler olarak ele alır; [Oracle Safety Stock Planning Methods](https://docs.oracle.com/en/cloud/saas/supply-chain-and-manufacturing/25d/faspf/safety-stock-planning-methods.html) yöntem seçimi ile kullanıcı override'ını ayırır. [Oracle'ın gün kapsamı açıklaması](https://docs.oracle.com/en/cloud/saas/readiness/scm/25c/scp25c/25C-sc-planning-wn-f38227.htm) günlük ortalama × açık kapsamayı doğrular; MOD-0191'de ilk fixture dilimi için yöntem ve takvim günü seçimi Ali'nindir, Oracle'ın ürün davranışı veya varsayılanları devralınmaz. Çok kademeli optimizasyon, istatistiksel Yöntem B, hizmet seviyesi, varsayılan eşik, yuvarlama veya otomatik override kararlaştırılmış değildir.

## 20. Follow-up Items

**İzole geliştirme başlangıcı için:**

1. Ali'nin ayrı geçici Diten.SafetyStockService:5069, MOD-0188'den bağımsız main tabanı ve ilk salt okunur Yöntem A fixture çekirdeği için dar `ready-for-dev` kararı kaydedildi. Ekip sahipliği yeterlidir; `target: TBD` için yalnız bu dilime açık istisna verildi, tarih uydurulmaz. Sonraki birleşme hedefi açık kalır. 5069'un AGENTS.md/ports.md kalıcı kaydı merkezî sahiplerin; Gateway route'u integration-agent'ın bekleyen ayrı işidir.
2. İlk dilimde fixture sürümü/kapsam/temel UoM/tarih ve gün bütünlüğü, pozitif açık kapsama günü, ham decimal sonuç ve hesap izi kararlaştırıldı. Fixture içi UoM tutarlılığı canlı MDM doğrulaması değildir; nihai stok/sipariş birimi yuvarlaması, hizmet hedefi, üst eşik ve haftalık Demand v2 dağıtımı sonraki ürün/entegrasyon kararlarıdır. Sonraki politika yazma/override/onay diliminde sürüm, görev ayrılığı, audit yolu ve etkinlik/retention ayrıca kararlaştırılır.

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

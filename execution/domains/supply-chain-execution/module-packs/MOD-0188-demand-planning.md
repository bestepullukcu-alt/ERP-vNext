---
id: MOD-0188
name: Demand Planning
domain: supply-chain-execution
service: Diten.PlanningService
service_port: 5068
service_status: temporary
consolidation_target: Diten.SupplyChainService
shell: tenant
golden_reference: none
entity_base: EntityBase
status: ready-for-dev
owner: "Codex MOD-0188 developer lane (AI agent; human owner unassigned)"
branch: feature/sce/mod-0188-demand-planning
started: 2026-10-02
target: 2026-11-20
---

# MOD-0188 — Demand Planning

> Bu belge Control Tower'ın onayladığı iş kararlarını ve MOD-0189'un şartlı tüketici kabulünü birleştiren `ready-for-dev` Module Pack'tir. `owner` AI geliştirici lane kimliğidir; insan sahibi atanmış değildir. `target` AC-D01–D29, ortam ve test yükü için lane tahminidir. Merkezi CT/Ali kararıyla geçici hedef `Diten.PlanningService:5068`, nihai birleşim hedefi `Diten.SupplyChainService` oldu. Yerel merkez dosyası değişiklikleri onaylı kayıt sayılmaz; Control Tower MOD-0188 geliştirmesini onayladı. İlk iskelet önceki raporlama dilimiydi; MOD-0189, canlı cross-service entegrasyon, merkezî kayıtlar ve nihai servis birleşimi bu onayın kapsamı dışındadır.

## 1. Module Summary

MOD-0188, Tenant × LegalEntity kapsamında SKU × Warehouse × hafta kırılımında gerçekleşmiş talep geçmişini yönetir, açıklanabilir 52 haftalık brüt tahmin üretir, inceleme ve onayla tek resmî baseline revision'ı yayımlar. Hedef kullanıcılar planlayıcı, inceleyici/yayımlayıcı, planlama veya kalite yöneticisi ve yetkili denetçidir. Birincil ekran özel tenant Demand Planning çalışma alanıdır; klasik CRUD/DataTable değildir.

## 2. Ownership and Boundaries

| Sahip | İş sınırı |
|---|---|
| MOD-0188 | History import, veri kalitesi/karantina, planning cycle, forecast run/policy, manuel brüt tahmin, revision/manifest, inceleme/onay/yayımlama/invalidation, v2 okuma ve olay. |
| MOD-0189 | Onaylı satış siparişlerinin brüt tahmini tüketmesi, stok/açık tedarik/termin/BOM ile MRP, dört durum kontrolü, çalışma/sonuç/öneri güvenliği, periyodik uzlaştırma. |
| MOD-0191 | Güvenlik stoğu optimizasyonu ve politikası. |
| MDM / LOCATION / dispatch | SKU ve temel birim/dönüşüm; Warehouse kimliği; gerçekleşmiş dispatch kaydı. MOD-0188 ikinci master veya dispatch producer açmaz. |
| MOD-0141 ve diğerleri | Resmî satın alma/tedarik işlemleri; MOD-0188 bunları otomatik değiştirmez. |

Tahmin için geçmiş gerçekleşmiş sevkiyat/tüketim, tahmini azaltacak gelecekteki onaylı satış siparişinden ayrıdır. Sıfır sevkiyat, tek başına sıfır talep veya stok yokluğu kanıtı değildir. MVP-4'te paralel yayımlanmış senaryolar, otomatik çevrim takvimi, net MRP, stok optimizasyonu ve satın alma işlemi bu modülün kapsamı dışındadır.

## 3. Owned Objects

- `PlanningCycle`: Yetkili planlayıcının LegalEntity için açtığı benzersiz çevrim; dönem, as-of, takvim/sürüm, saat dilimi ve 52 hafta sınırı sabittir.
- `DemandHistoryImportBatch`, `DemandHistoryRecord`, `QuarantineCase`: dosya/canlı entegrasyon, doğrulama, onay/red, düzeltme ve çok kaynaklı lineage; onaylı batch değiştirilemez.
- `ForecastPolicy` / `ForecastRun`: sürümlü yöntem/parametre, veri uygunluğu, geçmişe dönük test ve yöntem karşılaştırması.
- `DemandRevision` / `RevisionManifest` / `RevisionPart`: iş sözleşmesinde tek değişmez 52 haftalık sürüm; fiziksel MongoDB parçalanması tüketiciye yansımaz.
- `AuditEntry` ve outbox: aktör, izin, gerekçe, kanıt, önceki/yeni değer, durum sürümü, tekrar deneme ve yayın geçmişi.
- Üretici API/olayları `15'te, izinler `14'te tanımlıdır. Bu pack uygulama dosyası veya endpoint oluşturmaz.

## 4. Entity Fields

Tüm tenant-owned kayıtlar `EntityBase` kullanır: `Id`, sunucuda çözülen `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt` ve yalnız teknik concurrency için `Version` miras alınır. İş alanı durum belirteci ayrı `stateVersion` adını taşır. Kullanıcı iş kayıtlarında `CreatedBy`/`UpdatedBy` ve kalıcı audit bulunur.

| Nesne | Asgari alanlar / kural |
|---|---|
| PlanningCycle | `planningCycleId` her açılan çevrimin tekil kimliğidir. `planningPeriodKey`, seçilmiş takvim/sürüm ve LegalEntity saat dilimine göre hesaplanan 52 haftalık ufkun ilk haftasının yerel başlangıç tarihinden sunucuda türetilen resmî iş dönemi kimliğidir; planlayıcı serbest anahtar göndermez. `legalEntityId`, `asOfDate`, `calendarId`/`calendarVersion`, `timeZoneId`, `horizonStart`/`horizonEnd` ve sıralı 52 `weekStart`/`weekEnd` cycle açılınca sabitlenir. Kurumsal takvim yoksa ISO-8601 Pazartesi başlangıcı kullanılır. |
| History record | Kaynak iş anahtarı/içerik izi; `skuId`/`skuLevel`, `warehouseId`, gerçekleşme günü/haftası; özgün miktar/birim, MDM temel birimindeki miktar, dönüşüm oranı/sözleşmesi/sürümü; iptal/iade/düzeltme bağı; stockout kanıt durumu; file/live lineage. |
| Import batch/quarantine | Kaynak ve kapsadığı tarih/lokasyon/alan, yükleyen/onaylayan, validation sonucu, uyarılar, karantina nedeni, insan kararı, audit. |
| ForecastRun | Cycle ve seri kapsamı, history cutoff, yöntem/politika sürümü, seri bazında doğrulanmış aday uygunluğu, veri kapsamı, WAPE/MAE/bias ve uygun olduğunda MASE, yöntem sonuçları. Planlayıcının açık seçimi SKU × Warehouse serisine bağlanır; toplu seçim etkilenen serileri ve her serideki önceki/yeni yöntemi izler. |
| Revision manifest | `revisionId`, cycle, Tenant/LegalEntity/dönem, baseline, takvim snapshot'ı, SKU×Warehouse kapsamı, gerekçeli hariç seriler, ölçü/birim, beklenen parça/satır sayısı, iki tarafça doğrulanabilir checksum tanımı/değeri, `state`/`stateVersion`, hazırlayan/önemli editörler/inceleyen/yayımlayan. |
| Revision row/part | Sabit revision + benzersiz SKU×Warehouse×hafta anahtarı; temel birimde brüt miktar; gerçek sıfır / boş talep / eksik hafta / bilinmeyen ayrımı; yöntem veya manuel giriş/değişiklik etiketi ve gerekçe. |
| Invalidation | Neden kodu, iş etkisi, kanıt referansı, yetkili aktör, zaman, yeni durum sürümü. İçerik silinmez. |

`planningCycleId` işlem çevrimini, sunucunun ilk haftanın yerel başlangıç tarihinden türettiği `planningPeriodKey` resmî iş dönemini gösterir; cycle kimliği baseline tekilliğinin anahtarı değildir. Aynı Tenant × LegalEntity için ilk hafta başlangıç tarihi aynıysa farklı cycle, as-of tarihi veya takvim sürümü ikinci güncel Published plan açmaz. İlk hafta başlangıç tarihi farklıysa 52 haftalık ufuklar örtüşse de iş dönemleri farklıdır. Tenant-first bileşik indeksler, Tenant×LegalEntity×cycle kimlik tekilliği, cycle başına tek aktif aday ve Tenant×LegalEntity×ilk hafta yerel başlangıç tarihi için tek güncel Published baseline eşzamanlılık korumasıyla sağlanır. Fiziksel indeks/transaction yöntemi uygulama ayrıntısıdır. Published/Superseded/Invalidated otomatik silinmez; `EntityBase` alanları iş durumu yerine kullanılmaz.

## 5. Repo Scope

Ayrı yetkilendirilen MOD-0188 geliştirmesinin genel repo kapsamı: geçici `services/Diten.PlanningService/**` içinde yalnız tek self-contained MOD-0188 feature klasörü, beş katman ve testleri; `frontend/Diten.Web/Views/SupplyChain/DemandPlanning/**` ile ilgili MVC/JS/RESX. Lane, MOD-0188 owned Demand v2 sözleşme taslağını hazırlayabilir; dondurma/yayımlama yetkisi merkezindir. Bu ilk iskelet aşamasında `services/Diten.SupplyChainService/**`, MDM, LOCATION, dispatch, MOD-0189, MOD-0191, procurement, gateway veya frozen Demand v1 kod/sözleşmesi yazma kapsamı değildir. Nihai SupplyChainService birleşimi ayrı gelecek iştir; bu pack ona bugünkü kod yetkisi vermez.

## 6. Protected Paths

`.antigravity/**`, FROZEN `docs/analysis/contracts/demand.openapi.yaml` v1, `AGENTS.md`, diğer domain servisleri, `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**`, `frontend/Diten.Web/Views/Shared/_Layout.cshtml` ve `gateway/Diten.ApiGateway/**/ocelot.json` korunur. Gateway yalnız ayrı integration-agent işiyle değişebilir. DCP-009, domain config ve registry yönetişim sahiplerine aittir.

## 7. Dependencies

| Kaynak | Geliştirme: frozen contract/mock | Sonraki canlı entegrasyon kabulü | Canlı kapı |
|---|---|---|---|
| MDM SKU/UoM | FROZEN Product Master v1 mock/fixture yeterlidir. | Gerçek temel birim, tarihsel dönüşüm oranı/sürümü/etkinliği ve hatalı dönüşüm yanıtı kabul edilir. | Yetkili producer çalışır ve kimlik/dönüşüm kapsamı doğrulanır. |
| LOCATION | FROZEN LOCATION v1 mock ile Warehouse kimliği tüketilir. Site, Warehouse, tesis ve planlama lokasyonu eşit varsayılmaz. | Warehouse–LegalEntity ve SKU×Warehouse uygunluğunun yetkili sözleşmesi/sahibi doğrulanır. | Gerçek producer ve veri kapsamı doğrulanır. |
| Dispatch | Kontrollü dosya importu ve mock ile geliştirme mümkündür. WarehouseOutbound ReadyToShip gerçekleşmiş sevkiyat değildir. | Canlı producer, iptal/iade/düzeltme, coverage, kaynak çakışması uçtan uca test edilir. | Doğrulanmış canlı kaynak kapsadığı tarih/lokasyon/alan için yetkilidir; dosya sadece kontrollü düzeltme. |
| LegalEntity saat dilimi | Fixture ile cycle denenir; tenant saat dilimi sessiz ikame edilmez. | Yetkili LE saat dilimi sözleşmesi ve DST sınırları kabul edilir. | Geçerli LE saat dilimi sağlanır; belirsizse cycle açılmaz. |
| Planlama takvimi | Doğrulanmış kurumsal takvim yoksa ISO-8601 Pazartesi haftası + LE saat dilimi. | Kurumsal takvim seçilirse kimlik/sürüm/52 hafta producer kabulünden geçer. | Seçilen provider çalışır veya onaylı ISO fallback sürer. |
| Platform LegalEntity veri kapsamı | Pozisyon → Organization Unit → LegalEntity çözümünü ve Demand işlem iznini ayrı mock/fixture ile izole geliştir; mevcut tek şirketlik context ve Planning'in yerel doğrulama arayüzü tam yetkili şirket listesi entegrasyonu değildir. | Platform sahibi iptal edilmiş atama riskini düzeltip test kanıtı verir; merkez onaylı Platform→Planning sözleşmesi aktörün tam yetkili LegalEntity kümesini ve dışlamalarını taşır. MDM şirket referansı doğrulamasının izin/güven modeli sahiplerce kararlaştırılır. | Bu üç koşul ve doğrudan API dahil uçtan uca kapsam/izin testi sağlanmadan canlı şirket seçimi kabul edilmez. |
| MOD-0189 | V2 tüketici semantiği şartlı kabul edildi. | Ortak testler `17'de. | Dört durum kontrolü ve fail-closed davranış MOD-0189 canlı kapısıdır. |

Geliştirme entegrasyonu yalnız frozen sözleşme ve mock/fixture üzerinden yapılır; şimdi canlı MDM, SupplyChain/LOCATION, dispatch veya başka servise cross-service çağrı eklenmez. Gerçek SKU/UoM, Warehouse, LegalEntity saat dilimi ve gerçekleşmiş dispatch bilgisinin canlıda hangi yetkili producer/sözleşme ile doğrulanacağı henüz kanıtlanmadıysa canlıya geçiş kapısı açık kalır. Mock çıktısı üretim doğrulama kanıtı değildir. Onaylı satış siparişi, stok, BOM, açık tedarik, termin, güvenlik stoğu ve MOD-0141 uyarı alındısı MOD-0189/aşağı akış kapılarıdır; MOD-0188 brüt tahmininin tek başına blocker'ı değildir.

## 8. Runtime Constraints

Geçici `Diten.PlanningService` port `5068` kullanır; `service_status: temporary` ve `consolidation_target: Diten.SupplyChainService` bu pack'in teknik hedefidir, merkezî kayıtlarda ayrıca doğrulanmalıdır. MDM/SupplyChain ile aynı beş katman (Api/Application/Domain/Persistence/Infrastructure), CQRS/MediatR, `EntityBase`, `Response<T>`, `CustomBaseController`, JWT/RBAC, audit/outbox ve Mongo V3 GUID subtype-4 uygulanır. Ortak supply-chain Mongo DB içinde yalnız MOD-0188'e ait collection'lar bulunur; TenantId zorunlu, LegalEntity/data-scope sunucuda doğrulanır; çapraz tenant 404, kimlikle erişilen kapsam dışı kayıt 404/boş, eksik Demand işlem izni 403 davranışını korur. Tek self-contained MOD-0188 feature klasörü ve tek DI-registration extension method birleşimi kolaylaştırır. Kısmi revision okuması yasaktır. Gelecekte tüm inventory MVP'leri tek `Diten.SupplyChainService` içinde birleştiğinde MOD-0188 feature taşınır ve geçici PlanningService kaldırılır; bu taşıma bugün yetkilendirilmez. Bu `ready-for-dev` pack MOD-0188 geliştirmesini yetkilendirir; ilk iskelet yalnız önceki raporlama dilimidir. MOD-0189, canlı cross-service entegrasyon ve gelecekteki servis birleşimi kapsam dışıdır.

## 9. Layout & Shell Contract

`shell: tenant`; ana ekran `frontend/Diten.Web/Views/SupplyChain/DemandPlanning/` altında özel çalışma alanıdır. Her Razor sayfası açıkça `Layout = "_LayoutTenantShell"` yazar; FROZEN `_Layout.cshtml` kullanılmaz. Tenant UI en/tr/fr/es/zh/ar/ru RESX karşılığı ve erişilemeyen izinlerde yalnız açıklayıcı yetkisiz yüzey gerekir. Şirket seçimi teklidir; seçenekler yalnız Platform'un pozisyon → Organization Unit → LegalEntity kapsamından gelen, dışlamaları uygulanmış yetkili kümeden oluşur. Boş veya doğrulanamayan kapsamda şirket seçimi/iş içeriği açılmaz; UI listesi yetki kanıtı değildir. Varsayılan görünüm 13 haftayı öne çıkarır, 52 haftanın tamamı erişilebilir. Ayrı DataTable ekranı sonradan eklenirse kendi golden_reference/data_mode/form_field_count kararıyla sınıflandırılır; mevcut ana ekran için bu alanlar uydurulmaz.

## 10. Backend File Convention

Yetkili geliştirme aşamasında `Diten.PlanningService`, MDM/SupplyChain ile aynı beş katmanı ve CQRS/MediatR düzenini kullanır. MOD-0188, tek self-contained feature klasöründe `Commands/`, `Queries/`, `Handlers/CommandHandlers/`, `Handlers/QueryHandlers/`, `Validators/` ve `DemandPlanningModels.cs` ile toplanır; tek DI-registration extension method bütün MOD-0188 kayıtlarını kurar. Her command/query/handler/validator ayrı dosyadır; `{Verb}{Object}Command/Query/Handler/Validator` adları ve ince controller kuralı korunur. MDM/LOCATION/dispatch bağımlılıkları geliştirmede yalnız frozen contract/mock arabirimleriyle temsil edilir; canlı cross-service HTTP/consumer eklenmez. Audit/outbox ve Mongo V3 GUID subtype-4 ortak DB'de MOD-0188 collection sınırına uyar. DataTable golden reference klasör/CRUD seti özel çalışma alanına zorlanmaz.
## 11. Frontend File Contract

Özel çalışma alanı import batch/karantina, seri kapsamı, yöntem karşılaştırması, 52 haftalık edit, veri kalitesi göstergeleri, inceleme/onay ve revision geçmişini ayrı izinli yüzeylerde sunar. Tekli LegalEntity seçicisi yalnız yetkili ve dışlamaları uygulanmış şirketleri gösterir; kullanıcı hesabında doğrudan LegalEntity alanı aranmaz. Seçim değişince ve sonraki her işlemde sunucu kapsamı yeniden doğrular; eski UI seçimi erişimi sürdürmez. Mevcut tek şirketlik context veya Planning yerel doğrulama arayüzü tamamlanmış liste entegrasyonu sayılmaz. Her manuel girilen/değiştirilen hafta, kaynak yöntem sonucundan ayrı işaretlenir; zorunlu gerekçe ve audit erişilebilir olur. Geçmiş veri kapsamı, eksik/karantina uyarıları, yöntem, WAPE, bias, manuel müdahale ve veri yeterliliği ayrı açıklanabilir göstergelerdir; birleşik veya yüzdelik "güven puanı" yoktur. Invalidated içerik yalnız yetkili tarihsel/audit görünümünde belirgin uyarıyla görünür. Frontend yalnız Gateway 5000 yolunu kullanır; yetki yoksa sayfa iskeleti çizilmez.

## 12. Validation Rules

| Alan/işlem | Zorunlu kural | Hata davranışı |
|---|---|---|
| Tenant/LegalEntity/data scope | Token, sunucu bağlamı ve kayıt kapsamı eşleşir; payload kimlik yükseltme aracı olamaz. Aynı oturumda kullanıcı yetkili olduğu birden çok LegalEntity arasından işlem başına tek şirket seçebilir. Kullanıcı hesabında doğrudan LegalEntity aranmaz: Platform'un pozisyon → Organization Unit → LegalEntity çözümünden gelen tam yetkili küme ve dışlamalar esas alınır; Demand işlem izni bundan ayrı doğrulanır. İstek bağlamındaki seçilen kimlik yalnız güvenilmeyen ipucudur; sunucu her istekte seçimin geçerli kapsamda olduğunu ve kanonik kimlikle eşleştiğini doğrular. Onaylı alias/eşleme sözleşmesi yokken farklı kimliğe sessiz geçiş yapılmaz. Boş/hatalı/erişilemeyen kapsam veya sonradan iptal edilen atama erişimi genişletmez; canlı Platform→Planning sözleşmesi ve düzeltme/test kanıtı henüz açık kapıdır. | Control Tower kararı: aynı tenant içinde yetkisiz LegalEntity seçilerek oluşturma 403 ve sıfır kayıt; başka tenant'a ait şirket/kayıt 404; kapsam dışındaki mevcut kayda kimlikle doğrudan erişim 404/boş; eksik Demand işlem izni 403. Kapsam kaynağı doğrulanamıyorsa erişim açılmadan kapalı hata verilir. Bu ayrımın kodda uygulanması yetkili kaynak sözleşmesi kapısında bekler. |
| Cycle | Tekil `planningCycleId`, geçerli LE saat dilimi, seçilmiş planlama takvimi/sürümü, as-of ve sıralı 52 hafta doğrulanır; ilk haftanın yerel başlangıç tarihi hesaplanır ve `planningPeriodKey` yalnız sunucuda bu tarihten türetilir. Kurumsal takvim yoksa ISO-8601 Pazartesi başlangıcı kullanılır. Planlayıcı serbest dönem anahtarı gönderemez; cycle takvim/hafta sınırları değişmez. | Çakışan cycle kimliği veya tutarsız hafta/takvim reddedilir; istemciden gelen dönem anahtarı kabul edilmez. |
| History | Gerçekleşme kaynağı, SKU, Warehouse, tarih, miktar, kaynak iş anahtarı ve düzeltme/iptal bağı doğrulanır. | Hatalı kayıt karantina; batch engelleyici hatayla onaylanmaz. |
| Birim | SKU'nun MDM temel birimi; özgün birim/oran/sözleşme sürümü lineage. | Eksik, belirsiz, geçersiz veya etkisiz dönüşüm geçmişe girmez, karantinaya gider. |
| Aynı kaynak kaydı | Aynı anahtar+aynı içerik tekrar sayılmaz, iki lineage korunur; aynı anahtar+farklı içerik otomatik birleştirilmez. | Çatışma karantina ve yetkili insan incelemesi. |
| Forecast run | Tarihsel veri as-of sonrası sızmaz; seçili yöntem kendi asgari verisiyle uygun olmalıdır. | Yetersiz güvenilir geçmişte otomatik öneri yok; gerekçeli manuel draft açık. |
| Tamamen manuel Draft incelemeye gönderimi | Güvenilir geçmiş yetersizliği nedeniyle tamamen manuel hazırlanan Draft'ta seçilmiş her SKU × Warehouse serisinin 52 haftasının tamamı açık miktarlı olmalıdır. `0` geçerli açık miktardır; Missing/Unknown haftalar Draft'ta korunur, sıfıra çevrilmez. Önceden kaydedilmiş eksik InReview da Approved olamaz. | Eksik/bilinmeyen hafta varsa gönderim veya onay reddedilir; durum, içerik/durum sürümü, aday slotu ve audit değişmez. |
| Draft edit | Haftalık miktar, ölçü birimi, kapsam, takvim, geçmiş, yöntem/parametre/politika değişimi önemli edit sayılır ve zorunlu gerekçe/audit ister. | İnceleme/onay iptal, Draft'a dönüş; yeniden inceleme gerekir. |
| Seri bazlı yöntem seçimi | Planlayıcı, uygunluğu doğrulanmış adaylar arasından her SKU × Warehouse serisi için yöntemi açıkça seçer; seçili serilere aynı yöntemi açık toplu işlemle uygulayabilir. Tekli/toplu işlemde gerekçe, aktör, zaman, önceki/yeni yöntem ve etkilenen seriler kaydedilir. Toplu işlem bütün serilerde uygunluk, kapsam ve sürüm ön doğrulamasını geçmelidir. | Herhangi bir seride uygunsuz aday, kapsam/sürüm çatışması veya eksik gerekçe varsa hiçbir seride seçim/audit etkisi oluşmaz; otomatik seçim veya sessiz yedek ölçü yoktur. Önemli değişiklik inceleme/onayı geçersiz kılar. |
| Publish | Geçerli Approved, görev ayrılığı, tek aday, tüm seriler ve parça/satır/checksum doğrulaması gerekir. Tenant × LegalEntity × ilk haftanın yerel başlangıç tarihi aynı olan tüm cycle'larda tek güncel Published baseline atomik korunur; yeni yayın öncekini Superseded yapar. As-of veya takvim sürümü farkı bu tekilliği değiştirmez; farklı ilk hafta başlangıcı, ufuklar örtüşse bile farklı dönemdir. | Tekillik sağlanamıyorsa yayın bütünüyle engellenir; kısmi Published/olay/okuma yok. |
| Invalidate | Ayrı izin, maddi MRP etkisi, neden/kanıt/aktör/zaman ve monoton durum sürümü gerekir. | Gerekçesiz/kanıtsız işlem reddedilir. |

Önerilebilir yöntemler: Naive, Moving Average, Simple Exponential Smoothing, Holt trend, Seasonal Naive, Holt-Winters ve Bias-corrected Croston. Genel uygunluk için en az 26 tamamlanmış güvenilir hafta; mevsimsel yöntem için en az 104 hafta; intermittent için en az 26 hafta ve 4 gerçek talep olayı gerekir. Sürümlü pilot politikası bu eşikleri kontrollü ayarlayabilir. Rolling-origin backtest, WAPE birincil karşılaştırma; MAE ve bias zorunlu, MASE uygun olduğunda. MAPE ana ölçü değildir. Yöntem sonuçları aynı run içinde karşılaştırılır, ayrı yayımlanmış senaryo sayılmaz. Tahmin motoru açıklanabilir .NET arabirimi arkasındadır; Python runtime şartı konmaz. Ali'nin 2026-10-06 MVP-4 kararı: uygun adayların WAPE/MAE/bias/uygunsa MASE sonuçları ve tahmin uzaklığı kırılımları yan yana gösterilir; otomatik sıralama veya kazanan yöntem seçimi yapılmaz. WAPE paydası sıfırsa sonuç tanımsız kalır, başka ölçüye sessiz otomatik geçilmez. Planlayıcı yöntemi her SKU × Warehouse serisi için açıkça seçer; seçili birden çok seriye aynı yöntemi gerekçeli toplu işlemle uygulayabilir. Her seçim normal inceleme/onay ve görev ayrılığı akışına tabidir.

Bias, rolling-origin test tahminlerinin işaretli ortalama hatasıdır: `gerçekleşen − tahmin`. Pozitif bias düşük tahmini, negatif bias yüksek tahmini, sıfır ise bu testlerde net yönlü sapma olmadığını gösterir. Uygulanabilir MASE için mevsimsel olmayan karşılaştırmada gecikme 1; onaylı yıllık haftalık mevsimsel değerlendirmede gecikme 52 ve en az 104 güvenilir eğitim haftası kullanılır. Her origin'in naive/mevsimsel-naive ölçeği yalnız kendi eğitim bölümünden hesaplanır; ölçek sıfır veya yetersizse MASE tanımsızdır, sıfır ya da sonsuz gösterilmez. Mevcut izole değerlendirici her `origin × test haftası` çiftini ayrı ölçüm sayar: aynı takvim haftası örtüşen test pencerelerinde birden fazla sayılabilir. Bu ağırlık kullanıcıya açıklanır; otomatik yöntem sıralaması yapılmaz.

Control Tower'ın 2026-10-06 kararı: Holt-Winters mevsimsel yöntem sınıfındadır ve uygunluk için en az 104 tamamlanmış güvenilir hafta gerektirir. Missing/Unknown veya çözülmemiş stok yokluğu bulunan hafta güvenilir sıfır talep sayılmaz. Bu kuralın izole fixture testi, canlı kabul edilmiş geçmiş kanıtı değildir.

## 13. Failure Path to Verify

| Durum | MOD-0188 sonucu, audit ve tekrar deneme |
|---|---|
| Aynı import kaydı iki kanaldan gelir | Aynı içerik tek talep, iki lineage; aynı idempotency key tekrarında aynı sonuç. Farklı içerik karantina; otomatik overwrite yok. |
| Yükleyen kendi batch'ini onaylar | 403/iş kuralı reddi; batch değişmez, deneme audit'e girer. Engelleyici hata varken tüm batch onayı engellenir. |
| Planı hazırlayan veya önemli editör yayımlar; integration actor yanlış izin taşır | Yayımlama reddedilir ve audit yazılır; salt RBAC kontrolü görev ayrılığını atlayamaz. |
| İnceleme başladıktan sonra kapsam/içerik değişir | Mevcut inceleme/Approved geçersiz; Draft'a dönüş, yeni inceleme gerekir. İnceleyici değişiklik yapamaz, reddeder. |
| Revision parçası eksik/çift/bozuk veya checksum uyuşmaz | Tüm publish reddedilir; outbox oluşmaz. Published okumasında kısmi sayfa verilmez, kapalı hata. |
| Eşzamanlı iki yayın veya yinelenen event/istek | Tek güncel Published ve tek aday korunur; idempotency key aynı işlemin tekrar yan etkisini üretmez; çatışma açık yanıt/audit. |
| Yeni yayın sırasında eski sürüm okunur | Eski `revisionId` ile bağlı MRP okuması Superseded snapshot'a sabit devam eder; cursor sürümleri karıştırmaz. |
| Durum kaynağı erişilemez | Yetkili status API kısmi/tahmini durum döndürmez; güvenli hata, retry bilgisi ve audit/observability. |
| Invalidated sürümden planlama okuması | Reddedilir. Yalnız audit izniyle belirgin uyarılı tarihsel okuma; otomatik Superseded fallback yok. |

Doğal tahmin-gerçekleşme farkı tek başına Invalidated nedeni değildir; performans ve bias ölçümüdür. Invalidation nedenleri: (a) yanlış Tenant/LegalEntity/SKU/Warehouse kimlik veya kapsamı, (b) yanlış hafta/takvim/ölçü birimi/miktar, (c) yanlış kaynak veri/yöntem/parametre/politika, (d) eksik, yinelenen veya bozuk içerik; her biri MRP sonucuna önemli etki kanıtı gerektirir.

## 14. Authorization Convention

JWT + `[Authorize]` + `[HasPermission]`; anahtarlar PKS-001 biçiminde küçük harfli, noktalı ve en az üç segmenttir. Aşağıdaki liste MOD-0188 için nihai aday izin yüzeyidir; yetki kataloğuna yayımlanması ayrı geliştirme/yönetişim işidir.

| İzin | Yetkili eylem / görev ayrılığı |
|---|---|
| `demand.plans.read` | Yetkili kapsamda plan/çalışma alanı okuma. |
| `demand.cycles.create` | Planlayıcının cycle açması. |
| `demand.history-imports.import` | Dosya batch yükleme. |
| `demand.history-integrations.import` | Sınırlı integration actor canlı kayıt alma; plan onay/yayın yetkisi vermez. |
| `demand.history-imports.review` | Başkasının batch'ini onay/red ve karantina inceleme; yükleyen kendi batch'ini onaylayamaz. |
| `demand.forecast-policies.update` | Sürümlü tahmin politikası yönetimi. |
| `demand.forecast-runs.create` | Uygun geçmişle koşu başlatma. |
| `demand.drafts.create` / `demand.drafts.update` | Draft hazırlama, gerekçeli manuel edit/seri dışlama ve doğrulanmış adaylardan seri bazlı yöntem seçimi veya açık toplu uygulama; dışlama inceleme bitmeden yalnız planlayıcıca yapılır. Yöntem değişimi önemli edit ve görev ayrılığı denetimine girer. |
| `demand.plans.review` | Bağımsız inceleme, Approved veya gerekçeli ret; içerik edit'i yok. |
| `demand.plans.publish` | Yalnız geçerli Approved; hazırlayan/önemli editör olamaz. Başkasının planını onaylayan aynı yetkili kişi yayımlayabilir. Integration actor hiçbir durumda yayımlayamaz. |
| `demand.plans.invalidate` | Yetkili planlama/kalite yöneticisinin koruyucu durdurması; önceki publisher da kullanabilir; gerekçe/kanıt/aktör/zaman zorunlu. |
| `demand.plans.consume` | MOD-0189 servis kimliğine kapsamlı v2 snapshot/status okuma; Published veya bağlı Superseded. |
| `demand.audit.read` | Tarihsel/audit içerik, Invalidated uyarısıyla. |

Hazırlayan ve tüm önemli manuel editör kimlikleri revision'a bağlanır; yalnız son editöre bakılmaz. Yalnız açıklama/yorum/gösterim metni düzeltmesi önemli edit değildir. Demand işlem izni ile Platform kaynaklı Tenant/LegalEntity veri kapsamı ayrı ayrı, sunucuda ve doğrudan API erişiminde de kontrol edilir; servis hesabı veri kapsamı atlamaz. UI seçimi veya gönderilen şirket kimliği yetki kanıtı değildir.

### Revision durum geçişleri

| Geçiş | İzin ve görev ayrılığı | Değişmezlik, hata, audit ve idempotency |
|---|---|---|
| Cycle aç → Draft oluştur | `cycles.create`, `drafts.create`; planlayıcı | Cycle snapshot sabit; mükerrer anahtar aynı sonucu verir, farklı içerik çatışır; aktör/zaman audit. |
| Draft → InReview | `drafts.update`; planlayıcı | Tek aktif aday; validation başarısızsa Draft; review isteği sürüm belirteciyle tekil audit. |
| InReview → Approved | `plans.review`; bağımsız inceleyici | İçerik değiştiremez; eski içerik/sürüm ile onay reddi; karar/gerekçe/aktör/zaman audit. |
| InReview → Draft (ret) | `plans.review`; inceleyici | Ret gerekçesiyle aday kapanır; yeniden gönderim gerekir; tekrar isteği ikinci karar üretmez. |
| InReview/Approved → Draft (önemli edit) | `drafts.update`; planlayıcı | Önceki review/onay iptal; önemli editör kaydı; stale onay/yayın çatışma; audit. |
| Approved → Published | `plans.publish`; hazırlayan/önemli editör ve integration actor hariç | Tüm parçalar/satırlar/checksum ve tek-baseline atomik doğrulanır; başarısızlıkta hiç publish/event yok; başarılı işlem tek outbox kaydı. |
| Önceki Published → Superseded | Başarılı yeni yayın işleminin parçası | İçerik değişmez; yeni MRP başlayamaz, eski bağlı MRP tamamlanabilir; durum sürümü ve olay audit. |
| Published/Superseded → Invalidated | Ayrı `plans.invalidate`; yetkili yönetici | Maddi hata + kanıt; tek yönlü, yeni MRP yasak; tekrar aynı karar çift olay/etki doğurmaz; audit/outbox. |

Geri dönüş bir durumu geriye çevirmek veya yayımlanmış içeriği değiştirmek değildir: eski içerik temel alınarak yeni revision hazırlanır, normal inceleme/onay/yayın döngüsünden geçer.

## 15. Gateway / API Routing Decision

Demand v1 `docs/analysis/contracts/demand.openapi.yaml` FROZEN kalır; v2 veri v1 yanıtına sessizce birleştirilmez veya çevrilmez. Lane MOD-0188 owned Demand v2 API/event sözleşme taslağını hazırlayabilir, fakat dondurma ve yayımlama yetkisi merkezindir; gerçek v1 tüketicileri canlıdan önce yeniden kontrol edilir. Yeni MOD-0188/MOD-0189 akışları v2 semantiğini hedefler. Gateway 5000 üzerinden ileride geçici PlanningService `5068` için explicit route gerekebilir; route/ocelot ve canlı cross-service bağlantılar bu ilk iskelet aşamasında eklenmez, merkezî izin ve ayrı integration-agent kapısındadır. Aşağıdaki yol adları v2 taslağıdır; kesin OpenAPI ve route kaydı ayrı incelemede doğrulanır.

| İşlem | Örnek v2 kaynak/alan |
|---|---|
| Cycle/history/forecast | `POST/GET /api/v2/demand/planning-cycles`; import batch oluştur/doğrula/karantina çöz/onayla/reddet; forecast run başlat/oku. |
| Draft ve durum | `POST/GET /api/v2/demand/revisions`; draft edit/seri dışla; submit-review, approve, reject, publish, invalidate işlemleri. Yazma isteği idempotency key, beklenen durum sürümü ve gerekçe gereken yerde kanıt taşır. |
| Authoritative read | `GET /api/v2/demand/revisions/{revisionId}/manifest`, `/status` ve `/rows?cursor=...`; yetkili current Published sorgusu. Manifest ve status `revisionId`, cycle, Tenant/LegalEntity, scope, durum, monoton `stateVersion`, contractVersion v2 döndürür. |
| Snapshot sayfası | Her sayfa revision, scope, sabit sıralama, cursor, satır sayısı/bütünlük bağlamı taşır. Cursor başka tenant/LE/revision'a uygulanamaz. Published bütünlüğü bozulursa hiçbir sayfa kısmi başarı gibi sunulmaz. |

Manifest 52 değişmez hafta sınırını, as-of/takvim ID/sürüm/saat dilimini, seçilmiş ve gerekçeyle dışlanmış serileri, temel birimi, beklenen parça/satır sayısını ve iki tarafın hesaplayabileceği checksum semantiğini taşır. Satır anahtarı tektir ve sıralama kararlıdır. Boş talep kümesi, açıkça sıfır miktar, eksik hafta ve bilinmeyen veri farklı kodlanır; eksik/bilinmeyen sessizce sıfıra dönmez. Checksum algoritması, cursor gösterimi ve kesin fencing protokolü teknik tasarımda seçilir; tutarlılık sonucu sabittir.

Yayımlama ve geçersiz kılma outbox olayları en az `eventId`, `revisionId`, Tenant/LegalEntity/data scope, `planningCycleId`, dönem, yeni durum, monoton `stateVersion`, zaman, v2 sözleşme sürümü, kapsam ve bütünlük özetini taşır; 52 haftalık satırları taşımaz. Superseded durum olayı veya eşdeğer doğrulanabilir durum değişimi de sağlanır. Olaylar yinelenebilir, gecikebilir veya sırası değişebilir; authoritative status sorgusu karar kaynağıdır. Aynı `eventId` tekrarında yeni iş etkisi üretilmez. `eventVersion` şema sürümüdür, `stateVersion` durum sıralama/fencing değeridir; karıştırılmaz.

## 16. Acceptance Criteria

Aşağıdakiler yalnız MOD-0188 üretici davranışlarıdır; MOD-0189'un kontrol/durdurma/uzlaştırması burada tamamlanma şartı değildir.

- [ ] **AC-D01** Yetkili planlayıcı tekil `planningCycleId` ile cycle açar; as-of, LE saat dilimi, seçilmiş takvim ID/sürüm ve sıralı 52 hafta değişmez biçimde sabitlenir. `planningPeriodKey` ilk haftanın LE saat dilimindeki yerel başlangıç tarihinden sunucuda türetilir; serbest istemci anahtarı kabul edilmez. Kurumsal takvim yoksa ISO-8601 Pazartesi başlangıcı kullanılır.
- [ ] **AC-D02** Doğrulanmış kurumsal takvim yoksa ISO-8601 Pazartesi başlangıcı ve LE saat dilimi kullanılır; eski Published revision'ın hafta sınırları takvim değişiminden etkilenmez.
- [ ] **AC-D03** Dosya ve canlı import batch'leri yükleyen/kaynak/kapsamla izlenir; validation ve karantina görünür; engelleyici hatalı batch bütünüyle onaylanamaz.
- [ ] **AC-D04** Yükleyen kendi batch'ini onaylayamaz; warning-only batch yetkili farklı aktörün açık onayıyla kabul edilir; ret ve tekrar deneme audit'te görünür.
- [ ] **AC-D05** Aynı iş kaydı aynı içerikle file/live gelirse bir kez sayılır ve iki lineage görünür; farklı içerik karantinaya gider, otomatik overwrite olmaz.
- [ ] **AC-D06** Doğrulanmış canlı dispatch kapsamı için dosya yalnız kontrollü düzeltmeyle kullanılır; ReadyToShip gerçekleşmiş dispatch sayılmaz.
- [ ] **AC-D07** Geçmiş ve tahmin SKU'nun MDM temel birimindedir; özgün birim, oran, dönüşüm sözleşmesi/sürümü lineage'dır; eksik/belirsiz dönüşüm geçmişe girmez.
- [ ] **AC-D08** Veri kapsamı/stockout bilinmiyorsa sıfır sevkiyat sessizce sıfır talep sayılmaz; kullanıcıya uyarı ve veri yeterliliği gösterilir.
- [ ] **AC-D09** Uygun yöntemler asgari geçmiş eşiğine göre seçilir; rolling-origin test geleceği sızdırmaz; WAPE, MAE, bias ve uygun MASE ayrı hesaplanır.
- [ ] **AC-D10** Güvenilir geçmiş yetersizse otomatik öneri verilmez; planlayıcı gerekçeyle 52 haftanın tamamını manuel girebilir ve normal inceleme/görev ayrılığı işler. Tamamen manuel Draft, seçilmiş her SKU × Warehouse serisinde 52 açık miktar (gerçek `0` dahil) olmadan InReview'a gönderilemez; Missing/Unknown Draft'ta aynen kalır. Önceden kaydedilmiş eksik InReview Approved olamaz.
- [ ] **AC-D11** Aynı run içinde uygun adayların WAPE/MAE/bias/uygunsa MASE ve tahmin uzaklığı sonuçları yan yana karşılaştırılır; otomatik sıralama/kazanan veya WAPE tanımsızken sessiz yedek ölçü yoktur. Planlayıcı, doğrulanmış uygun adaylardan yöntemi her SKU × Warehouse serisi için gerekçeyle açıkça seçer veya seçili serilere aynı yöntemi açık toplu işlemle uygular. Her işlemde aktör, zaman, önceki/yeni yöntem ve etkilenen seriler izlenir; toplu uygunluk/kapsam/sürüm ön doğrulaması başarısızsa hiçbir seride etki oluşmaz. Seçim normal inceleme/onay ve görev ayrılığına girer. Sonuçlar ayrı Published senaryo oluşturmaz; kullanıcı ekranında birleşik güven puanı yoktur.
- [ ] **AC-D12** Her manuel girilen/değişen hafta yöntem sonucundan ayrı etiketlenir; gerekçe, eski/yeni miktar, aktör ve zaman görülebilir.
- [ ] **AC-D13** Aynı cycle içinde birden çok Draft tarihsel olarak bulunabilse de aynı anda yalnız bir InReview/Approved/yayın adayı ilerler; ret/geri çekme audit iziyle slotu bırakır.
- [ ] **AC-D14** İnceleyici içeriği değiştiremez; InReview'dan olumlu karar ayrı Approved yapar, ret Draft'a gerekçeyle döner.
- [ ] **AC-D15** İçerik, SKU×Warehouse kapsamı, seri dışlama, hafta/takvim, birim, geçmiş veya yöntem/politika değişirse inceleme/onay geçersiz, durum Draft olur.
- [ ] **AC-D16** Seri dışlama yalnız planlayıcı tarafından inceleme tamamlanmadan önce gerekçeyle yapılır ve manifest/audit'te görünür; tek engelleyici seri tüm yayını durdurur.
- [ ] **AC-D17** Yalnız geçerli Approved yayımlanır; hazırlayan/önemli editör ve integration actor yayımlayamaz; başkasının planını onaylayan yetkili aynı kişi yayımlayabilir.
- [ ] **AC-D18** Yayın öncesi beklenen parça/satır sayısı, tekrarsız satır ve checksum doğrulanır; hata halinde Published veya yayın olayı oluşmaz.
- [ ] **AC-D19** Başarılı yayın doğrulanmış tek bütün revision snapshot'ını Published yapar. Tenant × LegalEntity × aynı ilk hafta yerel başlangıç tarihi için, cycle/as-of/takvim sürümü farklı olsa bile yalnız bir güncel resmî baseline kalır; önceki Published atomik olarak Superseded olur. İlk hafta başlangıcı farklıysa örtüşen 52 haftalık ufuklar ayrı iş dönemleridir.
- [ ] **AC-D20** Published/Superseded içerik değiştirilemez; rollback eski içeriği temel alan yeni revision ve tam onay döngüsüyle yapılır.
- [ ] **AC-D21** Maddi MRP etkisi kanıtlanan hata ayrı izinle Invalidated yapılır; neden/kanıt/aktör/zaman ve durum sürümü görünür; doğal tahmin sapması invalidation nedeni değildir.
- [ ] **AC-D22** Invalidated planlama/tüketici okumasına kapalıdır; audit izniyle belirgin uyarılı tarihsel okuma vardır; eski Superseded sürüme otomatik dönüş yoktur.
- [ ] **AC-D23** V2 manifest/status aynı revision'a bağlı takvim snapshot'ı, kapsam, tekil satır anahtarı, beklenen sayılar, karşılıklı doğrulanabilir checksum ve monoton stateVersion döndürür.
- [ ] **AC-D24** V2 sayfalama sabit revision/sıra/scope kullanır; eksik/çift parça veya bozuk bütünlükte kısmi veri döndürmez; boş/gerçek sıfır/eksik/bilinmeyen anlamları ayrıdır.
- [ ] **AC-D25** Yayın/invalidation olayları eventId, revisionId, Tenant/LegalEntity/data scope, cycle, yeni durum, stateVersion ve bütünlük özetini taşır, satır taşımaz; yinelenen istekte ikinci etki yoktur.
- [ ] **AC-D26** Published/Superseded/Invalidated otomatik silinmez; tarihsel okuma ve audit korunur.
- [ ] **AC-D27** Tüm istek/cursor/olaylar tenant, LegalEntity ve data-scope sınırını korur. İşlem başına tek seçilen LegalEntity, Platform'un pozisyon → Organization Unit → LegalEntity çözümünden gelen yetkili kümede ve dışlamalar sonrasında bulunmalı; Demand işlem izni ayrıca geçmelidir. UI listesi/gönderilen kimlik yetki sağlamaz; boş, hatalı veya sonradan daralan kapsam erişimi genişletmez. Aynı tenant içindeki yetkisiz şirketle oluşturma 403 ve sıfır kayıt; başka tenant şirketi/kaydı 404; kapsam dışındaki mevcut kayda kimlikle doğrudan erişim 404/boş; eksik Demand işlem izni 403; doğrulanamayan kapsam kapalı hata verir. Servis hesabı rolü bu sınırı aşamaz. Canlı kabul üç açık Platform/MDM kapısına bağlıdır.
- [ ] **AC-D28** Ana Demand Planning ekranı `_LayoutTenantShell` seçer, yedi tenant dili destekler, 13 haftayı öne çıkarıp 52 haftayı açar ve izinsiz kullanıcıya içerik iskeleti göstermez.
- [ ] **AC-D29** Demand v1 yanıtı/sözleşmesi değişmez; yeni MOD-0188/MOD-0189 akışları v2 kullanır, v2 verisi v1'e sessiz dönüşmez.

## 17. Test Expectations

**AC-D10 manuel gönderim kapısı:** Tek eksik hafta, Unknown hafta, çoklu seriden yalnız birinin eksikliği, 52 gerçek sıfır ve eksik hafta tamamlandıktan sonra gönderim ayrı denenir. Ret halinde Draft durum/içerik/durum sürümü, aday slotu ve audit değişmez; Missing/Unknown değeri korunur. Önceden kaydedilmiş eksik InReview onayı da reddedilir. Bellek/domain ile izole gerçek Mongo kanıtı ayrılır.

**AC-D09 ölçüm testleri:** Pozitif/negatif/sıfır bias; gecikme-1 ve onaylı gecikme-52 MASE; origin başına yalnız eğitimden ölçek; sıfır/yetersiz ölçek ve kısa seri; gerçek sıfır ile eksik/bilinmeyen hafta ayrımı; örtüşen test haftasının birden fazla origin'de sayılması ayrı doğrulanır. Bu izole hesap testleri AC-D09'un tamamlandığı veya gerçek history'nin forecast'e bağlandığı anlamına gelmez.

**AC-D11 karşılaştırma ve seçim testleri:** Aynı doğrulanmış sentetik seri üzerinde toplam ve her tahmin uzaklığı için aday ölçümleri yan yana taşınır; giriş sırası korunur, otomatik sıralama/kazanan alanı üretilmez. Farklı uzaklıklarda avantajın değişebildiği ve sıfır WAPE paydasında değerin tanımsız kaldığı doğrulanır. Seçim diliminde farklı seriler için tekli/açık toplu işlem, aday uygunluğu, zorunlu gerekçe, tekrar istek, sürüm/kapsam çatışması, eski/yeni yöntem izi ve toplu işlemde ya hep ya hiç ayrıca kanıtlanır. İzole karşılaştırma çekirdeği ekranı, gerçek ForecastRun'ı, seçim kalıcılığını veya AC-D11'in tamamını kanıtlamaz.

**MOD-0188 tek başına geçebilir:** AC-D01–D29 için domain/handler/API/manifest/parça/index/outbox/izin/tenant/LegalEntity/data-scope, eşzamanlı tek aday/baseline, dosya+canlı yinelenme/karantina, birim dönüşümü, sabit referans veri setinde yöntem sonuçları, takvim/DST, SoD, idempotency, eksik parça/checksum ve v2 sayfalama testleri. Dönem tekilliği testleri: (1) aynı ilk hafta + farklı cycle, (2) aynı ilk hafta + farklı as-of, (3) aynı ilk hafta + farklı takvim sürümü, (4) farklı ilk hafta + örtüşen 52 haftalık ufuk, (5) aynı döneme ait iki cycle'ın eşzamanlı yayımlanması; ilk üç ve beşinci durumda en çok bir güncel Published, dördüncüde ayrı dönem beklenir. Mock MDM/LOCATION/dispatch ile contract-first koşulur. Backend, frontend ve gateway build; RESX paritesi ve browser smoke beklenir. Özel ana ekran DataTable verifier'a tabi değildir; ayrı DataTable eklenirse kendi verifier kapısı olur. İlk iskelet, AC-D01 çevrim kesiti ve AC-D03 dosya import altyapısı önceki raporlama dilimleriydi; mevcut dilim AC-D04 bağımsız batch incelemesidir. Batch onayı satırları doğrulanmış sevkiyat veya kabul edilmiş talep geçmişi yapmaz; AC-D05/06/07 ve canlı dispatch kanıtı olmadan history veya forecast tüketimi yoktur. AC-D01 gerçek Mongo/takvim-LegalEntity doğrulaması olmadan, AC-D03 canlı kaynak ve entegrasyon doğrulaması olmadan, AC-D04 ise gerçek Mongo karar/audit/eşzamanlılık kanıtı olmadan tamamlanmış sayılmaz. AC-D05 file/live birleştirme ayrı dilimdir. Derleme ve test sonucu ayrıca raporlanır.

**AC-D27 kapsam testleri:** Mock/fixture ile iki yetkili LegalEntity arasında tekli seçim, yetkisiz veya dışlanmış şirket, boş/hatalı/erişilemeyen kapsam, seçimden sonra pozisyon atamasının iptali/yetki kaybı, doğrudan API ile başka şirket kimliği gönderimi ve Demand işlem izni yokluğu ayrı ayrı denenir. Liste ve detay/cycle/import/inceleme yollarında sunucu aynı daraltmayı uygular; eski UI listesi veya gönderilen kimlik erişim sağlamaz. Yetkili kaynak sözleşmesi nedenleri ayırdığında, aynı tenant içindeki yetkisiz şirketle oluşturmanın 403 ve sıfır kayıt, başka tenant şirketinin 404; kapsam dışındaki mevcut kayda kimlikle erişimin 404/boş, eksik Demand işlem izninin 403, doğrulanamayan kapsamın kapalı hata verdiği ayrı ayrı kanıtlanır. Bu testler henüz yapılmadı; mock testleri canlı Platform kapsam kaynağı, iptal düzeltmesi, tam küme/dışlama sözleşmesi veya MDM güven kararının kabul kanıtı değildir.

**MOD-0189 tüketici sözleşmesi ve ortak entegrasyon testi, MOD-0188'in tek başına AC'si değil:** MOD-0189 yeni MRP çalışması için hangi iş dönemini seçeceğini kendi tüketici sözleşmesinde belirler; bu pack seçim yapmaz. MOD-0189 (1) run başında, (2) snapshot tam alındığında, (3) sonuç kesinleşmeden, (4) öneri devrinden hemen önce yetkili v2 status'u doğrular; erişilemezse bekler/kapalı hata verir. Bir run tek revisionId ve bütünlük kanıtına sabitlenir; bağlı Superseded tarihsel snapshot okunabilir. Invalidation gelen devam eden run durur; tamamlanmış sonuç güvenilmez/yeniden hesaplanmalı, uygulanmamış öneri bloklu olur. Yinelenen/gecikmiş/sırası değişmiş olay ve periyodik uzlaştırma birlikte test edilir. Resmileşmiş satın alma otomatik iptal edilmez; etki uyarısı/insan incelemesi ilgili sahipte doğrulanır. Servisler arasında mutlak anlık invalidation garantisi MVP'de aranmaz; güvenli bekleme, bloke etme ve audit gevşetilemez. Kesin protokol ve süre hedefi ölçümlü teknik tasarımda belirlenir.

## 18. Ready-for-dev Checklist

- [x] Control Tower MOD-0188 Phase 1.5 mimari planını onayladı; bu onay modülün veya AC-D01–D29'un tamamlandığı anlamına gelmez.
- [ ] **Canlı kapı 1 — Platform sahibi:** İptal edilmiş pozisyon atamasının kapsamda kalma riski düzeltilmeli ve iptal sonrası erişimin kesildiği testle kanıtlanmalı.
- [ ] **Canlı kapı 2 — merkez/Ali + Platform sahibi:** Platform→Planning sözleşmesi aktörün tam yetkili LegalEntity kümesini ve dışlamalarını taşıyacak biçimde onaylanmalı; mevcut tek şirketlik context veya Planning yerel doğrulama arayüzü bunun yerine geçmez.
- [ ] **Canlı kapı 3 — MDM/Platform sahipleri:** MDM LegalEntity referansı doğrulamasının izin ve güven modeli kararlaştırılıp sözleşmede doğrulanmalı.
- [ ] **Açık uygulama kapısı — Platform/MDM sözleşme sahipleri ve merkez:** Oluşturmada aynı tenant içindeki yetkisiz LegalEntity için onaylanan 403 ile başka tenant şirketi için 404'ü, ayrıca importta LegalEntity reddi ile Warehouse kapsam reddini güvenle ayıran yetkili sonuç sözleşmesi doğrulanmalı. Mevcut `ResolveSelectedAsync: Guid?` ve `IsAuthorizedAsync: bool?` bu nedenleri ayırmaz; kaynak arızası ayrı kapalı hata kalmalıdır. Sözleşme kabulünden sonra PlanningService davranışı ve dört ayrık test uygulanır; bu pack değişikliği AC'nin veya canlı entegrasyonun tamamlandığı anlamına gelmez.

- [x] MOD-0188 iş ve tüketici kararları sohbet içinde kapandı; MOD-0189 v2'yi belirtilen semantiklerle şartlı kabul etti.
- [x] Özel tenant çalışma alanı ve `golden_reference: none` onaylandı; `entity_base: EntityBase` yazıldı.
- [x] Pack, 20 zorunlu bölüm ve yalnız üretici kabul kriterleriyle taslak olarak hazırlandı; Control Tower onayıyla `ready-for-dev` durumuna geçti.
- [x] Control Tower 2026-10-02 tarihinde pack'i `ready-for-dev` kabul etti ve MOD-0188 geliştirmesine onay verdi; ilk iskelet önceki raporlama dilimiydi.
- [x] `owner` gerçek Codex AI geliştirici lane kimliğiyle, `started` 2026-10-02 ve `target` 2026-11-20 lane tahminiyle dolduruldu; insan sahibi atanmış gibi gösterilmez.
- [x] Control Tower iş dönemini ilk haftanın LegalEntity saat dilimindeki yerel başlangıç tarihi olarak tanımladı; `planningPeriodKey` sunucuda türetilir ve farklı cycle/as-of/takvim sürümü tek güncel baseline kuralını aşamaz.
- [ ] — merkez/Ali: `sce` kısa kodunun yetkili AGENTS.md §9 kaydı doğrulanmalı; yerel değiştirilmiş AGENTS.md onay kanıtı sayılmaz.
- [ ] — merkez/Ali: Geçici `Diten.PlanningService:5068` ve nihai birleşim hedefi yetkili servis/port yönetişim kayıtlarına işlenmeli.
- [ ] — merkez/Ali: DCP-009 ve ilgili sıra/scaffold kararı yeni geçici PlanningService modeline göre doğrulanmalı; yerel MOD-0183 istisnası kaydı merkez kararı yerine geçmez.
- [x] MOD-0188 pack `ready-for-dev` ve MOD-0188 geliştirme onayı alındı. Bu statü şirket kapsamı için yalnız mock/fixture ile izole geliştirme yetkisidir; AC-D27 veya canlı entegrasyon kabulü değildir. DCP'deki yerel `runtime_code_allowed: false` merkezi yetki kanıtı değildir; merkezi uzlaştırma açık kalır. İzin MOD-0188 PlanningService kapsamındadır; MOD-0189 ve canlı cross-service entegrasyon bu onaya dahil değildir.
- [ ] Lane Demand v2 owned API/event sözleşme taslağını hazırlayabilir; merkezi dondurma/yayımlama onayı, izin kataloğu, checksum/cursor/fencing, atomik publish ve Gateway tasarımı ayrı kapılardır. Demand v1 FROZEN kalır.
- [ ] MDM SKU/UoM, LOCATION Warehouse, LegalEntity saat dilimi ve dispatch için canlı yetkili producer/sözleşme doğrulaması ve entegrasyon kabulü tamamlanmalı; geliştirmede yalnız frozen contract/mock kullanılır, mock üretim kanıtı değildir.
- [ ] Gerçek v1 tüketicileri canlı öncesi tekrar kontrol edilmeli; MOD-0189 ortak entegrasyon testleri kendi hazır olma kapısında geçmeli.
- [ ] Validation, failure path, yetki, build/RESX/browser smoke ve üretici AC test kanıtları geliştirme aşamasında sağlanmalı.

## 19. Implementation Notes

- Control Tower'ın revision modeli C'dir: `revisionId` tek Tenant×LegalEntity×planning period içinde seçilmiş SKU×Warehouse kapsamının 52 haftalık brüt tahminini temsil eder. Fiziksel MongoDB bölümleri iş/tüketici sözleşmesini bölmez.
- Durum akışı: Draft → InReview → Approved → Published → Superseded; Published veya Superseded → Invalidated. `Archived` bu iki iş durumunun eş anlamı değildir; teknik storage tier ayrı uygulama ayrıntısıdır.
- Şartlı MOD-0189 kabulü, yetkili durum sorgusu + değişmez manifest/sayfa + olay + bütünlük/fencing ile sağlanır. MOD-0188 yalnız üretici yükümlülüğünden sorumludur. Sipariş tüketimi ve dört kontrol MOD-0189'a aittir.
- V1 FROZEN `docs/analysis/contracts/demand.openapi.yaml` değiştirilmez. MOD-0188 lane'i v2 owned contract taslağını hazırlayabilir; dondurma/yayımlama yetkisi merkezindir.
- Yeni merkezî CT/Ali kararı, MOD-0188'i geçici `Diten.PlanningService:5068` içine yerleştirir; `Diten.SupplyChainService` nihai birleşim hedefidir. Çalışma ağacındaki AGENTS.md/domain config/DCP-009 yerel değişiklikleri merkezî onaylı kayıt gibi gösterilmez. `sce`, 5068 ve scaffold sırası merkez/Ali kapılarında doğrulanır; pack `ready-for-dev` onayı MOD-0188 geliştirmesine izin verir; ilk iskelet yalnız önceki raporlama dilimiydi ve merkezi kayıt uzlaştırması açıktır.
- Teknik uygulama ekibi checksum algoritması, cursor biçimi, fiziksel part boyutu/indeks, outbox altyapısı, fencing yöntemi ve ölçümlü süre hedefini seçebilir; iş semantiğini gevşetemez. Kurumsal saklama politikası belirlenene kadar Published/Superseded/Invalidated otomatik silinmez.

## 20. Follow-up Items

1. Control Tower pack'i `ready-for-dev` kabul etti; AI lane owner, başlangıç ve tahmini hedef kaydedildi. İlk iskelet sonrası AC-D01–D29 uygulama dilimleri ayrıca izlenir.
2. Dönem iş tanımı kapatıldı; MOD-0189'un yeni MRP için dönem seçimi kendi tüketici sözleşmesindedir. Merkez/Ali, `sce`, geçici PlanningService `5068`, nihai SupplyChainService birleşimi ve scaffold sırasını yetkili kayıtlarda doğrulamalı. `owner`, tarihler, pack onayı ve MOD-0188 geliştirme yetkisi tamamlandı; modülün AC-D01–D29 teslimi tamamlanmadı.
3. Lane, MOD-0188 v2 OpenAPI/event taslağını merkezî dondurma/yayımlama incelemesine sunar; MDM SKU/UoM, LOCATION Warehouse, LegalEntity saat dilimi ve dispatch canlı doğrulama yolu ile v1 tüketici kontrolü canlıya geçiş kapısıdır. Tekli LegalEntity seçiminin canlı kabulü ayrıca Platform'un iptal edilmiş atama düzeltmesi/testine, tam yetkili küme ve dışlamaları taşıyan merkez onaylı Platform→Planning sözleşmesine ve MDM şirket referansı doğrulamasının izin/güven modeline bağlıdır. Mock'lar üretim kanıtı değildir.
4. MOD-0189'un onaylı satış siparişi, stok/BOM/açık tedarik/termin, dört durum kontrolü, periyodik uzlaştırma ve MOD-0141 uyarı alındısı; MOD-0191'in güvenlik stoğu politikası kendi pack/kapılarında yürür.
5. Nihai aşamada MOD-0188 feature ve sahip olduğu collection/sözleşme sınırı tüm inventory MVP'leriyle tek `Diten.SupplyChainService` içinde birleştirilir, geçici `Diten.PlanningService` kaldırılır; bu ayrı gelecek işidir ve bugünkü kod yetkisi değildir. Kurumsal saklama politikası, otomatik cycle scheduling, senaryo planları ve ayrı DataTable ekranı sonraki kapsamdır.
6. Control Tower'ın 2026-10-06 kararıyla yöntem seçimi SKU × Warehouse serisi başınadır; birden çok seçili seriye aynı yöntemin uygulanması açık, gerekçeli ve izlenen toplu işlemdir. Mevcut izole karşılaştırma bileşeni adayın belirli seri için uygunluğunu doğrulamaz; kabul edilmiş/güvenilir geçmiş ve sürümlü yöntem politikasına dayalı uygunluk kanıtı kurulmadan seçim kaydı/API/ekranı tamamlanmış sayılmaz. Otomatik kazanan veya sessiz ölçü geçişi yoktur.

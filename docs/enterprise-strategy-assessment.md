# Enterprise Strategy Assessment

**Tarih:** 2026-07-27  
**Branch / baseline:** `feature/es/enterprise-strategy` / `ea31b77c`  
**Kapsam:** Demand & Ideas, Enterprise Strategy & Business Performance, Decomposition Tree Builder  
**Yöntem:** Üretim kodu değiştirilmeden statik envanter, bağımlılık restore'u, test çalıştırmaları ve yalnız `http://localhost:5102` üzerinde ES API smoke. Frontend ve gateway çalıştırılmadı. Mongo üzerinde yalnız `DitenEnterpriseDb` kullanıldı.

## Yönetici özeti

Servis derlenip 5102'de ayağa kalkıyor ve `DitenEnterpriseDb` erişimini `/health` ile doğruluyor. Bu, modüllerin çalışır durumda olduğu anlamına gelmiyor. API'nin kimlik doğrulaması fail-open davranıyor, gateway yanlış porta yönleniyor, iki backend test paketi kırık, bildirilen dokuz frontend testi birebir kırık ve üç frontend yüzeyi zorunlu DataTable v2 / Compact ile l10n standardını karşılamıyor.

En kritik gerçek, “frontend boş çünkü veri yok” açıklamasının Demand & Ideas için doğru olmamasıdır: canlı API altı seed kaydı döndürdü. Enterprise Strategy goals çağrısı ise başarılı fakat boş döndü; bu yüzeylerde boş görünüm veri yokluğundan kaynaklanabilir. Decomposition için backend persistence/controller zinciri ayrıca doğrulanmadan UI'nin yalnız veri nedeniyle boş olduğu söylenemez.

Bu branch üretime veya WorkCenter entegrasyonuna hazır değildir.

## Çalıştırma kanıtı

| Kontrol | Sonuç | Sınıf | Kanıt / yorum |
|---|---|---:|---|
| ES API 5102 | Çalışıyor | 🟢 | `dotnet run ... --urls http://localhost:5102`; Kestrel 5102'yi dinledi. |
| Mongo health | Çalışıyor | 🟢 | `GET /health` → 200, `{"status":"Healthy","databaseName":"DitenEnterpriseDb"}`. |
| Gateway route | Yanlış port | 🔴 | `gateway/Diten.ApiGateway/ocelot.json` ES upstream'lerini `localhost:5004`e gönderiyor; ayrılmış port 5102 ve launch profile da 5003/5004 kullanıyor. Gateway üzerinden canlı smoke yapılmadı; gateway çalıştırmak yasaktı. |
| Application.Tests | Kırık | 🔴 | 39 toplam: 33 geçti, 6 kaldı. RBAC unauthenticated/claim kontrolleri, planning horizon, duplicate alignment ve lineage akışı kırık. |
| EndToEnd.Tests | Kırık | 🔴 | 4 toplam: 2 geçti, 2 kaldı. Lineage akışı ve missing-permission/stale-write senaryosu kırık. Bunlar HTTP-hosted E2E değil; servis sınıflarını in-memory harness ile çağırıyor. |
| Belirtilen frontend testleri | Kırık | 🟠 | 11 toplam assertion: 2 geçti, 9 kaldı; bildirilen referans noktası birebir üretildi. |
| Authenticated HTTP round-trip | Yok | 🔴 | API'de `AddAuthentication` / `UseAuthentication` yok. Buna rağmen permission filtresi var ve development bootstrap bilinen izinleri herkese veriyor. Kimliksiz GET gerçek veriyle 200 döndü. |

Not: İlk iki .NET testini paralel çalıştırma denemesi ortak `bin/obj` üzerinde dosya kilidi üretti. Ürün bulgusu sayılmadı; Application.Tests tek başına yeniden çalıştırıldı. NuGet ve npm bağımlılıkları yeni worktree'de bulunmadığı için restore/install yapıldı; kaynak dosya değiştirilmedi.

## Demand & Ideas

### Ne var

- API controller, CQRS command/query handler'ları, Mongo generic repository ve seed mevcut.
- Frontend controller ve `Index`, `Capture`, `Detail`, `Dashboard` yüzeyleri ile ayrı JS dosyaları mevcut.
- Canlı `GET /api/v1/demand-ideas` altı kayıt döndürdü.

### Ne çalışıyor

- API ayağa kalkıyor, Mongo erişimi var ve liste endpoint'i veri döndürüyor.
- Seed idempotent olarak koleksiyon boşsa devreye giriyor.

### Ne kırık ve neden

- 🔴 **Kimliksiz veri ifşası:** `GET /api/v1/demand-ideas` authentication olmadan 200 ve bütün seed kayıtlarını döndürdü. Program yalnız `UseAuthorization()` çağırıyor; authentication kurulumu yok.
- 🔴 **Uydurma seed verisi:** Seed, gerçek referans kimliği olmayan “Sarah Chen”, “Mike Johnson” gibi kişi adları ve sabit iş içeriği üretiyor. Canlı smoke bu kayıtların Mongo'da bulunduğunu doğruladı. Kural 7 ve WorkCenter sahiplik gereksinimiyle çelişiyor.
- 🔴 **WorkCenter kimliği yok:** `Requestor`, `Sponsor`, `OwnerName`, `BusinessUnit`, `RequestType`, `Category`, `Priority` string. Atanan kişi kimliği ve typed reference yok.
- 🟠 **UI veri yokluğundan bozuk değil:** Liste için veri var. Dolayısıyla yanlış/boş görünüm frontend route/API-shape/render katmanında araştırılmalı.
- 🟠 **Golden Reference yok:** `DemandIdeas/Index.cshtml` tablosunda `data-dt-standard="v2"` yok. Inline CSS ve string-template inline CSS mevcut.
- 🟠 **l10n eksik:** Bu modüle ait `.resx` kaynak seti bulunmadı; Razor başlıkları ve JS metinleri ham İngilizce.
- 🟠 **Layout sınırı:** Menü entegrasyonu frozen `_Layout.cshtml` içinde bulunuyor. Mevcut kodu değiştirmedik; gelecekteki düzeltme `_LayoutTenantShell.cshtml` üzerinden tasarlanmalı.

## Enterprise Strategy & Business Performance

### Ne var

- Goals, objectives, planning cycles, strategy periods, KPI, library, connections ve delivery-reference yüzeyleri için geniş controller/application/domain/persistence kapsamı var.
- API Swagger dokümanı 5102'de üretildi; goals liste endpoint'i envelope ile cevap verdi.
- Planning ve strategy register'larında DataTables kütüphanesi programatik olarak başlatılıyor.

### Ne çalışıyor

- `GET /api/v1/enterprise-strategy/goals` → 200 ve geçerli `Response<T>` envelope; mevcut DB'de `totalCount: 0`.
- Mongo migration/collection hazırlama kodu startup sırasında çalışıyor.
- Frontend seçili testlerinde iki assertion geçti.

### Ne kırık ve neden

- 🔴 **RBAC fail-open:** `DefaultEnterpriseStrategyAuthorizationService`, `DITEN_ESBP_ENFORCE_PERMISSIONS` açıkça true değilse development bootstrap ile bilinen tüm izinlere kimliksiz kullanıcı için true döndürüyor. Application ve E2E RBAC testlerinin üçü bu nedenle kalıyor. Canlı kimliksiz GET de 200.
- 🔴 **Gerçek HTTP E2E kanıtı yok:** “EndToEnd” projesi HTTP host/auth pipeline round-trip kanıtlamıyor. Bu yüzden yeşil olsa bile kural 6'yı karşılamaz.
- 🔴 **Lineage/validation davranışı kırık:** Application'da goal→objective→initiative→project akışı, duplicate alignment ve planning horizon beklentileri; E2E'de lineage ve stale-write/missing-permission beklentileri başarısız.
- 🟠 **Frontend 9 hata:**
  - `strategy-apis` dört hata: production helper artık `response.text()` çağırıyor, test double yalnız `json()` sağlıyor. Bu dört hata öncelikle test double sözleşme drift'idir; production davranışının doğru olduğunu kanıtlamaz.
  - objective edit hydration: kaydedilmiş `Transformation` yerine `Growth` kalıyor; edit hydrate sırası/değer kaynağı saved DTO'yu eziyor.
  - planning-cycle ve strategy-period owner position: scoped liste boşken global/API pozisyon fallback'i select'e gelmiyor.
  - planning-cycle ve strategy-period register: test DOM'unda üç lifecycle satırı hiç render edilmiyor; DataTable/fallback render başlangıç sözleşmesi drift etmiş.
- 🟠 **Golden Reference sözleşmesi yok:** İncelenen ESBP Razor yüzeylerinde `data-dt-standard="v2"` bulunmadı. Bazı ekranlar DataTables kullanıyor olsa da repository verifier'ın beklediği v2 deklarasyonu ve Slim/Compact seçimi yok.
- 🟠 **FG-003 yaygın ihlal:** Üç kapsamın Razor/JS dosyalarında en az 57 `style="..."` ve 73 `.style.` kullanımı bulundu. Bu sayım alt sınırdır.
- 🟠 **l10n eksik:** ES modülüne ait yedi dil `.resx` seti bulunmadı; Razor ve JS'de yoğun ham İngilizce var.
- 🟠 **Enum JSON sözleşmesi yok:** ES servisinde `JsonStringEnumConverter` bulunmadı; domain statü/öncelik/tür alanlarının çoğu zaten string. Global converter eklenmemiş olması doğru, fakat typed enum gereksinimi karşılanmıyor.
- 🟠 **Sessiz/degraded davranış riski:** PPM adapter sync startup sırasında “success” logladı; fakat gateway/frontend çalıştırılmadan servisler arası gerçek sözleşme doğrulanmadı. İki ayrı entegrasyon tasarımı icat edilmemeli.

## Decomposition Tree Builder

### Ne var

- Frontend controller, tek `Index` view ve `decompositionPage.js` mevcut.
- Domain'de structure, node, dependency, validation issue ve audit event modelleri mevcut.

### Ne çalışıyor

- Statik olarak route ve view var. Bu değerlendirmede frontend çalıştırılmadığı için canlı render doğrulanmadı.

### Ne kırık ve neden

- 🔴 **Paralel görev motoru ileri seviyede:** `DecompositionNode` yalnız `Type="Task"` ve `ResponsibleName` taşımıyor; ayrıca `DueDate`, `Status`, dependency, validation state, audit trail ve budget yaşam döngüsü taşıyor. Bu artık “iskele”den fazlası ve MOD-0024 görev motoruyla sınır ihlaline açık.
- 🔴 **Bayrak tabanlı onay mevcut:** `DecompositionStructureAggregate.ApprovedAt` ve `ApprovedBy` alanları doğrudan modelde. MOD-0023 dışı onay motoru riski gerçekleşmiş.
- 🔴 **Kimliksiz sorumluluk:** `ResponsibleName` serbest metin; kişi/pozisyon identity reference yok.
- 🟠 **UI inline CSS:** depth indentation `style="padding-left:..."` ile üretiliyor; FG-003'e aykırı.
- 🟠 **Golden Reference yok:** Outline tablosu JS ile oluşturuluyor ve DataTable v2 işareti yok.
- 🟠 **l10n eksik:** Modüle ait yedi dil kaynak seti yok; UI label'ları JS/Razor içinde ham metin.
- 🟠 **Canlı veri ayrımı doğrulanamadı:** Decomposition endpoint/persistence zinciri Swagger ve route bazında ayrıca izlenmeli. Şu kanıtla “UI yalnız DB boş olduğu için bozuk” denemez.

## Dört WorkCenter tehlikesinin ilerleme seviyesi

| Tehlike | Seviye | Kanıt | Karar |
|---|---:|---|---|
| (a) ES `TaskAggregate` | 🔴 Mevcut ve seed ediliyor | Üç alanlı aggregate korunmuş olsa da startup sekiz sahte task seed ediyor ve repository aktif. | Aggregate büyütülmemeli; seed/runtime sahipliği MOD-0024'e taşınmadan entegrasyon yapılmamalı. |
| (b) Decomposition node görevleşmesi | 🔴 İlerlemiş | `Type=Task`, `ResponsibleName`, `DueDate`, `Status`, dependency, validation/audit/budget alanları var. | Yaşam döngüsü ve atama eklenmesi durmalı; iş kaydı MOD-0024 referansı olmalı. |
| (c) Bayrak tabanlı onay | 🔴 Gerçekleşmiş | `ApprovedAt` + `ApprovedBy` domain modelinde. | “Onayla” UI/API yapılmamalı; MOD-0023 sözleşmesi kararı beklenmeli. |
| (d) Demand identity/reference boşluğu | 🔴 Gerçekleşmiş | Kişi, BU, priority/category/request type serbest metin; canlı seed de isim metni döndürüyor. | WorkCenter projection kurulmadan önce canonical kişi/pozisyon ve referans veri sözleşmesi kararlaştırılmalı. |

## Mongo envanteri

- Connection: `mongodb://localhost:27017`
- Database: yalnız `DitenEnterpriseDb`
- Health: erişilebilir.
- Startup üç migration çağırıyor: planning cycle, strategy library, strategic goal.
- Koddan görülen koleksiyon aileleri: `TaskAggregate`, `DemandIdeaAggregate`, goal/metric/yearly-target/budget, objective, planning cycle, strategy period, connections, initiative/project links ve cache'leri, KPI/library/template/import/version/usage, audit ve migration state/report/backup/manual-review.
- Seed:
  - Task koleksiyonu boşsa sekiz örnek task oluşturuluyor.
  - DemandIdea koleksiyonu boşsa altı örnek demand oluşturuluyor.
  - Canlı API altı demand seed'inin DB'de mevcut olduğunu doğruladı.
- Mongo shell istemcisi (`mongosh`/`mongo`) makinede bulunmadığı için doğrudan `listCollections`/index dump alınmadı. Şema envanteri context/repository/migration kodu ve canlı endpoint üzerinden çıkarıldı; hiçbir DB drop/reset yapılmadı.

## Önce düzeltilmeli sırası

1. **Authentication/RBAC fail-closed:** Gerçek authentication scheme ve permission claim pipeline sözleşmesi kararlaştırılıp kimliksiz HTTP istek 401/403 olmalı. Development bootstrap varsayılanı fail-open kalmamalı.
2. **Sözleşme ve ownership kapısı:** ES domain config/module pack eksikliği giderilmeli; MOD-0023/MOD-0024 ve kişi/pozisyon/reference-data sınırları yazılı sözleşmeye bağlanmalı. WC-5 tasarlanmadan servisler arası görev aktarımı yapılmamalı.
3. **Gateway/port sözleşmesi:** 5102 canonical olacaksa launch profile ve yalnız integration-agent tarafından gateway route'u uyarlanmalı; aksi karar sahibinden alınmalı.
4. **Dört kırmızı WorkCenter tehlikesi:** Task seed/runtime sahipliği, decomposition approval/task lifecycle ve demand identity alanları ikinci motor üretmeden ayrıştırılmalı.
5. **Backend davranış kırıkları:** 6 Application + 2 EndToEnd failure kök nedenleri ayrı dilimlerde ele alınmalı; her düzeltmede regression testinin önce kırıldığı kanıtlanmalı.
6. **Gerçek authenticated HTTP E2E:** Mongo'lu host üzerinde auth claim, create/read/update, stale write, cross-tenant/not-found ve enum JSON round-trip testleri eklenmeli.
7. **Frontend veri sözleşmeleri:** Önce 9 referans kırığı test-double mı ürün mü diye ayrılmalı; objective hydration ve owner-position fallback ürün davranışı olarak doğrulanmalı.
8. **UI standardizasyonu:** Her yüzey alan sayısına göre Slim/Compact kararı, `data-dt-standard="v2"`, tenant shell, SweetAlert2 ve FG-003 temizliği.
9. **Yedi dil l10n:** en/tr/fr/es/zh/ar/ru `.resx` ve `window.L10n`; ham anahtar/metin smoke.
10. **Canlı modül smoke:** Frontend/gateway izole port/oturum planı belirlendikten sonra üç modül ayrı ayrı authenticated smoke edilmeli.

## Sahibinin cevaplaması gereken açık kararlar

1. Enterprise Strategy için canonical domain/module pack kimlikleri hangileri? Mevcut branch'te ESBP domain config/module pack yok.
2. ES API canonical local portu 5102 mi olacak, yoksa mevcut 5004 sözleşmesi mi korunacak? Gateway değişikliği integration-agent işi olarak mı açılacak?
3. Development permission bootstrap tamamen kaldırılacak mı, yoksa yalnız açık environment flag ile opt-in mi olacak?
4. DemandIdea kişi alanlarının canonical tipi nedir: `PersonId`, `EmployeeId`, `PositionId` kombinasyonlarından hangileri zorunlu?
5. Demand `Priority`, `Category`, `RequestType`, `BusinessUnit` için canonical referans-data sahibi ve ID'leri nedir?
6. Decomposition node bir plan kırılımı mı, yoksa MOD-0024 work item referansı mı? ES tarafında hangi alanlar kalmalı?
7. Decomposition approval için MOD-0023'te hangi approval subject/type kullanılacak?
8. Mevcut seed kayıtları geliştirme fixtures olarak ayrı mekanizmaya mı taşınacak, tamamen kaldırılacak mı?
9. Enterprise Strategy ekranlarının tenant shell olduğu ve yedi dil kapsamına girdiği teyit ediliyor mu?
10. Üç modül için form alan sayıları ve buna bağlı Golden Slim/Compact kararları nelerdir?

## Okunan dosyalar

Aşağıdaki dosyalar doğrudan okundu; bunlara ek olarak belirtilen dizinlerde `rg` ile sembol/kontrat taraması yapıldı.

- `AGENTS.md`
- `.antigravity/workflows/read-only-audit.md`
- `.antigravity/rules/git-safety.md`
- `gateway/Diten.ApiGateway/ocelot.json`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/Program.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/appsettings.json`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/appsettings.Development.json`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/Properties/launchSettings.json`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/Controllers/HealthController.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/Security/EnterpriseStrategyPermissionAttribute.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Persistence/DbInitializer.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Persistence/DemandIdeaSeed.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Persistence/Context/MongoDbContext.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Domain/Aggregates/Task/TaskAggregate.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Domain/Aggregates/Decomposition/DecompositionStructureAggregate.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Domain/Aggregates/DemandIdea/DemandIdeaAggregate.cs`
- `services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.Application/EnterpriseStrategy/Shared/EnterpriseStrategyPlatform.cs`
- `services/Diten.EnterpriseStrategyService/tests/Diten.Application.Tests/UnitTest1.cs` (başarısız assertion çevreleri)
- `services/Diten.EnterpriseStrategyService/tests/Diten.EnterpriseStrategy.EndToEnd.Tests/EnterpriseStrategyLineageE2ETests.cs` (başarısız assertion çevreleri)
- `frontend/Diten.Web/package.json`
- `frontend/Diten.Web/vitest.config.js`
- `frontend/Diten.Web/Config/ManagementGovernanceRegistry.cs`
- `frontend/Diten.Web/Controllers/DemandIdeasController.cs`
- `frontend/Diten.Web/Controllers/DecompositionTreeBuilderController.cs`
- `frontend/Diten.Web/Controllers/EnterpriseStrategyBusinessPerformanceController.cs`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (yalnız ilgili navigation eşleşmeleri)
- `frontend/Diten.Web/Views/DemandIdeas/**` (kontrat taraması)
- `frontend/Diten.Web/Views/DecompositionTreeBuilder/**` (kontrat taraması)
- `frontend/Diten.Web/Views/EnterpriseStrategyBusinessPerformance/**` (kontrat taraması)
- `frontend/Diten.Web/wwwroot/assets/js/pages/demand-ideas/**` (kontrat taraması)
- `frontend/Diten.Web/wwwroot/assets/js/pages/decomposition/**` (kontrat taraması)
- `frontend/Diten.Web/wwwroot/assets/js/pages/enterprise-strategy/**` (kontrat taraması)
- `frontend/Diten.Web/tests/strategy-apis.test.js`
- `frontend/Diten.Web/tests/objectives-edit-hydration.test.js`
- `frontend/Diten.Web/tests/planning-cycles-owner-position.test.js`
- `frontend/Diten.Web/tests/planning-cycles-register.test.js`
- `frontend/Diten.Web/tests/strategy-periods-owner-position.test.js`
- `frontend/Diten.Web/tests/strategy-periods-register.test.js`

## Git başlangıç logu

```text
ea31b77c docs(workcenter): audit findings, the action gap and a test sequence
3302450e fix(workcenter): show nothing rather than invent it
cff4587d docs(workcenter): inventory of the mock↔real seam
651b2df2 fix(workcenter): the pool stops naming a queue that does not exist
d880bbea docs(workflow): the gate's fail-closed contract documents the gate
ea02bb95 fix(workflow): the transition gate survives a real Mongo query
79dcf335 fix(tasks): a blocked transition answers 409 with a translatable reason
315433c9 fix(auth): post-login lands on the live Task Center, not the dead one
f5bc8ec8 fix(tasks): the review toggle no longer looks functional while doing nothing
9b5439c1 feat(tasks): Phase 3 complete — approval decided by MOD-0023, reported by MOD-0024
8f6f38bb feat(tasks): Phase 3 (partial) — approval handed to MOD-0023 through the transition gate
5c9e2e17 docs(backlog): BL-028 — task dependencies are a half-built capability
d4e8e3c9 feat(tasks): MOD-0024 Phase 2 frontend — checklists and subtasks wired to the engine
140287ae feat(tasks): MOD-0024 Phase 2 backend — checklists, subtasks and task templates
204149d4 fix(web): stop the second token-bridge pass from deleting the session it just refreshed
3eb749b9 feat(tasks): assignable-people picker and a user display-name seam
70043063 feat(tasks): carry assignee and requester in the projection, and stop mislabelling a missing plan date
3db2847e feat(tasks): real lifecycle transitions from the Task Center, and providers declare their own permissions
5f2bbf0e feat(tasks): MOD-0024 Phase 1 — task creation runtime, live in the Task Center
ceca702f feat(shared): add a premium modal helper so the MOD-0013 standard lives in one place
```

# MVP6-LOADS-UI-SCOPE-PREP-01 — önerilen ilk dilim

2026-09-24 · Tasarım/dispatch hazırlığı; **UI DEV ve VER HELD**. Backend pack §28'deki ready-for-dev, shell:none olan mock-first backend kapsamına aittir; UI yetkisi değildir.

## Yeniden kullanım ve writer sınırı

Repo docs/roadmap/plans ve frontend dosya/envanter aramasında mevcut Loads UI paketi veya Loads frontend dosyası bulunmadı. Mevcut MOD-0185 pack, published Loads annex, acceptance-consolidation-02 ve son effort update-05 tüketildi; kopya backend paketi hazırlanmadı. Uygulama task envanterinde diğer aktif lane'ler var; kısa task özetleri aktif writer yokluğunu kanıtlamaz. Bu klasör bu task'ın tek spec çıktısıdır. DEV dispatch öncesinde CT'nin tek Loads UI writer ataması ve aktif-writer çakışma kontrolü zorunlu; başka lane'in yazarı olduğu varsayılarak dispatch yapılmaz.

## Kaynak → operasyon → ekran

Published `docs/analysis/contracts/shipment-bundle.openapi.yaml` paths altında queryLoads (satır414), createLoadPlan (615), transitionLoad (996); exact hash SOURCE-HASHES.tsv'dedir. Publication authority `mvp6-loads-r2-publication-2026-09-18/README.md`; annex'in tarihsel “NOT PUBLISHED” preamble'ı bu kayıt ve pack§28 ile birlikte okunur. Current YAML metadata drift'i ayrıca VALIDATION'da gösterilir; eski dosya hash'i güncele taşınmaz.

Backend kaynağı `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Loads/LoadsController.cs` List/Create/Transition; permission sabitleri Infrastructure/Features/Loads/LoadPermissions.cs. Application/Features/Loads/LoadModels.cs ve Handlers/QueryHandlers/GetLoadListHandler.cs gerçek response alanlarını belirler. Kabul edilen 47-path snapshot `mvp6-mod0185-acceptance-consolidation-02/loads-scope.tsv`; kabul bütün güncel checkout'a genişletilmez.

| Ekran/işlem | Önerilen MVC / proxy route | Gerçek Gateway contract route | İzin / durum |
|---|---|---|---|
| Liste, yenile, inline filtre | GET /SupplyChain/Loads; GET /SupplyChain/Loads/api | GET /api/shipment-bundle/loads?status=&carrierId= | supplychain.loads.read; ilk dilim |
| Index içi oluşturma offcanvas | POST /SupplyChain/Loads/api | POST /api/shipment-bundle/loads | supplychain.loads.create + ekran için read; ilk dilim |
| Lifecycle transition | Bu dilimde MVC route/button YOK | POST /api/shipment-bundle/loads/{loadId}/transition | supplychain.loads.transition; backend mevcut, UI sonraki ayrı scope HELD |
| Detay/edit/delete/bulk/import/lookup/optimizasyon | YOK | Desteklenen karşılık yok | Uydurulmaz |

Tenant shell `_LayoutTenantShell.cshtml`; `/Platform` prefix'i yok. `proxy-profile`: browser aynı-origin MVC'ye, MVC mevcut GatewayUrl üzerinden Gateway'e; HttpOnly token yalnız server tarafında. Browser doğrudan service portuna gitmez, Authorization üretmez. Anti-forgery ve mevcut refresh mekanizması reuse edilir; yeni auth altyapısı kurulmaz.

Liste yalnız `loadId, loadNumber, carrierId, shipmentIds, status` gösterir. Stops, mode, plannedDepartAt, root, version veya detay kaydı backend listesinde yok: boş alanı gerçek bilgi diye gösterme. Carrier/Shipment isimlerini enrichment ile icat etme; opaque ID açık etiketli ve escaped gösterilir. Summary schema alanları required değildir; eksik alan “bilgi sağlanmadı” görünür, action için ID yoksa action üretilmez. Liste items/total/v1 doğrulaması yapılır; hata boş listeye çevrilmez.

API'de paging/search/sort yok. `serverSide:false`; client search/sort/paging yalnız dönen kapsam üzerinde. Status backend'de tek değer: proposed single Select2 için açık pack istisnası, `All` parametreyi omit eder; çoklu status göndermek veya N request fan-out yok. Carrier UUID filtre girişi lookup değildir. Save View, colvis/ColReorder ve factory Reset shared personalizationClient üzerinden; client localStorage preference sistemi yok. Import/delete/edit gibi desteklenmeyen template kontrolleri çıkarılır.

## Form sayımı ve Golden seçimi

Create/edit business alan türleri:
1. carrierId — tek UUID girişi.
2. shipmentIds — tekrarlanabilir UUID listesi, en az1; dizi sırası korunur.
3. mode — Road/Air/Sea/Rail/Parcel frozen enum, çevrilen label.
4. plannedDepartAt — offset'i açık tarih/saat girişi; wire RFC3339, aynı instant.
5. stops[].sequence — pozitif sıra girdisi.
6. stops[].locationReferenceId — opaque string; lookup değil.
7. stops[].action — Pickup/Delivery/Return.

**7 business alan türü → GoldenReferenceSlim (≤8)**; nested stops container ayrıca alan sayılmaz. Minimum iki stop ile fiili input sayısı `4 + 2×3 = 10`; daha çok stopta artar. Tekrarlanan satırlar yeni schema alan türü değildir. Bu ayrım gizlenmez: offcanvas repeatable editor ergonomisi 390/768 acceptance gate'idir. Owner fiili kontrol sayısını esas alarak Compact isterse ayrı scope kararı gerekir; sessiz template geçişi yapılmaz. Thin controller yalnız shell döndürür, modelle liste doldurmaz. Index/_CreateEditOffcanvas create-only; edit davranışı eklenmez. Correlation, idempotency key, Tenant/LE, load number/id, audit, version kullanıcı business alanı değildir; sayılmaz ve editable olmaz.

Required presence ile nonempty aynı değildir: locationReferenceId string boş olabilir, UI NotEmpty/trim/maxlength icat etmez; plan zamanı için gelecekte olma şartı yok. Stops en az2, sıra tam1..N, Pickup+Delivery; duplicate Shipment UUID değeri422. Date offset korunarak instant normalizasyonu; shipment/stop array sırası sessiz değişmez. Carrier Active+mode, Shipment Draft/Planned ve carrier eşleşmesini server doğrular. UUID girişinin geçmesi referansın bulunması/uygunluğu garantisi değildir.

## İlk kullanılabilir akış ve açık kapılar

Yetkili planlayıcı scoped listeyi görür; elindeki authoritative Carrier/Shipment/location referanslarını girer; oluşturma sonucu Draft ve LOAD-... numarasını alır; listeyi yeniden yükleyerek persisted satırı görür. Lookup/picker API'si eklenmez. Reference bilgisi erişilebilir değilse kullanıcıya yeni master oluşturma önerilmez; entegrasyon owner'ına yönlendiren açıklama verilir. Bu bounded create/list dilimi load lifecycle yönetiminin tamamı değildir.

Create intent'i için UUID correlation ve exact key bir kez hazırlanır; transient network/503 veya kayıp cevapta aynı body/key/root ile retry edilir. Fingerprint değişirse eski intent'in sonucu çözülmeden yeni key ile kör yeniden gönderim yapılmaz; çift-tıklama tek pending request. Create/replay HTTP201 ve idempotentReplay ayrımı korunur; replay snapshot güncel state sanılmaz, GET liste yeniden alınır. Intent'in tab kapanışından sonraki recovery mekanizması ayrı teknik karar: bu dilim tablar arası dayanıklı receipt deposu eklemez; belirsiz sonuçta “yeniden oluştur” otomasyonu yoktur.

**ROOT-UI-01 OPEN:** LoadSummary/LoadResponse immutable root'u vermez, by-ID GET yok. UI mevcut satır için root üretmez; response GET correlation'ını root sanmaz; DB/audit'ten okumaz; kullanıcıya teknik root alanı açmaz; browser cache'i kalıcı authority yapmaz. Dolayısıyla transition UI bu scope dışında. Aynı oturumda create root'u bilmek reload/başka actor akışını çözmez.

**LIVE-185 OPEN:** kabul edilen backend GET-only mock references kullanır. Gerçek Carrier/Shipment reference yetkilendirmesi, live uptake ve multi-Shipment root grouping kararı ayrıca gerekir. Create kendi Load root'unu backend contract'a uygun taşır; bu, Shipment köklerinin birleştirilmesini çözmüş sayılmaz. Bounded mock UI testi ancak SIMULATED dependency etiketiyle; live UI acceptance ve rollout bu kapılar kapanmadan PASS olmaz. Root/producer/gateway değişikliği bu UI writer'a devredilmez.

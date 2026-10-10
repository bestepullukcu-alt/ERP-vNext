# MVP6-SUPPLIER-EXACT-POLICY-01 — tek karar paketi

Prompt v1.0 · 2026-09-23 · INS / module-pack-author koordinasyonu · **PROPOSED; runtime HELD**.
Bu metindeki “olur/reddedilir” ifadeleri seçilmesi önerilen politikanın acceptance hükümleridir; mevcut production davranışı veya alınmış owner onayı değildir. Önceki seam-readiness paketini tüketir; readiness denetimini tekrarlamaz. [Acceptance](ACCEPTANCE.md), [yetki matrisi](AUTHORITY-MATRIX.md) ve [kaynak/doğrulama](VALIDATION.md) bu tek paketin parçalarıdır.

## SS-01 — ownership

Seçenek A (öneri): MOD-0147/0148 execution sahibi supply-chain-execution, servis Diten.SupplyChainService; Supplier master MOD-0140'da kalır. B: ayrı domain/service; yeni sınır ve operasyon maliyeti doğurur. A, WP-MVP6 ve mevcut draft pack niyetini korur; domain-config/DCP/registry mutabakatı yerine geçmez. AC-01. CT Supplier Seam Owner koordine eder; Enterprise/domain owner concurrence beklenir.

## SS-02 — mevcut base

A (öneri): SUPPLIER-BASE adı mevcut FROZEN SUPPLIER 1.0.0 identity/status tüketimini ifade eder. B: yeni Supplier base/master oluşturmak; seçilmesi önerilmez. Mevcut “merkez üretecek/absent” metinlerinin ileride ayrı yetkili revizyonla düzeltilmesi önerilir; bu pakette değiştirilmez. İletişim/tax/profile kopyası kurulmaz. SUPPLIER v1'in varlığı canlı producer, LE eligibility veya identity-binding kanıtı değildir. AC-02; MOD-0140 + iki consumer concurrence.

## SS-03 — uygunluk ve çağrı seçimi

A (öneri): tek Supplier için yeni evaluation, evaluation submit, risk register ve tüm portal işlemlerinde `getSupplier`; `listSuppliers` uygunluk veya actor mapping için kullanılmaz. B: her komutta singleton `validateSuppliers`; mevcut nullable/completeness belirsizliği nedeniyle ilk tercih değildir. Toplu işlem bu slice'ta yoktur; gelecekte validate kullanılırsa unique input ID başına tam bir sonuç, ID ile eşleştirme (sıra bağımsız), missing/duplicate/extra ID veya bilinmeyen enum → dependency-invalid/fail-closed gerekir. `known=false` yalnız explicit unknown; boş liste unknown anlamına gelmez.

Her yeni iş komutunda trusted Tenant+LE ve MOD-0140'ın o LE için affirmative Supplier eligibility kanıtı gerekir. Tenant-scoped getSupplier tek başına LE kanıtı değildir. LE bilgisi yoksa **GAP; runtime HELD**, Active varsayılmaz. Opaque ID eşitliği ordinal/exact; trim/case dönüşümüyle başka kimliğe yönlendirme yoktur.

| İşlem | Active | OnHold | Blocked | Inactive |
|---|---|---|---|---|
| Yeni evaluation / ilk publish | izin | 422 business rule | 422 | 422 |
| Yeni SupplierRisk | izin | izin | izin | izin |
| Var olan internal evaluation/scorecard/risk okuma, risk status | local Tenant+LE yetkisiyle izin | aynı | aynı | aynı |
| Portal status (geçerli binding+LE) | ACTIVE | SUSPENDED | SUSPENDED | SUSPENDED |
| Portal list/get/create/ilk submit | izin | 403 | 403 | 403 |

Risk kaydı/remediation kötü Supplier durumunda da gereklidir; risk register yine Supplier'ın varlığı ve LE eligibility doğrulamasını ister. Tarihsel internal okuma/status değişimi upstream kesintisine bağlanmaz; local scope ve izin zorunludur. Portal SUSPENDED status yalnız kendi kimlik/status projection'ını, `openSubmissionCount=0`, lastSubmissionAt olmadan döndürür; kayıt içeriği açmaz. PENDING bu slice'ta üretilmez: binding yokluğu PENDING başarı cevabı değil 403'tür. Alternatif OnHold'a corrective-action erişimi verilmesi daha geniş surface gerektirir; önerilmez.

Internal create unknown veya scope-dışı Supplier → 404 UNKNOWN_SUPPLIER; portal mapping'in Supplier'ı unknown/LE-ineligible → 403 identity unresolved. Base timeout, malformed/mismatched ID/status, incomplete response → 503 SUPPLIER_BASE_UNAVAILABLE, sıfır write/event. Current UUID correlation response/audit'e taşınır; v1 `req-1` örneği UUID garantisi değildir. AC-03..06. Yeni 403/503 kapsamları mevcut OAS'ın tüm route'larında tanımlı değildir; SS-04 kapanmadan implement edilemez.

## SS-04 — strictness ve wire kapısı

A (öneri): frozen byte'ları koruyarak tek-yazarlı, ayrıca review edilecek versioned successor/annex hazırlığı. B: consumer içinde sessiz tolerans; önerilmez. Successor incelemesinde required results/known/status, unknown için gerçek OAS3.1 null union, UUID correlation, 503, LE eligibility ve aşağıdaki yeni hata kapsamları açıkça yazılmalı. Bunlar bu pakette yeni API onayı değildir. Required alan/hata/status davranışları strict consumer'ı bozabilir; additive/minor uyumluluk varsayılmaz. Sürüm ve endpoint sahibi ayrıca exact diff üzerinden karar verir. SS-03/05/06'nın yeni davranışları mevcut v1 authority'si diye uygulanamaz. AC-07.

## SS-05 — actor mapping / cardinality / revocation

A (öneri): security-owned authoritative binding, key `(validated issuer, stable subject, trusted TenantId, trusted LegalEntityId)`; value opaque MOD-0140 SupplierId + binding identity/revision + active/revoked state. B: 1:N binding ve client selection; ilave selector/authorization seam gerektirir, bu slice'a alınmaz. Bir actor aynı Tenant+LE içinde **en fazla bir active Supplier**; bir Supplier birden çok actor'a bağlanabilir. Aynı actor farklı yetkili LE'lerde farklı binding taşıyabilir; her request bir trusted LE içerir, otomatik ilk-LE seçimi yoktur. Email, contact, displayName veya userId=SupplierId çıkarımı yasaktır. Binding alanları önerilen semantics'tir, mevcut claim veya endpoint ilanı değildir.

JWT geçersiz/expired → 401; authenticated ama trusted scope/permission yok → 403; mapping none/revoked/ambiguous → 403 PORTAL_SUPPLIER_IDENTITY_UNRESOLVED; mapping authority timeout → önerilen 503 PORTAL_IDENTITY_UNAVAILABLE (ayrı contract review gerekir). Scope ve mapping çözüldükten sonra target `(Tenant,LE,Supplier,Id,!IsDeleted)` ile aranır; yok/cross-supplier/cross-LE → 404 UNKNOWN_SUBMISSION. Başka kaydın varlığına göre 403/404 değiştirilmez. Body/query scope override girişimi trusted identity'yi değiştirmez; authenticated scoped request'te 400 INVALID_REQUEST. Payload içindeki aynı isimli değerler de kimlik kaynağı olamaz.

Her request/replay öncesinde güncel binding kontrolü; revocation commit'inden sonra başlayan request eski token/cache ile erişemez. Mutation commit öncesi binding revision fencing gerekir: revoke commit'i daha önce olduysa write/event yok ve 403; mutation daha önce commit olduysa tarihsel sonuç kalır ama sonraki erişim reddedilir. Bu atomik güvenceyi sağlayan canlı seam **GAP**; TTL tahmini revocation kanıtı değildir. Permission ve tenant/LE membership de güncel authorization gate'idir. AC-08..12.

## SS-06 — replay

A (öneri): receipt scope `(TenantId, LegalEntityId, mappedSupplierId, validatedIssuer, subject, operationId, targetId-or-CREATE, exactIdempotencyKey)`; actor ayrımı aynı Supplier'daki farklı kişinin receipt'ini açmaz. B: yalnız Supplier bazlı ortak receipt; actorlar arası sonuç paylaşımı nedeniyle önerilmez. LE eksik draft pack index'i ileride explicit amendment ister. Key 1..200 karakter; baş/son whitespace reddedilir, trim edilerek başka key ile birleştirilmez (draft pack'teki trimmed hükmüne önerilen değişiklik). Operation/target ve identity ordinal/exact.

Öncelik: authentication → trusted scope/permission → syntactic validation → current mapping/base portal eligibility → own target access → receipt → yeni işlemse If-Match/lifecycle → atomic write+receipt+outbox. Aynı tuple ve aynı semantic request replay → 200 özgün domain result, yeni event/version artışı yok; current request UUID response correlation, özgün correlation audit/receipt'te korunur (exact wire refinement gerekir). Request fingerprint: parsed JSON'un object-key sırasından bağımsız, array sırasını ve string değerlerini koruyan typed snapshot; method/operation/target/body ve If-Match dahil, correlation hariç; duplicate JSON property reddedilir. Aynı key farklı fingerprint → önerilen 409 IDEMPOTENCY_KEY_REUSED (v1'de ilan edilmemiş). Aynı key/body/original If-Match ile başarılı submit retry, artık SUBMITTED/stale-version olsa da receipt döner. Yeni key stale version →409 VERSION_CONFLICT; current version fakat SUBMITTED→SUBMITTED →422 INVALID_STATUS_TRANSITION.

Supplier S1→S2 remap eski S1 receipt'ini açmaz; aynı key S2 altında bağımsızdır. Revoked binding hiçbir receipt açmaz. Yeni active revision aynı actor+Supplier için önceki receipt'i okuyabilir, ancak current izin/eligibility geçmelidir; eski binding revival varsayılmaz. Receipt scope current target erişimini bypass etmez. Concurrent aynı tuple commit/unique-index yarışı tek result ve tek outbox üretir; loser fingerprint eşitse replay, farklıysa409. Receipt retention önerisi: ilgili business record ömrü boyunca; expiry ile duplicate create açılmaz. AC-13..17.

## SS-07 — Metric Registry / scoring

A (öneri): ilk policy `supplier-score-policy/1` yalnız registry-authorized, higher-is-better yüzde metrikleri; ölçüm 0..100, weight >0..100, benzersiz metricCode, toplam weight tam100. B: arbitrary UoM/direction/normalization; ayrı formula/version sözleşmesi olmadan önerilmez. Registry immutable revision'u Tenant+LE kapsamı, code, unit=PERCENT, direction=HIGHER_IS_BETTER, validity interval ve policy uyumluluğunu doğrular. Client weight v1'de mevcut olsa da seçilen registry policy weight'iyle exact numeric eşit olmalıdır; client scoring politikasını seçmez.

Input measuredValue/weight en çok 4 fractional digit, işaretli negatif reddedilir; scale aşımı sessiz yuvarlanmaz, 422. Decimal exact arithmetic; `sum(value * weight)/100`; ara yuvarlama yok, yalnız overall output iki basamak **round-half-even**. Individual score measuredValue'nun iki basamak half-even projection'ı; aggregate bu display değerlerinden hesaplanmaz. Risk bandı yuvarlanmamış aggregate üzerinden: [90,100] LOW; [75,90) MEDIUM; [50,75) HIGH; [0,50) CRITICAL. Bu scorecard riskLevel'ıdır, SupplierRisk açma/level değiştirme otomasyonu değildir.

Create'de server-resolved immutable metric revision+policy revision+weights+inputs+scope+source hash snapshot; submit aynı snapshot'tan hesaplar, registry'nin yeni revision'u eski evaluation'ı değiştirmez. Registry revision create request anında effective olmalı; unavailable, missing revision veya stale validity → sıfır write. Bilinen unsupported metric/unit/range →422 BUSINESS_RULE_VIOLATION; registry outage/incomplete authority → önerilen503 METRIC_REGISTRY_UNAVAILABLE. Snapshot'ın authenticity/revocation durumu submit'te doğrulanamıyorsa503; explicit withdrawn revision →422. Tarihsel scorecard yeniden hesaplanmaz. v1 metric revision/snapshot provenance sunmuyor: exact carrier ve canlı registry pointer **GAP**, yeni producer API onayı değil. AC-18..22; Metric owner ve bandlar için Risk concurrence.

## SS-08 — Risk Register rolü / lifecycle

A (öneri): RISK-REGISTER taxonomy/reference authority; SupplierRisk instance, version, lifecycle ve outbox MOD-0147'de. B: external register her instance'ın SoR'u; mevcut local-owned pack'e daha büyük sınır değişikliği gerektirir. Taxonomy immutable revision yalnız mevcut altı category ve dört level'ın izinli değerlerini doğrular; yeni taxonomy enum sessiz kabul edilmez. Register çağrısı live authority doğrulaması ister; outage/incomplete → önerilen503 RISK_REGISTER_UNAVAILABLE; scope-uyumlu olmayan/tanınmayan değer →422. Instance source/taxonomy revision snapshot saklanır; full registry clone yoktur.

Başlangıç OPEN; izinli geçişler **OPEN→ACKNOWLEDGED→MITIGATED→CLOSED**. Diğer bütün çiftler (self, reopen, skip dahil)422 INVALID_STATUS_TRANSITION; stale If-Match öncelikle409; başarılı receipt replay lifecycle öncesindedir. MITIGATED ve CLOSED için trimmed nonempty resolutionNote (max2000); MITIGATED'de server UTC resolvedAt set edilir, CLOSED'da korunur; her başarılı geçiş version+1 ve tek event. Diğer geçişler resolvedAt üretmez. Lifecycle local snapshot'ı kullanır; registry kesintisi remediation'ı durdurmaz. Deletion endpoint'i/yeniden açma/otomatik closure önerilmez.

SCORECARD sourceId: aynı Tenant+LE+Supplier'da published scorecard. PORTAL_SUBMISSION: aynı scope'ta SUBMITTED veya sonraki gözden geçirme statüsü; DRAFT reddedilir. Erişilemeyen/mismatched source →422 generic BUSINESS_RULE_VIOLATION; varlık detayı yok. EXTERNAL_SIGNAL ve MANUAL v1 enum'unda korunur, fakat authoritative source/evidence resolver tanımlı olmadığından ilk bounded slice'ta kabul edilmez (422). Alternatif serbest sourceId'yi güvenilir kanıt saymak önerilmez. Bu business kısıtlama ayrıca consumer contract concurrence gerektirir. Yeni Evidence API'si icat edilmez. Cross-module portal source resolver canlı seam'i GAP. AC-23..26.

## SS-09 — fixture sınırı

A (öneri): `supplier-exact-policy-fixture/1` adlı gelecekteki test-only fixture seti; AC tablosundaki fixed actor/scope, base statuses, mapping revisions, metric revision ve taxonomy snapshot'larını simüle eder. B: mock success'i production readiness saymak; kabul edilmez. Bu WP yalnız fixture specification verir, çalışan adapter/API/DI veya test implementation üretmez. Fixture policy approval ve ilgili concurrence sonrası ayrı bounded WP'de yazılır; outputs SIMULATED olarak işaretlenir, production config'te register edilmez. Timeout/malformed/revocation/LE isolation vakaları başarı fixture'ı kadar zorunludur. Fixture pass canlı auth/producer entegrasyonu, G5 veya runtime yetkisi sağlamaz. AC-27.

## Tek seferde verilebilecek somut karar metni

“Bu pakette SS-01…SS-09 altında A olarak işaretlenmiş önerileri ve AC-01…27 acceptance hükümlerini **sonraki spec hazırlığının policy hedefi** olarak seçiyorum. SS-01 SCE ownership ve SS-02 mevcut SUPPLIER v1 tüketim önerisini koruyorum. MOD-0140, security, Metric/Risk ve consumer concurrence'larının AUTHORITY-MATRIX'te ayrı ve halen bekleyen kararlar olduğunu kabul ediyorum; benim bu seçimim onların beyanı değildir. Yeni LE/binding/revocation/metric/taxonomy/source-resolution producer seam'lerini, endpoint/claim taşıyıcılarını veya contract version'ını onaylamıyorum; bunlar GAP olarak kalır ve ayrıca exact owner artefaktı gerektirir. Fixture yalnız ayrı test hazırlığı önerisidir. Bu karar domain-config/DCP/registry/pack/contract/runtime değişikliği, publication, permission seed, yeni Supplier master, client identity selection veya runtime activation yetkisi vermez.”

Karar henüz alınmadı. İstenmeyen A maddesi için ilgili SS ID ve seçilen alternatif açıkça belirtilmelidir; sessizlik concurrence değildir. Uygulama için ihtiyaç duyulan GAP'ler onay metnine tamamlanmış business kuralı olarak taşınmaz.

# MOD-0185 design approval — 2026-09-17

The repository user explicitly approved D185-01…05 v1.0 in this task.
This approves design decisions and preparation of a Loads amendment candidate,
not canonical publication, pack promotion, runtime, commit or push.

Approved proposal SHA256: 703d52b434f800dd0ad7632655a8f704b0f0fd9a1fc2484f1d232bec1d3ea236

The exact approved proposal is preserved below. Proposed release version remains
subject to compatibility assessment; it is not an approved minor-version claim.

---

**MVP6-MOD0185-OWNER-DECISIONS-01 — karar önerisi v1.0**

**Sonuç: D185-01…05 için uygulanabilir tek öneri paketi hazır; henüz onaylanmış karar veya DEV GO değildir.** Öneri, Loads’a özgü versioned amendment gerektiriyor. Pack `draft` kalır; Phase 1.5 ve merkezi dispatch ayrı kapılardır.

Branch ve HEAD beklenen değerlerle eşleşiyor: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Worktree önceden kirliydi. Bu görevde dosya, runtime, test, contract veya pack değiştirilmedi; commit/push/stash yapılmadı.

Dayanaklar: [MOD-0185 §§22–27](/Users/natig/Projects/ERP-vNext-recovery/execution/domains/supply-chain-execution/module-packs/MOD-0185-routing-load-planning.md:190), [frozen sözleşme](/Users/natig/Projects/ERP-vNext-recovery/docs/analysis/contracts/shipment-bundle.openapi.yaml), [CT SOP](/Users/natig/Projects/ERP-vNext-recovery/docs/guides/operations/control-tower-sop.md:1186), [Carrier karar kaydı](/Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-09/mod-0184-contract-owner-decisions-v1.0.md) ve [güncel CT devam kaydı](/Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-09/mod-0184-ct-continuation-2026-09-17/README.md).

Aşağıdaki acceptance sonuçları **önerilen test oracle’larıdır; çalıştırılmış runtime testleri değildir.**

**Contract referansları**

Bütün pointer’lar mevcut `shipment-bundle.openapi.yaml` dosyasınadır. Doküman sürümü **1.1.0**, wire `contractVersion` **v1**’dir.

| Kısaltma | Exact pointer / mevcut gerçek |
|---|---|
| Q | `#/paths/~1loads/get` — `queryLoads`; yalnız 200 |
| C | `#/paths/~1loads/post` — `createLoadPlan`; 201/404/409/422 |
| T | `#/paths/~1loads~1{loadId}~1transition/post` — `transitionLoad`; 200/404/422; **409/default yok** |
| CC / TC | `#/components/schemas/CreateLoadCommand`, `TransitionLoadCommand` |
| ST | `#/components/schemas/LoadStop` |
| SS / SD | `#/components/schemas/ShipmentSummary`, `ShipmentDetail` |
| CS | `#/components/schemas/CarrierSummary` |
| HC / HI | `#/components/parameters/CorrelationId`, `IdempotencyKey` |
| LR / ER | `#/components/schemas/LoadResponse`, `Error` |
| EV / EP | `#/components/schemas/LifecycleEventEnvelope`, `LoadEventPayload` |

**Tek önerilen karar seti**

| Karar | Mevcut frozen gerçek | Önerilen davranış | Gerekçe | Pozitif / negatif örnek | Etkilenen operation/schema | Niteliği |
|---|---|---|---|---|---|---|
| **D185-01 — uygunluk** | Shipment durumları tanımlı; Load için uygun altküme tanımlı değil. Shipment’ta mode alanı yok. Shipment ID dizisinde `uniqueItems` yok; stop sequence yalnız ≥1. | Uygun Shipment: **Draft veya Planned**. Carrier Active olmalı; Load.mode, Carrier.supportedModes içinde bulunmalı. Shipment.carrierId null veya seçilen Carrier olmalı. Shipment ID’leri benzersiz; stop sequence kümesi tam `1..N`; en az bir Pickup ve bir Delivery. Return ek stop olarak geçerli. | Planlama öncesi sevkiyatları kapsar; Shipment’ta olmayan mode karşılaştırmasını icat etmez. Tek Shipment’ın aynı Load içinde çoğaltılmasını önler. | Planned + eşleşen Carrier + Road destekli →201. InTransit →422; Carrier uyuşmazlığı →422; `[S1,S1]` →422; sequence `[1,3]` →422. | C, CC, ST, SS, CS; `ShipmentStatus`, `TransportMode` | **Versioned amendment:** schema-valid girdilere yeni business koşulları getirir. Sessiz clarification sayılamaz. |
| **D185-02 — referans zamanı** | Published GET’ler var; lease, conditional reference validation veya atomik çoklu okuma yok. | Fresh create ve hedefi **Planned/Tendered/Accepted/Dispatched** olan fresh transition’da Carrier ve tüm Shipment’ları yeniden oku. Cancelled/Completed ve başarılı replay’de yeniden okuma yapma. Her başarılı fresh işlem kendi gözlem snapshot’ını saklasın. | Dispatch öncesi güncel kontrol sağlar; iptal/kapanışı bağımlılık kesintisine bağlamaz. Replay geçmiş sonucu temsil eder. | Carrier önce Suspended olmuşsa ileri transition reddedilir. GET sonrasında Suspended olursa yerel commit yine gerçekleşebilir; kanıt snapshot zamanını gösterir. | C/T; `#/paths/~1carriers/get`, `#/paths/~1shipments~1{shipmentId}/get`; EV | **Versioned normative açıklama**; cross-service atomiklik sağlamaz. |
| **D185-03 — assignment** | Load lifecycle mevcut; assignment tutma/bırakma kuralı yok. Shipment yazımı bu scope’ta yok. | Assignment **Draft, Planned, Tendered, Accepted, Dispatched ve Completed** durumlarında tutulur. Yalnız başarılı Cancelled transition’ında bırakılır. Soft-delete bırakmaz. | Completed Shipment’ın yeniden Load’a alınmasını önler. Soft-delete, business iptalinin yerine geçmez. | İki Load aynı Shipment’ı yarışarak alır: biri başarılı, diğeri409. Cancelled commit sonrası yeni Load alabilir. Completed sonrası alamaz. | C/T; `LoadStatus`, SS.loadId; yerel assignment constraint | **Versioned business amendment** + Phase 1.5 persistence kararı. |
| **D185-04 — security/replay** | Bearer, required correlation/key ve Error shape var; Loads hata/response-header/replay semantiği eksik. EV root değişmez; HC event correlation ile eşitlik ister. | Aşağıdaki tam matris; mutation zincirinde **aynı root zorunlu**. Farklı correlation’lı mutation/replay409. Başarılı replay orijinal sonuç + `idempotentReplay:true`. | Carrier’da event zinciri bulunmadığından Carrier cross-root replay politikasını Loads’a taşımamak gerekir. | Aynı key/payload/root →orijinal201/200. Farklı root →409; event/audit değişmez. | Q/C/T, HC/HI, LR/ER, EV | **Versioned amendment zorunlu**, özellikle T409 ve bütün response headers. |
| **D185-05 — durability/events** | LoadNumber string; generator belirtilmemiş. EV/EP alanları ve event enum’ları mevcut. | Server UUID tabanlı LoadNumber; tek Mongo transaction’da aggregate+assignment+receipt+audit+outbox. Root create correlation. Local pending outbox; publisher yok. | Counter servisi/dağıtık transaction eklemeden benzersizlik ve tekrar güvenliği sağlar. | Commit öncesi fault →tümü rollback. Commit sonrası yanıt kaybı →receipt replay; ikinci event yok. | C/T, LR.loadNumber, EV/EP | Generator/transaction **Phase 1.5 clarification**; dış replay/event garantileri versioned annex’e bağlanmalı. |

**D185-01/02 — referansların exact yorumu**

Önerilen kontroller:

- Carrier, scoped `GET /carriers?status=Active` sonucunda **tam bir** eşleşmeyle bulunur. Yoksa `404 CARRIER_NOT_FOUND`; bu cevap “yok/inactive/başka scope” ayrımını açıklamaz. Birden fazla eşleşme veya Active filtresine aykırı response →`502 DEPENDENCY_RESPONSE_INVALID`.
- Her Shipment, scoped `GET /shipments/{shipmentId}` ile okunur. Explicit `carrierId:null` kabul edilir; farklı Carrier ID’si `422 SHIPMENT_CARRIER_MISMATCH`.
- Create sırasında explicit `loadId:null` gerekir. Transition’da null veya bu Load’ın ID’si kabul edilir; başka Load ID’si `409 SHIPMENT_ALREADY_ASSIGNED`.
- Shipment mode’u yoktur; yalnız **Load.mode ↔ Carrier.supportedModes** karşılaştırılır.
- Stop dizisinin gönderim sırası korunur; execution sırası `sequence` ile belirlenir. Sequence benzersiz ve kesintisiz olur. Yeni location master doğrulaması, boş location string yasağı veya tarih aralığı şartı eklenmez.

**Önemli source-profile sınırı:** SS’de `shipmentId/status/carrierId/loadId` alanlarının presence’ı zorunlu değil. Bu nedenle eksik alanı null/uygun kabul etmeyi önermiyorum.

Önerilen tüketim profili, bu dört alanın açıkça bulunmasını gerektirir. Schema-valid fakat karar için yetersiz cevap →`503 REFERENCE_STATE_UNAVAILABLE`; **“upstream schema ihlali” denmez**. Wrong type/enum veya istenenden farklı shipmentId →502. Bu profil ve producer/mock uyumluluğu DEV önkoşuludur; shared Shipment schema’sı bu WP’de değiştirilmez.

Acceptance: aynı fixture’da `carrierId:null` geçer; alanın kaldırılması503 üretir; hiçbirinde eksik bilgi uydurulmaz. İkinci açık seçenek, producer bu profili sağlayamıyorsa ayrı bir versioned Shipment reference-profile değişikliğidir; **eksik alanı null sayma seçeneği önerilmiyor**.

HTTP okumaları aynı zaman noktasını veya commit anındaki remote durumu garanti etmez. Her okumanın zamanı ve kullanılan referans alanları saklanır. Yerel transaction yeniden başlatılırsa henüz commit edilmemiş fresh komut referansları yeniden okur; unknown-commit recovery önce receipt’i arar.

Completed yalnız **Load operasyonunun tamamlanmasıdır**; Shipment Delivered/POD kanıtı değildir. Completed/Cancelled için remote okuma yapılmaması bu nedenle ayrıca onay kapsamındadır.

**D185-03 — yarışların sonucu**

Constraint kimliği `(TenantId, LegalEntityId, ShipmentId)`; sahibi `LoadId`. Her Load bütün Shipment assignment’larını **tek transaction’da**, tamamı veya hiçbiri olarak alır.

| Yarış / durum | Önerilen acceptance sonucu |
|---|---|
| Farklı key’lerle iki create, ortak Shipment | Unique constraint sonucunda yalnız bir Load commit; kaybeden409 `SHIPMENT_ALREADY_ASSIGNED`; kısmi assignment/event yok |
| Cancelled ile yeni create | Create cancel commit’inden önce değerlendirilirse409 olabilir; cancel commit’inden sonra yeniden denendiğinde kazanabilir |
| Eski cancel ile yeni assignment | Release işlemi scope+ShipmentId+**eski LoadId** ile koşullu; yeni sahibin kaydı silinmez |
| Aynı Load’da farklı transition’lar | Yerel Version/current-state kontrolüyle geçerli seri sıra; retry sonrası artık geçersiz ok422 `INVALID_LOAD_TRANSITION` |
| Completed veya soft-deleted Load | Assignment korunur; yeni create409 |
| Eski create receipt’inin Cancelled sonrasında replay’i | Orijinal Draft response döner; assignment yeniden alınmaz |

Frozen lifecycle aynen korunur: Draft→Planned/Cancelled; Planned→Tendered/Cancelled; Tendered→Accepted/Cancelled; Accepted→Dispatched/Cancelled; Dispatched→Completed. Yeni key ile aynı-state dahil diğer bütün oklar422.

**D185-04 — auth/header/error matrisi**

Önerilen güvenlik profili: tek geçerli `tenant_id`, `legal_entity_id`, `sub`; tenant/LE scope’u validated JWT’den gelir. `X-Tenant-Id` ve `X-Legal-Entity-Id` bununla eşleşen doğrulama header’larıdır, scope seçemez. Exact izinler:

- Q: `supplychain.loads.read`
- C: `supplychain.loads.create`
- T: `supplychain.loads.transition`

Bu exact claim/header profili **Loads için öneridir**; Carrier onayının devri değildir. Alias fallback veya platform bypass önerilmiyor.

İlk başarısız aşama kazanır:

1. Authentication.
2. Trusted tenant → LE → actor → operation permission.
3. Header syntax: correlation → tenant → LE → POST idempotency key.
4. Tenant/header eşitliği → LE/header eşitliği.
5. Path → query → content type → JSON/schema.
6. Scoped committed receipt: root → fingerprint → replay.
7. Fresh transition target → root → lifecycle.
8. Create business yapısı → Carrier → Shipment’lar gönderim sırasıyla → assignment/atomik commit.

Unknown query parametrelerine genel ret eklenmez; scope override girişimi reddedilir. Body scope alanları zaten `additionalProperties:false` ihlalidir.

| Koşul | Operation | HTTP / exact code | Test oracle |
|---|---|---|---|
| Eksik/invalid/ambiguous Bearer | Q/C/T | 401 `INVALID_REQUEST` | Invalid token + bozuk correlation →401 |
| Kullanılamayan/duplicate trusted claim veya eksik izin | Q/C/T | 403 `INVALID_REQUEST` | İzin yok + bozuk key →403 |
| Eksik/invalid/duplicate required header; path/query/JSON/schema hatası | Q/C/T, uygulanabildiği yerde | 400 `INVALID_REQUEST` | İki bozuk header’da yukarıdaki sıra kazanır |
| Scope header/claim uyuşmazlığı | Q/C/T | 404 `LOAD_NOT_FOUND` | DB/receipt sorgulanmaz |
| Unsupported request content type | C/T | 415 `INVALID_REQUEST` | `text/plain` →415; receipt yok |
| Missing/cross-scope/soft-deleted fresh Load | T | 404 `LOAD_NOT_FOUND` | Foreign ID ile existence sızmaz |
| Kullanılabilir scoped Carrier bulunamadı | C/T referans kapısı | 404 `CARRIER_NOT_FOUND` | Active listesinde yok →404 |
| Shipment reference404 | C/T referans kapısı | 404 `SHIPMENT_NOT_FOUND` | Yerel write yok |
| Different-root mutation/replay | C receipt / T | 409 `CORRELATION_ROOT_MISMATCH` | Mevcut root değişmez |
| Aynı scoped key, farklı valid payload | C/T | 409 `IDEMPOTENCY_KEY_REUSED` | Root da farklıysa root hatası öncelikli |
| Assignment başka Load’da | C/T | 409 `SHIPMENT_ALREADY_ASSIGNED` | Transaction bütünü rollback |
| Uygun olmayan Shipment status | C/T referans kapısı | 422 `SHIPMENT_NOT_ELIGIBLE` | Draft/Planned geçer; diğer altı durum reddedilir |
| Carrier uyuşmazlığı / desteklenmeyen mode | C/T referans kapısı | 422 `SHIPMENT_CARRIER_MISMATCH` / `CARRIER_MODE_UNSUPPORTED` | İki kural ayrı fixture |
| Duplicate Shipment / geçersiz stop düzeni | C | 422 `DUPLICATE_SHIPMENT` / `INVALID_LOAD_STOPS` | Repeated UUID değeri / eksik Pickup reddedilir |
| Geçersiz lifecycle | T | 422 `INVALID_LOAD_TRANSITION` | Bütün source×target çiftleri |
| Malformed/çelişkili dependency response | C/T referans kapısı | 502 `DEPENDENCY_RESPONSE_INVALID` | Yanlış ID/type veya duplicate Carrier |
| Schema-valid fakat yetersiz reference | C/T referans kapısı | 503 `REFERENCE_STATE_UNAVAILABLE` | Optional karar alanı missing |
| Dependency timeout/network/5xx veya upstream401/403 | C/T referans kapısı | 503 `DEPENDENCY_UNAVAILABLE` | İç servis yetki hatası caller’ın401’i yapılmaz |
| Persistence kesintisi, safe retry tükenmesi veya unresolved commit | Q/C/T | 503 `PERSISTENCE_UNAVAILABLE` | Unknown commit için “rollback oldu” iddiası yok |
| Beklenmeyen application hatası | Q/C/T | 500 `INTERNAL_ERROR` | Sanitized response; secret/stack yok |

Bütün application hata yanıtları ER biçimindedir:

```json
{
  "error": {
    "code": "CORRELATION_ROOT_MISMATCH",
    "message": "Request correlation does not match the load root.",
    "correlationId": "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa"
  },
  "contractVersion": "v1"
}
```

`message` açıklayıcı metindir; client branching `code` üzerinden yapılır. `details` bu bounded slice’ta üretilmez; başka tenant, root, token, key veya payload açıklanmaz. Başka envelope eklenmez.

Correlation davranışı:

- Tek schema-valid inbound UUID →`C_req`; nil UUID geçerli, UUID değeri karşılaştırılır.
- Missing/invalid/ambiguous correlation →request başına bir fallback UUID. Bu yalnız rejection trace’idir; business root olmaz.
- Her application response header’ı `X-Correlation-Id=C_req`; C_req yoksa fallback. Error body correlation aynı değerdir.
- 401 ayrıca `WWW-Authenticate: Bearer` taşır.
- Başarı body’sine correlation alanı eklenmez.
- Transport/router/startup seviyesinde operation’a ulaşmayan retler bu JSON garantisinin dışında kalır.
- Idempotency key parser sonrası exact string, uzunluk1–128; trim/case-fold/nonblank kısıtı yok. Tek string içindeki virgül geçerli; birden fazla field value reddedilir.

**Replay matrisi**

Receipt identity: `(TenantId, LegalEntityId, operationId, targetId-or-create, exact-key)`. Actor identity’ye dahil edilmez; replay yapan aktör yeniden yetkilendirilir, original audit actor değişmez.

| İstek | Önerilen sonuç | Body / header / kalıcı etki |
|---|---|---|
| Aynı key + payload + root | Create201 veya transition200 | Orijinal başarı snapshot’ı; yalnız `idempotentReplay:true`; header current=root; yeni audit/event yok |
| Aynı key + payload + farklı correlation | 409 `CORRELATION_ROOT_MISMATCH` | Error body/header **current** correlation; original root açıklanmaz/değişmez |
| Aynı key + farklı valid payload + aynı root | 409 `IDEMPOTENCY_KEY_REUSED` | Current correlation; mutation yok |
| Aynı key + invalid payload | 400 | Receipt karşılaştırmasından önce schema reddi |
| Eşzamanlı aynı istek | Tek commit; diğeri receipt replay | İkinci business işlem/event yok; outcome çözülemiyorsa503 ve aynı-key retry |
| Commit sonrası HTTP yanıtı kayıp | Aynı-key retry orijinal başarıyı getirir | Yeni Load/number/event üretilmez |
| Sonraki lifecycle değişiminden sonra eski receipt | Orijinal tarihsel response | Güncel state’i geri almaz; referans yeniden okunmaz |

Fingerprint: object property sırası önemsiz; UUID değerleri ve date-time instant’ları normalize; array sırası ve diğer string içerikleri korunur. Optional `note` omission/null eşdeğer, boş string farklıdır. Correlation fingerprint’e dahil edilmez; ayrı root kontrolüdür. Failed request için receipt/başarı key tüketimi yok; başarılı receipt’lere TTL yok.

HC’nin event correlation eşitliği ve EV’nin immutable root şartı nedeniyle **farklı correlation’lı fresh transition da reddedilir**. Alternatif cross-root kabul, bu iki normatif açıklamanın ayrıca değiştirilmesini gerektirir; bu paket onu önermiyor.

**D185-05 — number, transaction, audit/outbox**

- `LoadId`: server-generated UUID. `LoadNumber = "LOAD-" + LoadId.ToString("N").ToUpperInvariant()`.
- `(TenantId, LegalEntityId, LoadNumber)` unique; soft-delete numarayı serbest bırakmaz. Sıralı/gapless/yıllık sayaç garantisi yok.
- Gerçek UUID/number collision’da bütün işlem yeni ID ile en fazla üç aday dener; tükenirse503 `PERSISTENCE_UNAVAILABLE`. Unknown commit’te yeni ID üretmeden receipt recovery yapılır.
- Collections: `loads`, `load_assignments`, `loads_receipts`, `loads_audit`, `loads_outbox`. Hepsi scoped; replica-set transaction zorunlu. Standalone/in-memory fallback yok.
- Fresh success atomik olarak aggregate, assignment etkisi, receipt, audit ve bir lifecycle outbox kaydı yazar.
- Create event: `LoadCreated`, server UTC `occurredAt`, `causationId:null`.
- Transition event: frozen target event’i; `occurredAt` command’dan, aynı instant korunarak; `causationId` önceki committed Load eventId.
- Tüm event’lerde root, ilk create’in correlation’ıdır. Audit ayrıca actor, önce/sonra state, server commit zamanı, internal Version ve kullanılan reference snapshot’ını taşır.
- Geçmiş/gelecek `occurredAt` için yeni business yasağı konmaz. İşlem sırası server commit/Version ile belirlenir; event zamanı monotonluk garantisi değildir.
- Outbox **Pending** kalır; canlı publisher, delivery acknowledgement veya remote exactly-once iddiası yok. Restart sonrası pending kayıt korunur; replay yeni kayıt üretmez.

Acceptance: her write sınırına fault injection →ya tüm kayıtlar ya hiçbiri; commit sonrası kayıp response →tek receipt/audit/event; restart →aynı number/root/result; source Shipment/Carrier ve stock verisi değişmez.

**Gerekli contract amendment ve yayın kapsamı**

Önerilen yayın adayı: **SHIPMENT-BUNDLE info.version 1.2.0 + `loads-semantics-v1.2.0.md`**, wire `contractVersion:v1` korunarak. Bu sürüm adı onaylanmış değildir.

Exact değişiklik kapsamı:

1. Yalnız Q/C/T’ye Loads normative annex bağı ve yukarıdaki kararlar.
2. Q: mevcut200 yanına **400/401/403/404/500/503**.
3. C: mevcut201/404/409/422 yanına **400/401/403/415/500/502/503**.
4. T: mevcut200/404/422 yanına **400/401/403/409/415/500/502/503**. **Transition409 bugün yoktur; yayınlanmadan uygulanamaz.**
5. Loads’a özgü response/error/header component’leri ve operation’a uygun örnekler; mevcut ER reuse.
6. Bütün Loads application response’larında correlation header;401’de Bearer challenge.
7. Replay, reference-consumption profile, business422/409 koşulları ve event-root açıklaması.
8. Shared schemas, Carrier annex/operasyonları ve Shipment/Return/Claim operasyonları değiştirilmez. Yeni endpoint eklenmez.

**Uyumluluk kapısı:** Duplicate ID, stop düzeni ve uygunluk kuralları bugün schema-valid girdileri business seviyesinde reddedecektir. Bu yüzden “yalnız additive, risksiz minor” denemez.

Önerilen seçenek, henüz draft Loads tüketicileri için **1.2.0 adayını exact compatibility incelemesiyle** yayımlamaktır. Mevcut bir tüketicinin bu girdileri kabul edilmiş davranış olarak kullandığı saptanırsa1.2.0 yayını durur; **2.0.0 breaking-change/migration kapsamı** gerekir. Consumer inventory ve kanıt olmadan minor uygunluğu varsayılmaz.

Yayın acceptance’i: schema/ref/example kontrolleri; yukarıdaki pozitif/negatif fixture’lar; mevcut Carrier/Shipment consumer testleri; non-Loads operation/schema anlamlarının değişmediğini gösteren structural diff ve hash envanteri.

**Phase 1.5 ve Program.cs için exact onay kapsamı**

Phase 1.5 önerisi, D185-01…05’in tamamını ve pack §25’teki **tek tek sayılmış dosya allowlist’ini** kapsar; parent-directory wildcard yetkisi vermez. Özellikle şu seçimler açık onaylanmalıdır:

- Scoped module-specific repository ve atomik çoklu-document işlem istisnası.
- Assignment tutma/bırakma, unique index ve Version/current-state arbitration.
- Reference snapshot modeli ve **remote commit-time atomikliği bulunmadığının kabulü**.
- Receipt/fingerprint/root/retention, LoadNumber, audit/event ve pending outbox.
- Exact permission/claim/header matrisi.
- Phase 1.5 required-parity satırının ancak amendment yayını ve consumer uptake sonrası kapanması; UI kontrollerinin N/A kalması.

Tek shared-source istisnası önerisi:
[services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs](/Users/natig/Projects/ERP-vNext-recovery/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs).

İzin verilecek değişiklik yalnız:

- Loads scoped context/reference HTTP client ve owned persistence registration;
- publisher çalıştırmayan yerel outbox bileşeni;
- exact Loads route family için auth/error/context adapter;
- broad Shipment middleware’den yalnız bu onaylı route’ların ayrılması.

Carrier branch, Shipment worker/store, health/JWT/CORS, global error handling ve ortak `IEventOutboxStore` kaydı korunur. `loadsXYZ` gibi prefix eşleşmeleri Loads sayılmaz. Ayrı single-writer grant gerekir; yeni project reference/config/gateway/permission seed ihtiyacı bu iznin içinde değildir.

**Teslim ve kalan kapılar**

Agent verdict: **Karar önerisi tamamlandı; owner onayı ve yayın bekliyor.** D185-01…05’in hiçbiri bu cevapla `RESOLVED/approved` yapılmadı.

E1 static inceleme yapıldı; build/runtime/E4/E5 çalıştırılmadı. Yeni migration veya rollback işlemi yok. Changed files: **yok**. CT’ye harici dispatch yapılmadı.

İncelenen SHA-256 değerleri:

| Dosya | SHA-256 |
|---|---|
| SHIPMENT-BUNDLE | `ba9d85f086dd2bfc150c1818843fa22c5b00b0dba1948a57a3672e2529d9880f` |
| MOD-0185 pack | `e99dc412e065f45966e8ea34be2176756f6278e01e7ae63e07d42929a6f182b4` |
| Program.cs | `17177020c722d47c7dd29777315338212cb19c0825b141ed0053431ca3af93c0` |

DEV başlangıcı için bu karar setinin onayı, exact amendment yayını/uptake, reference-profile uyumluluğu, merkezi bağımlılık kabulü, Phase 1.5 ve Program.cs writer yetkisi, ardından ayrı pack promotion ve versioned DEV promptu gerekir. **Bu teslim bunların yerine geçmez.**
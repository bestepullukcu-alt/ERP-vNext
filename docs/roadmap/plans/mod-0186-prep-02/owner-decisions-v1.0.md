# MVP6-MOD0186-PREP-02 — tek owner karar paketi v1.0

2026-09-19 · ÖNERİ / OWNER REVIEW · pack draft · DEV/VER HELD.
Bu belge owner onayı, publication veya dispatch değildir. D186-01…06 tek seferde incelenecek
Returns karar setidir. Carrier/Loads kararları Returns'a otomatik taşınmaz.

## 1. Mevcut yetki — yeniden onaya açılmayanlar

- AGENTS.md, domain-config ve DCP-009: MOD0186 Return/RMA SoR; Tenant+LE izolasyonu,
  stock SoR=MOD0173; source DB/iç tip paylaşımı yok; contract-first, backend-only.
- DCP-002: Reverse Logistics mevcut MOD0186 kimliği; yeni MOD/FU/reservation yok; fresh exit0.
- MOD0183 bounded CT kabulü: docs/records/audits/2026-09/mvp6-mod0183-ct-review-01-2026-09-16.md.
- MOD0184 bounded CT kabulü: docs/records/audits/2026-09/mvp6-mod0184-ct-accept-02-2026-09-18/README.md.
- SHIPMENT-BUNDLE2.0.0 yayın/onay/uptake: docs/records/audits/2026-09/mvp6-loads-r2-publication-2026-09-18/README.md.
  YAML SHA25693c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571, wire v1.
  Carrier/Loads onayları ve eski17mühürlü kayıtlar yeniden açılmaz. Loads DEV/VER kayıtları CT kabulü değildir;
  MOD0185 CT kabulü bu çalışmada verilmiş sayılmaz. Tur sırasında gelen
  docs/records/audits/2026-09/mvp6-mod0185-ct-review-01/README.md verdict **REWORK**: A04 timeout/refusal
  persisted-count kanıtı eksik. Dosya salt okunmuştur; diğer lane yazımıdır. Returns runtime sequencing CT'nin ayrı kapısıdır.
- WP-MVP6 intake (docs/analysis/workpackages/WP-MVP6-logistics.md) runtime promptu değildir.
  Kayıt taramasında D186-01…06 için imzalı Returns owner kararı bulunmadı; aşağıdakiler açık öneridir.

## 2. Frozen pointer anahtarı

S = docs/analysis/contracts/shipment-bundle.openapi.yaml (info.version2.0.0; contractVersionv1).
I = docs/analysis/contracts/inventory-bundle.openapi.yaml (info.version1.0.0; /api/inventory).
W = docs/analysis/contracts/warehouse-outbound.openapi.yaml (info.version1.0.0; /api/warehouse).

| Ref | Exact JSON pointer / gerçek |
|---|---|
| Q | S#/paths/~1returns/get: queryReturns; yalnız200; query shipmentId/status |
| C | S#/paths/~1returns/post: createReturn;201/404/409/422; ilk state Requested |
| T | S#/paths/~1returns~1{returnId}~1transition/post: transitionReturn;200/404/422;409/default YOK |
| CR/TR/RL | S#/components/schemas/CreateReturnCommand, TransitionReturnCommand, ReturnLine |
| RS/RR | S#/components/schemas/ReturnSummary, ReturnResponse; summary alanları required değil; RR allOf + required replay/version |
| SD/SL | S#/components/schemas/ShipmentDetail, ShipmentLine; SL required lineNumber,itemId,skuId,quantity,uomId |
| SS | S#/components/schemas/ShipmentSummary; shipmentId/status optional presence |
| D | S#/components/schemas/Decimal: string ^-?\d+(\.\d+)?$; precision/scale sınırı yok |
| HC/HI/E | S#/components/parameters/CorrelationId, IdempotencyKey; S#/components/schemas/Error |
| EV/EP | S#/components/schemas/LifecycleEventEnvelope, ReturnEventPayload |
| IT | I#/paths/~1transactions/get; filters itemId,lotId,locationId,from,to,page,pageSize; transactionId lookup filtresi yok |
| TI | I#/components/schemas/TransactionItem; required list yok; transactionId/sourceDocumentId/sourceLineId vb. |
| WO | W#/paths/~1outbound-shipments/get ve ~1outbound-shipments~1{outboundId}/get; inbound receipt değil |

CR evidenceReferenceIds optional array (null değil), boş/duplicate/string-empty elemanlara schema yasağı yok.
TR inventoryTransactionReferenceId ve dispositionCode optional string|null; Received için frozen required evidence yok.
ReturnLine.lineNumber karşılığı shipmentLineNumber STRING; GUID'e çevrilmez. reasonCode empty schema-valid.
Bu gerçekler ile aşağıdaki yeni business retleri ayrı tutulur.

## 3. Tek önerilen karar seti

| ID / onay sahibi | Exact öneri + gerekçe | Alternatif ve etkisi | Acceptance / pointer |
|---|---|---|---|
| D186-01 business+contract | Eligible Shipment=Delivered veya Closed; cap=ShipmentLine.quantity, teslim edilen miktar iddiası değil sevk-satırı üst sınırı. Create her defasında scoped Shipment GET okur. Selected line exact ordinal eşleşir, command duplicate line reddedilir, UoM exact ordinal eşit; conversion yok; quantity>0. | Dispatched/InTransit dönüşlerini kapsamak daha geniş süreç kararıdır, önerilmiyor. Delivered-quantity cap istenirse frozen ayrı teslim miktarı sunmuyor; producer amendment gerekir. | Delivered10EA için4EA geçer; InTransit422;0/-1→422;10EA→geçer;10.01→422;EA/BOX→422; duplicate line422. SD/SL/SS/D/CR/RL. |
| D186-02 business+data | Sayılan states=Requested,Authorized,InTransit,Received,Dispositioned,Closed. Rejected/Cancelled bırakır. Closed ve soft-delete ASLA bırakmaz. Her satırın entitlement kaydı create ile atomik artırılır; yalnız gerçek counted→release transition bir kere düşürür. | Requested sayılmazsa authorize'da yeniden yarış/ret gerekir; Closed bırakılırsa aynı fiziksel miktar tekrar iade edilebilir. İkisi önerilmiyor. | Cap10:6+6 yarışında bir201/diğer422;4+6 ikisi seri commit ile201; multi-line all-or-none; reject/cancel+retry double release yapmaz. T/ReturnStatus/SL. |
| D186-03 Warehouse+business+contract | Bounded Received=aynı scope'ta .receive yetkili actor'un signed komut beyanı; audit'te actor,command occurredAt,server commit time,root ve kanıt türü manual-assertion saklanır. Fiziksel depo kabulü/Inventory posting doğrulanmış sayılmaz. Create evidenceReferenceIds aynen opaque history; zorunlu/verified kabul edilmez. | Warehouse doğrulanmış receipt zorunluysa mevcut WO yetmez: yeni sahibi tarafından published receiving evidence contract gerekir; Received ve sonrası BLOCKED olur. Bu alternatifte başarılı mock, üretim kanıtı olamaz. | Authorized→Received422; InTransit→Received yetki varsa200/manual audit; yetkisiz403; outbound ID/evidence string tek başına yetki/receipt sağlamaz. T/TR/WO. |
| D186-04 Inventory+contract | Bounded DEV'de inventoryTransactionReferenceId OPTIONAL OPAQUE, UNVERIFIED reconciliation text; absence/null/empty aynen kabul ve audit. Inventory HTTP çağrısı YOK; stock write kesin yasak. Dispositioned hedefinde dispositionCode string length>=1 gerekir (trim yok); başka hedefte supplied value audit edilir, business disposition değiştirmez. | Verified movement zorunlu seçilirse IT üzerinden pagination completeness, scoped transactionId binding, source/line/item/UoM ve reversal politikası ayrı owner contract'ı olmadan kanıtlanamaz. GET-by-ID icat edilmez. | Null/empty inventory ref ile Received geçer; foreign-looking string doğrulanmış etiketi ALMAZ; outbound HTTP sayısı Inventory/Warehouse için0. Dispositioned null/empty→422;" " length1 kabul, catalog doğrulaması yok. IT/TI/TR/EP. |
| D186-05 security+contract | §4 exact Returns permission/header/error/root/replay profili. Mutation root=create correlation; farklı-root fresh transition/replay409. Authz before receipt; original result201/200 replay. | Carrier current-root kabulü EV/HC ile uyumlu değildir; bunu seçmek shared semantics değişikliğidir, önerilmiyor. Güvenlik default'u kopyalanmaz; §4 Returns onayı gerekir. | Her operation/action grant, parser401/context403, missing header400, crossscope404, same-root replay ve409 corpus. Q/C/T/HC/HI/E/EV. |
| D186-06 data+CT | L3 replica-set transaction: Return+entitlement+receipt+audit+pending outbox. Arbitrary-precision coefficient/scale; raw quantity korunur; numeric compare exact. RMA- + server ReturnId uppercase N; scoped unique no TTL/reuse. | .NET decimal/Decimal128 sınırına sessiz daraltma veya standalone fallback önerilmiyor. Sayısal sınır istenirse versioned compatibility kararı gerekir. | 30+digit ve scale>28 exact;10.0==10.00 numeric; injected fault her write'da full rollback; lost-response tek receipt/event; restart exact raw quantity. D/EV/EP/RR. |

D186-01/02 exact source policy: scoped GET /api/shipment-bundle/shipments/{shipmentId} schema-first;
required omission/type/enum→502; optional shipmentId/status presence yok→503 REFERENCE_STATE_UNAVAILABLE;
wrong requested identity/duplicate source lineNumber/nonpositive source quantity→502.
Missing selected line404 SHIPMENT_LINE_NOT_FOUND; source404 SHIPMENT_NOT_FOUND. İlk başarılı cap kaydı
(tenant,LE,shipmentId,ordinal lineNumber) için source quantity+UoM+itemId+skuId snapshot'ı sabitlenir.
Daha sonraki create aynı source numeric quantity/identity/UoM göstermiyorsa409 RETURN_SOURCE_CHANGED;
azalmış sayacı otomatik sıfırlama/kapasite artırma yok, reconciliation ayrı WP. Raw quantity gösterim farkı
numeric eşitse drift değildir. Release sonrası0 olsa bile source identity snapshot'ı silinmez.
Fresh transition'lar source'u yeniden okumaz: frozen create snapshot ve yerel lifecycle kullanılır.
Source snapshot GET ile yerel transaction arasında remote source değişebilir; distributed serializability
vaadi yok. Retry fresh local conflict ise source tekrar okunur; unknown commit önce receipt recovery.
Concurrent creates farklı source snapshot görürse unique entitlement+CAS kazanan snapshot'ı sabitler,
diğeri retry'da drift409 veya cap422 alır. Inventory balance,availability,reservation bu hesabın girdisi DEĞİL.

Entitlement scalar'ın key'inde UoM yok: aynı source line'ı başka UoM ile ikinci kapasiteye çeviremezsin.
UoM/identity kayıtta karşılaştırılır. Her counted Return line bir kez katkı verir; multi-line transaction
bütün ilgili entitlement kayıtlarını koşullu günceller. Scope dışı veya deleted aggregate normal GET/query'de
görünmez, fakat entitlement katkısı audit amaçlı korunur. Delete/reopen endpoint yok.

## 4. D186-05 önerilen exact security / errors / replay

Q read=supplychain.returns.read; C create=supplychain.returns.create.
T tüm çağrılarda supplychain.returns.transition base grant + schema-valid hedefe göre:
Authorized/Rejected→supplychain.returns.authorize; InTransit→supplychain.returns.transit;
Cancelled→supplychain.returns.cancel; Received→supplychain.returns.receive;
Dispositioned→supplychain.returns.disposition; Closed→supplychain.returns.close.
Requested hedefi base grant sonrası lifecycle422 (yeni ok yok). Yeni .transition/.transit/.cancel/.close
izinleri yalnız öneri; shared permission catalog seed ayrı integration/security WP.
Tenant actor; platform bypass yok. Internal policy HasPermission standardı, route-specific target check.

Precedence: (1) authentication401; (2) trusted tenant_id→legal_entity_id→sub→base operation grant403;
(3) header correlation→tenant→LE→POST key syntax400; (4) tenant/LE header-claim mismatch404;
(5) path→query→content type→body schema; (6) parsed target action grant403;
(7) scoped committed receipt root→payload→replay; (8) fresh T target404→root409→lifecycle422→target business;
(9) fresh C duplicate/positive checks→Shipment reference/schema→eligibility→line/UoM→cap;
(10) atomik persist/recovery. İlk başarısız aşama kazanır. Target permission gövdeden ancak schema sonrası
seçilir; malformed body ile permission tahmini yapılmaz. All inputs auth before DB.

JWT parser/signature/expiry/issuer/audience rejection401; authentication başarılı ama unusable/duplicate
trusted claim403. Her duplicate sub403 diye parser üstü garanti yok; signed payload occurrence korunur,
last-wins bypass yok. Claim isimleri exact-case; alias yok. UUID ASCII8-4-4-4-12 hex, casing serbest,
trim/braces/URN/32hex yok; nil correlation/body/path/header syntactically valid; trusted claims non-nil.
Header isimleri case-insensitive; same-value duplicates dahil tek field-value şartı. Scope JWT'den,
header yalnız doğrulama. Query once-decoded ASCII-case-insensitive exact keys tenantId,tenant_id,
X-Tenant-Id,legalEntityId,legal_entity_id,X-Legal-Entity-Id →400; ordinary unknown ignored, scope binding yok.
Key parsed exact string1..128 Unicode codepoint; whitespace-only valid if parsed length>=1; no trim/casefold;
single-value comma valid, duplicate value ret. Empty reasonCode/evidence strings korunur.

C_req geçerli current UUID; yoksa tek generated rejection trace. Tüm application response header
X-Correlation-Id=C_req/fallback; error.error.correlationId aynı; fallback business root değil.
401 WWW-Authenticate:Bearer. Success RR/List plain frozen shape; error E plain shape, no Response<T>/
ProblemDetails wrapper. details optional fakat bu scope üretmez. Router/transport/startup retleri kapsam dışı.
Message sanitized English, code client branching otoritesi. Nil sırf sıfır diye reddedilmez.

| Operation / koşul | HTTP code / error.code | Kabul testi |
|---|---|---|
| Q/C/T auth |401 INVALID_REQUEST|invalid JWT+bad correlation→401/fallback|
| Q/C/T context/base grant; T action grant |403 INVALID_REQUEST|valid auth duplicate sub→403; .transition var .receive yok→403|
| Q/C/T headers/path/query/schema |400 INVALID_REQUEST|nil correlation geçer, duplicate ret; extra body scope ret|
| C/T content type |415 INVALID_REQUEST|text/plain ret; receipt yok|
| Q/C/T scope mismatch; T target missing/foreign/deleted |404 RETURN_NOT_FOUND|no existence disclosure|
| C source/line missing |404 SHIPMENT_NOT_FOUND / SHIPMENT_LINE_NOT_FOUND|no local write|
| C/T receipt changed root; T fresh wrong root |409 CORRELATION_ROOT_MISMATCH|body/header current; original root undisclosed|
| C/T valid different payload under same key |409 IDEMPOTENCY_KEY_REUSED|root conflict first|
| C source identity/cap drift |409 RETURN_SOURCE_CHANGED|no automatic cap rewrite|
| C duplicate lines / <=0 / UoM mismatch / ineligible |422 DUPLICATE_RETURN_LINE / INVALID_RETURN_QUANTITY / RETURN_UOM_MISMATCH / SHIPMENT_NOT_RETURNABLE|each independent fixture|
| C cap exceeded |422 RETURN_QUANTITY_EXCEEDED|including concurrent loser; no partial lines|
| T invalid arrow / required disposition missing |422 INVALID_RETURN_TRANSITION / DISPOSITION_REQUIRED|same-state new-key422; null/empty disposition422|
| C malformed/contradictory reference |502 DEPENDENCY_RESPONSE_INVALID|required omission502|
| C schema-valid insufficient optional reference |503 REFERENCE_STATE_UNAVAILABLE|missing SS.status503|
| C upstream401/403/5xx/network/timeout |503 DEPENDENCY_UNAVAILABLE|not caller401; no false404|
| Q/C/T persistence unavailable/unknown commit/exhausted retry |503 PERSISTENCE_UNAVAILABLE|same key recovery, not asserted rollback|
| Q/C/T unexpected |500 INTERNAL_ERROR|no stack/token/payload leak|

Receipt identity=(tenant,LE,operationId,targetId-or-create,exact key), actor not part of identity;
current actor must have current base/action grant. Success receipt retained without TTL; no failed receipt.
Fingerprint object order irrelevant; UUID normalize value, datetime instant normalize; quantity normalize
numeric exact (1.0=1.00), original raw value retained in first result/audit; all arrays order significant;
line/evidence strings ordinal no trim. Optional nullable TR fields omission=null equivalent; empty distinct.
Create evidenceReferenceIds omitted and [] equivalent, null schema400. Correlation excluded from fingerprint,
separate root check. Auth/schema then replay BEFORE current lifecycle, source lookup, cap checks.

| Retry | Result |
|---|---|
| same key+payload+root | original201(C)/200(T), original snapshot + idempotentReplay=true; response root=current=original |
| same key+payload+different root |409 CORRELATION_ROOT_MISMATCH; current response/error trace, immutable original audit/root |
| same key+different valid payload |409 IDEMPOTENCY_KEY_REUSED, unless root mismatch wins |
| concurrent same key / committed response loss | one commit; original receipt recovery; no new entitlement/audit/event; unresolved503 |
| old receipt after rejection/closure/soft delete | authorized historical result; no re-debit or release; fresh target absent/deleted404 |

Same-root requirement follows EV immutable first-command root + HC event UUID equality; not inferred from
Carrier. Event scope is Return chain (first createReturn), not mandatory equality with source Shipment root:
source detail offers no authoritative source event root. Cross-module causation is separate integration GAP.

## 5. D186-06 transaction / event details

RmaNumber="RMA-"+ReturnId uppercase32hex; tenant+LE+number unique, no gapless guarantee, deleted retained.
Collision tries at most3 fresh UUID candidates for known-uncommitted work; exhausted503. Unknown commit
never blindly retries create with new ID. Readiness requires replica set/unique indexes; no weak fallback.
Existing EntityBase Id/TenantId/IsDeleted/DeletedAt/CreatedAt/UpdatedAt/Version reused; module LE and actor fields.
Quantities persisted raw strings plus exact normalized coefficient/scale as needed; no binary float,
fixed Decimal128 saturation/rounding, arbitrary scale cap or base-UoM conversion. Source/request pattern
validation precedes parsing; resource-limit rejection belongs to transport, not invented business precision.
Transaction collections: returns,return_entitlements,returns_receipts,returns_audit,returns_outbox.
Indexes: scoped RMA unique; scoped receipt identity unique; scoped source-line entitlement unique;
scoped eventId unique; tenant+LE+IsDeleted+shipmentId/status query indexes. No collection per tenant.
Each success exactly one audit + event, all transaction writes rollback on precommit fault. Audit raw
command, caller, receipt link, state before/after, server commit time, source snapshot where applicable.
Audit DB history may store confidential business input; logs must not emit raw inputs/token/connection string.

EV aggregateType=Return, aggregateId=ReturnId; EP required returnId,rmaNumber,shipmentId,status.
Create ReturnRequested: occurredAt serverUTC, causationId null. Transition uses command occurredAt
(normalized instant; no invented time bounds), corresponding target event, causationId prior committed
Return eventId. Event sequence follows internal Version, not timestamps. Root unchanged. TR opaque Inventory
reference mirrored into that transition's EP only if supplied, preserving null/empty; no verified flag on wire.
Optional inputs retained in each command audit; actual disposition business value set only at Dispositioned.
Pending outbox persisted/restart-tested, no live bus/publisher/shared store replacement; no E5 claim.

## 6. Exact amendment / remaining contract GAPs

All rows OPEN until owner decision + required versioned publication/uptake; no canonical changes by PREP.

| GAP | Proposed exact publication scope / consumer impact |
|---|---|
| G186-01 business | Returns-only normative annex binds §3/§5 eligibility,cap,decimal,UoM,release,source-snapshot,manual assertion and opaque Inventory. New schema-valid business rejections require explicit compatibility decision; not silent clarification. Existing D185 breaking-release approval grants no exception here. |
| G186-02 errors | Q add400/401/403/404/500/503; C add400/401/403/415/500/502/503; T add400/401/403/**409**/415/500/503. Existing statuses retained, Returns-local examples/responses reuse E; response correlation headers and401 challenge explicit. No default shortcut. |
| G186-03 security/replay | Required validation-only tenant/LE headers, exact target grants/precedence,root/receipt/fingerprint and parser split normatively specified on only Q/C/T. No global Carrier/Loads policy change. |
| G186-04 external verification | Recommended bounded manual Received + opaque Inventory avoids new inbound/transaction endpoint. VERIFIED receiving/movement remains NOT PROVIDED; if owner selects mandatory external proof, DEV scope cannot claim complete Received without separately published owner seam. IT/TI pagination and mapping not guessed. |
| G186-05 source provenance | HTTP source snapshot is not cross-service lock; source Shipment root not exposed. Persist source IDs/local Return root only; live cross-module correlation/reconciliation remains separate. |
| G186-06 release | Propose SHIPMENT-BUNDLE3.0.0 candidate + returns-semantics-v3.0.0.md (name/version only proposal), wirev1; exact compatibility review may select release strategy. Metadata major gives no route negotiation/migration. No endpoint/shared-schema addition. Verify all non-Returns paths/components identical; input/output hashes and consumer uptake required. |
| G186-07 authority | Final canonical hash needs separate approved DocsPathGuard binding;17history seals/Carrier+Loads annexes unchanged. No guard edit in this scope. |

Frozen ReturnResponse's summary fields are not schema-required: implementation proposal emits all four
without widening schema required lists. No empty-field generic NotEmpty validator. Disposition required
business rule and positive quantities belong to amendment description/annex, not stealth schema rewrites.

## 7. Single owner review / readiness

Owner may approve D186-01…06 as this exact set (manual Received, opaque unverified Inventory, strict exact-UoM
cap and §4 security profile), or select D186-03/04 verified-external alternatives which explicitly retain the
receiving/reconciliation contract blocker. No generic "owner decide later" runtime default is left.
This review does not repeat MOD0183/0184 acceptance or Loads design/publication approvals.

After decision: versioned amendment candidate→independent verification→compatibility/consumer disposition→
explicit publication+guard binding→canonical uptake→Phase1.5/shared composition approval→pack promotion→
new versioned DEV dispatch. MOD0185 CT acceptance remains separate and is never inferred here.
Operational data absence cannot be proved from repo source: before runtime rollout, first-deployment
attestation or scoped migration plan required; never invent entitlement/history/root backfill.

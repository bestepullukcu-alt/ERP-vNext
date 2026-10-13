# Owner decision package — Loads authoritative transition root

## Single recommended decision

> MOD-0185 zorunlu transition UI için authoritative root ediniminde mevcut `queryLoads` read yüzeyinin dar, versioned amendment yaklaşımını seçiyorum.
>
> Her `LoadSummary` için `lifecycleCorrelationId`, yalnız persisted `LoadPlan.CorrelationRoot` değerinden üretilsin. Alan optional/nullable UUID olarak adaylaştırılsın; upgraded producer persisted root mevcutsa non-null değeri emit etsin. Missing/null/malformed değer transition için kullanılamaz; UI fail-closed kalır, kullanıcıdan root istemez, GET trace'inden/Shipment ID veya root'larından/cache/DB-audit erişiminden değer türetmez ve backfill yapmaz.
>
> Mevcut `queryLoads` tenant/LE filtresi ve `supplychain.loads.read` izni korunur. Transition ayrıca `supplychain.loads.transition` ile korunmaya devam eder. Mevcut root-before-fingerprint, replay, 404/409/422/503, audit ve Pending-outbox davranışları değişmez.
>
> Bu karar yeni Loads detail/search endpoint'i, detail sayfası veya searchable Carrier/Shipment/location lookup seçimi değildir. Çoklu Shipment root'larını eşitleme, birleştirme, ilkini seçme, farklı kökleri yasaklama veya multi-root event üretme politikası oluşturmaz; bu ayrı owner sınırı açık kalır.
>
> Contract owner yalnız bu alanı, list örneğini ve Loads annex açıklamasını içeren exact versioned candidate hazırlayabilir. Exact bytes/hash, compatibility, consumer consent ve canonical publication ayrıca onaylanacaktır. Producer/UI/runtime/pack/Gateway uygulaması bu kararla başlamaz.

## Decision identity

- Proposed decision ID: `D185-ROOT-ACQ-01`
- Contract owner: SHIPMENT-BUNDLE / Loads release owner
- Producer owner: MOD-0185 Loads backend owner
- Consumer owner: Loads UI owner
- Shared integration owner: single assigned integration owner after publication
- Rejected current-contract workaround: browser/session retention, user entry, Shipment-root derivation, audit/DB access
- Deferred alternatives: new Loads detail/root endpoint; independent detail page; searchable lookups


# MOD0186 DEV prompt v1.0 — HELD / DO NOT DISPATCH

WP: MVP6-MOD0186-DEV-01 (draft identity, not dispatch)
Target: @orchestrator / add-module; future sequential backend/security/testing roles within one writer lane.
Profile: B bounded backend; Risk HIGH; required E2/E3/E4, L3. UI pattern N/A.
Repository: /Users/natig/Projects/ERP-vNext-recovery
Branch: feature/mvp6-logistics
PREP observed HEAD:4a8d4d4b339528a88e6220fb8402e5a2c771136c (fresh dispatch baseline required).
Pack: execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md
CURRENT STATUS draft. Domain supply-chain-execution; service Diten.SupplyChainService;
shell none, golden_reference none, form_field_count0, EntityBase tenant-owned; no UI.

## Authority gate — stop before code

This prompt is HELD. Do not interpret this document as runtime authorization. CT must issue a NEW released
prompt version carrying all of: exact signed D186-01…06 decisions (manual/opaque recommendation or explicit
alternative disposition), Returns normative amendment publication+canonical hashes+consumer uptake,
Phase1.5 approval including specific repository/quantity/entitlement design, separately approved exact
Program.cs writer exception, promoted pack, current dependency acceptance including explicit MOD0185 CT
status, data-owner first-deployment or approved migration disposition, fresh HEAD/dirty/source hash baseline,
DEV evidence destination and independent VER assignment. No placeholders silently default to approval.
Do not reopen already approved upstream identity/Carrier/Loads publication. Do not infer CT acceptance from VER.

## Required context

Read AGENTS, domain-config, pack (especially§§22–29), owner-decisions-v1.0.md and owned-paths-v1.0.md here,
CT SOP§17/18/22/24, released Returns amendment (currently missing), current S/I/W contracts,
applicable agent rules and actual backend Slim naming reference. Frozen current bundle2.0.0 does not yet
carry the proposed Returns behavior. It cannot be substituted with a private runtime default.

## NE / NEDEN / NASIL (only after future release)

Implement exactly queryReturns/createReturn/transitionReturn, scoped Return/RMA lifecycle and all-line
entitlement cap, durable success receipts/audit/pending outbox. No getById/edit/delete endpoint.
Use published source GET /api/shipment-bundle/shipments/{shipmentId}; no DB/internal type reuse.
No runtime Inventory/Warehouse call under recommended opaque/manual design. No stock/shadow-stock,
reservation,posting,financial credit,live ingress,bus publisher,UI,gateway or Supplier master.

Owned paths: exact47files in owned-paths-v1.0.md; no directory wildcard. Program.cs ONLY after separate
release; preserve Shipment/Carrier/Loads pipeline and exact family boundaries. No project/config expansion.
Frozen wire RR/ReturnListResponse/Error; internal Response<T>/CustomBaseController adapted without wrapper.
Existing4pipeline behaviors reused; source scopes and all repository operations tenant+LE; soft-delete
filters for normal queries, deleted contributions still counted. No GlobalEntity.

Implement approved D186 rules, not this proposal if owner changed them. Current proposal is explicit in owner
record§§3–5: Delivered/Closed cap from shipment line; numeric exact positive quantity and ordinal UoM;
counted Requested/Authorized/InTransit/Received/Dispositioned/Closed; releaseRejected/Cancelled; source snapshot
and drift409; authorized manual Received; unverified optional Inventory text; disposition length1 at target;
root/header/error/target grants/fingerprint matrix; no TTL; RMA UUID number; L3 transaction+CAS+unique indexes.
Source HTTP snapshot is not a remote lock. Exact required/null cases and64lifecycle pairs mandatory.

## Acceptance / validation plan

Pack A01–A12 and R01–R11 map to test IDs in evidence. Build service solution; test SupplyChain test project;
architecture project; independently runnable owned HTTP/restart probes. DB-010 isolated test database naming
rules; never connect operational DB or use production credentials. Runtime tests require real signed JWT and
replica-set Mongo; in-memory mocks cannot stand in for E4. Reference mocks consume published exact schemas.
Capture source,binary,process,input hashes and actual sent request bytes,all statuses/body/headers; GET body
empty. Test two tenants×twoLEs,all permissions,target absence/deleted,decimal boundaries,6+6 and4+6 races,
release races,multi-line rollback,source drift,unknown commit/restart,original replay after state change,
pending outbox and no Inventory/Warehouse/stock write. Never log tokens/connection strings/raw payloads.
Existing Shipment/Carrier/Loads regression evidence must be fresh or content-bound with honest limits.
Architecture prior counts are historical; no new waiver or shared guard weakening.

## Failure / output contract

On contract/scope drift stop affected work; provide exact file:line+repro and separate rework request.
Do not fix other lanes. No commit/push/stash; no contract/guard/registry/pack promotion.
SOP§22 report with changed-path diff/hash inventory,AC results,commands/exits,raw request captures,DB/audit/
entitlement/outbox consistency,restart/failure evidence,known gaps and rollback. Failure rollback never drops
business history or stock data. Independent VER starts after frozen complete DEV handoff, CT acceptance separate.

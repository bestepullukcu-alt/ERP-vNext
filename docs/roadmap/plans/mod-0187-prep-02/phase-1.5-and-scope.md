# MOD-0187 Phase 1.5 / exact prospective ownership

**Design prepared; approval BLOCKED.** [Single decision source](owner-decisions.md).
This document supersedes pack §§25–27 as the current PREP-02 proposal only; it grants no runtime ownership today.
The closed list in [prospective-owned-paths.txt](prospective-owned-paths.txt) contains 48 exact future source/test paths,
no directory wildcard. Compared with old §25, it adds ExactClaimAmount.cs, ClaimReferenceSnapshot.cs and three test files
(ClaimAmountTests.cs, ClaimPermissionTests.cs, ClaimOutboxTests.cs). File necessity is reviewed at implementation; unused
files need not be fabricated. Additional files/renames require a versioned scope change.

## Nine mandatory checks

| Check | Concrete proposal and evidence to satisfy it | Status |
|---|---|---|
| 1 Entity fields | Claim inherits existing EntityBase; server tenant/LE/actor, immutable claim identity/references/amount/currency, exact approval text, lifecycle/internal Version; snapshot/evidence/transition audit owned; DTO has no tenant/LE/version additions. D187-01/02/03/06 and persistence boundary tests | Designed; owner approval pending |
| 2 Global names | Exact frozen field/status/event names; no SettlementReference/paidAmount/return-derived aliases; contractVersion v1 distinct from artifact metadata2.0.0 | Static parity inspected; amendment pending |
| 3 Repository | Specific IClaimRepository/ClaimRepository owns atomic four-document unit; tenant+LE on every access, soft-delete predicate on entity/list, separately scoped historical receipts; unique indexes and replica-set rollback/CAS proofs. Explicit REPO-001 exception D187-06 | BLOCKED approval/E4 |
| 4 Entity base | Domain/Common/EntityBase reused read-only; module-owned LegalEntityId/actor/internal CAS; no GlobalEntity or base change. ENTITY versioning exception: immutable transition audit + CAS, not new Draft/Revision statuses | BLOCKED architecture decision |
| 5 CQRS | Listed Commands/Queries and separated handlers/validators; existing application scan/four behaviors; exact contract response adapter without extra Response<T> wire wrapper. Claims context/error cannot borrow Shipment root defaults | Designed; execution HELD |
| 6 Golden reference | Golden Slim backend command/handler/validator naming only; no generic CRUD endpoint copying. shell:none, form0, golden_reference:none | N/A UI, applicable naming documented |
| 7 Compact sections | No Razor/details/forms/DataTable/RESX or frontend scaffold | N/A |
| 8 Required parity | S1 Create/Transition fields and nullability preserved; D187 amount/reference/security business restrictions remain unapproved, exhaustive scenarios in decision set. No blanket NotEmpty/string precision restrictions | BLOCKED owner/contract |
| 9 Platform lookup | No platform catalogue; currency regex only, frozen enums, scoped published Shipment/Carrier reads; evidence opaque. No Supplier/Inventory/Returns reads or master copies | Designed; contract sign-off pending |

## Shared composition: separately authorized single writer only

Protected current file: `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs`.
Future patch limited to Claims using directives; owned persistence extension; scoped context/reference reader client;
local pending outbox worker if approved; Claims-family middleware and family-local invalid-model response adapter.
Predicate must match exactly `/api/shipment-bundle/claims` or its slash-delimited descendants, never `/claimsXYZ`.
Add exclusion of that exact family from the existing Shipment branch only. Preserve existing Carrier and Loads branches,
registrations, reference client, all authentication/JWT settings, CORS/health/global controllers, Shipment outbox worker,
HTTP response policies outside Claims and current route behavior. No shared IEventOutboxStore substitution.

DEV cannot take a Program.cs lease implicitly. CT must assign a single composition writer and review the exact diff
against fresh current bytes. If separated, Claims author hands the ready patch to that writer; no parallel modifications.
No gateway, permission seed registry, Application DI, shared persistence/base/entity/config/csproj/solution edits allowed.
Existing DI scan discovers CQRS. Reuse the registered MongoClient/database, not a new connection/provider.
If a needed reference/configuration is absent, STOP and issue a scoped CT GAP instead of extending this list.

## Evidence destination proposal (not created or granted here)

CT must separately authorize exact runtime report roots
`docs/records/audits/2026-09/mod-0187-dev-01/` and
`docs/records/audits/2026-09/mod-0187-ver-01/` in the activated prompts; evidence manifests enumerate actual files.
This PREP writes only the pack and its two user-owned prep directories. No runtime paths are touched.
All preexisting HELD prompts and evidence stay intact. Actual source/binary/contract hashes and fresh baseline are required
at activation; this review baseline cannot serve as an execution lock.

## Regression and exit evidence

Full existing SupplyChain tests (Shipment, Carrier and Loads) before/after; architecture guards with fresh results,
never turn historical14/4 or50/3 into a waiver. Capture any preexisting failure by exact test and compare unchanged inputs.
Shipment create→dispatch→delivery/POD golden flow, tenant/LE isolation, original-root event chain, duplicate/replay,
invalid headers/schema/error bodies, restart and local outbox must remain identical after composition. Carrier and Loads
published contract behavior and `/claimsXYZ` negative route probe required. No MOD-0183 ingress exercise or implementation.
Independent VER replays actual wire and inspects four collections, scope, rollback and saved receipts; E2+E3+E4 required.
Mock/local transport evidence is not E5 remote integration or finance acceptance. No G5/full capability claim.

Gate sequence: six exact owner decisions → compatible amendment + producer seam authorization → exact canonical
publication + mock uptake → Phase1.5/exception approvals → separate composition lease/scope → explicit pack promotion
and new DEV prompt → DEV evidence → independent VER → CT acceptance. Any missing authority keeps relevant gate BLOCKED.
